using MadMax.Building;
using MadMax.Story;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    // P3.2 SHALLOW OR DEEP: the two wreck sites as story props on the sea floor, each with a marker buoy at the surface:
    // the ALBA's spilled crates in wading depth (her cargo tin, a framed photograph, tinned fish) and the MERIDIAN's
    // debris and chart locker in deep water (brass instruments, a tin of letters, binoculars). Salvage = take from them.
    public partial class WastelandGame
    {
        partial void Scene_P3_2()
        {
            if (!Build || !Build.Structures) return;
            if (StoryAnchors.Has("p3_shallow"))
            {
                var alba = PutAt("p3_shallow", "crate", Vector3.zero, 25f);
                P3Fill(alba, "ALBA'S CARGO CRATE", ("story_p3_cargo", 1), ("story_p3_photo", 1), ("food_can_fish", 3));
                PutAt("p3_shallow", "crate", new Vector3(1.6f, 0f, 1.1f), -30f);
                PutAt("p3_shallow", "barrel", new Vector3(-1.5f, 0f, 0.8f), 0f);
                P3Buoy("p3_shallow", new Vector3(3f, 0f, -2f));
            }
            if (StoryAnchors.Has("p3_deep"))
            {
                var locker = PutAt("p3_deep", "crate", Vector3.zero, 0f);
                P3Fill(locker, "MERIDIAN'S CHART LOCKER", ("story_p3_instruments", 1), ("story_p3_letters", 1), ("tool_binoculars", 1));
                PutAt("p3_deep", "tyres", new Vector3(-2.2f, 0f, 1.4f), 40f);
                PutAt("p3_deep", "barrel", new Vector3(2f, 0f, -1.6f), 0f);
                PutAt("p3_deep", "crate", new Vector3(2.4f, 0f, 1.8f), 60f);
                P3Buoy("p3_deep", new Vector3(0f, 0f, 3f));
            }
        }

        partial void Tick_P3_2()
        {
            if (Time.time < p3Check) return;
            p3Check = Time.time + 0.5f;
            string how = Story.Story.Route("P3.2", "salvage");
            if (how != null)
                StoryLibrary.Get("P3.2").payoff = StoryLibrary.P3Deep(how)
                    ? "YOU WALKED THE MERIDIAN'S DECK IN HALVARD'S HELMET AND CAME UP WITH HER BRASS AND HER CHART LOCKER. THE DEEP ONE."
                    : "YOU WADED OUT TO THE ALBA AND BROUGHT HER CARGO IN DRY-SHOD, NEARLY. THE SAFE ONE.";
        }

        /// <summary>Fill a story crate (salvage to take with [E]).</summary>
        static void P3Fill(Placeable crate, string title, params (string id, int n)[] items)
        {
            if (!crate || !crate.TryGetComponent<Container>(out var box)) return;
            box.title = title;
            foreach (var (id, n) in items) box.inventory.AddItem(id, n);
            crate.Dirty();
        }

        /// <summary>A marker buoy: a barrel floating at the surface with a flag on it.</summary>
        void P3Buoy(string anchor, Vector3 local)
        {
            var r = Quaternion.Euler(0f, StoryAnchors.Yaw(anchor), 0f);
            var p = StoryAnchors.Get(anchor) + r * local;
            foreach (var (id, y) in new[] { ("barrel", WorldGen.SeaLevel - 0.55f), ("flag", WorldGen.SeaLevel + 0.3f) })
            {
                var pl = FurnitureLibrary.Spawn(id, Build.Structures, new Vector3(p.x, Mathf.Max(y, terrain.HeightNoLoad(p.x, p.z)), p.z), r, propMaterial);
                if (pl) storyProps.Add(pl.Id);
            }
        }
    }
}
