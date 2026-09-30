using MadMax.Animals;
using MadMax.Building;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>S02 THE GOAT HAS A LAWYER: Bess's pen with a gap in it, a trail of chewed cabbage beds, Oswin's yard with a
    /// smoking fire next door; Horace himself (a goat that follows whoever carries his favourite food).</summary>
    public partial class WastelandGame
    {
        Animal s02Goat;
        bool s02Luring, s02Wp1, s02Wp2, s02Paid;
        float s02Check;

        partial void Scene_S02()
        {
            if (!StoryAnchors.Has("s02_pen") || !Build || !Build.Structures) return;
            // the pen: 4 x 4 m of fence, one piece gone on the far side, a trough inside
            foreach (var (x, z, turn) in new[] { (-1f, 2f, 0f), (-1f, -2f, 180f), (1f, -2f, 180f), (-2f, -1f, -90f), (-2f, 1f, -90f), (2f, -1f, 90f), (2f, 1f, 90f) })
                PutAt("s02_pen", "fence_wood", new Vector3(x, 0f, z), turn);
            PutAt("s02_pen", "trough", new Vector3(-0.8f, 0f, -0.9f), 90f);
            PutAt("bess", "bench", new Vector3(0f, 0f, -1.6f), 180f);
            PutAt("bess", "barrel", new Vector3(2.4f, 0f, -1.4f), 0f);
            // the trail: two chewed cabbage beds
            foreach (var t in new[] { "s02_trail1", "s02_trail2" })
            {
                var bed = PutAt(t, "garden_plot", Vector3.zero, 20f);
                if (bed && bed.TryGetComponent<GardenPlot>(out var plot)) { plot.crop = "seed_cabbage"; plot.growth = 0.35f; plot.health = 0.3f; plot.water = 0.5f; bed.Dirty(); }
            }
            // Oswin's yard: a fire with something in it, a bench, two poles with no washing line between them
            PutAt("s02_fire", "campfire", Vector3.zero, 0f);
            PutAt("oswin", "bench", new Vector3(-2.2f, 0f, -1.2f), 90f);
            PutAt("oswin", "post", new Vector3(3.5f, 0f, -2.5f), 0f);
            PutAt("oswin", "post", new Vector3(3.5f, 0f, 1.5f), 0f);
            PutAt("oswin", "barrel", new Vector3(-3.2f, 0f, 1.6f), 0f);
        }

        partial void Tick_S02()
        {
            if (!StoryAnchors.Has("s02_pen")) return;
            var q = StoryLibrary.Get("S02");
            bool home = Story.Story.StepDone("S02", "lure");
            bool lost = Story.Story.Route("S02", "lure") == "HORACE DIDN'T MAKE IT" || Story.Story.Flag("s02_lost");
            if (q != null) q.payoff = StoryLibrary.S02_Payoff(Story.Story.Route("S02", "settle"), lost);

            // Horace: found by his tag, or put back where the story has him (a reload doesn't keep animals)
            if (!s02Goat)
            {
                var tag = StoryTag.Find("s02_goat");
                s02Goat = tag ? tag.GetComponent<Animal>() : null;
                if (!s02Goat && !lost && Time.time > s02Check)
                {
                    s02Check = Time.time + 2f;
                    var def = AnimalLibrary.Get("goat");
                    var at = StoryAnchors.Get(home ? "s02_pen" : "s02_goat");
                    at.y = terrain.HeightNoLoad(at.x, at.z);
                    if (def != null)
                    {
                        s02Goat = Animal.Spawn(def, at, StoryAnchors.Yaw("s02_pen"), propMaterial, "story:s02");
                        s02Goat.name = "Horace";
                        StoryTag.Set(s02Goat.gameObject, "s02_goat");
                        if (home) { s02Goat.owned = true; s02Goat.order = 0; s02Goat.home = at; }
                    }
                }
                if (!s02Goat) return;
            }
            if (!s02Goat.Alive)
            {
                if (!Story.Story.Flag("s02_lost")) { Story.Story.SetFlag("s02_lost"); Story.Story.Note("s02:lost"); Journal.Add("STORY", "HORACE IS DEAD. BESS WILL HAVE TO HEAR IT FROM YOU."); }
                return;
            }

            // the trail: each chewed bed points at the next
            if (!s02Wp1 && Story.Story.StepDone("S02", "chew1") && !Story.Story.StepDone("S02", "find")) { s02Wp1 = true; SetWaypoint(StoryAnchors.Get("s02_trail2"), "MORE CHEWED PLANTS", true); }
            if (!s02Wp2 && Story.Story.StepDone("S02", "chew2") && !Story.Story.StepDone("S02", "find")) { s02Wp2 = true; SetWaypoint(s02Goat.transform.position, "HORACE", true); }

            // the settlement: the one shirt he didn't eat
            if (!s02Paid && Story.Story.Route("S02", "settle") == StoryLibrary.S02Me && !Story.Story.Flag("s02_scarf"))
            {
                s02Paid = true; Story.Story.SetFlag("s02_scarf");
                Inventory.AddItem("cloth_scarf");
                Journal.Add("JOB", "THE GOAT HAS A LAWYER: OSWIN HANDED OVER THE ONE SHIRT HORACE DIDN'T EAT (+SCARF)");
            }
            if (home) return;

            // the lure: on foot, carrying something he likes, close enough to smell it
            var me = Player.transform.position; var g = s02Goat.transform.position;
            float d = new Vector2(me.x - g.x, me.z - g.z).magnitude;
            string treat = null;
            foreach (var f in s02Goat.Def.likes) if (Inventory.GetItem(f) > 0) { treat = f; break; }
            bool onFoot = !Current && !Player.SeatedIn;
            if (!s02Luring)
            {
                if (onFoot && treat != null && d < 7f)
                {
                    s02Luring = true; s02Goat.owned = true; s02Goat.order = 1;
                    Toast("HORACE SMELLS THE " + MadMax.Items.ItemCatalog.Name(treat) + " AND FOLLOWS YOU");
                }
                return;
            }
            if (!onFoot || treat == null || d > 16f)
            {
                s02Luring = false; s02Goat.owned = false; s02Goat.order = 0; s02Goat.home = g;
                Toast(treat == null ? "NOTHING HE LIKES IN YOUR PACK: HORACE WANDERS OFF" : "HORACE LOSES INTEREST");
                return;
            }
            var pen = StoryAnchors.Get("s02_pen");
            if (new Vector2(g.x - pen.x, g.z - pen.z).magnitude < 4.5f && new Vector2(me.x - pen.x, me.z - pen.z).magnitude < 2.6f)
            {
                s02Luring = false; s02Goat.order = 0; s02Goat.home = pen;                     // he stays: roams his pen
                Story.Story.Note("s02:home");
                Toast("HORACE TROTS INTO HIS PEN AFTER THE " + MadMax.Items.ItemCatalog.Name(treat));
            }
        }
    }
}
