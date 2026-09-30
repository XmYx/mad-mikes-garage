using MadMax.Building;
using MadMax.Items;
using MadMax.Story;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>S17 SOUP AGAINST THE WEATHER in the world: Bea's roadside kitchen (a wood stove, the woodbox crate beside
    /// it that the stove burns from, a table and benches) and the coach stranded at the washout with a cold camp. While it
    /// runs: the woodbox is watched (12 wood, charcoal or coal = stocked), a paid load drops 12 charcoal into it, and a
    /// hard knock to the vehicle carrying the haybox costs the tip.</summary>
    public partial class WastelandGame
    {
        Placeable s17Box;
        VehicleDamage s17Watched;
        bool s17Set, s17Gentle, s17GentleDone;
        string s17Plan, s17Cook;

        partial void Scene_S17()
        {
            if (!Build || !Build.Structures) return;
            Q2Put("bea", "porch_awning", new Vector3(0f, 0f, -1.8f), 0f);
            Q2Put("bea", "stove", new Vector3(-1.2f, 0f, -2.4f), 0f);
            Q2Put("bea", "crate", new Vector3(-0.1f, 0f, -2.6f), 0f);                                   // the woodbox
            Q2Put("bea", "table", new Vector3(1.7f, 0f, -1.3f), 90f);
            Q2Put("bea", "bench", new Vector3(1.7f, 0f, 0.1f), 0f);
            Q2Put("bea", "bench", new Vector3(1.7f, 0f, -2.7f), 180f);
            Q2Put("bea", "barrel", new Vector3(-2.7f, 0f, -1.9f), 0f);
            Q2Put("bea", "lamp", new Vector3(-2.6f, 0f, -0.6f), 0f);
            // the washout: the coach nose-down on the verge, a cold fire, benches, luggage
            var pf = PrefabFor("Bus");
            if (pf && StoryAnchors.Has("s17_camp"))
            {
                var r = Quaternion.Euler(0f, StoryAnchors.Yaw("s17_camp"), 0f);
                var p = StoryAnchors.Get("s17_camp") + r * new Vector3(4f, 0f, -4f); p.y = terrain.HeightNoLoad(p.x, p.z) + 1f;
                var v = Instantiate(pf, p, r * Quaternion.Euler(0f, 80f, 0f)).GetComponent<VehicleDriver>();
                v.name = "Stranded Coach";
                Register(v, null);
                StoryTag.Set(v.gameObject, "s17_coach");
                var paint = v.GetComponent<VehiclePaint>() ?? v.gameObject.AddComponent<VehiclePaint>();
                paint.colour = 7; paint.Apply();
                if (v.Engine && v.Engine.TryGetComponent<VehiclePart>(out var ep)) ep.damage = 1f;               // drowned in the washout
                if (v.TryGetComponent<VehicleSystems>(out var sys)) sys.fuel = 0f;
            }
            Q2Put("s17_camp", "campfire", new Vector3(-2.2f, 0f, -2.6f), 0f);
            Q2Put("s17_camp", "bench", new Vector3(-2.2f, 0f, -4.2f), 0f);
            Q2Put("s17_camp", "bench", new Vector3(-3.9f, 0f, -2.6f), 90f);
            Q2Put("s17_camp", "crate", new Vector3(0.4f, 0f, -5.2f), 25f);
            Q2Put("s17_camp", "crate", new Vector3(1.1f, 0f, -5.6f), 60f);
            Journal.Add("PLACE", "BEA'S KITCHEN: A STOVE, A WOODBOX AND A COACHLOAD STRANDED AT THE WASHOUT DOWN THE ROAD");
        }

        /// <summary>Bea's woodbox (the story crate beside her stove).</summary>
        Container S17Woodbox()
        {
            if (!s17Box && StoryAnchors.Has("bea"))
            {
                var a = StoryAnchors.Get("bea");
                foreach (var p in Placeable.All) if (p && p.id == "crate" && IsStoryProp(p) && Q2Flat(p.transform.position, a) < 4f) { s17Box = p; break; }
            }
            return s17Box ? s17Box.GetComponent<Container>() : null;
        }

        void S17Bump(float strength, Vector3 point)
        {
            if (strength < 3f || Inventory.GetItem(StoryLibrary.S17Haybox) <= 0 || Story.Story.Flag("s17_spilled")) return;
            Story.Story.SetFlag("s17_spilled");
            Toast("THE HAYBOX LID JUMPS: SOUP ON THE FLOOR, NOT ALL OF IT");
        }

        partial void Tick_S17()
        {
            if (Time.frameCount % 20 == 0)
            {
                var box = S17Woodbox();
                if (box && !Story.Story.StepDone("S17", "fuel")
                    && box.inventory.Get(ResourceType.Wood) + box.inventory.Get(ResourceType.Charcoal) + box.inventory.Get(ResourceType.Coal) >= 12)
                    Story.Story.Note("s17:woodbox");
                var fuel = Story.Story.Route("S17", "fuel");
                if (box && fuel != null && fuel.StartsWith("PAY THE CHARCOAL") && !Story.Story.Flag("s17_load"))
                {
                    Story.Story.SetFlag("s17_load");
                    box.inventory.Add(ResourceType.Charcoal, 12);
                    Toast("THE CHARCOAL BURNER TIPS A LOAD INTO BEA'S WOODBOX");
                }
            }
            // carrying the haybox: a hard knock to the vehicle spills some; arriving without one earns the tip
            var dmg = Current ? Current.GetComponent<VehicleDamage>() : null;
            if (dmg != s17Watched)
            {
                if (s17Watched) s17Watched.Impact -= S17Bump;
                s17Watched = dmg;
                if (dmg) dmg.Impact += S17Bump;
            }
            if (!s17Gentle && Inventory.GetItem(StoryLibrary.S17Haybox) > 0 && !Story.Story.Flag("s17_spilled") && StoryAnchors.Has("s17_camp")
                && Q2Flat(FocusPos, StoryAnchors.Get("s17_camp")) < 12f)
            {
                s17Gentle = true;
                Story.Story.Note("s17:gentle");
            }
            string plan = Story.Story.Route("S17", "plan"), cook = Story.Story.Route("S17", "cook");
            bool gentle = Story.Story.StepDone("S17", "gentle");
            if (!s17Set || !ReferenceEquals(plan, s17Plan) || !ReferenceEquals(cook, s17Cook) || gentle != s17GentleDone)
            {
                s17Set = true; s17Plan = plan; s17Cook = cook; s17GentleDone = gentle;
                var q = StoryLibrary.Get("S17"); if (q != null) q.payoff = StoryLibrary.S17Payoff();
            }
        }
    }
}
