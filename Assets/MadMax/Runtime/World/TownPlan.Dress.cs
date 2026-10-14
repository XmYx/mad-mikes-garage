using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Dressing the plan: buildings on their lots (doors to the street, fenced yards, interior loot spots),
    /// street furniture along the sidewalks (lamps, benches, bins, hydrants, bus stops, phone booths, traffic lights
    /// and stop signs at the corners), roundabout islands, squares, parks, alleys with trash and graffiti, parked wrecks,
    /// market stalls, the residents' sidewalk beats, birds, strays and the gang's hangout. Templates live in
    /// <c>BiomeProps.Town.cs</c>; every prop faces its street with its -Z side.</summary>
    public sealed partial class TownPlan
    {
        readonly Dictionary<long, List<Vector3>> taken = new Dictionary<long, List<Vector3>>();

        static long TakeKey(int x, int y) => ((long)x << 32) ^ (uint)y;

        /// <summary>Nothing else of the dressing within <paramref name="r"/> (street furniture only).</summary>
        bool Room(Vector2 l, float r)
        {
            int cx = Mathf.FloorToInt(l.x / 4f), cy = Mathf.FloorToInt(l.y / 4f);
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (taken.TryGetValue(TakeKey(cx + dx, cy + dy), out var list))
                    foreach (var t in list) if ((new Vector2(t.x, t.y) - l).magnitude < r + t.z) return false;
            return true;
        }

        void Take(Vector2 l, float r)
        {
            long k = TakeKey(Mathf.FloorToInt(l.x / 4f), Mathf.FloorToInt(l.y / 4f));
            if (!taken.TryGetValue(k, out var list)) taken[k] = list = new List<Vector3>();
            list.Add(new Vector3(l.x, l.y, r));
        }

        void Prop(string id, Vector2 l, float yaw, bool dyn = false, bool crate = false, string table = null)
            => spots.Add(new TownSpot { kind = SpotKind.Prop, id = id, pos = World(l), yaw = yaw, dynamic = dyn, crate = crate, table = table });

        void Decor(string id, Vector2 l, float yaw) => spots.Add(new TownSpot { kind = SpotKind.Decor, id = id, pos = World(l), yaw = yaw });

        void Mark(SpotKind kind, Vector2 l, float yaw, string id = null, string table = null) => spots.Add(new TownSpot { kind = kind, id = id, pos = World(l), yaw = yaw, table = table });

        /// <summary>Searchable furniture and lights inside a building (offsets in metres in the building's frame, as the
        /// old settlement layouts placed them).</summary>
        void Inside(Vector2 l, float yaw, params (float x, float y, float z, string table, string visual)[] list)
        {
            var at = World(l);
            var q = Quaternion.Euler(0f, yaw, 0f);
            foreach (var sp in list)
            {
                var o = q * new Vector3(sp.x, 0f, sp.z);
                spots.Add(new TownSpot { kind = SpotKind.Prop, id = sp.table == "light" ? "Light" : "Loot", pos = at + new Vector2(o.x, o.z), yaw = yaw + 180f, y = sp.y, table = sp.table, visual = sp.visual });
            }
        }

        bool IsWalk(Vector2 l) => Query(l, out var h) && (h.feature == FSidewalk || h.feature == FKerb);
        bool InLot(Vector2 l, TownLot lot) => Query(l, out var h) && h.lot == lot.id;

        string LampPiece(Vector2 l) => st.kind == Biome.Village ? "streetlamp_pole" : st.kind == Biome.Town && l.magnitude < radius * 0.5f ? "streetlamp_gas" : "streetlamp_cobra";

        void Dress(System.Random rnd)
        {
            foreach (var lot in lots) DressLot(lot, rnd);
            foreach (var s in streets) DressStreet(s, rnd);
            foreach (var j in junctions) DressJunction(j, rnd);
            Beats();
        }

        // ------------------------------------------------------------------ lots

        void Fence(string id, TownLot lot, float x0, float y0, float x1, float y1)
        {
            var a = lot.At(x0, y0); var b = lot.At(x1, y1);
            float len = (b - a).magnitude;
            if (len < 1f) return;
            int n = Mathf.Max(1, Mathf.CeilToInt(len / 3.84f));
            float yaw = Yaw(new Vector2(-(b - a).y, (b - a).x));                                 // template X along the run
            for (int i = 0; i < n; i++) Prop(id, Vector2.Lerp(a, b, (i + 0.5f) / n), yaw);
        }

        void DressLot(TownLot lot, System.Random rnd)
        {
            float w = lot.Width, dep = lot.Depth, hw = w * 0.5f;
            float door = Yaw(-lot.front), street = Yaw(lot.front), along = Yaw(lot.Across);
            bool city = st.kind == Biome.City;
            switch (lot.use)
            {
                case LotUse.House:
                {
                    int v = w >= 14f && rnd.NextDouble() < 0.5 ? 1 : 0;
                    float front = 3.84f, back = 3.4f;
                    lot.setback = Mathf.Clamp(dep - front - back - 1.2f, 1.2f, 3f);
                    var at = lot.At(0f, lot.setback + front);
                    Prop("BrickHouse" + v, at, door);
                    Inside(at, door, (-2.6f, 0.16f, 2f, "kitchen", "fridge"), (-2.6f, 3.2f, 1.5f, "house", "locker"), (0.5f, 0.16f, 2.2f, "house", "shelf"));
                    if (rnd.NextDouble() < 0.5) Inside(at, door, (0f, 2.6f, 0f, "light", null));
                    if (lot.setback >= 1.8f)
                    {
                        Fence("FencePicket0", lot, -hw + 0.2f, 0.3f, -1.3f, 0.3f);
                        Fence("FencePicket0", lot, 1.3f, 0.3f, hw - 0.2f, 0.3f);
                        if (rnd.NextDouble() < 0.6) Prop("Bush1", lot.At(-hw + 1.2f, 1.3f), 0f);
                        if (rnd.NextDouble() < 0.6) Prop("Bush0", lot.At(hw - 1.2f, 1.3f), 90f);
                    }
                    Fence("Fence0", lot, -hw + 0.2f, 0.6f, -hw + 0.2f, dep - 0.3f);
                    if (lot.last) Fence("Fence0", lot, hw - 0.2f, 0.6f, hw - 0.2f, dep - 0.3f);
                    float yard = dep - lot.setback - front - back;
                    if (yard > 2.6f && rnd.NextDouble() < 0.45) Prop("ParkTree" + rnd.Next(2), lot.At((float)rnd.NextDouble() * (w - 4f) - (hw - 2f), dep - 1.4f), rnd.Next(4) * 90f);
                    if (rnd.NextDouble() < 0.35) Prop("Bin0", lot.At(hw - 0.8f, lot.setback + 1f), street);
                    break;
                }
                case LotUse.Shop:
                {
                    int v = rnd.Next(2);
                    lot.setback = Mathf.Clamp(dep - 9.4f, 0.6f, 1.6f);
                    var at = lot.At(0f, lot.setback + 4.6f);
                    Prop("Shop" + v, at, door);
                    Inside(at, door, (-2f, 0.2f, -1.6f, "shop", "shelf"), (2f, 0.2f, -1.6f, "shop", "shelf"), (-2f, 0.2f, 1.6f, "shop", "shelf"), (4.5f, 0.2f, 3f, "shop", "locker"), (0f, 3f, 0f, "light", null));
                    if (hw > 6.9f) Prop("Bin0", lot.At(hw - 0.4f, 0.7f), street);
                    if (rnd.NextDouble() < 0.6) Decor("TrashBags0", lot.At(hw - 0.6f, dep - 0.7f), rnd.Next(360));
                    Decor("Litter" + rnd.Next(2), lot.At((float)rnd.NextDouble() * w - hw, 0.3f), rnd.Next(360));
                    break;
                }
                case LotUse.Apartment:
                {
                    bool wide = w >= 17f;
                    int v = wide ? (city && rnd.NextDouble() < 0.6 ? 3 : 1) : (city && rnd.NextDouble() < 0.5 ? 2 : 0);
                    lot.setback = Mathf.Clamp(dep - 10.4f, 0.8f, 1.6f);
                    var at = lot.At(0f, lot.setback + 4.4f);
                    Prop("Tower" + v, at, door);
                    Inside(at, door, (-3f, 0.2f, 3f, "office", "locker"), (-3.5f, 3.4f, -2.5f, "house", "shelf"), (1f, 3.4f, 3f, "kitchen", "fridge"));
                    if (rnd.NextDouble() < 0.5) Prop("Crate", lot.At(hw - 1f, dep - 1f), rnd.Next(360), true, true);
                    Decor("TrashBags0", lot.At(-hw + 0.7f, dep - 0.8f), rnd.Next(360));
                    if (city && rnd.NextDouble() < 0.5) Prop("Planter0", lot.At(-hw + 1f, 0.8f), street);
                    break;
                }
                case LotUse.Warehouse:
                {
                    int v = rnd.Next(2);
                    lot.setback = Mathf.Clamp(dep - 10.2f, 1.5f, 4f);
                    var at = lot.At(0f, lot.setback + 4.8f);
                    Prop("Warehouse" + v, at, door);
                    Inside(at, door, (-4f, 0.2f, 2f, "tools", "locker"), (3f, 0.2f, 2.5f, "garage", "workbench"));
                    Fence("ChainLink0", lot, -hw + 0.2f, 0.4f, -hw + 0.2f, dep - 0.2f);
                    Fence("ChainLink0", lot, -hw + 0.2f, dep - 0.2f, hw - 0.2f, dep - 0.2f);
                    if (lot.last) Fence("ChainLink0", lot, hw - 0.2f, 0.4f, hw - 0.2f, dep - 0.2f);
                    if (lot.setback > 2.5f)
                    {
                        Prop("Crate", lot.At(-hw + 1.4f, 1.2f), rnd.Next(360), true, true);
                        Prop("OilDrum0", lot.At(hw - 1.2f, 1f), rnd.Next(360));
                        if (rnd.NextDouble() < 0.5) Prop("OilDrum0", lot.At(hw - 2.1f, 1.3f), rnd.Next(360));
                    }
                    break;
                }
                case LotUse.Shack:
                {
                    int v = rnd.Next(3);
                    lot.setback = 1.5f;
                    var at = lot.At(0f, Mathf.Min(1.5f + 2f, dep - 2f));
                    Prop("Shack" + v, at, door);
                    Inside(at, door, (0.8f, 0.08f, 0.9f, v == 0 ? "tools" : "house", "locker"));
                    Prop("Table", at, door);
                    var q = Quaternion.Euler(0f, door, 0f);
                    foreach (var off in new[] { new Vector3(2.4f, 0, -2.2f), new Vector3(-2.3f, 0, -2.6f) })
                    {
                        var o = q * off;
                        var wl = Local(World(at).x + o.x, World(at).y + o.z);
                        if (InLot(wl, lot)) Prop("Crate", wl, door + 15f, true, true);
                    }
                    if (rnd.NextDouble() < 0.4) Prop("OilDrum0", lot.At(-hw + 0.9f, dep - 0.9f), 0f);
                    break;
                }
                case LotUse.Farm:
                {
                    int v = w >= 18f ? 1 : 0;
                    lot.setback = Mathf.Min(4f, Mathf.Max(1.5f, dep - 9f));
                    var at = lot.At(0f, lot.setback + 2.9f);
                    Prop("Farmhouse" + v, at, door);
                    Inside(at, door, (-2.2f, 0.16f, 1.8f, "kitchen", "fridge"), (2.4f, 0.16f, 1.8f, "farm", "crate"));
                    if (rnd.NextDouble() < 0.4) Inside(at, door, (0f, 2.3f, 0f, "light", null));
                    float yd = Mathf.Min(16f, dep) - 0.3f;
                    Fence("Fence0", lot, -hw + 0.2f, 0.3f, -1.4f, 0.3f);
                    Fence("Fence0", lot, 1.4f, 0.3f, hw - 0.2f, 0.3f);
                    Fence("Fence0", lot, -hw + 0.2f, 0.6f, -hw + 0.2f, yd);
                    if (lot.last) Fence("Fence0", lot, hw - 0.2f, 0.6f, hw - 0.2f, yd);
                    if (yd > 11f && rnd.NextDouble() < 0.55) Prop("Haystack0", lot.At((rnd.NextDouble() < 0.5 ? -1f : 1f) * (hw - 2.6f), yd - 2.2f), 0f);
                    Prop("Crate", lot.At(hw - 1.6f, Mathf.Min(yd - 1f, lot.setback + 7f)), door + 10f, true, true);
                    break;
                }
                case LotUse.Ruin:
                    if (dep >= 12f && w >= 14f && rnd.NextDouble() < 0.5)
                    {
                        var at = lot.At(0f, 5.9f);
                        int v = rnd.NextDouble() < 0.5 ? 0 : 2;
                        Prop("Tower" + v, at, door);
                        Inside(at, door, (-3f, 0.2f, 3f, "office", "locker"));
                    }
                    else
                    {
                        int n = 2 + rnd.Next(2);
                        for (int i = 0; i < n; i++) Prop("Rubble0", lot.At(((float)rnd.NextDouble() - 0.5f) * (w - 4f), 2f + (float)rnd.NextDouble() * (dep - 4f)), rnd.Next(4) * 90f);
                        if (w >= 8f && rnd.NextDouble() < 0.2) Prop("CarStack" + rnd.Next(2), lot.At(0f, dep - 1.4f), street + 90f);
                    }
                    if (rnd.NextDouble() < 0.35) Prop("Graffiti" + rnd.Next(2), lot.At(((float)rnd.NextDouble() - 0.5f) * (w - 4f), 0.5f), door);
                    break;
                case LotUse.Empty:
                    if (rnd.NextDouble() < 0.4) Prop("Rubble0", lot.At(((float)rnd.NextDouble() - 0.5f) * (w - 4f), dep * 0.6f), rnd.Next(4) * 90f);
                    if (dep > 6f && w > 6f && rnd.NextDouble() < 0.4) Mark(SpotKind.Wreck, lot.At(0f, dep * 0.45f), street + 60f + rnd.Next(60));
                    if (rnd.NextDouble() < 0.3) Prop("OilDrum0", lot.At(-hw + 1f, dep - 1f), 0f);
                    for (int i = 0; i < 3; i++) Decor(i == 0 ? "TrashBags0" : "Litter" + rnd.Next(2), lot.At(((float)rnd.NextDouble() - 0.5f) * (w - 2f), 0.8f + (float)rnd.NextDouble() * (dep - 1.6f)), rnd.Next(360));
                    break;
                case LotUse.Parking:
                {
                    int bays = Mathf.FloorToInt((w - 1.6f) / 2.7f);
                    for (int row = 0; row < (dep > 15f ? 2 : 1); row++)
                        for (int i = 0; i < bays; i++)
                        {
                            float x = -(bays * 2.7f) * 0.5f + (i + 0.5f) * 2.7f;
                            Mark(SpotKind.Wreck, lot.At(x, row == 0 ? 3.5f : dep - 3.5f), row == 0 ? door : street);
                        }
                    Mark(SpotKind.Lamp, lot.At(-hw + 0.5f, dep - 0.5f), Yaw(lot.Across), "streetlamp_cobra");
                    if (w > 14f) Mark(SpotKind.Lamp, lot.At(hw - 0.5f, dep - 0.5f), Yaw(-lot.Across), "streetlamp_cobra");
                    for (int i = 0; i < 3; i++) Decor("Litter" + rnd.Next(2), lot.At(((float)rnd.NextDouble() - 0.5f) * (w - 2f), 1f + (float)rnd.NextDouble() * (dep - 2f)), rnd.Next(360));
                    if (lots.IndexOf(lot) % 3 == 0) Mark(SpotKind.Birds, lot.At(0f, dep * 0.5f), 0f);
                    break;
                }
                case LotUse.GasStation:
                {
                    lot.setback = 0f;
                    var c = lot.At(0f, 6f);
                    Prop("Canopy0", c, along);
                    Prop("GasPump0", lot.At(-2.4f, 6f), along, false, false, "pump");
                    Prop("GasPump0", lot.At(2.4f, 6f), along + 180f, false, false, "pump");
                    var k = lot.At(hw - 3f, dep - 2.6f);
                    Prop("Shack1", k, door);
                    Inside(k, door, (0.8f, 0.08f, 0.9f, "shop", "shelf"));
                    Prop("OilDrum0", lot.At(-hw + 1.2f, dep - 1.2f), 0f);
                    Prop("OilDrum0", lot.At(-hw + 2.1f, dep - 1f), 0f);
                    Mark(SpotKind.Lamp, lot.At(-hw + 0.5f, 0.6f), Yaw(lot.Across), "streetlamp_cobra");
                    break;
                }
                case LotUse.Hangout: DressHangout(lot, rnd); break;
                case LotUse.Park: DressPark(lot, rnd); break;
                case LotUse.Plaza: case LotUse.Market: DressPlaza(lot, rnd); break;
                case LotUse.Green: DressGreen(lot, rnd); break;
            }
        }

        void DressPlaza(TownLot lot, System.Random rnd)
        {
            float w = lot.Width, dep = lot.Depth, hw = w * 0.5f;
            bool city = st.kind == Biome.City;
            var c = lot.At(0f, dep * 0.5f);
            Prop(city ? "Fountain0" : "Statue0", c, Yaw(lot.front));
            for (int i = 0; i < 4; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 0.5f;
                var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (city ? 5.5f : 4.5f);
                if (!InLot(p, lot)) continue;
                Prop("Bench0", p, Yaw(p - c));
                if (i % 2 == 0) Prop("Bin0", p + (p - c).normalized * 0.2f + new Vector2(-(p - c).y, (p - c).x).normalized * 1.3f, 0f);
            }
            foreach (var (x, y) in new[] { (-hw + 2f, 2f), (hw - 2f, 2f), (-hw + 2f, dep - 2f), (hw - 2f, dep - 2f) })
                if (InLot(lot.At(x, y), lot)) Prop("Planter0", lot.At(x, y), Yaw(lot.front));
            foreach (var (x, y) in new[] { (-hw + 0.9f, 0.9f), (hw - 0.9f, 0.9f), (-hw + 0.9f, dep - 0.9f), (hw - 0.9f, dep - 0.9f) })
                if (InLot(lot.At(x, y), lot)) Mark(SpotKind.Lamp, lot.At(x, y), Yaw(c - lot.At(x, y)), "streetlamp_gas");
            if (InLot(lot.At(hw - 2.8f, dep - 3f), lot)) Mark(SpotKind.Mast, lot.At(hw - 2.8f, dep - 3f), Yaw(c - lot.At(hw - 2.8f, dep - 3f)), "floodlight_mast");
            string[] kinds = city ? new[] { "food", "salvage" } : new[] { "food", "parts", "scrap" };
            for (int i = 0; i < kinds.Length; i++)
            {
                var p = lot.At((i - (kinds.Length - 1) * 0.5f) * Mathf.Min(6.5f, w / (kinds.Length + 1)), dep - 3.2f);
                if (InLot(p, lot)) Mark(SpotKind.Stall, p, Yaw(lot.front), null, kinds[i]);
            }
            Mark(SpotKind.Board, lot.At(-hw + 1.8f, 1.6f), Yaw(-lot.front));
            Mark(SpotKind.Fire, lot.At(-w * 0.25f, dep * 0.3f), 0f);
            Mark(SpotKind.Boss, lot.At(-w * 0.25f + 2.6f, dep * 0.3f - 0.6f), Yaw(lot.At(-w * 0.25f, dep * 0.3f) - lot.At(-w * 0.25f + 2.6f, dep * 0.3f - 0.6f)));
            Mark(SpotKind.Birds, c + new Vector2(2.5f, 2.5f), 0f);
            if (InLot(lot.At(hw - 1f, dep * 0.5f), lot)) Prop("PhoneBooth0", lot.At(hw - 1f, dep * 0.5f), Yaw(lot.Across));
            for (int i = 0; i < 6; i++) Decor("Litter" + rnd.Next(2), lot.At(((float)rnd.NextDouble() - 0.5f) * (w - 2f), 1f + (float)rnd.NextDouble() * (dep - 2f)), rnd.Next(360));
        }

        void DressPark(TownLot lot, System.Random rnd)
        {
            float w = lot.Width, dep = lot.Depth, hw = w * 0.5f, rp = Mathf.Min(w, dep) * 0.3f;
            var c = lot.At(0f, dep * 0.5f);
            Prop("Pond0", c, 0f);
            for (float x = -hw + 2f; x <= hw - 2f; x += 6.5f)
            for (float y = 2f; y <= dep - 2f; y += 6.5f)
            {
                float jx = x + ((float)rnd.NextDouble() - 0.5f) * 3f, jy = y + ((float)rnd.NextDouble() - 0.5f) * 3f;
                var l = lot.At(jx, jy);
                if (Mathf.Abs(jx) < 2.4f || Mathf.Abs(jy - dep * 0.5f) < 2.4f || Mathf.Abs(new Vector2(jx, jy - dep * 0.5f).magnitude - rp) < 2f) continue;
                if (!InLot(l, lot) || !Query(l, out var h) || h.feature != FPark) continue;
                double r = rnd.NextDouble();
                string id = r < 0.7 ? "ParkTree" + rnd.Next(2) : r < 0.8 ? "Pine0" : "Bush" + (rnd.Next(2) * 2);
                Prop(id, l, rnd.Next(4) * 90f + (float)rnd.NextDouble() * 20f);
            }
            for (int i = 0; i < 4; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 0.5f;
                var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (rp + 1.4f);
                if (!InLot(p, lot)) continue;
                Prop("Bench0", p, Yaw(p - c) + 180f);
                if (i % 2 == 1) Prop("Bin0", p + new Vector2(-(p - c).y, (p - c).x).normalized * 1.3f, 0f);
                var lp = c + new Vector2(Mathf.Cos(a + 0.35f), Mathf.Sin(a + 0.35f)) * (rp - 1.4f);
                if (i % 2 == 0 && InLot(lp, lot)) Mark(SpotKind.Lamp, lp, Yaw(c - lp), "streetlamp_gas");
            }
            // hedges along the front and back edges, open where the paths come in
            for (int side = 0; side < 2; side++)
            {
                float y = side == 0 ? 0.5f : dep - 0.5f;
                for (float x = -hw + 1.4f; x <= hw - 1.4f; x += 2.4f)
                    if (Mathf.Abs(x) > 2.4f) Prop("Hedge0", lot.At(x, y), Yaw(side == 0 ? lot.front : -lot.front));
            }
            Mark(SpotKind.Birds, c + new Vector2(rp, 0f), 0f);
            for (int i = 0; i < 3; i++) Decor("Litter" + rnd.Next(2), lot.At(((float)rnd.NextDouble() - 0.5f) * 1.6f, 1f + (float)rnd.NextDouble() * (dep - 2f)), rnd.Next(360));
        }

        void DressGreen(TownLot lot, System.Random rnd)
        {
            float w = lot.Width, dep = lot.Depth, hw = w * 0.5f;
            var c = lot.At(0f, dep * 0.5f);
            Prop("Well0", c, Yaw(lot.front));
            foreach (var (x, y) in new[] { (-hw + 2.5f, 2.5f), (hw - 2.5f, dep - 2.5f), (-hw + 2.5f, dep - 2.5f) }) Prop("ParkTree" + rnd.Next(2), lot.At(x, y), rnd.Next(4) * 90f);
            Prop("Bench0", lot.At(-3.6f, dep * 0.5f + 2.4f), Yaw(lot.At(-3.6f, dep * 0.5f + 2.4f) - c));
            Prop("Bench0", lot.At(3.6f, dep * 0.5f - 2.4f), Yaw(lot.At(3.6f, dep * 0.5f - 2.4f) - c));
            Mark(SpotKind.Lamp, lot.At(-1.6f, 1f), Yaw(-lot.front), "streetlamp_pole");
            Mark(SpotKind.Lamp, lot.At(1.6f, dep - 1f), Yaw(lot.front), "streetlamp_pole");
            Mark(SpotKind.Fire, lot.At(-w * 0.25f, dep * 0.75f), 0f);
            Mark(SpotKind.Boss, lot.At(-w * 0.25f + 2.6f, dep * 0.75f - 0.4f), Yaw(-lot.Across));
            Mark(SpotKind.Board, lot.At(hw - 1.6f, 1.4f), Yaw(-lot.front));
            Mark(SpotKind.Stall, lot.At(w * 0.25f, dep - 2.8f), Yaw(lot.front), null, "food");
            Mark(SpotKind.Birds, lot.At(w * 0.25f, dep * 0.4f), 0f);
        }

        void DressHangout(TownLot lot, System.Random rnd)
        {
            float w = lot.Width, dep = lot.Depth, hw = w * 0.5f;
            var c = lot.At(0f, dep * 0.55f);
            Prop("BurnBarrel0", c, 0f, false, false, "gang");
            Prop("Graffiti0", lot.At(-hw + 1.8f, dep - 0.6f), Yaw(-lot.front));
            Prop("Graffiti1", lot.At(hw - 1.8f, dep - 0.6f), Yaw(-lot.front));
            Prop("Sandbags0", lot.At(w * 0.2f, 1.2f), Yaw(lot.front));
            if (w >= 12f) Prop("CarStack" + rnd.Next(2), lot.At(-hw + 1.6f, dep * 0.45f), Yaw(lot.front) + 90f);
            Prop("Crate", lot.At(hw - 1.5f, dep * 0.4f), rnd.Next(360), true, true);
            Prop("Crate", lot.At(hw - 2.4f, dep * 0.5f), rnd.Next(360), true, true);
            Prop("OilDrum0", lot.At(1.6f, dep * 0.55f + 1.4f), 0f);
            for (int i = 0; i < 5; i++) Decor(i < 2 ? "TrashBags0" : "Litter" + rnd.Next(2), lot.At(((float)rnd.NextDouble() - 0.5f) * (w - 2f), 1f + (float)rnd.NextDouble() * (dep - 2f)), rnd.Next(360));
            Mark(SpotKind.Gang, c, Yaw(lot.front));
            if (st.kind == Biome.City && st.index != 0) Mark(SpotKind.Dogs, lot.At(-hw + 2f, dep - 2f), 0f);
        }

        // ------------------------------------------------------------------ streets

        float EndClear(int ji)
        {
            if (ji < 0) return 1f;
            var j = junctions[ji];
            return j.ring > 0f ? j.ring + j.walk + 2f : j.box + j.walk + 1.5f;
        }

        void DressStreet(TownStreet s, System.Random rnd)
        {
            var nrm = new Vector2(-s.dir.y, s.dir.x);
            float e0 = EndClear(s.juncA), e1 = s.len - EndClear(s.juncB);
            bool city = st.kind == Biome.City, village = st.kind == Biome.Village;
            if (s.kind == StreetKind.Alley) { DressAlley(s, nrm, rnd); return; }
            if (s.kind == StreetKind.Pedestrian) { DressPedestrian(s, nrm, e0, e1, rnd); return; }
            if (s.kind == StreetKind.Lane)
            {
                for (float t = e0 + 8f; t < e1; t += 38f) Mark(SpotKind.Lamp, s.a + s.dir * t + nrm * (s.half + 0.7f), Yaw(-nrm), "streetlamp_pole");
                return;
            }
            float spacing = village ? 26f : city ? 18f : 22f;
            for (int sd = -1; sd <= 1; sd += 2)
            {
                var side = nrm * sd;
                // lamps by the kerb
                for (float t = e0 + (sd > 0 ? 1f : 1f + spacing * 0.5f); t < e1 - 0.5f; t += spacing)
                {
                    var l = s.a + s.dir * t + side * (s.half + 0.5f);
                    if (!IsWalk(l)) continue;
                    Mark(SpotKind.Lamp, l, Yaw(-side), LampPiece(l));
                    Take(l, 0.6f);
                }
                if (village || s.walk < 1.4f) continue;
                // the bus stop on an arterial
                float lat = s.half + s.walk - 0.55f;
                if ((s.kind == StreetKind.Arterial || s.kind == StreetKind.Ring) && s.walk >= 2.4f && e1 - e0 > 24f && rnd.NextDouble() < 0.4)
                {
                    var l = s.a + s.dir * ((e0 + e1) * 0.5f + sd * 3f) + side * (s.half + s.walk - 0.75f);
                    if (IsWalk(l) && Room(l, 1.8f)) { Prop("BusStop0", l, Yaw(side)); Take(l, 1.8f); }
                }
                // street furniture along the outer edge of the sidewalk
                for (float t = e0 + 2.5f; t < e1 - 2f; t += 5.5f)
                {
                    var l = s.a + s.dir * t + side * lat;
                    if (!IsWalk(l) || !Room(l, 0.9f)) continue;
                    double r = rnd.NextDouble();
                    bool core = l.magnitude < radius * 0.6f;
                    if (r < 0.06 && core) { Prop("Bench0", l, Yaw(side)); Take(l, 1f); }
                    else if (r < 0.13) { Prop("Bin0", l, Yaw(side)); Take(l, 0.5f); }
                    else if (r < 0.17) { Prop("Hydrant0", s.a + s.dir * t + side * (s.half + 0.45f), Yaw(side)); }
                    else if (r < 0.2 && city && core) { Prop("Planter0", l, Yaw(side)); Take(l, 0.7f); }
                    else if (r < 0.48) Decor("Litter" + rnd.Next(2), s.a + s.dir * t + side * (s.half + (float)rnd.NextDouble() * s.walk), rnd.Next(360));
                    else if (r < 0.52) Decor("TrashBags0", l, rnd.Next(360));
                }
                // cars left at the kerb on the side streets
                if (s.kind == StreetKind.Local && !s.path)
                    for (float t = e0 + 4f; t < e1 - 4f; t += 6.5f)
                        if (rnd.NextDouble() < (city ? 0.2 : 0.11))
                            Mark(SpotKind.Wreck, s.a + s.dir * t + side * (s.half - 1.15f), Yaw(sd < 0 ? s.dir : -s.dir) + ((float)rnd.NextDouble() - 0.5f) * 8f);
            }
        }

        void DressAlley(TownStreet s, Vector2 nrm, System.Random rnd)
        {
            bool city = st.kind == Biome.City;
            for (float t = 3f; t < s.len - 3f; t += 3.5f)
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    var l = s.a + s.dir * t + nrm * sd * (s.half - 0.3f);
                    double r = rnd.NextDouble();
                    if (r < 0.3) Decor("TrashBags0", l, rnd.Next(360));
                    else if (r < 0.62) Decor("Litter" + rnd.Next(2), l, rnd.Next(360));
                    else if (r < 0.7) Prop("OilDrum0", l, 0f);
                    else if (r < 0.74) Prop("Crate", l, rnd.Next(360), true, true);
                }
            if (rnd.NextDouble() < 0.45)
            {
                int sd = rnd.Next(2) * 2 - 1;
                Prop("Graffiti" + rnd.Next(2), s.a + s.dir * (s.len * (0.3f + 0.4f * (float)rnd.NextDouble())) + nrm * sd * (s.half + 0.45f), Yaw(nrm * sd));
            }
            if (city && rnd.NextDouble() < 0.2) Prop("BurnBarrel0", s.a + s.dir * (s.len * 0.5f) + nrm * (s.half - 0.5f), 0f);
            if (rnd.NextDouble() < 0.35) Mark(SpotKind.Rats, s.a + s.dir * (s.len * 0.5f), 0f);
            if (city && st.index != 0 && rnd.NextDouble() < 0.15) Mark(SpotKind.Dogs, s.a + s.dir * (s.len * 0.3f), 0f);
        }

        void DressPedestrian(TownStreet s, Vector2 nrm, float e0, float e1, System.Random rnd)
        {
            foreach (float t in new[] { e0 - 1.2f, e1 + 1.2f })
                for (int k = -1; k <= 1; k++) Prop("Bollard0", s.a + s.dir * t + nrm * (k * (s.half - 0.9f)), 0f);
            int n = 0;
            for (float t = e0 + 3f; t < e1 - 2f; t += 7f, n++)
            {
                int sd = n % 2 == 0 ? 1 : -1;
                Prop("Planter0", s.a + s.dir * t + nrm * sd * (s.half - 1f), Yaw(-nrm * sd));
                if (n % 2 == 1) Prop("Bench0", s.a + s.dir * (t + 3f) + nrm * -sd * (s.half - 0.9f), Yaw(nrm * sd));
                if (n % 2 == 0) Mark(SpotKind.Lamp, s.a + s.dir * (t + 3.5f) + nrm * -sd * (s.half - 0.4f), Yaw(nrm * sd), "streetlamp_gas");
                Decor("Litter" + rnd.Next(2), s.a + s.dir * (t + 1.5f) + nrm * (((float)rnd.NextDouble() - 0.5f) * s.half), rnd.Next(360));
            }
            Prop("Statue0", s.a + s.dir * (s.len * 0.5f), Yaw(s.dir));
            Mark(SpotKind.Birds, s.a + s.dir * (s.len * 0.5f + 3f), 0f);
        }

        void DressJunction(TownJunction j, System.Random rnd)
        {
            bool city = st.kind == Biome.City;
            if (j.ring > 0f)
            {
                Prop(j.p.sqrMagnitude < 1f ? (city ? "Fountain0" : "Statue0") : "ParkTree" + rnd.Next(2), j.p, Yaw(Vector2.down));
                for (int i = 0; i < 4; i++)
                {
                    float a = (i + 0.5f) * Mathf.PI * 0.5f;
                    var l = j.p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (j.ring + 0.5f);
                    if (IsWalk(l)) { Mark(SpotKind.Lamp, l, Yaw(j.p - l), "streetlamp_cobra"); Take(l, 0.6f); }
                }
                return;
            }
            bool arterial = false; int legs = 0;
            foreach (var s in streets) if (s.juncA == j.id || s.juncB == j.id) { legs++; if (s.kind == StreetKind.Arterial || s.kind == StreetKind.Ring) arterial = true; }
            if (legs < 3) return;
            float o = j.box + 0.6f, f = j.box + Mathf.Max(0.8f, j.walk - 0.7f);
            for (int q = 0; q < 4; q++)
            {
                float sx = q == 0 || q == 3 ? 1f : -1f, sy = q < 2 ? 1f : -1f;
                var corner = j.p + new Vector2(sx * o, sy * o);
                if (!IsWalk(corner) || !Room(corner, 0.8f)) continue;
                bool diag = q == 0 || q == 2;
                if (city && arterial && diag) { Prop("TrafficLight0", corner, Yaw(j.p - corner)); Take(corner, 0.8f); continue; }
                if (!city && !arterial && !diag && rnd.NextDouble() < 0.6) { Prop("StopSign0", corner, Yaw(j.p - corner) + 45f); Take(corner, 0.5f); continue; }
                var outer = j.p + new Vector2(sx * f, sy * f);
                if (!IsWalk(outer) || !Room(outer, 0.8f)) continue;
                double r = rnd.NextDouble();
                if (st.kind != Biome.Village && r < 0.2) { Prop("PhoneBooth0", outer, Yaw(new Vector2(sx, 0f))); Take(outer, 0.9f); }
                else if (r < 0.45) { Prop("Bin0", outer, 0f); Take(outer, 0.5f); }
                else if (r < 0.6) { Prop("Hydrant0", corner, 0f); Take(corner, 0.4f); }
            }
        }

        // ------------------------------------------------------------------ people

        void Beats()
        {
            foreach (var b in blocks)
            {
                if (b.l == null || b.r == null || b.b == null || b.t == null) continue;
                float l = b.l.walk * 0.5f + 0.4f, r = b.r.walk * 0.5f + 0.4f, d = b.b.walk * 0.5f + 0.4f, u = b.t.walk * 0.5f + 0.4f;
                var c = new[] { new Vector2(b.min.x - l, b.min.y - d), new Vector2(b.max.x + r, b.min.y - d), new Vector2(b.max.x + r, b.max.y + u), new Vector2(b.min.x - l, b.max.y + u) };
                var beat = new Vector2[4];
                bool cw = (beats.Count & 1) == 1;
                for (int i = 0; i < 4; i++) beat[i] = World(c[cw ? 3 - i : i]);
                beats.Add(beat);
            }
            if (beats.Count == 0 && streets.Count > 0)
            {
                // villages: up and down the high street's footpaths, round the green
                var s = streets[0];
                float y = s.half + s.walk * 0.5f;
                beats.Add(new[] { World(new Vector2(-26f, y)), World(new Vector2(26f, y)), World(new Vector2(26f, -y)), World(new Vector2(-26f, -y)) });
                foreach (var lot in lots)
                    if (lot.use == LotUse.Green)
                        beats.Add(new[] { World(lot.At(-lot.Width * 0.5f + 1f, 0.6f)), World(lot.At(lot.Width * 0.5f - 1f, 0.6f)), World(lot.At(lot.Width * 0.5f - 1f, lot.Depth - 1f)), World(lot.At(-lot.Width * 0.5f + 1f, lot.Depth - 1f)) });
            }
        }
    }
}
