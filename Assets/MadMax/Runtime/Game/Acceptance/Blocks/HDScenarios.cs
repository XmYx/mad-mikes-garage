using System.Collections;
using System.Collections.Generic;
using MadMax.Animals;
using MadMax.Building;
using MadMax.Items;
using MadMax.Rendering;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>The HD asset pack outside vehicles and characters (hd.*): a town streams in with HD buildings, a hit
    /// carves a wall and the HD model is clipped there, a placed piece of furniture, a dropped item, a held tool, a
    /// hotbar icon and a herd of animals all show their HD models. Blocked (not failed) when the HD catalog is not built
    /// (MadMax/HD/Build HD Catalog) or the assets are missing.</summary>
    public static class HDScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new HDWorld();
        }
    }

    class HDWorld : Scenario
    {
        public override string Id => "hd.world";
        public override float Timeout => 120f;

        static readonly string[] Buildings = { "BrickHouse0", "BrickHouse1", "Farmhouse0", "Farmhouse1", "Shop0", "Shop1", "Tower0", "Tower1", "Tower2", "Tower3", "Shack0", "Shack1", "Shack2" };

        static bool IsBuilding(string id) => id != null && System.Array.IndexOf(Buildings, id) >= 0;

        static int ShownHD(HDVisual v)
        {
            int n = 0;
            foreach (var r in v.Renderers) if (r && r.enabled && !r.forceRenderingOff) n++;
            return n;
        }

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var t = DeformableTerrain.Instance;
            if (!HDAssets.Enabled) { c.Block("HD visuals are off (--no-hd / MadMax > Dev > Voxel Visuals)"); yield break; }
            bool anyBuilding = false;
            foreach (var id in Buildings) anyBuilding |= HDAssets.Has(HDDomain.World, id);
            if (!anyBuilding) { c.Block("no HD building in Resources/HDGen: run MadMax/HD/Build HD Catalog after the HD export"); yield break; }
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }

            // ---- a town streams in with HD buildings
            Settlement town = null;
            float best = float.MaxValue;
            var me = g.Player.transform.position;
            foreach (var st in t.World.settlements)
            {
                float d = (st.pos - new Vector2(me.x, me.z)).sqrMagnitude;
                if (d < best) { best = d; town = st; }
            }
            if (town == null) { c.Block("no settlement in this world"); yield break; }
            var centre = new Vector3(town.pos.x, 0f, town.pos.y);
            centre.y = t.Height(centre.x, centre.z) + 0.3f;
            g.Player.Teleport(centre, 0f);
            c.Fixture($"player teleported to settlement {town.index} ({town.kind}) to stream it in");
            DestructibleVoxels house = null;
            HDVisual houseHD = null;
            int dressed = 0, buildings = 0;
            float t0 = Time.time;
            while (Time.time - t0 < 30f)
            {
                dressed = 0; buildings = 0; house = null;
                foreach (var d in DestructibleVoxels.All)
                {
                    if (!d || (d.transform.position - centre).sqrMagnitude > 90f * 90f || !IsBuilding(d.TemplateId)) continue;
                    buildings++;
                    if (!d.TryGetComponent<HDVisual>(out var v) || ShownHD(v) == 0) continue;
                    dressed++;
                    float dist = (d.transform.position - centre).sqrMagnitude;
                    if (!house || dist < (house.transform.position - centre).sqrMagnitude) { house = d; houseHD = v; }
                }
                if (dressed >= 2) break;
                yield return new WaitForSeconds(0.5f);
            }
            c.Metric("town_buildings", buildings, "");
            c.Metric("town_buildings_hd", dressed, "");
            if (!c.Check(house && dressed > 0, $"the town streams in with HD buildings ({dressed} of {buildings} within 90 m)")) yield break;
            var voxelR = house.GetComponent<Renderer>();
            c.Check(voxelR && voxelR.forceRenderingOff && HDVisual.IsHost(voxelR), "the voxel mesh under the HD building stays dark (collider and carving only)");
            c.Check(house.GetComponent<Collider>(), "the building keeps its voxel collider");
            g.Player.Teleport(house.transform.position + house.transform.rotation * new Vector3(0f, 0.3f, -9f), house.transform.eulerAngles.y);
            if (g.cameraRig) g.cameraRig.LookToward(house.transform.position - g.Player.transform.position);
            yield return new WaitForSeconds(1f);
            c.Screenshot("hd_town");
            yield return null;

            // ---- hitting a wall carves it; the HD model is clipped and the broken voxel faces show
            var hb = voxelR.bounds;
            var from = hb.center + house.transform.rotation * Vector3.back * (hb.extents.magnitude + 2f);
            from.y = house.transform.position.y + 1.4f;
            var aim = new Vector3(hb.center.x, from.y, hb.center.z) - from;
            Vector3 hitAt, dir = aim.normalized;
            if (Physics.Raycast(from, dir, out var hit, aim.magnitude + 5f, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<DestructibleVoxels>() == house) hitAt = hit.point;
            else { hitAt = house.GetComponent<Collider>().ClosestPoint(from); c.Note("no clear ray to the wall: hitting its closest point"); }
            int before = house.VoxelCount;
            house.ApplyHit(hitAt, dir, 1.5f, 0.6f, null);
            c.Fixture("one hit (power 1.5, radius 0.6 m: a hole of about a metre) on the nearest wall, applied directly");
            yield return new WaitForSeconds(0.6f);
            if (!c.Check(house, "the building survives the hit")) yield break;
            var carve = house.GetComponent<HDCarve>();
            c.Metric("voxels_removed", before - house.VoxelCount, "");
            c.Check(house.VoxelCount < before, "the hit carved voxels out of the template copy");
            c.Check(carve && carve.Active && carve.CarvedCells > 0, $"the HD visual tracks the carved cells ({(carve ? carve.CarvedCells : 0)})");
            bool clipped = false;
            foreach (var r in houseHD.Renderers) if (r && r.sharedMaterial && r.sharedMaterial.HasProperty("_CarveOn") && r.sharedMaterial.GetFloat("_CarveOn") > 0.5f) { clipped = true; break; }
            c.Check(clipped, "the HD materials now clip against the carve mask");
            var rim = house.transform.Find("HDRim");
            var rimMesh = rim ? rim.GetComponent<MeshFilter>().sharedMesh : null;
            c.Check(rimMesh && rimMesh.vertexCount > 0, "the broken faces of the remaining voxels are drawn (rim)");
            c.Check(ShownHD(houseHD) > 0, "the building still shows its HD model");
            c.Screenshot("hd_carved");
            yield return null;

            // ---- furniture, a dropped item, a held tool, an icon
            if (!TestWorld.Pad(6f, out var pad)) pad = g.Player.transform.position + g.Player.transform.forward * 3f;
            g.Player.Teleport(pad + new Vector3(0f, 0.3f, -4f), 0f);
            yield return new WaitForSeconds(0.4f);
            Vector3 P(float x, float z) { var q = pad + new Vector3(x, 0f, z); q.y = t.Height(q.x, q.z); return q; }
            var spawned = new List<GameObject>();
            string pieceId = HDAssets.Has(HDDomain.Furniture, "workbench") ? "workbench" : null;
            if (pieceId == null) foreach (var d in FurnitureLibrary.All) if (d.mesh && d.link == UtilityKind.None && HDAssets.Has(HDDomain.Furniture, d.id)) { pieceId = d.id; break; }
            if (pieceId == null) c.Note("no HD furniture exported yet: furniture check skipped");
            else
            {
                var piece = FurnitureLibrary.Spawn(pieceId, g.Build.Structures, P(-1.5f, 0f), Quaternion.identity, g.propMaterial);
                if (piece) spawned.Add(piece.gameObject);
                var pv = piece ? piece.GetComponent<HDVisual>() : null;
                c.Check(pv && ShownHD(pv) > 0 && piece.GetComponent<Renderer>().forceRenderingOff, $"a placed {pieceId} shows its HD model over the voxel collider");
                if (piece) { piece.SetDye(1); c.Check(pv && pv.Renderers.Count > 0 && pv.Renderers[0].sharedMaterial && pv.Renderers[0].sharedMaterial.name.Contains("_dye"), "a dye tints the HD piece"); piece.SetDye(0); }
            }
            string itemId = HDAssets.Has(HDDomain.Item, "food_apple") ? "food_apple" : HDAssets.Has(HDDomain.Item, "drink_water") ? "drink_water" : null;
            if (itemId == null) c.Note("no HD item exported yet: item check skipped");
            else
            {
                var w = WorldItem.Create(itemId, 1, -1f, g.propMaterial, P(1.5f, 0f) + Vector3.up * 0.3f, Quaternion.identity);
                spawned.Add(w.gameObject);
                var hd = w.transform.Find("HD");
                c.Check(hd && hd.GetComponentInChildren<Renderer>(), $"a dropped {itemId} lies in the world as its HD model");
                c.Check(ItemsScenarios.HasMesh(w), "it keeps its voxel mesh for the collider and the item tests");
            }
            if (HDAssets.Has(HDDomain.Tool, "tool_wrench"))
            {
                g.Player.Equip(ToolLibrary.Create("tool_wrench", g.propMaterial));
                yield return null;
                var tv = g.Player.Tool ? g.Player.Tool.GetComponent<HDVisual>() : null;
                c.Check(tv && ShownHD(tv) > 0, "the held wrench is the HD tool at the grip");
                MadMax.Rendering.IconRenderer.Get("tool_wrench", ToolLibrary.MeshFor("tool_wrench"), 14, true);   // queues the HD icon
                for (int i = 0; i < 10 && !HDIcons.TryGet("tool_wrench", 14, true, out _); i++) yield return null;
                c.Check(HDIcons.TryGet("tool_wrench", 14, true, out var px) && System.Array.Exists(px, p => p.a > 0), "its hotbar icon is rendered from the HD model");
            }
            else c.Note("no HD tools exported yet: tool check skipped");
            c.Screenshot("hd_items");
            yield return null;

            // ---- an animal herd
            string species = HDAssets.Has(HDDomain.Animal, "cow") ? "cow" : HDAssets.Has(HDDomain.Animal, "sheep") ? "sheep" : HDAssets.Has(HDDomain.Animal, "goat") ? "goat" : null;
            if (species == null) c.Note("no HD animals exported yet: herd check skipped");
            else
            {
                int partsHD = 0, parts = 0;
                for (int i = 0; i < 3; i++)
                {
                    var a = Animal.Spawn(AnimalLibrary.Get(species), P(-3f + i * 2.5f, 5f) + Vector3.up * 0.1f, 180f, g.propMaterial, "t:hd" + i);
                    spawned.Add(a.gameObject);
                    var rig = a.transform.Find("Rig");
                    for (int k = 0; rig && k < rig.childCount; k++) { parts++; if (rig.GetChild(k).Find("HD")) partsHD++; }
                }
                c.Metric("herd_parts_hd", partsHD, "of " + parts);
                c.Check(parts > 0 && partsHD == parts, $"a herd of three {species} wears HD part meshes on every rig part ({partsHD}/{parts})");
                yield return new WaitForSeconds(0.8f);
                c.Screenshot("hd_herd");
                yield return null;
            }
            foreach (var go in spawned) if (go) Object.Destroy(go);
        }
    }
}
