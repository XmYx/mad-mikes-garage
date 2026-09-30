using MadMax.Building;
using MadMax.Items;
using MadMax.Story;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>B5 THE NIGHT WE STAYED: the crew's camp down the road (Silas's saddlebag), the flag at the gate the player
    /// raises when their defences are ready (the confrontation: deterred at a high enough defence score, else a real
    /// raid through <see cref="MadMax.Npc.BaseRaid"/>), what each answer means for the territory's gang, the keepsake wall
    /// and the charter's daily life afterwards.</summary>
    public partial class WastelandGame
    {
        static readonly string[] B5Works = { "landmine", "tripwire", "watchtower", "motorised_gate", "gate_frame", "mg_nest", "auto_turret", "sandbag_wall", "spike_wall", "barbed_wire", "alarm_bell", "wall_panel", "doorway_panel", "wall_brick_fired" };
        float b5ConfrontAt = -1f, b5RaidAt = -1f;

        /// <summary>The claim the garage stands on (B1's flag), if any.</summary>
        static ClaimFlag B5Claim() => StoryAnchors.Has("garage") ? ClaimFlag.Near(StoryAnchors.Get("garage")) : null;

        /// <summary>Defence works the player built on the garage's claim.</summary>
        int B5WorksBuilt(ClaimFlag claim)
        {
            if (!claim) return 0;
            int n = 0;
            foreach (var p in Placeable.All)
                if (p && !IsStoryProp(p) && System.Array.IndexOf(B5Works, p.id) >= 0 && claim.Inside(p.transform.position)) n++;
            return n;
        }

        /// <summary>The gang whose road runs past the garage (the one Silas rides with).</summary>
        static MadMax.Npc.Convoy B5Gang() => MadMax.Npc.NpcDirector.Instance && StoryAnchors.Has("garage") ? MadMax.Npc.NpcDirector.Instance.RaidersByRoad(StoryAnchors.Get("garage")) : null;

        partial void Scene_B5()
        {
            if (!StoryAnchors.Has("b5_camp") || !Build || !Build.Structures) return;
            PutAt("b5_camp", "campfire", Vector3.zero, 0f);
            PutAt("b5_camp", "porch_awning", new Vector3(0f, 0f, -3.2f), 0f);
            PutAt("b5_camp", "crate", new Vector3(2.4f, 0f, -2f), 20f);                          // Silas's saddlebag on a crate
            PutAt("b5_camp", "tyres", new Vector3(-2.8f, 0f, -1.4f), 0f);
            Q7Vehicle("DirtBike", "b5_camp", new Vector3(-4.5f, 0f, 1.5f), 70f, "b5_bike");
            Q7Vehicle("DirtBike", "b5_camp", new Vector3(-5.5f, 0f, 3.2f), 80f, "b5_bike");
            PutAt("garage", "flag", new Vector3(4.5f, 0f, 12f), 0f);                           // the flag you raise when you're ready
        }

        partial void Tick_B5()
        {
            var q = StoryLibrary.Get("B5");
            if (q == null) return;
            if (Time.frameCount % 20 == 0)
            {
                var flag = Q7Prop("garage", "flag", new Vector3(4.5f, 0f, 12f), 2.5f);
                if (flag && !flag.GetComponent<StoryControl>()) StoryControl.On(flag.gameObject, "b5_flag", B5FlagPrompt, B5FlagUse);
            }
            if (Time.frameCount % 15 != 0) return;
            string answer = Story.Story.Route("B5", "answer"), charter = Story.Story.Route("B5", "charter");
            q.payoff = StoryLibrary.B5_Payoff(answer, charter, Story.Story.Flag("b5_deterred"), Story.Story.Flag("b5_looted"));
            B5Confront();
            if (Story.Story.StepDone("B5", "camp") && !Story.Story.StepDone("B5", "answer") && Inventory.GetItem(StoryLibrary.B5Ledger) == 0) Inventory.AddItem(StoryLibrary.B5Ledger);   // Moth copied the pages
            if (answer != null && !Story.Story.Flag("b5:settled")) B5Settle(answer);
            if (charter != null && !Story.Story.Flag("b5:charter"))
            {
                Story.Story.SetFlag("b5:charter");
                Story.Story.SetFlag(charter == StoryLibrary.B5Workshop ? "charter_workshop" : charter == StoryLibrary.B5Coop ? "charter_coop" : "charter_private");
                B5Wall();
                Story.Story.Note("b5:posted");
            }
        }

        string B5FlagPrompt(WastelandGame g)
        {
            if (!Q7State.At("B5", "answer") || Story.Story.Flag("b5:confront")) return null;
            var claim = B5Claim();
            if (!claim) return "THE FLAG AT THE GATE: PLANT A CLAIM FLAG AT THE GARAGE FIRST";
            int works = B5WorksBuilt(claim), def = claim.Defence();
            string s = "SEND WORD TO SILAS: DEFENCE " + def + " (" + StoryLibrary.B5Deter + "+ AND THEY THINK TWICE, BELOW THAT EXPECT A FIGHT), " + Mathf.Min(works, 3) + "/3 DEFENCES BUILT";
            return s + (works >= 3 ? "  [E] RAISE THE FLAG: WE'RE READY" : "");
        }

        void B5FlagUse(WastelandGame g, bool secondary)
        {
            if (secondary || !Q7State.At("B5", "answer") || Story.Story.Flag("b5:confront")) return;
            var claim = B5Claim();
            if (!claim) { Toast("PLANT A CLAIM FLAG AT THE GARAGE FIRST"); return; }
            if (B5WorksBuilt(claim) < 3) { Toast("NOT READY: BUILD AT LEAST THREE DEFENCES ON THE CLAIM (MINES, GATE, WATCHTOWER, MG NEST, SANDBAGS...)"); return; }
            Story.Story.SetFlag("b5:confront");
            b5ConfrontAt = Time.time;
            MadMax.Audio.Sfx.Play("horn", StoryAnchors.Get("b5_gate"), 0.9f, 0.85f, 200f);
            Toast("YOU RAISE THE FLAG. SILAS'S CREW RIDES UP TO THE GATE AND LOOKS YOUR PLACE OVER");
        }

        /// <summary>The confrontation, once the flag is up: they look (five seconds), then back off or come in.</summary>
        void B5Confront()
        {
            if (!Story.Story.Flag("b5:confront") || Story.Story.StepDone("B5", "answer")) return;
            var claim = B5Claim();
            if (!Story.Story.Flag("b5:raid"))
            {
                if (b5ConfrontAt < 0f) b5ConfrontAt = Time.time;
                if (Time.time - b5ConfrontAt < 5f) return;
                var raid = MadMax.Npc.BaseRaid.Instance;
                if (!claim || claim.Defence() >= StoryLibrary.B5Deter || !raid)
                {
                    Story.Story.SetFlag("b5_deterred");
                    Toast("SILAS COUNTS YOUR GUNS FROM THE GATE, SPITS, AND TURNS HIS CREW ROUND");
                    Journal.Add("STORY", "THE NIGHT WE STAYED: SILAS'S CREW LOOKED AT THE DEFENCES AND RODE ON. NOBODY FIRED A SHOT.");
                    Story.Story.Note("b5:stood");
                    return;
                }
                Story.Story.SetFlag("b5:raid");
                b5RaidAt = Time.time;
                raid.Begin(claim);
                Journal.Add("STORY", "THE NIGHT WE STAYED: SILAS'S CREW DIDN'T LIKE WHAT IT SAW, AND CAME IN ANYWAY.");
                return;
            }
            var br = MadMax.Npc.BaseRaid.Instance;
            if (br && br.Live && (b5RaidAt < 0f || Time.time - b5RaidAt < 900f)) return;
            if (b5RaidAt >= 0f && Time.time - b5RaidAt < 3f) return;
            int standing = 0;
            if (claim) foreach (var n in MadMax.Npc.Npc.All)                                     // raiders still on their feet made off with something
                    if (n && n.Alive && n.Profile != null && n.Profile.id.StartsWith("raid:") && (n.transform.position - claim.transform.position).sqrMagnitude < 200f * 200f) standing++;
            Story.Story.SetFlag("b5_fought");
            if (standing > 0) Story.Story.SetFlag("b5_looted");
            Journal.Add("STORY", standing > 0 ? "THE NIGHT WE STAYED: THEY TOOK SOME STORES AND LEFT. THE GARAGE STANDS." : "THE NIGHT WE STAYED: THE GARAGE HELD.");
            Story.Story.Note("b5:stood");
        }

        /// <summary>What the answer means on the road: the territory's gang spares the base for a season (levy), for
        /// a while and friendlier (passage), for long and much friendlier (exposed); defences just have to keep working.</summary>
        void B5Settle(string answer)
        {
            Story.Story.SetFlag("b5:settled");
            var gang = B5Gang();
            var faction = gang != null ? MadMax.Npc.Factions.OfGang(gang.Gang) : MadMax.Npc.Faction.None;
            int spare = 0, shift = 0;
            if (answer == StoryLibrary.B5Levy) { spare = Mathf.Max(7, Weather.DaysPerSeason); Story.Story.SetFlag("b5_levy"); }   // one season, as signed
            else if (answer == StoryLibrary.B5Passage) { spare = 20; shift = 5; Inventory.TakeItem("use_repair_kit"); Story.Story.SetFlag("b5_passage"); }
            else if (answer == StoryLibrary.B5Expose)
            {
                spare = 40; shift = 8; Inventory.TakeItem(StoryLibrary.B5Ledger); Story.Story.SetFlag("b5_exposed");
                MadMax.Audio.RadioNetwork.Flash("ROAD NEWS: SILAS VANCE'S OWN RIDERS LEFT HIM AT THE ROADSIDE AFTER HIS DOUBLE BOOKS CAME OUT. THE BEND ROAD IS QUIET");
            }
            else { Story.Story.SetFlag("b5_defended"); shift = Story.Story.Flag("b5_fought") ? -3 : 0; }
            if (gang != null && spare > 0) gang.save.spareUntil = Mathf.Max(gang.save.spareUntil, DayNight.Day + spare);
            if (faction != MadMax.Npc.Faction.None && shift != 0) MadMax.Npc.Factions.Shift(faction, shift);
            if (gang != null) Journal.Add("HOME", "SILAS RIDES WITH THE " + gang.Gang + (spare > 0 ? ": THEY LEAVE THE GARAGE ALONE FOR " + spare + " DAYS" : ": THE DEFENCES ARE WHAT KEEPS THEM OFF"));
        }

        /// <summary>The start of the keepsake wall: three mounts on the back wall of the bay, one already holding Silas's
        /// hood ornament (the others wait for the plate and the hubcap from supper).</summary>
        void B5Wall()
        {
            if (!StoryAnchors.Has("garage") || !Build || !Build.Structures || Story.Story.Flag("b5:wall")) return;
            Story.Story.SetFlag("b5:wall");
            var a = StoryAnchors.Get("garage"); var r = Quaternion.Euler(0f, StoryAnchors.Yaw("garage"), 0f);
            var onWall = r * new Quaternion(0f, 0.70710678f, 0.70710678f, 0f);                 // +Y out of the back wall, +Z up it
            float y = terrain.HeightNoLoad(a.x, a.z);
            for (int i = 0; i < 3; i++)
            {
                var p = a + r * new Vector3(-1.4f + i * 1.4f, 0f, -2.78f); p.y = y + 1.7f;
                var m = FurnitureLibrary.Spawn("trophy_mount", Build.Structures, p, onWall, propMaterial);
                if (!m) continue;
                storyProps.Add(m.Id);
                if (i == 1 && m.TryGetComponent<TrophyMount>(out var tm)) tm.LoadState("trophy_ornament");
            }
            Journal.Add("HOME", "THE KEEPSAKE WALL: THREE MOUNTS IN THE BAY. SILAS'S HOOD ORNAMENT IS THE FIRST THING ON IT; YOUR PLATE AND HUBCAP CAN GO UP BESIDE IT ([E]).");
        }

        int b5Day = -1;

        /// <summary>After B5, once a day at noon, the charter shows: a paid workshop gets a driver wanting a repair (paid
        /// if there's scrap in the garage's stores for parts and someone to do it), a cooperative refuge gets a
        /// traveller's share for the pot and feeds one guest.</summary>
        void B5Aftermath()
        {
            if (Story.Story.StateOf("B5") != Story.Story.State.Done || DayNight.Hours < 12f || b5Day == DayNight.Day) return;
            b5Day = DayNight.Day;
            if (Story.Story.Flag("b5:day:" + DayNight.Day)) return;
            Story.Story.SetFlag("b5:day:" + DayNight.Day);
            bool here = Q7Near("garage", 200f);
            if (Story.Story.Flag("charter_workshop"))
            {
                bool hands = here || Residents.Current == Residents.Service.Repair && Residents.Working;
                var stores = new System.Collections.Generic.List<Container>();
                Residents.Stores(stores);
                Container parts = null;
                foreach (var c in stores) if (c.inventory.Get(ResourceType.Scrap) >= 2) { parts = c; break; }
                if (hands && parts)
                {
                    parts.inventory.TrySpend(ResourceType.Scrap, 2);
                    parts.inventory.Add(ResourceType.Scrap, 8);
                    Journal.Add("HOME", "WORKSHOP: A DRIVER PULLED IN WITH A SHOT WHEEL BEARING. 2 SCRAP OF PARTS, PAID 8 (IN THE GARAGE STORES).");
                    if (here) Toast("A DRIVER PULLED IN FOR A REPAIR: PAID 8 SCRAP (2 WENT ON PARTS)");
                }
                else Journal.Add("HOME", "WORKSHOP: A DRIVER WANTED A REPAIR AND MOVED ON (" + (parts ? "NOBODY AROUND TO DO IT" : "NO SCRAP IN THE GARAGE STORES FOR PARTS") + ").");
            }
            else if (Story.Story.Flag("charter_coop"))
            {
                var stores = new System.Collections.Generic.List<Container>();
                Residents.Stores(stores);
                if (stores.Count == 0) return;
                string[] gifts = { "food_bread", "food_potato", "food_carrot", "food_apple", "food_cornbread" };
                string gift = gifts[DayNight.Day % gifts.Length];
                if (FoodLibrary.Get(gift) == null) gift = "food_can";
                stores[0].inventory.AddItem(gift);
                Journal.Add("HOME", "REFUGE: A TRAVELLER LEFT " + ItemCatalog.Name(gift) + " FOR THE POT AND STAYED FOR A MEAL. EVERYONE ARGUED ABOUT THE WASHING-UP.");
            }
        }
    }
}
