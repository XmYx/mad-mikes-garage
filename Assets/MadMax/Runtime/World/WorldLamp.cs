using MadMax.Building;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>A street lamp spawned along a settlement's roads (<see cref="TownLights"/>): on the town grid, dusk to
    /// dawn. Smashing its head leaves it dark; a vehicle hitting the post faster than <see cref="KnockSpeed"/> knocks it
    /// over (dark, leaning, no longer solid). Both are remembered by key (saved in the lights block).</summary>
    public class WorldLamp : MonoBehaviour
    {
        public const float KnockSpeed = 6f;
        public string key;
        public int town = -1;
        PoweredLight lamp;
        float poll;

        public PoweredLight Lamp => lamp;

        void Awake() => lamp = GetComponent<PoweredLight>();

        void Start()
        {
            if (!lamp) return;
            lamp.external = true;
            lamp.smashed += _ => TownLights.Smashed.Add(key);
            if (TownLights.Smashed.Contains(key)) lamp.broken = true;
            if (TownLights.Knocked.Contains(key)) Lean(transform.forward);
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

        void OnCollisionEnter(Collision c)
        {
            var rb = c.rigidbody;
            if (!rb || rb.mass < 300f || c.relativeVelocity.magnitude < KnockSpeed || TownLights.Knocked.Contains(key)) return;
            TownLights.Knocked.Add(key);
            var push = rb.linearVelocity; push.y = 0f;
            if (push.sqrMagnitude < 0.25f) { push = transform.position - rb.position; push.y = 0f; }
            if (lamp) lamp.Smash(transform.position + Vector3.up * 1.5f);
            Lean(push);
            MadMax.Audio.Sfx.Play("creak", transform.position, 0.6f, 0.8f, 30f, 0.5f);
        }

        /// <summary>Knocked over: leaning 60° away from the hit, no longer blocking.</summary>
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
