using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using MadMax.Building;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>The megacity in the world (roadmap 28 R4): <see cref="MegaCity.Build"/> on a worker, then towers, cluster
    /// decks, bridges, undercroft, plaza and pad as static pieces (collision baked on workers, cutaway like buildings),
    /// the panel blocks as shared storey modules with rentable flats (door signs, <see cref="CityHost"/>), a lift per
    /// cluster from the plaza (callable, gated landings, carries cars) and drivable decks on the pad, the hangar and every
    /// cluster. Frame: yawed 90° so generator x = world +X, generator y = world +Z.</summary>
    public class MegaCityHost : CityHost
    {
        public MegaSite Site { get; private set; }
        public MegaCity.Built Built { get; private set; }
        public readonly List<CityLift> Lifts = new List<CityLift>();
        public readonly List<LiftGate> Gates = new List<LiftGate>();
        bool ready;
        readonly List<Mesh> owned = new List<Mesh>();

        public const float LiftSpeed = 5f, LiftAccel = 1.2f, LiftDwell = 8f;

        public static MegaCityHost Create(MegaSite site, Material baseMat)
        {
            var go = new GameObject("MegaCity_" + site.name);
            go.transform.SetPositionAndRotation(new Vector3(site.centre.x, site.height, site.centre.y), Quaternion.Euler(0f, 90f, 0f));
            var h = go.AddComponent<MegaCityHost>();
            h.Site = site;
            h.StartCoroutine(h.Build(baseMat));
            return h;
        }

        IEnumerator Build(Material baseMat)
        {
            var mat = RollingCity.IndustrialMaterial(baseMat, Site.p.seed);
            var glowMat = RollingCity.GlowMaterial(baseMat);
            var p = Site.p;
            var task = Task.Run(() => MegaCity.Build(p));
            var platTask = Task.Run(() => TrackedCity.LiftPlatform(p.seed + 9));
            var gateTask = Task.Run(() => MegaCity.Gate(MegaCity.ShaftHX * 2f + 0.24f, p.seed + 13));
            while (!task.IsCompleted || !platTask.IsCompleted || !gateTask.IsCompleted) yield return null;
            if (task.IsFaulted) { Debug.LogException(task.Exception); yield break; }
            var B = Built = task.Result;
            var plan = B.plan;
            var kit = plan.Kit();
            var lamps = gameObject.AddComponent<CityLights>();
            var interiors = gameObject.AddComponent<CityInteriors>();
            var bakes = new List<(MeshCollider col, Mesh mesh)>();
            var baking = new Dictionary<Mesh, Task>();
            void Bake(MeshCollider col, Mesh m)
            {
                if (!baking.ContainsKey(m)) { var id = m.GetEntityId(); baking[m] = Task.Run(() => Physics.BakeMesh(id, false)); }
                col.enabled = false;
                bakes.Add((col, m));
            }
            Mesh M(VoxelMesher.MeshData md, string name) { var m = VoxelMesher.ToMesh(md, name); owned.Add(m); return m; }

            foreach (var piece in B.pieces)                                                       // towers, clusters, bridges, undercroft, plaza, pad
            {
                var go = Part(piece.name, M(piece.kit.mesh, "Mega_" + piece.name), mat, transform);
                var glow = piece.kit.glow.verts.Count > 0 ? Part("Glow", M(piece.kit.glow, "Mega_" + piece.name + "_Glow"), glowMat, go.transform) : null;
                lamps.lamps.AddRange(piece.kit.lamps);
                if (piece.collide && piece.kit.solid.verts.Count > 0)
                {
                    Bake(go.AddComponent<MeshCollider>(), M(piece.kit.solid, "Mega_" + piece.name + "_Solid"));
                    if (piece.cutaway) go.AddComponent<Cutaway>().extra = glow ? new[] { glow.GetComponent<Renderer>() } : null;
                }
                yield return null;
            }
            var blocksRoot = new GameObject("Blocks").transform;                                  // panel blocks: shared storey modules
            blocksRoot.SetParent(transform, false);
            for (int bi = 0; bi < B.blocks.Count; bi++)
            {
                var bb = B.blocks[bi];
                int n = bb.shells.Length;
                var shell = new Mesh[n]; var glow = new Mesh[n]; var inside = new Mesh[n]; var solid = new Mesh[n];
                for (int v = 0; v < n; v++)
                {
                    shell[v] = M(bb.shells[v].mesh, $"Mega_B{bi}_M{v}");
                    glow[v] = bb.shells[v].glow.verts.Count > 0 ? M(bb.shells[v].glow, $"Mega_B{bi}_M{v}_Glow") : null;
                    inside[v] = M(bb.insides[v].mesh, $"Mega_B{bi}_M{v}_Inside");
                    var both = new CityKit(Vector3.zero);
                    both.Append(bb.shells[v], Vector3.zero);
                    both.Append(bb.insides[v], Vector3.zero);
                    solid[v] = M(both.solid, $"Mega_B{bi}_M{v}_Solid");
                }
                var root = new GameObject("Block" + bi).transform;
                root.SetParent(blocksRoot, false);
                for (int f = 0; f < bb.block.floors; f++)
                {
                    int v = bb.floorModule[f];
                    var at = bb.FloorAt(plan, f);
                    var go = Part("Storey" + f, shell[v], mat, root);
                    go.transform.localPosition = at;
                    var extra = new List<Renderer>();
                    if (glow[v]) extra.Add(Part("Glow", glow[v], glowMat, go.transform).GetComponent<Renderer>());
                    var ins = Part("Inside", inside[v], mat, go.transform).GetComponent<Renderer>();
                    extra.Add(ins);
                    interiors.renderers.Add(ins);
                    Bake(go.AddComponent<MeshCollider>(), solid[v]);
                    go.AddComponent<Cutaway>().extra = extra.ToArray();
                    foreach (var l in bb.shells[v].lamps) lamps.lamps.Add((l.at + at, l.color, l.range));
                }
                foreach (var room in plan.rooms)                                                  // door signs (rent / buy, WastelandGame.Quarters)
                {
                    if (room.block != bb.block.index) continue;
                    var door = new GameObject("FlatDoor").AddComponent<FlatDoor>();
                    door.transform.SetParent(root, false);
                    door.transform.localPosition = kit.P(room.door + new Vector3(0f, 0f, 1.3f));
                    door.city = this; door.room = room.index;
                }
                var ex = Part("Roof", M(bb.extras.mesh, $"Mega_B{bi}_Roof"), mat, root);
                ex.transform.localPosition = bb.FloorAt(plan, 0);
                Bake(ex.AddComponent<MeshCollider>(), M(bb.extras.solid, $"Mega_B{bi}_RoofSolid"));
                ex.AddComponent<Cutaway>();
                yield return null;
            }
            // lifts: one per cluster from the plaza, gated at both landings
            var plat = platTask.Result;
            var platMesh = M(plat.mesh, "Mega_Lift"); var platGlow = plat.glow.verts.Count > 0 ? M(plat.glow, "Mega_LiftGlow") : null; var platSolid = M(plat.solid, "Mega_LiftSolid");
            var gateMesh = M(gateTask.Result.mesh, "Mega_Gate");
            foreach (var cl in B.clusters)
            {
                var top = kit.P(cl.lift.x, cl.lift.y, cl.z);
                var lift = CityLift.Create(transform, top, cl.z - 0.05f, platMesh, platGlow, platSolid, mat, glowMat);
                lift.transform.localRotation = cl.liftAlongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
                lift.speed = LiftSpeed; lift.accel = LiftAccel; lift.dwell = LiftDwell; lift.callable = true;
                lift.name = "Lift_" + Lifts.Count;
                Lifts.Add(lift);
                var rot = cl.liftAlongX ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
                float e = MegaCity.ShaftHY + 0.08f;
                foreach (int s in new[] { -1, 1 })
                    foreach (bool atTop in new[] { false, true })
                    {
                        var g = cl.liftAlongX ? new Vector3(cl.lift.x + s * e, cl.lift.y, atTop ? cl.z : 0.05f) : new Vector3(cl.lift.x, cl.lift.y + s * e, atTop ? cl.z : 0.05f);
                        Gates.Add(LiftGate.Create(transform, kit.P(g), rot, MegaCity.ShaftHX * 2f + 0.24f, gateMesh, mat, lift, atTop));
                    }
                yield return null;
            }
            // drivable decks: the pad, the hangar floor, every cluster deck (aircraft land, cars come up the lifts)
            if (B.hasPad) { Deck("PadDeck", B.padRect, B.padZ); Deck("HangarDeck", B.hangarRect, B.padZ); }
            foreach (var cl in B.clusters) foreach (var r in cl.rects) Deck("ClusterDeck", r, cl.z);

            foreach (var t in baking.Values) while (!t.IsCompleted) yield return null;
            foreach (var b in bakes) { b.col.sharedMesh = b.mesh; b.col.enabled = true; }
            ready = true;
        }

        void Deck(string name, MegaCity.Rect r, float z)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(-(r.y0 + r.y1) / 2, z, (r.x0 + r.x1) / 2);
            StructureGround.AddDeck(this, go.transform, null, new Vector4((r.y1 - r.y0) / 2, (r.x1 - r.x0) / 2, 0f, 0f));
        }

        GameObject Part(string name, Mesh mesh, Material mat, Transform parent)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>World position of the landing pad's centre (on its surface).</summary>
        public Vector3 PadCentreWorld => PlanPoint(Built.padCentre);

        protected override void OnDestroy()
        {
            base.OnDestroy();
            StructureGround.Remove(this);
            foreach (var m in owned) if (m) Destroy(m);
            owned.Clear();
        }

        // ------------------------------------------------------------------ CityHost
        public override string CityName => Site != null ? Site.name : "THE SPIRES";
        public override TrackedCity.Plan CityPlan => Built?.plan;
        public override bool IsReady => ready;
        public override float FolkRadius => Site != null ? Site.half : 0f;
        public override float PersonReach => 80f;                                                  // decks stack up: only the people around you
        public override bool InCity(Vector3 w)
        {
            var b = ToPlan(w);
            float h = Site.half + 2f;
            return Mathf.Abs(b.x) < h && Mathf.Abs(b.y) < h && b.z > -4f && b.z < Site.p.maxHeight + 80f;
        }
        public override Vector3 PlanPoint(Vector3 b) => transform.TransformPoint(new Vector3(-b.y, b.z, b.x));
        public override Vector3 ToPlan(Vector3 world)
        {
            var l = transform.InverseTransformPoint(world);
            return new Vector3(l.z, -l.x, l.y);
        }
        public override List<Vector3> Spots() => Built.spots;
        /// <summary>The clerk waits on the plaza beside the first lift.</summary>
        public override Vector3 ClerkSpot(out Vector3 facing)
        {
            var cl = Built.clusters.Count > 0 ? Built.clusters[0] : null;
            if (cl == null) { facing = Vector3.right; return new Vector3(0f, -Site.half + 8f, 0.05f); }
            var o = new Vector3(cl.liftOut.x, cl.liftOut.y, 0f);
            var along = cl.liftAlongX ? Vector3.right : Vector3.up;
            facing = -o;
            return cl.lift + o * (MegaCity.ShaftHX + 2.2f) + along * (MegaCity.ShaftHY + 1.5f) + new Vector3(0f, 0f, 0.05f);
        }
    }
}
