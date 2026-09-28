using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Car transport trailer: drive up the rear ramps, then on foot [E] secures every vehicle standing on a deck
    /// (it rides along, strapped down) or releases them. Double deckers: [T] tilts the upper deck down for loading and
    /// raises it again (strapped cars go with it). Ramps fold up while towing.</summary>
    public class TrailerDeck : MonoBehaviour, MadMax.Building.IInteractable
    {
        public static readonly List<TrailerDeck> All = new List<TrailerDeck>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();

        Transform deck, upper, rampL, rampR;
        Quaternion rampDown;
        float upperT = 1f, upperTarget = 1f, rampT;            // upper deck: 1 raised, 0 tilted down; ramps: 0 down, 1 folded
        TowCoupling tow;
        readonly List<(VehicleDriver v, Transform parent)> strapped = new List<(VehicleDriver, Transform)>();
        public const float UpperTilt = 21f;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Awake()
        {
            deck = transform.Find("Deck"); upper = transform.Find("UpperDeck");
            rampL = transform.Find("RampL"); rampR = transform.Find("RampR");
            if (rampL) rampDown = rampL.localRotation;
            tow = GetComponent<TowCoupling>();
        }

        public int Loaded => strapped.Count;

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            string s = strapped.Count > 0 ? "[E] RELEASE " + strapped.Count + " CAR" + (strapped.Count > 1 ? "S" : "") : "[E] STRAP DOWN CARS";
            if (upper) s += upperTarget > 0.5f ? "  [T] LOWER UPPER DECK" : "  [T] RAISE UPPER DECK";
            return s;
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary) { if (upper) upperTarget = upperTarget > 0.5f ? 0f : 1f; return; }
            if (strapped.Count > 0) { ReleaseAll(); g.Toast("CARGO RELEASED"); return; }
            int n = Secure(g);
            g.Toast(n > 0 ? "STRAPPED " + n + " VEHICLE" + (n > 1 ? "S" : "") : "DRIVE A VEHICLE ONTO THE DECK FIRST");
        }

        int Secure(MadMax.Game.WastelandGame g)
        {
            int n = 0;
            foreach (var v in g.AllVehicles)
            {
                if (!v || v.transform == transform || v == g.Current || (tow && tow.Tower == v)) continue;
                var local = transform.InverseTransformPoint(v.Body.worldCenterOfMass);
                if (Mathf.Abs(local.x) > 1.6f || Mathf.Abs(local.z) > 3.6f || local.y < 0.3f || local.y > 4.5f) continue;
                bool onUpper = upper && local.y > 2f;
                var parent = onUpper ? upper : (deck ? deck : transform);
                v.Body.isKinematic = true;
                v.transform.SetParent(parent, true);
                SetIgnore(v, true);
                strapped.Add((v, parent));
                n++;
            }
            return n;
        }

        public void ReleaseAll()
        {
            var rb = GetComponent<Rigidbody>();
            foreach (var (v, _) in strapped)
            {
                if (!v) continue;
                v.transform.SetParent(null, true);
                v.Body.isKinematic = false;
                if (rb) v.Body.linearVelocity = rb.linearVelocity;
                SetIgnore(v, false);
            }
            strapped.Clear();
        }

        void SetIgnore(VehicleDriver v, bool ignore)
        {
            var mine = GetComponentsInChildren<Collider>();
            foreach (var a in v.GetComponentsInChildren<Collider>())
                foreach (var b in mine)
                    if (!b.transform.IsChildOf(v.transform)) Physics.IgnoreCollision(a, b, ignore);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            // ramps fold up while hitched, drop when parked
            float rampTarget = tow && tow.Tower ? 1f : 0f;
            if (!Mathf.Approximately(rampT, rampTarget))
            {
                rampT = Mathf.MoveTowards(rampT, rampTarget, dt * 0.7f);
                var q = rampDown * Quaternion.Euler(-rampT * 92f, 0, 0);
                if (rampL) rampL.localRotation = q;
                if (rampR) rampR.localRotation = q;
            }
            if (upper && !Mathf.Approximately(upperT, upperTarget))
            {
                upperT = Mathf.MoveTowards(upperT, upperTarget, dt * 0.25f);
                upper.localRotation = Quaternion.Euler((1f - upperT) * UpperTilt, 0, 0);
            }
        }
    }
}
