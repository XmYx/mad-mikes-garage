using MadMax.Building;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>A street lamp spawned along a settlement's roads (<see cref="TownLights"/>): on the town grid, dusk to
    /// dawn. Smashing its head leaves it dark; a vehicle hitting the post hard enough snaps it (<see cref="BreakablePole"/>:
    /// it falls, dark). Both are remembered by key (saved in the lights block; old saves' knocked lamps lean).</summary>
    public class WorldLamp : MonoBehaviour
    {
        public string key;
        public int town = -1;
        PoweredLight lamp;
        float poll;

        public PoweredLight Lamp => lamp;

        void Awake() => lamp = GetComponent<PoweredLight>();

        void Start()
        {
            if (MadMax.Building.SalvageDesigns.Removed.Contains(key)) { Destroy(gameObject); return; }   // taken apart for good
            if (!lamp) return;
            lamp.external = true;
            lamp.smashed += _ => TownLights.Smashed.Add(key);
            if (TownLights.Smashed.Contains(key)) lamp.broken = true;
            if (TryGetComponent<BreakablePole>(out var pole)) pole.Broke += _ => { TownLights.Knocked.Add(key); if (lamp) lamp.broken = true; };
            if (TownLights.Knocked.Contains(key) && !BreakablePole.Fallen.ContainsKey(key)) { Lean(transform.forward); if (pole) pole.Retire(); }   // knocked before poles fell
            lamp.externalPower = TownLights.TownPowered(town);
        }

        void Update()
        {
            if ((poll -= Time.deltaTime) > 0f) return;
            poll = 2f;
            if (!lamp) return;
            lamp.externalPower = TownLights.TownPowered(town);
            if (!lamp.broken && TownLights.Smashed.Contains(key)) lamp.broken = true;          // a save loaded after the chunk spawned
        }

        /// <summary>Knocked over in an old save: leaning 60° away, no longer blocking.</summary>
        void Lean(Vector3 away)
        {
            if (lamp) lamp.broken = true;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
            var axis = Vector3.Cross(Vector3.up, away.normalized);
            transform.rotation = Quaternion.AngleAxis(60f, axis) * transform.rotation;
            foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;
        }
    }
}
