using UnityEngine;

namespace MadMax.World
{
    /// <summary>Caps dynamic lights by distance to the camera and count (LIGHT DETAIL setting: 0 vehicles only,
    /// 1 low, 2 high). Call <see cref="Allowed"/> from a light's Update.</summary>
    public static class LightBudget
    {
        public static int Detail = 2;
        static int frame = -1, used;
        static Camera cam;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Detail = 2; frame = -1; used = 0; cam = null; }

        public static bool Allowed(Light l, bool vehicle = false)
        {
            if (!l) return false;
            if (Time.frameCount != frame) { frame = Time.frameCount; used = 0; }
            if (Detail <= 0 && !vehicle) return false;
            if (!cam || !cam.isActiveAndEnabled) cam = Camera.main ?? Object.FindAnyObjectByType<Camera>();
            float reach = Detail >= 2 ? 80f : 35f;
            int cap = Detail >= 2 ? 40 : 12;
            if (cam && (l.transform.position - cam.transform.position).sqrMagnitude > reach * reach * (vehicle ? 2f : 1f)) return false;
            if (used >= cap) return false;
            used++;
            return true;
        }
    }
}
