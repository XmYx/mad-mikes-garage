using UnityEngine;

namespace MadMax.World
{
    /// <summary>Makes rivers visibly run: flecks of foam appear on the water around the player and drift downstream at
    /// the current's speed (<see cref="WorldGen.RiverFlow"/>), thicker where it runs fast. Lakes stay still.</summary>
    public class RiverFoam : MonoBehaviour
    {
        const float Radius = 45f;
        float tick;

        void Update()
        {
            var g = MadMax.Game.WastelandGame.Instance;
            var t = DeformableTerrain.Instance;
            if (!g || !g.Player || !t || t.World == null || t.World.rivers.Count == 0 || Application.isBatchMode) return;
            if ((tick -= Time.deltaTime) > 0f) return;
            tick = 0.05f;
            var focus = g.Current ? g.Current.transform.position : g.Player.transform.position;
            for (int i = 0; i < 6; i++)
            {
                var off = Random.insideUnitCircle * Radius;
                float x = focus.x + off.x, z = focus.z + off.y;
                var flow = t.World.RiverFlow(x, z);
                float speed = flow.magnitude;
                if (speed < 0.2f || Random.value > 0.35f + speed * 0.3f) continue;
                float lvl = t.WaterLevel(x, z);
                if (float.IsNaN(lvl) || t.WaterDepthNoLoad(x, z) < 0.1f) continue;
                var v = new Vector3(flow.x, 0f, flow.y);
                Fx.Foam(new Vector3(x, lvl + 0.03f, z), v, Random.Range(0.12f, 0.3f) + speed * 0.05f, new Color(0.86f, 0.92f, 0.95f, 0.75f), 4f + speed);
            }
        }
    }
}
