using MadMax.RPG;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Surgery table (depth stage G). [E] lie down and operate: every lodged fragment (<see cref="Injury.shrapnel"/>)
    /// is cut out and every deep wound stitched closed, one roll each (<see cref="Chance"/>: Survival skill, Intelligence,
    /// a companion within 4 m to assist). An operation takes a dose of antibiotics and one of painkillers (not needed
    /// while painkillers already work), from the pack or a medicine cabinet within 5 m. A failed cut tears the wound
    /// open again (bleeding, worse, a little infection).</summary>
    public class SurgeryTable : MonoBehaviour, IInteractable
    {
        public static readonly Vector3 Spot = new Vector3(0f, 0.84f, 0.16f);
        Seat seat;

        void Awake()
        {
            seat = GetComponent<Seat>();
            if (!seat) { seat = gameObject.AddComponent<Seat>(); seat.spots = new[] { Spot }; }
            seat.hidden = true; seat.rest = 1.2f; seat.reading = 1f;
        }

        /// <summary>Odds one cut goes well.</summary>
        public static float Chance(int survival, int intelligence, bool assisted) =>
            Mathf.Clamp(0.45f + survival * 0.05f + (intelligence - 5) * 0.03f + (assisted ? 0.15f : 0f), 0.25f, 0.95f);

        public static bool Target(Injury i) => i.shrapnel || i.type == Wound.DeepWound;

        public static int Targets(CharacterStats s) { int n = 0; foreach (var i in s.injuries) if (Target(i)) n++; return n; }

        static bool Assisted(Vector3 at)
        {
            foreach (var n in MadMax.Npc.Npc.All) if (n && n.companion && n.Alive && (n.transform.position - at).sqrMagnitude < 16f) return true;
            return false;
        }

        public float ChanceFor(MadMax.Game.WastelandGame g) => Chance(g.Stats.Level(Skill.Survival), g.Stats.Attribute(Attr.Intelligence), Assisted(transform.position));

        /// <summary>What is missing for an operation (null: ready).</summary>
        public string Missing(MadMax.Game.WastelandGame g)
        {
            var at = transform.position;
            if (MedSupply.Count(g, at, "med_antibiotics") <= 0) return "ANTIBIOTICS";
            if (!g.Stats.painkilled && MedSupply.Count(g, at, "med_painkillers") <= 0) return "PAINKILLERS";
            return null;
        }

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            if (g.Player && g.Player.SeatedOn == seat) return null;
            int n = Targets(g.Stats);
            if (n == 0) return "SURGERY TABLE: NO FRAGMENTS OR DEEP WOUNDS";
            var need = Missing(g);
            if (need != null) return "SURGERY TABLE: NEEDS " + need;
            return "[E] OPERATE (" + n + " WOUND" + (n == 1 ? "" : "S") + ", " + MadMax.Game.Words.Odds(ChanceFor(g)) + ")";
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (!secondary) Operate(g, new System.Random());
        }

        /// <summary>Lie down and operate on every target wound. Returns (cuts that went well, cuts that failed); nothing
        /// happens (0, 0) without a target or supplies.</summary>
        public (int ok, int failed) Operate(MadMax.Game.WastelandGame g, System.Random rnd)
        {
            var s = g.Stats;
            if (Targets(s) == 0) { g.Toast("NOTHING TO OPERATE ON"); return (0, 0); }
            var need = Missing(g);
            if (need != null) { g.Toast("SURGERY NEEDS " + need); return (0, 0); }
            var at = transform.position;
            MedSupply.Take(g, at, "med_antibiotics");
            if (!s.painkilled && MedSupply.Take(g, at, "med_painkillers"))
            {
                s.painkillerUntil = Mathf.Max(s.painkillerUntil, MadMax.World.DayNight.TotalDays * 24f + 3f);
                s.painkilled = true;
            }
            if (seat && g.Player && g.Player.SeatedOn != seat) seat.Use(g, false);
            float chance = ChanceFor(g);
            int ok = 0, failed = 0;
            foreach (var inj in s.injuries)
            {
                if (!Target(inj)) continue;
                if (rnd.NextDouble() < chance)
                {
                    inj.shrapnel = false;
                    if (inj.type == Wound.DeepWound) inj.type = Wound.Laceration;          // stitched closed
                    inj.severity = Mathf.Min(inj.severity, 0.6f);
                    inj.bandaged = true; inj.bandageAge = 0f; inj.disinfected = true; inj.infection = 0f;
                    ok++;
                }
                else
                {
                    inj.severity = Mathf.Min(1f, inj.severity + 0.2f);
                    inj.bandaged = false; inj.infection = Mathf.Min(1f, inj.infection + 0.1f);
                    s.health = Mathf.Max(1f, s.health - 6f);
                    failed++;
                }
            }
            s.Practice(Skill.Survival, 4f * (ok + failed) + 2f * ok);
            MadMax.Audio.Sfx.Play("scratch", transform.position + Vector3.up, 0.5f, 0.8f, 10f);
            MadMax.Story.Story.Note("surgery");
            g.Toast(failed == 0 ? "SURGERY: ALL " + ok + " WENT WELL" : ok == 0 ? "SURGERY WENT BADLY: " + failed + " WOUND" + (failed == 1 ? "" : "S") + " TORN OPEN" : "SURGERY: " + ok + " WENT WELL, " + failed + " TORN OPEN");
            return (ok, failed);
        }
    }
}
