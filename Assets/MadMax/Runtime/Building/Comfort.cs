using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>How homely a spot is (0..10): a roof overhead, walls around, and the distinct pieces furnishing the
    /// room (each kind counts once: variety beats spam; painted pieces add a personal touch). Blood and bodies nearby
    /// take it away. Beds use it for sleep quality and the WELL RESTED buff.</summary>
    public static class Comfort
    {
        public const float Max = 10f;

        static readonly Dictionary<string, float> value = new Dictionary<string, float>
        {
            { "rug", 1f }, { "painting", 1f }, { "flag", 0.5f }, { "flower_pot", 1f }, { "planter", 0.5f }, { "skull_pole", 0.25f },
            { "sofa", 1.5f }, { "armchair", 1.5f }, { "chair", 0.5f }, { "bench", 0.5f }, { "table", 0.5f }, { "dining_table", 1f },
            { "lamp", 0.5f }, { "light_ceiling", 0.5f }, { "fireplace", 1.5f }, { "stove", 0.5f }, { "heater", 0.5f },
            { "bookshelf", 1f }, { "wall_clock", 0.5f }, { "mirror", 0.5f }, { "trophy_mount", 1f }, { "weapon_rack", 0.5f },
            { "radio", 1f }, { "tv", 1f }, { "wardrobe", 0.5f }, { "bathtub", 1f }, { "shower", 0.5f }, { "sink", 0.5f },
            { "kitchen_counter", 0.5f }, { "fridge", 0.5f }, { "bed", 0.5f }, { "chest", 0.25f }, { "shelf", 0.25f },
        };

        static readonly HashSet<string> seen = new HashSet<string>();
        static readonly RaycastHit[] hits = new RaycastHit[8];

        /// <summary>Comfort at <paramref name="p"/> (a floor point); <paramref name="note"/> says why ("ROOF, WALLS, 6 PIECES").
        /// <paramref name="home"/>: the vehicle the spot is in (its walls count, other vehicles never do).</summary>
        public static float At(Vector3 p, out string note, Rigidbody home = null)
        {
            var eye = p + Vector3.up * 1.2f;
            bool roof = Shelter(eye, Vector3.up, 8f, home);
            int walls = 0;
            for (int i = 0; i < 8; i++)
                if (Shelter(eye, Quaternion.Euler(0, i * 45f, 0) * Vector3.forward, 7f, home)) walls++;
            float score = (roof ? 2f : 0f) + (walls >= 7 ? 2f : walls >= 5 ? 1f : 0f);

            seen.Clear();
            float decor = 0f, painted = 0f;
            foreach (var piece in Placeable.All)
            {
                if (!piece || (piece.transform.position - p).sqrMagnitude > 49f || !value.TryGetValue(piece.id, out float v)) continue;
                if (piece.id == "trophy_mount" && piece.TryGetComponent<TrophyMount>(out var tm) && string.IsNullOrEmpty(tm.trophy)) continue;
                if (!InRoom(eye, piece)) continue;
                if (seen.Add(piece.id)) decor += v;
                if (piece.dye > 0) painted += 0.25f;
            }
            // furniture only makes a home under a roof; out in the open it is half as cosy
            score += roof ? Mathf.Min(6f, decor) + Mathf.Min(1f, painted) : Mathf.Min(3f, decor * 0.5f);

            int stains = MadMax.World.BloodStains.Count(p, 5f);
            int bodies = 0;
            foreach (var n in MadMax.Npc.Npc.All) if (n && !n.Alive && (n.transform.position - p).sqrMagnitude < 64f) bodies++;
            score -= Mathf.Min(3f, stains * 0.5f) + bodies * 2f;

            score = Mathf.Clamp(score, 0f, Max);
            note = (roof ? "ROOF" : "OPEN SKY") + (walls >= 5 ? ", WALLS" : "") + ", " + seen.Count + " PIECE" + (seen.Count == 1 ? "" : "S") + (stains + bodies > 0 ? ", GRIM" : "");
            return score;
        }

        /// <summary>A wall or roof along the ray: built structure pieces, buildings, bunkers, the home vehicle's shell —
        /// not furniture and not parked cars.</summary>
        static bool Shelter(Vector3 from, Vector3 dir, float range, Rigidbody home)
        {
            int n = Physics.RaycastNonAlloc(from, dir, hits, range, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.rigidbody && h.rigidbody != home) continue;
                var pl = h.collider.GetComponentInParent<Placeable>();
                if (pl) { var def = FurnitureLibrary.Get(pl.id); if (def != null && def.category != BuildCategory.Structure) continue; }
                if (h.collider.GetComponentInParent<MadMax.Game.PlayerCharacter>()) continue;
                return true;
            }
            return false;
        }

        /// <summary>The piece is seen from the spot (nothing but itself in the way).</summary>
        static bool InRoom(Vector3 eye, Placeable piece)
        {
            var target = piece.transform.position + piece.transform.up * 0.3f;
            var d = target - eye;
            float len = d.magnitude;
            if (len < 0.3f) return true;
            // walls in between hide the piece (other furniture does not)
            int n = Physics.RaycastNonAlloc(eye, d / len, hits, len - 0.25f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var t = hits[i].collider.transform;
                if (t == piece.transform || t.IsChildOf(piece.transform)) continue;
                var pl = hits[i].collider.GetComponentInParent<Placeable>();
                var def = pl ? FurnitureLibrary.Get(pl.id) : null;
                if (pl && def != null && def.category != BuildCategory.Structure) continue;
                if (hits[i].rigidbody || hits[i].collider.GetComponentInParent<MadMax.Game.PlayerCharacter>()) continue;
                return false;
            }
            return true;
        }

        public static string Word(float c) => c >= 8f ? "LUXURIOUS" : c >= 6f ? "COSY" : c >= 3f ? "DECENT" : c >= 1f ? "BARE" : "MISERABLE";
    }
}
