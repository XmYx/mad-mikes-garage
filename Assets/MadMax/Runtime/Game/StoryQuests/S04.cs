using System.Collections.Generic;
using MadMax.Building;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>S04 THE HOUSE THAT WALKED: Della's 4 x 4 m timber house raised on posts over a slope (kitchen half and door
    /// downhill), the corner post fallen into a washed-out gully, the rain barrel whose overflow did it, a pantry full of jars
    /// and bricks, a flag where a ditch would take the water away. Checks that a support really stands on the ground and
    /// carries the floor, that the water was sent elsewhere and that the kitchen can be walked into.</summary>
    public partial class WastelandGame
    {
        static readonly HashSet<string> S04Supports = new HashSet<string> { "post", "mine_prop", "sandbag_wall", "wall_wood", "wall_plank", "wall_brick", "wall_brick_fired", "wall_concrete", "wall_panel", "wall_scrap", "foundation_wood", "foundation_stone" };
        readonly HashSet<uint> s04Warned = new HashSet<uint>();
        Placeable s04Tile, s04Pantry;
        string s04Drain = "-"; bool s04WallSeen;

        partial void Scene_S04()
        {
            if (!StoryAnchors.Has("s04_house") || !Build || !Build.Structures) return;
            var a = StoryAnchors.Get("s04_house"); var r = Quaternion.Euler(0f, StoryAnchors.Yaw("s04_house"), 0f);
            Vector3 W(float x, float z) => a + r * new Vector3(x, 0f, z);
            float top = float.MinValue;
            foreach (float x in new[] { -2f, 0f, 2f }) foreach (float z in new[] { -2f, 0f, 2f }) { var p = W(x, z); top = Mathf.Max(top, terrain.HeightNoLoad(p.x, p.z)); }
            float floorY = top + 0.25f, wallY = floorY + 0.12f;
            Placeable Put(string id, float x, float z, float y, float turn)
            {
                var p = W(x, z); p.y = y;
                var pl = FurnitureLibrary.Spawn(id, Build.Structures, p, r * Quaternion.Euler(0f, turn, 0f), propMaterial);
                if (pl) storyProps.Add(pl.Id);
                return pl;
            }
            // stilts (the downhill kitchen corner's is gone), floor, walls, roof
            foreach (float x in new[] { -2f, 0f, 2f }) foreach (float z in new[] { -2f, 0f, 2f }) if (x != -2f || z != -2f) Put("post", x, z, floorY - 2.36f, 0f);
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) Put("floor_wood", x, z, floorY, 0f);
            Put("wall_wood", -1f, -2f, wallY, 180f); Put("wall_wood", 1f, -2f, wallY, 180f);
            Put("wall_wood_window", -1f, 2f, wallY, 0f); Put("wall_wood", 1f, 2f, wallY, 0f);
            Put("wall_wood", 2f, -1f, wallY, 90f); Put("wall_wood_window", 2f, 1f, wallY, 90f);
            Put("wall_wood_window", -2f, -1f, wallY, -90f); Put("doorway_wood", -2f, 1f, wallY, -90f);
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) Put("roof_flat", x, z, wallY + 2.42f, 0f);
            // the kitchen (downhill half) and the rest of the room
            Put("kitchen_counter", -1.3f, -1.45f, wallY, 0f);
            var pantry = Put("locker", -0.3f, -1.5f, wallY, 0f);
            if (pantry && pantry.TryGetComponent<Container>(out var box))
            {
                box.title = "DELLA'S PANTRY";
                box.inventory.AddItem("food_jam", 3); box.inventory.AddItem("food_can", 3);
                box.inventory.Add(MadMax.Items.ResourceType.Stone, 12);                        // her spare bricks
                pantry.Dirty();
            }
            Put("stove", 1.1f, -1.5f, wallY, 0f);
            Put("table", 1.0f, 0.7f, wallY, 0f);
            Put("chair", 1.0f, 1.45f, wallY, 180f);
            Put("rug", 0.2f, 0.4f, wallY, 90f);
            // the water: the barrel at the uphill back corner, its runnel along the back wall, the gully under the kitchen corner
            PutAt("s04_spout", "rain_collector", Vector3.zero, 0f);
            foreach (var (x, z, rad, depth) in new[] { (-2f, -2f, 1.5f, 0.6f), (-3.4f, -2.5f, 1.1f, 0.4f), (-0.4f, -2.7f, 0.7f, 0.25f), (1.4f, -2.8f, 0.7f, 0.15f) })
            {
                var p = W(x, z); p.y = terrain.HeightNoLoad(p.x, p.z);
                terrain.ApplyTerraform((byte)MadMax.World.DeformableTerrain.TerraOp.Dig, p, rad, depth, 0);
            }
            var fallen = W(-2.5f, -2.6f); fallen.y = terrain.Height(fallen.x, fallen.z) + 0.1f;
            var fp = FurnitureLibrary.Spawn("post", Build.Structures, fallen, r * Quaternion.Euler(0f, 30f, 90f), propMaterial);
            if (fp) storyProps.Add(fp.Id);
            PutAt("s04_ditch", "flag", new Vector3(0.8f, 0f, 0f), 0f);
            PutAt("della", "bench", new Vector3(-1.8f, 0f, 0f), 90f);
        }

        partial void Tick_S04()
        {
            if (!StoryAnchors.Has("s04_house")) return;
            var q = StoryLibrary.Get("S04");
            string drain = Story.Story.Route("S04", "drain"); bool wall = Story.Story.Flag("s04_wall");
            if (q != null && (drain != s04Drain || wall != s04WallSeen || q.payoff == null)) { s04Drain = drain; s04WallSeen = wall; q.payoff = StoryLibrary.S04_Payoff(drain, wall); }
            if (Story.Story.StepDone("S04", "tell") && !Story.Story.Flag("s04_plan")) { S04_GivePlan(); Story.Story.SetFlag("s04_plan"); Story.Story.Note("s04:plan"); }
            if (Time.frameCount % 20 != 0) return;
            var corner = StoryAnchors.Get("s04_corner");
            if (!s04Tile || !s04Pantry)
                foreach (var p in Placeable.All)
                {
                    if (!p || !IsStoryProp(p)) continue;
                    if (p.id == "floor_wood" && S04_Flat(p.transform.position, corner) < 1.6f) s04Tile = p;
                    if (p.id == "locker" && S04_Flat(p.transform.position, StoryAnchors.Get("s04_kitchen")) < 3f) s04Pantry = p;
                }
            // the pantry emptied (or taken apart)
            if (!Story.Story.StepDone("S04", "unload"))
            {
                var box = s04Pantry ? s04Pantry.GetComponent<Container>() : null;
                if (!box || box.Weight < 0.01f) { Story.Story.Note("s04:unloaded"); if (box) Toast("THE PANTRY IS EMPTY: NOTHING HEAVY OVER THE CORNER NOW"); }
            }
            // a support under the corner that stands on the ground and reaches the floor
            if (!Story.Story.StepDone("S04", "shore") && s04Tile)
            {
                var tile = s04Tile.GetComponent<Renderer>().bounds;
                foreach (var p in Placeable.All)
                {
                    if (!p || IsStoryProp(p) || !S04Supports.Contains(p.id) || S04_Flat(p.transform.position, corner) > 1.8f) continue;
                    var rend = p.GetComponent<Renderer>();
                    if (!rend) continue;
                    var b = rend.bounds;
                    var reach = b; reach.Expand(0.15f);
                    if (StructureSupport.Grounded(p, b) && reach.Intersects(tile))
                    {
                        if (p.id.StartsWith("wall") || p.id == "sandbag_wall") Story.Story.SetFlag("s04_wall");
                        Story.Story.Note("s04:shored");
                        MadMax.Audio.Sfx.Play("creak", corner, 0.7f, 0.8f);
                        Toast("THE KITCHEN SETTLES ONTO THE NEW " + (FurnitureLibrary.Get(p.id)?.name ?? "SUPPORT") + " WITH A LONG CREAK");
                        break;
                    }
                    if (s04Warned.Add(p.Id)) Toast("THAT " + (FurnitureLibrary.Get(p.id)?.name ?? "PIECE") + " ISN'T CARRYING THE FLOOR: IT HAS TO STAND ON THE GROUND AND REACH THE KITCHEN");
                }
            }
            // the barrel moved to the flag (the old one taken down, a new one there)
            if (!Story.Story.StepDone("S04", "drain"))
            {
                bool oldGone = true, newOne = false;
                var ditch = StoryAnchors.Get("s04_ditch"); var spout = StoryAnchors.Get("s04_spout");
                foreach (var p in Placeable.All)
                {
                    if (!p || p.id != "rain_collector") continue;
                    if (IsStoryProp(p) && S04_Flat(p.transform.position, spout) < 2f) oldGone = false;
                    else if (!IsStoryProp(p) && S04_Flat(p.transform.position, ditch) < 4f) newOne = true;
                }
                if (oldGone && newOne) Story.Story.Note("s04:barrel_moved");
            }
            // someone standing on the kitchen floor
            if (!Story.Story.StepDone("S04", "walk") && s04Tile && !Current && !Player.SeatedIn
                && S04_Flat(Player.transform.position, StoryAnchors.Get("s04_kitchen")) < 1.4f && Player.transform.position.y > s04Tile.transform.position.y - 0.3f)
                Story.Story.Note("s04:inside");
        }

        static float S04_Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>Della's porch as a structure plan: two timber foundations, her awning and a bench.</summary>
        void S04_GivePlan()
        {
            const string porch = "foundation_wood,-1,0,0,0,0,0,0;foundation_wood,1,0,0,0,0,0,0;porch_awning,0,0.12,0,0,0,0,0;bench,0,0.12,-0.6,0,0,0,0";
            var saved = StructurePlans.Save();
            var lines = new List<string>(string.IsNullOrEmpty(saved) ? new string[0] : saved.Split('\n'));
            while (lines.Count >= StructurePlans.Max) lines.RemoveAt(0);
            lines.Add(porch);
            StructurePlans.Load(string.Join("\n", lines));
            Journal.Add("PLAN", "DELLA'S PORCH IS IN YOUR BUILD MENU: STRUCTURE, PLAN " + StructurePlans.Count + " (TWO TIMBER FOUNDATIONS, AN AWNING, A BENCH)");
            Toast("DELLA'S PORCH PLAN: BUILD MENU, STRUCTURE");
        }
    }
}
