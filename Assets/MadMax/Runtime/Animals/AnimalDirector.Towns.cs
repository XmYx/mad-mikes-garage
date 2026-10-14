using System.Collections.Generic;
using MadMax.World;
using UnityEngine;

namespace MadMax.Animals
{
    /// <summary>Town wildlife from the town plans (<see cref="TownPlan"/> spots): crow flocks pecking about the
    /// squares, parks and car parks, a giant rat or two by the alley trash, a pair of strays at a city's back lots.
    /// Spawned when the player comes within 150 m of a town, folded away past 220 m.</summary>
    public partial class AnimalDirector
    {
        readonly Dictionary<int, List<Animal>> townLife = new Dictionary<int, List<Animal>>();

        void UpdateTownLife(Vector3 focus)
        {
            var w = game.World;
            var t = DeformableTerrain.Instance;
            for (int i = 0; i < w.settlements.Count; i++)
            {
                var st = w.settlements[i];
                if (st.plan == null) continue;
                float d = Vector2.Distance(st.pos, new Vector2(focus.x, focus.z)) - st.radius;
                bool has = townLife.TryGetValue(i, out var list);
                if (!has && d < 150f)
                {
                    list = new List<Animal>();
                    var rnd = new System.Random(game.seed ^ (i * 4513) ^ 0x7a11);
                    int flocks = 0, rats = 0, dogs = 0, k = 0;
                    foreach (var sp in st.plan.spots)
                    {
                        string species = null; int n = 0;
                        if (sp.kind == SpotKind.Birds && flocks < 4) { species = "crow"; n = 4 + rnd.Next(4); flocks++; }
                        else if (sp.kind == SpotKind.Rats && rats < 2 && rnd.NextDouble() < 0.6) { species = "rat"; n = 1 + rnd.Next(2); rats++; }
                        else if (sp.kind == SpotKind.Dogs && dogs < 1) { species = "dog"; n = 2; dogs++; }
                        var def = species != null ? AnimalLibrary.Get(species) : null;
                        if (def == null) continue;
                        for (int j = 0; j < n; j++)
                        {
                            float r = 0.6f + (float)rnd.NextDouble() * 2.5f, a = (float)rnd.NextDouble() * 6.283f;
                            var p = new Vector3(sp.pos.x + Mathf.Cos(a) * r, 0f, sp.pos.y + Mathf.Sin(a) * r);
                            if (t) p.y = t.HeightNoLoad(p.x, p.z);
                            var an = Animal.Spawn(def, p, (float)rnd.NextDouble() * 360f, game.propMaterial, "t:" + i + ":" + k + ":" + j);
                            list.Add(an);
                        }
                        k++;
                    }
                    townLife[i] = list;
                }
                else if (has && d > 220f)
                {
                    foreach (var a in list) if (a && a.Alive && !a.owned) Destroy(a.gameObject);
                    townLife.Remove(i);
                }
            }
        }

        /// <summary>Town animals alive near a point (tests).</summary>
        public int TownAnimals(Vector3 at, float radius)
        {
            int n = 0;
            foreach (var kv in townLife) foreach (var a in kv.Value) if (a && a.Alive && (a.transform.position - at).sqrMagnitude < radius * radius) n++;
            return n;
        }
    }
}
