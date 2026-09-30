using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // S18 A PERFECTLY LEGAL RACE (storyline §10): Tamsin Rook, bicycle courier, races anyone with wheels round her loop:
    // out along the road, across the dry wash, back over the market crossing. Scout it (ride the flags or have her talk
    // you round), race on a bike (her spare leans on the sign) or with an engine (motors round one extra flag). Cutting
    // corners is legal; hitting someone at the crossing costs the medal, and restitution (paid, or the stall mended)
    // still closes the day. The race runs in the game (Game/StoryQuests/S18.cs).
    public static partial class StoryLibrary
    {
        public const string S18Ticket = "story_race_ticket", S18Medal = "story_courier_medal";

        static partial void Author_S18(QuestDef q)
        {
            ItemIds.Register(S18Ticket, "MARSHAL'S TICKET: ONE STALL, ONE BRUISE");
            ItemIds.Register(S18Medal, "TAMSIN'S TIN MEDAL: FASTEST ON THE LOOP");
            foreach (var k in new[] { "tamsin", "s18_start", "s18_g1", "s18_g1b", "s18_g2", "s18_g3", "s18_market", "s18_marshal" }) if (!Anchors.Contains(k)) Anchors.Add(k);

            q.build = Build.Playable;
            q.offerSay = "NICE BIKE.";
            q.offerReply = "IT'S A GREAT BIKE. BETTER THAN WHATEVER YOU DRIVE, AND I'LL PROVE IT: ONE LOOP, ROAD, WASH AND MARKET. BRING ANYTHING WITH WHEELS. " +
                           "SHORTCUTS ARE LEGAL IF NOBODY'S STANDING ON THEM. HIT A PEDESTRIAN AND YOU LOSE, EVEN IF YOU WIN.";
            q.hook = "TAMSIN ROOK, BICYCLE COURIER, SAYS SHE CAN BEAT ANY MOTOR ROUND HER LOOP. THE RULES ARE SHORT AND SHE REPEATS THEM.";

            Step(q, "g1", "SCOUT: THE FIRST FLAG, OUT ALONG THE ROAD", "s18_g1").When(Goal.Reach, "s18_g1", 12f).Optional();
            Step(q, "g2", "SCOUT: THE FLAG IN THE DRY WASH", "s18_g2").When(Goal.Reach, "s18_g2", 12f).Optional();
            Step(q, "g3", "SCOUT: THE MARKET CROSSING", "s18_g3").When(Goal.Reach, "s18_g3", 12f).Optional();
            Step(q, "scout", "SCOUT THE LOOP: PASS ITS THREE FLAGS (ROAD, WASH, MARKET CROSSING), OR HAVE TAMSIN TALK YOU ROUND IT", "s18_g1")
                .When(Goal.Steps, "g1,g2,g3", 3f, "RODE THE LOOP")
                .Says("tamsin", "s18_route", "WALK ME THROUGH THE LOOP.",
                      "OUT ALONG THE ROAD TO THE FIRST FLAG. ANYTHING WITH AN ENGINE ALSO ROUNDS THE WATER-TANK FLAG FURTHER OUT: YOUR PENALTY FOR CHEATING WITH PETROL. " +
                      "THEN CROSS-COUNTRY TO THE FLAG IN THE WASH, BACK OVER THE MARKET CROSSING AND HOME. THE CROSSING IS FULL OF PEOPLE BUYING TURNIPS. BE A PERSON ABOUT IT.");
            Step(q, "ready", "SAY WHEN", "tamsin")
                .Says("tamsin", "s18_ready", "LET'S RACE.",
                      "LINE UP BY THE START FLAG IN WHATEVER YOU'RE RIDING. MY SPARE BIKE'S LEANING ON THE SIGN IF YOU'RE BRAVE. WE GO ON THREE.");
            Step(q, "race", "LINE UP AT THE START FLAG ON A BIKE OR BEHIND A WHEEL, THEN RIDE EVERY FLAG OF THE LOOP", "s18_start")
                .When(Goal.Event, "s18:done_bike", label: "ON A BIKE")
                .When(Goal.Event, "s18:done_car", label: "WITH AN ENGINE")
                .Pays(r => r.training.Add((Skill.Driving, 5f)));
            Step(q, "medal", "OPTIONAL: BEAT TAMSIN HOME WITHOUT HURTING ANYONE", "s18_start").When(Goal.Event, "s18:medal").Optional()
                .Pays(r => { r.items.Add((S18Medal, 1)); r.training.Add((Skill.Driving, 4f)); r.flag = "s18_medal"; });
            Step(q, "square", "SOMEONE GOT HURT AT THE CROSSING: PAY RESTITUTION THROUGH TAMSIN, OR MEND THE KNOCKED-OVER STALL ([B], REPAIR)", "s18_market")
                .When(Goal.Event, "s18:clean", label: "NOBODY HURT")
                .Says("tamsin", "s18_owe", "I HIT SOMEONE AT THE CROSSING. WHAT DO I OWE?",
                      "TWENTY FOR OTTO'S FRUIT AND PEG'S SHIN. THEY'LL TAKE IT. THEY'LL ALSO TELL EVERYONE, WHICH IS FAIR.").Needs(S18Ticket)
                .When(Goal.Event, "repaired:table", label: "MENDED THE STALL");
            q.steps[q.steps.Count - 1].any[1].price = 20;
            Step(q, "wrap", "SHAKE ON IT WITH TAMSIN", "tamsin")
                .Says("tamsin", "s18_wrap", "GOOD RACE. SAME TIME NEXT WEEK?",
                      "ONLY IF YOU BRING SNACKS. HERE: A COURIER'S SATCHEL, FOR ALL THAT SHOUTING, AND A REPAIR KIT FOR WHATEVER YOU SCRAPED IN THE WASH.")
                .Pays(r => r.take.Add((S18Ticket, 1)));
            q.reward.items.Add(("cloth_schoolbag", 1)); q.reward.items.Add(("use_repair_kit", 1)); q.reward.resources.Add((ResourceType.Rubber, 3));
            q.reward.training.Add((Skill.Athletics, 4f)); q.reward.flag = "s18_done";
            q.payoff = "TAMSIN'S LOOP IS RUN. SHE'S ALREADY LOOKING FOR THE NEXT MOTORIST.";
        }

        /// <summary>S18's closing journal line from the routes taken.</summary>
        public static string S18Payoff()
        {
            string ride = Story.Route("S18", "race"), sq = Story.Route("S18", "square");
            bool medal = Story.StepDone("S18", "medal");
            string a = medal ? "YOU BEAT TAMSIN ROUND HER LOOP" : "TAMSIN BEAT YOU ROUND HER LOOP";
            a += ride == "ON A BIKE" ? " ON TWO WHEELS AND YOUR OWN LEGS" : " WITH AN ENGINE, WHICH SHE CALLS CHEATING";
            string b = sq == null || sq == "NOBODY HURT" ? "NOBODY AT THE CROSSING GOT SO MUCH AS A FRIGHT"
                     : sq == "MENDED THE STALL" ? "OTTO'S STALL IS MENDED AND PEG HAS FORGIVEN YOU, MOSTLY" : "OTTO AND PEG WERE PAID FOR THE FRUIT AND THE BRUISE";
            return a + ". " + b + ".";
        }
    }

    public static partial class StoryCast
    {
        static bool S18Around() => Story.StateOf("S18") != Story.State.Locked;

        static partial void Cast_S18(List<Member> into)
        {
            // Tamsin is on her bike while the race runs, not standing at the line
            into.Add(new Member { key = "tamsin", name = "TAMSIN ROOK", title = "BICYCLE COURIER", anchor = "tamsin", temper = Temper.Joker, female = true,
                                  outfit = new[] { "tank", "shorts", "boots", "goggles", "fingerless" },
                                  present = () => S18Around() && !MadMax.Game.WastelandGame.S18RaceOn });
            into.Add(new Member { key = "s18_otto", name = "OTTO FENN", title = "FRUIT SELLER", anchor = "s18_market", temper = Temper.Friendly,
                                  outfit = new[] { "sweater", "pants", "boots", "cowboy" }, present = S18Around });
            into.Add(new Member { key = "s18_peg", name = "PEG MALLORY", title = "RACE MARSHAL", anchor = "s18_marshal", temper = Temper.Gruff, female = true,
                                  outfit = new[] { "vest", "jeans", "boots", "sunhat" }, present = S18Around });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>S18: a loop on a road out of town (the second-longest, so it isn't S11's post road when there is a
        /// choice): the start by the town edge, a flag 320 m out, a water-tank flag 520 m out (engines only), a flag in
        /// open country 110-170 m off the road, the market crossing on the way back.</summary>
        static partial void Anchors_S18(WorldGen world, Settlement town)
        {
            if (town == null) return;
            var tc = new Vector3(town.pos.x, 0f, town.pos.y);
            var roads = Q2.RoadsOut(world, town);
            List<Vector3> road = null;
            foreach (int pick in new[] { 1, 0, 2, 3 })
                if (pick < roads.Count && Q2.Length(roads[pick]) - Q2.EdgeAlong(roads[pick], tc, town.radius) > 620f) { road = roads[pick]; break; }
            string[] mine = { "tamsin", "s18_start", "s18_g1", "s18_g1b", "s18_g2", "s18_g3", "s18_market", "s18_marshal" };
            bool laid = false;
            if (road != null)
            {
                float e = Q2.EdgeAlong(road, tc, town.radius);
                // shift the whole loop outwards until the start and the market find clear verges
                for (float s = 0f; s <= 160f && !laid; s += 40f)
                {
                    if (!Q2.PointAt(road, e + 45f + s, out var st, out var sd) || !Q2.PointAt(road, e + 360f + s, out var g1, out var g1d) || !Q2.PointAt(road, e + 560f + s, out var g1b, out var g1bd)
                        || !Q2.PointAt(road, e + 110f + s, out var g3, out var g3d) || !Q2.PointAt(road, e + 230f + s, out var mid, out var md)) continue;
                    // Tamsin and her spare bike on the verge right by the line
                    var sa = new Vector3(sd.z, 0f, -sd.x);
                    Vector3 tam = Vector3.zero; bool tamOk = false;
                    foreach (float off in new[] { 10f, -10f, 13f, -13f, 16f, -16f })
                    {
                        var p = st + sa * off;
                        if (Open(world, p) && Level(world, p, 3f, 1.4f) && Q2.Free(world, p, 30f, mine)) { tam = p; tamOk = true; break; }
                    }
                    if (!tamOk) continue;
                    var tt = st - tam;
                    float tf = Mathf.Atan2(tt.x, tt.z) * Mathf.Rad2Deg;
                    // the wash: open country 110-170 m off the road, either side
                    var across = new Vector3(md.z, 0f, -md.x);
                    Vector3 g2 = Vector3.zero; float side = 0f;
                    foreach (float off in new[] { 130f, -130f, 110f, -110f, 150f, -150f, 170f, -170f })
                    {
                        var p = mid + across * off;
                        if (Open(world, p) && Level(world, p, 6f, 2.5f) && world.Sample(p.x, p.z).roadDist > 40f && Q2.Free(world, p, 35f, mine)) { g2 = p; side = Mathf.Sign(off); break; }
                    }
                    if (side == 0f) continue;
                    // the market: on the verge by the crossing, on the wash side (where the loop comes back across)
                    var ga = new Vector3(g3d.z, 0f, -g3d.x) * side;
                    Vector3 mk = Vector3.zero; bool mkOk = false;
                    foreach (float off in new[] { 10f, 12f, 14f })
                        foreach (float sl in new[] { 0f, 6f, -6f })
                        {
                            if (mkOk) break;
                            var p = g3 + ga * off + g3d * sl;
                            if (Open(world, p) && Level(world, p, 3f, 1.2f) && Q2.Free(world, p, 25f, mine)) { mk = p; mkOk = true; }
                        }
                    if (!mkOk) continue;
                    Q2.Put(world, "tamsin", tam, tf);
                    Q2.Put(world, "s18_start", st, Mathf.Atan2(sd.x, sd.z) * Mathf.Rad2Deg);
                    float Heading(Vector3 d) => Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;                // gates face along the road: +X is its right verge
                    Q2.Put(world, "s18_g1", g1, Heading(g1d));
                    Q2.Put(world, "s18_g1b", g1b, Heading(g1bd));
                    Q2.Put(world, "s18_g2", g2, Heading(md));
                    Q2.Put(world, "s18_g3", g3, Heading(g3d));
                    var toRoad = g3 - mk;
                    float mf = Mathf.Atan2(toRoad.x, toRoad.z) * Mathf.Rad2Deg;
                    Q2.Put(world, "s18_market", mk, mf);
                    Q2.Put(world, "s18_marshal", mk + g3d * 4.5f, mf);
                    laid = true;
                }
            }
            if (!laid)
            {
                // no usable road: a loop round open country beside the town (same places, shorter straights)
                var st = Q2.Anywhere(world, tc, town.radius + 40f, 300f, out var sf);
                var fwd = Quaternion.Euler(0f, sf + 180f, 0f) * Vector3.forward;                        // away from town
                var right = new Vector3(fwd.z, 0f, -fwd.x);
                Q2.Put(world, "tamsin", st + right * 10f, sf);
                Q2.Put(world, "s18_start", st, sf + 180f);
                Q2.Put(world, "s18_g1", Q2.Anywhere(world, st + fwd * 320f, 0f, 0f, out _), 0f);
                Q2.Put(world, "s18_g1b", Q2.Anywhere(world, st + fwd * 520f, 0f, 0f, out _), 0f);
                Q2.Put(world, "s18_g2", Q2.Anywhere(world, st + fwd * 220f + right * 130f, 0f, 0f, out _), 0f);
                Q2.Put(world, "s18_g3", st + fwd * 90f + right * 40f, 0f);
                Q2.Put(world, "s18_market", st + fwd * 90f + right * 51f, sf);
                Q2.Put(world, "s18_marshal", st + fwd * 94f + right * 51f, sf);
            }
            Clearing("tamsin", 8f); Clearing("s18_market", 9f); Clearing("s18_g2", 10f);
        }
    }
}
