using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Hydraulic crane (part "cargo_crane" on a cargo socket; the Wrecker carries one). While driving:
    /// 7 grab / release whatever hangs under the hook (vehicle, wreck, part), hold 8 hoist up, hold 9 lower,
    /// Shift+8 / Shift+9 luff the boom up / down, hold 0 slew (Shift+0 the other way; eased). The turret and the boom are
    /// separate hinged segments of the part; the load hangs on a rope joint from the boom tip.</summary>
    public class Crane : MonoBehaviour
    {
        public const string PartId = "cargo_crane";
        const float BoomTipX = 0f, BoomTipY = 2.4f, BoomTipZ = -3.2f;   // boom tip in the crane part's space (m)
        VehicleDriver driver;
        Rigidbody rb;
        Rigidbody load;
        ConfigurableJoint joint;
        float rope = 2.5f, slew, slewSpeed, luff;
        LineRenderer line;
        public string Status { get; private set; }

        void Awake() { driver = GetComponent<VehicleDriver>(); rb = GetComponent<Rigidbody>(); }

        VehiclePart cranePart;
        MountSocket[] sockets;

        /// <summary>The mounted crane part (every vehicle carries this component: no allocation per lookup).</summary>
        public VehiclePart CranePart
        {
            get
            {
                if (cranePart && cranePart.Socket && cranePart.transform.IsChildOf(transform)) return cranePart;
                sockets ??= GetComponentsInChildren<MountSocket>(true);
                cranePart = null;
                foreach (var s in sockets) if (s && s.Current && s.Current.partId == PartId) { cranePart = s.Current; break; }
                return cranePart;
            }
        }

        Vector3 Tip
        {
            get
            {
                var p = CranePart;
                if (!p) return transform.position;
                var boom = p.transform.Find("turret/boom");
                return boom ? boom.TransformPoint(new Vector3(0f, 21f, -40f) * 0.08f) : p.transform.TransformPoint(new Vector3(BoomTipX, BoomTipY, BoomTipZ));
            }
        }
        Vector3 Hook => load ? load.worldCenterOfMass + Vector3.up * 0.8f : Tip + Vector3.down * rope;
        /// <summary>Where the hook hangs (world) and what it holds (automation, HUD).</summary>
        public Vector3 HookPoint => Hook;
        public Rigidbody Load => load;
        public float Rope => rope;

        /// <param name="slewDir">-1, 0 or +1</param>
        public void Control(bool press7, bool hold8, bool hold9, float slewDir, bool shift = false)
        {
            var part = CranePart;
            if (!part) { Status = null; if (load) Drop(); return; }
            float dt = Time.deltaTime;
            var turret = part.transform.Find("turret");
            var boom = turret ? turret.Find("boom") : null;
            slewSpeed = Mathf.MoveTowards(slewSpeed, slewDir * 35f, 70f * dt);          // hydraulic slew: ease in and out
            if (Mathf.Abs(slewSpeed) > 0.01f)
            {
                slew = Mathf.Repeat(slew + slewSpeed * dt, 360f);
                (turret ? turret : part.transform).localRotation = Quaternion.Euler(0, slew, 0);
                if (joint) joint.anchor = transform.InverseTransformPoint(Tip);             // the load swings round with the boom
                if (load) load.WakeUp();
            }
            if (shift && boom)
            {
                // luff: the boom pitches about its foot pin (+ = down)
                luff = Mathf.Clamp(luff + ((hold9 ? 1f : 0f) - (hold8 ? 1f : 0f)) * 12f * dt, -20f, 25f);
                boom.localRotation = Quaternion.Euler(luff, 0f, 0f);
                if (load) load.WakeUp();
            }
            else
            {
                if (hold8) rope = Mathf.Max(0.8f, rope - 1f * dt);
                if (hold9) rope = Mathf.Min(8f, rope + 1f * dt);
            }
            if (joint) joint.linearLimit = new SoftJointLimit { limit = rope };
            if (press7) { if (load) Drop(); else Grab(); }
            Status = load ? "CRANE: " + load.name.Replace("(Clone)", "").ToUpperInvariant() + " " + Mathf.RoundToInt(load.mass) + " KG  [7] RELEASE  [8/9] HOIST  [SHIFT+8/9] BOOM  [0]/[SHIFT+0] SLEW"
                          : "CRANE  [7] GRAB UNDER HOOK  [8/9] HOIST  [SHIFT+8/9] BOOM  [0]/[SHIFT+0] SLEW";
        }

        void Grab()
        {
            var hook = Tip + Vector3.down * rope;
            Rigidbody best = null; float bd = 3f;
            foreach (var c in Physics.OverlapSphere(hook, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                var r = c.attachedRigidbody;
                if (!r || r == rb || r.transform.IsChildOf(transform)) continue;
                float d = Vector3.Distance(c.ClosestPoint(hook), hook);
                if (d < bd) { bd = d; best = r; }
            }
            if (!best) { Status = "NOTHING UNDER THE HOOK"; return; }
            if (best.mass > rb.mass * 1.4f) { Status = "TOO HEAVY (" + Mathf.RoundToInt(best.mass) + " KG)"; return; }
            best.isKinematic = false;
            load = best;
            joint = rb.gameObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = load;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = transform.InverseTransformPoint(Tip);
            joint.connectedAnchor = load.transform.InverseTransformPoint(load.worldCenterOfMass + Vector3.up * 0.8f);
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Limited;
            rope = Mathf.Max(0.8f, Vector3.Distance(Tip, load.worldCenterOfMass + Vector3.up * 0.8f));
            joint.linearLimit = new SoftJointLimit { limit = rope };
            joint.linearLimitSpring = new SoftJointLimitSpring { spring = 400000f, damper = 30000f };
            joint.enableCollision = true;
            if (!line)
            {
                line = new GameObject("CraneRope").AddComponent<LineRenderer>();
                line.transform.SetParent(transform, false);
                line.sharedMaterial = MadMax.World.Fx.TransparentMaterial(null);
                line.widthMultiplier = 0.04f; line.positionCount = 2;
                line.startColor = line.endColor = new Color(0.1f, 0.1f, 0.1f, 1f);
            }
        }

        public void Drop()
        {
            if (joint) Destroy(joint);
            joint = null; load = null;
        }

        void FixedUpdate()
        {
            if (!load) return;
            driver.KeepAwakeUntil = Time.time + 0.5f;
            var other = load.GetComponent<VehicleDriver>();
            if (other) other.KeepAwakeUntil = Time.time + 0.5f;
            if (load.IsSleeping()) load.WakeUp();
        }

        void LateUpdate()
        {
            if (!CranePart) { if (line) line.enabled = false; return; }
            if (!line)
            {
                line = new GameObject("CraneRope").AddComponent<LineRenderer>();
                line.transform.SetParent(transform, false);
                line.sharedMaterial = MadMax.World.Fx.TransparentMaterial(null);
                line.widthMultiplier = 0.04f; line.positionCount = 2;
                line.startColor = line.endColor = new Color(0.1f, 0.1f, 0.1f, 1f);
            }
            if (joint) joint.anchor = transform.InverseTransformPoint(Tip);        // follows the slewing boom
            line.enabled = true;
            line.SetPosition(0, Tip);
            line.SetPosition(1, Hook);
        }
    }
}
