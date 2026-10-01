using MadMax.Items;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>The glass head of a lamp (child "Head" with its own collider): a shot, a thrown rock or a tool blow
    /// smashes the lamp (<see cref="PoweredLight.Smash"/>) instead of damaging the post.</summary>
    public class LampHead : MonoBehaviour, IDamageable
    {
        public PoweredLight lamp;

        public void ApplyHit(Vector3 point, Vector3 direction, float power, float radius, GameObject source)
        {
            if (!lamp) lamp = GetComponentInParent<PoweredLight>();
            if (lamp && power >= 0.1f) lamp.Smash(point);
        }

        /// <summary>Give a lamp a smashable head: a box of <paramref name="size"/> around <paramref name="center"/> (local).</summary>
        public static LampHead Fit(GameObject go, Vector3 center, Vector3 size)
        {
            var h = new GameObject("Head");
            h.transform.SetParent(go.transform, false);
            var box = h.AddComponent<BoxCollider>();
            box.center = center; box.size = size;
            var lh = h.AddComponent<LampHead>();
            lh.lamp = go.GetComponent<PoweredLight>();
            return lh;
        }
    }
}
