using System.Collections.Generic;
using MadMax.Npc;
using MadMax.Rendering;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>The player's market stall (roadmap 15): stock it with goods ([E] opens its crates) and passers-by buy one
    /// thing at a time at the local price, a little under what a trader would pay you... but without you there. Sells
    /// best in a town, well by a road, faster with people about; the takings wait in its cash box ([T]).
    /// 2026-10-27: the counter shows what is stocked (up to <see cref="MaxShown"/> goods), the cash box shows its coins,
    /// and a sale is a customer — someone idling within <see cref="BuyerReach"/> m walks over, looks the wares over and
    /// buys (the item leaves the counter, coins clink), then wanders on; with nobody about the sale happens unseen.
    /// Sales run on the authority (host / offline).</summary>
    public class PlayerStall : MonoBehaviour, IInteractable, IPlaceState
    {
        public const float BuyerReach = 25f, DisplayReach = 60f, LookSeconds = 3f;
        public const int MaxShown = 8;
        const float Counter = 11.5f * 0.08f;                                                    // top of the counter (voxel y 10..11)
        public int cash;
        Container goods;
        float timer = 45f;
        static readonly List<string> stock = new List<string>(), showing = new List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { stock.Clear(); showing.Clear(); }

        // what the counter shows
        readonly string[] slotIds = new string[MaxShown];
        readonly GameObject[] slots = new GameObject[MaxShown];
        readonly Transform[] coins = new Transform[3];
        Transform cashBox;
        Material mat;
        bool dirty = true, hd, looked;
        int cashLevel = -1;
        // the customer on the way
        Npc.Npc buyer;
        string pendingId;

        /// <summary>Goods drawn on the counter (tests).</summary>
        public int Shown { get; private set; }
        /// <summary>Coins shown in the cash box: 0 none, 1-3 by the takings (tests).</summary>
        public int CashShown => cashLevel + 1;
        /// <summary>The person walking over to buy (null: none) (tests).</summary>
        public Npc.Npc Buyer => buyer;
        /// <summary>Sales made, the last item, whether nobody came for it, how far the buyer stood (tests).</summary>
        public int Sales { get; private set; }
        public string LastSold { get; private set; }
        public bool LastSaleInstant { get; private set; }
        public float LastBuyerDistance { get; private set; }

        static bool Authority => !(MadMax.Net.NetSession.Instance && MadMax.Net.NetSession.Instance.IsClient);

        void Awake() => goods = GetComponent<Container>();

        void Start() { if (goods) goods.inventory.Changed += () => dirty = true; }

        void Update()
        {
            if (goods && PieceBits.Near(transform.position, DisplayReach)) Display();
            if (!goods || !Authority) return;
            if (pendingId != null)
            {
                if (!buyer || !buyer.Alive || !buyer.Browsing) Quit(buyer);                       // folded away, hurt, scared off
                return;
            }
            if ((timer -= Time.deltaTime) > 0f) return;
            Customer();
        }

        /// <summary>A customer comes along now (tests; the timer otherwise).</summary>
        public void SellNow() { if (pendingId == null) Customer(); }

        void Customer()
        {
            var p = transform.position;
            var terrain = MadMax.World.DeformableTerrain.Instance;
            var town = Market.Near(p);
            float roadDist = terrain && terrain.World != null ? terrain.World.Sample(p.x, p.z).roadDist : 999f;
            int people = 0;
            foreach (var n in Npc.Npc.All) if (n && n.Alive && (n.transform.position - p).sqrMagnitude < 30f * 30f) people++;
            // a customer every so often: towns and roads bring trade, people nearby bring more
            float rate = (town != null ? 1f : roadDist < 20f ? 0.5f : 0.15f) * (1f + people * 0.35f);
            timer = Random.Range(40f, 90f) / rate;
            Stock(stock, int.MaxValue);
            if (stock.Count == 0) return;
            string id = stock[Random.Range(0, stock.Count)];
            var b = FindBuyer(p);
            if (b)
            {
                buyer = b; pendingId = id;
                b.Browse(this, StandSpot(id), LookSeconds + Random.Range(0f, 2f), Land, Quit);
                return;
            }
            Sell(id, true);
        }

        /// <summary>The nearest person free to come over (resident or wanderer, idle, friendly).</summary>
        Npc.Npc FindBuyer(Vector3 p)
        {
            Npc.Npc best = null; float bd = BuyerReach * BuyerReach;
            foreach (var n in Npc.Npc.All)
            {
                if (!n || !n.MayBrowse) continue;
                var d = n.transform.position - p;
                if (Mathf.Abs(d.y) > 3f) continue;
                float s = d.x * d.x + d.z * d.z;
                if (s < bd) { bd = s; best = n; }
            }
            return best;
        }

        /// <summary>In front of the counter, before the item when it is on show.</summary>
        Vector3 StandSpot(string id)
        {
            float x = 0f;
            for (int i = 0; i < MaxShown; i++) if (slotIds[i] == id) { x = SlotPos(i).x; break; }
            return transform.TransformPoint(new Vector3(x, 0f, 1.15f));
        }

        void Land(Npc.Npc n)
        {
            if (n != buyer || pendingId == null) return;
            string id = pendingId;
            buyer = null; pendingId = null;
            var d = n.transform.position - transform.position; d.y = 0f;
            LastBuyerDistance = d.magnitude;
            Sell(id, false);
        }

        void Quit(Npc.Npc n)
        {
            if (n != buyer && buyer) return;
            buyer = null; pendingId = null;
            timer = Mathf.Min(timer, 8f);                                                         // someone else soon
        }

        bool Sell(string id, bool instant)
        {
            var p = transform.position;
            var town = Market.Near(p);
            int price = Mathf.Max(1, Mathf.RoundToInt(Trade.Value(id) * Market.Factor(town, id) * 0.8f));
            bool ok = id.StartsWith("res:") ? goods.inventory.TrySpend((MadMax.Items.ResourceType)int.Parse(id.Substring(4)), 1) : goods.inventory.TakeItem(id);
            if (!ok) return false;
            cash += price;
            Market.Sold(town, id, 1);
            Sales++; LastSold = id; LastSaleInstant = instant;
            GetComponent<Placeable>()?.Dirty();
            var g = MadMax.Game.WastelandGame.Instance;
            bool near = g && g.Player && (g.Player.transform.position - p).sqrMagnitude < 40f * 40f;
            if (!instant) MadMax.Audio.Sfx.Play("cash", transform.TransformPoint(new Vector3(0f, Counter, 0.4f)), 0.3f, 1.45f, 16f);   // coins on the counter
            else if (near) MadMax.Audio.Sfx.Play("cash", p, 0.4f);
            if (near) g.Toast("YOUR STALL SOLD " + Trade.Name(id) + " FOR " + price);
            return true;
        }

        /// <summary>What is on sale: items, then resources (not scrap), at most <paramref name="max"/>.</summary>
        void Stock(List<string> into, int max)
        {
            into.Clear();
            foreach (var kv in goods.inventory.Items) if (kv.Value > 0 && into.Count < max) into.Add(kv.Key);
            for (int t = 1; t < MadMax.Items.ResourceInfo.Count && into.Count < max; t++)
                if (t != (int)MadMax.Items.ResourceType.Scrap && goods.inventory.Get((MadMax.Items.ResourceType)t) > 0) into.Add("res:" + t);
        }

        public string Prompt(MadMax.Game.WastelandGame g) => "[E] STOCK THE STALL" + (cash > 0 ? "  [T] TAKE " + cash + " SCRAP" : "  (NO TAKINGS YET)");

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (!secondary) { if (goods) g.Menus.OpenContainer(goods); return; }
            if (cash <= 0) return;
            g.Inventory.Add(MadMax.Items.ResourceType.Scrap, cash);
            g.Toast("TOOK " + cash + " SCRAP FROM THE CASH BOX");
            MadMax.Audio.Sfx.Play2D("cash", 0.7f);
            cash = 0;
            GetComponent<Placeable>()?.Dirty();
        }

        public string SaveState() => cash.ToString();
        public void LoadState(string s) { if (!string.IsNullOrEmpty(s)) int.TryParse(s, out cash); }

        // ------------------------------------------------------------------ the counter and the cash box

        static Vector3 SlotPos(int i) => new Vector3(-0.84f + i * 0.24f, Counter, (i & 1) == 0 ? 0.42f : 0.48f);

        void Display()
        {
            if (!looked)
            {
                looked = true;
                hd = PieceBits.HD(gameObject);
                var mr = GetComponent<MeshRenderer>();
                mat = mr ? mr.sharedMaterial : null;
            }
            if (dirty) { dirty = false; Wares(); }
            int level = cash <= 0 ? -1 : cash < 25 ? 0 : cash < 120 ? 1 : 2;
            if (level != cashLevel || !cashBox) CashBox(level);
        }

        /// <summary>One item model per stocked good along the counter (only the slots that changed are rebuilt).</summary>
        void Wares()
        {
            Stock(showing, MaxShown);
            int n = 0;
            for (int i = 0; i < MaxShown; i++)
            {
                string id = i < showing.Count ? showing[i] : null;
                if (id != null) n++;
                if (slotIds[i] == id && (id == null || slots[i])) continue;
                if (slots[i]) Destroy(slots[i]);
                slots[i] = null; slotIds[i] = id;
                if (id == null) continue;
                var root = new GameObject("Ware").transform;
                root.gameObject.layer = gameObject.layer;
                root.SetParent(transform, false);
                root.localPosition = SlotPos(i);
                root.localRotation = Quaternion.Euler(0f, (i * 47) % 40 - 20f, 0f);
                MadMax.Items.WorldItemModels.AddVisual(root, id, mat, out var b);
                float k = Mathf.Min(1f, 0.21f / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z)), 0.3f / Mathf.Max(0.01f, b.size.y));
                root.localScale = Vector3.one * k;
                slots[i] = root.gameObject;
            }
            Shown = n;
        }

        /// <summary>The tin cash box on the right-hand crate, lid up; coins heaped in it by the takings.</summary>
        void CashBox(int level)
        {
            cashLevel = level;
            if (!cashBox)
            {
                cashBox = PieceBits.Shape(transform, "CashBox", new Vector3(0.48f, 6.5f * 0.08f, -0.12f), "stall_cashbox", hd, BoxVoxel, BoxHD, mat);
                cashBox.localScale = Vector3.one * 0.5f;
                for (int i = 0; i < 3; i++)
                {
                    int k = i;
                    coins[i] = PieceBits.Shape(cashBox, "Coins" + i, Vector3.zero, "stall_coins" + i, hd, g => CoinsVoxel(g, k), s => CoinsHD(s, k), mat);
                }
            }
            for (int i = 0; i < 3; i++) if (coins[i]) coins[i].gameObject.SetActive(i == level);
        }

        // cash box: 7 x 3 x 5 at half scale (4 cm voxels), open on top, the lid standing up at the back
        static void BoxVoxel(VoxelGrid g)
        {
            g.Mat((byte)MadMax.Items.ResourceType.Scrap);
            g.Box(-3, 0, -2, 3, 2, 2, Pal.Ramp(Pal.Navy, 2, 2711));
            g.ClearBox(-2, 1, -1, 2, 2, 1);
            g.Box(-3, 3, -3, 3, 6, -3, Pal.Ramp(Pal.Navy, 3, 2712));                                 // lid
            g.Set(0, 5, -2, Pal.Ramp(Pal.Chrome, 2)); g.Set(0, 1, 3, Pal.Ramp(Pal.Bronze, 3));        // lid catch, lock
        }

        static void BoxHD(HDShapes s)
        {
            const float v = 0.08f;
            s.Box(new Vector3(0f, 0f, 0f), new Vector3(7f * v, v, 5f * v), Pal.Navy[2], 0.01f);
            s.Box(new Vector3(-3f * v, 1.5f * v, 0f), new Vector3(v, 2f * v, 5f * v), Pal.Navy[2], 0.01f);
            s.Box(new Vector3(3f * v, 1.5f * v, 0f), new Vector3(v, 2f * v, 5f * v), Pal.Navy[2], 0.01f);
            s.Box(new Vector3(0f, 1.5f * v, -2f * v), new Vector3(7f * v, 2f * v, v), Pal.Navy[2], 0.01f);
            s.Box(new Vector3(0f, 1.5f * v, 2f * v), new Vector3(7f * v, 2f * v, v), Pal.Navy[2], 0.01f);
            s.Box(new Vector3(0f, 4.5f * v, -2.8f * v), new Vector3(7f * v, 4f * v, 0.6f * v), Pal.Navy[3], 0.01f, Quaternion.Euler(-12f, 0f, 0f));
            s.Box(new Vector3(0f, 1.2f * v, 2.6f * v), new Vector3(0.8f * v, 0.8f * v, 0.3f * v), Pal.Bronze[3], 0.005f);
        }

        static Color32 Coin(int h) => (h % 5) switch { 0 => Pal.Chrome[2], 1 => Pal.Rust[3], 2 => Pal.Chrome[3], _ => Pal.Bronze[3] };

        /// <summary>A few coins / a floor of coins and scrap / heaped over the rim.</summary>
        static void CoinsVoxel(VoxelGrid g, int level)
        {
            g.Mat((byte)MadMax.Items.ResourceType.Scrap);
            for (int x = -2; x <= 2; x++)
                for (int z = -1; z <= 1; z++)
                {
                    int h = (x * 7 + z * 13 + 40) * 31 % 97;
                    int top = level == 0 ? (h % 3 == 0 ? 1 : 0) : level == 1 ? 1 + (h % 4 == 0 ? 1 : 0) : 2 + (Mathf.Abs(x) + Mathf.Abs(z) <= 1 ? 1 : 0) + (h % 3 == 0 ? 1 : 0);
                    for (int y = 1; y <= top; y++) g.Set(x, y, z, Pal.Solid(Coin(h + y * 3)));
                }
        }

        static void CoinsHD(HDShapes s, int level)
        {
            const float v = 0.08f;
            int n = level == 0 ? 5 : level == 1 ? 16 : 34;
            for (int i = 0; i < n; i++)
            {
                int h = (i * 7919 + level * 131) % 9973;
                float x = ((h % 41) / 40f - 0.5f) * 4.4f * v, z = ((h / 41 % 29) / 28f - 0.5f) * 2.4f * v;
                float dome = level == 2 ? (1f - Mathf.Clamp01((x * x / (2.4f * v * 2.4f * v)) + (z * z / (1.4f * v * 1.4f * v)))) * 1.6f * v : 0f;
                float y = 0.55f * v + (i * 0.37f % (level + 1)) * 0.18f * v + dome;
                s.Ellipsoid(new Vector3(x, y, z), new Vector3(0.45f * v, 0.08f * v, 0.45f * v), Coin(h), 8, 3, false, Quaternion.Euler((h % 30) - 15f, 0f, (h % 23) - 11f));
            }
        }
    }
}
