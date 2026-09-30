using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.Npc;
using MadMax.Story;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    // P1.3 ONE MORE SUNDAY: oil and coolant in Nell's barrel; the coupe counts as ready once it holds fuel, oil and
    // coolant. The trip: Nell takes the wheel (Npc.TakeWheel, the AiDriver following the road to the lookout once the
    // player sits in the passenger seat, [E] at the door), or rides beside the player (companion-style boarding). At
    // the lookout she gets out and stands at the view; home again, she parks, gets out and goes back to her stop.
    // Her body is kept out of the cast table while she is far from her stop, so the cast manager neither folds her away
    // nor spawns a second Nell.
    public partial class WastelandGame
    {
        MadMax.Npc.Npc p1Nell;
        Vector3 p1NellHome; float p1NellYaw; bool p1HomeSaved;
        float p1Warn;

        partial void Scene_P1_3()
        {
            if (!StoryAnchors.Has("nell")) return;
            var a = StoryAnchors.Get("nell");
            foreach (var p in Placeable.All)
                if (p && p.id == "barrel" && IsStoryProp(p) && (p.transform.position - a).sqrMagnitude < 81f && p.TryGetComponent<Container>(out var box))
                {
                    box.inventory.Add(ResourceType.Oil, 5); box.inventory.Add(ResourceType.Coolant, 8);
                    p.Dirty();
                    return;
                }
            Inventory.Add(ResourceType.Oil, 5); Inventory.Add(ResourceType.Coolant, 8);                   // no barrel left: she hands them over
            Journal.Add("STORY", "NELL HANDS YOU 5 L OF OIL AND 8 L OF COOLANT FOR THE COUPE");
        }

        partial void Tick_P1_3()
        {
            var q = StoryLibrary.Get("P1.3");
            var cur = Story.Story.Current(q);
            if (cur == null) return;
            var car = P1Car();
            if (!car) { EnsureNellCar(); return; }
            if (cur.id == "ready")
            {
                if (Time.time < p1Check) return;
                p1Check = Time.time + 0.5f;
                var sys = car.GetComponent<VehicleSystems>();
                if (sys && car.Engine && sys.fuel >= 8f && sys.OilFraction >= 0.6f && sys.CoolantFraction >= 0.6f) Story.Story.Note("p1_3:ready");
                return;
            }
            string who = Story.Story.Route("P1.3", "who");
            if (cur.id == "who" || who == null) return;
            bool nellDrives = StoryLibrary.P1NellDrives(who);
            var nell = p1Nell ? p1Nell : CastBody("nell");
            if (!nell || !nell.Alive) return;
            if (p1Nell != nell) { p1Nell = nell; p1HomeSaved = false; }
            if (!p1HomeSaved) { p1NellHome = nell.home; p1NellYaw = nell.homeYaw; p1HomeSaved = true; }
            P1KeepNell(nell);
            StoryLibrary.Get("P1.3").payoff = nellDrives
                ? "NELL DROVE THE LOOKOUT ROAD WITH ONE HAND ON THE WHEEL AND ONE ON THE WINDOW. THE COUPE STAYS HERS; SHE GAVE YOU TOM'S JACK AND HIS PAINT CODE, BRONZE OVER BLACK."
                : "YOU DROVE; NELL SAT WHERE TOM USED TO AND TOLD YOU EVERY BUMP BEFORE IT CAME. THE COUPE STAYS HERS; SHE GAVE YOU TOM'S JACK AND HIS PAINT CODE, BRONZE OVER BLACK.";

            if (cur.id == "talk")
            {
                // at the view: out of the car, standing by the drop
                nell.companion = false;
                if (nell.Driving && Mathf.Abs(car.ForwardSpeed) < 1f) nell.LeaveWheel();
                if (StoryAnchors.Has("p1_view")) { nell.home = StoryAnchors.Get("p1_view"); nell.homeYaw = StoryAnchors.Yaw("p1_view"); nell.homeRadius = 2f; }
                return;
            }
            // travelling: to the lookout, then home
            bool home = cur.id == "home";
            var target = StoryAnchors.Get(home ? "p1_car" : "p1_view");
            float dist = Flat(car.transform.position - target);
            bool arrived = dist < (home ? 12f : 24f);
            bool dark = DayNight.Hours >= 20.5f || DayNight.Hours < 6f;
            var seat = car.GetComponentInChildren<PassengerSeat>();
            if (nellDrives)
            {
                nell.companion = false;
                if (nell.Riding) nell.Unboard();
                if (!nell.Driving)
                {
                    if (arrived) return;
                    if (dark && !home) { P1Say("NELL: NOT IN THE DARK. FIRST LIGHT, SUNDAY OR NOT."); return; }
                    var door = car.transform.position - car.transform.right * 1.8f;
                    if (Flat(nell.transform.position - car.transform.position) > 30f) { nell.home = door; nell.homeRadius = 1.5f; return; }   // far off (a reload): walks over first
                    nell.homeRadius = 5f;
                    nell.TakeWheel(car);
                    if (car.TryGetComponent<AiDriver>(out var ai0)) ai0.goal = AiDriver.Goal.Park;
                    return;
                }
                if (!car.TryGetComponent<AiDriver>(out var ai)) return;
                if (arrived)
                {
                    ai.goal = AiDriver.Goal.Park;
                    if (Mathf.Abs(car.ForwardSpeed) > 0.8f) return;
                    nell.LeaveWheel();
                    if (home) { P1RestoreNell(nell); Story.Story.Note("p1_3:home"); }
                    else { nell.home = target; Story.Story.Note("p1_3:lookout"); Toast("NELL PULLS OVER AT THE LOOKOUT. [F] TO GET OUT"); }
                    return;
                }
                bool aboard = seat && seat.Taken;
                if (!aboard) { ai.goal = AiDriver.Goal.Park; P1Say("NELL WAITS AT THE WHEEL: [E] AT THE PASSENGER DOOR"); return; }
                if (ai.goal != AiDriver.Goal.Path) ai.SetPath(P1Route(car.transform.position, target, home), 1);
                ai.cruise = dist < 30f ? 4f : 9f;
                return;
            }
            // the player drives; Nell rides beside them (she follows like a companion and gets in when they drive)
            nell.leaving = false;                                                                       // "go your own way" doesn't apply to Nell
            if (nell.Driving) nell.LeaveWheel();
            if (dark && !home && !nell.Riding) { nell.companion = false; P1Say("NELL: IN THE MORNING. I DON'T DO THE LOOKOUT ROAD IN THE DARK."); return; }
            if (arrived && Mathf.Abs(car.ForwardSpeed) < 1.5f && (nell.Riding || Flat(nell.transform.position - car.transform.position) < 8f))
            {
                if (home) { nell.companion = false; if (nell.Riding && Current != car) nell.Unboard(); P1RestoreNell(nell); Story.Story.Note("p1_3:home"); }
                else Story.Story.Note("p1_3:lookout");
                return;
            }
            nell.companion = true; nell.order = 0;
        }

        /// <summary>Keep Nell's body while she's away with the coupe: out of the cast table beyond 140 m of her stop (the
        /// cast manager folds bodies away past 170 m), back in it nearer (it would spawn another inside 110 m).</summary>
        void P1KeepNell(MadMax.Npc.Npc nell)
        {
            if (!StoryAnchors.Has("nell")) return;
            float d = Flat(StoryAnchors.Get("nell") - FocusPos);
            castBodies.TryGetValue("nell", out var listed);
            if (d > 140f) { if (listed == nell) castBodies.Remove("nell"); return; }
            if (listed && listed != nell) Destroy(listed.gameObject);
            castBodies["nell"] = nell; castAt["nell"] = "nell";
        }

        void P1RestoreNell(MadMax.Npc.Npc nell)
        {
            nell.companion = false; nell.homeRadius = 5f;
            if (p1HomeSaved) { nell.home = p1NellHome; nell.homeYaw = p1NellYaw; }
            castBodies["nell"] = nell; castAt["nell"] = "nell";
            p1Nell = null; p1HomeSaved = false;
        }

        void P1Say(string line) { if (Time.time < p1Warn) return; p1Warn = Time.time + 8f; Toast(line); }

        /// <summary>The road between two places (the road passing nearest both), ending at <paramref name="to"/> itself
        /// when asked (the verge beside Nell's stop).</summary>
        List<Vector3> P1Route(Vector3 from, Vector3 to, bool endAtTarget)
        {
            var roads = World.roads.roads;
            int bestR = -1, bi = 0, bj = 0; float bestScore = float.MaxValue;
            for (int ri = 0; ri < roads.Count; ri++)
            {
                var pts = roads[ri].points;
                int i0 = 0, j0 = 0; float di = float.MaxValue, dj = float.MaxValue;
                for (int i = 0; i < pts.Count; i++)
                {
                    float a = Flat(pts[i] - from), b = Flat(pts[i] - to);
                    if (a < di) { di = a; i0 = i; }
                    if (b < dj) { dj = b; j0 = i; }
                }
                if (di + dj < bestScore) { bestScore = di + dj; bestR = ri; bi = i0; bj = j0; }
            }
            var path = new List<Vector3>();
            if (bestR >= 0 && bestScore < 160f)
            {
                var pts = roads[bestR].points; int step = bj >= bi ? 1 : -1;
                for (int i = bi; ; i += step) { path.Add(pts[i]); if (i == bj) break; }
            }
            if (endAtTarget || path.Count < 2) path.Add(to);
            if (path.Count < 2) path.Insert(0, from);
            return path;
        }
    }
}
