using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Health and stamina. Running and tool actions spend stamina; health falls from crashes, hard landings
    /// and hazards, and slowly regenerates. At zero health the player dies (respawn, or game over with permadeath).</summary>
    public class PlayerVitals : MonoBehaviour
    {
        public CharacterStats stats;
        public float Health => stats.health;
        public float Stamina => stats.stamina;
        public bool Exhausted { get; private set; }
        public bool Dead => stats.health <= 0f;
        /// <summary>Seconds since the player was last hurt.</summary>
        public float SinceHurt => Time.time - lastHurt;

        float lastSpend, lastHurt;
        PlayerCharacter player;

        void Awake() { player = GetComponent<PlayerCharacter>(); }

        public void Init(CharacterStats s)
        {
            stats = s;
            if (stats.health < 0f) stats.health = stats.MaxHealth;
            if (stats.stamina < 0f) stats.stamina = stats.MaxStamina;
        }

        public bool Spend(float amount)
        {
            if (stats == null) return true;
            lastSpend = Time.time;
            stats.stamina = Mathf.Max(0f, stats.stamina - amount);
            if (stats.stamina <= 0f) Exhausted = true;
            return true;
        }

        public void Hurt(float amount, string cause)
        {
            if (stats == null || Dead || amount <= 0f) return;
            if (WastelandGame.Instance) amount *= WastelandGame.Instance.ArmourFactor(cause);        // body armour
            stats.health -= amount * GameRules.Current.DamageTaken;
            WastelandGame.Instance?.Injure(amount * GameRules.Current.DamageTaken, cause);
            lastHurt = Time.time;
            var game = WastelandGame.Instance;
            if (game)
            {
                if (game.cameraRig) game.cameraRig.Shake(Mathf.Min(6f, amount * 0.2f));
                if (stats.health <= 0f) { stats.health = 0f; game.PlayerDied(cause); }
            }
        }

        void Update()
        {
            if (stats == null || Dead) return;
            float dt = Time.deltaTime;
            bool running = player && player.run && player.Velocity.sqrMagnitude > 4f && !player.SeatedIn;
            if (running) Spend(14f * dt * (WastelandGame.Instance ? WastelandGame.Instance.LimbStamina : 1f));   // heavy or springy prosthetics
            float regenMult = (stats.bodyTemp < 36f ? 0.6f : 1f) * (stats.hunger < 20f || stats.thirst < 20f ? 0.5f : 1f)
                * (stats.fed ? 1.2f : 1f) * (player && player.Sitting && player.SeatedOn ? player.SeatedOn.rest : 1f);
            if (Time.time - lastSpend > 1.2f) stats.stamina = Mathf.Min(stats.MaxStamina, stats.stamina + (10f + stats.Attribute(Attr.Endurance) * 1.5f) * dt * regenMult);
            if (Exhausted && stats.stamina > stats.MaxStamina * 0.3f) Exhausted = false;
            bool bleeding = false; foreach (var inj in stats.injuries) if (inj.Bleeding) { bleeding = true; break; }
            if (Time.time - lastHurt > 8f && !bleeding) stats.health = Mathf.Min(stats.MaxHealth, stats.health + 0.4f * dt);
            if (running) WastelandGame.Instance?.Stats?.Practice(Skill.Survival, dt * 0.15f);
        }
    }
}
