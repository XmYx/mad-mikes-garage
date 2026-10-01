using System.Globalization;
using System.Text;
using UnityEngine;

namespace MadMax.Items
{
    /// <summary>What kind of liquid a component is: fuels burn, lubricants oil, aqueous liquids cool or quench.</summary>
    public enum FluidFamily { None, Fuel, Lube, Aqueous, Other }

    /// <summary>A blend of liquids by volume fraction (indexed by <see cref="ResourceType"/>; the fractions sum to 1, all
    /// zero = empty). Tanks, sumps, radiators and hand containers keep one each; pouring blends by volume
    /// (<see cref="Blend(float, FluidMix, float)"/>), draining takes every component in proportion (the fractions stay).
    /// <see cref="Version"/> changes on every edit so readers can cache derived values (engine blend effects).</summary>
    public sealed class FluidMix
    {
        readonly float[] f = new float[ResourceInfo.Count];
        public int Version { get; private set; }
        public bool Empty { get; private set; } = true;

        public FluidMix() { }
        public FluidMix(ResourceType pure) { Set(pure); }

        public float this[ResourceType t] => (int)t < f.Length ? f[(int)t] : 0f;

        public void Clear()
        {
            if (Empty) return;
            System.Array.Clear(f, 0, f.Length);
            Empty = true; Version++;
        }

        /// <summary>Pure <paramref name="t"/> (None = empty).</summary>
        public void Set(ResourceType t)
        {
            System.Array.Clear(f, 0, f.Length);
            Empty = t == ResourceType.None;
            if (!Empty) f[(int)t] = 1f;
            Version++;
        }

        public void CopyFrom(FluidMix o)
        {
            if (o == null) { Clear(); return; }
            System.Array.Copy(o.f, f, f.Length);
            Empty = o.Empty; Version++;
        }

        public FluidMix Clone() { var m = new FluidMix(); m.CopyFrom(this); return m; }

        /// <summary>Pour <paramref name="addLitres"/> of <paramref name="add"/> into <paramref name="haveLitres"/> of this.</summary>
        public void Blend(float haveLitres, FluidMix add, float addLitres)
        {
            if (add == null || add.Empty || addLitres <= 0f) return;
            if (Empty || haveLitres <= 1e-4f) { CopyFrom(add); return; }
            float total = haveLitres + addLitres;
            for (int i = 0; i < f.Length; i++) f[i] = (f[i] * haveLitres + add.f[i] * addLitres) / total;
            Normalise();
        }

        public void Blend(float haveLitres, ResourceType t, float addLitres)
        {
            if (t == ResourceType.None || addLitres <= 0f) return;
            if (Empty || haveLitres <= 1e-4f) { Set(t); return; }
            float total = haveLitres + addLitres;
            for (int i = 0; i < f.Length; i++) f[i] = f[i] * haveLitres / total;
            f[(int)t] += addLitres / total;
            Normalise();
        }

        void Normalise()
        {
            float s = 0f;
            for (int i = 0; i < f.Length; i++) { if (f[i] < 0.0005f) f[i] = 0f; s += f[i]; }
            Empty = s <= 0f;
            if (!Empty) for (int i = 0; i < f.Length; i++) f[i] /= s;
            Version++;
        }

        /// <summary>The largest component (None when empty).</summary>
        public ResourceType Main
        {
            get
            {
                if (Empty) return ResourceType.None;
                int best = 0;
                for (int i = 1; i < f.Length; i++) if (f[i] > f[best]) best = i;
                return (ResourceType)best;
            }
        }

        public float Purity => Empty ? 0f : f[(int)Main];
        /// <summary>98 %+ one liquid: it counts as that resource in the pack.</summary>
        public bool IsPure => Purity >= 0.98f;

        /// <summary>Sum of the fractions in <paramref name="family"/>.</summary>
        public float Of(FluidFamily family)
        {
            float s = 0f;
            for (int i = 1; i < f.Length; i++) if (f[i] > 0f && Family((ResourceType)i) == family) s += f[i];
            return s;
        }

        /// <summary>The family of the blend (its largest family).</summary>
        public FluidFamily MainFamily
        {
            get
            {
                if (Empty) return FluidFamily.None;
                float fu = Of(FluidFamily.Fuel), lu = Of(FluidFamily.Lube), aq = Of(FluidFamily.Aqueous), ot = Of(FluidFamily.Other);
                return fu >= lu && fu >= aq && fu >= ot ? FluidFamily.Fuel : lu >= aq && lu >= ot ? FluidFamily.Lube : aq >= ot ? FluidFamily.Aqueous : FluidFamily.Other;
            }
        }

        public static FluidFamily Family(ResourceType t)
        {
            switch (t)
            {
                case ResourceType.Fuel: case ResourceType.Diesel: case ResourceType.Ethanol: case ResourceType.CrudeOil: return FluidFamily.Fuel;
                case ResourceType.Oil: case ResourceType.SeedOil: return FluidFamily.Lube;
                case ResourceType.Water: case ResourceType.DirtyWater: case ResourceType.SeaWater: case ResourceType.Coolant: return FluidFamily.Aqueous;
                case ResourceType.None: return FluidFamily.None;
                default: return ResourceInfo.IsFluid(t) ? FluidFamily.Other : FluidFamily.None;
            }
        }

        /// <summary>Same liquids within <paramref name="tolerance"/> per component.</summary>
        public bool Like(FluidMix o, float tolerance = 0.02f)
        {
            if (o == null || Empty != o.Empty) return false;
            for (int i = 0; i < f.Length; i++) if (Mathf.Abs(f[i] - o.f[i]) > tolerance) return false;
            return true;
        }

        /// <summary>Short HUD name: "DIESEL", "E85", "2-STROKE MIX 4%", "DIESEL 85% PETROL 15%"; "EMPTY".</summary>
        public string Label()
        {
            if (Empty) return "EMPTY";
            var main = Main;
            if (IsPure) return ResourceInfo.Name(main);
            float fu = this[ResourceType.Fuel], et = this[ResourceType.Ethanol], oil = this[ResourceType.Oil];
            if (fu + et > 0.98f && et >= 0.05f) return "E" + Mathf.RoundToInt(et * 100f);
            if (fu + oil > 0.98f && oil > 0.005f && oil <= 0.1f) return "2-STROKE MIX " + Pct(oil);
            var sb = new StringBuilder();
            int p1 = -1, p2 = -1;
            for (int pass = 0; pass < 3; pass++)
            {
                int best = -1;
                for (int i = 1; i < f.Length; i++)
                {
                    if (f[i] < 0.005f || i == p1 || i == p2) continue;
                    if (best < 0 || f[i] > f[best]) best = i;
                }
                if (best < 0) break;
                if (pass == 0) p1 = best; else p2 = best;
                if (pass > 0) sb.Append(' ');
                sb.Append(ResourceInfo.Name((ResourceType)best)).Append(' ').Append(Pct(f[best]));
            }
            return sb.ToString();
        }

        static string Pct(float x) => (x < 0.095f ? (x * 100f).ToString("0.#", CultureInfo.InvariantCulture) : Mathf.RoundToInt(x * 100f).ToString()) + "%";

        /// <summary>"36:0.85;7:0.15" (resource index : fraction); "" when empty.</summary>
        public string Save()
        {
            if (Empty) return "";
            var sb = new StringBuilder();
            for (int i = 1; i < f.Length; i++)
            {
                if (f[i] <= 0f) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(i).Append(':').Append(f[i].ToString("0.####", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>Restore from <see cref="Save"/>; false (and empty) on bad input.</summary>
        public bool Load(string s)
        {
            System.Array.Clear(f, 0, f.Length);
            Empty = true; Version++;
            if (string.IsNullOrEmpty(s)) return true;
            foreach (var part in s.Split(';'))
            {
                var kv = part.Split(':');
                if (kv.Length != 2 || !int.TryParse(kv[0], out int i) || i <= 0 || i >= f.Length) continue;
                if (float.TryParse(kv[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) && x > 0f) f[i] = x;
            }
            Normalise();
            return !Empty;
        }

        public override string ToString() => Label();
    }
}
