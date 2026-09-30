using System.Collections.Generic;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Tilt-rod landmine (<c>kit_landmine</c>, laid in build mode): buried flush, it arms
    /// <see cref="ArmSeconds"/> s after it is laid (later if something already stands on it) and goes off under any
    /// vehicle whose body passes over its rod, under a person stepping on it — never its owner on foot, who knows where
    /// it lies — or when it is shot or blasted. The owner driving towards one of their own mines is warned. Raiders
    /// don't see it (<see cref="DefenceWorks.Concealed"/>). The blast (<see cref="DefenceWorks.Blast"/>) wrecks the
    /// tyre over it and whatever stands close; the mine is spent. Only the authority detonates; clients see the blast
    /// through the piece's state.</summary>
    public class Landmine : MonoBehaviour, IPlaceState, IInteractable
    {
        public const float ArmSeconds = 8f;
        public float radius = 2.2f, power = 4f;
        float armIn = ArmSeconds, scan, warnCd;
        bool armed, blown, onVehicle;
        Placeable piece;

        public static readonly List<Landmine> All = new List<Landmine>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public bool Armed => armed && !blown && !onVehicle;
        public bool Blown => blown;

        void Awake() => piece = GetComponent<Placeable>();
        void Start() => onVehicle = GetComponentInParent<Rigidbody>();                               // carried on a truck bed: inert

        Vector3 At => transform.position + transform.up * 0.08f;

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            if (blown || !g.OwnsPiece(piece)) return null;                                            // hidden to everyone else
            return onVehicle ? "LANDMINE (INERT ON A VEHICLE)" : armed ? "YOUR LANDMINE: ARMED" : "YOUR LANDMINE: ARMING " + Mathf.CeilToInt(armIn) + " S";
        }
        public void Use(MadMax.Game.WastelandGame g, bool secondary) { }

        void Update()
        {
            if (blown || onVehicle || !DefenceWorks.Authority) return;
            var g = MadMax.Game.WastelandGame.Instance;
            if (!g) return;
            if (!armed)
            {
                if ((armIn -= Time.deltaTime) > 0f) return;
                if (Pressed(g, true)) { armIn = 2f; return; }                                           // something stands on it: wait
                armed = true;
                if (piece) piece.Dirty();
                MadMax.Audio.Sfx.Play("click", At, 0.3f, 1.4f, 6f);
                return;
            }
            if ((scan -= Time.deltaTime) > 0f) return;
            scan = 0.08f;
            if (Pressed(g, false)) { Detonate(); return; }
            Warn(g);
        }

        /// <summary>A vehicle's body over the rod, or someone on the plate (not the owner on foot).</summary>
        bool Pressed(MadMax.Game.WastelandGame g, bool anyone)
        {
            var at = At;
            foreach (var v in g.AllVehicles)
            {
                if (!v || !v.Body || (v.transform.position - at).sqrMagnitude > 8f * 8f) continue;
                if (DefenceWorks.Over(v, at)) return true;
            }
            foreach (var n in MadMax.Npc.Npc.All)
            {
                if (!n || !n.Alive || (n.companion && !anyone)) continue;
                if (OnPlate(n.transform.position, at)) return true;
            }
            var pl = g.Player;
            if (pl && pl.gameObject.activeSelf && !g.Current && (anyone || !g.OwnsPiece(piece)) && OnPlate(pl.transform.position, at)) return true;
            return false;
        }

        static bool OnPlate(Vector3 feet, Vector3 at)
        {
            var d = feet - at;
            return d.y > -0.6f && d.y < 0.8f && d.x * d.x + d.z * d.z < 0.55f * 0.55f;
        }

        /// <summary>The owner at the wheel, heading for one of their own mines within 14 m: a beep and a warning.</summary>
        void Warn(MadMax.Game.WastelandGame g)
        {
            var v = g.Current;
            if (!v || !v.Body || Time.time < warnCd || !g.OwnsPiece(piece)) return;
            var rel = At - v.transform.position; rel.y = 0f;
            float d = rel.magnitude;
            if (d > 14f || d < 0.5f) return;
            var vel = v.Body.linearVelocity; vel.y = 0f;
            if (vel.magnitude < 1f || Vector3.Dot(vel.normalized, rel / d) < 0.85f) return;
            warnCd = Time.time + 4f;
            MadMax.Audio.Sfx.Play2D("click", 0.8f);
            g.Toast("YOUR MINE AHEAD: " + Mathf.RoundToInt(d) + " M");
        }

        /// <summary>Go off: the blast, the tyre over it popped, the piece spent a moment later (after the state has
        /// reached the clients).</summary>
        public void Detonate()
        {
            if (blown) return;
            blown = true;
            var at = At;
            DefenceWorks.Blasts++;
            PopTyres(at);
            DefenceWorks.Blast(at, radius, power, gameObject, true);
            Hide();
            if (piece) piece.Dirty();
            Invoke(nameof(Consume), 0.3f);
        }

        /// <summary>Gone off in a raid nobody saw: removed without a blast.</summary>
        public void Spend() { if (blown) return; blown = true; Hide(); Consume(); }

        void Hide()
        {
            if (TryGetComponent<Renderer>(out var r)) r.enabled = false;
            if (TryGetComponent<Collider>(out var c)) c.enabled = false;
        }

        void Consume() { if (piece) piece.ApplyHit(transform.position, Vector3.up, 999f, 0.2f, gameObject); else Destroy(gameObject); }

        static void PopTyres(Vector3 at)
        {
            var g = MadMax.Game.WastelandGame.Instance;
            if (!g) return;
            foreach (var v in g.AllVehicles)
            {
                if (!v || (v.transform.position - at).sqrMagnitude > 8f * 8f || !DefenceWorks.Over(v, at)) continue;
                WheelStats best = null; float bd = 2.2f * 2.2f;
                foreach (var w in v.GetComponentsInChildren<WheelStats>())
                {
                    var d = w.transform.position - at; d.y = 0f;
                    if (!w.Popped && d.sqrMagnitude < bd) { bd = d.sqrMagnitude; best = w; }
                }
                if (best) best.Pop();
            }
        }

        /// <summary>Shot or caught in another blast while armed: it goes off where it lies.</summary>
        void OnDestroy()
        {
            // (the piece may already be torn down itself: read its flag through the managed reference)
            if (blown || !armed || onVehicle || ReferenceEquals(piece, null) || !piece.Collapsing || !DefenceWorks.Authority || !Application.isPlaying) return;
            blown = true;
            DefenceWorks.Blasts++;
            DefenceWorks.Blast(At, radius, power, gameObject, true);
        }

        public string SaveState() => blown ? "B" : armed ? "A" : "T" + Mathf.CeilToInt(Mathf.Max(0f, armIn));

        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            if (s[0] == 'B')
            {
                if (blown) return;
                blown = true;                                                                          // a client learns of the blast
                if (!DefenceWorks.Authority) DefenceWorks.Blast(At, radius, power, gameObject, false);
                else Invoke(nameof(Consume), 0.1f);                                                  // saved in the moment it went off
                Hide();
                return;
            }
            armed = s[0] == 'A';
            if (!armed && s.Length > 1 && int.TryParse(s.Substring(1), out int left)) armIn = left;
        }
    }
}
