using System.Collections.Generic;
using MadMax.Game;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Tripwire: a wire at shin height between two stakes. Anyone crossing it — a person on foot (not its owner
    /// or a companion) or a vehicle that isn't the owner's — pulls the pin: a red flare goes up on its parachute (seen
    /// and heard far off), the nearest alarm bell within <see cref="BellRange"/> m rings and the owner hears of it. It
    /// re-arms after <see cref="Rearm"/> s. Raiders don't see it (<see cref="DefenceWorks.Concealed"/>).</summary>
    public class Tripwire : MonoBehaviour, IInteractable
    {
        public const float BellRange = 80f, Rearm = 20f;
        public float halfLength = 0.96f;
        float scan, rearmAt;
        Placeable piece;

        public static readonly List<Tripwire> All = new List<Tripwire>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>When it was last pulled (Time.time, -1 never) and the bell it rang.</summary>
        public float LastTripped { get; private set; } = -1f;
        public AlarmBell LastBell { get; private set; }
        public bool Ready => Time.time >= rearmAt;

        void Awake() => piece = GetComponent<Placeable>();

        public string Prompt(WastelandGame g) => g.OwnsPiece(piece) ? (Ready ? "YOUR TRIPWIRE: SET" : "YOUR TRIPWIRE: RESETTING") : null;
        public void Use(WastelandGame g, bool secondary) { }

        void Update()
        {
            if (!DefenceWorks.Authority || !Ready || (scan -= Time.deltaTime) > 0f) return;
            scan = 0.05f;
            var g = WastelandGame.Instance;
            if (!g) return;
            var at = transform.position;
            foreach (var n in MadMax.Npc.Npc.All)
                if (n && n.Alive && !n.companion && (n.transform.position - at).sqrMagnitude < 9f && Across(n.transform.position)) { Trip(g); return; }
            var pl = g.Player;
            if (pl && pl.gameObject.activeSelf && !g.Current && !g.OwnsPiece(piece) && Across(pl.transform.position)) { Trip(g); return; }
            foreach (var v in g.AllVehicles)
            {
                if (!v || (v.transform.position - at).sqrMagnitude > 64f || Mine(g, v)) continue;
                for (int i = -1; i <= 1; i++)
                    if (DefenceWorks.Over(v, transform.TransformPoint(new Vector3(i * halfLength * 0.8f, 0.16f, 0f)))) { Trip(g); return; }
            }
        }

        bool Mine(WastelandGame g, MadMax.Vehicles.VehicleDriver v)
        {
            if (!g.OwnsPiece(piece)) return false;
            if (v == g.Current) return true;
            foreach (var f in g.Fleet) if (f == v) return true;
            return false;
        }

        /// <summary>Feet within the wire's span and a stride of it.</summary>
        bool Across(Vector3 feet)
        {
            var lp = transform.InverseTransformPoint(feet);
            return Mathf.Abs(lp.x) < halfLength && Mathf.Abs(lp.z) < 0.32f && lp.y > -0.5f && lp.y < 0.9f;
        }

        /// <summary>Pull the pin: flare, bell, word to the owner.</summary>
        public void Trip(WastelandGame g)
        {
            LastTripped = Time.time; rearmAt = Time.time + Rearm;
            DefenceWorks.Trips++;
            var at = transform.TransformPoint(new Vector3(0.96f, 0.6f, 0.08f));
            MadMax.Audio.Sfx.Play("click", at, 0.8f, 0.8f, 20f);
            MadMax.Audio.Sfx.Play("pop", at, 0.9f, 1.3f, 120f);
            var flare = Projectile.Launch(Projectile.Kind.Flare, at, Vector3.up * 11f + Random.insideUnitSphere * 1.2f, 0f, null, gameObject, g.propMaterial);
            flare.gravity = 1.1f; flare.life = 17f;                                                  // hangs under its parachute, burns out aloft
            if (MadMax.Npc.NpcDirector.Instance) MadMax.Npc.NpcDirector.Instance.Noise(at, 80f);
            LastBell = null; float bd = BellRange * BellRange;
            foreach (var b in AlarmBell.All)
            {
                if (!b) continue;
                float d = (b.transform.position - at).sqrMagnitude;
                if (d < bd) { bd = d; LastBell = b; }
            }
            if (LastBell) LastBell.Ring(true);
            else if (g.OwnsPiece(piece) && g.Player && Vector3.Distance(g.Player.transform.position, at) < 300f) g.Toast("TRIPWIRE PULLED: SOMEONE AT THE BASE");
        }
    }
}
