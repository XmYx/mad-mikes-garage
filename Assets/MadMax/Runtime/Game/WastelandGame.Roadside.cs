using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.RPG;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Scheduled update 2026-10-07: burned-out wrecks as map landmarks (nobody strips them for a week), car
    /// breakers walking off with parts that turn up on the stalls (<c>Trade.Stolen</c>), spare keys cut at a workbench
    /// and keys ordered from a town mechanic, companions dressing the player's wounds, and binoculars that find a
    /// vague giver's camp inside the search circle.</summary>
    public partial class WastelandGame
    {
        // ------------------------------------------------------------------ burned-out wrecks

        public struct BurnedOut { public Vector3 pos; public float day; public string name, whose; public bool told; }
        /// <summary>Vehicles that burned out on the map: a landmark ("BURNED-OUT COUPE") and left alone by scavengers
        /// for <see cref="BurnedDays"/>. Saved as <c>SaveData.burnedOut</c>.</summary>
        public readonly List<BurnedOut> BurnedOuts = new List<BurnedOut>();
        public const float BurnedDays = 7f;

        /// <summary>A vehicle burned out (<c>VehicleBurn.Char</c>, live): it marks the map where it stands.</summary>
        public void NoteBurnedOut(VehicleDriver v)
        {
            if (!v) return;
            var p = v.transform.position;
            for (int i = BurnedOuts.Count - 1; i >= 0; i--) if (Flat(BurnedOuts[i].pos - p) < 6f) BurnedOuts.RemoveAt(i);
            string n = "BURNED-OUT " + Name(v);
            string whose = BurnedWhose(v, out bool raider, out string gang);
            BurnedOuts.Add(new BurnedOut { pos = p, day = DayNight.TotalDays, name = n, whose = whose });
            BurnedReaction(v, p, whose, raider, gang);
            if (BurnedOuts.Count > 16) BurnedOuts.RemoveAt(0);
            Journal.Add("ROAD", "A " + n + " SMOULDERS AT " + Mathf.RoundToInt(p.x) + "," + Mathf.RoundToInt(p.z));
        }

        /// <summary>A burned-out wreck younger than <see cref="BurnedDays"/> within 12 m: nobody wants to pick at it.</summary>
        public bool BurnedNear(Vector3 at)
        {
            foreach (var b in BurnedOuts) if (Flat(b.pos - at) < 12f && DayNight.TotalDays - b.day < BurnedDays) return true;
            return false;
        }

        void BurnedPins(List<Pin> into)
        {
            var col = new Color32(120, 110, 100, 255);
            foreach (var b in BurnedOuts) if (DayNight.TotalDays - b.day < BurnedDays) into.Add(new Pin { label = b.name, pos = b.pos, color = col });
        }

        // ------------------------------------------------------------------ car breakers take parts

        /// <summary>A car breaker unbolts a part off one of the player's vehicles (not the engine) and carries it off:
        /// it reaches the nearest town's salvage stalls tomorrow, where the player buys it back for half.</summary>
        public bool BreakerSteals(VehicleDriver v, Vector3 at, string gang = null)
        {
            if (!v || !InFleet(v) || !v.TryGetComponent<VehicleChassis>(out var chassis)) return false;
            var picks = new List<MountSocket>();
            foreach (var s in chassis.Sockets)
                if (s.Current && s.Current.category != PartCategory.Engine && s.Current.category != PartCategory.Armor) picks.Add(s);
            if (picks.Count == 0) return false;
            var sock = picks[Random.Range(0, picks.Count)];
            var part = sock.Detach(false);
            if (!part) return false;
            string id = "part:" + part.partId;
            Destroy(part.gameObject);
            var town = NearestTown(at);
            MadMax.Npc.Trade.AddStolen(town, id, at, gang);
            string what = ItemName(id), car = Name(v);
            Toast((string.IsNullOrEmpty(gang) ? "RAIDERS" : "THE " + gang) + " MADE OFF WITH THE " + what + " OF YOUR " + car);
            Journal.Add("RAID", "RAIDERS TOOK THE " + what + " OFF YOUR " + car + (town != null ? " - IT MAY TURN UP AT " + MadMax.Npc.Market.TownName(town) + " MARKET" : ""));
            LastStolen = id;
            return true;
        }

        /// <summary>The last part car breakers took (tests).</summary>
        public static string LastStolen;

        static string ItemName(string id) => id.StartsWith("part:") ? id.Substring(5).Replace('_', ' ').ToUpperInvariant() : ItemIds.Name(id);

        public Settlement NearestTown(Vector3 at)
        {
            Settlement town = null; float td = float.MaxValue;
            if (World != null) foreach (var st in World.settlements) { float d = Vector2.Distance(st.pos, new Vector2(at.x, at.z)); if (d < td) { td = d; town = st; } }
            return town;
        }

        // ------------------------------------------------------------------ keys

        public const string KeyBlank = "misc_key_blank";
        public const int LocksmithScrap = 40;

        /// <summary>A car key and a blank at a workbench (within 4 m): a spare cut from it.</summary>
        public bool CutSpareKey(string key)
        {
            if (!ItemIds.IsCarKey(key) || Inventory.GetItem(key) <= 0) return false;
            CraftingStation bench = null;
            foreach (var st in CraftingStation.All) if (st && st.type == "workbench" && Flat(st.transform.position - Player.transform.position) < 4f) { bench = st; break; }
            if (!bench) { Toast("CUT A SPARE AT A WORKBENCH (WITH A KEY BLANK)"); return false; }
            if (!Inventory.TakeItem(KeyBlank)) { Toast("NEED A KEY BLANK TO CUT A SPARE"); return false; }
            Inventory.AddItem(key);
            Stats.Practice(Skill.Mechanics, 1.5f);
            MadMax.Audio.Sfx.Play("scratch", bench.transform.position, 0.6f, 1.4f);
            Toast("CUT A SPARE " + ItemIds.Name(key));
            return true;
        }

        public struct KeyOrder { public string key, smith, car; public int ready; }
        /// <summary>Keys ordered from town mechanics for vehicles whose key is lost: collected from the same person from
        /// <see cref="KeyOrder.ready"/> on. Saved as <c>SaveData.keyOrders</c>.</summary>
        public readonly List<KeyOrder> KeyOrders = new List<KeyOrder>();

        /// <summary>A vehicle within 40 m of <paramref name="at"/> whose key nobody here has (lost, or carried off),
        /// that no order is out for: what a mechanic could cut a key for.</summary>
        public VehicleIgnition KeylessNear(Vector3 at)
        {
            VehicleIgnition best = null; float bd = 40f;
            foreach (var v in AllVehicles)
            {
                if (!v || v.aiDriven || !v.TryGetComponent<VehicleIgnition>(out var ign) || !ign.NeedsKey || ign.hotwired) continue;
                if (ign.key == VehicleIgnition.Where.Ignition || ign.key == VehicleIgnition.Where.Glovebox || ign.Has(Inventory)) continue;
                string k = ign.KeyItem;
                if (KeyOrders.Exists(o => o.key == k)) continue;
                float d = Flat(v.transform.position - at);
                if (d < bd) { bd = d; best = ign; }
            }
            return best;
        }

        public bool OrderKey(VehicleIgnition ign, string smith)
        {
            if (!ign || !Inventory.TrySpend(ResourceType.Scrap, LocksmithScrap)) return false;
            KeyOrders.Add(new KeyOrder { key = ign.KeyItem, smith = smith, car = Name(ign), ready = DayNight.Day + 1 });
            Journal.Add("KEYS", "A KEY FOR THE " + Name(ign) + " IS BEING CUT - READY TOMORROW");
            return true;
        }

        /// <summary>Hand over the finished keys this smith has for the player; returns how many.</summary>
        public int CollectKeys(string smith)
        {
            int n = 0;
            for (int i = KeyOrders.Count - 1; i >= 0; i--)
            {
                var o = KeyOrders[i];
                if (o.smith != smith || o.ready > DayNight.Day) continue;
                Inventory.AddItem(o.key);
                KeyOrders.RemoveAt(i);
                n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ companion medic

        /// <summary>Something a companion could treat: a bleeding wound or a broken bone without a splint.</summary>
        public bool NeedsTending
        {
            get
            {
                if (Stats == null || Stats.injuries == null) return false;
                foreach (var i in Stats.injuries) if (i.Bleeding || (i.type == Wound.Fracture && !i.splinted)) return true;
                return false;
            }
        }

        /// <summary>A companion treats the player: a first-aid kit (from <paramref name="from"/> or their pack) splints
        /// and dresses everything; otherwise their bandages and splint go on the worst wounds. Returns wounds treated.</summary>
        public int CompanionTreat(MadMax.Npc.Npc medic, Container from)
        {
            if (!medic) return 0;
            var pack = medic.pack ? medic.pack.inventory : null;
            bool rough = !MadMax.Npc.Companions.Patches(medic.Profile);
            Inventory kitFrom = from && from.inventory.GetItem(MadMax.Npc.Companions.FirstAidKit) > 0 ? from.inventory
                              : pack != null && pack.GetItem(MadMax.Npc.Companions.FirstAidKit) > 0 ? pack : null;
            int n = 0;
            if (kitFrom != null)
            {
                foreach (var inj in Stats.injuries)
                {
                    bool did = false;
                    if (inj.type == Wound.Fracture && !inj.splinted) { inj.splinted = true; inj.rough = rough; did = true; }
                    if (MedOpenWound(inj))
                    {
                        if (!inj.disinfected) { inj.disinfected = true; inj.infection = Mathf.Max(0f, inj.infection - (rough ? 0.4f : 0.6f)); did = true; }
                        if (!inj.bandaged || inj.BandageDirty) { inj.bandaged = true; inj.bandageAge = 0f; inj.rough = rough; did = true; }
                    }
                    if (did) n++;
                }
                if (n > 0) kitFrom.TakeItem(MadMax.Npc.Companions.FirstAidKit);
            }
            else if (pack != null)
            {
                foreach (var inj in Stats.injuries)
                {
                    if (inj.type == Wound.Fracture && !inj.splinted && pack.TakeItem("med_splint")) { inj.splinted = true; inj.rough = rough; n++; }
                    else if (inj.Bleeding && pack.TakeItem("med_bandage")) { inj.bandaged = true; inj.bandageAge = 0f; inj.rough = rough; n++; }
                }
            }
            if (n == 0) { Toast(medic.KnownName + " HAS NOTHING TO TREAT YOU WITH"); return 0; }
            MadMax.Audio.Sfx.Play2D("scratch", 0.4f, 1.1f);
            Toast(medic.KnownName + (rough ? " PATCHED YOU UP, ROUGHLY (" : " PATCHED YOU UP (") + n + " WOUND" + (n == 1 ? "" : "S") + ")");
            if (rough) Hints.Show("rough_dressing", "A COMPANION'S DRESSING SOILS SOONER AND THEIR SPLINT HOLDS WORSE: REDO IT YOURSELF (O) WHEN YOU CAN, OR TRAVEL WITH SOMEONE WHO KNOWS WOUNDS");
            Journal.Add("HEALTH", medic.KnownName + " DRESSED YOUR WOUNDS");
            if (rough && MadMax.Npc.Companions.Practise(medic.Profile))
            {
                Toast(medic.KnownName + " HAS GOT THE KNACK OF IT: THEIR DRESSINGS HOLD NOW");
                Journal.Add("HEALTH", medic.KnownName + " LEARNED TO DRESS WOUNDS PROPERLY");
            }
            return n;
        }

        // ------------------------------------------------------------------ binoculars find a vague giver

        public const float SpotReach = 260f;

        /// <summary>Looking through binoculars from inside a vague giver's search circle, towards their camp and within
        /// <see cref="SpotReach"/> (further from higher ground): the camp is spotted and pinned.</summary>
        void UpdateBinocularSpotting()
        {
            if (!BinocularsTool.Looking || Current || !Player || HeardVague.Count == 0 || World == null) return;
            var me = Player.transform.position;
            var cam = Camera.main;
            foreach (var q in MadMax.Story.StoryLibrary.All)
            {
                if (q.giver == null || MadMax.Story.Story.StateOf(q.id) != MadMax.Story.Story.State.Open) continue;
                var m = MadMax.Story.StoryCast.Find(q.giver);
                if (m == null || !HeardVague.Contains(m.Value.key) || m.Value.anchor == null || !MadMax.Story.StoryAnchors.Has(m.Value.anchor)) continue;
                if (SpotCamp(m.Value.key, MadMax.Story.StoryAnchors.Get(m.Value.anchor), me, cam)) { Toast("SPOTTED THROUGH THE BINOCULARS: THE " + m.Value.title + "'S CAMP (ON THE MAP)"); Journal.Add("FOUND", "SPOTTED THE " + m.Value.title + "'S CAMP"); }
            }
        }

        /// <summary>The spotting check for one vague giver at <paramref name="real"/>; true when it is now pinned.</summary>
        public bool SpotCamp(string key, Vector3 real, Vector3 me, Camera cam)
        {
            float r = VagueRadiusOf(key);
            if (Flat(me - VagueCentre(key, real, r)) > r + 40f) return false;                      // search from inside the circle
            if (!InSight(me, real, cam)) return false;
            HeardVague.Remove(key); VagueAsks.Remove(key);
            HeardOf.Add(key);
            return true;
        }

        void UpdateRoadside()
        {
            UpdateBinocularSpotting();
            UpdateBurnedStories();
            UpdateBinocularWrecks();
            for (int i = BurnedOuts.Count - 1; i >= 0; i--) if (DayNight.TotalDays - BurnedOuts[i].day > BurnedDays * 2f) BurnedOuts.RemoveAt(i);
        }

        // ------------------------------------------------------------------ save

        static readonly System.Globalization.CultureInfo RoadInv = System.Globalization.CultureInfo.InvariantCulture;

        void SaveRoadside(SaveData d)
        {
            d.burnedOut = new List<string>();
            foreach (var b in BurnedOuts) d.burnedOut.Add(b.pos.x.ToString("0.#", RoadInv) + "," + b.pos.y.ToString("0.#", RoadInv) + "," + b.pos.z.ToString("0.#", RoadInv) + "," + b.day.ToString("0.###", RoadInv) + "|" + b.name + "|" + b.whose);
            d.keyOrders = new List<string>();
            foreach (var o in KeyOrders) d.keyOrders.Add(o.key + "|" + o.smith + "|" + o.ready + "|" + o.car);
        }

        void LoadRoadside(SaveData d)
        {
            BurnedOuts.Clear(); KeyOrders.Clear();
            if (d.burnedOut != null)
                foreach (var line in d.burnedOut)
                {
                    int bar = line.IndexOf('|');
                    var f = (bar < 0 ? line : line.Substring(0, bar)).Split(',');
                    if (f.Length < 4) continue;
                    if (float.TryParse(f[0], System.Globalization.NumberStyles.Float, RoadInv, out float x) && float.TryParse(f[1], System.Globalization.NumberStyles.Float, RoadInv, out float y)
                        && float.TryParse(f[2], System.Globalization.NumberStyles.Float, RoadInv, out float z) && float.TryParse(f[3], System.Globalization.NumberStyles.Float, RoadInv, out float day))
                    {
                        var rest = bar < 0 ? null : line.Substring(bar + 1).Split('|');
                        BurnedOuts.Add(new BurnedOut { pos = new Vector3(x, y, z), day = day, name = rest == null ? "BURNED-OUT WRECK" : rest[0], whose = rest != null && rest.Length > 1 ? rest[1] : null });
                    }
                }
            if (d.keyOrders != null)
                foreach (var line in d.keyOrders)
                {
                    var f = line.Split('|');
                    if (f.Length >= 3 && int.TryParse(f[2], out int ready)) KeyOrders.Add(new KeyOrder { key = f[0], smith = f[1], ready = ready, car = f.Length > 3 ? f[3] : "" });
                }
        }
    }
}
