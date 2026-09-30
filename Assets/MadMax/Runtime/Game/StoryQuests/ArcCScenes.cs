using MadMax.Building;
using MadMax.Story;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Shared scene helpers for arc C (C1-C5): story props and story vehicles at anchors, finding them again after a
    /// load, a vehicle's wear, a trench across a road, a quest's closing line.</summary>
    public partial class WastelandGame
    {
        static float ArcCFlat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>A story prop at an anchor (nothing when the anchor or the build root is missing).</summary>
        Placeable ArcCPut(string anchor, string id, Vector3 local, float turn)
        {
            if (!StoryAnchors.Has(anchor) || !Build || !Build.Structures) return null;
            return PutAt(anchor, id, local, turn);
        }

        /// <summary>The first story prop <paramref name="id"/> within <paramref name="within"/> m of an anchor.</summary>
        Placeable ArcCProp(string id, string anchor, float within)
        {
            if (!StoryAnchors.Has(anchor)) return null;
            var a = StoryAnchors.Get(anchor);
            foreach (var p in Placeable.All) if (p && p.id == id && IsStoryProp(p) && ArcCFlat(p.transform.position, a) < within) return p;
            return null;
        }

        /// <summary>A story vehicle parked at an anchor (local offset in the anchor's frame, <paramref name="turn"/> from its
        /// yaw), registered like any other world vehicle and tagged so quests find it after a load.</summary>
        VehicleDriver ArcCVehicle(string design, string anchor, Vector3 local, float turn, string tag, string name)
        {
            var pf = PrefabFor(design);
            if (!pf || !StoryAnchors.Has(anchor)) return null;
            var rot = Quaternion.Euler(0f, StoryAnchors.Yaw(anchor) + turn, 0f);
            var p = StoryAnchors.Get(anchor) + Quaternion.Euler(0f, StoryAnchors.Yaw(anchor), 0f) * local;
            p.y = terrain.HeightNoLoad(p.x, p.z) + 0.8f;
            var v = Instantiate(pf, p, rot).GetComponent<VehicleDriver>();
            v.name = name;
            Register(v, null);
            StoryTag.Set(v.gameObject, tag);
            v.handbrake = true;
            return v;
        }

        static VehicleDriver ArcCTagged(string tag)
        {
            var t = StoryTag.Find(tag);
            return t ? t.GetComponent<VehicleDriver>() : null;
        }

        /// <summary>Put a loose part into a vehicle's socket (clearing whatever was there).</summary>
        void ArcCMount(VehicleDriver v, string socket, string part)
        {
            if (!v) return;
            var ch = v.GetComponent<VehicleChassis>();
            var s = ch ? ch.FindSocket(socket) : null;
            if (!s) return;
            if (s.Current) { var old = s.Detach(false); if (old) Destroy(old.gameObject); }
            var p = SpawnPart(part, s.transform.position, s.transform.rotation);
            if (p && !s.Attach(p)) Destroy(p.gameObject);
        }

        /// <summary>A vehicle's wear, 0..n: the sum of its mounted parts' damage plus its bent frame.</summary>
        static float ArcCWear(VehicleDriver v)
        {
            if (!v) return 0f;
            float d = 0f;
            foreach (var p in v.GetComponentsInChildren<VehiclePart>()) if (p.Socket) d += p.damage + (p.TryGetComponent<WheelStats>(out var w) ? w.wear * 0.5f : 0f);
            if (v.TryGetComponent<VehicleDamage>(out var dmg)) d += dmg.FrameDamage;
            return d;
        }

        static float ArcCEngineDamage(VehicleDriver v)
        {
            if (!v || !v.Engine) return 1f;
            return v.Engine.TryGetComponent<VehiclePart>(out var e) ? e.damage : 0f;
        }

        static float ArcCFuel(VehicleDriver v) => v && v.TryGetComponent<VehicleSystems>(out var s) && s.fuelCapacity > 0f ? s.fuel / s.fuelCapacity : 0f;

        /// <summary>A washout across a road: digs along the line across the road at <paramref name="centre"/> (heading
        /// <paramref name="yaw"/>), <paramref name="half"/> m to either side, ~<paramref name="depth"/> m deep in the middle.</summary>
        void ArcCTrench(Vector3 centre, float yaw, float half, float depth, float radius)
        {
            if (!terrain) return;
            var across = Quaternion.Euler(0f, yaw + 90f, 0f) * Vector3.forward;
            float per = depth / 2.1f;                                                                   // overlapping digs every 1.5 m sum to ~2.1x
            for (float s = -half; s <= half + 0.01f; s += 1.5f)
            {
                var p = centre + across * s;
                float taper = Mathf.Clamp01((half - Mathf.Abs(s)) / 4f + 0.25f);                        // shallower towards the ends
                terrain.ApplyTerraform((byte)MadMax.World.DeformableTerrain.TerraOp.Dig, new Vector3(p.x, terrain.Height(p.x, p.z), p.z), radius, per * taper, 0);
            }
        }

        /// <summary>Set a quest's closing journal line (read when it completes).</summary>
        static void ArcCPayoff(string id, string text)
        {
            var q = StoryLibrary.Get(id);
            if (q != null && text != null) q.payoff = text;
        }

        /// <summary>The current step id of an active quest (null otherwise).</summary>
        static string ArcCStep(string id)
        {
            var q = StoryLibrary.Get(id);
            var s = q != null ? Story.Story.Current(q) : null;
            return s != null ? s.id : null;
        }

        /// <summary>Change what a cast member answers to a quest topic (dialogue that reflects earlier choices).</summary>
        static void ArcCReply(string quest, string step, string topic, string reply)
        {
            var q = StoryLibrary.Get(quest);
            if (q == null) return;
            foreach (var s in q.steps) if (s.id == step) foreach (var c in s.any) if (c.topic == topic) c.reply = reply;
        }
    }
}
