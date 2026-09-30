using System;
using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Game
{
    public enum BodyPart { Pelvis, Chest, Head, UpperArmL, UpperArmR, ForearmL, ForearmR, HandL, HandR, ThighL, ThighR, ShinL, ShinR, FootL, FootR }
    public enum ClothingSlot { Head, Face, Torso, Outer, Hands, Legs, Feet, Back, Pack, Vest, Arms, Shins, Belt }
    /// <summary>Damage kinds armour protects against (index into <see cref="ClothingDef.armor"/>).</summary>
    public enum DamageKind { Melee, Shot, Crash, Fall, Burn }
    public enum HairStyle { Bald, Buzz, Short, Mohawk, Ponytail, Long }

    /// <summary>Body look. Serializable (saves, network).</summary>
    [Serializable]
    public class Appearance
    {
        public int skinTone = 1;             // 0..3
        public HairStyle hair = HairStyle.Short;
        public int hairColor = 0;            // index into HumanDesign.HairColors
        public int beard;                    // 0 none, 1 stubble, 2 full
        public float height = 1f;            // 0.9..1.1
        public float build = 1f;             // 0.85..1.2

        public Appearance Clone() => (Appearance)MemberwiseClone();
    }

    /// <summary>A wearable garment: covers parts of body segments with an inflated voxel shell.</summary>
    public class ClothingDef
    {
        public string id, name;
        public ClothingSlot slot;
        public float warmth, cooling;     // °C of cold protection / heat relief (negative cooling = traps heat)
        public float inflate = 1f;                                   // voxels over the skin (layering)
        public Dictionary<BodyPart, Vector2> coverage = new Dictionary<BodyPart, Vector2>(); // (from,to) along the segment 0..1
        public Func<Vector3Int, BodyPart, float, Color32> paint;     // voxel, part, t along segment (alpha 0 = no voxel)
        public float carry;                                          // extra carry capacity, kg (backpacks)
        public float waterproof;                                     // 0..1 how much rain it keeps off (best garment counts)
        public float radiation;                                      // 0..1 radiation shielding (stacks multiplicatively)
        public bool filter;                                          // breathes through dust and smoke
        public float durability = 1f;                                // wear resistance (leather, metal > 1)
        public string style;                                         // look for first impressions: raider, hazmat, drifter
        public Func<Appearance, VoxelGrid> prop;                     // rigid extra (hat brim, backpack) in 4 cm voxels, bone-local
        public BodyPart propBone = BodyPart.Chest;
        public float[] armor;                                        // protection per DamageKind on the covered zones (null = none)
        public float weight;                                         // kg (0 = light clothing)
        public float noise;                                          // clank while moving (metal armour)
        public MadMax.Items.ResourceType mendWith = MadMax.Items.ResourceType.Cloth;
    }

    /// <summary>Human proportions, voxel meshes for body/garments/hair (4 cm voxels = twice the world detail).</summary>
    public static class HumanDesign
    {
        public const float S = 0.04f;

        public static readonly Color32[][] SkinTones =
        {
            new[] { Pal.Hex("e9c09a"), Pal.Hex("f5d2ae"), Pal.Hex("c99a72") },
            new[] { Pal.Hex("c98e62"), Pal.Hex("dba478"), Pal.Hex("a8734c") },
            new[] { Pal.Hex("8e5a38"), Pal.Hex("a56c46"), Pal.Hex("6e4228") },
            new[] { Pal.Hex("5a3620"), Pal.Hex("6e442a"), Pal.Hex("422616") },
        };
        public static readonly Color32[] HairColors = { Pal.Hex("1c1512"), Pal.Hex("4a2c18"), Pal.Hex("8a5a2a"), Pal.Hex("c8a060"), Pal.Hex("b0b0b0"), Pal.Hex("8a2a1a") };

        // ------------------------------------------------------------------ skeleton (metres, parent-relative, rest pose)
        public struct Bone { public BodyPart part; public BodyPart? parent; public Vector3 offset; public float length; }

        public static List<Bone> Skeleton(Appearance a)
        {
            float h = a.height, w = a.build;
            return new List<Bone>
            {
                new Bone { part = BodyPart.Pelvis, parent = null, offset = new Vector3(0, 0.94f * h, 0), length = 0.1f * h },
                new Bone { part = BodyPart.Chest, parent = BodyPart.Pelvis, offset = new Vector3(0, 0.1f * h, 0), length = 0.44f * h },
                new Bone { part = BodyPart.Head, parent = BodyPart.Chest, offset = new Vector3(0, 0.44f * h, 0), length = 0.3f * h },
                new Bone { part = BodyPart.UpperArmL, parent = BodyPart.Chest, offset = new Vector3(-0.2f * w, 0.39f * h, 0), length = 0.29f * h },
                new Bone { part = BodyPart.UpperArmR, parent = BodyPart.Chest, offset = new Vector3(0.2f * w, 0.39f * h, 0), length = 0.29f * h },
                new Bone { part = BodyPart.ForearmL, parent = BodyPart.UpperArmL, offset = new Vector3(0, -0.29f * h, 0), length = 0.25f * h },
                new Bone { part = BodyPart.ForearmR, parent = BodyPart.UpperArmR, offset = new Vector3(0, -0.29f * h, 0), length = 0.25f * h },
                new Bone { part = BodyPart.HandL, parent = BodyPart.ForearmL, offset = new Vector3(0, -0.25f * h, 0), length = 0.09f * h },
                new Bone { part = BodyPart.HandR, parent = BodyPart.ForearmR, offset = new Vector3(0, -0.25f * h, 0), length = 0.09f * h },
                new Bone { part = BodyPart.ThighL, parent = BodyPart.Pelvis, offset = new Vector3(-0.09f * w, -0.02f * h, 0), length = 0.43f * h },
                new Bone { part = BodyPart.ThighR, parent = BodyPart.Pelvis, offset = new Vector3(0.09f * w, -0.02f * h, 0), length = 0.43f * h },
                new Bone { part = BodyPart.ShinL, parent = BodyPart.ThighL, offset = new Vector3(0, -0.43f * h, 0), length = 0.43f * h },
                new Bone { part = BodyPart.ShinR, parent = BodyPart.ThighR, offset = new Vector3(0, -0.43f * h, 0), length = 0.43f * h },
                new Bone { part = BodyPart.FootL, parent = BodyPart.ShinL, offset = new Vector3(0, -0.43f * h, 0), length = 0.08f * h },
                new Bone { part = BodyPart.FootR, parent = BodyPart.ShinR, offset = new Vector3(0, -0.43f * h, 0), length = 0.08f * h },
            };
        }

        // ------------------------------------------------------------------ shapes: signed distance (metres, bone space; negative = inside)
        static float Capsule(Vector3 p, float len, float r0, float r1)
        {
            float t = Mathf.Clamp01(-p.y / len);
            var c = new Vector3(0, -t * len, 0);
            return Vector3.Distance(p, c) - Mathf.Lerp(r0, r1, t);
        }

        static float Ellipsoid(Vector3 p, Vector3 c, Vector3 r)
        {
            var q = new Vector3((p.x - c.x) / r.x, (p.y - c.y) / r.y, (p.z - c.z) / r.z);
            return (q.magnitude - 1f) * Mathf.Min(r.x, Mathf.Min(r.y, r.z));
        }

        static float Box(Vector3 p, Vector3 min, Vector3 max)
        {
            var c = (min + max) * 0.5f; var e = (max - min) * 0.5f;
            var d = new Vector3(Mathf.Abs(p.x - c.x) - e.x, Mathf.Abs(p.y - c.y) - e.y, Mathf.Abs(p.z - c.z) - e.z);
            return Vector3.Max(d, Vector3.zero).magnitude + Mathf.Min(Mathf.Max(d.x, Mathf.Max(d.y, d.z)), 0f);
        }

        public static float Sdf(BodyPart part, Vector3 p, Appearance a)
        {
            float h = a.height, w = a.build;
            switch (part)
            {
                case BodyPart.Pelvis: return Ellipsoid(p, new Vector3(0, 0.02f, 0), new Vector3(0.15f * w, 0.1f * h, 0.1f));
                case BodyPart.Chest:
                {
                    float t = Mathf.Clamp01(p.y / (0.44f * h));
                    if (p.y < -0.02f || p.y > 0.44f * h) return 1f;
                    float rx = Mathf.Lerp(0.13f, 0.185f, Mathf.SmoothStep(0, 1, t * 1.3f)) * w * (t > 0.85f ? Mathf.Lerp(1f, 0.75f, (t - 0.85f) / 0.15f) : 1f);
                    float rz = Mathf.Lerp(0.085f, 0.105f, t) * (p.z > 0 ? 1.05f : 1f);
                    var q = new Vector2(p.x / rx, p.z / rz);
                    return (q.magnitude - 1f) * Mathf.Min(rx, rz);
                }
                case BodyPart.Head:
                {
                    float neck = Capsule(new Vector3(p.x, p.y - 0.09f * h, p.z), 0.09f * h, 0.048f, 0.052f);
                    float skull = Ellipsoid(p, new Vector3(0, 0.2f * h, 0.005f), new Vector3(0.082f, 0.108f * h, 0.098f));
                    float jaw = Ellipsoid(p, new Vector3(0, 0.14f * h, 0.03f), new Vector3(0.068f, 0.055f, 0.07f));
                    float nose = Box(p, new Vector3(-0.012f, 0.165f * h, 0.085f), new Vector3(0.012f, 0.205f * h, 0.115f));
                    return Mathf.Min(Mathf.Min(neck, skull), Mathf.Min(jaw, nose));
                }
                case BodyPart.UpperArmL: case BodyPart.UpperArmR: return Capsule(p, 0.29f * h, 0.047f * w, 0.04f * w);
                case BodyPart.ForearmL: case BodyPart.ForearmR: return Capsule(p, 0.25f * h, 0.039f * w, 0.031f * w);
                case BodyPart.HandL: case BodyPart.HandR: return Box(p, new Vector3(-0.022f, -0.09f * h, -0.018f), new Vector3(0.022f, 0.005f, 0.018f));
                case BodyPart.ThighL: case BodyPart.ThighR: return Capsule(p, 0.43f * h, 0.075f * w, 0.055f * w);
                case BodyPart.ShinL: case BodyPart.ShinR: return Capsule(p, 0.43f * h, 0.052f * w, 0.038f * w);
                default: return Box(p, new Vector3(-0.045f, -0.08f * h, -0.06f), new Vector3(0.045f, 0.01f, 0.2f)); // feet
            }
        }

        public static float Along(BodyPart part, Vector3 p, Appearance a)
        {
            float h = a.height;
            switch (part)
            {
                case BodyPart.Pelvis: return Mathf.InverseLerp(0.12f * h, -0.08f * h, p.y);
                case BodyPart.Chest: return Mathf.InverseLerp(0f, 0.44f * h, p.y);
                case BodyPart.Head: return Mathf.InverseLerp(0f, 0.32f * h, p.y);
                case BodyPart.FootL: case BodyPart.FootR: return Mathf.InverseLerp(-0.06f, 0.2f, p.z);
                default:
                    float len = part <= BodyPart.UpperArmR ? 0.29f : part <= BodyPart.ForearmR ? 0.25f : part <= BodyPart.HandR ? 0.09f : 0.43f;
                    return Mathf.Clamp01(-p.y / (len * h));
            }
        }

        static Bounds PartBounds(BodyPart part, Appearance a)
        {
            float h = a.height, w = a.build;
            switch (part)
            {
                case BodyPart.Pelvis: return new Bounds(new Vector3(0, 0.02f, 0), new Vector3(0.36f * w, 0.26f * h, 0.26f));
                case BodyPart.Chest: return new Bounds(new Vector3(0, 0.22f * h, 0), new Vector3(0.44f * w, 0.5f * h, 0.26f));
                case BodyPart.Head: return new Bounds(new Vector3(0, 0.18f * h, 0.02f), new Vector3(0.24f, 0.4f * h, 0.28f));
                case BodyPart.FootL: case BodyPart.FootR: return new Bounds(new Vector3(0, -0.03f, 0.07f), new Vector3(0.14f, 0.14f, 0.32f));
                default: return new Bounds(new Vector3(0, -0.23f * h, 0), new Vector3(0.18f * w, 0.52f * h, 0.18f * w));
            }
        }

        // ------------------------------------------------------------------ meshes
        static VoxelMesher.MeshData Build(BodyPart part, Appearance a, float inflate, Func<Vector3Int, Vector3, float, Color32> paint, Vector2? range)
        {
            var g = new VoxelGrid();
            var b = PartBounds(part, a);
            b.Expand(inflate * S * 2f + S * 2f);
            var min = Vector3Int.FloorToInt(b.min / S); var max = Vector3Int.CeilToInt(b.max / S);
            for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            {
                var v = new Vector3Int(x, y, z);
                var p = (Vector3)v * S;
                if (Sdf(part, p, a) > inflate * S) continue;
                float t = Along(part, p, a);
                if (range.HasValue && (t < range.Value.x || t > range.Value.y)) continue;
                var col = paint(v, p, t);
                if (col.a == 0) continue;                                           // straps, holes, open backs
                g.Set(v, _ => col);
            }
            if (g.Count == 0) return null;
            g.Bevel(0.12f, 0.18f);
            return VoxelMesher.BuildData(g, S);
        }

        static Mesh Upload(VoxelMesher.MeshData md, string name) => md == null ? null : VoxelMesher.ToMesh(md, name);

        // the *Data builders are pure (worker threads: HumanRig.Prewarm); the *Mesh wrappers upload on the main thread
        public static Mesh BodyMesh(BodyPart part, Appearance a) => Upload(BodyData(part, a), "Body_" + part);
        public static Mesh GarmentMesh(ClothingDef def, BodyPart part, Appearance a, bool torn = false) => Upload(GarmentData(def, part, a, torn), def.id + "_" + part + (torn ? "_torn" : ""));
        public static Mesh PropMesh(ClothingDef def, Appearance a) => Upload(PropData(def, a), def.id + "_prop");
        public static Mesh HairCap(Appearance a) => Upload(HairCapData(a), "HairCap");

        public static VoxelMesher.MeshData BodyData(BodyPart part, Appearance a)
        {
            var tone = SkinTones[Mathf.Clamp(a.skinTone, 0, 3)];
            var hair = HairColors[Mathf.Clamp(a.hairColor, 0, HairColors.Length - 1)];
            float h = a.height;
            return Build(part, a, 0f, (v, p, t) =>
            {
                var c = Pal.Hash(v, 17) > 0.8f ? tone[1] : Pal.Hash(v, 18) > 0.85f ? tone[2] : tone[0];
                if (part == BodyPart.Head)
                {
                    // face (front, z > 0): eyes, brows, mouth, beard
                    if (p.z > 0.07f)
                    {
                        float eyeY = 0.195f * h, ex = Mathf.Abs(p.x);
                        if (Mathf.Abs(p.y - eyeY) < 0.012f && ex > 0.018f && ex < 0.05f) c = Pal.Hash(v, 3) > 0.5f ? Pal.Hex("f2ece4") : Pal.Hex("2a1c14");
                        if (Mathf.Abs(p.y - (eyeY + 0.035f)) < 0.012f && ex > 0.015f && ex < 0.055f) c = hair;
                        if (Mathf.Abs(p.y - 0.135f * h) < 0.01f && ex < 0.028f) c = Color32.Lerp(tone[2], Pal.Hex("7a2a20"), 0.5f);
                    }
                    if (a.beard > 0 && p.z > 0.02f && p.y < 0.165f * h && p.y > 0.09f * h && Pal.Hash(v, 9) < (a.beard == 1 ? 0.45f : 0.95f))
                        if (!(Mathf.Abs(p.y - 0.135f * h) < 0.008f && Mathf.Abs(p.x) < 0.022f)) c = hair;
                    if (p.y < 0.105f * h) c = tone[2];                                 // neck in shadow
                }
                if (part == BodyPart.Pelvis) c = Pal.Hex("3a3634");                  // underwear
                return c;
            }, null);
        }

        public static VoxelMesher.MeshData GarmentData(ClothingDef def, BodyPart part, Appearance a, bool torn = false)
        {
            if (!def.coverage.TryGetValue(part, out var range)) return null;
            // worn-out garments get ragged holes
            return Build(part, a, def.inflate, (v, p, t) => torn && Pal.Hash(v, 97) < 0.16f ? default : def.paint(v, part, t), range);
        }

        /// <summary>A garment's rigid extra (hat brim, backpack) in the bone's space.</summary>
        public static VoxelMesher.MeshData PropData(ClothingDef def, Appearance a)
        {
            if (def.prop == null) return null;
            var g = def.prop(a);
            if (g == null || g.Count == 0) return null;
            g.Bevel(0.12f, 0.18f);
            return VoxelMesher.BuildData(g, S);
        }

        /// <summary>Static hair cap on the head (dynamic strands are added by HairStrands).</summary>
        public static VoxelMesher.MeshData HairCapData(Appearance a)
        {
            if (a.hair == HairStyle.Bald) return null;
            var col = HairColors[Mathf.Clamp(a.hairColor, 0, HairColors.Length - 1)];
            float h = a.height;
            var g = new VoxelGrid();
            float thick = a.hair == HairStyle.Buzz ? 0.5f : 1.3f;
            for (int x = -5; x <= 5; x++)
            for (int y = 0; y <= 12; y++)
            for (int z = -6; z <= 6; z++)
            {
                var v = new Vector3Int(x, y, z);
                var p = (Vector3)v * S;
                float d = Sdf(BodyPart.Head, p, a);
                if (d > thick * S || d < -S) continue;
                bool top = p.y > 0.2f * h;
                bool back = p.z < 0.02f && p.y > 0.13f * h;
                bool sides = Mathf.Abs(p.x) > 0.06f && p.y > 0.17f * h && p.z < 0.06f;
                if (a.hair == HairStyle.Mohawk) { if (Mathf.Abs(p.x) > 0.018f || p.y < 0.19f * h) continue; }
                else if (!(top || back || sides)) continue;
                if (p.z > 0.075f && p.y < 0.26f * h) continue;                       // keep the face clear
                g.Set(v, _ => Pal.Hash(v, 21) > 0.75f ? Color32.Lerp(col, Color.white, 0.15f) : col);
            }
            if (a.hair == HairStyle.Mohawk)
                for (int z = -4; z <= 3; z++) for (int y = 0; y < 3; y++) g.Set(0, Mathf.RoundToInt(0.31f * h / S) + y - Mathf.Abs(z) / 3, z, Pal.Solid(col));
            if (g.Count == 0) return null;
            g.Bevel(0.15f, 0.2f);
            return VoxelMesher.BuildData(g, S);
        }

        public static Mesh HairStrandMesh(Color32 col, float length)
        {
            var g = new VoxelGrid();
            int n = Mathf.Max(1, Mathf.RoundToInt(length / S));
            for (int i = 0; i < n; i++) { g.Set(0, -i, 0, Pal.Solid(col)); g.Set(1, -i, 0, Pal.Solid(Color32.Lerp(col, Color.black, 0.2f))); }
            return VoxelMesher.Build(g, "HairStrand", S);
        }
    }
}
