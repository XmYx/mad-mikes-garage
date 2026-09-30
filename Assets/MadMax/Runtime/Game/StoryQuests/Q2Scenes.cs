using MadMax.Building;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Scene helpers shared by S11, S13, S16, S17 and S18: story props in an anchor's frame (+Z faces the road
    /// for roadside places) at an explicit height, and small box houses built from ordinary pieces.</summary>
    public partial class WastelandGame
    {
        /// <summary>A story prop in <paramref name="anchor"/>'s frame. <paramref name="baseY"/> = the height it stands on
        /// (null: the ground under it); local.y lifts it from there; <paramref name="tilt"/> rolls or pitches it after the turn.</summary>
        Placeable Q2Put(string anchor, string id, Vector3 local, float turn, float? baseY = null, Vector3? tilt = null)
        {
            if (!Build || !Build.Structures || !StoryAnchors.Has(anchor)) return null;
            var a = StoryAnchors.Get(anchor); var r = Quaternion.Euler(0f, StoryAnchors.Yaw(anchor), 0f);
            var p = a + r * new Vector3(local.x, 0f, local.z);
            p.y = (baseY ?? terrain.HeightNoLoad(p.x, p.z)) + local.y;
            var pl = FurnitureLibrary.Spawn(id, Build.Structures, p, r * Quaternion.Euler(0f, turn, 0f) * Quaternion.Euler(tilt ?? Vector3.zero), propMaterial);
            if (pl) storyProps.Add(pl.Id);
            return pl;
        }

        static float Q2Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>Hold a cast member at a spot (a roof, a stall): moved there, home set there, standing by day and
        /// night (no walk to a campfire in the evening, no bed indoors).</summary>
        void Q2Pin(MadMax.Npc.Npc n, Vector3 spot, float face)
        {
            if (!n) return;
            if (n.Profile.role != MadMax.Npc.NpcRole.Shopkeeper) n.Profile.role = MadMax.Npc.NpcRole.Shopkeeper;   // not a vendor (no stock kind): only the routine
            n.homeRadius = 0.3f; n.homeYaw = face;
            if ((n.transform.position - spot).sqrMagnitude < 0.64f) return;
            var cc = n.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            n.transform.position = spot;
            if (cc) cc.enabled = true;
            n.home = spot;
        }

        /// <summary>The highest ground under a rectangle of an anchor's frame.</summary>
        float Q2Ground(string anchor, Vector3 c, float halfX, float halfZ)
        {
            var a = StoryAnchors.Get(anchor); var r = Quaternion.Euler(0f, StoryAnchors.Yaw(anchor), 0f);
            float top = float.MinValue;
            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                {
                    var p = a + r * new Vector3(c.x + i * halfX, 0f, c.z + j * halfZ);
                    top = Mathf.Max(top, terrain.HeightNoLoad(p.x, p.z));
                }
            return top;
        }

        /// <summary>A small house of story props: <paramref name="w"/> × <paramref name="d"/> wall pieces (2 m each)
        /// centred on <paramref name="c"/> in the anchor's frame, a doorway at the front-left (+Z), a window at the
        /// front-right, timber foundations level with the highest ground under it (and a metre round), optionally a flat roof. Returns the
        /// height the walls stand on (a flat roof's top is 2.46 m above it).</summary>
        float Q2House(string anchor, Vector3 c, int w, int d, string wall, string door, string window, bool roof)
        {
            float floor = Q2Ground(anchor, c, w + 1f, d + 1f) + 0.02f;                             // a metre past the walls: no ledge low enough to mantle
            float y = floor + 0.12f;                                                                  // on the foundation deck
            for (int i = 0; i < w; i++)
                for (int j = 0; j < d; j++)
                {
                    var cell = new Vector3(c.x - w + 1 + 2 * i, 0f, c.z - d + 1 + 2 * j);
                    Q2Put(anchor, "foundation_wood", cell, 0f, floor);
                    if (roof) Q2Put(anchor, "roof_flat", cell + Vector3.up * 2.42f, 0f, y);
                }
            for (int i = 0; i < w; i++)
            {
                float x = c.x - w + 1 + 2 * i;
                Q2Put(anchor, i == 0 ? door : i == w - 1 && window != null ? window : wall, new Vector3(x, 0f, c.z + d), 0f, y);
                Q2Put(anchor, wall, new Vector3(x, 0f, c.z - d), 180f, y);
            }
            for (int j = 0; j < d; j++)
            {
                float z = c.z - d + 1 + 2 * j;
                Q2Put(anchor, wall, new Vector3(c.x - w, 0f, z), -90f, y);
                Q2Put(anchor, wall, new Vector3(c.x + w, 0f, z), 90f, y);
            }
            return y;
        }
    }
}
