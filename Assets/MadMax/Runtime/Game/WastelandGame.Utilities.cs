using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Depth stage F on the player's side: the water test kit (and what each piece tested last), filter
    /// cartridges from the hotbar, drinking tainted water (salty makes you thirstier, oily and foul water sicken,
    /// fallout burns), sea water at the shore, generator outages (a hint, a journal line) and a spoilage hook for
    /// single containers. Samples persist in <c>SaveData.blockUtilities</c> ("s:pieceId=VERDICT|day").</summary>
    public partial class WastelandGame
    {
        /// <summary>Last test per sampled piece: verdict and day, so a re-test tells what changed.</summary>
        readonly Dictionary<uint, (string verdict, int day)> waterSamples = new Dictionary<uint, (string, int)>();

        partial void UtilitiesSave(SaveData d)
        {
            d.blockUtilities.Clear();
            foreach (var kv in waterSamples) d.blockUtilities.Add("s:" + kv.Key + "=" + kv.Value.verdict + "|" + kv.Value.day);
        }

        partial void UtilitiesLoad(SaveData d)
        {
            waterSamples.Clear();
            if (d.blockUtilities == null) return;
            foreach (var e in d.blockUtilities)
            {
                if (!e.StartsWith("s:")) continue;
                int eq = e.IndexOf('='), bar = e.LastIndexOf('|');
                if (eq < 0 || bar < eq || !uint.TryParse(e.Substring(2, eq - 2), out uint id) || !int.TryParse(e.Substring(bar + 1), out int day)) continue;
                waterSamples[id] = (e.Substring(eq + 1, bar - eq - 1), day);
            }
        }

        partial void UtilitiesNewGame() => waterSamples.Clear();

        /// <summary>Hotbar use of the stage F consumables; false = not one of them.</summary>
        bool UseUtilityItem(string id)
        {
            if (id == UtilityIds.WaterTest) { TestWater(); return true; }
            if (id == UtilityIds.Cartridge)
            {
                FilterCartridge best = null; float bd = 3f;
                if (Player)
                    foreach (var n in UtilityNode.All)
                    {
                        if (!n || !n.TryGetComponent<FilterCartridge>(out var f)) continue;
                        float d = Vector3.Distance(n.transform.position, Player.transform.position);
                        if (d < bd) { bd = d; best = f; }
                    }
                if (best) best.Replace(this); else Toast("STAND AT A WATER FILTER TO SWAP ITS CARTRIDGE");
                return true;
            }
            return false;
        }

        /// <summary>The water test kit: samples the nearest water piece within 3 m (tank, well, tap, still), else open
        /// water in front, else the water in the pack. Returns the reading (also toasted; troubles go in the journal).</summary>
        public string TestWater()
        {
            if (!Player) return null;
            var at = Player.transform.position;
            UtilityNode best = null; float bd = 3f;
            foreach (var n in UtilityNode.All)
            {
                if (!n || (n.kinds & UtilityKind.Water) == 0 || n.waterCapacity <= 0f) continue;
                float d = Vector3.Distance(n.transform.position, at);
                if (d < bd) { bd = d; best = n; }
            }
            string reading, verdict;
            if (best)
            {
                reading = WaterQuality.Reading(best, out verdict, out var taint);
                uint pid = best.Id;
                int day = DayNight.Day + 1;
                if (pid != 0 && verdict != "EMPTY")
                {
                    if (waterSamples.TryGetValue(pid, out var was) && was.verdict != verdict) reading += "  (WAS " + was.verdict + " ON DAY " + was.day + ")";
                    waterSamples[pid] = (verdict, day);
                }
                if (taint != WaterTaint.None)
                {
                    var def = best.Piece ? FurnitureLibrary.Get(best.Piece.id) : null;
                    WaterQuality.GroundTaint(best.transform.position, out string cause);
                    Journal.Add("WATER", (def != null ? def.name : "WATER") + " " + WaterQuality.Words(taint) + (cause != null && best.GetComponent<HandPump>() ? " (" + cause + ")" : "") + ": " + WaterQuality.Advice(taint));
                }
            }
            else
            {
                var p = at + Player.transform.forward * 0.8f;
                if (terrain && terrain.WaterDepth(p.x, p.z) > 0.1f) { reading = WaterQuality.OpenWaterReading(p); verdict = reading.Contains("SALTY") ? "SALTY" : reading.Contains("TOXIC") ? "TOXIC" : "DIRTY"; }
                else
                {
                    int c = Inventory.Get(ResourceType.Water), dw = Inventory.Get(ResourceType.DirtyWater), sw = Inventory.Get(ResourceType.SeaWater);
                    verdict = dw + sw > 0 ? "DIRTY" : c > 0 ? "CLEAN" : "EMPTY";
                    reading = c + dw + sw == 0 ? "NOTHING TO SAMPLE: STAND BY A TANK, WELL, TAP OR OPEN WATER"
                        : "WATER TEST (PACK): " + c + " L CLEAN" + (dw > 0 ? ", " + dw + " L DIRTY" : "") + (sw > 0 ? ", " + sw + " L SALTY" : "");
                }
            }
            MadMax.Audio.Sfx.Play("pour", at, 0.3f, 1.6f);
            Stats.Practice(Skill.Survival, 0.5f);
            MadMax.Story.Story.Note("water_test:" + verdict.ToLowerInvariant());
            Toast(reading);
            return reading;
        }

        /// <summary>Drink water carrying <paramref name="taint"/>: salt makes you thirstier, oil and sewage sicken more
        /// often than plain dirty water, fallout burns.</summary>
        public void DrinkTainted(float thirst, WaterTaint taint)
        {
            if (taint == WaterTaint.None) { Drink(thirst, false); return; }
            if ((taint & WaterTaint.Salt) != 0 && (taint & (WaterTaint.Oil | WaterTaint.Sewage | WaterTaint.Toxic)) == 0)
            {
                Stats.thirst = Mathf.Max(0f, Stats.thirst - thirst * 0.25f);
                Toast("SALT WATER: IT ONLY MAKES YOU THIRSTIER");
                return;
            }
            float survival = Stats.Level(Skill.Survival) * 0.015f;
            if ((taint & WaterTaint.Oil) != 0)
            {
                Stats.thirst = Mathf.Min(100f, Stats.thirst + thirst * 0.5f);
                if (Random.value < 0.6f - survival) Poison("IT TASTES OF DIESEL"); else Toast("OILY WATER: IT TASTES OF DIESEL");
            }
            else if ((taint & WaterTaint.Sewage) != 0)
            {
                Stats.thirst = Mathf.Min(100f, Stats.thirst + thirst);
                if (Random.value < 0.5f - survival) Poison("FOUL WATER"); else Toast("DRANK FOUL WATER");
            }
            else Drink(thirst, true);
            if ((taint & WaterTaint.Toxic) != 0 && Vitals) Vitals.Hurt(5f, "TOXIC WATER");
        }

        /// <summary>At the sea's edge (called from the lake interaction): [E] drinks salt water, [T] fills 5 L of sea water.</summary>
        bool SeaInteraction(Vector3 p, bool E, bool T, out string prompt)
        {
            prompt = null;
            if (!WaterQuality.IsSea(p.x, p.z)) return false;
            if (E) DrinkTainted(30f, WaterTaint.Salt);
            if (T) { Inventory.Add(ResourceType.SeaWater, 5); Toast("FILLED 5L SEA WATER: DISTIL IT BEFORE DRINKING"); }
            prompt = "[E] DRINK (SALT WATER)  [T] FILL 5L SEA WATER";
            return true;
        }

        /// <summary>A generator stalled on overload: a toast and hint near it, a journal line.</summary>
        public void OnGeneratorStall(Generator gen)
        {
            if (!gen) return;
            MadMax.Story.Story.Note("generator_stalled");
            var me = Current ? Current.transform.position : Player ? Player.transform.position : gen.transform.position;
            if (Vector3.Distance(me, gen.transform.position) > 60f) return;
            Toast("GENERATOR STALLED: MORE LOAD THAN IT CAN CARRY");
            Hints.Show("outage", "OUTAGE: SWITCH LOADS OFF OR PUT LOW ONES BEHIND A LOAD BREAKER, THEN [E] RESTART THE GENERATOR", 2);
            Journal.Add("POWER", "A GENERATOR STALLED ON OVERLOAD: SHED LOAD (SWITCHES, LOAD BREAKERS) AND RESTART IT");
        }

        /// <summary>Let food in one container rot for <paramref name="seconds"/> at its own rate (tests, story events).</summary>
        public void SpoilContainer(Container c, float seconds)
        {
            if (c) SpoilIn(c.inventory, seconds, c.SpoilFactor);
        }
    }
}
