using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Shows the furnishings of a generated city's storeys only near the camera's target (they are only seen
    /// through windows and cutaways up close): renderers further than <see cref="reach"/> are switched off.</summary>
    public class CityInteriors : MonoBehaviour
    {
        public float reach = 55f;
        public readonly List<Renderer> renderers = new List<Renderer>();
        float scanAt;
        int next;

        void Update()
        {
            if (renderers.Count == 0) return;
            var focus = Camera.main ? Camera.main.transform.position : Vector3.zero;
            var game = MadMax.Game.WastelandGame.Instance;
            if (game && game.Player) focus = game.Player.transform.position;
            // a slice per frame so a city's hundreds of storeys never cost a spike
            int n = Mathf.Max(8, renderers.Count / 20);
            for (int i = 0; i < n; i++)
            {
                if (next >= renderers.Count) next = 0;
                var r = renderers[next++];
                if (!r) continue;
                bool near = r.bounds.SqrDistance(focus) < reach * reach;
                if (r.enabled != near) r.enabled = near;
            }
        }
    }
}
