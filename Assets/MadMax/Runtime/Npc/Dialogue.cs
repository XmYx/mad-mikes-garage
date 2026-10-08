using System;
using System.Collections.Generic;
using MadMax.Game;
using MadMax.Items;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>A conversation with one NPC. The first meeting opens with a tone (polite, blunt, joke, flattery,
    /// threat) that each temperament takes differently; charisma scales how much it helps or hurts. From the hub the
    /// player steers: who they are (backstory unlocks in layers as they warm up), rumours (bunkers, tunnels, raiders,
    /// towns, fuel), trade, haggling, errands, small talk. Options marked [CHA n] only appear with enough charisma;
    /// [STR n] with enough strength. Raiders get a parley instead: pay the toll, threaten, lie, joke, recruit, insult.
    /// Checks roll against charisma, speech skill and disposition, and practise Speech.</summary>
    public partial class Dialogue
    {
        public struct Choice { public string label, hint; public Action act; }

        public readonly Npc npc;
        public string line;
        public readonly List<Choice> choices = new List<Choice>();
        public bool Ended { get; private set; }
        public bool WantsTrade;

        readonly WastelandGame g;
        bool smallTalked, asked;
        NpcProfile P => npc.Profile;
        NpcSave S => npc.State;
        int Cha => g.Stats.Attribute(Attr.Charisma);
        int Str => g.Stats.Attribute(Attr.Strength);
        int Speech => g.Stats.Level(Skill.Speech);
        int Day => DayNight.Day;

        public Dialogue(WastelandGame g, Npc npc)
        {
            this.g = g; this.npc = npc;
            if (npc.companion) { CompanionHub(); return; }
            if (npc.Surrendered) { SurrenderTalk(); return; }
            if (P.Raider) { Parley(); return; }
            if (npc.Hostile) { Hostile(); return; }
            if (P.Cast && !S.Has(NpcSave.Met)) { S.Set(NpcSave.Met); S.disposition = Mathf.Max(S.disposition, 10); }   // story characters skip the sizing-up
            line = NpcLore.Greeting(P.temper, S.Has(NpcSave.Met), S.disposition);
            if (!S.Has(NpcSave.Met)) FirstLook();
            if (S.disposition < -60) { Refuse(); return; }
            if (!S.Has(NpcSave.Met)) Opening();
            else Hub();
        }

        void Add(string label, Action act, string hint = null) => choices.Add(new Choice { label = label, act = act, hint = hint });

        /// <summary>First impression: the outfit (raider gear, hazmat, duster and hat, rags, clean clothes), taken
        /// according to temperament.</summary>
        void FirstLook()
        {
            string style = g.OutfitStyle();
            if (style == null) return;
            int d = NpcLore.StyleReaction(P.temper, style);
            if (d == 0) return;
            S.disposition = Mathf.Clamp(S.disposition + d, -100, 100);
            line += "  " + NpcLore.StyleNote(style, d > 0);
        }

        void End(string text)
        {
            line = text;
            choices.Clear();
            Add("(LEAVE)", () => Ended = true);
        }

        /// <summary>Disposition change scaled by charisma: good words land better, bad ones hurt less.</summary>
        int Change(int delta)
        {
            float s = delta > 0 ? delta * (0.7f + Cha * 0.06f) : delta * Mathf.Max(0.5f, 1.3f - Cha * 0.05f);
            int d = Mathf.RoundToInt(s);
            S.disposition = Mathf.Clamp(S.disposition + d, -100, 100);
            return d;
        }

        /// <summary>A persuasion roll. Difficulty ~ the charisma a sure thing needs.</summary>
        bool Check(float difficulty, int bonusAttr = 0)
        {
            float chance = Mathf.Clamp(0.5f + (Cha + bonusAttr - difficulty) * 0.1f + Speech * 0.04f + S.disposition * 0.003f, 0.05f, 0.95f);
            bool ok = UnityEngine.Random.value < chance;
            g.Stats.Practice(Skill.Speech, ok ? 4f : 2f);
            return ok;
        }

        string Title => P.Vendor ? " - " + NpcLore.TradeName(P.kind) : "";

        // ------------------------------------------------------------------ openers

        void Opening()
        {
            line += "  I'M " + P.Name + Title + ".";
            choices.Clear();
            int v = P.seed & 7;
            Add("(POLITE) " + NpcLore.ToneLine(Tone.Polite, v), () => Respond(Tone.Polite));
            Add("(BLUNT) " + NpcLore.ToneLine(Tone.Blunt, v), () => Respond(Tone.Blunt));
            Add("(JOKE) " + NpcLore.ToneLine(Tone.Joke, v), () => Respond(Tone.Joke));
            Add("(FLATTER) " + NpcLore.ToneLine(Tone.Flatter, v), () => Respond(Tone.Flatter));
            Add("(THREATEN) " + NpcLore.ToneLine(Tone.Threat, v), () => Respond(Tone.Threat), "THEY WILL REMEMBER THIS");
        }

        void Respond(Tone t)
        {
            int d = NpcLore.Reaction(P.temper, t);
            if (t == Tone.Flatter && Cha <= 3) d -= 3;                          // clumsy flattery sounds creepy
            if (t == Tone.Joke && Cha >= 7) d += 2;                             // good timing
            if (t == Tone.Threat && Str >= 8 && (P.temper == Temper.Greedy || P.temper == Temper.Gruff)) d += 3;   // they respect muscle
            if (t == Tone.Threat && g.OutfitStyle() == "RAIDER") d += P.temper == Temper.Nervous ? 4 : 2;   // the skull mask does the talking
            int got = Change(d);
            S.Set(NpcSave.Met);
            line = NpcLore.ReactionLine(P.temper, got);
            NpcVoice.Say(npc, got >= 2 ? (t == Tone.Joke ? "laugh" : "thanks") : got <= -2 ? (t == Tone.Threat ? "threatened" : "insulted") : t == Tone.Joke ? "laugh" : "yes", true);
            if (t == Tone.Threat)
            {
                S.Set(NpcSave.Threatened);
                NpcRegistry.Reputation = Mathf.Max(-100, NpcRegistry.Reputation - 1);
                if (P.temper == Temper.Nervous) { npc.Scare(12f); End(line + " (THEY RUN)"); return; }
                if (S.disposition < -30 && (P.tool != null || P.temper == Temper.Proud || P.temper == Temper.Gruff)) { S.Set(NpcSave.Hostile); End(line + " (THEY GO FOR THEIR WEAPON)"); return; }
            }
            g.Stats.Practice(Skill.Speech, 1f);
            Hub(false);
        }

        void Refuse()
        {
            choices.Clear();
            line = "GET LOST. I'M DONE TALKING TO YOU.";
            NpcVoice.Say(npc, "no", true);
            Add("(POLITE) I WAS OUT OF LINE. I'M SORRY.", () =>
            {
                if (Check(6f + (-S.disposition - 60) / 10f)) { S.disposition = -35; line = "...FINE. ONE MORE CHANCE."; Hub(false); }
                else End("SORRY DOESN'T FIX IT.");
            });
            Add("(LEAVE)", () => Ended = true);
        }

        void Hostile()
        {
            line = "YOU'VE GOT SOME NERVE COMING BACK HERE.";
            choices.Clear();
            Add("(POLITE) I LOST MY HEAD. I'M SORRY. IT WON'T HAPPEN AGAIN.", () =>
            {
                if (Check(7f)) { S.Set(NpcSave.Hostile, false); S.disposition = Mathf.Max(S.disposition, -35); End("...ALRIGHT. BUT I'M WATCHING YOU."); }
                else End("NOT GOOD ENOUGH.");
            });
            if (Cha >= 8) Add("[CHA 8] NOBODY NEEDS TO DIE TODAY. LET'S BOTH WALK AWAY.", () =>
            {
                if (Check(5f)) { S.Set(NpcSave.Hostile, false); S.disposition = Mathf.Max(S.disposition, -20); End("...YOU'RE RIGHT. GO."); }
                else End("NICE WORDS. STILL GOING TO HURT YOU.");
            });
            Add("(LEAVE)", () => Ended = true);
        }

        // ------------------------------------------------------------------ hub

        void Hub(bool setLine = true)
        {
            if (setLine) line = NpcLore.Greeting(P.temper, true, S.disposition);
            choices.Clear();
            foreach (var t in MadMax.Story.StoryTalk.Topics(g, npc))
            {
                var topic = t;
                Add(topic.say, () => { line = topic.reply; topic.act?.Invoke(); Change(2); Hub(false); });
            }
            Add("WHO ARE YOU?", About);
            Add("WHAT'S HAPPENING AROUND HERE?", Rumours);
            if (P.Vendor) Add("SHOW ME WHAT YOU'VE GOT.", () =>
            {
                if (S.disposition <= -40) { line = "I DON'T SELL TO YOUR KIND."; return; }
                if (Factions.Hostile(Factions.Of(npc))) { line = "THE " + Factions.Names[(int)Factions.Of(npc)] + " DON'T TRADE WITH YOU. NOT AFTER WHAT YOU DID."; return; }
                if (npc.Closed) { line = "WE'RE SHUT. COME BACK AFTER SUNRISE."; return; }
                WantsTrade = true;
            });
            if (P.role == NpcRole.Leader) Add("ANY WORK FOR THE TOWN?", BossJob, TownQuests.Stage(P.town) < TownQuests.Stages ? TownQuests.Title(TownQuests.Stage(P.town)) : null);
            if (P.role == NpcRole.Leader && TownQuests.ParcelFor(g, P.town, out var parcelFrom))
                Add("I'M HERE FOR THE PARCEL FOR " + parcelFrom + ".", () => { g.Inventory.AddItem(TownQuests.Parcel); line = "MEDICINE. THEY NEED IT MORE THAN WE DO. GO CAREFUL."; Hub(false); });
            if (Companions.CanAsk(npc) && !Companions.Full(g) && S.disposition >= 10)
            {
                if (Cha >= 6) Add("[CHA 6] WALK WITH ME. I COULD USE SOMEONE LIKE YOU.", () =>
                {
                    if (S.disposition >= 40 || Check(7f - S.disposition / 20f)) { Companions.Recruit(g, npc); End("...ALRIGHT. LEAD THE WAY."); }
                    else { Change(-2); line = "I DON'T KNOW YOU WELL ENOUGH FOR THAT."; Hub(false); }
                }, "A COMPANION FOLLOWS, FIGHTS, CARRIES AND GUARDS");
                Add("(" + Companions.HireScrap + " SCRAP) I'M HIRING. INTERESTED?", () =>
                {
                    if (g.Inventory.TrySpend(ResourceType.Scrap, Companions.HireScrap)) { Companions.Recruit(g, npc); End("SCRAP UP FRONT? YOU'VE GOT YOURSELF A HAND."); }
                    else { line = "COME BACK WHEN YOU CAN PAY."; Hub(false); }
                }, "A COMPANION FOLLOWS, FIGHTS, CARRIES AND GUARDS");
            }
            if (P.Vendor && S.haggleDay != Day && Cha >= 4) Add("[CHA " + Cha + "] COME ON, A LITTLE DISCOUNT FOR A FRIEND?", Haggle, "BETTER PRICES TODAY IF IT WORKS");
            if (S.jobState == 0 && S.disposition >= -5) Add("NEED A HAND WITH ANYTHING?", OfferJob);
            else if (S.jobState == 1) Add("ABOUT THAT ERRAND...", TurnIn);
            SeasonChoreChoice();                                                                // residents' seasonal chores (Dialogue.Seasons)
            PantryChoice();
            KeyChoice();                                                                        // mechanics cut keys for lost ones (Dialogue.Keys)
            StolenTrail();                                                                      // salvagers name who sold them the player's part
            if (!smallTalked) Add("(SMALL TALK)", SmallTalk);
            if (Cha >= 7 && S.revealed < 3 && S.disposition >= 5) Add("[CHA 7] YOU CAN TRUST ME. WHAT'S REALLY ON YOUR MIND?", Confide);
            if (Cha >= 9 && !S.Has(NpcSave.Helped)) Add("[CHA 9] PEOPLE LIKE US SHOULD LOOK OUT FOR EACH OTHER.", Bond);
            Add("GOODBYE.", () => { if (!asked) Change(-1); End(NpcLore.Farewell[((P.seed & 0xffff) + Day) % NpcLore.Farewell.Length]); });
        }

        // ------------------------------------------------------------------ roadmap 20: bosses, companions, the beaten

        void BossJob()
        {
            asked = true;
            int t = P.town;
            if (t < 0) { line = "THIS TOWN RUNS ITSELF."; Hub(false); return; }
            int stage = TownQuests.Stage(t);
            line = TownQuests.Describe(g, t);
            choices.Clear();
            if (stage < TownQuests.Stages)
            {
                if (!TownQuests.Accepted(t)) Add("I'LL DO IT.", () => { TownQuests.Accept(g, t); Change(3); line = "GOOD. DON'T LET US DOWN."; Hub(false); });
                else Add("IT'S DONE.", () =>
                {
                    var reply = TownQuests.TurnIn(g, t, npc.transform.position + npc.transform.forward * 1.5f);
                    if (reply != null) { Change(8); S.Set(NpcSave.Helped); }
                    line = reply ?? "NOT YET IT ISN'T. " + TownQuests.Describe(g, t);
                    Hub(false);
                });
            }
            Add("LATER.", () => Hub());
        }

        void CompanionHub()
        {
            line = npc.order == 1 ? "WAITING, LIKE YOU SAID." : npc.order == 2 ? "KEEPING WATCH." : "RIGHT BEHIND YOU, BOSS.";
            choices.Clear();
            if (npc.order != 0) Add("FOLLOW ME.", () => { npc.order = 0; End("ON YOUR SIX."); });
            if (npc.order != 1) Add("WAIT HERE.", () => { npc.order = 1; npc.home = npc.transform.position; npc.homeYaw = npc.transform.eulerAngles.y; End("I'LL BE HERE."); });
            var claim = MadMax.Building.ClaimFlag.Near(npc.transform.position);
            if (claim && npc.order != 2) Add("GUARD THE BASE.", () => { npc.order = 2; npc.home = claim.transform.position; npc.homeRadius = 10f; End("NOBODY GETS PAST ME."); });
            var car = SpareCar();
            if (car) Add("TAKE THE " + WastelandGame.Name(car) + " AND FOLLOW ME.", () => { npc.order = 0; npc.TakeWheel(car); End("I'LL STAY ON YOUR TAIL."); }, "THEY DRIVE IT BEHIND YOU; GET IN IT YOURSELF TO TAKE IT BACK");
            Add("(T OPENS THEIR PACK)  WE'RE DONE. GO YOUR OWN WAY.", () => { Companions.Dismiss(g, npc); End("...FINE. TAKE CARE OUT THERE."); }, "THEY LEAVE, DROPPING WHAT THEY CARRY FOR YOU");
            Add("(LEAVE)", () => Ended = true);
        }

        /// <summary>A fleet vehicle near the companion nobody is driving.</summary>
        MadMax.Vehicles.VehicleDriver SpareCar()
        {
            MadMax.Vehicles.VehicleDriver best = null; float bd = 25f * 25f;
            foreach (var v in g.Fleet)
            {
                if (!v || v == g.Current || v.aiDriven || !v.driveable || !v.Engine) continue;
                float d = (v.transform.position - npc.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = v; }
            }
            return best;
        }

        void SurrenderTalk()
        {
            line = P.temper == Temper.Proud ? "...GO ON THEN. FINISH IT." : "DON'T SHOOT! I'M DONE, I'M DONE!";
            choices.Clear();
            Add("GET OUT OF HERE. DON'T LET ME SEE YOU AGAIN.", () =>
            {
                npc.LetGo();
                Factions.Shift(Faction.Settlers, 2, false);
                Factions.Shift(Factions.Of(npc), 5);
                End("THANK YOU... I WON'T FORGET THIS.");
            }, "MERCY: +REPUTATION");
            Add("EMPTY YOUR POCKETS.", () =>
            {
                var loot = LootTables.Roll(P.Raider ? "raider" : "house", new System.Random(P.seed ^ Day), 1, g.Stats.Attribute(Attr.Perception));
                foreach (var (id, n) in loot)
                {
                    if (id.StartsWith("res:") && int.TryParse(id.Substring(4), out int ri)) g.Inventory.Add((ResourceType)ri, n);
                    else g.Inventory.AddItem(id, n);
                }
                npc.LetGo();
                End(loot.Count > 0 ? "TAKE IT, TAKE IT ALL!" : "I'VE GOT NOTHING! LOOK!");
            });
            if (Companions.CanAsk(npc) && !Companions.Full(g) && Cha >= 7)
                Add("[CHA 7] YOUR CREW LEFT YOU TO DIE. RIDE WITH ME INSTEAD.", () =>
                {
                    if (Check(6f)) { Companions.Recruit(g, npc); End("...YEAH. BETTER THAN BEING DEAD."); }
                    else End("...I'D RATHER TAKE MY CHANCES OUT THERE.");
                });
            Add("(LEAVE THEM)", () => Ended = true);
        }

        void About()
        {
            asked = true;
            switch (S.revealed)
            {
                case 0: line = "I'M " + P.Name + ". " + P.Backstory(0); S.revealed = 1; Change(1); break;
                case 1:
                    if (S.disposition >= 15) { line = P.Backstory(1); S.revealed = 2; Change(1); }
                    else line = "THAT'S ALL YOU NEED TO KNOW FOR NOW.";
                    break;
                case 2:
                    if (S.disposition >= 45) { line = P.Backstory(2); S.revealed = 3; }
                    else line = P.Backstory(1) + " ...ENOUGH ABOUT ME.";
                    break;
                default: line = "YOU KNOW MY STORY. " + P.Backstory(1); break;
            }
            Hub(false);
        }

        void Confide()
        {
            asked = true;
            if (Check(6f)) { line = P.Backstory(2); S.revealed = 3; Change(5); }
            else { line = "I DON'T KNOW YOU THAT WELL."; Change(-1); }
            Hub(false);
        }

        void Bond()
        {
            S.Set(NpcSave.Helped);
            Change(12);
            line = P.temper == Temper.Gruff ? "...YEAH. MAYBE WE SHOULD." : "YOU KNOW WHAT? YOU'RE RIGHT. COME BY ANY TIME.";
            Hub(false);
        }

        void SmallTalk()
        {
            smallTalked = true;
            asked = true;
            line = P.temper == Temper.Joker ? "OH, WE'RE CHATTING NOW? LOVELY." : "SURE. WHAT ABOUT?";
            choices.Clear();
            Add("(POLITE) HOW ARE YOU HOLDING UP OUT HERE?", () => Chat(Tone.Polite));
            Add("(JOKE) I HEAR THE WATER'S NICE THIS TIME OF YEAR. ALL THREE DROPS.", () => Chat(Tone.Joke));
            Add("(FLATTER) YOU'VE KEPT YOURSELF ALIVE. THAT TAKES SKILL.", () => Chat(Tone.Flatter));
            Add("(BLUNT) THE WEATHER'S BAD. THE ROADS ARE WORSE. THAT'S IT.", () => Chat(Tone.Blunt));
        }

        void Chat(Tone t)
        {
            int got = Change(NpcLore.Reaction(P.temper, t) / 2 + (t == Tone.Joke && P.temper == Temper.Joker ? 2 : 0));
            line = NpcLore.ReactionLine(P.temper, got * 2);
            g.Stats.Practice(Skill.Speech, 1f);
            Hub(false);
        }

        void Rumours()
        {
            asked = true;
            if (S.disposition < -25) { line = "WHY WOULD I TELL YOU ANYTHING?"; Hub(false); return; }
            var facts = new List<string>();
            var at = npc.transform.position;
            var near = new List<Site>();
            g.World.SitesNear(at, 1500f, near);
            Site best = null; float bd = float.MaxValue;
            foreach (var s in near) { float d = Vector2.Distance(s.pos, new Vector2(at.x, at.z)); if (d < bd) { bd = d; best = s; } }
            if (best != null)
            {
                var dir = NpcLore.Compass(best.pos.x - at.x, best.pos.y - at.z);
                facts.Add(best.kind == SiteKind.Bunker
                    ? "THERE'S AN OLD ARMY BUNKER " + dir + ", " + NpcLore.Distance(bd) + ". SOME SAY IT'S STILL STOCKED."
                    : "A TUNNEL RUNS THROUGH THE ROCK " + dir + ", " + NpcLore.Distance(bd) + ". A SHORTCUT, IF YOU'RE BRAVE.");
            }
            float rd = float.MaxValue;
            var raid = NpcDirector.Instance ? NpcDirector.Instance.NearestRaiders(at, out rd) : null;
            if (raid != null && rd < 2000f)
            {
                var p = raid.PointAt(raid.travel, out _);
                if (raid.cars.Count > 0 && raid.cars[0]) p = raid.cars[0].transform.position;
                facts.Add("THE " + raid.Gang + " RIDE " + NpcLore.Compass(p.x - at.x, p.z - at.z) + " OF HERE. " + raid.designs.Count + " CARS. KEEP YOUR HEAD DOWN.");
            }
            Settlement town = null; float td = float.MaxValue;
            var here = g.World.SettlementAt(at.x, at.z);
            foreach (var st in g.World.settlements) { if (st == here) continue; float d = Vector2.Distance(st.pos, new Vector2(at.x, at.z)); if (d < td) { td = d; town = st; } }
            if (town != null) facts.Add("NEAREST " + (town.kind == Biome.City ? "CITY" : town.kind == Biome.Town ? "TOWN" : "VILLAGE") + " IS " + Market.TownName(town) + ", " + NpcLore.Compass(town.pos.x - at.x, town.pos.y - at.z) + ", " + NpcLore.Distance(td) + ".");
            if (town != null) facts.Add("IN " + Market.TownName(town) + " " + Market.Hint(town) + ".");                  // prices travel by word of mouth
            GasPump pump = null; float pd = float.MaxValue;
            foreach (var gp in GasPump.All) { if (!gp || gp.stock < 20f) continue; float d = Vector3.Distance(gp.transform.position, at); if (d < pd) { pd = d; pump = gp; } }
            if (pump != null && pd < 1500f) facts.Add("THE OLD PUMP " + NpcLore.Compass(pump.transform.position.x - at.x, pump.transform.position.z - at.z) + " STILL GIVES FUEL.");
            if (Weather.Raining) facts.Add("THIS RAIN TURNS THE FLATS TO GLUE. STAY ON THE ROADS.");
            if (S.disposition >= 50 && S.revealed >= 3) facts.Add("AND DON'T FORGET WHAT I TOLD YOU. " + P.Backstory(2));
            if (facts.Count == 0) facts.Add("QUIET. TOO QUIET.");
            if (((P.seed & 0xffff) + Day) % 3 == 0)                                          // the legend of the Last Engine
            {
                var relic = MadMax.Game.LastEngine.Rumour(at, (P.seed & 0xffff) + Day);
                if (relic != null) { facts.Clear(); facts.Add(relic); }
            }
            int i0 = ((P.seed & 0xffff) + Day) % facts.Count;
            line = facts[i0] + (facts.Count > 1 && S.disposition >= 20 ? " " + facts[(i0 + 1) % facts.Count] : "");
            if (S.disposition >= -5 && !P.Raider && g.GiverRumour(at, TrustsWithWay, P.id) is string giver) line = giver + " " + line;   // word of someone with work
            MadMax.Game.Journal.Add("RUMOUR", P.Name + ": " + line);
            g.Stats.Practice(Skill.Speech, 0.5f);
            Hub(false);
        }

        /// <summary>Gives the way to a story giver (a point) rather than a direction: someone who likes the player or
        /// whose people do.</summary>
        bool TrustsWithWay => S.disposition >= TrustForWay || Factions.Friendly(Factions.Of(npc));
        public const int TrustForWay = 20;

        void Haggle()
        {
            float difficulty = 6f - S.disposition / 25f + (P.temper == Temper.Greedy ? 2f : 0f) - (P.temper == Temper.Friendly ? 1f : 0f);
            S.haggleDay = Day;
            if (Check(difficulty)) { Change(2); line = P.temper == Temper.Greedy ? "YOU'RE ROBBING ME. FINE. TODAY ONLY." : "ALRIGHT, ALRIGHT. FRIENDS' PRICES, TODAY."; NpcVoice.Say(npc, "haggle_win", true); }
            else { Change(-4); line = P.temper == Temper.Gruff ? "PRICES ARE PRICES." : "NICE TRY."; NpcVoice.Say(npc, "haggle_lose", true); }
            Hub(false);
        }

        void OfferJob()
        {
            asked = true;
            var j = NpcLore.Jobs[P.job];
            line = j.ask;
            choices.Clear();
            Add("I'LL DO IT.", () => { S.jobState = 1; Change(2); line = "GOOD. I'LL BE AROUND."; NpcVoice.Say(npc, "thanks", true); MadMax.Game.Journal.Add("ERRAND", P.Name + ": " + j.ask); Hub(false); });
            Add("NOT RIGHT NOW.", () => Hub());
        }

        void TurnIn()
        {
            var j = NpcLore.Jobs[P.job];
            bool res = j.item.StartsWith("res:");
            var rt = res ? (ResourceType)int.Parse(j.item.Substring(4)) : ResourceType.None;
            int have = res ? g.Inventory.Get(rt) : g.Inventory.GetItem(j.item);
            if (have < j.n) { line = "COME BACK WHEN YOU HAVE " + j.n + " " + Trade.Name(j.item) + ". YOU HAVE " + have + "."; Hub(false); return; }
            if (res) g.Inventory.TrySpend(rt, j.n); else g.Inventory.TakeItem(j.item, j.n);
            string reward;
            switch (j.reward)
            {
                case "part":
                {
                    string[] keys = { "wheel_street", "radiator_car", "exhaust_side_pipes", "bumper_bull_bar" };
                    var key = keys[P.seed & 3];
                    var part = g.SpawnPart(key, npc.transform.position + npc.transform.forward * 1.2f + Vector3.up * 0.6f, npc.transform.rotation);
                    if (part) { part.gameObject.AddComponent<Rigidbody>().mass = part.mass; MadMax.Net.NetSession.Instance?.SendLooseSpawn(part); }
                    reward = "TAKE THIS " + key.Replace('_', ' ').ToUpperInvariant() + ". I WON'T NEED IT.";
                    break;
                }
                case "item":
                    g.Inventory.AddItem("med_bandage", 2); g.Inventory.AddItem("food_ration", 1);
                    reward = "BANDAGES AND A RATION. IT'S WHAT I HAVE.";
                    break;
                default:
                {
                    int scrap = Mathf.CeilToInt(Trade.Value(j.item) * j.n * 1.8f) + 8;
                    g.Inventory.Add(ResourceType.Scrap, scrap);
                    reward = scrap + " SCRAP, AS PROMISED.";
                    break;
                }
            }
            S.jobState = 2; S.Set(NpcSave.Helped);
            Change(15);
            NpcRegistry.Reputation = Mathf.Min(100, NpcRegistry.Reputation + 3);
            MadMax.Audio.Sfx.Play2D("cash", 0.6f);
            line = "YOU DID IT! " + reward;
            Hub(false);
        }

        // ------------------------------------------------------------------ raiders

        void Parley()
        {
            var c = npc.convoy;
            bool attacking = c != null && c.phase == Convoy.Phase.Attack;
            c?.BeginParley();
            int fuelToll = 15, scrapToll = 40;
            choices.Clear();
            if (attacking || npc.Hostile)
            {
                line = "TALK?! NOW?! YOU'RE ROADKILL!";
                if (Cha >= 6) Add("[CHA 6] (PLEAD) ENOUGH! TAKE " + scrapToll * 2 + " SCRAP AND GO!", () =>
                {
                    if (g.Inventory.Get(ResourceType.Scrap) >= scrapToll * 2 && Check(7f)) { g.Inventory.TrySpend(ResourceType.Scrap, scrapToll * 2); Finish(Convoy.Outcome.Paid, "...FINE. SCRAP'S SCRAP. LET'S GO, BOYS."); }
                    else Finish(Convoy.Outcome.Failed, "TOO LATE FOR THAT.");
                });
                Add("(FIGHT)", () => Ended = true);
                return;
            }
            line = NpcLore.RaiderDemand[((P.seed & 0xffff) + Day) % NpcLore.RaiderDemand.Length].Replace("{GANG}", P.gang);
            if (g.Inventory.Get(ResourceType.Fuel) >= fuelToll)
                Add("(PAY) " + fuelToll + " LITRES OF FUEL.", () => { g.Inventory.TrySpend(ResourceType.Fuel, fuelToll); Finish(Convoy.Outcome.Paid, "SMART. THE ROAD'S YOURS... FOR TODAY."); });
            if (g.Inventory.Get(ResourceType.Scrap) >= scrapToll)
                Add("(PAY) " + scrapToll + " SCRAP.", () => { g.Inventory.TrySpend(ResourceType.Scrap, scrapToll); Finish(Convoy.Outcome.Paid, "PLEASURE DOING BUSINESS. NOW MOVE."); });
            if (Str >= 6 || Cha >= 5)
                Add("[STR " + Str + "] (THREATEN) WALK AWAY WHILE YOU STILL HAVE WHEELS.", () =>
                {
                    float d = 8f + (P.temper == Temper.Proud ? 2f : 0f) - (Str - 5) * 0.8f;
                    if (Check(d)) Finish(Convoy.Outcome.Scared, "...NOT WORTH THE AMMO. MOVE OUT!");
                    else Finish(Convoy.Outcome.Failed, "BIG TALK. LET'S SEE YOU BLEED.");
                });
            if (g.OutfitStyle() == "RAIDER")
                Add("[RAIDER GEAR] WE RIDE THE SAME ROADS. TOLL'S FOR CIVILIANS.", () =>
                {
                    if (Check(6.5f)) Finish(Convoy.Outcome.Fooled, "...HEH. NICE MASK. RIDE ON, ROAD KIN.");
                    else Finish(Convoy.Outcome.Failed, "NICE COSTUME. YOU'RE STILL MEAT.");
                });
            if (Cha >= 7)
                Add("[CHA 7] (LIE) THE FUEL GUILD PAYS MY WAY. TOUCH ME AND THEY BURN YOUR CAMP.", () =>
                {
                    if (Check(7.5f)) Finish(Convoy.Outcome.Fooled, "THE GUILD... TCH. GET OUT OF MY SIGHT.");
                    else Finish(Convoy.Outcome.Failed, "LIAR! GET THEM!");
                });
            if (Cha >= 9)
                Add("[CHA 9] YOUR BOSS SHORTCHANGES YOU. RIDE WITH ME AND YOU'LL NEVER GO DRY.", () =>
                {
                    if (Check(6f)) { Change(40); Finish(Convoy.Outcome.Recruited, "...HA! I LIKE YOU. THE " + P.gang + " WON'T TOUCH YOU. NOT TODAY, NOT EVER."); }
                    else Finish(Convoy.Outcome.Failed, "NICE TRY, SNAKE.");
                });
            Add("(JOKE) YOU CALL THAT A CONVOY? MY GRANDMA'S SHOPPING CART HAS MORE ARMOUR.", () =>
            {
                if (P.temper == Temper.Joker && UnityEngine.Random.value < 0.4f + Cha * 0.04f) Finish(Convoy.Outcome.Fooled, "HAHAHA! GRANDMA'S CART! ...GET OUT OF HERE BEFORE I CHANGE MY MIND.");
                else Finish(Convoy.Outcome.Failed, "FUNNY. LET'S SEE YOU LAUGH WITH NO TEETH.");
            });
            Add("(INSULT) GET OUT OF MY WAY, ROADKILL.", () => Finish(Convoy.Outcome.Failed, "YOU'RE DEAD!"));
            Add("(SAY NOTHING)", () => Ended = true, "THEY WON'T WAIT LONG");
        }

        void Finish(Convoy.Outcome o, string text)
        {
            End(text);
            var gang = Factions.OfGang(P.gang);                                                   // standing with the gang (roadmap 21)
            Factions.Shift(gang, o == Convoy.Outcome.Paid ? 3 : o == Convoy.Outcome.Recruited ? 40 : o == Convoy.Outcome.Failed ? -5 : o == Convoy.Outcome.Scared ? -2 : 0);
            if (o != Convoy.Outcome.Failed) S.Set(NpcSave.Parleyed);
            npc.convoy?.OnParley(o);
            if (o == Convoy.Outcome.Failed && npc.convoy == null) S.Set(NpcSave.Hostile);
        }
    }
}
