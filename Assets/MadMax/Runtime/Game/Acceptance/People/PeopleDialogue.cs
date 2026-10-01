using System.Collections;
using System.Linq;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Meeting people in the town nearest the start, through the talk page's own rows: everyone around has a
    /// name, a job, a temperament and three layers of history; a first meeting opens with a tone that moves disposition
    /// by the temperament's reaction scaled by charisma; backstory unlocks by layer as they warm up (the second layer
    /// waits for disposition 15); rumours go to the journal; an errand is taken, handed in once and paid once; leaving
    /// without asking anything costs a point; a threat is remembered and costs reputation.</summary>
    class PeopleDialogue : Scenario
    {
        public override string Id => "people.dialogue";
        public override float Timeout => 120f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            PH.Daylight(c);
            PH.Attribute(c, g, Attr.Charisma, 10);
            var st = PH.HomeTown(g, false);
            if (st == null) { c.Block("no settlement in the world"); yield break; }
            yield return PH.ToTown(c, st);
            var centre = PH.Centre(st);
            float reach = st.radius + 40f;
            yield return PH.Until(() => PH.Nearest(centre, reach, n => n.Profile.role == NpcRole.Resident && n.Available && !n.Hostile) != null, 25f);

            // ---- who lives here
            var people = MadMax.Npc.Npc.All.Where(n => n && n.Alive && !n.proxy && PH.Flat(n.transform.position, centre) < reach + 60f).ToList();
            c.Metric("people_in_town", people.Count, "");
            if (!c.Check(people.Count >= 3, $"the town is lived in ({people.Count} people within {reach + 60f:0} m of {MadMax.Npc.Market.TownName(st)})")) yield break;
            int whole = 0;
            foreach (var n in people)
            {
                var p = n.Profile;
                bool ok = !string.IsNullOrEmpty(p.Name) && !string.IsNullOrEmpty(p.Title) && System.Enum.IsDefined(typeof(Temper), p.temper)
                          && !string.IsNullOrEmpty(p.Backstory(0)) && !string.IsNullOrEmpty(p.Backstory(1)) && !string.IsNullOrEmpty(p.Backstory(2))
                          && (p.Cast || NpcProfile.Make(p.id, p.role, p.seed, p.kind).Name == p.Name);   // story cast keep their authored names
                if (ok) whole++; else c.Note("incomplete person: " + p.id + " '" + p.Name + "' " + p.Title);
            }
            c.Check(whole == people.Count, $"every one has a name, a job, a temperament and a three-layer history, the same on every meeting ({whole}/{people.Count})");
            c.Note("met: " + string.Join(", ", people.Take(8).Select(n => n.Profile.Name + " (" + n.Profile.Title + ", " + n.Profile.temper + ")")));

            // ---- a first meeting
            c.Metric("story_cast_in_town", people.Count(n => n.Profile.Cast), "");
            var who = PH.Nearest(centre, reach, n => n.Profile.role == NpcRole.Resident && !n.Profile.Cast && n.Available && !n.Hostile && !n.State.Has(NpcSave.Met));
            if (!c.Check(who, "a resident we have not met yet")) yield break;
            var P = who.Profile; var S = who.State;
            yield return PH.Face(c, who, 1.6f);
            if (!c.Check(PH.Talk(g, who), "[E] opens a conversation with " + P.Name)) yield break;
            c.Check(PH.Line(g).Contains("I'M " + P.Name), "they introduce themselves: " + PH.Line(g));
            var rows = g.Menus.Labels();
            c.Check(new[] { "(POLITE)", "(BLUNT)", "(JOKE)", "(FLATTER)", "(THREATEN)" }.All(t => rows.Any(r => r.StartsWith(t))), "the first meeting offers a tone: " + PH.Rows(g));
            c.Check(!rows.Contains("WHO ARE YOU?"), "no small talk before the sizing-up");
            int d0 = S.disposition;
            int expect = Mathf.Clamp(d0 + PH.Scaled(NpcLore.Reaction(P.temper, Tone.Polite), 10), -100, 100);
            g.Menus.Pick("(POLITE)");
            c.Metric("polite_reaction", S.disposition - d0, "");
            c.Check(S.Has(NpcSave.Met) && S.disposition == expect, $"a polite opening to a {P.temper} person: disposition {d0} -> {S.disposition} (expected {expect})");
            c.Check(g.Menus.Labels().Contains("WHO ARE YOU?") && g.Menus.Labels().Contains("WHAT'S HAPPENING AROUND HERE?"), "then the hub: " + PH.Rows(g));
            c.Screenshot("first_meeting");
            yield return null;

            // ---- backstory in layers
            g.Menus.Pick("WHO ARE YOU?");
            c.Check(S.revealed == 1 && PH.Line(g).Contains(P.Backstory(0)), "layer one: where they come from");
            if (S.disposition < 15)
            {
                g.Menus.Pick("WHO ARE YOU?");
                c.Check(S.revealed == 1 && PH.Line(g) == "THAT'S ALL YOU NEED TO KNOW FOR NOW.", $"layer two waits for trust (disposition {S.disposition} < 15)");
                int before = S.disposition;
                if (!c.Check(g.Menus.Pick("[CHA 9]"), "[CHA 9] option for a charismatic character: " + PH.Rows(g))) yield break;
                c.Check(S.Has(NpcSave.Helped) && S.disposition == Mathf.Min(100, before + PH.Scaled(12, 10)), $"a bond warms them: {before} -> {S.disposition}");
            }
            g.Menus.Pick("WHO ARE YOU?");
            c.Check(S.revealed == 2 && PH.Line(g) == P.Backstory(1), $"layer two at disposition {S.disposition}: what drives them");

            // ---- rumours
            g.Menus.Pick("WHAT'S HAPPENING AROUND HERE?");
            string rumour = PH.Line(g);
            c.Check(rumour.Length > 10 && rumour != "WHY WOULD I TELL YOU ANYTHING?", "a rumour: " + rumour);
            c.Check(Journal.Entries.Count > 0 && Journal.Entries[0].kind == "RUMOUR" && Journal.Entries[0].text.EndsWith(rumour), "the rumour goes into the journal (newest first): " + (Journal.Entries.Count > 0 ? Journal.Entries[0].kind + " " + Journal.Entries[0].text : "empty"));

            // ---- an errand: taken, handed in once, paid once
            var job = NpcLore.Jobs[P.job];
            if (!c.Check(g.Menus.Pick("NEED A HAND WITH ANYTHING?"), "they have an errand: " + PH.Rows(g))) yield break;
            c.Check(PH.Line(g) == job.ask, "the errand: " + job.ask);
            g.Menus.Pick("I'LL DO IT.");
            c.Check(S.jobState == 1 && Journal.Entries.Count > 0 && Journal.Entries[0].kind == "ERRAND", "errand accepted and written down");
            int had = PH.Have(g, job.item);
            if (had > 0)
            {
                if (job.item.StartsWith("res:")) g.Inventory.TrySpend((MadMax.Items.ResourceType)int.Parse(job.item.Substring(4)), had); else g.Inventory.TakeItem(job.item, had);
                c.Fixture("emptied the pack of " + had + " " + Trade.Name(job.item) + " (to ask before having it)");
            }
            g.Menus.Pick("ABOUT THAT ERRAND...");
            c.Check(S.jobState == 1 && PH.Line(g).StartsWith("COME BACK WHEN YOU HAVE"), "not done yet: " + PH.Line(g));
            int want = job.n - PH.Have(g, job.item);
            if (want > 0) PH.Grant(c, g, job.item, want);
            int have = PH.Have(g, job.item), scrap = PH.Scrap(g), bandages = g.Inventory.GetItem("med_bandage"), rations = g.Inventory.GetItem("food_ration");
            int rep = NpcRegistry.Reputation;
            g.Menus.Pick("ABOUT THAT ERRAND...");
            c.Check(S.jobState == 2 && S.Has(NpcSave.Helped) && PH.Line(g).StartsWith("YOU DID IT!"), "handed in: " + PH.Line(g));
            c.Check(PH.Have(g, job.item) == have - job.n, $"{job.n} {Trade.Name(job.item)} handed over");
            c.Check(NpcRegistry.Reputation == Mathf.Min(100, rep + 3), "helping raises reputation by 3");
            switch (job.reward)
            {
                case "part":
                {
                    string[] keys = { "wheel_street", "radiator_car", "exhaust_side_pipes", "bumper_bull_bar" };
                    var key = keys[P.seed & 3];
                    yield return new WaitForSeconds(0.3f);
                    bool part = Object.FindObjectsByType<VehiclePart>(FindObjectsSortMode.None).Any(v => v && v.partId == key && Vector3.Distance(v.transform.position, who.transform.position) < 4f);
                    c.Check(part, "paid with a " + key + " set down beside them");
                    break;
                }
                case "item": c.Check(g.Inventory.GetItem("med_bandage") == bandages + 2 && g.Inventory.GetItem("food_ration") == rations + 1, "paid with two bandages and a ration"); break;
                default:
                {
                    int pay = Mathf.CeilToInt(Trade.Value(job.item) * job.n * 1.8f) + 8;
                    c.Check(PH.Scrap(g) == scrap + pay, $"paid {pay} scrap (got {PH.Scrap(g) - scrap})");
                    break;
                }
            }
            c.Check(!g.Menus.Labels().Any(r => r.StartsWith("ABOUT THAT ERRAND") || r.StartsWith("NEED A HAND")), "the errand can't be handed in or taken twice");

            // ---- goodbye
            g.Menus.Pick("GOODBYE.");
            c.Check(g.Menus.Labels().SequenceEqual(new[] { "(LEAVE)" }), "a farewell, then only (LEAVE)");
            g.Menus.Pick("(LEAVE)");
            c.Check(!g.Menus.IsOpen, "(LEAVE) closes the conversation");

            // ---- a second meeting: a greeting, and a curt goodbye costs a point
            yield return PH.Face(c, who, 1.6f);
            PH.Talk(g, who);
            c.Check(!g.Menus.Labels().Any(r => r.StartsWith("(POLITE)")) && g.Menus.Labels().Contains("WHO ARE YOU?"), "met before: straight to the hub");
            int d1 = S.disposition;
            g.Menus.Pick("GOODBYE.");
            c.Check(S.disposition == d1 - 1, $"leaving without asking anything costs 1 ({d1} -> {S.disposition})");
            g.Menus.Pick("(LEAVE)");

            // ---- a threat is remembered
            var other = PH.Nearest(centre, reach, n => n != who && n.Profile.role == NpcRole.Resident && !n.Profile.Cast && n.Available && !n.Hostile && !n.State.Has(NpcSave.Met));
            if (!other) { c.Note("no second stranger to threaten"); yield break; }
            yield return PH.Face(c, other, 1.6f);
            PH.Talk(g, other);
            int rep2 = NpcRegistry.Reputation;
            c.Check(g.Menus.Pick("(THREATEN)"), "(THREATEN) is offered");
            c.Check(other.State.Has(NpcSave.Threatened) && NpcRegistry.Reputation == rep2 - 1, $"{other.Profile.Name} ({other.Profile.temper}) remembers the threat; reputation {rep2} -> {NpcRegistry.Reputation}: {PH.Line(g)}");
            if (g.Menus.IsOpen) g.Menus.Close();
        }
    }
}
