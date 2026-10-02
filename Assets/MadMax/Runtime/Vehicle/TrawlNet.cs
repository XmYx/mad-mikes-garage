using System.Collections.Generic;
using MadMax.Game;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>The trawler's net (user additions): [1] while at the helm pays it out or winches it in. Towed at 1–4 m/s
    /// over sea deeper than 4 m it fills the fish hold (the trawler's cargo <see cref="MadMax.Building.Container"/>)
    /// with whatever the local sea holds (<see cref="FishLibrary"/>); too fast and it tears loose nothing but weed.</summary>
    public class TrawlNet : MonoBehaviour
    {
        public bool deployed;
        VehicleDriver v;
        BoatModel boat;
        MadMax.Building.Container hold;
        readonly List<FishDef> pool = new List<FishDef>();
        float haul;

        void Awake() { v = GetComponent<VehicleDriver>(); boat = GetComponent<BoatModel>(); hold = GetComponent<MadMax.Building.Container>(); }

        void Update()
        {
            var g = WastelandGame.Instance;
            if (!g || !boat) return;
            if (g.Current == v && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                deployed = !deployed;
                g.Toast(deployed ? "NET OUT: TOW IT SLOWLY (4-14 KM/H) OVER DEEP WATER" : "NET IN");
                MadMax.Audio.Sfx.Play("chain", transform.position, 0.7f, 0.9f, 30f);
            }
            if (!deployed || !boat.Afloat || !hold) return;
            var t = DeformableTerrain.Instance;
            var p = transform.position;
            if (!t || t.World == null || !t.World.Ocean(p.x, p.z) || t.WaterDepthNoLoad(p.x, p.z) < 4f) return;
            float sp = Mathf.Abs(boat.Speed);
            if (sp < 1f || sp > 4f) return;
            if ((haul += Time.deltaTime * sp) < 20f) return;                                  // a fish every ~20 m of towing
            haul = 0f;
            FishLibrary.Pool(t.BiomeAt(p.x, p.z), false, pool, true);
            var fish = FishLibrary.Pick(pool, null, DayNight.Hours, Weather.Raining, Weather.Temperature, out _, Weather.Season);
            if (fish == null || fish.junk) return;
            if (hold.Weight + 1f > hold.capacity) { if (g.Current == v) g.Toast("THE HOLD IS FULL"); return; }
            hold.inventory.AddItem(fish.mutant ? "food_fish_glow" : "food_fish_raw", 1);
            if (g.Current == v) g.Toast("NET: " + fish.name + " INTO THE HOLD");
        }
    }
}
