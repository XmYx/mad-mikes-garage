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
            int kind = KindOf(cause);
            InjuryRules.Apply(Stats.injuries, amount, cause, injuryRnd, kind < 0 ? null : (System.Func<BodyZone, float>)(z => Protection(z, kind)), z => Deflected(z, amount));
            // clothes take the damage too: a crash or fire everything, a hit what covers the wound
            if (cause == "CRASH") TearClothes(null, amount / 150f);
            else if (cause == "BURNED") TearClothes(null, amount / 40f);
            else if (Stats.injuries.Count > before) TearClothes(Stats.injuries[Stats.injuries.Count - 1].zone, amount / 60f);
            if (Stats.injuries.Count > before)
            {
                var inj = Stats.injuries[Stats.injuries.Count - 1];
                Toast(Injury.WoundNames[(int)inj.type] + ": " + Injury.ZoneNames[(int)inj.zone] + (inj.Bleeding ? " (BLEEDING)" : "") + "  [O] HEALTH");
                if (cause != "BURNED") MadMax.World.BloodStains.Splash(Player.transform.position, amount / 40f);
                if (cause != "BURNED" && inj.Bleeding && Player.Rig) Player.Rig.Bleed(WoundPoint(inj.zone), Mathf.Clamp(amount / 30f, 0.6f, 1.6f));
            }
        }

        /// <summary>A point on the player's body in a wound zone (for the blood splat).</summary>
        Vector3 WoundPoint(BodyZone z)
        {
            var part = z switch
            {
                BodyZone.Head => BodyPart.Head, BodyZone.ArmL => BodyPart.UpperArmL, BodyZone.ArmR => BodyPart.UpperArmR,
                BodyZone.HandL => BodyPart.HandL, BodyZone.HandR => BodyPart.HandR, BodyZone.LegL => BodyPart.ThighL, BodyZone.LegR => BodyPart.ThighR,
                BodyZone.FootL => BodyPart.ShinL, BodyZone.FootR => BodyPart.ShinR, _ => BodyPart.Chest
            };
            var b = Player.Rig.Bone(part);
            bool up = part == BodyPart.Head || part == BodyPart.Chest;
            var along = b.rotation * (up ? Vector3.up : Vector3.down) * (float)(0.05 + injuryRnd.NextDouble() * 0.2);
            var side = Quaternion.AngleAxis((float)injuryRnd.NextDouble() * 360f, b.rotation * Vector3.up) * (b.rotation * Vector3.forward);
            return b.position + along + side * 0.4f;
        }

        /// <summary>Clothing totals (warmth, cooling) of the current outfit.</summary>
        public (float warmth, float cooling) Insulation()
        {
            float w = 0f, c = 0f;
            // torn garments keep half their warmth; soaked clothes lose most of it
            foreach (var id in Player.Rig.outfit) { var d = ClothingLibrary.Get(id); if (d != null) { w += d.warmth * (GarmentCondition(id) < 0.35f ? 0.5f : 1f); c += d.cooling; } }
            return (w * (1f - 0.55f * Stats.wetness), c);
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
            if (Player.Swimming) env = Mathf.Min(env, outside - 4f);
            // wet clothes chill (evaporation), worse in snow; in the heat they cool you a little
            env -= s.wetness * (Weather.Snowing ? 7f : 5f);
            var (warmth, cooling) = Insulation();
            FeltTemperature = env < 20f ? Mathf.Min(20f, env + warmth * 1.4f) : env - cooling * 1.2f;
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

        /// <summary>How much an injury hampers its limb, 0..1 (fractures most; splints and healing help).</summary>
        static float Hamper(Injury i) => (i.type switch
        {
            Wound.Fracture => i.splinted ? 0.55f : 0.95f, Wound.DeepWound => 0.55f, Wound.Laceration => 0.3f, Wound.Burn => 0.25f, Wound.Bruise => 0.12f, _ => 0.08f
        }) * Mathf.Clamp01(i.type == Wound.Fracture ? 0.5f + i.severity * 0.5f : i.severity);

        float Worst(params BodyZone[] zones)
        {
            float m = 0f;
            foreach (var i in Stats.injuries) if (System.Array.IndexOf(zones, i.zone) >= 0) m = Mathf.Max(m, Hamper(i));
            return Stats.painkilled ? m * 0.45f : m;
        }

        /// <summary>Per-side limp (legs, feet) and arm impairment (arms, hands), 0..1: drive the gait and the actions.</summary>
        public float LimpL => Worst(BodyZone.LegL, BodyZone.FootL);
        public float LimpR => Worst(BodyZone.LegR, BodyZone.FootR);
        public float ArmHurtL => Worst(BodyZone.ArmL, BodyZone.HandL);
        public float ArmHurtR => Worst(BodyZone.ArmR, BodyZone.HandR);
        public float HeadDaze => Worst(BodyZone.Head);
        /// <summary>No running on a badly hurt leg, no jumping on a broken one.</summary>
        public bool CanRunInjured => Mathf.Max(LimpL, LimpR) < 0.5f && BackHurt < 0.3f;   // + a strained back (WastelandGame.Bags)
        public bool CanJumpInjured => Mathf.Max(LimpL, LimpR) < 0.35f && BackHurt < 0.15f;
        /// <summary>Two-handed tools and weapons need two working arms.</summary>
        public bool ArmBroken => Mathf.Max(ArmHurtL, ArmHurtR) >= 0.85f;
        /// <summary>Aim spread multiplier: shaky arms, a dazed head.</summary>
        public float AimPenalty => 1f + Mathf.Max(ArmHurtL, ArmHurtR) * 1.4f + HeadDaze * 0.8f;

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
                return m * BagSpeed;                                                                // a bad back, luggage in hand
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
            if (inj.type == Wound.Strain) { TreatBack(inj); return; }
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
