using System.Collections.Generic;
using MadMax.Npc;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>The player's market stall (roadmap 15): stock it with goods ([E] opens its crates) and passers-by buy one
    /// thing at a time at the local price, a little under what a trader would pay you... but without you there. Sells
    /// best in a town, well by a road, faster with people about; the takings wait in its cash box ([T]).</summary>
    public class PlayerStall : MonoBehaviour, IInteractable, IPlaceState
    {
        public int cash;
        Container goods;
        float timer = 45f;
        static readonly List<string> stock = new List<string>();

        void Awake() => goods = GetComponent<Container>();

        void Update()
        {
            if (!goods || (timer -= Time.deltaTime) > 0f) return;
            var p = transform.position;
            var terrain = MadMax.World.DeformableTerrain.Instance;
            var town = Market.Near(p);
            float roadDist = terrain && terrain.World != null ? terrain.World.Sample(p.x, p.z).roadDist : 999f;
            int people = 0;
            foreach (var n in Npc.Npc.All) if (n && n.Alive && (n.transform.position - p).sqrMagnitude < 30f * 30f) people++;
            // a customer every so often: towns and roads bring trade, people nearby bring more
            float rate = (town != null ? 1f : roadDist < 20f ? 0.5f : 0.15f) * (1f + people * 0.35f);
            timer = Random.Range(40f, 90f) / rate;
            stock.Clear();
            foreach (var kv in goods.inventory.Items) if (kv.Value > 0) stock.Add(kv.Key);
            for (int t = 1; t < MadMax.Items.ResourceInfo.Count; t++) if (goods.inventory.Get((MadMax.Items.ResourceType)t) > 0 && t != (int)MadMax.Items.ResourceType.Scrap) stock.Add("res:" + t);
            if (stock.Count == 0) return;
            string id = stock[Random.Range(0, stock.Count)];
            int price = Mathf.Max(1, Mathf.RoundToInt(Trade.Value(id) * Market.Factor(town, id) * 0.8f));
            bool ok = id.StartsWith("res:") ? goods.inventory.TrySpend((MadMax.Items.ResourceType)int.Parse(id.Substring(4)), 1) : goods.inventory.TakeItem(id);
            if (!ok) return;
            cash += price;
            Market.Sold(town, id, 1);
            GetComponent<Placeable>()?.Dirty();
            var g = MadMax.Game.WastelandGame.Instance;
            if (g && g.Player && (g.Player.transform.position - p).sqrMagnitude < 40f * 40f) { g.Toast("YOUR STALL SOLD " + Trade.Name(id) + " FOR " + price); MadMax.Audio.Sfx.Play("cash", p, 0.4f); }
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
    }
}
