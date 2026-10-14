using System.Collections.Generic;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>The railway in the game (rail block): the director and the track streaming start once the world is
    /// ready; [F] at a loco's cab takes the controls (steals a makeshift train standing at a platform), [G] with a can
    /// of diesel fills its tank; in the cab W / S throttle and brake, Space emergency brake, R the reverser, Q / E set
    /// the next facing points ahead to the left / right, the horn key sounds the horn, J uncouples the last car, F
    /// climbs down. Rolling stock kits are put on the nearest track. Saved in <c>SaveData.blockRail</c>.</summary>
    public partial class WastelandGame
    {
        partial void RailUpdate();
        partial void RailSave(SaveData d);
        partial void RailLoad(SaveData d);
        partial void RailNewGame();

        public TrainDirector Rail { get; private set; }
        List<string> pendingRail;
        Train cabWas;

        /// <summary>The train whose cab the player sits in (null: none).</summary>
        public Train CabTrain
        {
            get
            {
                if (!Player || !Player.Sitting || !Player.SeatedOn) return null;
                var car = Player.SeatedOn.GetComponentInParent<RailCar>();
                return car && car.CabSeat == Player.SeatedOn ? car.train : null;
            }
        }

        partial void RailUpdate()
        {
            if (!Rail)
            {
                if (World == null || World.rail == null || !Ready) return;
                Rail = TrainDirector.Create(this, World.rail);
                RailTrack.Create(World.rail, World, Rail.Mat, Rail.Glow, World.seed);
                if (pendingRail != null) { Rail.Load(pendingRail); pendingRail = null; }
            }
            var cab = CabTrain;
            if (cabWas && cabWas != cab) { cabWas.driven = false; cabWas.throttle = 0f; cabWas.brake = 1f; }
            cabWas = cab;
            if (!cab) return;
            Hints.Show("rail_cab", Controls.Name(Controls.Act.Forward) + "/" + Controls.Name(Controls.Act.Back) + " THROTTLE / BRAKE  " + Controls.Name(Controls.Act.Jump) + " EMERGENCY  "
                + Controls.Name(Controls.Act.Reload) + " REVERSER  " + Controls.Name(Controls.Act.Drop) + "/" + Controls.Name(Controls.Act.Use) + " POINTS LEFT/RIGHT  " + Controls.Name(Controls.Act.Hitch) + " UNCOUPLE");
            if (Controls.Down(Controls.Act.Horn)) cab.Horn();
            if (Controls.Down(Controls.Act.Reload) && !cab.FlipReverser()) Toast("STOP BEFORE YOU FLIP THE REVERSER");
            bool left = Controls.Down(Controls.Act.Drop), right = Controls.Down(Controls.Act.Use);
            if (left || right) SetPointsAhead(cab, left ? -1 : 1);
            if (Controls.Down(Controls.Act.Hitch))
            {
                if (Mathf.Abs(cab.speed) > 0.5f) Toast("STOP TO UNCOUPLE");
                else { var t = cab.UncoupleLast(Rail.Mat, Rail.Glow); Toast(t ? "UNCOUPLED THE " + t.trainName : "NOTHING COUPLED"); }
            }
        }

        /// <summary>The next facing points ahead (within 400 m): set for the road going to that side.</summary>
        void SetPointsAhead(Train t, int side)
        {
            int sign = Mathf.Abs(t.speed) > 0.3f ? (t.speed > 0f ? 1 : -1) : t.PullSign;
            float from = sign > 0 ? t.head : t.head - t.Length;
            // look along what is planned, and plan further if the path is short
            for (int i = 0; i < 6 && (sign > 0 ? t.path.End < from + 400f : t.path.Start > from - 400f); i++) { if (sign > 0 ? !t.path.ExtendFront(World.rail) : !t.path.ExtendBack(World.rail)) break; }
            int node = t.path.NextFacingSwitch(World.rail, from, sign, 400f, out float at);
            if (node < 0) { Toast("NO POINTS AHEAD"); return; }
            var n = World.rail.nodes[node];
            int div = World.rail.DivergeSide(n);
            int state = div == side ? 1 : 0;
            RailSwitch.Throw(n, state);
            Toast("POINTS " + Mathf.RoundToInt(at) + " M AHEAD SET " + (state == 0 ? "STRAIGHT ON" : side < 0 ? "TO THE LEFT" : "TO THE RIGHT"));
        }

        /// <summary>The cab prompt while seated in one (null: not in a cab).</summary>
        string RailSeatPrompt() => CabTrain ? "[F] CLIMB DOWN" : null;

        /// <summary>On foot by a train: [F] at a cab, [G] to fuel a loco with the can in hand.</summary>
        string RailInteraction(bool F, ref bool G)
        {
            if (!Rail || !Player) return null;
            var p = Player.transform.position + Vector3.up;
            RailCar best = null; float bd = 3.2f;
            foreach (var t in Train.All)
                foreach (var c in t.cars)
                {
                    if (!c || !c.def.cab || (c.transform.position - p).sqrMagnitude > 400f) continue;
                    float d = Vector3.Distance(c.transform.TransformPoint(c.def.seat), p);
                    if (d < bd) { bd = d; best = c; }
                }
            if (!best) return null;
            var train = best.train;
            string name = train.trainName;
            if (train.service != null)
            {
                var s = train.service;
                if (s.armoured) return s.Hostile ? null : "THE " + name + "'S CREW KEEPS THE HATCH SHUT";
                if (s.AtStop < 0) return null;
                if (F)
                {
                    s.End(train, "YOU TAKE THE " + name + " - THE GUILD WILL HEAR OF IT");
                    MadMax.Npc.Factions.Shift(MadMax.Npc.Faction.FuelGuild, -15);
                    train.fuel = train.tank * 0.6f;
                    Player.SitOn(best.CabSeat, best.def.seat);
                }
                return "[F] TAKE OVER THE " + name + " (THEFT)";
            }
            string text = "[F] DRIVE THE " + name;
            if (best.def.skill >= 2 && Stats.Level(MadMax.RPG.Skill.Rail) < best.def.skill) text += " (" + MadMax.RPG.CharacterStats.SkillNames[(int)MadMax.RPG.Skill.Rail] + " " + best.def.skill + " NEEDED)";
            if (F) { Player.SitOn(best.CabSeat, best.def.seat); train.owned = true; }
            if (!best.def.hand)
            {
                var can = HeldCan;
                text += "   [G] FUEL " + Mathf.RoundToInt(train.fuel) + "/" + Mathf.RoundToInt(train.tank) + " L";
                if (G) { FuelLoco(train, can); G = false; }
            }
            return text;
        }

        /// <summary>Pour the can in hand into the loco's tank (diesel only).</summary>
        void FuelLoco(Train t, CanContents can)
        {
            if (can == null || can.Empty) { Toast("HOLD A CAN OF DIESEL"); return; }
            if (can.mix[ResourceType.Diesel] < 0.6f) { Toast("IT RUNS ON DIESEL - THAT SMELLS WRONG"); return; }
            float room = t.tank - t.fuel;
            if (room < 0.5f) { Toast("THE TANK IS FULL"); return; }
            float pour = Mathf.Min(room, can.litres);
            can.litres -= pour; if (can.litres < 0.01f) can.Clear();
            t.fuel += pour;
            MadMax.Audio.Sfx.Play("pour", Player.transform.position, 0.8f);
            Toast("POURED " + Mathf.RoundToInt(pour) + " L INTO THE " + t.trainName);
        }

        /// <summary>Rolling stock kits: put on the nearest track within 8 m.</summary>
        bool RailKitUse(string id)
        {
            CarKind kind;
            switch (id)
            {
                case "kit_rail_cart": kind = CarKind.Draisine; break;
                case "kit_rail_loco": kind = CarKind.MakeshiftLoco; break;
                case "kit_rail_flatcar": kind = CarKind.Flatcar; break;
                case "kit_rail_hopper": kind = CarKind.Hopper; break;
                case "kit_rail_shack": kind = CarKind.ShackWagon; break;
                default: return false;
            }
            if (!Rail) { Toast("NO RAILWAY HERE"); return true; }
            var l = World.rail.NearestTrack(Player.transform.position, 8f, out float s, out _);
            if (l == null) { Toast("STAND BY A TRACK TO PUT IT ON THE RAILS"); return true; }
            var at = l.At(s, out var tan);
            if (TrainDirector.Occupied(at, 4f)) { Toast("SOMETHING STANDS ON THE TRACK THERE"); return true; }
            bool fwd = Vector3.Dot(Player.transform.forward, tan) >= 0f;
            var t = Rail.SpawnFree(new[] { kind }, l, s, fwd, TrainDesigns.Get(kind).name, true, 0f);
            if (!t) { Toast("NOT ENOUGH TRACK THERE"); return true; }
            Inventory.TakeItem(id);
            MadMax.Audio.Sfx.Play("hammer", at, 0.8f);
            Toast("THE " + t.trainName + " IS ON THE RAILS");
            return true;
        }

        partial void RailSave(SaveData d)
        {
            d.blockRail = new List<string>();
            if (Rail) Rail.Save(d.blockRail);
            else if (pendingRail != null) d.blockRail.AddRange(pendingRail);
        }

        partial void RailLoad(SaveData d)
        {
            pendingRail = d.blockRail != null ? new List<string>(d.blockRail) : null;
            if (Rail && pendingRail != null) { Rail.ClearFree(); Rail.Load(pendingRail); pendingRail = null; }
        }

        partial void RailNewGame() { pendingRail = null; }
    }
}
