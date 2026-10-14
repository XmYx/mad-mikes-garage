using System.Collections.Generic;
using MadMax.Items;
using MadMax.Rendering;
using MadMax.Voxel;
using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Shared bits of the heavy lifters (<see cref="MagnetCrane"/>, <see cref="GrappleCrusher"/>): what a crane may
    /// pick up, runtime meshes (magnet disc, hydraulic rams, crushed bales), hydraulic ram visuals and outriggers.</summary>
    public static class LifterKit
    {
        const float S = VoxelMesher.DefaultSize;
        /// <summary>Quality value that marks a resource <see cref="WorldItem"/> as a crushed bale (resources have no make:
        /// the pack ignores it; the save keeps it, so a reload restores the bale look).</summary>
        public const float BaleMark = 7f;

        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => meshes.Clear();

        static bool Cached(string key, out Mesh m) => meshes.TryGetValue(key, out m) && m;

        // ------------------------------------------------------------------ what lifts
        /// <summary>People and animals are never lifted (ragdoll bones, carcasses included).</summary>
        public static bool Living(Rigidbody r) =>
            r.GetComponentInParent<MadMax.Npc.Npc>() || r.GetComponentInParent<MadMax.Animals.Animal>() || r.GetComponentInParent<MadMax.Animals.Carcass>()
            || r.GetComponentInParent<MadMax.Game.PlayerCharacter>() || r.GetComponentInParent<MadMax.Game.HumanRig>();

        /// <summary>A body a grapple may take: anything loose with a rigidbody that is not alive, not this truck, not a car
        /// someone is driving.</summary>
        public static bool Liftable(Rigidbody r, Transform self)
        {
            if (!r || r.transform.IsChildOf(self) || Living(r)) return false;
            if (r.TryGetComponent<VehicleDriver>(out var v) && (v.Occupied || v.aiDriven)) return false;
            return true;
        }

        /// <summary>Iron and steel the lifting magnet holds: vehicles and wrecks, loose parts, scrap / iron / steel stacks.
        /// Wood, glass, cloth, people and animals stay put.</summary>
        public static bool Ferrous(Rigidbody r, Transform self)
        {
            if (!Liftable(r, self)) return false;
            if (r.GetComponent<VehicleDriver>()) return true;
            if (r.TryGetComponent<VehiclePart>(out var p)) return !p.IsMounted;
            if (r.TryGetComponent<WorldItem>(out var w))
                return w.IsResource && (w.Resource == ResourceType.Scrap || w.Resource == ResourceType.Iron || w.Resource == ResourceType.Steel || w.Resource == ResourceType.IronOre)
                       || w.key.StartsWith("tool_") || w.key.StartsWith("ammo_");
            return false;
        }

        /// <summary>Closest point of a collider (non-convex mesh colliders: of their bounds).</summary>
        public static Vector3 Closest(Collider c, Vector3 p) =>
            c is MeshCollider mc && !mc.convex ? c.bounds.ClosestPoint(p) : c.ClosestPoint(p);

        /// <summary>Ignore (or restore) contacts between a load and the crane's own segments.</summary>
        public static void IgnoreLoad(Rigidbody load, Collider[] crane, bool ignore)
        {
            if (!load || crane == null) return;
            foreach (var lc in load.GetComponentsInChildren<Collider>())
                foreach (var cc in crane)
                    if (lc && cc) Physics.IgnoreCollision(lc, cc, ignore);
        }

        /// <summary>Rope line used by both cranes.</summary>
        public static LineRenderer Line(Transform parent, string name, float width)
        {
            var l = new GameObject(name).AddComponent<LineRenderer>();
            l.transform.SetParent(parent, false);
            l.sharedMaterial = Fx.TransparentMaterial(null);
            l.widthMultiplier = width; l.positionCount = 2;
            l.startColor = l.endColor = new Color(0.08f, 0.08f, 0.09f, 1f);
            l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return l;
        }

        /// <summary>The material the vehicle body draws with (runtime pieces match it).</summary>
        public static Material BodyMaterial(Transform vehicle)
        {
            if (HDBits.On) return HDShapes.Solid;
            var b = vehicle.Find("Body");
            return b && b.TryGetComponent<MeshRenderer>(out var r) ? r.sharedMaterial : null;
        }

        public static MeshRenderer Visual(string name, Transform parent, Mesh mesh, Material mat)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            return r;
        }

        // ------------------------------------------------------------------ meshes
        /// <summary>Lifting magnet: a 1.45 m pole disc (concentric pole ring underneath, riveted rim with hazard band,
        /// six radial ribs, terminal box) on a three-leg chain bridle. Origin = the shackle; the pole face is
        /// <see cref="MagnetCrane.DiscDepth"/> below it.</summary>
        public static Mesh MagnetDisc()
        {
            if (Cached("magnet", out var m)) return m;
            if (HDBits.On)
            {
                var b = new HDShapes();
                b.Tube(new Vector3(0f, -12f, 0f) * S, new Vector3(0f, -8f, 0f) * S, 9f * S, 9f * S, Pal.Metal[1], 16, true);
                b.Tube(new Vector3(0f, -8f, 0f) * S, new Vector3(0f, -7f, 0f) * S, 8.6f * S, 8.6f * S, Pal.Amber, 16, true);
                b.Disc(new Vector3(0f, -12.05f, 0f) * S, Vector3.down, 6f * S, Pal.Chrome[1], 16);
                b.Box(new Vector3(0f, -5.5f, 0f) * S, new Vector3(4f, 3f, 4f) * S, Pal.Black[1], 0.3f * S);
                for (int i = 0; i < 3; i++)
                {
                    float a = i * Mathf.PI * 2f / 3f;
                    var foot = new Vector3(Mathf.Cos(a) * 6f, -7f, Mathf.Sin(a) * 6f) * S;
                    b.Tube(foot, Vector3.zero, 0.35f * S, 0.35f * S, Pal.Metal[3], 5);
                }
                return meshes["magnet"] = b.ToMesh("MagnetDiscHD");
            }
            var g = new VoxelGrid().Mat((byte)ResourceType.Iron);
            var housing = Pal.Weathered(Pal.Metal, 0.35f, 7301, 2, 0);
            g.CylY(0, 0, 9f, -12, -8, housing);
            g.CylY(0, 0, 9f, -12, -12, Pal.Ramp(Pal.Metal, 0, 7302));                                   // pole face
            g.CylY(0, 0, 6.2f, -12, -12, Pal.Solid(Pal.Chrome[1]), 5.2f);                                // inner pole ring
            g.CylY(0, 0, 9f, -10, -9, Pal.Stripe(Pal.Ramp(Pal.Black, 1, 7303), Pal.Solid(Pal.Amber), 0, 4, 2), 8f);   // rim band
            for (int i = 0; i < 24; i++)                                                                   // rim rivets
            {
                float a = i * Mathf.PI / 12f;
                g.Set(Mathf.RoundToInt(Mathf.Cos(a) * 8.6f), -11, Mathf.RoundToInt(Mathf.Sin(a) * 8.6f), Pal.Solid(Pal.Chrome[2]));
            }
            for (int i = 0; i < 6; i++)                                                                    // radial ribs
            {
                float a = i * Mathf.PI / 3f;
                g.Tube(new Vector3(Mathf.Cos(a) * 2.5f, -7f, Mathf.Sin(a) * 2.5f), new Vector3(Mathf.Cos(a) * 8f, -7f, Mathf.Sin(a) * 8f), 0.6f, Pal.Ramp(Pal.Ochre, 2, 7304));
            }
            g.Box(-2, -7, -2, 2, -4, 2, Pal.Ramp(Pal.Black, 1, 7305));                                       // terminal box
            g.Box(-1, -4, 2, 1, -4, 3, Pal.Ramp(Pal.Black, 2, 7306));                                        // cable gland
            for (int i = 0; i < 3; i++)                                                                    // chain bridle
            {
                float a = i * Mathf.PI * 2f / 3f + 0.5f;
                var foot = new Vector3(Mathf.Cos(a) * 6f, -7f, Mathf.Sin(a) * 6f);
                g.Box(Mathf.RoundToInt(foot.x), -8, Mathf.RoundToInt(foot.z), Mathf.RoundToInt(foot.x), -7, Mathf.RoundToInt(foot.z), Pal.Solid(Pal.Metal[3]));
                for (int k = 0; k <= 10; k++)
                {
                    var p = Vector3.Lerp(foot, new Vector3(0f, -1f, 0f), k / 10f);
                    g.Set(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z), Pal.Solid(k % 2 == 0 ? Pal.Metal[2] : Pal.Metal[0]));
                }
            }
            g.Box(0, -1, -1, 0, 1, 1, Pal.Solid(Pal.Chrome[2]));                                            // shackle
            g.Bevel();
            return meshes["magnet"] = VoxelMesher.Build(g, "MagnetDisc");
        }

        /// <summary>Little lamp on the magnet's terminal box (shown while it is energised).</summary>
        public static Mesh MagnetLamp()
        {
            if (Cached("magnet_lamp", out var m)) return m;
            var g = new VoxelGrid();
            g.Box(0, -3, 0, 0, -3, 0, Pal.Solid(Pal.LightY));
            return meshes["magnet_lamp"] = VoxelMesher.Build(g, "MagnetLamp");
        }

        /// <summary>Hydraulic ram halves along +Z from the origin: barrel (painted, with a gland ring) or chrome rod
        /// with its clevis eye. <paramref name="length"/> in voxels.</summary>
        public static Mesh RamPiece(bool rod, int length, float radius)
        {
            string key = (rod ? "rod" : "barrel") + length + "_" + radius;
            if (Cached(key, out var m)) return m;
            if (HDBits.On)
            {
                var b = new HDShapes();
                if (rod) b.Tube(Vector3.zero, new Vector3(0f, 0f, length) * S, radius * 0.55f * S, radius * 0.55f * S, Pal.Chrome[3], 8, true);
                else
                {
                    b.Tube(Vector3.zero, new Vector3(0f, 0f, length) * S, radius * S, radius * S, Pal.Ochre[3], 8, true);
                    b.Tube(new Vector3(0f, 0f, length - 1f) * S, new Vector3(0f, 0f, length) * S, radius * 1.15f * S, radius * 1.15f * S, Pal.Metal[2], 8, true);
                }
                return meshes[key] = b.ToMesh("RamHD_" + key);
            }
            var g = new VoxelGrid().Mat((byte)ResourceType.Iron);
            if (rod)
            {
                g.CylZ(0, 0, radius * 0.55f, 1, length, Pal.Ramp(Pal.Chrome, 2, 7311));
                g.CylX(0, 0, 1.2f, -1, 1, Pal.Solid(Pal.Metal[2]));                                         // eye
            }
            else
            {
                g.CylZ(0, 0, radius, 1, length, Pal.Weathered(Pal.Ochre, 0.3f, 7312, 3, 0));
                g.CylZ(0, 0, radius + 0.4f, length - 1, length, Pal.Solid(Pal.Metal[2]));                   // gland
                g.CylZ(0, 0, radius + 0.4f, 1, 2, Pal.Solid(Pal.Metal[1]));                                // base cap
                g.CylX(0, 0, 1.2f, -1, 1, Pal.Solid(Pal.Metal[2]));                                         // eye
                g.Set(0, Mathf.CeilToInt(radius), length / 2, Pal.Solid(Pal.Black[2]));                      // hose port
            }
            g.Bevel();
            return meshes[key] = VoxelMesher.Build(g, "Ram_" + key);
        }

        /// <summary>Generic crushed bale (restored saves, or when the car's mesh cannot be read): a flattened block of
        /// layered sheet, rust, paint scraps and a squashed tyre.</summary>
        public static Mesh GenericBale()
        {
            if (Cached("bale", out var m)) return m;
            if (HDBits.On)
            {
                var b = new HDShapes();
                b.Box(new Vector3(0f, 2.5f, 0f) * S, new Vector3(20f, 5f, 42f) * S, Pal.Rust[2], 0.6f * S);
                b.Box(new Vector3(1f, 5.4f, -4f) * S, new Vector3(16f, 1.2f, 30f) * S, Pal.Navy[2], 0.3f * S);
                b.Box(new Vector3(-9f, 2f, 12f) * S, new Vector3(4f, 4f, 9f) * S, Pal.Tire[1], 0.6f * S);
                return meshes["bale"] = b.ToMesh("BaleHD");
            }
            var g = new VoxelGrid().Mat((byte)ResourceType.Scrap);
            Color32[][] ramps = { Pal.Rust, Pal.Metal, Pal.Navy, Pal.Crimson, Pal.Chrome, Pal.Moss };
            for (int x = -10; x <= 10; x++)
            for (int y = 0; y <= 5; y++)
            for (int z = -21; z <= 21; z++)
            {
                float h = Pal.Hash(x, y, z, 7321);
                if ((Mathf.Abs(x) == 10 || Mathf.Abs(z) == 21 || y == 5) && h < 0.18f) continue;                // ragged edges
                int layer = Mathf.FloorToInt(Pal.Hash(0, y, z / 6, 7322) * ramps.Length);
                var ramp = h < 0.3f ? Pal.Rust : ramps[layer];
                g.Set(x, y, z, Pal.Solid(Pal.Pick(ramp, new Vector3Int(x, y, z), 7323, 1)));
            }
            g.CylX(2, 12, 2.6f, -10, -7, Pal.Ramp(Pal.Tire, 1, 7324));                                        // squashed tyre
            g.Box(4, 6, -8, 8, 6, -6, Pal.Ramp(Pal.Glass, 2, 7325));                                         // a shard of windscreen
            g.Bevel();
            return meshes["bale"] = VoxelMesher.Build(g, "Bale");
        }

        /// <summary>A crushed bale made from the car's own meshes (body + mounted parts drawn with the body material),
        /// flattened to <paramref name="squash"/> of its height and crumpled. Cached per vehicle type.</summary>
        public static Mesh BaleFrom(VehicleDriver car, float squash, out Material mat)
        {
            var chassis = car.GetComponent<VehicleChassis>();
            string key = "bale:" + (chassis ? chassis.vehicleName : car.name);
            var body = car.transform.Find("Body");
            var bodyR = body ? body.GetComponent<MeshRenderer>() : null;
            mat = bodyR ? bodyR.sharedMaterial : BodyMaterial(car.transform);
            if (Cached(key, out var cachedMesh)) return cachedMesh;
            if (!bodyR) { mat = BodyMaterial(car.transform); return GenericBale(); }
            var list = new List<CombineInstance>();
            var toRoot = car.transform.worldToLocalMatrix;
            foreach (var mf in car.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.sharedMesh || !mf.sharedMesh.isReadable || mf.name == "Driver" || VehicleDashboard.IsDash(mf) || mf.name == "Glass") continue;
                if (!mf.TryGetComponent<MeshRenderer>(out var r) || !r.enabled || r.sharedMaterial != bodyR.sharedMaterial) continue;
                if (HDModel.IsLod(mf.transform) || mf.GetComponentInParent<MadMax.Building.Placeable>()) continue;
                for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
                    list.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = s, transform = toRoot * mf.transform.localToWorldMatrix });
            }
            if (list.Count == 0) { mat = BodyMaterial(car.transform); return GenericBale(); }
            var mesh = new Mesh { name = "Bale_" + key, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(list.ToArray(), true, true);
            var v = new List<Vector3>(mesh.vertexCount);
            mesh.GetVertices(v);
            var bounds = mesh.bounds;
            float minY = bounds.min.y, h = Mathf.Max(0.3f, bounds.size.y);
            for (int i = 0; i < v.Count; i++)
            {
                var p = v[i];
                var q = new Vector3Int(Mathf.RoundToInt(p.x * 12f), Mathf.RoundToInt(p.y * 12f), Mathf.RoundToInt(p.z * 12f));
                float t = (p.y - minY) / h;                                                                // 0 floor .. 1 roof
                float bulge = 1f + 0.07f * Mathf.Sin(t * Mathf.PI);                                         // the sides belly out
                p.x = p.x * bulge * 0.97f + (Pal.Hash(q, 7331) - 0.5f) * 0.07f;
                p.z = p.z * 0.96f + (Pal.Hash(q, 7332) - 0.5f) * 0.05f;
                p.y = minY + (p.y - minY) * squash + (Pal.Hash(q, 7333) - 0.5f) * 0.05f * squash * 4f;
                v[i] = p;
            }
            mesh.SetVertices(v);
            mesh.RecalculateBounds();
            return meshes[key] = mesh;
        }

        /// <summary>A crushed bale: one resource stack of scrap (count = the car's metal) drawn as the flattened car.</summary>
        public static WorldItem SpawnBale(int scrap, Mesh visual, Material mat, Vector3 at, Quaternion rot)
        {
            var w = WorldItem.Create("res:" + (int)ResourceType.Scrap, Mathf.Max(1, scrap), BaleMark, mat, at, rot, null, visual);
            Weigh(w);
            return w;
        }

        /// <summary>A bale weighs like one (the item default keeps loose stacks light).</summary>
        static void Weigh(WorldItem w)
        {
            if (w.Body) { w.Body.mass = Mathf.Clamp(w.count * 18f, 120f, 1400f); w.Body.maxDepenetrationVelocity = 1f; }
            w.gameObject.name = "Crushed bale (" + w.count + " scrap)";
        }

        /// <summary>Save restore hook: a bale comes back with the bale look and weight.</summary>
        public static void RestoreBale(WorldItem w)
        {
            if (!w || !w.IsResource || w.quality < BaleMark - 0.5f) return;
            var vis = w.transform.Find("Visual");
            if (vis) Object.Destroy(vis.gameObject);
            var mesh = GenericBale();
            var mat = HDBits.On ? HDShapes.Solid : MadMax.Game.WastelandGame.Instance ? MadMax.Game.WastelandGame.Instance.propMaterial : null;
            var r = Visual("Visual", w.transform, mesh, mat);
            var mb = mesh.bounds;
            r.transform.localPosition = new Vector3(-mb.center.x, -mb.min.y, -mb.center.z);
            if (w.Box) { w.Box.center = new Vector3(0f, mb.size.y * 0.5f, 0f); w.Box.size = mb.size; }
            Weigh(w);
        }
    }

    /// <summary>A hydraulic ram drawn between two moving points: the barrel stays on its anchor, the chrome rod on the
    /// other end slides in and out of it. Visual only (no collider).</summary>
    public class HydraulicRam
    {
        readonly Transform barrel, rod;

        public HydraulicRam(Transform parent, Material mat, int length, float radius, string name)
        {
            barrel = LifterKit.Visual(name + "Barrel", parent, LifterKit.RamPiece(false, length, radius), mat).transform;
            rod = LifterKit.Visual(name + "Rod", parent, LifterKit.RamPiece(true, length, radius), mat).transform;
        }

        public void Set(Vector3 a, Vector3 b, Vector3 up)
        {
            if (!barrel || !rod) return;
            var d = b - a;
            if (d.sqrMagnitude < 1e-4f) return;
            barrel.SetPositionAndRotation(a, Quaternion.LookRotation(d, up));
            rod.SetPositionAndRotation(b, Quaternion.LookRotation(-d, up));
        }

        public void Show(bool on)
        {
            if (barrel) barrel.gameObject.SetActive(on);
            if (rod) rod.gameObject.SetActive(on);
        }

        public void Destroy()
        {
            if (barrel) Object.Destroy(barrel.gameObject);
            if (rod) Object.Destroy(rod.gameObject);
        }
    }

    /// <summary>Outriggers of a crane truck: the vehicle's movable pieces "Outrigger_XY" (beam, slides out sideways) and
    /// "OutriggerLeg_XY" (leg + pad, slides out with the beam, then down). Deployed, every pad that reaches the ground
    /// pushes the truck up like a stiff spring with friction (the wheels unload), so a load on the boom can't tip it.</summary>
    public class Outriggers
    {
        public const float Reach = 0.9f, Drop = 0.32f;
        struct Leg { public Transform beam, leg; public Vector3 beamRest, legRest, pad; public float side, shown; }
        readonly List<Leg> legs = new List<Leg>();
        readonly Rigidbody rb;
        readonly Transform root;
        public float Deployed { get; private set; }
        /// <summary>Pads pressing on the ground (last physics step).</summary>
        public int Planted { get; private set; }
        public bool Any => legs.Count > 0;

        public Outriggers(Transform vehicle, Rigidbody body)
        {
            root = vehicle; rb = body;
            foreach (Transform c in vehicle)
            {
                if (!c.name.StartsWith("Outrigger_")) continue;
                var leg = vehicle.Find("OutriggerLeg_" + c.name.Substring(10));
                foreach (var col in c.GetComponentsInChildren<Collider>()) col.enabled = false;            // the pads below carry the load
                if (leg) foreach (var col in leg.GetComponentsInChildren<Collider>()) col.enabled = false;
                var l = new Leg { beam = c, leg = leg, beamRest = c.localPosition, legRest = leg ? leg.localPosition : c.localPosition, side = Mathf.Sign(c.localPosition.x) };
                var mf = leg ? leg.GetComponent<MeshFilter>() : c.GetComponent<MeshFilter>();
                var mb = mf && mf.sharedMesh ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one * 0.1f);
                l.pad = new Vector3(mb.center.x, mb.min.y, mb.center.z);
                legs.Add(l);
            }
        }

        /// <summary>Animate towards deployed (true) or stowed; returns true while anything moves.</summary>
        public bool Tick(bool deploy, float dt)
        {
            float before = Deployed;
            Deployed = Mathf.MoveTowards(Deployed, deploy ? 1f : 0f, dt * 0.45f);
            var t = DeformableTerrain.Instance;
            for (int i = 0; i < legs.Count; i++)
            {
                var l = legs[i];
                float slide = Mathf.Clamp01(Deployed / 0.6f) * Reach, lower = Mathf.Clamp01((Deployed - 0.6f) / 0.4f) * Drop;
                if (l.beam) l.beam.localPosition = l.beamRest + Vector3.right * (l.side * slide);
                if (l.leg)
                {
                    // the visible pad stops on the ground
                    var padRest = root.TransformPoint(l.legRest + l.pad + Vector3.right * (l.side * slide));
                    float room = t ? padRest.y - t.Height(padRest.x, padRest.z) : lower;
                    l.shown = Mathf.Clamp(Mathf.Min(lower, room + 0.02f), 0f, Drop);
                    l.leg.localPosition = l.legRest + Vector3.right * (l.side * slide) + Vector3.down * l.shown;
                }
                legs[i] = l;
            }
            return !Mathf.Approximately(before, Deployed);
        }

        /// <summary>Ground forces of the planted pads (FixedUpdate).</summary>
        public void Push()
        {
            Planted = 0;
            if (Deployed < 0.6f || !rb || rb.isKinematic) return;
            var t = DeformableTerrain.Instance;
            if (!t) return;
            float lower = Mathf.Clamp01((Deployed - 0.6f) / 0.4f) * Drop;
            int n = Mathf.Max(1, legs.Count);
            float k = rb.mass * 9.81f / (n * 0.05f), c = 1.4f * Mathf.Sqrt(k * rb.mass / n);
            foreach (var l in legs)
            {
                var pad = root.TransformPoint(l.legRest + l.pad + Vector3.right * (l.side * Reach)) + root.up * -lower;
                float pen = t.Height(pad.x, pad.z) - pad.y;
                if (pen <= 0f) continue;
                Planted++;
                var v = rb.GetPointVelocity(pad);
                float f = Mathf.Clamp(k * Mathf.Min(pen, 0.25f) - c * v.y, 0f, rb.mass * 9.81f);
                var flat = new Vector3(v.x, 0f, v.z);
                var fric = Vector3.ClampMagnitude(-flat * c * 0.8f, f * 0.9f);
                rb.AddForceAtPosition(Vector3.up * f + fric, pad);
            }
        }
    }
}
