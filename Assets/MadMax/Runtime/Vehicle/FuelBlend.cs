using MadMax.Items;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>What an engine burns natively.</summary>
    public enum EngineFuel { Petrol, Diesel, TwoStroke }

    /// <summary>How a fuel blend runs in an engine (multipliers on the healthy engine; see <see cref="FuelBlend"/>).</summary>
    public struct BlendEffect
    {
        /// <summary>False: won't start, stalls if running (<see cref="Fault.WrongFuel"/>).</summary>
        public bool runs;
        /// <summary>Rough band: runs, but badly (<see cref="Fault.RoughFuel"/>).</summary>
        public bool rough;
        /// <summary>Torque multiplier, burn (litres) multiplier.</summary>
        public float power, burn;
        /// <summary>0..1 misfire strength, extra exhaust darkness, engine damage per running second, start chance penalty.</summary>
        public float misfire, smoke, wear, start;
        /// <summary>Engine damage per failed crank on a blend that won't run (injection pump, hydro-lock).</summary>
        public float crankWear;
        /// <summary>The worst contaminant (for the fault text).</summary>
        public ResourceType worst;

        public static BlendEffect Clean => new BlendEffect { runs = true, power = 1f, burn = 1f };
    }

    /// <summary>The fuel mixing model (roadmap "Fluids"). A tank holds a blend by volume; each foreign component is
    /// rated against the engine by a row of <see cref="Rows"/>:
    /// <list type="bullet">
    /// <item>up to <c>ok</c>: runs; power falls linearly to <c>power</c>, burn rises by <c>burnPerFrac</c> × fraction,
    /// misfire / smoke / wear / start penalty grow linearly to their "at ok" values;</item>
    /// <item>between <c>ok</c> and <c>dead</c>: rough — power slides from <c>power</c> to 0.5, misfire to 0.7, smoke to at
    /// least 0.6, wear adds up to 0.002 /s, start penalty to 0.5;</item>
    /// <item>above <c>dead</c> (or when the contaminants together pass their limits, Σ x/dead &gt; 1): won't run.</item>
    /// </list>
    /// Water, dirty water, sea water and coolant count together as "water" (sea water wears 3×, coolant 2×).
    /// <code>
    /// engine   in it          ok    dead   power@ok burn/frac misfire smoke wear/s   start crank
    /// petrol   ethanol        1.00  -      0.95     +0.35     0       0     0        0.15  0      E85: -4 % power, +30 % burn
    /// petrol   diesel         0.10  0.25   0.90     0         0.10    0.35  0        0.10  0.005
    /// petrol   water          0.10  0.10   0.85     +1.5      0.30    0.10  0.0004   0.15  0.02   ≤10 % runs, costs fuel, misfires
    /// petrol   oil            0.04  0.20   0.97     0         0.02    0.50  0        0.05  0      four-strokes smoke on 2T mix
    /// petrol   seed oil       0.03  0.15   0.95     0         0.05    0.50  0        0.10  0
    /// petrol   crude          0.02  0.12   0.90     0         0.10    0.60  0.0002   0.15  0.005
    /// 2-stroke oil            0.08  0.25   1.00     0         0       0.15  0        0     0      the proper 2T mix
    /// 2-stroke (otherwise as petrol; pure petrol counts as pre-mixed at the pump)
    /// diesel   petrol         0.15  0.35   0.94     0         0.08    0.10  0.0001   0     0.02   ≤15 % fine, &gt;35 % dead
    /// diesel   ethanol        0.05  0.20   0.95     0         0.10    0.10  0.0001   0.10  0.01
    /// diesel   water          0.10  0.10   0.85     +1.5      0.30    0.10  0.0004   0.15  0.02
    /// diesel   oil            0.15  0.50   0.97     0         0.02    0.40  0        0.10  0
    /// diesel   seed oil       0.30  0.80   0.95     +0.10     0.02    0.30  0        0.15  0      veg oil runs a diesel
    /// diesel   crude          0.05  0.30   0.92     0         0.10    0.70  0.0003   0.20  0.01
    /// any      anything else  0.01  0.05   0.90     0         0.20    0.50  0.0005   0.20  0.01
    /// </code>
    /// Other systems: oil protection = 1 − 5·water − 2.5·fuel − 1.5·crude − 0.6·seed oil (below 0.95 the oil ages faster
    /// and the engine wears; below 0.8 = <see cref="Fault.BadOil"/>). Coolant: the COOLANT resource is a ready mix rated
    /// to −37 °C, so the freezing point is −37 °C × its fraction (pure water 0 °C); below it a parked engine's coolant is
    /// <see cref="Fault.Frozen"/> (won't start; the radiator cracks slowly 5 °C under it); fuel or oil in the coolant
    /// cuts cooling, sea water corrodes the radiator.</summary>
    public static class FuelBlend
    {
        struct Row
        {
            public EngineFuel engine; public ResourceType what;
            public float ok, dead, power, burnPerFrac, misfire, smoke, wear, start, crank;
            public Row(EngineFuel e, ResourceType t, float ok, float dead, float power, float burn, float misfire, float smoke, float wear, float start, float crank)
            { engine = e; what = t; this.ok = ok; this.dead = dead; this.power = power; burnPerFrac = burn; this.misfire = misfire; this.smoke = smoke; this.wear = wear; this.start = start; this.crank = crank; }
        }

        const ResourceType Water = ResourceType.Water;      // stands for the whole aqueous group in the table
        static readonly Row[] Rows =
        {
            new Row(EngineFuel.Petrol, ResourceType.Ethanol, 1.00f, 9f,   0.95f, 0.35f, 0f,    0f,    0f,      0.15f, 0f),
            new Row(EngineFuel.Petrol, ResourceType.Diesel,  0.10f, 0.25f, 0.90f, 0f,    0.10f, 0.35f, 0f,      0.10f, 0.005f),
            new Row(EngineFuel.Petrol, Water,                0.10f, 0.10f, 0.85f, 1.5f,  0.30f, 0.10f, 0.0004f, 0.15f, 0.02f),
            new Row(EngineFuel.Petrol, ResourceType.Oil,     0.04f, 0.20f, 0.97f, 0f,    0.02f, 0.50f, 0f,      0.05f, 0f),
            new Row(EngineFuel.Petrol, ResourceType.SeedOil, 0.03f, 0.15f, 0.95f, 0f,    0.05f, 0.50f, 0f,      0.10f, 0f),
            new Row(EngineFuel.Petrol, ResourceType.CrudeOil, 0.02f, 0.12f, 0.90f, 0f,   0.10f, 0.60f, 0.0002f, 0.15f, 0.005f),
            new Row(EngineFuel.TwoStroke, ResourceType.Oil,  0.08f, 0.25f, 1.00f, 0f,    0f,    0.15f, 0f,      0f,    0f),
            new Row(EngineFuel.Diesel, ResourceType.Fuel,    0.15f, 0.35f, 0.94f, 0f,    0.08f, 0.10f, 0.0001f, 0f,    0.02f),
            new Row(EngineFuel.Diesel, ResourceType.Ethanol, 0.05f, 0.20f, 0.95f, 0f,    0.10f, 0.10f, 0.0001f, 0.10f, 0.01f),
            new Row(EngineFuel.Diesel, Water,                0.10f, 0.10f, 0.85f, 1.5f,  0.30f, 0.10f, 0.0004f, 0.15f, 0.02f),
            new Row(EngineFuel.Diesel, ResourceType.Oil,     0.15f, 0.50f, 0.97f, 0f,    0.02f, 0.40f, 0f,      0.10f, 0f),
            new Row(EngineFuel.Diesel, ResourceType.SeedOil, 0.30f, 0.80f, 0.95f, 0.10f, 0.02f, 0.30f, 0f,      0.15f, 0f),
            new Row(EngineFuel.Diesel, ResourceType.CrudeOil, 0.05f, 0.30f, 0.92f, 0f,   0.10f, 0.70f, 0.0003f, 0.20f, 0.01f),
        };
        static readonly Row Unknown = new Row(EngineFuel.Petrol, ResourceType.None, 0.01f, 0.05f, 0.90f, 0f, 0.20f, 0.50f, 0.0005f, 0.20f, 0.01f);

        static bool Aqueous(ResourceType t) => t == ResourceType.Water || t == ResourceType.DirtyWater || t == ResourceType.SeaWater || t == ResourceType.Coolant;

        static bool Native(EngineFuel e, ResourceType t) => e == EngineFuel.Diesel ? t == ResourceType.Diesel : t == ResourceType.Fuel;

        static Row Find(EngineFuel e, ResourceType t)
        {
            if (e == EngineFuel.TwoStroke)
            {
                foreach (var r in Rows) if (r.engine == EngineFuel.TwoStroke && r.what == t) return r;
                e = EngineFuel.Petrol;
            }
            foreach (var r in Rows) if (r.engine == e && r.what == t) return r;
            return Unknown;
        }

        /// <summary>How <paramref name="mix"/> runs in an <paramref name="engine"/> engine (an empty mix runs clean: it is
        /// the engine's own fuel by default).</summary>
        public static BlendEffect Evaluate(FluidMix mix, EngineFuel engine)
        {
            var e = BlendEffect.Clean;
            if (mix == null || mix.Empty) return e;
            float severity = 0f, worstSev = 0f;
            float water = 0f, waterWear = 0f;
            for (int i = 1; i < ResourceInfo.Count; i++)
            {
                var t = (ResourceType)i;
                float x = mix[t];
                if (x <= 0f || Native(engine, t)) continue;
                if (Aqueous(t)) { water += x; waterWear += x * (t == ResourceType.SeaWater ? 3f : t == ResourceType.Coolant ? 2f : 1f); continue; }
                Apply(ref e, Find(engine, t), x, 1f, ref severity, ref worstSev, t);
            }
            if (water > 0f) Apply(ref e, Find(engine, Water), water, waterWear / water, ref severity, ref worstSev, ResourceType.Water);
            if (severity > 1f) e.runs = false;
            e.power = Mathf.Clamp01(e.power);
            e.misfire = Mathf.Clamp01(e.misfire);
            e.smoke = Mathf.Clamp01(e.smoke);
            e.start = Mathf.Clamp01(e.start);
            return e;
        }

        static void Apply(ref BlendEffect e, Row r, float x, float wearMult, ref float severity, ref float worstSev, ResourceType t)
        {
            float sev = r.dead < 5f ? x / r.dead : 0f;
            severity += sev;
            if (sev > worstSev || e.worst == ResourceType.None) { worstSev = sev; e.worst = t; }
            if (x > r.dead + 1e-4f) { e.runs = false; e.crankWear += r.crank; return; }
            float power, misfire, smoke, wear, start;
            if (x <= r.ok + 1e-4f)
            {
                float s = r.ok > 0f ? Mathf.Clamp01(x / r.ok) : 1f;
                power = 1f - (1f - r.power) * s; misfire = r.misfire * s; smoke = r.smoke * s; wear = r.wear * s; start = r.start * s;
            }
            else
            {
                float s = Mathf.Clamp01((x - r.ok) / Mathf.Max(1e-4f, r.dead - r.ok));
                e.rough = true;
                power = Mathf.Lerp(r.power, 0.5f, s); misfire = Mathf.Lerp(Mathf.Max(r.misfire, 0.15f), 0.7f, s);
                smoke = Mathf.Lerp(r.smoke, Mathf.Max(r.smoke, 0.6f), s); wear = r.wear + 0.002f * s; start = Mathf.Lerp(r.start, 0.5f, s);
            }
            e.power *= power;
            e.burn *= 1f + r.burnPerFrac * x;
            e.misfire += misfire; e.smoke += smoke; e.wear += wear * wearMult; e.start += start;
        }

        /// <summary>0..1 how well a sump of <paramref name="oil"/> protects the engine (1 = clean oil).</summary>
        public static float OilProtection(FluidMix oil)
        {
            if (oil == null || oil.Empty) return 1f;
            float water = oil[ResourceType.Water] + oil[ResourceType.DirtyWater] + oil[ResourceType.SeaWater] + oil[ResourceType.Coolant];
            float fuel = oil[ResourceType.Fuel] + oil[ResourceType.Diesel] + oil[ResourceType.Ethanol];
            float other = 1f - water - fuel - oil[ResourceType.Oil] - oil[ResourceType.SeedOil] - oil[ResourceType.CrudeOil];
            return Mathf.Clamp01(1f - 5f * water - 2.5f * fuel - 1.5f * oil[ResourceType.CrudeOil] - 0.6f * oil[ResourceType.SeedOil] - 3f * Mathf.Max(0f, other));
        }

        /// <summary>Freezing point (°C) of the cooling system's blend: the COOLANT ready mix is rated to −37 °C.</summary>
        public static float FreezePoint(FluidMix coolant)
        {
            if (coolant == null || coolant.Empty) return -37f;
            return -37f * coolant[ResourceType.Coolant] - 2f * coolant[ResourceType.SeaWater];
        }

        /// <summary>0..1 cooling efficiency of the blend: fuel or oil in the radiator foul it.</summary>
        public static float CoolingFactor(FluidMix coolant)
        {
            if (coolant == null || coolant.Empty) return 1f;
            float foul = coolant.Of(FluidFamily.Fuel) + coolant.Of(FluidFamily.Lube) + coolant.Of(FluidFamily.Other);
            return Mathf.Clamp(1f - 2f * foul, 0.4f, 1f);
        }
    }
}
