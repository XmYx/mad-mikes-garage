using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Parkour you can feel (2026-10-29): hands meet what they vault and climb — a palm slap on the moment they
    /// land (a clang off a car, a knock off planks, a scuff off brick and stone, the ground's own step sound) with a little
    /// dust, rust or splinters of that surface; a mantle catches the edge with both hands, then the knee scrapes over it.
    /// A slide hisses along with a dust trail in the ground's colour (snow powder, mud spatter, sand); a landing from
    /// more than a jump thumps down with a ring of dust that grows with the fall.</summary>
    public partial class PlayerCharacter
    {
        /// <summary>Hands / knees that met a surface, landings thumped (tests), and what the last hand met.</summary>
        public static int HandPlants, LandingThumps;
        public static string LastPlant;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetContactStatics() { HandPlants = 0; LandingThumps = 0; LastPlant = null; }

        /// <summary>Contact moments of a vault (hands on top at 32 %) and a mantle (hands catch the edge at 20 %, the
        /// knee goes over at 70 %).</summary>
        void ContactTick(float before, float now)
        {
            var fwd = moveTo - moveFrom; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = transform.forward;
            fwd.Normalize();
            if (moveKind == 0 && before < 0.32f && now >= 0.32f)
                Plant(moveMid + Vector3.up * 0.4f, 1.6f, fwd, 1f, "HANDS");
            else if (moveKind == 1)
            {
                var edge = new Vector3(moveMid.x, climbTop + 0.3f, moveMid.z) + fwd * 0.38f;
                if (before < 0.2f && now >= 0.2f) Plant(edge, 0.7f, fwd, 1.2f, "HANDS");
                if (before < 0.7f && now >= 0.7f) Plant(edge + fwd * 0.1f, 0.7f, fwd, 0.55f, "KNEE");
            }
        }

        void Plant(Vector3 from, float down, Vector3 fwd, float weight, string what)
        {
            if (!Physics.Raycast(from, Vector3.down, out var hit, down, ~0, QueryTriggerInteraction.Ignore) || hit.collider.transform.IsChildOf(transform)) return;
            HandPlants++;
            string key; Color dust; float pitch = 1f;
            if (hit.collider.GetComponentInParent<MadMax.Vehicles.VehicleDriver>()) { key = "hit_metal"; dust = (Color)MadMax.Voxel.Pal.Rust[3]; pitch = 1.35f; LastPlant = what + ":METAL"; }
            else if (hit.collider.GetComponentInParent<MadMax.Building.Placeable>()) { key = "hit_wood"; dust = (Color)MadMax.Voxel.Pal.Wood[3]; pitch = 1.25f; LastPlant = what + ":WOOD"; }
            else if (hit.collider.GetComponentInParent<DestructibleVoxels>()) { key = "scratch"; dust = new Color(0.62f, 0.58f, 0.54f); pitch = 0.8f; LastPlant = what + ":STONE"; }
            else
            {
                var t = DeformableTerrain.Instance;
                key = t ? StepSound(hit.point, t.SurfaceAt(hit.point.x, hit.point.z)) : "step_gravel";
                dust = GroundDust(hit.point);
                LastPlant = what + ":GROUND";
            }
            MadMax.Audio.Sfx.Play(key, hit.point, 0.22f * weight, pitch * Random.Range(0.92f, 1.08f), 12f);
            dust.a = 0.6f;
            int n = Mathf.RoundToInt(3 + 3 * weight);
            for (int i = 0; i < n; i++)
            {
                var v = (Random.insideUnitSphere * 0.5f + Vector3.up * 0.35f + fwd * 0.25f) * weight;
                Fx.Smoke(hit.point + Vector3.up * 0.03f + Random.insideUnitSphere * 0.06f, v, 0.045f * weight, dust, 0.6f);
            }
        }

        /// <summary>The colour the ground kicks up: snow powder, dark mud, sand, road grit or dry earth.</summary>
        public static Color GroundDust(Vector3 p)
        {
            var t = DeformableTerrain.Instance;
            if (!t) return new Color(0.7f, 0.55f, 0.4f);
            var s = t.SurfaceAt(p.x, p.z);
            if (Weather.LocalSnow > 0.35f && s.road < 0.5f) return new Color(0.92f, 0.94f, 0.98f);
            if (s.mud > 0.35f) return new Color(0.3f, 0.21f, 0.14f);
            if (s.road > 0.5f) return new Color(0.5f, 0.48f, 0.45f);
            return t.BiomeAt(p.x, p.z) == Biome.Desert ? new Color(0.8f, 0.6f, 0.38f) : new Color(0.66f, 0.52f, 0.38f);
        }

        /// <summary>Down from more than a hop: a thump on the ground's own sound and a ring of its dust, by the fall.</summary>
        void LandingDust(float speed)
        {
            if (speed < 4.5f) return;
            LandingThumps++;
            var p = transform.position;
            var t = DeformableTerrain.Instance;
            float k = Mathf.Clamp01((speed - 4.5f) / 9f);
            MadMax.Audio.Sfx.Play(t ? StepSound(p, t.SurfaceAt(p.x, p.z)) : "step_gravel", p, 0.25f + 0.35f * k, 0.6f - 0.15f * k, 14f);
            if (k > 0.3f) MadMax.Audio.Sfx.Play("punch", p + Vector3.up * 0.3f, 0.2f * k, 0.55f, 10f);
            var c = GroundDust(p); c.a = 0.55f;
            int n = 6 + Mathf.RoundToInt(k * 10f);
            for (int i = 0; i < n; i++)
            {
                var d = Quaternion.Euler(0f, i * 360f / n + Random.Range(-10f, 10f), 0f) * Vector3.forward;
                Fx.Smoke(p + d * 0.2f + Vector3.up * 0.05f, d * (0.8f + 1.6f * k) + Vector3.up * 0.25f, 0.06f + 0.05f * k, c, 0.8f);
            }
        }
    }
}
