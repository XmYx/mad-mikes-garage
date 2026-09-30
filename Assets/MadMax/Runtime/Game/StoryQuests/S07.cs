using MadMax.Building;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>S07 AN HONEST FISH: Milt's bench and bait barrel on the bank, Hal's weigh-in (a table with the scale, the fish
    /// crate, pennants). Milt lends his spare rod (or, if you have one, pays for your trouble); a fish landed at this pond
    /// counts as the catch; crouching by the table finds the shim under the pan.</summary>
    public partial class WastelandGame
    {
        int s07Fish = -1;
        string s07Caught, s07CaughtSeen, s07Choice = "-";

        partial void Scene_S07()
        {
            if (!StoryAnchors.Has("s07_weigh") || !Build || !Build.Structures) return;
            PutAt("s07_scale", "table", Vector3.zero, 0f);
            PutAt("s07_scale", "crate", new Vector3(1.4f, 0f, 0.2f), 20f);
            PutAt("s07_weigh", "bench", new Vector3(-2.6f, 0f, -0.6f), 90f);
            foreach (float x in new[] { -3.5f, 3.5f }) PutAt("s07_scale", "flag", new Vector3(x, 0f, -0.5f), 0f);
            PutAt("milt", "bench", new Vector3(-2.2f, 0f, -1.4f), 0f);
            PutAt("milt", "barrel", new Vector3(2.4f, 0f, -1.2f), 0f);
        }

        /// <summary>"CAUGHT A 1.2 KG PERCH (TROPHY)" → "1.2 KG PERCH" (the rod's own landing message).</summary>
        static string S07_Parse(string toast)
        {
            if (string.IsNullOrEmpty(toast)) return null;
            int k = toast.IndexOf(" KG ");
            if (k <= 0) return null;
            int s = toast.LastIndexOf(' ', k - 1);
            string sp = toast.Substring(k + 4);
            int p = sp.IndexOf(" (");
            if (p >= 0) sp = sp.Substring(0, p);
            return toast.Substring(s + 1, k - s - 1) + " KG " + sp;
        }

        partial void Tick_S07()
        {
            if (!StoryAnchors.Has("milt")) return;
            var q = StoryLibrary.Get("S07");
            string choice = Story.Story.Route("S07", "choice");
            if (q != null && (choice != s07Choice || s07Caught != s07CaughtSeen || q.payoff == null)) { s07Choice = choice; s07CaughtSeen = s07Caught; q.payoff = StoryLibrary.S07_Payoff(choice, s07Caught); }

            // Milt's spare rod, or fair credit if you brought your own
            if (!Story.Story.Flag("s07_rod"))
            {
                Story.Story.SetFlag("s07_rod");
                if (Inventory.GetItem("tool_fishing_rod") > 0)
                {
                    Inventory.Add(MadMax.Items.ResourceType.Scrap, 10);
                    Journal.Add("JOB", "AN HONEST FISH: YOU HAVE A ROD, SO MILT PAYS FOR YOUR TROUBLE INSTEAD (+10 SCRAP)");
                }
                else
                {
                    Inventory.AddItem("tool_fishing_rod");
                    Journal.Add("JOB", "AN HONEST FISH: MILT HANDS YOU HIS SPARE ROD. KEEP IT, HE SAYS; IT'S A GOOD ONE (+FISHING ROD)");
                }
            }

            // a fish landed here, by you
            if (!Story.Story.StepDone("S07", "catch"))
            {
                int n = Inventory.GetItem("food_fish_raw");
                var m = StoryAnchors.Get("milt"); var me = Player.transform.position;
                if (s07Fish >= 0 && n > s07Fish && new Vector2(me.x - m.x, me.z - m.z).magnitude < 70f)
                {
                    s07Caught = S07_Parse(toastText);
                    Story.Story.Note("s07:catch");
                    Journal.Add("JOB", "AN HONEST FISH: " + (s07Caught != null ? "A " + s07Caught : "A FISH") + ", CAUGHT FAIR IN MILT'S POND");
                }
                s07Fish = n;
            }

            // under the pan
            var cur = q != null ? Story.Story.Current(q) : null;
            if (cur != null && cur.id == "scale" && !Current && (Player.Crouching || Player.crouch))
            {
                var sc = StoryAnchors.Get("s07_scale"); var me = Player.transform.position;
                if (new Vector2(me.x - sc.x, me.z - sc.z).magnitude < 1.9f)
                {
                    Story.Story.Note("s07:shim");
                    Toast("UNDER THE PAN: A LEAD SHIM WEDGED ON THE SPRING. EVERYTHING ON THIS SCALE WEIGHS HALF AGAIN");
                }
            }

            // how it was settled, for the banter later
            if (choice != null && !Story.Story.Flag("s07_public") && !Story.Story.Flag("s07_quiet"))
                Story.Story.SetFlag(choice == StoryLibrary.S07Public ? "s07_public" : "s07_quiet");
        }
    }
}
