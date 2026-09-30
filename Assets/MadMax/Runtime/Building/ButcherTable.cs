using System.Collections.Generic;
using MadMax.Animals;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Butchering table (depth stage E): a carcass within reach of it is butchered with the table's cleaver and
    /// hooks — the best cut of every drop, 40 % more, the whole hide, no blood on the ground (<see cref="Carcass"/>).
    /// [E] at the table butchers the nearest carcass; drag small ones over with [T].</summary>
    public class ButcherTable : MonoBehaviour, IInteractable
    {
        public static readonly List<ButcherTable> All = new List<ButcherTable>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();

        public const float Reach = 3.5f, Yield = 1.4f;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>The table within reach of <paramref name="p"/>, or null.</summary>
        public static ButcherTable Near(Vector3 p)
        {
            ButcherTable best = null; float bd = Reach * Reach;
            foreach (var t in All)
            {
                if (!t) continue;
                float d = (t.transform.position - p).sqrMagnitude;
                if (d < bd) { bd = d; best = t; }
            }
            return best;
        }

        Carcass NearestCarcass()
        {
            Carcass best = null; float bd = Reach * Reach;
            foreach (var c in Carcass.All)
            {
                if (!c || c.butchered || c.def == null) continue;
                float d = (c.transform.position - transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            var c = NearestCarcass();
            return c ? "[E] BUTCHER THE " + c.def.name + " ON THE TABLE" : "BUTCHERING TABLE: BRING A CARCASS WITHIN " + Reach + " M (DRAG SMALL ONES OVER)";
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary) return;
            var c = NearestCarcass();
            if (c) c.Use(g, false);
        }
    }
}
