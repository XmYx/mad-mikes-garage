using System.Collections.Generic;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Town plans in the game: wrecked cars at the kerbs of side streets, in car parks and on empty lots
    /// (a share of each plan's wreck spots: village 2, town 6, city 12), and the paper litter blowing down the streets.</summary>
    public partial class WastelandGame
    {
        void PlanTownWrecks(List<GameObject> prefabs, List<int> cars)
        {
            if (cars.Count == 0) return;
            var rnd = new System.Random(seed * 53 + 11);
            foreach (var st in World.settlements)
            {
                if (st.plan == null) continue;
                int cap = st.kind == Biome.Village ? 2 : st.kind == Biome.Town ? 6 : 12, n = 0;
                foreach (var sp in st.plan.spots)
                {
                    if (sp.kind != SpotKind.Wreck || n >= cap) continue;
                    if (rnd.NextDouble() > 0.45) continue;
                    if (World.YardWeight(sp.pos.x, sp.pos.y) > 0f || World.Reserved(sp.pos.x, sp.pos.y)) continue;
                    float a = sp.yaw * Mathf.Deg2Rad;
                    wreckPlans.Add(new WreckPlan { prefab = cars[rnd.Next(cars.Count)], pos = new Vector3(sp.pos.x, 0f, sp.pos.y), dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)), seed = rnd.Next(), cold = false });
                    n++;
                }
            }
        }
    }
}
