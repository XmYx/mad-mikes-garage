using MadMax.Building;
using MadMax.Story;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Scene helpers shared by the A5, A6, B3, B4 and B5 hooks (the Q7 prefix keeps them apart from other
    /// quests' helpers): find a story prop again after a reload, park a story vehicle, pose a cast body exactly, take a
    /// prop away, and the post-quest life of the refuge (<see cref="StoryAftermath"/> calls <see cref="Q7Aftermath"/>).</summary>
    public partial class WastelandGame
    {
        /// <summary>The story prop <paramref name="id"/> nearest the point <paramref name="local"/> of an anchor (within
        /// <paramref name="radius"/> m), or null.</summary>
        internal Placeable Q7Prop(string anchor, string id, Vector3 local, float radius = 3f)
        {
            if (!StoryAnchors.Has(anchor)) return null;
            var p = StoryAnchors.Get(anchor) + Quaternion.Euler(0f, StoryAnchors.Yaw(anchor), 0f) * local;
            Placeable best = null; float bd = radius * radius;
            foreach (var pl in Placeable.All)
            {
                if (!pl || pl.id != id || !IsStoryProp(pl)) continue;
                var d = pl.transform.position - p; d.y = 0f;
                if (d.sqrMagnitude < bd) { bd = d.sqrMagnitude; best = pl; }
            }
            return best;
        }

        /// <summary>Take a story prop away (the scene is over).</summary>
        internal void Q7Drop(Placeable p)
        {
            if (!p) return;
            storyProps.Remove(p.Id);
            Destroy(p.gameObject);
        }

        /// <summary>A story vehicle (not the player's) parked at an anchor's local point, tagged <paramref name="tag"/>.</summary>
        internal VehicleDriver Q7Vehicle(string design, string anchor, Vector3 local, float turn, string tag, int paint = -1)
        {
            var pf = PrefabFor(design);
            if (!pf || !StoryAnchors.Has(anchor)) return null;
            var rot = Quaternion.Euler(0f, StoryAnchors.Yaw(anchor) + turn, 0f);
            var p = StoryAnchors.Get(anchor) + Quaternion.Euler(0f, StoryAnchors.Yaw(anchor), 0f) * local;
            p.y = terrain.HeightNoLoad(p.x, p.z) + 0.9f;
            var v = Instantiate(pf, p, rot).GetComponent<VehicleDriver>();
            v.name = design;
            Register(v, null);
            if (tag != null) StoryTag.Set(v.gameObject, tag);
            if (paint >= 0)
            {
                var vp = v.GetComponent<VehiclePaint>(); if (!vp) vp = v.gameObject.AddComponent<VehiclePaint>();
                vp.colour = paint; vp.Apply();
            }
            return v;
        }

        /// <summary>Park a (story) vehicle somewhere else at once, resting.</summary>
        internal void Q7Move(VehicleDriver v, Vector3 at, float yaw)
        {
            if (!v) return;
            at.y = terrain.Height(at.x, at.z) + 0.9f;
            v.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            if (v.Body)
            {
                v.Body.position = at; v.Body.rotation = Quaternion.Euler(0f, yaw, 0f);
                v.Body.linearVelocity = Vector3.zero; v.Body.angularVelocity = Vector3.zero;
                v.Body.WakeUp();
            }
        }

        readonly System.Collections.Generic.HashSet<MadMax.Npc.Npc> q7Posed = new System.Collections.Generic.HashSet<MadMax.Npc.Npc>();

        /// <summary>Stand a freshly spawned cast body exactly at <paramref name="at"/> (once per body): inside a hut or at
        /// a trailer door rather than wherever the shared spawn offsets put it.</summary>
        internal MadMax.Npc.Npc Q7Pose(string key, Vector3 at, float yaw)
        {
            var n = CastBody(key);
            if (!n || !q7Posed.Add(n)) return n;
            q7Posed.RemoveWhere(x => !x);
            at.y = terrain.Height(at.x, at.z) + 0.05f;
            var cc = n.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            n.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            n.home = at; n.homeYaw = yaw;
            if (cc) cc.enabled = true;
            return n;
        }

        /// <summary>A point laid out from an anchor (<paramref name="x"/> right, <paramref name="z"/> to its front).</summary>
        internal static Vector3 Q7At(string anchor, float x, float z) =>
            StoryAnchors.Get(anchor) + Quaternion.Euler(0f, StoryAnchors.Yaw(anchor), 0f) * new Vector3(x, 0f, z);

        /// <summary>The player (or the vehicle they drive) is within <paramref name="r"/> m of an anchor.</summary>
        internal bool Q7Near(string anchor, float r)
        {
            if (!StoryAnchors.Has(anchor)) return false;
            var a = StoryAnchors.Get(anchor); var me = FocusPos;
            return new Vector2(me.x - a.x, me.z - a.z).sqrMagnitude < r * r;
        }

        /// <summary>A built piece (not a story prop) of one of <paramref name="ids"/> ("a|b") within <paramref name="r"/> m of an anchor.</summary>
        internal int Q7Built(string anchor, string ids, float r)
        {
            if (!StoryAnchors.Has(anchor)) return 0;
            var a = StoryAnchors.Get(anchor); var list = ids.Split('|'); int n = 0;
            foreach (var p in Placeable.All)
            {
                if (!p || IsStoryProp(p) || System.Array.IndexOf(list, p.id) < 0) continue;
                var d = p.transform.position - a; d.y = 0f;
                if (d.sqrMagnitude < r * r) n++;
            }
            return n;
        }

        /// <summary>Everything the refuge does once its quests are done (called every frame by <see cref="StoryAftermath"/>).</summary>
        internal void Q7Aftermath()
        {
            if (!played || !Player) return;
            Residents.Tick(this);
            A5Aftermath();
            A6Aftermath();
            B5Aftermath();
        }
    }
}
