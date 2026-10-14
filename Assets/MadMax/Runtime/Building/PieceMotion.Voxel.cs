using System.Collections.Generic;
using MadMax.Items;
using MadMax.Rendering;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    public partial class PieceMotion
    {
        /// <summary>One moving part cut out of a voxel template: the voxels it takes and its pivot (voxel coords).</summary>
        struct Spec
        {
            public string name;
            public System.Func<Vector3Int, Vox, bool> pick;
            public Vector3 pivot;
            public Spec(string n, System.Func<Vector3Int, Vox, bool> p, Vector3 pv) { name = n; pick = p; pivot = pv; }
        }

        /// <summary>The template split once per piece id: the rest of the piece and each moving part (piece space).</summary>
        sealed class Cut
        {
            public Mesh rest;
            public Mesh[] meshes;
            public Vector3[] pivots;
            public string[] names;
        }

        static readonly Dictionary<string, Cut> cuts = new Dictionary<string, Cut>();
        static Mesh flywheelMesh, flywheelHD;
        const byte Cloth = (byte)ResourceType.Cloth, Copper = (byte)ResourceType.Copper, Iron = (byte)ResourceType.Iron;

        /// <summary>Voxel looks of pieces with a lid, door, flag, bell or handle (the templates in FurnitureLibrary).</summary>
        static Spec[] Specs(string id)
        {
            switch (id)
            {
                case "chest": return new[] { new Spec("Lid", (p, v) => p.y >= 7, new Vector3(0f, 6.5f, -4.5f)) };
                case "crate": return new[] { new Spec("Lid", (p, v) => p.y >= 6, new Vector3(0f, 5.5f, -3.5f)) };
                case "barrel": return new[] { new Spec("Lid", (p, v) => p.y >= 10, new Vector3(0f, 9.5f, -3.5f)) };
                case "freezer": return new[] { new Spec("Lid", (p, v) => p.y >= 11, new Vector3(0f, 10.5f, -4.5f)) };
                case "ice_box": return new[] { new Spec("Lid", (p, v) => p.y >= 9, new Vector3(0f, 8.5f, -4.5f)) };
                case "fridge": return new[]
                {
                    new Spec("Door_Top", (p, v) => p.z >= 3 && p.y >= 15, new Vector3(-4.5f, 0f, 3.5f)),
                    new Spec("Door", (p, v) => p.z >= 3 && p.y >= 3 && p.y <= 14, new Vector3(-4.5f, 0f, 3.5f)),
                };
                case "locker": return new[]
                {
                    new Spec("Door_L", (p, v) => p.z >= 2 && p.y >= 1 && p.y <= 19 && p.x >= -2 && p.x <= 0, new Vector3(-2.5f, 0f, 2.5f)),
                    new Spec("Door_R", (p, v) => p.z >= 2 && p.y >= 1 && p.y <= 19 && p.x >= 1 && p.x <= 2, new Vector3(2.5f, 0f, 2.5f)),
                };
                case "claim_flag": return new[] { new Spec("Flag", (p, v) => v.mat == Cloth, new Vector3(0.5f, 51f, 0f)) };
                case "flag": return new[] { new Spec("Flag", (p, v) => v.mat == Cloth, new Vector3(0.5f, 35f, 0f)) };
                case "alarm_bell": return new[]
                {
                    new Spec("Bell", (p, v) => v.mat == Copper && p.y >= 22 && p.y <= 30 && Mathf.Abs(p.x) <= 5, new Vector3(0f, 31f, 0f)),
                    new Spec("Clapper", (p, v) => p.x == 0 && p.z == 0 && p.y >= 20 && p.y <= 21, new Vector3(0f, 30.5f, 0f)),
                };
                case "well": return new[] { new Spec("Handle", (p, v) => v.mat == Iron && ((p.y >= 20 && p.z <= -3) || (p.y == 19 && p.z <= -5)), new Vector3(0f, 19.5f, -3f)) };
                default: return null;
            }
        }

        static bool HasCut(string id) => Specs(id) != null;

        static Cut CutFor(FurnitureDef def)
        {
            if (cuts.TryGetValue(def.id, out var c) && c != null && c.rest) return c;
            var specs = Specs(def.id);
            if (specs == null || def.grid == null) return null;
            var rest = def.grid.Clone();
            var meshes = new List<Mesh>(); var pivots = new List<Vector3>(); var names = new List<string>();
            foreach (var s in specs)
            {
                var part = new VoxelGrid();
                foreach (var kv in def.grid.voxels)
                    if (rest.voxels.ContainsKey(kv.Key) && s.pick(kv.Key, kv.Value)) part.voxels[kv.Key] = kv.Value;
                if (part.Count == 0) continue;
                foreach (var k in part.voxels.Keys) rest.voxels.Remove(k);
                meshes.Add(VoxelMesher.Build(part, "Furniture_" + def.id + "_" + s.name, def.voxel));
                pivots.Add(s.pivot * def.voxel);
                names.Add(s.name);
            }
            if (meshes.Count == 0) { cuts[def.id] = null; return null; }
            c = new Cut { rest = VoxelMesher.Build(rest, "Furniture_" + def.id + "_rest", def.voxel), meshes = meshes.ToArray(), pivots = pivots.ToArray(), names = names.ToArray() };
            cuts[def.id] = c;
            return c;
        }

        Mesh restMesh;
        readonly List<GameObject> voxelParts = new List<GameObject>();

        /// <summary>The voxel look: swap in the template without its moving parts and hang each part on its pivot; a
        /// generator gets a spoked flywheel at its end. Dyed pieces (their own recoloured mesh) stay whole.</summary>
        void CutVoxels()
        {
            var mf = GetComponent<MeshFilter>(); var mr = GetComponent<MeshRenderer>();
            if (!piece || !mf || !mr) return;
            var def = FurnitureLibrary.Get(piece.id);
            if (def == null) return;
            if (piece.dye == 0 && mf.sharedMesh == def.mesh)
            {
                var cut = CutFor(def);
                if (cut != null)
                {
                    mf.sharedMesh = restMesh = cut.rest;
                    for (int i = 0; i < cut.meshes.Length; i++)
                    {
                        var pv = MakePart(cut.names[i], cut.meshes[i], cut.pivots[i], -cut.pivots[i], mr);
                        TryKind(cut.names[i], out var kind);
                        AddPart(kind, cut.names[i], new[] { pv }, true);
                    }
                }
            }
            if (gen && mf.sharedMesh) AddFlywheel(mf, mr);
        }

        /// <summary>A spoked flywheel at the generator's +X end (voxel, or smooth with the HD pack on and no flywheel
        /// in the model).</summary>
        void AddFlywheel(MeshFilter mf, MeshRenderer mr)
        {
            bool hdLook = HDBits.On && hd && hd.root;
            if (hdLook)
            {
                if (!flywheelHD)
                {
                    var s = new HDShapes();
                    s.Tube(new Vector3(-0.02f, 0f, 0f), new Vector3(0.02f, 0f, 0f), 0.2f, 0.2f, Pal.Metal[2], 18, true);
                    s.Box(new Vector3(0.026f, 0f, 0f), new Vector3(0.014f, 0.34f, 0.045f), Pal.Chrome[2], 0.004f);
                    s.Box(new Vector3(0.026f, 0f, 0f), new Vector3(0.014f, 0.045f, 0.34f), Pal.Rust[2], 0.004f);
                    s.Tube(new Vector3(0.02f, 0f, 0f), new Vector3(0.05f, 0f, 0f), 0.035f, 0.035f, Pal.Chrome[3], 10, true);
                    flywheelHD = s.ToMesh("PieceFlywheelHD");
                }
            }
            else
            {
                if (!flywheelMesh)
                {
                    var g = new VoxelGrid().Mat(Iron);
                    g.CylX(0f, 0f, 2.6f, 0, 0, Pal.Ramp(Pal.Metal, 1, 5101));
                    g.Box(0, -2, 0, 0, 2, 0, Pal.Solid(Pal.Chrome[2]));                                    // spokes: the turn reads
                    g.Box(0, 0, -2, 0, 0, 2, Pal.Ramp(Pal.Rust, 2, 5102));
                    g.Set(0, 0, 0, Pal.Solid(Pal.Chrome[3]));
                    g.Bevel();
                    flywheelMesh = VoxelMesher.Build(g, "PieceFlywheel");
                }
            }
            var def = piece ? FurnitureLibrary.Get(piece.id) : null;
            var b = def != null && def.mesh ? def.mesh.bounds : mf.sharedMesh.bounds;
            var at = new Vector3(b.max.x + (hdLook ? 0.03f : VoxelMesher.DefaultSize * 0.5f), Mathf.Lerp(b.min.y, b.max.y, 0.45f), b.center.z);
            var fw = MakePart("Flywheel", hdLook ? flywheelHD : flywheelMesh, at, Vector3.zero, mr);
            if (hdLook) fw.GetComponentInChildren<MeshRenderer>().sharedMaterial = HDShapes.Solid;
            AddPart(Kind.Flywheel, "Flywheel", new[] { fw }, true);
        }

        Transform MakePart(string name, Mesh mesh, Vector3 pivot, Vector3 meshOffset, MeshRenderer like)
        {
            var pv = new GameObject(name).transform;
            pv.SetParent(transform, false);
            pv.localPosition = pivot;
            var m = new GameObject(name + "Mesh", typeof(MeshFilter), typeof(MeshRenderer));
            m.layer = gameObject.layer; pv.gameObject.layer = gameObject.layer;
            m.transform.SetParent(pv, false);
            m.transform.localPosition = meshOffset;
            m.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = m.GetComponent<MeshRenderer>();
            r.sharedMaterial = like.sharedMaterial;
            r.shadowCastingMode = like.shadowCastingMode;
            voxelParts.Add(pv.gameObject);
            return pv;
        }

        /// <summary>A dye swapped the piece's mesh: back to the cut mesh when it was undone, else drop the cut parts
        /// (the dyed whole mesh shows them already).</summary>
        void CheckVoxelMesh()
        {
            if (!restMesh) return;
            var mf = GetComponent<MeshFilter>();
            if (!mf || mf.sharedMesh == restMesh) return;
            var def = piece ? FurnitureLibrary.Get(piece.id) : null;
            if (def != null && mf.sharedMesh == def.mesh) { mf.sharedMesh = restMesh; return; }
            restMesh = null;
            for (int i = parts.Count - 1; i >= 0; i--) if (parts[i].voxel && parts[i].kind != Kind.Flywheel) parts.RemoveAt(i);
            foreach (var go in voxelParts) if (go && go.name != "Flywheel") Destroy(go);
        }
    }
}
