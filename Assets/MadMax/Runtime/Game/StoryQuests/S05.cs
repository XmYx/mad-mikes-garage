using System.Collections.Generic;
using MadMax.Building;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>S05 NOT THAT KIND OF SHOT: Amos's range (firing-line posts, a loading table, a bench, three battered target
    /// boards 20 m out and an earth berm behind them). While the course runs the boards soak up hits (counted only when the
    /// shooter stands behind the line), a dummy round stops the gun after the second shot, and clearing it ([R]) with the
    /// same gun counts.</summary>
    public partial class WastelandGame
    {
        const int S05Armour = 40;                                                             // board hits while the course runs (restored after every hit)
        readonly Dictionary<uint, int> s05Hits = new Dictionary<uint, int>();
        readonly List<Placeable> s05Boards = new List<Placeable>();
        RangedTool s05Tool, s05Jammed;
        int s05Rounds = -1, s05Fired;
        bool s05JamSet, s05Restored, s05Warned;
        float s05LastShot = -9f;

        partial void Scene_S05()
        {
            if (!StoryAnchors.Has("s05_line") || !Build || !Build.Structures) return;
            foreach (float x in new[] { -3f, 3f }) PutAt("s05_line", "post", new Vector3(x, 0f, 0f), 0f);            // the line
            PutAt("s05_line", "table", new Vector3(-1.6f, 0f, -1.1f), 0f);
            PutAt("s05_line", "bench", new Vector3(1.8f, 0f, -2.2f), 0f);
            PutAt("amos", "weapon_rack", new Vector3(-2.6f, 0f, -0.8f), 90f);
            foreach (float x in new[] { -3f, 0f, 3f })                                              // three battered boards facing the line
            {
                var b = PutAt("s05_targets", "sign", new Vector3(x, 0f, 0f), 180f);
                if (b) { b.hits = 1; b.Dirty(); }
            }
            var t = StoryAnchors.Get("s05_targets"); var o = Quaternion.Euler(0f, StoryAnchors.Yaw("s05_targets"), 0f);
            foreach (float x in new[] { -3.5f, 0f, 3.5f })                                          // the berm: a bank of earth behind the boards
            {
                var p = t + o * new Vector3(x, 0f, 4.5f); p.y = terrain.HeightNoLoad(p.x, p.z);
                terrain.ApplyTerraform((byte)MadMax.World.DeformableTerrain.TerraOp.Dump, p, 3.2f, 1.4f, 0);
            }
        }

        /// <summary>The boards on Amos's line (his or any sign the player put up there).</summary>
        void S05_Boards()
        {
            s05Boards.Clear();
            var t = StoryAnchors.Get("s05_targets");
            foreach (var p in Placeable.All)
                if (p && p.id == "sign" && new Vector2(p.transform.position.x - t.x, p.transform.position.z - t.z).magnitude < 6f) s05Boards.Add(p);
        }

        /// <summary>Standing at or behind the firing line (not downrange of it), on foot.</summary>
        bool S05_AtLine()
        {
            if (Current || Player.SeatedIn) return false;
            var l = StoryAnchors.Get("s05_line"); var r = Quaternion.Euler(0f, StoryAnchors.Yaw("s05_line"), 0f);
            var v = Player.transform.position - l;
            float along = Vector3.Dot(v, r * Vector3.forward), side = Vector3.Dot(v, r * Vector3.right);
            return along <= 0.8f && along >= -5f && Mathf.Abs(side) <= 6f;
        }

        static void S05_Jam(RangedTool gun, bool on)
        {
            var set = typeof(RangedTool).GetProperty("Jammed")?.GetSetMethod(true);                   // the dummy round: the same stoppage a worn gun gets
            if (set != null) set.Invoke(gun, new object[] { on });
        }

        partial void Tick_S05()
        {
            if (!StoryAnchors.Has("s05_line")) return;
            var q = StoryLibrary.Get("S05");
            if (q != null) q.payoff = StoryLibrary.S05_Payoff(Story.Story.Route("S05", "course"), Story.Story.StepDone("S05", "medal"));
            if (Time.frameCount % 15 == 0) S05_Boards();

            // the boards restored: three whole ones on the line
            if (!Story.Story.StepDone("S05", "targets") && Time.frameCount % 15 == 0)
            {
                int whole = 0;
                foreach (var b in s05Boards) if (b && b.hits >= b.MaxHits) whole++;
                if (whole >= 3) Story.Story.Note("s05:targets");
            }
            if (!Story.Story.StepDone("S05", "rules")) return;
            bool assisted = Story.Story.Route("S05", "rules") == StoryLibrary.S05Assisted;
            bool courseOpen = !Story.Story.StepDone("S05", "course") || !Story.Story.StepDone("S05", "jam");
            if (!courseOpen)
            {
                if (!s05Restored)                                                                // back to ordinary boards; Amos oils your kit
                {
                    s05Restored = true;
                    foreach (var b in s05Boards) if (b && b.hits > b.MaxHits) { b.hits = b.MaxHits; b.Dirty(); }
                    if (!Story.Story.Flag("s05_cleaned"))
                    {
                        Story.Story.SetFlag("s05_cleaned");
                        foreach (var kv in new List<KeyValuePair<string, int>>(Inventory.Items))
                            if (kv.Value > 0 && MadMax.Items.ItemCatalog.Category(kv.Key) == MadMax.Items.ItemCategory.Weapon) ToolWear[kv.Key] = 0f;
                        Toast("AMOS CLEANS AND OILS EVERY WEAPON IN YOUR PACK");
                    }
                }
                return;
            }
            bool atLine = S05_AtLine();

            // shots: rounds leaving the held gun
            var gun = Player.Tool as RangedTool;
            if (gun != s05Tool) { s05Tool = gun; s05Rounds = gun ? Rounds(gun.id) : -1; }
            if (gun)
            {
                int left = Rounds(gun.id);
                if (s05Rounds >= 0 && left < s05Rounds)
                {
                    if (atLine) { s05Fired += s05Rounds - left; s05LastShot = Time.time; }
                    else if (!s05Warned) { s05Warned = true; Toast("AMOS: BEHIND THE LINE WHEN YOU SHOOT. THAT ONE DOESN'T COUNT."); }
                }
                s05Rounds = left;
                // the dummy round Amos loaded: the gun stops after the second shot from the line
                if (!Story.Story.StepDone("S05", "jam"))
                {
                    if (!s05JamSet && s05Fired >= 2)
                    {
                        s05JamSet = true; s05Jammed = gun; S05_Jam(gun, true);
                        Toast("AMOS: THE NEXT ONE'S A DUMMY. WHEN IT CLICKS, KEEP IT POINTED AT THE BERM AND CLEAR IT: [R]");
                    }
                    else if (s05JamSet && gun != s05Jammed) { s05Jammed = gun; S05_Jam(gun, true); }       // swapped guns: the dummy's in this one now
                    else if (s05JamSet && gun == s05Jammed && !gun.Jammed)
                    {
                        Story.Story.Note("s05:jam");
                        Toast(atLine ? "AMOS: CLEARED, AND THE MUZZLE NEVER LEFT THE BERM. GOOD." : "AMOS: CLEARED. NEXT TIME, DO IT FROM THE LINE.");
                    }
                }
            }

            // hits on the boards (counted from the line, or just after a shot from it)
            bool fromLine = atLine || Time.time - s05LastShot < 1.5f;
            foreach (var b in s05Boards)
            {
                if (!b) continue;
                if (!s05Hits.ContainsKey(b.Id)) { s05Hits[b.Id] = 0; if (b.hits < S05Armour) { b.hits = S05Armour; b.Dirty(); } continue; }
                if (b.hits >= S05Armour) continue;
                b.hits = S05Armour; b.Dirty();
                if (!fromLine) continue;
                s05Hits[b.Id]++;
                MadMax.Audio.Sfx.Play("ding", b.transform.position + Vector3.up * 1.1f, 0.5f, 1.3f, 40f);
            }
            if (Story.Story.StepDone("S05", "course")) return;
            if (assisted)
            {
                if (s05Fired >= 6) { Story.Story.Note("s05:assisted"); Toast("AMOS: SIX ROUNDS, ALL SAFE. THAT'S THE LESSON."); }
                return;
            }
            int boards = 0;
            foreach (var b in s05Boards) if (b && s05Hits.TryGetValue(b.Id, out int n) && n >= 2) boards++;
            if (boards >= 3)
            {
                Story.Story.Note("s05:course");
                if (s05Fired <= 10) Story.Story.Note("s05:medal");
                Toast("AMOS: EVERY BOARD, TWICE. " + s05Fired + " ROUNDS." + (s05Fired <= 10 ? " THAT'S MEDAL SHOOTING." : ""));
            }
        }
    }
}
