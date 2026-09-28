using MadMax.Building;
using MadMax.Items;
using MadMax.RPG;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Body temperature (weather, shelter, heaters, fires, wetness, clothing) and injuries (wounds per body
    /// zone, bleeding, infection, treatment with bandages, splints and disinfectant).</summary>
    public partial class WastelandGame
    {
        public float FeltTemperature { get; private set; } = 20f;
        public bool Sheltered { get; private set; }
        float shelterCheck;
        readonly System.Random injuryRnd = new System.Random();

        public void Injure(float amount, string cause)
        {
            int before = Stats.injuries.Count;
            InjuryRules.Apply(Stats.injuries, amount, cause, injuryRnd);
            if (Stats.injuries.Count > before)
            {
                var inj = Stats.injuries[Stats.injuries.Count - 1];
                Toast(Injury.WoundNames[(int)inj.type] + ": " + Injury.ZoneNames[(int)inj.zone] + (inj.Bleeding ? " (BLEEDING)" : "") + "  [O] HEALTH");
                if (cause != "BURNED") MadMax.World.BloodStains.Splash(Player.transform.position, amount / 40f);
            }
        }

        /// <summary>Clothing totals (warmth, cooling) of the current outfit.</summary>
        public (float warmth, float cooling) Insulation()
        {
            float w = 0f, c = 0f;
            foreach (var id in Player.Rig.outfit) { var d = ClothingLibrary.Get(id); if (d != null) { w += d.warmth; c += d.cooling; } }
            return (w, c);
        }

        float dripTimer;

        void UpdateHealth(float dt)
        {
            if (Vitals == null || Vitals.Dead) return;
            // bleeding leaves a trail on the ground (on foot)
            if (!Current && (dripTimer -= dt) <= 0f)
            {
                dripTimer = 1.1f;
                foreach (var inj in Stats.injuries) if (inj.Bleeding) { MadMax.World.BloodStains.Drip(Player.transform.position); break; }
            }
            var s = Stats;
            // ---- environment temperature at the player
            if ((shelterCheck -= dt) <= 0f)
            {
                shelterCheck = 1f;
                var head = Player.transform.position + Vector3.up * 1.8f;
                Sheltered = Physics.Raycast(head, Vector3.up, out var roof, 14f, ~0, QueryTriggerInteraction.Ignore) && !roof.collider.GetComponentInParent<VehicleDriver>();
            }
            float outside = Weather.Temperature - DayNight.Darkness * 7f;
            float env = outside;
            if (Current && Current.TryGetComponent<VehicleClimate>(out var cabin) && cabin.Enclosed) env = cabin.CabinTemperature;
            else if (Sheltered) env = Mathf.Lerp(outside, 17f, 0.4f);
            var pos = Player.transform.position;
            env += Climate.At(pos) * (Sheltered || Current ? 1f : 0.35f);
            foreach (var f in Fire.All) { if (!f) continue; float d = Vector3.Distance(f.transform.position, pos); if (d < 5f) env += 14f * f.intensity * (1f - d / 5f); }
            bool wet = Player.Swimming || (Weather.Raining && !Sheltered && !Current);
            if (Player.Swimming) env = Mathf.Min(env, outside - 4f);
            if (wet) env -= Weather.Snowing ? 7f : 5f;
            var (warmth, cooling) = Insulation();
            FeltTemperature = env < 20f ? Mathf.Min(20f, env + warmth * 1.4f * (wet ? 0.5f : 1f)) : env - cooling * 1.2f;
            // ---- core temperature drifts outside the comfort band
            float target = 37f - Mathf.Max(0f, 12f - FeltTemperature) * 0.14f + Mathf.Max(0f, FeltTemperature - 30f) * 0.1f;
            target = Mathf.Clamp(target, 28f, 43f);
            s.bodyTemp = Mathf.MoveTowards(s.bodyTemp, target, dt * (Mathf.Abs(target - s.bodyTemp) * 0.004f + 0.002f));
            float t = s.bodyTemp;
            if (t < 35f) Vitals.Hurt(dt * (t < 33f ? 1f : 0.25f), "HYPOTHERMIA");
            if (t > 39f) { Vitals.Hurt(dt * (t > 40.5f ? 1f : 0.3f), "HEATSTROKE"); s.stamina = Mathf.Max(0f, s.stamina - dt * 2f); }
            if (t > 38f) s.thirst = Mathf.Max(0f, s.thirst - dt * 100f / (28f * 60f));          // sweating: double thirst
            if (Mathf.Abs(t - 37f) > 1.4f) Stats.Practice(Skill.Survival, dt * 0.05f);

            // ---- wounds
            float nutrition = Mathf.Clamp01(Mathf.Min(s.hunger, s.thirst) / 60f);
            float loss = 0f;
            for (int i = s.injuries.Count - 1; i >= 0; i--)
            {
                var inj = s.injuries[i];
                loss += inj.Tick(dt, s.hygiene, nutrition);
                if (inj.infection > 0.6f && s.sick <= 0f) { s.sick = 60f; Toast("INFECTED WOUND: FEVER"); }
                if (inj.severity <= 0f) { s.injuries.RemoveAt(i); Toast(Injury.WoundNames[(int)inj.type] + " HEALED"); }
            }
            if (loss > 0f) { s.health -= loss; if (s.health <= 0f) { s.health = 0f; PlayerDied("BLED OUT"); } }
        }

        /// <summary>Movement / tool penalties from leg and arm injuries and hypothermia.</summary>
        public float InjurySpeed
        {
            get
            {
                float m = 1f;
                foreach (var i in Stats.injuries)
                {
                    bool leg = i.zone == BodyZone.LegL || i.zone == BodyZone.LegR || i.zone == BodyZone.FootL || i.zone == BodyZone.FootR;
                    if (!leg) continue;
                    m *= i.type == Wound.Fracture ? (i.splinted ? 0.7f : 0.45f) : 1f - 0.15f * i.severity;
                }
                if (Stats.bodyTemp < 35f) m *= 0.75f;
                return m;
            }
        }

        public float InjuryToolSpeed
        {
            get
            {
                float m = 1f;
                foreach (var i in Stats.injuries)
                    if (i.zone == BodyZone.ArmL || i.zone == BodyZone.ArmR || i.zone == BodyZone.HandL || i.zone == BodyZone.HandR)
                        m *= i.type == Wound.Fracture ? (i.splinted ? 0.75f : 0.5f) : 1f - 0.1f * i.severity;
                return m;
            }
        }

        /// <summary>Apply the fitting treatment to an injury with what is in the pack.</summary>
        public void Treat(Injury inj)
        {
            if (inj == null) return;
            if (inj.type == Wound.Fracture && !inj.splinted)
            {
                if (Inventory.TakeItem("med_splint")) { inj.splinted = true; Toast("SPLINT APPLIED"); Stats.Practice(Skill.Survival, 4f); }
                else Toast("NEED A SPLINT (WOOD + CLOTH)");
                return;
            }
            if (inj.type != Wound.Bruise && inj.type != Wound.Fracture && !inj.disinfected && Inventory.TakeItem("med_disinfectant"))
            {
                inj.disinfected = true; inj.infection = Mathf.Max(0f, inj.infection - 0.5f); Toast("DISINFECTED"); Stats.Practice(Skill.Survival, 2f);
                return;
            }
            if (inj.type != Wound.Bruise && (!inj.bandaged || inj.BandageDirty))
            {
                if (Inventory.TakeItem("med_bandage")) { inj.bandaged = true; inj.bandageAge = 0f; Toast(inj.BandageDirty ? "BANDAGE CHANGED" : "BANDAGED"); Stats.Practice(Skill.Survival, 2f); }
                else if (Inventory.Get(ResourceType.Cloth) >= 1 && Inventory.TrySpend(ResourceType.Cloth, 1)) { inj.bandaged = true; inj.bandageAge = 300f; Toast("RIPPED CLOTH BANDAGE"); }
                else Toast("NEED BANDAGES OR CLOTH");
                return;
            }
            Toast("NOTHING MORE TO DO: REST AND EAT");
        }
    }
}
