using MadMax.Building;
using MadMax.Story;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    // Shared scene helpers of the S19-S22 and L1-L5 quest hooks (the Q3 prefix keeps them apart from other quests').
    public partial class WastelandGame
    {
        /// <summary>A campaign prop at a world position (on the ground unless <paramref name="y"/> is given).</summary>
        Placeable Q3Put(string id, Vector3 at, float yawDeg, float y = float.NaN)
        {
            if (!Build || !Build.Structures) return null;
            at.y = float.IsNaN(y) ? terrain.HeightNoLoad(at.x, at.z) : y;
            var pl = FurnitureLibrary.Spawn(id, Build.Structures, at, Quaternion.Euler(0f, yawDeg, 0f), propMaterial);
            if (pl) storyProps.Add(pl.Id);
            return pl;
        }

        /// <summary>The campaign prop <paramref name="id"/> nearest <paramref name="near"/> within <paramref name="r"/> m.</summary>
        Placeable Q3Prop(string id, Vector3 near, float r)
        {
            Placeable best = null; float bd = r * r;
            foreach (var p in Placeable.All)
            {
                if (!p || p.id != id || !IsStoryProp(p)) continue;
                var d = p.transform.position - near; d.y = 0f;
                if (d.sqrMagnitude <= bd) { bd = d.sqrMagnitude; best = p; }
            }
            return best;
        }

        /// <summary>A story vehicle (not in the fleet; anyone may drive it) tagged <paramref name="tag"/>.</summary>
        VehicleDriver Q3Vehicle(string design, Vector3 at, float yawDeg, string tag, float lift = 0.7f)
        {
            var pf = PrefabFor(design);
            if (!pf) return null;
            at.y = terrain.HeightNoLoad(at.x, at.z) + lift;
            var v = Instantiate(pf, at, Quaternion.Euler(0f, yawDeg, 0f)).GetComponent<VehicleDriver>();
            if (!v) return null;
            Register(v, null);
            if (tag != null) StoryTag.Set(v.gameObject, tag);
            return v;
        }

        static float Q3Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>The quest's closing journal line, written from the choices made (set before the last step).</summary>
        static void Q3Payoff(string quest, string text) { var q = StoryLibrary.Get(quest); if (q != null) q.payoff = text; }

        static bool Q3Route(string quest, string step, string prefix) { var r = MadMax.Story.Story.Route(quest, step); return r != null && r.StartsWith(prefix); }
    }
}
