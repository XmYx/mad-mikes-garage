using System.Collections.Generic;
using MadMax.Items;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Shared rules of the stage I defences: the contained mine blast, vehicle footprints (what stands over a
    /// mine or across a wire), which pieces raiders can't see, what each piece adds to a claim's defence score, and the
    /// toll an off-screen raid takes on the mines. Tallies (mines gone off, people they took, wires pulled) are saved.</summary>
    public static class DefenceWorks
    {
        /// <summary>Mines gone off, people killed by them, tripwires pulled (saved in <c>SaveData.blockDefence</c>).</summary>
        public static int Blasts, Kills, Trips;
        static readonly Dictionary<VehicleDriver, Bounds> footprints = new Dictionary<VehicleDriver, Bounds>();
        static readonly Collider[] hits = new Collider[64];
        static readonly HashSet<Object> done = new HashSet<Object>();
        static readonly List<Landmine> scratch = new List<Landmine>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Blasts = Kills = Trips = 0; footprints.Clear(); done.Clear(); scratch.Clear(); }

        /// <summary>This peer simulates the defences (single player, host, dedicated server).</summary>
        public static bool Authority => !(MadMax.Net.NetSession.Instance && MadMax.Net.NetSession.Instance.IsClient);

        /// <summary>Hidden pieces raiders don't go for (they walk into them instead).</summary>
        public static bool Concealed(Placeable p) => p && (p.GetComponent<Landmine>() || p.GetComponent<Tripwire>());

        /// <summary>What a stage I piece adds to its claim's defence score; -1 when it is not one of them.</summary>
        public static float Score(Placeable p)
        {
            if (!p) return -1f;
            if (p.TryGetComponent<Landmine>(out var m)) return m.Armed ? 0.8f : 0.3f;
            if (p.GetComponent<Tripwire>()) return 0.4f;
            if (p.GetComponent<Watchtower>()) return 1.5f;
            if (p.GetComponent<MotorGate>()) return 1f;
            if (p.GetComponent<GunNest>()) return p.TryGetComponent<AutoTurret>(out var t) && t.on ? 2.5f : 1f;
            switch (p.id)
            {
                case "sandbag_wall": return 0.4f;
                case "gate_frame": return 0.3f;
                case "wall_panel": case "doorway_panel": return 0.7f;
                case "wall_brick_fired": case "wall_brick_fired_window": case "doorway_brick_fired": return 0.4f;
                case "wall_plank": case "wall_plank_window": case "doorway_plank": return 0.15f;
            }
            return -1f;
        }

        /// <summary>Is a point under or inside a vehicle's body footprint (from 1.2 m below the body to its roof)?</summary>
        public static bool Over(VehicleDriver v, Vector3 at, float margin = 0f)
        {
            if (!v) return false;
            if (!footprints.TryGetValue(v, out var b)) { if (footprints.Count > 256) footprints.Clear(); footprints[v] = b = LocalBounds(v); }
            var lp = v.transform.InverseTransformPoint(at);
            return Mathf.Abs(lp.x - b.center.x) < b.extents.x + margin && Mathf.Abs(lp.z - b.center.z) < b.extents.z + margin
                && lp.y > b.min.y - 1.2f && lp.y < b.max.y;
        }

        /// <summary>The body mesh's box in the vehicle's space (a car-sized box when there is none).</summary>
        static Bounds LocalBounds(VehicleDriver v)
        {
            var body = v.transform.Find("Body");
            if (!body || !body.TryGetComponent<MeshFilter>(out var mf) || !mf.sharedMesh) return new Bounds(new Vector3(0f, 0.8f, 0f), new Vector3(1.8f, 1.2f, 4.2f));
            var mb = mf.sharedMesh.bounds;
            var b = new Bounds(v.transform.InverseTransformPoint(body.TransformPoint(mb.center)), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                b.Encapsulate(v.transform.InverseTransformPoint(body.TransformPoint(corner)));
            }
            return b;
        }

        /// <summary>A mine's blast. Sound, flash, smoke, shake and the noise go through <see cref="Explosion.Blast"/>;
        /// the damage is contained here: a shallow crater, hits on everything breakable within 1.6 × radius (weaker with
        /// distance; people and animals, vehicles, built pieces), a capped upward kick to loose bodies (a car's corner
        /// jumps, it is not thrown), the player on foot hurt as by any blast. Authority applies the damage.</summary>
        public static void Blast(Vector3 at, float radius, float power, GameObject source, bool authority)
        {
            Explosion.Blast(at, radius, power, 0f, source, false);
            if (!authority) return;
            var terrain = DeformableTerrain.Instance;
            if (terrain && at.y < terrain.Height(at.x, at.z) + 0.6f)
            {
                terrain.ApplyTerraform((byte)DeformableTerrain.TerraOp.Dig, at, radius * 0.5f, 0.25f, 0);
                MadMax.Net.NetSession.Instance?.SendTerraform((byte)DeformableTerrain.TerraOp.Dig, at, radius * 0.5f, 0.25f, 0);
            }
            float reach = radius * 1.6f;
            done.Clear();
            int n = Physics.OverlapSphereNonAlloc(at, reach, hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i];
                if (!c || (source && c.transform.IsChildOf(source.transform))) continue;
                var dmg = c.GetComponentInParent<IDamageable>();
                var key = dmg as Object;
                if (dmg != null && key && done.Add(key))
                {
                    var p = (c is MeshCollider mc && !mc.convex) || c is CharacterController ? c.bounds.ClosestPoint(at) : c.ClosestPoint(at);
                    float fall = Mathf.Clamp01(1f - Vector3.Distance(p, at) / reach);
                    var dir = (p - at).sqrMagnitude > 1e-4f ? (p - at).normalized : Vector3.up;
                    var npc = key as MadMax.Npc.Npc;
                    bool alive = npc && npc.Alive;
                    if (fall > 0f) dmg.ApplyHit(p, dir, power * fall, radius * (0.4f + 0.6f * fall), source);
                    if (alive && !npc.Alive) Kills++;
                }
                var rb = c.attachedRigidbody;
                if (rb && !rb.isKinematic && done.Add(rb))
                {
                    float fall = Mathf.Clamp01(1f - Vector3.Distance(rb.worldCenterOfMass, at) / (reach + 1f));
                    var away = rb.worldCenterOfMass - at; away.y = 0f;
                    rb.AddForceAtPosition((Vector3.up * 3.5f + away.normalized * 1.2f) * (fall * rb.mass), at, ForceMode.Impulse);
                }
            }
            var game = MadMax.Game.WastelandGame.Instance;
            if (game && game.Vitals && game.Player && !game.Current)
            {
                float d = Vector3.Distance(game.Player.transform.position + Vector3.up, at);
                if (d < reach) game.Vitals.Hurt(power * 9f * (1f - d / reach), "BLAST");
            }
        }

        /// <summary>An off-screen raid walks into the claim's mines: each armed one goes off with a 40 % chance (spent
        /// without a scene); returns how many, noted in the journal.</summary>
        public static int SpendMines(ClaimFlag claim)
        {
            if (!claim) return 0;
            scratch.Clear();
            foreach (var m in Landmine.All) if (m && m.Armed && claim.Inside(m.transform.position)) scratch.Add(m);
            int n = 0;
            foreach (var m in scratch) if (Random.value < 0.4f) { m.Spend(); n++; }
            scratch.Clear();
            if (n > 0) { Blasts += n; MadMax.Game.Journal.Add("BASE", n + (n == 1 ? " MINE" : " MINES") + " WENT OFF UNDER RAIDERS AT YOUR BASE"); }
            return n;
        }
    }
}
