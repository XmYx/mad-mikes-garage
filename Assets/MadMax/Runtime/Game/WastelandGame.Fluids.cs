using System.Collections.Generic;
using System.Globalization;
using MadMax.Building;
using MadMax.Items;
using MadMax.RPG;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game
{
    /// <summary>Fluids block: hand liquid containers (<see cref="FluidContainers"/>) and what the one in hand does.
    /// <para>Every container in the pack has its own contents (<see cref="CanContents"/>: litres + a
    /// <see cref="FluidMix"/>); the first of an id is the one in hand. Containers leaving the pack (dropped, stored,
    /// sold) leave their contents in a per-id pool that the next one of that id coming in takes back, so no litre is made
    /// or lost.</para>
    /// <para>With a container in hand, K opens a radial of what to siphon from (a vehicle's fuel tank, sump or cooling
    /// system, a tanker, a pump, a generator, open water, the pack's liquids) — only sources whose liquid fits the
    /// container's (or anything into an empty one) — and fills it up to its capacity with that single fluid; G opens what
    /// to pour into (the same tanks, a player-built pump, the pack for a pure liquid, the ground) with the blend the tank
    /// will end up with. At a vehicle both are timed work (WastelandGame.Anim: the filler or the engine bay, the
    /// container in hand). Without one in hand, K / G at a vehicle equip the best container in the pack and open the choice
    /// (<see cref="UseCanAt"/>); with none, nothing moves: no liquid goes in or out of the pack without a container.</para></summary>
    public partial class WastelandGame
    {
        readonly Dictionary<string, List<CanContents>> cans = new Dictionary<string, List<CanContents>>();
        readonly Dictionary<string, List<CanContents>> canPool = new Dictionary<string, List<CanContents>>();
        Inventory canInventory;
        int fluidClickFrame = -1, fluidKeyFrame = -1;
        bool fluidChoice, fluidChoiceSiphon;
        Vector3 fluidChoiceAt;
        Vector2 fluidAim;

        /// <summary>Text under the action radial while it is a container's choice (null = the Tab wheel's).</summary>
        public string RadialHint { get; private set; }
        /// <summary>The siphon / pour radial is open (automation: <see cref="PickRadial"/>).</summary>
        public bool FluidChoiceOpen => fluidChoice && RadialOpen;
        /// <summary>Last transfer: litres moved and what happened (automation, HUD toast).</summary>
        public float LastTransferLitres { get; private set; }
        public string LastTransferNote { get; private set; }

        // ------------------------------------------------------------------ containers in the pack

        /// <summary>The contents of every container of <paramref name="id"/> in the pack (index 0 = the one in hand),
        /// kept as long as the pack's count.</summary>
        public List<CanContents> CansOf(string id)
        {
            if (!cans.TryGetValue(id, out var l)) cans[id] = l = new List<CanContents>();
            int n = Inventory != null ? Inventory.GetItem(id) : 0;
            while (l.Count < n) l.Add(FromPool(id));
            while (l.Count > n) { var c = l[l.Count - 1]; l.RemoveAt(l.Count - 1); ToPool(id, c); }
            return l;
        }

        CanContents FromPool(string id)
        {
            if (canPool.TryGetValue(id, out var p) && p.Count > 0) { var c = p[p.Count - 1]; p.RemoveAt(p.Count - 1); return c; }
            return new CanContents();
        }

        void ToPool(string id, CanContents c)
        {
            if (c == null || c.Empty) return;
            if (!canPool.TryGetValue(id, out var p)) canPool[id] = p = new List<CanContents>();
            p.Add(c);
        }

        void OnCanDelta(string id, ResourceType t, int n)
        {
            if (id == null || !FluidContainers.Is(id)) return;
            if (!cans.TryGetValue(id, out var l)) cans[id] = l = new List<CanContents>();
            for (int i = 0; i < n; i++) l.Add(FromPool(id));
            for (int i = 0; i < -n && l.Count > 0; i++) { var c = l[l.Count - 1]; l.RemoveAt(l.Count - 1); ToPool(id, c); }
        }

        /// <summary>The container in hand and its contents (null when the hand holds something else).</summary>
        public CanContents HeldCan
        {
            get
            {
                if (!Player || !(Player.Tool is FluidCanTool t)) return null;
                var l = CansOf(t.id);
                return l.Count > 0 ? l[0] : null;
            }
        }
        public FluidContainers.Def HeldCanDef => Player && Player.Tool is FluidCanTool t ? FluidContainers.Get(t.id) : null;
        /// <summary>HUD line for the container in hand: "JERRY CAN 20L 12/20 L DIESEL".</summary>
        public string HeldCanText { get { var c = HeldCan; var d = HeldCanDef; return c != null && d != null ? d.name.Replace(" " + d.litres.ToString("0", CultureInfo.InvariantCulture) + "L", "") + " " + c.Describe(d.litres) : null; } }

        /// <summary>Inventory label suffix for a container id: what the first one holds.</summary>
        public string CanNote(string id)
        {
            var d = FluidContainers.Get(id);
            if (d == null) return "";
            var l = CansOf(id);
            if (l.Count == 0) return "";
            int full = 0; foreach (var c in l) if (!c.Empty) full++;
            return " [" + l[0].Describe(d.litres) + (l.Count > 1 ? ", " + full + "/" + l.Count + " IN USE" : "") + "]";
        }

        float CansWeight(Inventory inv)
        {
            if (inv != Inventory) return 0f;
            float kg = 0f;
            foreach (var kv in cans) foreach (var c in kv.Value) if (!c.Empty) kg += c.litres * FluidContainers.Density(c.mix);
            return kg;
        }

        partial void FluidsUpdate()
        {
            if (Inventory != null && canInventory != Inventory)
            {
                if (canInventory != null) canInventory.Delta -= OnCanDelta;
                canInventory = Inventory;
                canInventory.Delta += OnCanDelta;
                foreach (var d in FluidContainers.All) CansOf(d.id);
            }
            if (ItemCatalog.ContentsWeight == null) ItemCatalog.ContentsWeight = inv => Instance ? Instance.CansWeight(inv) : 0f;
            if (!ContextActions.Has("pack.fluids")) ContextActions.Register("pack.fluids", (g, t, into) => { if (t.item != null) g.CanPackOptions(t.item, into); });
            if (!Spills.Instance && DeformableTerrain.Instance) Spills.Ensure();
            UpdatePour(Time.deltaTime);
        }

        partial void FluidsNewGame()
        {
            cans.Clear(); canPool.Clear();
            Spills.Clear();
            Inventory.AddItem(FluidContainers.FuelCan);                                              // an empty 10 L fuel can in every starting kit
        }

        /// <summary>Hand over a new container of <paramref name="id"/> holding <paramref name="litres"/> of one liquid (rewards, gifts).</summary>
        public void GiveFilledCan(string id, ResourceType t, float litres)
        {
            var def = FluidContainers.Get(id);
            if (def == null) return;
            Inventory.AddItem(id);
            var l = CansOf(id);
            CanContents c = null;
            for (int i = l.Count - 1; i >= 0 && c == null; i--) if (l[i].Empty) c = l[i];
            if (c == null) return;
            c.litres = Mathf.Min(litres, def.litres);
            c.mix.Set(t);
        }

        /// <summary>Starting kit: the fuel can (added by FluidsNewGame) filled with <paramref name="litres"/> of one liquid.</summary>
        void FillStarterCan(ResourceType t, float litres)
        {
            if (Inventory.GetItem(FluidContainers.FuelCan) <= 0) Inventory.AddItem(FluidContainers.FuelCan);
            var l = CansOf(FluidContainers.FuelCan);
            if (l.Count == 0) return;
            l[0].litres = Mathf.Min(litres, FluidContainers.Get(FluidContainers.FuelCan).litres);
            l[0].mix.Set(t);
        }

        partial void FluidsSave(SaveData d)
        {
            d.blockFluids = new List<string>();
            foreach (var kv in cans)
                foreach (var c in CansOf(kv.Key)) d.blockFluids.Add("c\u001f" + kv.Key + "\u001f" + c.litres.ToString("0.###", CultureInfo.InvariantCulture) + "\u001f" + c.mix.Save());
            foreach (var kv in canPool)
                foreach (var c in kv.Value) if (!c.Empty) d.blockFluids.Add("p\u001f" + kv.Key + "\u001f" + c.litres.ToString("0.###", CultureInfo.InvariantCulture) + "\u001f" + c.mix.Save());
            Spills.Save(d.blockFluids, "s\u001f");                                                   // liquids on the ground
        }

        partial void FluidsLoad(SaveData d)
        {
            cans.Clear(); canPool.Clear();
            Spills.Clear();
            if (d.blockFluids == null) return;
            foreach (var line in d.blockFluids)
            {
                if (line.StartsWith("s\u001f")) { Spills.Load(line.Substring(2)); continue; }
                var a = line.Split('\u001f');
                if (a.Length < 4 || !FluidContainers.Is(a[1])) continue;
                var c = new CanContents();
                float.TryParse(a[2], NumberStyles.Float, CultureInfo.InvariantCulture, out c.litres);
                c.mix.Load(a[3]);
                if (c.mix.Empty) c.litres = 0f;
                var dict = a[0] == "p" ? canPool : cans;
                if (!dict.TryGetValue(a[1], out var l)) dict[a[1]] = l = new List<CanContents>();
                l.Add(c);
            }
            foreach (var def in FluidContainers.All) CansOf(def.id);                                   // trims / pads to the pack's counts
        }

        /// <summary>Put a container of <paramref name="id"/> in hand (and the one at <paramref name="index"/> first).</summary>
        public bool EquipCan(string id, int index = 0)
        {
            if (Current || !Player || Inventory.GetItem(id) <= 0 || !FluidContainers.Is(id)) return false;
            var l = CansOf(id);
            if (index > 0 && index < l.Count) { var c = l[index]; l[index] = l[0]; l[0] = c; }
            if (!(Player.Tool && Player.Tool.id == id)) Player.Equip(ToolLibrary.Create(id, propMaterial));
            return Player.Tool && Player.Tool.id == id;
        }

        /// <summary>The pack holds at least one liquid container.</summary>
        public bool HasContainer { get { foreach (var d in FluidContainers.All) if (Inventory.GetItem(d.id) > 0) return true; return false; } }

        /// <summary>K / G at a vehicle with no container in hand. Liquids only ever move through a container: equip the
        /// best one in the pack and open its choice (K: one with room, an empty one first; G: one holding liquid, the
        /// vehicle's own fuel first — with only empty ones, the fill choice opens so the pack's liquids can go in first).
        /// False with a toast when the pack has no container that can do it.</summary>
        public bool UseCanAt(VehicleDriver v, bool siphon, FluidFamily prefer = FluidFamily.Fuel)
        {
            if (Working) return false;
            var sys = v ? v.GetComponent<VehicleSystems>() : null;
            var fuelFamily = sys ? FluidMix.Family(sys.FuelKind) : prefer;
            string bestId = null; int bestIndex = 0; float bestScore = -1f;
            string roomId = null; int roomIndex = 0; float roomScore = -1f;
            bool any = false;
            foreach (var d in FluidContainers.All)
            {
                var l = CansOf(d.id);
                for (int i = 0; i < l.Count; i++)
                {
                    any = true;
                    float room = d.litres - l[i].litres;
                    float rs = room + (l[i].Empty ? 100f : 0f) + (d.meant == prefer ? 10f : 0f) + (!l[i].Empty && l[i].mix.MainFamily == prefer ? 50f : 0f);
                    if (room > 0.05f && rs > roomScore) { roomScore = rs; roomId = d.id; roomIndex = i; }
                    if (l[i].Empty) continue;
                    float ps = l[i].litres + (l[i].mix.MainFamily == fuelFamily ? 1000f : 0f);
                    if (ps > bestScore) { bestScore = ps; bestId = d.id; bestIndex = i; }
                }
            }
            if (!any) { Toast("NO CONTAINER: LIQUIDS GO IN A CAN, BOTTLE, BUCKET OR JUG"); return false; }
            if (!siphon && bestId != null)
            {
                if (!EquipCan(bestId, bestIndex)) return false;
                OpenFluidChoice(false);
                return true;
            }
            if (roomId == null) { Toast("EVERY CONTAINER YOU CARRY IS FULL"); return false; }
            if (!EquipCan(roomId, roomIndex)) return false;
            if (!siphon) Toast("YOUR CONTAINERS ARE EMPTY: FILL ONE FIRST");
            OpenFluidChoice(true);
            return true;
        }

        // ------------------------------------------------------------------ what a container can draw from / pour into

        /// <summary>One end of a transfer: a tank, a sump, a pump, open water, the pack, the ground.</summary>
        abstract class FluidEnd
        {
            public string name;
            public VehicleDriver vehicle;
            public VehicleSystems.FluidSystem system;
            public abstract float Available { get; }
            public abstract float Room { get; }
            /// <summary>What comes out (siphon preview, compatibility).</summary>
            public abstract FluidMix Mix { get; }
            public abstract float Draw(float litres, FluidMix into);
            public abstract float Pour(FluidMix mix, float litres);
            /// <summary>Why a pour is refused (null = fine).</summary>
            public virtual string Refuse(FluidMix mix) => null;
            /// <summary>The blend after pouring <paramref name="litres"/> of <paramref name="mix"/> in (preview).</summary>
            public virtual FluidMix After(FluidMix mix, float litres) => null;
        }

        sealed class TankEnd : FluidEnd
        {
            public VehicleSystems sys;
            public override float Available => sys.Level(system);
            public override float Room => Mathf.Max(0f, sys.Capacity(system) - sys.Level(system));
            public override FluidMix Mix => sys.EffectiveMix(system);
            public override float Draw(float litres, FluidMix into) => sys.Draw(system, litres, into);
            public override float Pour(FluidMix mix, float litres) => sys.Pour(system, mix, litres);
            public override FluidMix After(FluidMix mix, float litres)
            {
                var m = sys.Level(system) < 0.05f ? new FluidMix() : Mix.Clone();
                m.Blend(sys.Level(system) < 0.05f ? 0f : sys.Level(system), mix, Mathf.Min(litres, Room));
                return m;
            }
        }

        sealed class PumpEnd : FluidEnd
        {
            public GasPump pump;
            public override float Available => pump.Fuel;
            public override float Room => pump.key == null ? Mathf.Max(0f, pump.capacity - pump.stock) : 0f;
            public override FluidMix Mix => new FluidMix(pump.kind);
            public override float Draw(float litres, FluidMix into) { into.Set(pump.kind); return pump.Take(litres); }
            public override string Refuse(FluidMix mix)
            {
                var main = mix.Main;
                if (!mix.IsPure || (main != ResourceType.Fuel && main != ResourceType.Diesel && main != ResourceType.Ethanol)) return "PUMPS TAKE CLEAN FUEL ONLY";
                if (pump.stock >= 1f && (main == ResourceType.Diesel) != (pump.kind == ResourceType.Diesel)) return "THIS PUMP HOLDS " + ResourceInfo.Name(pump.kind);
                return null;
            }
            public override float Pour(FluidMix mix, float litres)
            {
                if (Refuse(mix) != null) return 0f;
                float add = Mathf.Min(litres, Room);
                if (pump.stock < 1f) pump.kind = mix.Main == ResourceType.Diesel ? ResourceType.Diesel : ResourceType.Fuel;
                pump.stock += add;
                pump.GetComponent<Placeable>()?.Dirty();
                return add;
            }
        }

        sealed class GeneratorEnd : FluidEnd
        {
            public Generator gen;
            public override float Available => gen.fuel;
            public override float Room => Mathf.Max(0f, gen.tankLitres - gen.fuel);
            public override FluidMix Mix => new FluidMix(ResourceType.Fuel);
            public override float Draw(float litres, FluidMix into) { float t = Mathf.Min(litres, gen.fuel); gen.fuel -= t; into.Set(ResourceType.Fuel); gen.GetComponent<Placeable>()?.Dirty(); return t; }
            public override string Refuse(FluidMix mix) => FuelBlend.Evaluate(mix, EngineFuel.Petrol).runs ? null : "THE GENERATOR TAKES PETROL";
            public override float Pour(FluidMix mix, float litres)
            {
                if (Refuse(mix) != null) return 0f;
                float add = Mathf.Min(litres, Room);
                gen.fuel += add; gen.GetComponent<Placeable>()?.Dirty();
                return add;
            }
        }

        sealed class WaterEnd : FluidEnd
        {
            public ResourceType kind;
            public override float Available => 9999f;
            public override float Room => 9999f;
            public override FluidMix Mix => new FluidMix(kind);
            public override float Draw(float litres, FluidMix into) { into.Set(kind); return litres; }
            public override float Pour(FluidMix mix, float litres) => litres;
        }

        sealed class PackEnd : FluidEnd
        {
            public Inventory inv; public ResourceType kind;
            public override float Available => inv.Get(kind);
            public override float Room => 9999f;
            public override FluidMix Mix => new FluidMix(kind);
            public override float Draw(float litres, FluidMix into)
            {
                int n = Mathf.Min(inv.Get(kind), Mathf.FloorToInt(litres + 1e-4f));
                if (n <= 0 || !inv.TrySpend(kind, n)) return 0f;
                into.Set(kind);
                return n;
            }
            public override string Refuse(FluidMix mix) => mix.IsPure ? null : "THE PACK CARRIES PURE LIQUIDS: POUR THE BLEND SOMEWHERE";
            public override float Pour(FluidMix mix, float litres)
            {
                if (Refuse(mix) != null) return 0f;
                int n = Mathf.FloorToInt(litres + 1e-4f);
                if (n > 0) inv.Add(mix.Main, n);
                return n;
            }
        }

        sealed class PumpjackEnd : FluidEnd
        {
            public Pumpjack jack;
            public override float Available => jack.stored;
            public override float Room => 0f;
            public override FluidMix Mix => new FluidMix(ResourceType.CrudeOil);
            public override float Draw(float litres, FluidMix into) { float t = Mathf.Min(litres, jack.stored); jack.stored -= t; into.Set(ResourceType.CrudeOil); jack.GetComponent<Placeable>()?.Dirty(); return t; }
            public override float Pour(FluidMix mix, float litres) => 0f;
        }

        /// <summary>A puddle or pond on the ground (scooped: clean water comes up dirty).</summary>
        sealed class PoolEnd : FluidEnd
        {
            public Vector3 at;
            public const float Reach = 1.2f, MinCell = 0.5f;
            public override float Available => Spills.PoolNear(at, Reach, out _, MinCell);
            public override float Room => 0f;
            public override FluidMix Mix { get { Spills.PoolNear(at, Reach, out var m, MinCell); var c = m != null ? m.Clone() : new FluidMix(); c.Swap(ResourceType.Water, ResourceType.DirtyWater); return c; } }
            public override float Draw(float litres, FluidMix into) => Spills.Take(at, Reach, litres, into, MinCell);
            public override float Pour(FluidMix mix, float litres) => 0f;
        }

        /// <summary>A built liquid store (pool, pond, drum).</summary>
        sealed class StoreEnd : FluidEnd
        {
            public FluidStore store;
            public override float Available => store.Frozen ? 0f : store.Contents;
            public override float Room => store.Room;
            public override FluidMix Mix => store.Mix;
            public override float Draw(float litres, FluidMix into) => store.Draw(litres, into);
            public override string Refuse(FluidMix mix) => store.Refuse(mix);
            public override float Pour(FluidMix mix, float litres) => store.Pour(mix, litres);
            public override FluidMix After(FluidMix mix, float litres)
            {
                var m = store.Contents < 0.05f ? new FluidMix() : store.Mix.Clone();
                m.Blend(store.Contents < 0.05f ? 0f : store.Contents, mix, Mathf.Min(litres, Room));
                return m;
            }
        }

        /// <summary>A tap or outlet on the water network.</summary>
        sealed class NetEnd : FluidEnd
        {
            public UtilityNode node;
            public override float Available => UtilityGrid.NetWater(node, out _);
            public override float Room => 0f;
            public override FluidMix Mix { get { UtilityGrid.NetWater(node, out float clean); return new FluidMix(clean > 0.99f ? ResourceType.Water : WaterQuality.Carried(node)); } }
            public override float Draw(float litres, FluidMix into) { float got = UtilityGrid.Draw(node, litres, out bool clean); into.Set(clean ? ResourceType.Water : WaterQuality.Carried(node)); return got; }
            public override float Pour(FluidMix mix, float litres) => 0f;
        }

        sealed class GroundEnd : FluidEnd
        {
            public override float Available => 0f;
            public override float Room => 9999f;
            public override FluidMix Mix => new FluidMix();
            public override float Draw(float litres, FluidMix into) => 0f;
            public override float Pour(FluidMix mix, float litres) => litres;
        }

        /// <summary>Everything within reach the container in hand could draw from or pour into.</summary>
        List<FluidEnd> FluidEnds()
        {
            var l = new List<FluidEnd>();
            var v = FindNearby(enterDistance + 0.5f, true);
            if (!v && Player.Interior) v = Player.Interior.GetComponent<VehicleDriver>();
            if (v && v.TryGetComponent<VehicleSystems>(out var sys))
            {
                bool tanker = v.GetComponent<FuelTanker>();
                string vn = Name(v);
                if (sys.fuelCapacity > 0f) l.Add(new TankEnd { sys = sys, vehicle = v, system = VehicleSystems.FluidSystem.Fuel, name = tanker ? "TANKER" : "FUEL TANK" });
                if (!tanker && sys.HasEngine)
                {
                    if (sys.Capacity(VehicleSystems.FluidSystem.Oil) > 0f) l.Add(new TankEnd { sys = sys, vehicle = v, system = VehicleSystems.FluidSystem.Oil, name = "ENGINE OIL" });
                    if (sys.Capacity(VehicleSystems.FluidSystem.Coolant) > 0f) l.Add(new TankEnd { sys = sys, vehicle = v, system = VehicleSystems.FluidSystem.Coolant, name = "COOLANT" });
                }
                foreach (var e in l) e.name = e.name + (vn.Length <= 10 ? " " + vn : "");
            }
            var feet = Player.transform.position;
            foreach (var p in GasPump.All)
                if (p && Vector3.Distance(p.transform.position, feet) < 3f) l.Add(new PumpEnd { pump = p, name = "PUMP " + ResourceInfo.Name(p.kind) });
            foreach (var u in UtilityNode.All)
                if (u && u.TryGetComponent<Generator>(out var gen) && !gen.solid && !gen.gas && Vector3.Distance(u.transform.position, feet) < 2.5f) l.Add(new GeneratorEnd { gen = gen, name = "GENERATOR" });
            foreach (var j in Pumpjack.All)
                if (j && Vector3.Distance(j.transform.position, feet) < 4f) l.Add(new PumpjackEnd { jack = j, name = "PUMPJACK CRUDE" });
            var store = FluidStore.Near(feet, 2.2f);
            if (store) l.Add(new StoreEnd { store = store, name = store.title });
            foreach (var u in UtilityNode.All)
                if (u && (u.GetComponent<WaterTap>() || u.GetComponent<WaterOutlet>()) && !u.GetComponent<FluidStore>() && Vector3.Distance(u.transform.position, feet) < 1.8f)
                { l.Add(new NetEnd { node = u, name = u.GetComponent<WaterTap>() ? "TAP" : "OUTLET" }); break; }
            var terrain = DeformableTerrain.Instance;
            var ahead = feet + Player.transform.forward * 1f;
            float puddle = Spills.PoolNear(ahead, PoolEnd.Reach, out _, PoolEnd.MinCell);
            if (puddle >= 0.3f) l.Add(new PoolEnd { at = ahead, name = puddle > 60f ? "POND" : "PUDDLE" });
            if (terrain && (terrain.WaterDepth(ahead.x, ahead.z) > 0.08f || terrain.WaterDepth(feet.x, feet.z) > 0.08f))
            {
                var k = WaterQuality.IsSea(ahead.x, ahead.z) ? ResourceType.SeaWater : ResourceType.DirtyWater;
                l.Add(new WaterEnd { kind = k, name = k == ResourceType.SeaWater ? "THE SEA" : "OPEN WATER" });
            }
            for (int i = 1; i < ResourceInfo.Count; i++)
            {
                var t = (ResourceType)i;
                if (ResourceInfo.IsFluid(t) && FluidMix.Family(t) != FluidFamily.Other && t != ResourceType.Biogas && Inventory.Get(t) > 0)
                    l.Add(new PackEnd { inv = Inventory, kind = t, name = "PACK " + ResourceInfo.Name(t) });
            }
            return l;
        }

        // ------------------------------------------------------------------ prompt, keys, the choice radial

        string CanInteraction(bool G, bool K)
        {
            var can = HeldCan; var def = HeldCanDef;
            if (can == null || def == null) return null;
            if (Time.frameCount == fluidKeyFrame) { G = false; K = false; }
            if (Pouring)
            {
                if (G || K) { fluidKeyFrame = Time.frameCount; StopPour(); }
                return "POURING " + HeldCanText + "   [" + Controls.Name(Controls.Act.Service) + "] STOP";
            }
            if (K) OpenFluidChoice(true);
            else if (G) OpenFluidChoice(false);
            string what = HeldCanText;
            var v = FindNearby(enterDistance + 0.5f, true);
            string at = v ? " " + Name(v) : "";
            return what + "   [K] FILL FROM" + at + (can.Empty ? "" : "   [G] POUR INTO" + at);
        }

        /// <summary>Gather the siphon (K) or pour (G) options for the container in hand and open the radial (one option
        /// runs at once).</summary>
        public void OpenFluidChoice(bool siphon)
        {
            fluidKeyFrame = Time.frameCount;
            var can = HeldCan; var def = HeldCanDef;
            if (can == null || def == null) return;
            if (Working) { CancelWork("STOPPED"); return; }
            var ends = FluidEnds();
            RadialActions.Clear();
            var first = new List<RadialAction>(); var rest = new List<RadialAction>();
            if (siphon)
            {
                float room = def.litres - can.litres;
                if (room < 0.05f) { Toast(def.name + " IS FULL: " + can.mix.Label()); return; }
                foreach (var e in ends)
                {
                    if (e.Available < 0.05f || (e is PackEnd && e.Available < 1f)) continue;
                    var m = e.Mix;
                    if (!can.Empty && m.MainFamily != can.mix.MainFamily) continue;                 // one kind of liquid per container
                    float n = Mathf.Min(room, e.Available);
                    if (e is PackEnd) n = Mathf.Floor(n + 1e-4f);
                    if (n <= 0f) continue;
                    var end = e;
                    var a = new RadialAction { label = Short(e.name), detail = m.Label() + " +" + Litres(n), run = () => Transfer(end, true) };
                    (m.MainFamily == def.meant ? first : rest).Add(a);
                }
            }
            else
            {
                if (can.Empty) { Toast(def.name + " IS EMPTY"); return; }
                foreach (var e in ends)
                {
                    if (e is WaterEnd || e.Room < 0.05f) continue;
                    if (e is PackEnd pe && (pe.kind != can.mix.Main || !can.mix.IsPure || can.litres < 1f)) continue;
                    var end = e;
                    string detail;
                    var why = e.Refuse(can.mix);
                    var after = e.After(can.mix, can.litres);
                    if (why != null) detail = "REFUSED";
                    else detail = after != null ? after.Label() : "-" + Litres(Mathf.Min(can.litres, e.Room));
                    var a = new RadialAction { label = Short(e.name), detail = detail, run = () => Transfer(end, false) };
                    (e is TankEnd ? first : rest).Add(a);
                }
                string canId = def.id;
                rest.Add(new RadialAction { label = "POUR OUT", detail = "ON THE GROUND (WALK TO LAY A TRAIL)", run = () => StartPour(canId) });
            }
            RadialActions.AddRange(first); RadialActions.AddRange(rest);
            if (RadialActions.Count > 12) RadialActions.RemoveRange(12, RadialActions.Count - 12);
            if (RadialActions.Count == 0) { Toast(siphon ? "NOTHING HERE FITS THE " + def.name + (can.Empty ? "" : " (" + can.mix.Label() + ")") : "NOTHING TO POUR INTO"); return; }
            if (RadialActions.Count == 1 && siphon) { var only = RadialActions[0]; RadialActions.Clear(); only.run(); return; }
            fluidChoice = true; fluidChoiceSiphon = siphon;
            RadialOpen = true; RadialHover = -1; fluidAim = Vector2.zero; fluidChoiceAt = Player.transform.position;
            RadialHint = (siphon ? "FILL THE " : "POUR THE ") + def.name + ": MOUSE + CLICK OR [" + Controls.Name(siphon ? Controls.Act.Siphon : Controls.Act.Service) + "], RMB CANCELS";
            RadialBlocksLook = true;
        }

        static string Short(string s) => s.Length > 22 ? s.Substring(0, 22) : s;
        static string Litres(float l) => l.ToString(l < 10f ? "0.#" : "0", CultureInfo.InvariantCulture) + " L";

        /// <summary>Close the container's choice without running anything.</summary>
        public void CancelFluidChoice() { if (fluidChoice) CloseFluidChoice(); }

        void CloseFluidChoice()
        {
            fluidChoice = false; RadialHint = null;
            RadialOpen = false; RadialHover = -1; RadialBlocksLook = false;
            RadialActions.Clear();
        }

        /// <summary>Runs the container's choice radial while it is open (from UpdateRadial). True = it owns the frame.</summary>
        bool UpdateFluidChoice(Keyboard kb, Mouse mouse, Gamepad pad)
        {
            if (!fluidChoice) return false;
            if (!RadialOpen || Menus.IsOpen || Current || !Player || !(Player.Tool is FluidCanTool) || Flat(Player.transform.position, fluidChoiceAt) > 1.5f) { CloseFluidChoice(); return false; }
            int n = RadialActions.Count;
            if (mouse != null)
            {
                fluidAim = Vector2.ClampMagnitude(fluidAim + mouse.delta.ReadValue(), 120f);
                if (fluidAim.magnitude > 18f) RadialHover = Slice(fluidAim, n);
            }
            if (pad != null)
            {
                var st = pad.rightStick.ReadValue();
                if (st.magnitude > 0.5f) RadialHover = Slice(st, n);
            }
            bool pick = (mouse != null && mouse.leftButton.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame)
                        || Controls.Down(fluidChoiceSiphon ? Controls.Act.Siphon : Controls.Act.Service) || (kb != null && kb.enterKey.wasPressedThisFrame);
            bool cancel = (mouse != null && mouse.rightButton.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame)
                          || Controls.Down(fluidChoiceSiphon ? Controls.Act.Service : Controls.Act.Siphon);
            if (pick || cancel)
            {
                fluidClickFrame = Time.frameCount; fluidKeyFrame = Time.frameCount;
                var chosen = pick && RadialHover >= 0 && RadialHover < n ? RadialActions[RadialHover] : (RadialAction?)null;
                CloseFluidChoice();
                chosen?.run?.Invoke();
                return true;
            }
            RadialBlocksLook = true;
            return true;
        }

        static int Slice(Vector2 aim, int n)
        {
            float a = (Mathf.Atan2(aim.x, aim.y) * Mathf.Rad2Deg + 360f) % 360f;
            return Mathf.Clamp(Mathf.FloorToInt(((a + 180f / n) % 360f) / (360f / n)), 0, n - 1);
        }

        /// <summary>Automation: run the first open radial slice whose label (or detail) contains <paramref name="text"/>.</summary>
        public bool PickRadial(string text)
        {
            if (!RadialOpen) return false;
            for (int i = 0; i < RadialActions.Count; i++)
            {
                var a = RadialActions[i];
                if (!a.label.Contains(text) && (a.detail == null || !a.detail.Contains(text))) continue;
                if (fluidChoice) CloseFluidChoice(); else { RadialOpen = false; RadialHover = -1; RadialBlocksLook = false; }
                fluidClickFrame = Time.frameCount;
                if (a.run != null) a.run(); else if (a.act.HasValue) Controls.Inject(a.act.Value);
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ the transfer

        void Transfer(FluidEnd end, bool siphon)
        {
            var def = HeldCanDef; var can = HeldCan;
            if (def == null || can == null) return;
            string canId = def.id;
            System.Action act = () => DoTransfer(canId, end, siphon);
            var v = end.vehicle;
            if (v && WorkAnimated(v))
            {
                if (job != null) { KeyStopsWork(); return; }
                bool fuel = end.system == VehicleSystems.FluidSystem.Fuel;
                var kind = fuel ? (siphon ? WorkKind.Siphon : WorkKind.Refuel) : WorkKind.Service;
                if (Begin(v, kind, null, null, act))
                {
                    float litres = siphon ? Mathf.Min(def.litres - can.litres, end.Available) : Mathf.Min(can.litres, end.Room);
                    job.duration = Mathf.Max(1.2f, litres / (siphon ? 2f : 3f) * SkillPace(Skill.Mechanics));
                    job.label = (siphon ? "SIPHONING " : "POURING INTO ") + end.name + " (" + Litres(litres) + ")";
                    job.key = siphon ? Controls.Act.Siphon : Controls.Act.Service;
                    job.propSet = true; job.propKey = null;                                             // the container in hand
                    job.pose = siphon ? WorkPose.Siphon : fuel ? WorkPose.Pour : WorkPose.LeanIn;
                    return;
                }
            }
            act();
        }

        void DoTransfer(string canId, FluidEnd end, bool siphon)
        {
            LastTransferLitres = 0f;
            var def = FluidContainers.Get(canId);
            if (def == null || !Player || !(Player.Tool is FluidCanTool t) || t.id != canId) { LastTransferNote = "THE CONTAINER LEFT THE HAND"; Toast(LastTransferNote); return; }
            var can = CansOf(canId)[0];
            if (siphon)
            {
                float room = def.litres - can.litres;
                if (room < 0.01f) { LastTransferNote = def.name + " IS FULL"; Toast(LastTransferNote); return; }
                if (!can.Empty && end.Mix.MainFamily != can.mix.MainFamily) { LastTransferNote = "WON'T MIX " + end.Mix.Label() + " INTO " + can.mix.Label(); Toast(LastTransferNote); return; }
                var drawn = new FluidMix();
                float got = end.Draw(Mathf.Min(room, end.Available), drawn);
                if (got <= 0f) { LastTransferNote = end.name + " IS DRY"; Toast(LastTransferNote); return; }
                if (can.Empty) { can.mix.CopyFrom(drawn); can.litres = got; }
                else { can.mix.Blend(can.litres, drawn, got); can.litres += got; }
                LastTransferLitres = got;
                LastTransferNote = "FILLED " + Litres(got) + " " + drawn.Label() + " FROM " + end.name;
                Stats.Practice(Skill.Survival, 0.5f + got * 0.05f);
                MadMax.Audio.Sfx.Play(end is WaterEnd ? "splash" : "pour", Player.transform.position, 0.5f, 0.9f, 15f, 0.2f);
            }
            else
            {
                if (can.Empty) { LastTransferNote = def.name + " IS EMPTY"; Toast(LastTransferNote); return; }
                var why = end.Refuse(can.mix);
                if (why != null) { LastTransferNote = why; Toast(why); return; }
                var mix = can.mix.Clone();
                float poured = end.Pour(mix, Mathf.Min(can.litres, end.Room));
                if (poured <= 0f) { LastTransferNote = end.name + " IS FULL"; Toast(LastTransferNote); return; }
                can.litres -= poured;
                if (can.litres < 0.01f) can.Clear();
                LastTransferLitres = poured;
                string result = end is TankEnd te ? ": NOW " + te.sys.MixOf(te.system).Label() : "";
                LastTransferNote = (end is GroundEnd ? "POURED OUT " : "POURED ") + Litres(poured) + " " + mix.Label() + (end is GroundEnd ? "" : " INTO " + end.name) + result;
                if (end is GroundEnd)
                    for (int i = 0; i < 6; i++) Fx.Smoke(Player.transform.position + Player.transform.forward * 0.6f + Vector3.up * 0.4f, Vector3.down * 1.5f + Random.insideUnitSphere * 0.4f, 0.08f, new Color(0.6f, 0.55f, 0.4f, 0.6f), 0.4f);
                MadMax.Audio.Sfx.Play("pour", Player.transform.position, 0.5f, 1f, 15f, 0.2f);
            }
            Toast(LastTransferNote);
            if (end.vehicle) MadMax.Net.NetSession.Instance?.SendVehicleMeta(end.vehicle);
        }

        // ------------------------------------------------------------------ pouring out (a stream you can walk with)

        string pourCan;
        float pourAcc, pourFx;
        Vector3 pourAt;

        /// <summary>A container is being poured out onto the ground (G stops it; walking lays a trail).</summary>
        public bool Pouring => pourCan != null;

        /// <summary>Litres a second a container pours: a bucket dumps, a bottle trickles.</summary>
        public static float PourRate(string canId) => canId == FluidContainers.Bucket ? 2.5f : canId == FluidContainers.JerryCan ? 1f
            : canId == FluidContainers.FuelCan ? 0.8f : canId == FluidContainers.OilJug ? 0.5f : 0.25f;

        /// <summary>Start pouring the container in hand onto the ground in front (or into a store under the spout).</summary>
        public void StartPour(string canId)
        {
            var def = FluidContainers.Get(canId);
            if (def == null || !Player || !(Player.Tool is FluidCanTool t) || t.id != canId || CansOf(canId)[0].Empty) return;
            pourCan = canId; pourAcc = 0f;
            Toast("POURING OUT THE " + def.name + "  [" + Controls.Name(Controls.Act.Service) + "] STOPS");
        }

        public void StopPour()
        {
            if (pourCan == null) return;
            FlushPour();
            pourCan = null;
            if (Player) MadMax.Audio.Sfx.Loop(Player, "pour", 0f);
        }

        void UpdatePour(float dt)
        {
            if (pourCan == null) return;
            if (!Player || Current || !(Player.Tool is FluidCanTool t) || t.id != pourCan || Menus.IsOpen) { StopPour(); return; }
            var l = CansOf(pourCan);
            if (l.Count == 0 || l[0].Empty) { StopPour(); Toast("EMPTY"); return; }
            var can = l[0];
            float n = Mathf.Min(can.litres, PourRate(pourCan) * dt);
            var tr = Player.transform;
            var spout = tr.position + tr.forward * 0.45f + Vector3.up * 0.85f;
            var at = tr.position + tr.forward * 0.75f;
            if (Physics.Raycast(at + Vector3.up * 1.2f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(tr)) at = hit.point;
            if (pourAcc > 0f && Flat(at, pourAt) > 0.4f) FlushPour();                       // walking on: land the last bit where it fell
            pourAt = at;
            pourAcc += n;
            pourMix.CopyFrom(can.mix);
            can.litres -= n;
            if (can.litres < 0.01f) can.Clear();
            if (pourAcc >= 0.25f) FlushPour();
            if ((pourFx -= dt) <= 0f)
            {
                pourFx = 0.05f;
                var col = FluidProps.Colour(pourMix); col.a = Mathf.Max(0.6f, col.a);
                Fx.Smoke(spout, (at - spout) * 2.2f + Random.insideUnitSphere * 0.15f, 0.03f, col, 0.35f);
            }
            MadMax.Audio.Sfx.Loop(Player, "pour", 0.45f, 1f, 15f);
            if (can.Empty) { StopPour(); Toast("POURED IT ALL OUT"); }
        }

        readonly FluidMix pourMix = new FluidMix();

        void FlushPour()
        {
            if (pourAcc <= 0f || pourMix.Empty) { pourAcc = 0f; return; }
            float left = pourAcc; pourAcc = 0f;
            var store = FluidStore.Below(pourAt);
            if (store && store.Refuse(pourMix) == null) left -= store.Pour(pourMix, left);
            if (left > 0.001f)
            {
                Spills.Pour(pourAt, pourMix, left);
                if (pourMix.Of(FluidFamily.Aqueous) > 0.8f) Fire.Douse(pourAt + Vector3.up * 0.2f, 1.2f, left);   // fuel onto flames catches (Spills.Burn)
            }
            LastTransferLitres += left;
        }

        /// <summary>Fill the container in hand from a water network (a tap's [E]).</summary>
        public void FillHeldFromNetwork(UtilityNode node, string name)
        {
            if (!node || HeldCanDef == null) return;
            Transfer(new NetEnd { node = node, name = name }, true);
        }

        // ------------------------------------------------------------------ LMB with a container; pack options

        /// <summary>LMB with a container in hand: douse a fire (water), dip it in open water, drink from it, or say what
        /// is in it.</summary>
        public void CanStrike(FluidCanTool tool, PlayerCharacter user)
        {
            var def = FluidContainers.Get(tool.id);
            if (def == null || user != Player) return;
            var can = CansOf(tool.id).Count > 0 ? CansOf(tool.id)[0] : null;
            if (can == null) return;
            var at = user.transform.position + user.transform.forward * 1.0f;
            bool watery = !can.Empty && can.mix.Of(FluidFamily.Aqueous) > 0.9f;
            if (watery && Fire.Douse(at + Vector3.up * 0.3f, 1.8f, 0f) > 0)
            {
                float use = Mathf.Min(can.litres, 2f);
                Fire.Douse(at + Vector3.up * 0.3f, 1.8f, use);
                can.litres -= use; if (can.litres < 0.01f) can.Clear();
                MadMax.Audio.Sfx.Play("sizzle", at, 0.8f, 1f, 20f);
                Toast("DOUSED THE FLAMES");
                return;
            }
            var terrain = DeformableTerrain.Instance;
            if (terrain && terrain.WaterDepth(at.x, at.z) > 0.08f && (can.Empty || can.mix.MainFamily == FluidFamily.Aqueous) && can.litres < def.litres - 0.01f)
            {
                var k = WaterQuality.IsSea(at.x, at.z) ? ResourceType.SeaWater : ResourceType.DirtyWater;
                float add = def.litres - can.litres;
                if (can.Empty) { can.mix.Set(k); can.litres = add; } else { can.mix.Blend(can.litres, k, add); can.litres += add; }
                MadMax.Audio.Sfx.Play("splash", at, 0.6f, 1.2f, 15f);
                Toast("FILLED THE " + def.name + ": " + can.Describe(def.litres));
                return;
            }
            if (can.litres < def.litres - 0.01f && Spills.PoolNear(at, PoolEnd.Reach, out var pm, PoolEnd.MinCell) >= 0.3f && pm != null && (can.Empty || pm.MainFamily == can.mix.MainFamily))
            {
                var got = new FluidMix();
                float n = Spills.Take(at, PoolEnd.Reach, def.litres - can.litres, got, PoolEnd.MinCell);
                if (n > 0.01f)
                {
                    if (can.Empty) { can.mix.CopyFrom(got); can.litres = n; } else { can.mix.Blend(can.litres, got, n); can.litres += n; }
                    MadMax.Audio.Sfx.Play("splash", at, 0.4f, 1.3f, 12f);
                    Toast("SCOOPED UP " + Litres(n) + ": " + can.Describe(def.litres));
                    return;
                }
            }
            if (watery && can.mix.Of(FluidFamily.Fuel) + can.mix.Of(FluidFamily.Lube) < 0.01f)
            {
                float sip = Mathf.Min(can.litres, 0.5f);
                bool sea = can.mix[ResourceType.SeaWater] > 0.3f, coolant = can.mix[ResourceType.Coolant] > 0.05f;
                can.litres -= sip;
                var mixNow = can.mix.Clone();
                if (can.litres < 0.01f) can.Clear();
                if (coolant) { Stats.sick = Mathf.Max(Stats.sick, 200f); Toast("THAT WAS COOLANT: SWEET AND POISONOUS!"); return; }
                if (sea) { Drink(-6f, false); Toast("SALT WATER: THIRSTIER THAN BEFORE"); return; }
                Drink(sip * 90f, mixNow[ResourceType.DirtyWater] > 0.1f);
                return;
            }
            Toast(def.name + ": " + can.Describe(def.litres) + (can.Empty ? "  (LMB AT WATER FILLS IT)" : "  [K] FILL  [G] POUR"));
        }

        /// <summary>Pack menu options for a container: pour it into the pack, fill it from the pack, pour it out.</summary>
        void CanPackOptions(string id, List<ContextOption> into)
        {
            var def = FluidContainers.Get(id);
            if (def == null) return;
            var l = CansOf(id);
            if (l.Count == 0) return;
            var can = l[0];
            if (!can.Empty && can.mix.IsPure && can.litres >= 1f)
                into.Add(Opt("EMPTY INTO THE PACK (" + Litres(Mathf.Floor(can.litres)) + " " + can.mix.Label() + ")", () =>
                {
                    int n = Mathf.FloorToInt(can.litres + 1e-4f);
                    Inventory.Add(can.mix.Main, n); can.litres -= n; if (can.litres < 0.01f) can.Clear();
                }, false));
            if (!can.Empty) into.Add(Opt("POUR OUT (" + Litres(can.litres) + ")", () => { if (Player) Spills.Pour(Player.transform.position + Player.transform.forward * 0.5f, can.mix, can.litres); can.Clear(); }, false));
            float room = def.litres - can.litres;
            if (room >= 1f)
                for (int i = 1; i < ResourceInfo.Count; i++)
                {
                    var t = (ResourceType)i;
                    if (!ResourceInfo.IsFluid(t) || FluidMix.Family(t) == FluidFamily.Other || t == ResourceType.Biogas || Inventory.Get(t) < 1) continue;
                    if (!can.Empty && FluidMix.Family(t) != can.mix.MainFamily) continue;
                    var kind = t;
                    into.Add(Opt("FILL WITH " + ResourceInfo.Name(t) + " FROM THE PACK", () =>
                    {
                        int n = Mathf.Min(Inventory.Get(kind), Mathf.FloorToInt(def.litres - can.litres + 1e-4f));
                        if (n <= 0 || !Inventory.TrySpend(kind, n)) return;
                        if (can.Empty) { can.mix.Set(kind); can.litres = n; } else { can.mix.Blend(can.litres, kind, n); can.litres += n; }
                    }, false));
                }
        }
    }
}
