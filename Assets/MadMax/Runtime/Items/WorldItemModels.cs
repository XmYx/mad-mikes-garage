using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Items
{
    /// <summary>Meshes for items and resources lying in the world (<see cref="WorldItem"/>, the PLACE preview) and for
    /// the HUD item feed: tools use their hand mesh laid flat, hats / helmets / packs their worn prop, other items their
    /// icon model scaled to real size (flat things lie face up), resources a crate, a sack or a jerry can tinted by
    /// <see cref="ResourceInfo.Color"/>, build kits a flat-pack crate.</summary>
    public static class WorldItemModels
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        /// <summary>World mesh for an item id or "res:N", with the scale and the rotation it lies at.</summary>
        public static Mesh For(string key, out float scale, out Quaternion lie)
        {
            scale = 1f; lie = Quaternion.identity;
            if (key.StartsWith("res:")) return Resource((ResourceType)int.Parse(key.Substring(4)));
            if (key.StartsWith("tool_")) { lie = Quaternion.Euler(90f, 0f, 0f); scale = 0.7f; return MadMax.Game.ToolLibrary.MeshFor(key); }   // held along -Y, at hand scale: lay it down
            var cat = ItemCatalog.Category(key);
            if (cat == ItemCategory.Kit) return Kit();
            if (cat == ItemCategory.Clothing && MadMax.Game.ClothingLibrary.Get(key) is MadMax.Game.ClothingDef cd && cd.prop != null)
            {
                var pm = Prop(cd);                                                                  // helmets, packs: their own rigid shape
                if (pm) return pm;
            }
            var mesh = ItemModels.Get(key);
            var e = mesh ? mesh.bounds.size : Vector3.one;
            // flat things (clothes, books, shells, a fish, a canteen: thin in z, drawn standing) lie front (+Z) face up
            bool flat = cat == ItemCategory.Clothing || cat == ItemCategory.Media || cat == ItemCategory.Ammo && !key.StartsWith("bait_")
                        || key.StartsWith("trophy_") || e.z < e.y * 0.5f;
            if (flat) lie = Quaternion.Euler(-90f, 0f, 0f);
            scale = cat == ItemCategory.Clothing ? 0.45f : cat == ItemCategory.Media || cat == ItemCategory.Seed || cat == ItemCategory.Ammo ? 0.3f : 0.35f;
            return mesh;
        }

        /// <summary>Mesh for a small icon (HUD feed): same models, tools drawn diagonally like the hotbar.</summary>
        public static Mesh IconMesh(string key, out bool diagonal)
        {
            diagonal = key.StartsWith("tool_");
            return For(key, out _, out _);
        }

        /// <summary>Add the visual as a child of <paramref name="root"/>: lowest point on the root's origin, centred in
        /// x/z. <paramref name="bounds"/> is the visual's box in the root's space (for the collider).</summary>
        public static Transform AddVisual(Transform root, string key, Material mat, out Bounds bounds)
        {
            var mesh = For(key, out float scale, out var lie);
            var vis = new GameObject("Visual", typeof(MeshFilter), typeof(MeshRenderer)).transform;
            vis.SetParent(root, false);
            vis.GetComponent<MeshFilter>().sharedMesh = mesh;
            vis.GetComponent<MeshRenderer>().sharedMaterial = mat;
            vis.localRotation = lie;
            vis.localScale = Vector3.one * scale;
            var b = mesh ? mesh.bounds : new Bounds(Vector3.zero, Vector3.one * 0.1f);
            var min = Vector3.positiveInfinity; var max = Vector3.negativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                var c = lie * Vector3.Scale(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z), Vector3.one * scale);
                min = Vector3.Min(min, c); max = Vector3.Max(max, c);
            }
            var centre = (min + max) * 0.5f;
            vis.localPosition = new Vector3(-centre.x, -min.y, -centre.z);
            bounds = new Bounds(new Vector3(0f, (max.y - min.y) * 0.5f, 0f), max - min);
            AddHD(root, vis, key, ref bounds);
            return vis;
        }

        /// <summary>HD asset id of a world item key: items and tools by their id, resources by their container
        /// (<c>world_res_fluid</c> jerry can, <c>world_res_sack</c>, <c>world_res_crate</c>), build kits the flat-pack crate.</summary>
        public static string HDId(string key, out MadMax.Rendering.HDDomain domain)
        {
            domain = MadMax.Rendering.HDDomain.Item;
            if (string.IsNullOrEmpty(key)) return null;
            if (key.StartsWith("res:") && int.TryParse(key.Substring(4), out int r))
            {
                var t = (ResourceType)r;
                return ResourceInfo.IsFluid(t) ? "world_res_fluid" : Sacked(t) ? "world_res_sack" : "world_res_crate";
            }
            if (key.StartsWith("tool_")) { domain = MadMax.Rendering.HDDomain.Tool; return key; }
            if (ItemCatalog.Category(key) == ItemCategory.Kit && !MadMax.Rendering.HDAssets.Has(domain, key)) return "world_kit";
            return key;
        }

        /// <summary>The HD model beside the voxel one (which goes dark but keeps its mesh): real size as exported (tools at
        /// the laid-down hand scale of the sidecar, lying like the voxel tool), lowest point on the root's origin.</summary>
        static void AddHD(Transform root, Transform vis, string key, ref Bounds bounds)
        {
            string id = HDId(key, out var domain);
            var a = MadMax.Rendering.HDAssets.Get(domain, id);
            if (!a) return;
            bool tool = domain == MadMax.Rendering.HDDomain.Tool;
            var lie = tool ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            float scale = tool ? a.worldScale : 1f;
            var b = a.Bounds;
            var min = Vector3.positiveInfinity; var max = Vector3.negativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                var c = lie * (new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z) * scale);
                min = Vector3.Min(min, c); max = Vector3.Max(max, c);
            }
            var centre = (min + max) * 0.5f;
            var v = MadMax.Rendering.HDVisual.DressAt(vis.gameObject, a, root, new Vector3(-centre.x, -min.y, -centre.z), lie, scale, 0, true);
            if (v) bounds = new Bounds(new Vector3(0f, (max.y - min.y) * 0.5f, 0f), max - min);
        }

        /// <summary>A garment's rigid extra (hat, helmet, pack) as worn, at real size.</summary>
        static Mesh Prop(MadMax.Game.ClothingDef cd)
        {
            string name = "WorldProp_" + cd.id;
            if (cache.TryGetValue(name, out var m) && m) return m;
            var data = MadMax.Game.HumanDesign.PropData(cd, new MadMax.Game.Appearance());
            if (data == null) return null;
            m = VoxelMesher.ToMesh(data, name);
            cache[name] = m;
            return m;
        }

        static bool Sacked(ResourceType t) => t switch
        {
            ResourceType.Sand or ResourceType.Clay or ResourceType.Laterite or ResourceType.Rubble or ResourceType.Slag or ResourceType.Stone
                or ResourceType.IronOre or ResourceType.CopperOre or ResourceType.TinOre or ResourceType.Bauxite or ResourceType.Silica
                or ResourceType.Charcoal or ResourceType.Lime or ResourceType.Gunpowder or ResourceType.Sulfur or ResourceType.Coal
                or ResourceType.LeadOre or ResourceType.UraniumOre or ResourceType.Gravel or ResourceType.Wool or ResourceType.Hay
                or ResourceType.Feed or ResourceType.GoldOre or ResourceType.Salt => true,
            _ => false
        };

        static Color32 Shade(Color32 c, float k) => new Color32((byte)Mathf.Min(255, c.r * k), (byte)Mathf.Min(255, c.g * k), (byte)Mathf.Min(255, c.b * k), 255);

        /// <summary>The content colour with a little speckle (lighter / darker voxels).</summary>
        static VoxMat Content(Color32 c, int seed) => p => { float h = Pal.Hash(p, seed); return h < 0.2f ? Shade(c, 0.78f) : h > 0.85f ? Shade(c, 1.15f) : c; };

        static Mesh Resource(ResourceType t)
        {
            string name = "WorldRes_" + (int)t;
            if (cache.TryGetValue(name, out var m) && m) return m;
            var g = new VoxelGrid();
            var c = ResourceInfo.Color(t); c.a = 255;
            if (ResourceInfo.IsFluid(t))
            {
                // jerry can: body in the fluid's colour, embossed X on the flanks, handle and spout on top
                g.Box(-1, 0, -2, 0, 5, 2, p => System.Math.Abs(System.Math.Abs(p.z) - System.Math.Abs(p.y - 2.5f)) < 0.6f && p.y > 0 && p.y < 5 ? Shade(c, 0.7f) : c);
                g.Box(-1, 6, -2, 0, 6, -2, Pal.Ramp(Pal.Black, 2)); g.Box(-1, 6, 0, 0, 6, 0, Pal.Ramp(Pal.Black, 2));
                g.Box(-1, 7, -2, 0, 7, 0, Pal.Ramp(Pal.Black, 2));                                  // handle
                g.Box(-1, 6, 2, 0, 7, 2, Pal.Ramp(Pal.Chrome, 1));                                   // spout
            }
            else if (Sacked(t))
            {
                // sack: burlap body, the load showing at the open neck, a tie and a stencilled patch
                g.CylY(0, 0, 2.3f, 0, 3, Pal.Ramp(Pal.Cream, 0, 311));
                g.CylY(0, 0, 1.6f, 4, 4, Content(c, 312));
                g.Repaint(-1, 1, 2, 1, 2, 2, Content(c, 313));
                g.Set(0, 5, 0, Pal.Ramp(Pal.Wood, 1));
            }
            else
            {
                // crate: slatted planks, the goods heaped inside, a painted band on the front
                g.Box(-2, 0, -2, 2, 3, 2, Pal.Stripe(Pal.Ramp(Pal.Wood, 2, 321), Pal.Ramp(Pal.Wood, 1, 322), 1, 2));
                g.ClearBox(-1, 3, -1, 1, 3, 1);
                g.Box(-1, 3, -1, 1, 3, 1, Content(c, 323));
                g.Set(0, 4, 0, Content(c, 324)); g.Set(-1, 4, 1, Content(c, 325));
                g.Repaint(-1, 1, 2, 1, 1, 2, Pal.Solid(Shade(c, 0.9f)));
            }
            g.Bevel();
            m = VoxelMesher.Build(g, name);
            cache[name] = m;
            return m;
        }

        static Mesh Kit()
        {
            const string name = "WorldKit";
            if (cache.TryGetValue(name, out var m) && m) return m;
            var g = new VoxelGrid();
            // flat-pack crate: planks with a stencilled band, the instruction sheet on top, corner brackets
            g.Box(-3, 0, -2, 3, 2, 2, Pal.Stripe(Pal.Ramp(Pal.Wood, 3, 331), Pal.Ramp(Pal.Wood, 2, 332), 0, 3));
            g.Repaint(-2, 1, 2, 2, 1, 2, Pal.Ramp(Pal.Navy, 3));
            g.Repaint(-1, 2, -1, 1, 2, 1, Pal.Ramp(Pal.Cream, 2));
            foreach (int x in new[] { -3, 3 }) foreach (int z in new[] { -2, 2 }) g.Set(x, 2, z, Pal.Ramp(Pal.Metal, 2));
            g.Bevel();
            m = VoxelMesher.Build(g, name);
            cache[name] = m;
            return m;
        }
    }
}
