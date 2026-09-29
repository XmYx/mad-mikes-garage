using System.Collections.Generic;
using MadMax.Building;
using MadMax.Net;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>The garage as the fleet's home: a vehicle parked on a claim within reach of a built GARAGE is looked
    /// after every in-game hour (parts mended, dents beaten out, frame straightened, a little at a time) and topped up
    /// from the claim's built fuel pumps; home vehicles come first in the Tab cycle. Raiders go for them too
    /// (<see cref="MadMax.Npc.BaseRaid"/>). Authority only for the upkeep.</summary>
    public partial class WastelandGame
    {
        const float GarageReach = 14f;
        float garageHour = -1f;

        /// <summary>The garage looking after <paramref name="v"/> (parked on a claim near one), or null.</summary>
        public static CraftingStation HomeGarage(VehicleDriver v)
        {
            if (!v) return null;
            var at = v.transform.position;
            var claim = ClaimFlag.Near(at);
            if (!claim) return null;
            foreach (var st in CraftingStation.All)
                if (st && st.type == "garage" && claim.Inside(st.transform.position) && (st.transform.position - at).sqrMagnitude < GarageReach * GarageReach) return st;
            return null;
        }

        void UpdateGarage()
        {
            float hour = Mathf.Floor(DayNight.TotalDays * 24f);
            if (garageHour < 0f || hour < garageHour) { garageHour = hour; return; }
            if (hour <= garageHour || (NetSession.Instance && NetSession.Instance.IsClient)) return;
            float hours = Mathf.Min(24f, hour - garageHour);
            garageHour = hour;
            foreach (var v in fleet)
            {
                if (!v || v == Current || v.Occupied || v.aiDriven || v.Body.linearVelocity.sqrMagnitude > 0.25f) continue;
                var garage = HomeGarage(v);
                if (!garage) continue;
                // mend: a slow shop job, not a welder in the hand
                var chassis = v.GetComponent<VehicleChassis>();
                if (chassis) foreach (var p in chassis.Parts) if (p) p.damage = Mathf.Max(0f, p.damage - 0.03f * hours);
                if (v.TryGetComponent<VehicleDamage>(out var dmg) && dmg.FrameDamage > 0f) dmg.StraightenFrame(0.02f * hours);
                foreach (var dm in v.GetComponentsInChildren<DeformableMesh>()) if (dm.IsDamaged) dm.RepairPartial(Mathf.Min(1f, 0.04f * hours));
                // fuel from the claim's own pumps
                if (v.TryGetComponent<VehicleSystems>(out var sys) && sys.fuelCapacity > 0f && sys.fuel < sys.fuelCapacity - 1f)
                    foreach (var pump in GasPump.All)
                    {
                        if (!pump || pump.key != null || pump.Fuel < 0.5f || (pump.transform.position - garage.transform.position).sqrMagnitude > 25f * 25f) continue;
                        if (!sys.Accepts(pump.kind) || (pump.kind == MadMax.Items.ResourceType.Diesel) != (sys.FuelKind == MadMax.Items.ResourceType.Diesel)) continue;
                        float want = Mathf.Min(20f * hours, sys.fuelCapacity - sys.fuel);
                        float got = pump.Take(want);
                        if (got > 0f) sys.AddFuel(pump.kind, got);
                        break;
                    }
            }
        }

        /// <summary>Tab: the next fleet vehicle, those at home in a garage first.</summary>
        VehicleDriver NextFleet()
        {
            var order = new List<VehicleDriver>();
            foreach (var v in fleet) if (v && HomeGarage(v)) order.Add(v);
            foreach (var v in fleet) if (v && !order.Contains(v)) order.Add(v);
            if (order.Count == 0) return null;
            int i = order.IndexOf(Current);
            return order[(i + 1) % order.Count];
        }
    }
}
