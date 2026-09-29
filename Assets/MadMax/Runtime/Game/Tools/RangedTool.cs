using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Guns, bows and launchers (roadmap 13). Hitscan (pellets or one bullet) or a physical <see cref="Projectile"/>;
    /// a magazine of rounds loaded from the pack's ammo (R reloads, one-round weapons reload by themselves), crude guns jam
    /// (R clears it; worse when worn), and every shot makes noise NPCs hear (bows and crossbows barely any). Holding RMB
    /// aims: steadier (half the spread), shoulder camera, and in the top-down views the shot goes at the cursor.</summary>
    public class RangedTool : HandTool
    {
        public int pellets = 8;
        public float spread = 5f;
        public float range = 30f;
        public float power = 0.35f;
        public string ammo = ItemIds.Shells;
        public ResourceType ammoResource;                    // slingshot: stones from the pack instead of an item
        public int magazine = 1;
        public float reloadTime = 1.5f;
        public float jamChance;
        public Projectile.Kind? projectile;
        public float projectileSpeed = 40f;
        public string recover;                               // projectile item picked up again (arrows, bolts)
        public float noise = 80f;
        public string sound = "shotgun";
        public float shake = 1.5f;
        public float aimZoom = 0.75f;                        // first-person field of view while aiming (scoped rifle: less)
        public Transform muzzle;

        public bool Jammed { get; private set; }
        public bool Reloading => Time.time < reloadUntil;
        float reloadUntil;
        int reloadTo = -1;

        /// <summary>Rounds left in the pack.</summary>
        public int Reserve(WastelandGame g) => ammoResource != ResourceType.None ? g.Inventory.Get(ammoResource) : g.Inventory.GetItem(ammo);
        public string AmmoName => ammoResource != ResourceType.None ? ResourceInfo.Name(ammoResource) : ItemCatalog.Name(ammo);

        void Start()
        {
            var g = WastelandGame.Instance;
            if (g && g.Player && g.Player.Tool == this && g.Rounds(id) == 0) BeginReload(g);        // chamber a round when drawn
        }

        void Update()
        {
            if (reloadTo < 0 || Reloading) return;
            var g = WastelandGame.Instance;
            if (g) FinishReload(g);
        }

        /// <summary>R: clear a jam, or top the magazine up from the pack.</summary>
        public void ReloadKey(WastelandGame g)
        {
            if (Reloading) return;
            if (Jammed)
            {
                Jammed = false; reloadUntil = Time.time + 0.9f;
                MadMax.Audio.Sfx.Play("ratchet", transform.position, 0.6f, 1.4f);
                g.Toast("CLEARED THE JAM");
                return;
            }
            if (!BeginReload(g)) g.Toast(g.Rounds(id) >= magazine ? toolName + " IS LOADED" : "NO " + AmmoName);
        }

        bool BeginReload(WastelandGame g)
        {
            int have = g.Rounds(id);
            if (have >= magazine || Reserve(g) <= 0) return false;
            reloadTo = magazine;
            reloadUntil = Time.time + reloadTime * (g.Stats != null ? Mathf.Max(0.5f, 1f - g.Stats.Level(MadMax.RPG.Skill.Firearms) * 0.04f) : 1f);
            MadMax.Audio.Sfx.Play("ratchet", transform.position, 0.5f, 1.6f);
            if (reloadTime > 0.3f) g.Toast("RELOADING " + toolName);
            return true;
        }

        void FinishReload(WastelandGame g)
        {
            int have = g.Rounds(id), want = reloadTo - have;
            reloadTo = -1;
            int take = Mathf.Min(want, Reserve(g));
            if (take <= 0) return;
            if (ammoResource != ResourceType.None) g.Inventory.TrySpend(ammoResource, take);
            else for (int i = 0; i < take; i++) g.Inventory.TakeItem(ammo);
            g.SetRounds(id, have + take);
        }

        public override void Strike(PlayerCharacter user)
        {
            var game = WastelandGame.Instance;
            if (!game) return;
            if (Reloading) return;
            var origin = muzzle ? muzzle.position : user.transform.position + Vector3.up * 1.3f;
            if (Jammed) { MadMax.Audio.Sfx.Play("click", origin, 0.8f, 0.7f); game.Toast("JAMMED!  [R] CLEAR IT"); return; }
            if (game.Rounds(id) <= 0)
            {
                // one-round weapons (bow, crossbow, pipe guns) load as part of the action; the rest click dry
                if (!BeginReload(game)) { MadMax.Audio.Sfx.Play("click", origin, 0.8f, 1f); game.Toast("NO " + AmmoName); return; }
                if (reloadTime > 0.05f) return;
                reloadUntil = 0f; FinishReload(game);
            }
            if (jamChance > 0f && Random.value < jamChance * (2f - game.Condition(id)))
            {
                Jammed = true;
                MadMax.Audio.Sfx.Play("click", origin, 0.9f, 0.6f);
                game.Toast("JAMMED!  [R] CLEAR IT");
                return;
            }
            game.SetRounds(id, game.Rounds(id) - 1);

            var aim = game.AimDirection(user, origin, range);
            var stats = game.Stats;
            float sp = spread * (stats != null ? stats.Spread : 1f) * game.AimPenalty * (2f - game.QualityPower(id)) * (game.Aiming ? 0.5f : 1f);
            var fx = DebrisSystem.Instance;
            bool anyHit = false;
            MadMax.Audio.Sfx.Play(sound, origin, sound == "shotgun" ? 1f : 0.7f, Random.Range(0.95f, 1.05f) * (sound == "shotgun" && pellets == 1 ? 1.35f : 1f), Mathf.Max(20f, noise * 1.5f));
            if (projectile.HasValue)
            {
                var d = Quaternion.Euler(Random.Range(-sp, sp), Random.Range(-sp, sp), 0f) * aim;
                float spd = projectileSpeed * (0.85f + 0.15f * game.QualityPower(id));
                Projectile.Launch(projectile.Value, origin, d * spd + user.Velocity, power, recover, user.gameObject, GetComponent<MeshRenderer>().sharedMaterial);
                anyHit = true;
            }
            else
            {
                for (int i = 0; i < pellets; i++)
                {
                    var d = Quaternion.Euler(Random.Range(-sp, sp), Random.Range(-sp, sp), 0f) * aim;
                    if (!Physics.Raycast(origin, d, out var hit, range, ~0, QueryTriggerInteraction.Ignore) || hit.collider.transform.IsChildOf(user.transform)) continue;
                    var dmg = hit.collider.GetComponentInParent<IDamageable>();
                    if (dmg != null) { dmg.ApplyHit(hit.point, d, power, 0.1f, user.gameObject); anyHit = true; }
                    if (hit.rigidbody && !hit.rigidbody.isKinematic) hit.rigidbody.AddForceAtPosition(d * Mathf.Min(40f, hit.rigidbody.mass * 3f), hit.point, ForceMode.Impulse);
                    if (fx) fx.EmitPuff(hit.point, new Color32(190, 150, 110, 255), 0.06f, hit.normal * 0.8f + Vector3.up * 0.5f, 0.5f);
                }
                if (fx)
                    for (int i = 0; i < 5; i++)
                        fx.EmitPuff(origin + aim * 0.1f, i < 2 ? new Color32(255, 240, 180, 255) : new Color32(255, 170, 60, 255), 0.07f, aim * Random.Range(2f, 5f) + Random.insideUnitSphere, 0.12f);
            }
            if (noise > 0f) MadMax.Npc.NpcDirector.Instance?.Noise(origin, noise);
            if (anyHit) stats?.Practice(MadMax.RPG.Skill.Firearms, 2f);
            if (user == game.Player) game.WearTool(id, jamChance > 0f ? 0.008f : 0.004f);   // crude guns wear out
            if (game.cameraRig) game.cameraRig.Shake(shake);
            // one-round weapons chamber the next one straight away when they can
            if (magazine == 1 && reloadTime > 0.05f && game.Rounds(id) == 0) BeginReload(game);
        }
    }
}
