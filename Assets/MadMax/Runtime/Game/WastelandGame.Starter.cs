using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>FIRST STEPS: a short chain of jobs for a new game, each teaching one system and unlocking the next —
    /// drive, fuel up, swap a part off a wreck, run a haul from a town board, build a workbench and a wall, tame an
    /// animal. Shown as one line under the compass and in the journal; each step pays a little scrap. Saved as the
    /// step index (-1 = off: loaded older saves and finished chains).</summary>
    public partial class WastelandGame
    {
        struct Step { public string text, done; public int pay; }

        static readonly Step[] Steps =
        {
            new Step { text = "DRIVE 300 M IN ONE OF YOUR CARS", done = "YOU CAN DRIVE", pay = 15 },
            new Step { text = "TOP UP A TANK: {Service} WITH FUEL IN THE PACK, OR A PUMP", done = "TANK FILLED", pay = 20 },
            new Step { text = "TAKE A PART OFF A WRECK WITH THE WRENCH, MOUNT IT ON A CAR", done = "PARTS SWAP BETWEEN ANY VEHICLES", pay = 30 },
            new Step { text = "TAKE A HAUL FROM A TOWN BOARD AND DELIVER IT", done = "FIRST HAUL PAID", pay = 40 },
            new Step { text = "BUILD A WORKBENCH AND A WALL: {Build} WITH THE CLAW HAMMER", done = "A PLACE OF YOUR OWN", pay = 50 },
            new Step { text = "TAME AN ANIMAL: FEED IT TILL IT TRUSTS YOU, OR BUY LIVESTOCK", done = "A FRIEND ON FOUR LEGS", pay = 60 },
            new Step { text = "SLEEP THE NIGHT IN A BED: THE HOMESTEAD HAS ONE", done = "HOME IS WHERE YOU WAKE UP", pay = 30 },
        };

        /// <summary>Current FIRST STEPS step (-1 = off or finished).</summary>
        public int StarterStep { get; private set; } = -1;
        static readonly HashSet<string> starterNotes = new HashSet<string>();
        float starterCheck, starterDriven;
        Vector3 starterLast;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStarter() => starterNotes.Clear();

        /// <summary>Something the chain listens for happened ("fuel", "take", "mount", "paid").</summary>
        public static void StarterNote(string what) => starterNotes.Add(what);

        /// <summary>The objective line for the HUD (null when off).</summary>
        public string StarterLine => StarterStep < 0 || StarterStep >= Steps.Length ? null
            : "FIRST STEPS " + (StarterStep + 1) + "/" + Steps.Length + ": " + StarterText(StarterStep);

        static string StarterText(int i) => Steps[i].text.Replace("{Service}", Controls.Name(Controls.Act.Service)).Replace("{Build}", Controls.Name(Controls.Act.Build));

        void BeginStarter()
        {
            StarterStep = 0;
            starterNotes.Clear();
            Journal.Add("JOB", "FIRST STEPS: " + StarterText(0));
        }

        void UpdateStarter()
        {
            if (StarterStep < 0 || !played || !Player || (Menus && Menus.IsOpen)) return;
            // distance driven is counted every frame
            if (Current)
            {
                var p = Current.transform.position;
                if (starterLast != Vector3.zero) starterDriven += Mathf.Min(5f, Vector3.Distance(p, starterLast));
                starterLast = p;
            }
            else starterLast = Vector3.zero;
            if (Time.time < starterCheck) return;
            starterCheck = Time.time + 1f;
            if (!StepDone(StarterStep)) return;
            var s = Steps[StarterStep];
            Inventory.Add(ResourceType.Scrap, s.pay);
            MadMax.Audio.Sfx.Play2D("cash", 0.6f);
            Journal.Add("DONE", "FIRST STEPS: " + s.done + " (+" + s.pay + " SCRAP)");
            StarterStep++;
            starterNotes.Clear();
            if (StarterStep >= Steps.Length)
            {
                StarterStep = -1;
                Toast("FIRST STEPS DONE: THE WASTES ARE YOURS (+" + s.pay + " SCRAP)");
                return;
            }
            Toast(s.done + " (+" + s.pay + " SCRAP)  NEXT: " + StarterText(StarterStep));
            Journal.Add("JOB", "FIRST STEPS: " + StarterText(StarterStep));
            StarterGuide();
        }

        bool StepDone(int i)
        {
            switch (i)
            {
                case 0: return starterDriven >= 300f;
                case 1: return starterNotes.Contains("fuel");
                case 2: return starterNotes.Contains("take") && starterNotes.Contains("mount");
                case 3: return starterNotes.Contains("paid");
                case 4:
                {
                    bool bench = false, wall = false;
                    foreach (var p in Placeable.All) { if (!p || IsHomestead(p)) continue; bench |= p.id == "workbench"; wall |= p.id.StartsWith("wall"); }   // the homestead's own bench does not count
                    return bench && wall;
                }
                case 5: return MadMax.Animals.AnimalDirector.Instance && MadMax.Animals.AnimalDirector.Instance.kept.Count > 0;
                case 6: return starterNotes.Contains("slept");
            }
            return true;
        }

        /// <summary>A hand-carried part on <paramref name="wreck"/> (no wheels, engines or heavy gear) that a free socket of a
        /// fleet car accepts.</summary>
        bool HasFittingPart(VehicleDriver wreck)
        {
            if (!wreck.TryGetComponent<VehicleChassis>(out var wc)) return false;
            foreach (var ws in wc.Sockets)
            {
                var part = ws.Current;
                if (!part || part.mass > 120f || part.category == PartCategory.Wheel || part.category == PartCategory.Engine) continue;   // something to carry by hand
                foreach (var car in fleet)
                    if (car && car.TryGetComponent<VehicleChassis>(out var cc))
                        foreach (var s in cc.Sockets) if (!s.Current && s.CanAccept(part)) return true;
            }
            return false;
        }

        /// <summary>Point the way for the steps that need a place: the nearest wreck, the nearest town board.</summary>
        void StarterGuide()
        {
            var at = FocusPos;
            if (StarterStep == 2)
            {
                // the nearest wreck carrying a part that fits a free socket on one of your cars
                VehicleDriver best = null; float bd = 700f;
                foreach (var w in wrecks)
                {
                    if (!w) continue;
                    float d = Vector3.Distance(w.transform.position, at);
                    if (d < bd && HasFittingPart(w)) { bd = d; best = w; }
                }
                if (best) SetWaypoint(best.transform.position, "A WRECK TO STRIP");
                if (Inventory.GetItem(ItemIds.Wrench) <= 0) Toast("NO WRENCH? CRAFT ONE AT A WORKBENCH (4 SCRAP)");
            }
            else if (StarterStep == 3 && World != null)
            {
                MadMax.World.Settlement best = null; float bd = float.MaxValue;
                foreach (var st in World.settlements) { float d = Vector2.Distance(st.pos, new Vector2(at.x, at.z)); if (d < bd) { bd = d; best = st; } }
                if (best != null) SetWaypoint(new Vector3(best.pos.x, 0f, best.pos.y), MadMax.Npc.Market.TownName(best) + " BOARD");
            }
        }
    }
}
