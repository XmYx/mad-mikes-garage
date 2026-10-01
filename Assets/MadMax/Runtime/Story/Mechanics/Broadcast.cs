using System.Collections.Generic;
using MadMax.Game;
using UnityEngine;

namespace MadMax.Story
{
    /// <summary>A broadcast with choices (system key <c>broadcast</c>, storyline A6): a script assembled from the
    /// evidence records the player actually holds, read in one of three ways — FULL (names and all), REDACTED (names cut,
    /// quantities kept; needs a second source) or HELD (never aired, kept as leverage) — rehearsed with captions, aired
    /// through <see cref="MadMax.Audio.RadioNetwork.Flash"/> with a transcript and a written summary in the journal, and
    /// remembered in story flags ("broadcast:full" / "broadcast:redacted" / "broadcast:held") for later news and the
    /// finale. Captions play one line at a time from <see cref="Tick"/>.</summary>
    public static class Broadcast
    {
        public enum Kind { Full, Redacted, Held }

        public const float LineSeconds = 3f;
        static readonly Queue<string> captions = new Queue<string>();
        static float next;
        static string doneNote;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { captions.Clear(); next = 0f; doneNote = null; }

        /// <summary>The way it went out, once decided (null before).</summary>
        public static Kind? Aired
        {
            get
            {
                if (Story.Flag("broadcast:full")) return Kind.Full;
                if (Story.Flag("broadcast:redacted")) return Kind.Redacted;
                if (Story.Flag("broadcast:held")) return Kind.Held;
                return null;
            }
        }

        /// <summary>Captions still to show (a rehearsal or the broadcast is playing).</summary>
        public static bool Playing => captions.Count > 0;

        /// <summary>The lines June reads, from the records on file (<see cref="Story.Evidence"/>).</summary>
        public static List<string> Script(Kind k)
        {
            var l = new List<string> { "JUNE: THIS IS WASTETALK, NINETY POINT ONE. EVERYTHING THAT FOLLOWS HAS BEEN CHECKED AGAINST PAPER." };
            if (Story.Evidence("manifest")) l.Add("JUNE: A FREIGHT MANIFEST UNDER ADA VENN'S OWN STAMP HAS A DEAD DRIVER HAULING TO A DEPOT THAT OFFICIALLY RECEIVES NOTHING.");
            if (Story.Evidence("receipt")) l.Add("JUNE: HIS DELIVERY CHIT WAS CASHED THE DAY AFTER HE WAS WRITTEN OFF. SOMEBODY ELSE SIGNED FOR HIM.");
            if (Story.Evidence("recording")) l.Add("JUNE: THE DISTRESS CALL YOU'VE ALL HEARD WAS A RECORDING, MOVED FROM MAST TO MAST TO KEEP PEOPLE OFF ONE ROUTE. I CUT CALLS FOR THEM. I'M SORRY.");
            if (Story.Evidence("cargo")) l.Add("JUNE: THAT DEPOT HOLDS PUMPS, FILTERS AND MEDICINE MEANT FOR THE SMALL TOWNS.");
            if (Story.Evidence("tally")) l.Add("JUNE: ONE TOWN'S BOOK, CHECKED LINE BY LINE: ELEVEN FUEL DELIVERIES SIGNED FOR. FOUR ARRIVED.");
            if (Story.Evidence("schedule")) l.Add("JUNE: AND THE GUILD'S OWN ALLOCATION SCHEDULE CUTS TWELVE TOWNS OFF NEXT SEASON. NOT A BREAKDOWN. A DECISION.");
            if (k == Kind.Full) l.Add("JUNE: THE RECORDS ALSO NAME THE DRIVERS WHO SMUGGLED MEDICINE THROUGH THE GUILD'S OWN CHECKPOINTS. WE'RE READING EVERY NAME.");
            else l.Add("JUNE: THE NAMES OF THE DRIVERS WHO HELPED ARE CUT. EVERY QUANTITY IS BACKED BY A SECOND, INDEPENDENT LOGBOOK" + (Story.Evidence("logbook") ? "." : " WE'RE STILL CHECKING."));
            l.Add("JUNE: COPIES ARE WITH THE REMNANT ARCHIVE. IF YOU DROVE THOSE ROUTES, YOU ALREADY KNOW. KEEP YOUR LIGHTS ON.");
            return l;
        }

        /// <summary>One paragraph for the journal: what went out and what it costs.</summary>
        public static string Summary(Kind k) => k switch
        {
            Kind.Full => "BROADCAST (FULL RECORDS): EVERY LOAD, EVERY DATE, EVERY NAME. NOBODY CAN CALL IT A RUMOUR NOW, AND THE DRIVERS WHO HELPED ARE AS EXPOSED AS ADA.",
            Kind.Redacted => "BROADCAST (REDACTED): LOADS, DATES AND THE SCHEDULE, BACKED BY A SECOND LOGBOOK; THE HELPERS' NAMES STAY IN IVO'S DRAWER.",
            _ => "NOT BROADCAST: THE RECORDS ARE SEALED IN IVO'S ARCHIVE, TO BE PUT ON THE TABLE WITH ADA. LEVERAGE, UNTIL SOMEONE CALLS THE BLUFF.",
        };

        /// <summary>The news flash that goes out over the relay.</summary>
        public static string Headline(Kind k) => k == Kind.Full
            ? "JUNE BELL READS THE FUEL GUILD'S RECORDS ON AIR: DIVERTED PUMPS, A FAKED DEATH, TWELVE TOWNS TO BE CUT OFF, AND THE NAMES OF EVERY DRIVER INVOLVED"
            : "JUNE BELL READS THE FUEL GUILD'S RECORDS ON AIR: DIVERTED PUMPS, A FAKED DEATH, TWELVE TOWNS TO BE CUT OFF. THE HELPERS' NAMES ARE WITHHELD";

        /// <summary>Play a run-through: captions only, nothing goes out.</summary>
        public static void Rehearse(Kind k)
        {
            captions.Clear();
            captions.Enqueue("REHEARSAL AT THE RELAY. JUNE READS, YOU LISTEN.");
            foreach (var line in Script(k)) captions.Enqueue(line);
            next = 0f; doneNote = "broadcast:rehearsed";
        }

        /// <summary>Put it out (or seal it): remembered in the flags, the transcript and the summary in the journal, the
        /// flash on WasteTalk, standing with the Guild and the towns shifted by how it was done.</summary>
        public static void Air(WastelandGame g, Kind k)
        {
            if (Aired != null) return;
            Story.SetFlag(k == Kind.Full ? "broadcast:full" : k == Kind.Redacted ? "broadcast:redacted" : "broadcast:held");
            Journal.Add("STORY", Summary(k));
            if (k == Kind.Held)
            {
                MadMax.Npc.Factions.Shift(MadMax.Npc.Faction.Remnants, 5);
                return;
            }
            captions.Clear();
            foreach (var line in Script(k)) { captions.Enqueue(line); Journal.Add("RADIO", line); }
            next = 0f; doneNote = "broadcast:aired";
            MadMax.Audio.RadioNetwork.Flash(Headline(k), 90f);
            MadMax.Npc.Factions.Shift(MadMax.Npc.Faction.FuelGuild, k == Kind.Full ? -15 : -10);
            MadMax.Npc.Factions.Shift(MadMax.Npc.Faction.Settlers, k == Kind.Full ? 4 : 8);
            if (g) g.Toast("ON AIR: WASTETALK 90.1");
        }

        /// <summary>Show the next caption when its time comes (called every frame by the story's game side).</summary>
        public static void Tick(WastelandGame g)
        {
            if (captions.Count == 0 || !g || Time.time < next) return;
            string line = captions.Dequeue();
            g.Toast(line);
            // June's voice over the air (quest_cast radio_june) when the line has a clip: wait for it
            float said = line.StartsWith("JUNE: ") ? MadMax.Npc.QuestVoice.Radio("radio_june", line.Substring(6)) : 0f;
            next = Time.time + Mathf.Max(LineSeconds, said + 0.4f);
            if (captions.Count == 0 && doneNote != null) { Story.Note(doneNote); doneNote = null; }
        }
    }
}
