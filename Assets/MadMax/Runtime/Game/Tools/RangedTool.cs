using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Pellet gun (pipe shotgun): hitscan pellets that hit IDamageable targets; consumes one ammo item per shot.</summary>
    public class RangedTool : HandTool
    {
        public int pellets = 8;
        public float spread = 5f;
        public float range = 30f;
        public float power = 0.35f;
        public string ammo = ItemIds.Shells;
        public Transform muzzle;

        public override void Strike(PlayerCharacter user)
        {
            var game = WastelandGame.Instance;
            var fx = DebrisSystem.Instance;
            if (game && !game.Inventory.TakeItem(ammo)) { game.Toast("NO SHELLS"); return; }
            var origin = muzzle ? muzzle.position : user.transform.position + Vector3.up * 1.3f;
            MadMax.Audio.Sfx.Play("shotgun", origin, 1f, Random.Range(0.95f, 1.05f), 120f);
            var aim = user.transform.forward;
            var rig = game ? game.cameraRig : null;
            if (rig && (rig.mode == ViewMode.FirstPerson || rig.mode == ViewMode.ThirdPerson))
            {
                var cam = rig.pixel.transform;
                aim = Physics.Raycast(cam.position, cam.forward, out var ch, range, ~0, QueryTriggerInteraction.Ignore) ? (ch.point - origin).normalized : cam.forward;
            }
            var stats = game ? game.Stats : null;
            float sp = spread * (stats != null ? stats.Spread : 1f) * (game ? game.AimPenalty : 1f);
            bool anyHit = false;
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
            if (anyHit) stats?.Practice(MadMax.RPG.Skill.Firearms, 3f);
            if (rig) rig.Shake(1.5f);
        }
    }
}
