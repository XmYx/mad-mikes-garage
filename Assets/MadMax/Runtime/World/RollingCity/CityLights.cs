using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>The night lamps of a generated city (street lamps, floods, lit flats, bulkheads): hundreds of points in the
    /// owner's local space served by a small pool of real point lights, moved every half second to the lamps nearest the
    /// camera while it is dark (within the <see cref="LightBudget"/>).</summary>
    public class CityLights : MonoBehaviour
    {
        public readonly List<(Vector3 at, Color color, float range)> lamps = new List<(Vector3, Color, float)>();
        const int Pool = 18;
        readonly Light[] pool = new Light[Pool];
        readonly int[] order = new int[Pool];
        float scanAt;
        Camera cam;

        void Start()
        {
            for (int i = 0; i < Pool; i++)
            {
                var go = new GameObject("CityLamp" + i);
                go.transform.SetParent(transform, false);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point; l.shadows = LightShadows.None; l.enabled = false;
                pool[i] = l;
            }
        }

        void Update()
        {
            if (Time.time < scanAt) { Keep(); return; }
            scanAt = Time.time + 0.5f;
            bool dark = DayNight.Darkness > 0.3f;
            if (!cam || !cam.isActiveAndEnabled) cam = Camera.main;
            if (!dark || !cam || lamps.Count == 0) { foreach (var l in pool) if (l) l.enabled = false; return; }
            var local = transform.InverseTransformPoint(cam.transform.position);
            for (int i = 0; i < Pool; i++) order[i] = -1;
            var best = new float[Pool];
            for (int i = 0; i < Pool; i++) best[i] = float.MaxValue;
            for (int k = 0; k < lamps.Count; k++)
            {
                float d = (lamps[k].at - local).sqrMagnitude;
                if (d >= best[Pool - 1]) continue;
                int j = Pool - 1;
                while (j > 0 && best[j - 1] > d) { best[j] = best[j - 1]; order[j] = order[j - 1]; j--; }
                best[j] = d; order[j] = k;
            }
            for (int i = 0; i < Pool; i++)
            {
                var l = pool[i];
                if (!l) continue;
                if (order[i] < 0) { l.enabled = false; continue; }
                var lamp = lamps[order[i]];
                l.transform.localPosition = lamp.at;
                l.color = lamp.color; l.range = lamp.range; l.intensity = lamp.range * 1.6f;
            }
            Keep();
        }

        void Keep()
        {
            bool dark = DayNight.Darkness > 0.3f;
            for (int i = 0; i < Pool; i++) if (pool[i]) pool[i].enabled = dark && order[i] >= 0 && LightBudget.Allowed(pool[i]);
        }
    }
}
