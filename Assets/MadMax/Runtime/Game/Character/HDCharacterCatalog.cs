using System;
using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Every HD character mesh the game can wear (tools/blender/hd/PIPELINE.md, group <c>character</c>), re-bound to
    /// the <see cref="HumanRig"/> bones: built in the editor by <c>MadMax/HD/Build Character Catalog</c> into
    /// <c>Models/HD/Resources/HDCharacters.asset</c> (beside the gitignored HD pack, so a checkout without the pack simply
    /// has no catalogue and keeps the voxel humans). Pieces are per body shape (M rugged, M2 lean, F soft, F2 sharp);
    /// covers say which body triangles a garment hides.</summary>
    public class HDCharacterCatalog : ScriptableObject
    {
        public const int Version = 1;

        [Serializable]
        public class Piece
        {
            public string key;            // "<asset>/<piece>": unique
            public string shape;          // body shape key ("M", "M2", "F", "F2" or "h0.96w0.90" for unknown ones)
            public bool female;
            public string kind;           // body | under | eyes | brows | hair | beard | garment
            public string piece;          // piece name in the export (jacket_zip, hair_Short_cut0.236, beard)
            public string garment;        // game ClothingLibrary id (garments)
            public string variant;        // HD variant of the garment ("worn" for tshirt_worn, "up" for goggles_up)
            public string hair;           // HairStyle name (hair pieces)
            public float cut;             // hat cut height of a hair variant (0 = full hair)
            public int tint;              // 0 none, 1 skin, 2 hair
            public int toneRef = -1;      // the skin tone / hair colour index baked into the texture
            public Material material;
            public Mesh[] lods;           // LOD0..2, bindposes for the HumanRig rest pose (identity rotations)
            public string[] bones;        // bone names per mesh bone index ("Root" = the HumanRig transform)
            public string geom;           // geometry key (bodies with the same mesh share covers)
        }

        /// <summary>Body triangles hidden under a garment piece (bitset over the body LOD's triangle list).</summary>
        [Serializable]
        public class Cover
        {
            public string body;           // body geometry key
            public int lod;
            public string garment;        // garment piece key
            public int[] bits;
        }

        public int version;
        public string builtAt;
        public List<Piece> pieces = new List<Piece>();
        public List<Cover> covers = new List<Cover>();
        public List<string> shapes = new List<string>();

        [NonSerialized] Dictionary<string, Cover> coverMap;

        public Cover CoverOf(string bodyGeom, int lod, string garmentKey)
        {
            if (coverMap == null)
            {
                coverMap = new Dictionary<string, Cover>();
                foreach (var c in covers) coverMap[c.body + "|" + c.lod + "|" + c.garment] = c;
            }
            return coverMap.TryGetValue(bodyGeom + "|" + lod + "|" + garmentKey, out var r) ? r : null;
        }

        static HDCharacterCatalog loaded;
        static bool tried;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { loaded = null; tried = false; }

        /// <summary>The catalogue, or null when the HD pack (or its catalogue) is missing.</summary>
        public static HDCharacterCatalog Instance
        {
            get
            {
                if (loaded) return loaded;
                if (tried) return null;
                tried = true;
                loaded = Resources.Load<HDCharacterCatalog>("HDCharacters");
                if (loaded && (loaded.version != Version || loaded.pieces.Count == 0)) { Debug.LogWarning("[HD] character catalogue out of date: run MadMax/HD/Build Character Catalog"); loaded = null; }
                return loaded;
            }
        }

        /// <summary>Forget the loaded catalogue (the editor rebuilt it).</summary>
        public static void Reload() { loaded = null; tried = false; }
    }
}
