using MadMax.Game;
using MadMax.World;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>Melee guard and directional hit reactions (roadmap 13): fighters with a hand weapon sometimes catch a blow
    /// from the front on it (raiders more, bosses most; sledge-weight blows go through), the player's parry throws them off
    /// balance, every flinch gives way from the side the blow came from. Proxies replay reactions and guards from the
    /// host's flags (<see cref="FlagReact"/>, <see cref="FlagStagger"/>, <see cref="FlagGuard"/>) and play their own at
    /// once when this client lands the blow.</summary>
    public partial class Npc
    {
        public const byte FlagReact = 32, FlagStagger = 64, FlagGuard = 128;
        public const float GuardHold = 0.45f, GuardAgain = 1.6f;

        Vector3 reactFrom;                         // where the last blow came from (world, toward the striker; length = weight)
        float guardUntil, guardReady;

        /// <summary>The frame a blow was caught on their guard (the striker's tool rings off it); −1 = never.</summary>
        public int GuardedFrame { get; private set; } = -1;
        /// <summary>Weapon up across the body after catching a blow.</summary>
        public bool Guarding => Time.time < guardUntil;
        /// <summary>The side the last blow came from, in the body's frame (x right, z ahead; tests); zero = none.</summary>
        public Vector3 LastHitFrom => reactFrom.sqrMagnitude > 0.01f ? transform.InverseTransformDirection(reactFrom) : Vector3.zero;

        /// <summary>Seconds from <see cref="BeginSwing"/> to the blow landing (tests time a parry by it).</summary>
        public float StrikeDelay => tool ? tool.swingDuration * tool.strikeAt : 0.3f;
        /// <summary>Tests: who this person is fighting and what they are doing.</summary>
        public string DebugFoe => foe ? foe.name : "-";
        public string DebugMode => mode.ToString();

        Vector3 HitFromLocal => reactFrom.sqrMagnitude > 0.01f ? transform.InverseTransformDirection(reactFrom) : Vector3.zero;
        float HitT => (Time.time - reactAt) / PlayerCharacter.HitLeanTime;

        void SetReactFrom(Vector3 from, bool heavy)
        {
            from.y = 0f;
            reactFrom = from.sqrMagnitude > 1e-4f ? from.normalized * (heavy ? 1.5f : 1f) : Vector3.zero;
        }

        /// <summary>The player parried their blow: thrown off balance away from them, no swing for a beat.</summary>
        public void Parried(Vector3 by)
        {
            if (mode == Mode.Dead) return;
            swing = -1f; CancelInvoke(nameof(MeleeHit));
            var d = Flat(transform.position - by);
            staggerDir = d.sqrMagnitude > 0.01f ? d.normalized : -transform.forward;
            staggerUntil = Time.time + StaggerTime;
            reactClip = HumanClips.Get("stagger") != null ? "stagger" : "flinch";
            reactAt = Time.time;
            SetReactFrom(-staggerDir, true);
            attackCd = Mathf.Max(attackCd, 1.8f);
        }

        /// <summary>A hand-weapon blow from the front while they fight with one: sometimes caught on the guard (a jar, no
        /// wound, a quick riposte). True = stopped.</summary>
        bool TryGuard(Vector3 point, Vector3 direction, float power, GameObject source)
        {
            if (mode != Mode.Fight || !(tool is MeleeTool) || swing >= 0f || Staggering || seated || Time.time < guardReady || !source) return false;
            if (power >= 1.1f) return false;                                                      // sledge-weight blows go through
            var g = WastelandGame.Instance;
            var byNpc = source.GetComponentInParent<Npc>();
            bool melee = byNpc ? byNpc != this && byNpc.tool is MeleeTool
                               : g && g.Player && source.transform.IsChildOf(g.Player.transform) && g.Player.Tool is MeleeTool;
            if (!melee) return false;
            var from = Flat(-direction);
            if (from.sqrMagnitude < 1e-4f || Vector3.Dot(transform.forward, from.normalized) < 0.5f) return false;   // only what they see coming
            float chance = Profile.role == NpcRole.RaiderBoss ? 0.4f : Profile.Raider ? 0.25f : 0.15f;
            if (Profile.temper == Temper.Proud || Profile.temper == Temper.Gruff) chance += 0.1f;
            if (Random.value >= chance) return false;
            guardUntil = Time.time + GuardHold; guardReady = Time.time + GuardAgain;
            GuardedFrame = Time.frameCount;
            health = Mathf.Max(1f, health - power * 28f * 0.1f);                                    // the jar of it
            reactClip = "flinch"; reactAt = Time.time;
            SetReactFrom(from, false);
            attackCd = Mathf.Min(attackCd, 0.35f);                                                // a riposte comes quick
            if (byNpc) { hurtBy = byNpc; hurtByUntil = Time.time + 8f; }
            MadMax.Audio.Sfx.Play("hit_metal", point, 0.8f, Random.Range(1.1f, 1.3f));
            Fx.Sparks(point, (from.normalized + Vector3.up).normalized, 5, new Color(1f, 0.8f, 0.45f));
            return true;
        }

        /// <summary>A client's copy struck or told it was struck: the flinch / stagger plays here (no brain, no damage).</summary>
        void ProxyReact(Vector3 from, bool heavy)
        {
            reactClip = heavy && HumanClips.Get("stagger") != null ? "stagger" : "flinch";
            reactAt = Time.time;
            SetReactFrom(from, heavy);
        }

        /// <summary>Reaction / guard bits for the snapshot.</summary>
        byte ReactFlags => (byte)((Time.time - reactAt < 0.4f ? FlagReact : 0) | (Staggering ? FlagStagger : 0) | (Guarding ? FlagGuard : 0));

        /// <summary>New reaction bits from the host: replay them (the side is unknown here: from the front).</summary>
        void ProxyFlagsChanged(byte old, byte flags)
        {
            byte rise = (byte)(flags & ~old);
            if ((rise & (FlagReact | FlagStagger)) != 0 && Time.time - reactAt > 0.35f) ProxyReact(transform.forward, (flags & FlagStagger) != 0);
        }
    }
}
