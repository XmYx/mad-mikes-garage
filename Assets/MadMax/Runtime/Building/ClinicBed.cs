using System.Collections.Generic;
using MadMax.RPG;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Clinic bed (depth stage G). [E] lie down: wounds heal <see cref="healRate"/> times as fast (once more under
    /// a roof), infection is drawn off, an unsplinted fracture is set in traction after <see cref="setAfter"/> s, and
    /// health comes back. Stocked from a medicine cabinet within 5 m it also dresses bleeding wounds, disinfects open
    /// ones, splints and gives antibiotics against a spreading infection. [T] lays a hurt companion or friendly
    /// neighbour on it: their bleeding stops and they mend until they are well, then get up.</summary>
    public class ClinicBed : MonoBehaviour, IInteractable
    {
        public static readonly List<ClinicBed> All = new List<ClinicBed>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();

        public float healRate = 4f;
        public float setAfter = 15f;
        /// <summary>Hips of whoever lies here (bed-local), facing the bed's front edge.</summary>
        public static readonly Vector3 Spot = new Vector3(0f, 0.6f, 0.16f);

        Seat seat;
        Transform berth;
        MadMax.Npc.Npc patient;
        float lain, roofCheck, stockCheck, well, toastAt;
        bool roofed;

        /// <summary>Seconds the player has lain here without getting up.</summary>
        public float Lain => lain;
        public bool Roofed => roofed;
        public MadMax.Npc.Npc Patient => patient;
        public Seat BedSeat => seat;

        void Awake()
        {
            seat = GetComponent<Seat>();
            if (!seat) { seat = gameObject.AddComponent<Seat>(); seat.spots = new[] { Spot }; }
            seat.hidden = true; seat.rest = 2.5f; seat.reading = 1f;
            berth = new GameObject("Berth").transform;
            berth.SetParent(transform, false);
            berth.localPosition = Spot;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() { All.Remove(this); Release(false); }

        bool PlayerLying(MadMax.Game.WastelandGame g) => g.Player && seat && g.Player.SeatedOn == seat;

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            if (patient) return patient.Profile.Name + " RESTS (" + MadMax.Game.Words.Health(patient.Health / Mathf.Max(1f, patient.MaxHealth)) + ")  [T] SEND AWAY";
            if (PlayerLying(g)) return null;
            var who = Candidate(g);
            string s = "[E] LIE DOWN (CLINIC BED" + (MedSupply.Cabinets(transform.position, MedSupply.Reach).Count > 0 ? ", STOCKED)" : ")");
            if (who) s += "  [T] LAY " + who.Profile.Name + " HERE";
            return s;
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (!secondary)
            {
                if (patient) { g.Toast(patient.Profile.Name + " IS IN THIS BED"); return; }
                seat.Use(g, false);
                lain = 0f;
                g.Toast("LYING IN THE CLINIC BED: REST TO HEAL");
                return;
            }
            if (patient) { Release(true); return; }
            var who = Candidate(g);
            if (who) Admit(who);
        }

        /// <summary>The nearest hurt, friendly person (a companion first) within 8 m who could lie here.</summary>
        public MadMax.Npc.Npc Candidate(MadMax.Game.WastelandGame g)
        {
            MadMax.Npc.Npc best = null; float bd = 8f;
            foreach (var n in MadMax.Npc.Npc.All)
            {
                if (!n || n.proxy || !n.Alive || n.Hostile || !n.Wounded || n.Riding || n.Driving || n.Berthed) continue;
                float d = Vector3.Distance(n.transform.position, transform.position) - (n.companion ? 4f : 0f);
                if (d < bd) { bd = d; best = n; }
            }
            return best;
        }

        /// <summary>Lay someone on the bed (they get up beside it when well or sent away).</summary>
        public bool Admit(MadMax.Npc.Npc n)
        {
            if (!n || patient || !n.Alive) return false;
            var g = MadMax.Game.WastelandGame.Instance;
            if (g && PlayerLying(g)) return false;
            patient = n; well = 0f;
            n.Berth(berth, transform.TransformPoint(new Vector3(0f, 0.05f, 0.9f)));
            if (g) g.Toast(n.Profile.Name + " LIES DOWN IN THE CLINIC BED");
            return true;
        }

        void Release(bool sent)
        {
            if (!patient) { patient = null; return; }
            patient.Unberth();
            if (sent) MadMax.Game.WastelandGame.Instance?.Toast(patient.Profile.Name + " GETS UP");
            patient = null;
        }

        void Update()
        {
            var g = MadMax.Game.WastelandGame.Instance;
            if (!g || !g.Player) return;
            float dt = Time.deltaTime;
            if ((roofCheck -= dt) <= 0f)
            {
                roofCheck = 3f;
                roofed = Physics.Raycast(transform.TransformPoint(0f, 1.5f, 0f), Vector3.up, 14f, ~0, QueryTriggerInteraction.Ignore);
            }
            if (PlayerLying(g) && g.Vitals && !g.Vitals.Dead) { lain += dt; Heal(g, g.Stats, dt); }
            else lain = 0f;
            if (patient) Nurse(g, dt);
        }

        /// <summary>Extra healing on top of the normal rate, traction, clean sheets, and the cabinet's supplies.</summary>
        void Heal(MadMax.Game.WastelandGame g, CharacterStats s, float dt)
        {
            float extra = healRate - 1f + (roofed ? 1f : 0f);
            foreach (var inj in s.injuries)
            {
                if (inj.type == Wound.Fracture && !inj.splinted && lain > setAfter)
                {
                    inj.splinted = true;
                    Say(g, "TRACTION: THE FRACTURE IS SET (" + Injury.ZoneNames[(int)inj.zone] + ")");
                    s.Practice(Skill.Survival, 3f);
                }
                if (inj.type == Wound.Fracture && !inj.splinted) continue;
                float care = inj.bandaged || inj.type == Wound.Bruise || inj.type == Wound.Scratch || inj.type == Wound.Fracture ? 1f : 0.5f;
                inj.severity = Mathf.Max(inj.shrapnel ? Injury.ShrapnelFloor : 0f, inj.severity - dt * extra * care / (inj.HealMinutes * 60f));
                if (inj.infection > 0f && inj.infection < 0.6f) inj.infection = Mathf.Max(0f, inj.infection - dt * 0.003f);
            }
            s.health = Mathf.Min(s.MaxHealth, s.health + dt * 0.35f);
            if ((stockCheck -= dt) > 0f || lain < 2f) return;
            stockCheck = 2f;
            var at = transform.position;
            foreach (var inj in s.injuries)
            {
                bool open = inj.Open;
                if (open && (!inj.bandaged || inj.BandageDirty) && (inj.Bleeding || inj.BandageDirty) && MedSupply.TakeFromCabinet(at, "med_bandage"))
                { inj.bandaged = true; inj.bandageAge = 0f; Say(g, "THE CLINIC DRESSES YOUR " + Injury.ZoneNames[(int)inj.zone]); }
                if (open && !inj.disinfected && MedSupply.TakeFromCabinet(at, "med_disinfectant"))
                { inj.disinfected = true; inj.infection = Mathf.Max(0f, inj.infection - 0.5f); Say(g, "WOUND DISINFECTED"); }
                if (inj.type == Wound.Fracture && !inj.splinted && MedSupply.TakeFromCabinet(at, "med_splint"))
                { inj.splinted = true; Say(g, "SPLINT FROM THE CABINET"); }
                if (inj.infection > 0.3f && MedSupply.TakeFromCabinet(at, "med_antibiotics"))
                { foreach (var i in s.injuries) i.infection = 0f; s.sick = Mathf.Min(s.sick, 5f); Say(g, "ANTIBIOTICS: THE INFECTION CLEARS"); }
            }
        }

        void Nurse(MadMax.Game.WastelandGame g, float dt)
        {
            if (!patient.Alive) { patient = null; return; }
            patient.Mend(dt * (roofed ? 3f : 2.2f));
            if (patient.Wounded) { well = 0f; return; }
            if ((well += dt) < 1.5f) return;
            var n = patient;
            Release(false);
            g.Toast(n.Profile.Name + " IS PATCHED UP");
            g.Stats.Practice(Skill.Survival, 4f);
            if (!n.companion) n.State.disposition = Mathf.Min(100, n.State.disposition + 10);
            MadMax.Story.Story.Note("treat:" + n.Profile.id);
            MadMax.Story.Story.Note("clinic_treat");
        }

        void Say(MadMax.Game.WastelandGame g, string text)
        {
            if (Time.time < toastAt) return;
            toastAt = Time.time + 2f;
            g.Toast(text);
        }
    }
}
