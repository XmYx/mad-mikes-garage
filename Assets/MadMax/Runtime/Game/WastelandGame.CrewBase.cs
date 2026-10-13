using System.Collections.Generic;
using MadMax.Building;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Check-ins carry the base (2026-10-13): a companion holding a post at home reads the home ledger into the
    /// dusk report ("THE FORGE IS OUT OF POWER, FOOD FOR TWO DAYS"), and station calls from a claim with a companion on
    /// post come in their voice instead of the bare HOME FREQUENCY line.</summary>
    public partial class WastelandGame
    {
        /// <summary>Below these shares the crew mentions batteries / water; pantry days below <see cref="PantryShortDays"/>.</summary>
        public const float BatteryLowShare = 0.2f, WaterLowShare = 0.15f, PantryShortDays = 3f;

        /// <summary>What is wrong (or done) inside the claim at <paramref name="at"/>, most urgent first, as a companion
        /// would say it. Empty when nothing needs the player.</summary>
        public void HomeWorries(Vector3 at, List<string> into)
        {
            into.Clear();
            var claim = ClaimFlag.Near(at);
            if (!claim) return;
            string stalled = null, ready = null; int nStalled = 0, nReady = 0;
            foreach (var st in CraftingStation.All)
            {
                if (!st || !claim.Inside(st.transform.position)) continue;
                int s = StationState(st);
                if (s == 2) { nStalled++; stalled = st.title; }
                else if (s == 3) { nReady++; ready = st.title; }
            }
            if (nStalled > 0) into.Add(nStalled == 1 ? "THE " + stalled + " IS OUT OF POWER" : nStalled + " STATIONS ARE OUT OF POWER");
            float wh = 0f, whMax = 0f, water = 0f, waterMax = 0f;
            var counted = new HashSet<int>();
            foreach (var n in UtilityNode.All)
            {
                if (!n || !claim.Inside(n.transform.position)) continue;
                if ((n.kinds & UtilityKind.Power) != 0) { wh += n.batteryCharge; whMax += n.batteryWh; }
                if ((n.kinds & UtilityKind.Water) != 0 && counted.Add(n.waterNet < 0 ? -1 - UtilityNode.All.IndexOf(n) : n.waterNet))
                { water += UtilityGrid.NetWater(n, out _); waterMax += UtilityGrid.NetCapacity(n); }
            }
            if (whMax > 0f && wh < whMax * BatteryLowShare) into.Add("THE BATTERIES ARE NEARLY FLAT");
            if (waterMax > 0f && water < waterMax * WaterLowShare) into.Add("WATER'S NEARLY OUT");
            Pantry(PantryShortDays, out float keeps, out float rots);
            if (keeps + rots < PantryShortDays) into.Add(keeps + rots < 1f ? "WE'RE OUT OF FOOD" : (keeps + rots < 2f ? "FOOD FOR A DAY OR SO" : "FOOD FOR A COUPLE OF DAYS"));
            int worn = BaseUpkeep.WornIn(claim);
            if (worn > 2) into.Add(worn + " PIECES NEED PATCHING");
            if (nReady > 0) into.Add(nReady == 1 ? "THE " + ready + " IS DONE" : nReady + " STATIONS ARE DONE");
        }

        /// <summary>The companion on post inside the claim around <paramref name="at"/> who answers the home frequency
        /// (null: the bare station call goes out).</summary>
        public static MadMax.Npc.Npc HomeVoice(Vector3 at)
        {
            var claim = ClaimFlag.Near(at);
            if (!claim) return null;
            foreach (var n in MadMax.Npc.Companions.OnRadio())
                if (n.order != 0 && !n.Driving && !n.Riding && claim.Inside(n.transform.position)) return n;
            return null;
        }
    }
}
