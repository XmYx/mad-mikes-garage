using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using MadMax.Building;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    public partial class RollingCity
    {
        static Material glowMaterial;

        /// <summary>The unlit variant of the city material for lamps, lit windows, screens and beacons.</summary>
        public static Material GlowMaterial(Material source)
        {
            if (!glowMaterial)
            {
                glowMaterial = new Material(source) { name = "RollingCity_Glow" };
                glowMaterial.SetFloat("_Unlit", 1f);
                glowMaterial.SetFloat("_OutlinePx", 0f);
            }
            return glowMaterial;
        }

        /// <summary>Build the generated tracked city (<see cref="TrackedCity"/>): meshes generated on a worker, turned into
        /// objects a few per frame; every storey of a block shares its module's meshes (shell, glow, furnishings and the
        /// collision mesh, baked once on a worker); crawler units are instanced (mirrored by scale) with four belt
        /// phases; the deck plane and the gangway are moving <see cref="StructureGround"/> decks.</summary>
        IEnumerator BuildTracked(int seed, Material baseMat)
        {
            var mat = IndustrialMaterial(baseMat, seed);
            var glowMat = GlowMaterial(baseMat);
            var p = Tracked;
            var task = Task.Run(() => TrackedCity.Build(p));
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) { Debug.LogException(task.Exception); yield break; }
            var B = task.Result;
            Plan = B.plan;
            stacks = B.stacks;
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
            Mesh M(VoxelMesher.MeshData md, string name) { var m = VoxelMesher.ToMesh(md, name); ownedMeshes.Add(m); return m; }

            // fixed pieces: hull + galleries, halls, industry, plants, details, fixtures
            foreach (var piece in B.pieces)
            {
                var go = Part(piece.name, M(piece.kit.mesh, "City_" + piece.name), mat, transform);
                var glow = piece.kit.glow.verts.Count > 0 ? Part("Glow", M(piece.kit.glow, "City_" + piece.name + "_Glow"), glowMat, go.transform) : null;
                lamps.lamps.AddRange(piece.kit.lamps);
                if (piece.collide && piece.kit.solid.verts.Count > 0)
                {
                    var col = go.AddComponent<MeshCollider>();
                    Bake(col, M(piece.kit.solid, "City_" + piece.name + "_Solid"));
                    if (piece.cutaway) go.AddComponent<Cutaway>().extra = glow ? new[] { glow.GetComponent<Renderer>() } : null;
                }
                yield return null;
            }
            var kit = Plan.Kit();
            // panel blocks: shared storey modules
            var blocksRoot = new GameObject("Blocks").transform;
            blocksRoot.SetParent(transform, false);
            for (int bi = 0; bi < B.blocks.Count; bi++)
            {
                var bb = B.blocks[bi];
                int n = bb.shells.Length;
                var shell = new Mesh[n]; var glow = new Mesh[n]; var inside = new Mesh[n]; var solid = new Mesh[n];
                for (int v = 0; v < n; v++)
                {
                    shell[v] = M(bb.shells[v].mesh, $"City_B{bi}_M{v}");
                    glow[v] = bb.shells[v].glow.verts.Count > 0 ? M(bb.shells[v].glow, $"City_B{bi}_M{v}_Glow") : null;
                    inside[v] = M(bb.insides[v].mesh, $"City_B{bi}_M{v}_Inside");
                    var both = new CityKit(Vector3.zero);
                    both.Append(bb.shells[v], Vector3.zero);
                    both.Append(bb.insides[v], Vector3.zero);
                    solid[v] = M(both.solid, $"City_B{bi}_M{v}_Solid");
                }
                var root = new GameObject("Block" + bi).transform;
                root.SetParent(blocksRoot, false);
                for (int f = 0; f < bb.block.floors; f++)
                {
                    int v = bb.floorModule[f];
                    var at = bb.FloorAt(Plan, f);
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
                foreach (var room in Plan.rooms)                                                 // door signs (rent / buy, WastelandGame.Quarters)
                {
                    if (room.block != bb.block.index) continue;
                    var door = new GameObject("FlatDoor").AddComponent<FlatDoor>();
                    door.transform.SetParent(root, false);
                    door.transform.localPosition = kit.P(room.door + new Vector3(0f, 0f, 1.3f));
                    door.city = this; door.room = room.index;
                }
                var ex = Part("Roof", M(bb.extras.mesh, $"City_B{bi}_Roof"), mat, root);
                ex.transform.localPosition = bb.FloorAt(Plan, 0);
                Bake(ex.AddComponent<MeshCollider>(), M(bb.extras.solid, $"City_B{bi}_RoofSolid"));
                ex.AddComponent<Cutaway>();
                yield return null;
            }
            // crawler units: shared meshes, mirrored by scale, four belt phases
            var unitMesh = M(B.unit.mesh, "City_Unit");
            var unitGlow = B.unit.glow.verts.Count > 0 ? M(B.unit.glow, "City_UnitGlow") : null;
            var treadMeshes = new Mesh[4];
            for (int i = 0; i < 4; i++) treadMeshes[i] = M(B.treads[i].mesh, "City_Tread" + i);
            float s = Plan.s;
            foreach (float ux in Plan.units)
                foreach (int sy in new[] { -1, 1 })
                {
                    var u = Part("Bogie", unitMesh, mat, transform);
                    u.transform.localPosition = kit.P(ux, sy * Plan.uy, 0f);
                    u.transform.localScale = new Vector3(sy < 0 ? s : -s, s, ux >= 0 ? s : -s);
                    if (unitGlow) Part("Glow", unitGlow, glowMat, u.transform);
                    var bx = new GameObject("BogieBox");                                         // mirrored units: the box on a child with positive scale
                    bx.transform.SetParent(u.transform, false);
                    bx.transform.localScale = new Vector3(Mathf.Sign(u.transform.localScale.x), 1f, Mathf.Sign(u.transform.localScale.z));
                    var box = bx.AddComponent<BoxCollider>();
                    box.center = new Vector3(0f, TrackedCity.TrackR + TrackedCity.TrackLift, 0f);
                    box.size = new Vector3(TrackedCity.TrackW, 2f * TrackedCity.TrackR + 1.6f, Plan.Lu);
                    var set = new GameObject[4];
                    for (int i = 0; i < 4; i++)
                    {
                        set[i] = Part("Tread" + i, treadMeshes[i], mat, u.transform);
                        set[i].SetActive(i == 0);
                    }
                    treads.Add(set);
                }
            treadStep = TrackedCity.ShoePitch * 0.25f * s;
            // the deck plane for wheels, the gangway onto the pier
            var deckGo = new GameObject("Deck");
            deckGo.transform.SetParent(transform, false);
            var deckBox = deckGo.AddComponent<BoxCollider>();
            deckBox.center = new Vector3(0f, -0.25f, 0f);
            deckBox.size = new Vector3(Route.halfWidth * 2f, 0.5f, Route.halfLength * 2f);
            StructureGround.AddMovingDeck(this, transform, deckBox, new Vector4(Route.halfWidth, Route.halfLength, 0f, 0f), PointVelocity);
            gangLen = Route.pierGap + 1.2f; gangHalf = TrackedCity.GangHalfW;
            gangway = new GameObject("Gangway").transform;
            gangway.SetParent(transform, false);
            gangway.localPosition = new Vector3(Route.halfWidth, 0f, 0f);
            var gk = TrackedCity.Gangway(gangLen, seed);
            var gm = Part("GangwayMesh", M(gk.mesh, "City_Gangway"), mat, gangway);
            Bake(gm.AddComponent<MeshCollider>(), M(gk.solid, "City_GangwaySolid"));
            gangDeckBox = gm.AddComponent<BoxCollider>();
            gangDeckBox.center = new Vector3(gangLen * 0.5f, -0.125f, 0f); gangDeckBox.size = new Vector3(gangLen, 0.25f, gangHalf * 2f);
            gangDeck = new GameObject("GangwayDeck").transform;
            gangDeck.SetParent(gangway, false);
            gangDeck.localPosition = new Vector3(gangLen * 0.5f, 0f, 0f);

            foreach (var t in baking.Values) while (!t.IsCompleted) yield return null;
            foreach (var b in bakes) { b.col.sharedMesh = b.mesh; b.col.enabled = true; }
            Ready = true;
            Snap();
        }

        GameObject Part(string name, Mesh mesh, Material mat, Transform parent)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        // ------------------------------------------------------------------ CityHost
        public override string CityName => Name;
        public override TrackedCity.Plan CityPlan => Plan;
        public override bool IsReady => Ready;
        public override Vector3 PlanPoint(Vector3 b) => transform.TransformPoint(Plan.Kit().P(b));
        public override Vector3 ToPlan(Vector3 world)
        {
            var l = transform.InverseTransformPoint(world);
            return new Vector3(l.z, -l.x, l.y + Plan.ZD);                                            // deck-local game → generator space
        }
        List<Vector3> spots;
        public override List<Vector3> Spots() => spots ??= TrackedCity.DeckSpots(Plan);
        public override Vector3 ClerkSpot(out Vector3 facing) { facing = new Vector3(0f, -1f, 0f); return TrackedCity.ClerkSpot(Plan); }
    }
}
