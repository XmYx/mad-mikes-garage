using MadMax.RPG;
using MadMax.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game
{
    /// <summary>Melee guard (roadmap 13): holding RMB (pad LT) with a melee tool while someone hostile is close raises the
    /// weapon across the body. A blow from the front that lands within <see cref="ParryWindow"/> of raising it is parried
    /// (no harm, the striker reels back off balance); later it is blocked for stamina (no wound, a flinch); out of stamina
    /// the guard breaks (full blow, stagger, no guard for a beat). Blows from the sides or behind get through.</summary>
    public partial class WastelandGame
    {
        public const float ParryWindow = 0.25f, GuardStamina = 14f, GuardReach = 7f, GuardArc = 0.25f, GuardBrokenTime = 1.4f;

        /// <summary>The player's guard is up.</summary>
        public bool Guarding { get; private set; }
        /// <summary>Automation: hold the guard up without input or a threat nearby.</summary>
        [System.NonSerialized] public bool ForceGuard;
        /// <summary>Outcome of the last blow met with the guard: "parry", "block", "broken" (tests); null = none yet.</summary>
        public string LastGuard { get; private set; }
        float guardSince = -9f, guardBrokenUntil;

        /// <summary>Someone hostile within <paramref name="reach"/> on foot (the guard only comes up in a fight).</summary>
        public static bool ThreatNear(Vector3 p, float reach)
        {
            foreach (var n in MadMax.Npc.Npc.All)
                if (n && n.Alive && n.Hostile && !n.companion && (n.transform.position - p).sqrMagnitude < reach * reach) return true;
            return false;
        }

        /// <summary>Whether RMB belongs to the guard now (a melee tool in hand in a fight): no context menu on it.</summary>
        bool GuardOwnsRmb => !Current && Player && Player.Tool is MeleeTool && (ForceGuard || ThreatNear(Player.transform.position, GuardReach));

        void UpdateGuard(Mouse mouse, Gamepad pad)
        {
            bool owns = GuardOwnsRmb;
            if (owns && !ForceGuard && !Menus.IsOpen) Hints.Show("melee_guard", "IN A FIGHT, HOLD RMB TO GUARD WITH YOUR WEAPON: RAISE IT JUST AS THEY STRIKE TO PARRY AND THROW THEM OFF BALANCE. BLOCKING COSTS STAMINA; BLOWS FROM THE SIDE OR BEHIND GET THROUGH");
            bool held = ForceGuard || (mouse != null && mouse.rightButton.isPressed) || (pad != null && pad.leftTrigger.isPressed);
            bool want = held && owns && !Menus.IsOpen && !Player.Swinging && !Player.Ragdolled && Time.time >= guardBrokenUntil
                        && (Vitals == null || !Vitals.Exhausted);
            if (want && !Guarding) guardSince = Time.time;
            Guarding = want;
            Player.guarding = want;
        }

        /// <summary>A melee blow from <paramref name="attacker"/> meets the guard: true = it was stopped (parried or blocked);
        /// <paramref name="broken"/> = the guard gave way (the blow lands as a heavy one).</summary>
        public bool GuardAgainst(MadMax.Npc.Npc attacker, float power, out bool broken)
        {
            broken = false;
            if (!Guarding || !Player || !attacker || Menus.IsOpen) return false;
            var to = attacker.transform.position - Player.transform.position; to.y = 0f;
            if (to.sqrMagnitude < 1e-4f || Vector3.Dot(Player.transform.forward, to.normalized) < GuardArc) return false;   // from the side or behind
            var hand = Player.Tool ? Player.Tool.transform.position : Player.transform.position + Vector3.up * 1.2f;
            string id = Player.Tool ? Player.Tool.id : null;
            // a quick reader of the fight times it a little wider
            float window = ParryWindow + Stats.Level(Skill.Melee) * 0.008f;
            if (Time.time - guardSince <= window)
            {
                LastGuard = "parry";
                attacker.Parried(Player.transform.position);
                MadMax.Audio.Sfx.Play("hit_metal", hand, 0.9f, Random.Range(1.25f, 1.4f));
                Fx.Sparks(hand, (to.normalized + Vector3.up).normalized, 10, new Color(1f, 0.8f, 0.45f));
                if (cameraRig) cameraRig.Shake(1.2f);
                Stats.Practice(Skill.Melee, 4f);
                WearTool(id, 0.006f);
                return true;
            }
            float cost = GuardStamina * Mathf.Max(0.6f, power) * (Player.Tool && Player.Tool.TwoHanded ? 0.8f : 1f);
            if (Vitals != null && Vitals.Stamina < cost)
            {
                LastGuard = "broken"; broken = true;
                Vitals.Spend(cost);
                Guarding = false; Player.guarding = false; guardBrokenUntil = Time.time + GuardBrokenTime;
                MadMax.Audio.Sfx.Play("hit_wood", hand, 0.8f, 0.8f);
                Toast("GUARD BROKEN");
                return false;
            }
            LastGuard = "block";
            Vitals?.Spend(cost);
            Player.React(false, to);
            MadMax.Audio.Sfx.Play(Player.Tool is LightTool ? "hit_wood" : "hit_metal", hand, 0.8f, Random.Range(0.95f, 1.1f));
            Fx.Sparks(hand, (to.normalized + Vector3.up).normalized, 4, new Color(1f, 0.8f, 0.45f));
            if (cameraRig) cameraRig.Shake(0.6f);
            Stats.Practice(Skill.Melee, 1.5f);
            WearTool(id, 0.01f * Mathf.Max(0.6f, power));
            return true;
        }
    }
}
