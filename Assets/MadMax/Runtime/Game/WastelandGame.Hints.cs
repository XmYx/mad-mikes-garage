using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Context hints (what the keys do the first times you meet something), the bleed-out warning, and the stash
    /// you leave where you died (part of your pack by difficulty, searchable, saved until emptied).</summary>
    public partial class WastelandGame
    {
        float hintCheck, playedFor;

        /// <summary>Seconds until the open wounds bleed you out (-1 = not bleeding).</summary>
        public float BleedOutSeconds
        {
            get
            {
                float rate = 0f;
                foreach (var inj in Stats.injuries) rate += inj.BleedRate;
                return rate <= 0f ? -1f : Stats.health / rate;
            }
        }

        void UpdateHints()
        {
            playedFor += Time.deltaTime;
            if (Time.time < hintCheck || !Player || (Vitals && Vitals.Dead)) return;
            hintCheck = Time.time + 0.5f;
            string K(Controls.Act a) => Controls.Name(a);
            float bleed = BleedOutSeconds;
            if (bleed >= 0f && bleed < 90f && Hints.Show("bleed", "BLEEDING: " + K(Controls.Act.Health) + " HEALTH PAGE, BANDAGE THE WOUND", 5)) return;
            if (playedFor < 3f) return;
            if (Current)
            {
                var car = Current;
                if (car.GetComponent<FlightModel>()) Hints.Show("fly", K(Controls.Act.Forward) + "/" + K(Controls.Act.Back) + " THROTTLE LEVER  " + K(Controls.Act.Jump) + " PULL UP  " + K(Controls.Act.Crouch) + " PUSH DOWN  " + K(Controls.Act.Left) + "/" + K(Controls.Act.Right) + " BANK");
                else if (car.TryGetComponent<BikeBalance>(out var bike))
                {
                    if (bike.Pedals) Hints.Show("bicycle", "PEDAL: " + K(Controls.Act.Forward) + "  SPRINT: HOLD " + K(Controls.Act.Run) + " (STAMINA)");
                    else Hints.Show("bike", K(Controls.Act.Left) + "/" + K(Controls.Act.Right) + " LEAN INTO TURNS  " + K(Controls.Act.Run) + " WHEELIE  HIT SOMETHING HARD: THROWN OFF");
                }
                else if (car.GetComponent<Machine>()) Hints.Show("machine", "1 2 3 WORK THE TOOL  (SHIFT+1/2 SLEW)  " + K(Controls.Act.Enter) + " GET OUT");
                else Hints.Show("drive", K(Controls.Act.Forward) + "/" + K(Controls.Act.Back) + " THROTTLE / BRAKE  " + K(Controls.Act.Jump) + " HANDBRAKE  " + K(Controls.Act.Enter) + " EXIT  " + K(Controls.Act.View) + " CAMERA");
                if (MadMax.World.DayNight.Darkness > 0.5f && car.TryGetComponent<VehicleLights>(out var vl) && !vl.On) Hints.Show("lights", K(Controls.Act.Lights) + " HEADLIGHTS");
                if (car.TryGetComponent<VehicleSystems>(out var sys) && sys.fuelCapacity > 0f && sys.FuelFraction < 0.15f)
                    Hints.Show("fuel", "LOW FUEL: " + K(Controls.Act.Service) + " FILLS FROM YOUR PACK (OUTSIDE)  " + K(Controls.Act.Siphon) + " SIPHONS ANOTHER VEHICLE");
                return;
            }
            if (Player.Sitting && Player.SeatedOn && Player.SeatedOn.ride != null) { Hints.Show("horse", K(Controls.Act.Forward) + " TROT  " + K(Controls.Act.Run) + " GALLOP  " + K(Controls.Act.Jump) + " JUMP  " + K(Controls.Act.Enter) + " DISMOUNT"); return; }
            if (Build && Build.Active) { Hints.Show("build", "LMB PLACE  " + K(Controls.Act.BuildRotate) + " ROTATE  " + K(Controls.Act.BuildDismantle) + " DISMANTLE  " + K(Controls.Act.BuildPrevCategory) + " " + K(Controls.Act.BuildNextCategory) + " CATEGORY  HOLD " + K(Controls.Act.Build) + ": WHEEL"); return; }
            if (Player.Tool is FishingRodTool) { Hints.Show("fish", "LMB CAST  CLICK WHEN IT BITES  HOLD LMB TO REEL"); return; }
            if (Player.Tool is RangedTool) { Hints.Show("gun", "RMB AIM  LMB FIRE  " + K(Controls.Act.Reload) + " RELOAD"); return; }
            if (Hints.Show("walk", K(Controls.Act.Forward) + K(Controls.Act.Left) + K(Controls.Act.Back) + K(Controls.Act.Right) + " WALK  " + K(Controls.Act.Run) + " RUN  " + K(Controls.Act.Jump) + " JUMP / CLIMB  " + K(Controls.Act.Help) + " ALL KEYS")) return;
            if (playedFor > 45f && Hints.Show("wheel", "HOLD TAB: ACTION WHEEL  TAP TAB: NEXT FLEET VEHICLE")) return;
            if (playedFor > 90f && Hints.Show("map", K(Controls.Act.Map) + " MAP & JOURNAL: FOUND PLACES, JOBS, WAYPOINTS")) return;
            if (NearbyVehicle) Hints.Show("enter", K(Controls.Act.Enter) + " DRIVE  " + K(Controls.Act.Use) + " WITH A WRENCH: TAKE / MOUNT PARTS");
        }

        // ------------------------------------------------------------------ death stash

        [System.Serializable] public class StashSave { public Vector3 position; public string contents; }
        readonly List<Container> stashes = new List<Container>();

        /// <summary>Part of the pack stays where you died (NORMAL a third, HARD two thirds, BRUTAL everything but what you
        /// wear; EASY nothing), in a searchable pile that is kept until emptied.</summary>
        void DropStash()
        {
            float share = Rules.difficulty switch { 0 => 0f, 1 => 0.34f, 2 => 0.67f, _ => 1f };
            if (share <= 0f) return;
            var inv = new Inventory();
            for (int t = 1; t < ResourceInfo.Count; t++)
            {
                var rt = (ResourceType)t;
                int n = Mathf.FloorToInt(Inventory.Get(rt) * share);
                if (n > 0 && Inventory.TrySpend(rt, n)) inv.Add(rt, n);
            }
            var worn = new HashSet<string>();
            foreach (var o in Player.Rig.outfit) { var cd = ClothingLibrary.Get(o); if (cd != null) worn.Add(ClothingLibrary.ItemId(cd)); }
            var keep = new HashSet<string>();
            if (Rules.difficulty < 3) foreach (var h in Hotbar) if (h != null) keep.Add(h);             // your tools stay with you but on BRUTAL
            foreach (var kv in new List<KeyValuePair<string, int>>(Inventory.Items))
            {
                if (kv.Value <= 0 || keep.Contains(kv.Key) || worn.Contains(kv.Key)) continue;
                int n = Mathf.CeilToInt(kv.Value * share);
                if (n > 0 && Inventory.TakeItem(kv.Key, n)) inv.AddItem(kv.Key, n);
            }
            if (ItemCatalog.TotalWeight(inv) <= 0f) return;
            var at = Player.transform.position;
            at.y = terrain ? terrain.Height(at.x, at.z) : at.y;
            if (SpawnStash(at, InventoryCodec.Encode(inv))) Toast("YOUR STASH STAYS WHERE YOU FELL (WAYPOINT SET)");
            UpdateHotbarNow();
        }

        Container SpawnStash(Vector3 at, string contents)
        {
            var def = FurnitureLibrary.Get("crate");
            var go = new GameObject("DeathStash", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.position = at;
            if (def != null) go.GetComponent<MeshFilter>().sharedMesh = def.mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = propMaterial;
            var box = go.AddComponent<BoxCollider>();
            if (def != null && def.mesh) { box.center = def.mesh.bounds.center; box.size = def.mesh.bounds.size; } else box.size = Vector3.one * 0.6f;
            var c = go.AddComponent<Container>();
            c.title = "YOUR STASH";
            c.capacity = 999f;
            InventoryCodec.Decode(c.inventory, contents);
            stashes.Add(c);
            if (!HasWaypoint) SetWaypoint(at, "YOUR STASH");                                        // back to your things
            return c;
        }

        void SaveStashes(SaveData d)
        {
            d.stashes = new List<StashSave>();
            foreach (var c in stashes) if (c && ItemCatalog.TotalWeight(c.inventory) > 0f) d.stashes.Add(new StashSave { position = c.transform.position, contents = InventoryCodec.Encode(c.inventory) });
        }

        void LoadStashes(SaveData d)
        {
            if (d.stashes == null) return;
            foreach (var s in d.stashes) SpawnStash(s.position, s.contents);
        }

        /// <summary>Emptied stashes go away.</summary>
        void UpdateStashes()
        {
            for (int i = stashes.Count - 1; i >= 0; i--)
            {
                var c = stashes[i];
                if (!c) { stashes.RemoveAt(i); continue; }
                if (Menus && Menus.IsOpen) continue;
                if (ItemCatalog.TotalWeight(c.inventory) <= 0f) { Destroy(c.gameObject); stashes.RemoveAt(i); }
            }
        }
    }
}
