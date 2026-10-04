using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Bookshelf (with a Container): books and tapes stored on it make reading nearby faster
    /// (+4 % per volume, up to +40 %, within 6 m).</summary>
    public class Bookshelf : MonoBehaviour, IInteractable
    {
        public static readonly List<Bookshelf> All = new List<Bookshelf>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public int Volumes
        {
            get
            {
                if (!TryGetComponent<Container>(out var box)) return 0;
                int n = 0;
                foreach (var kv in box.inventory.Items) if (kv.Key.StartsWith("book_") || kv.Key.StartsWith("vhs_")) n += kv.Value;
                return n;
            }
        }

        public string Prompt(MadMax.Game.WastelandGame g) { int v = Volumes; return v + " VOLUME" + (v == 1 ? "" : "S") + (v > 0 ? " (A GOOD PLACE TO READ)" : ""); }
        public void Use(MadMax.Game.WastelandGame g, bool secondary) { }

        static float Bonus(int volumes) => Mathf.Min(0.4f, volumes * 0.04f);

        /// <summary>Reading speed multiplier at a spot (best shelf in reach).</summary>
        public static float ReadingBonus(Vector3 p)
        {
            float best = 0f;
            foreach (var s in All) if (s && (s.transform.position - p).sqrMagnitude < 36f) best = Mathf.Max(best, Bonus(s.Volumes));
            return 1f + best;
        }
    }
}
