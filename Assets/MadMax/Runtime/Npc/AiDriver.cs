using System.Collections.Generic;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>Drives a vehicle through the same inputs as the player (throttle, brake, steer, handbrake):
    /// follow a road polyline (ping-pong), chase a target with lead and ramming, follow a leader in formation,
    /// circle a target, or park. Sphere-casts ahead to steer around obstacles, backs out when stuck, reports
    /// flipped / disabled so the crew can bail out. Hands over when the player takes the wheel. <see cref="DriveTo"/> plans a
    /// one-way route over generated and player-built roads (<see cref="RoadRoute.FindForDriving"/>); on the player's
    /// paving the driver looks less far ahead and keeps a road pace so it stays on the narrow lane.</summary>
    [DefaultExecutionOrder(-20)]
    public class AiDriver : MonoBehaviour
    {
        public enum Goal { Park, Path, Chase, Follow, Circle, Escort, Flank }
        public Goal goal = Goal.Park;
        public List<Vector3> path;
        public int index, dir = 1;
        public Transform target;
        public AiDriver leader;
        public Vector3 slot = new Vector3(0f, 0f, -12f);   // formation offset in the leader's frame
        public float cruise = 12f, chaseSpeed = 28f, circleRadius = 20f;
        public bool avoidTarget;                             // chase without ramming
        public float flankSide = 1f;                         // Flank: ride alongside the target on this side (+1 right)
        public bool oneWay;                                  // Path: stop at the last point instead of turning back
        public float playerRoadSpeed = 13f, gravelRoadSpeed = 10f;   // m/s on the player's hard / gravel roads

        /// <summary>A one-way route (<see cref="DriveTo"/>) reached its end; the driver parked.</summary>
        public bool Arrived { get; private set; }
        /// <summary>The last planned route runs along the player's roads.</summary>
        public bool RouteUsesPlayerRoad { get; private set; }
        /// <summary>On the player's paving right now (last physics step).</summary>
        public bool OnPlayerRoad { get; private set; }
        Vector3 routeDest;
        float nextPlan;
        bool lastPlanOk;

        VehicleDriver v;
        float stuckT, reverseT, flipT, circleSide = 1f;
        readonly RaycastHit[] hits = new RaycastHit[8];

        public VehicleDriver Vehicle => v;
        public bool Flipped => flipT > 4f;
        public bool Disabled
        {
            get
            {
                if (!v || Flipped) return true;
                if (v.TryGetComponent<BikeBalance>(out var bike) && bike.Crashed) return true;   // the biker went down
                foreach (var p in v.GetComponentsInChildren<VehiclePart>())
                    if (p.category == PartCategory.Engine && p.Socket) return p.damage >= 0.98f;
                return true;                                         // no engine left
            }
        }

        void Awake()
        {
            v = GetComponent<VehicleDriver>();
            v.aiDriven = true;
            v.bakedDriver = true;
            v.Occupied = true;
            circleSide = Random.value < 0.5f ? -1f : 1f;
        }

        /// <summary>Stop driving: the crew bailed out or the player took over.</summary>
        public void Release()
        {
            if (!v) return;
            v.throttleInput = v.brakeInput = v.steerInput = 0f;
            v.handbrake = true;
            v.aiDriven = false;
            v.bakedDriver = false;
            v.Occupied = false;
            enabled = false;
        }

        /// <summary>The driver is back behind the wheel (after <see cref="Release"/>, e.g. a beaten engine fire).</summary>
        public void Retake()
        {
            if (!v) return;
            v.handbrake = false;
            v.aiDriven = true;
            v.bakedDriver = true;
            v.Occupied = true;
            enabled = true;
        }

        /// <summary>A hand-set one-way path starts afresh (not yet arrived).</summary>
        public void ClearArrived() => Arrived = false;

        /// <summary>Start on the path at the point nearest to the vehicle.</summary>
        public void SetPath(List<Vector3> pts, int direction)
        {
            path = pts; dir = direction >= 0 ? 1 : -1; goal = Goal.Path; oneWay = false; Arrived = false;
            float best = float.MaxValue;
            var p = transform.position;
            for (int i = 0; i < pts.Count; i++) { float d = (pts[i] - p).sqrMagnitude; if (d < best) { best = d; index = i; } }
            index = Mathf.Clamp(index + dir * 2, 0, pts.Count - 1);
        }

        /// <summary>Plan a route to <paramref name="dest"/> along the roads (generated and the player's, open ground only
        /// where no road serves) and drive it one way, parking at the end. False when the straight line was all there is
        /// (it is driven anyway).</summary>
        public bool DriveTo(Vector3 dest)
        {
            var world = MadMax.Game.WastelandGame.Instance ? MadMax.Game.WastelandGame.Instance.World : null;
            var pts = new List<Vector3>();
            bool ok = RoadRoute.FindForDriving(world, transform.position, dest, pts, out bool viaPlayer);
            var t = DeformableTerrain.Instance;
            for (int i = 0; i < pts.Count; i++) { var p = pts[i]; if (t) p.y = t.HeightNoLoad(p.x, p.z); pts[i] = p; }
            path = pts; dir = 1; index = Mathf.Min(1, pts.Count - 1); goal = Goal.Path; oneWay = true; Arrived = false;
            routeDest = dest; RouteUsesPlayerRoad = viaPlayer; lastPlanOk = ok;
            nextPlan = Time.time + 3f;
            return ok;
        }

        /// <summary>Keep a one-way road route to <paramref name="dest"/> (re-planned at most every 3 s, when the
        /// destination moved 15 m or more). With <paramref name="playerRoadOnly"/> only a route over the player's roads
        /// counts. True while such a route is being driven.</summary>
        public bool RouteToward(Vector3 dest, bool playerRoadOnly)
        {
            bool fits = goal == Goal.Path && oneWay && !Arrived && lastPlanOk && (!playerRoadOnly || RouteUsesPlayerRoad);
            if (fits && Flat(dest - routeDest).magnitude < 15f) return true;
            if (Time.time < nextPlan) return fits;
            var keepGoal = goal; var keepPath = path; int keepIndex = index, keepDir = dir; bool keepOneWay = oneWay;
            if (DriveTo(dest) && (!playerRoadOnly || RouteUsesPlayerRoad)) return true;
            goal = keepGoal; path = keepPath; index = keepIndex; dir = keepDir; oneWay = keepOneWay;   // not worth it: carry on as before
            lastPlanOk = false;
            return false;
        }

        void FixedUpdate()
        {
            var g = MadMax.Game.WastelandGame.Instance;
            if (!v || (g && g.Current == v)) { Release(); if (v && g && g.Current == v) v.Occupied = true; return; }   // the player took the wheel: still occupied
            float dt = Time.fixedDeltaTime;
            var pos = transform.position;
            float speed = v.ForwardSpeed;
            flipT = transform.up.y < 0.3f ? flipT + dt : 0f;

            Vector3 aim = pos + transform.forward * 10f;
            float desired = 0f;
            bool ram = false;
            switch (goal)
            {
                case Goal.Path:
                {
                    if (path == null || path.Count < 2) break;
                    // on the player's paving (4 m cells, often one lane wide): a shorter look-ahead and a road pace
                    var tr = DeformableTerrain.Instance;
                    bool gravel = false;
                    OnPlayerRoad = tr && tr.PlayerRoadAt(pos.x, pos.z, out gravel);
                    float look = OnPlayerRoad ? 4.5f + Mathf.Abs(speed) * 0.45f : 7f + Mathf.Abs(speed) * 0.7f;
                    for (int guard = 0; guard < 8 && Flat(path[index] - pos).magnitude < look; guard++)
                    {
                        int next = index + dir;
                        if (next < 0 || next >= path.Count)
                        {
                            if (oneWay) break;
                            dir = -dir; next = index + dir;
                        }
                        index = next;
                    }
                    aim = path[index];
                    desired = OnPlayerRoad ? Mathf.Min(cruise, gravel ? gravelRoadSpeed : playerRoadSpeed) : cruise;
                    if (oneWay && index == path.Count - 1)
                    {
                        float left = Flat(aim - pos).magnitude;
                        desired = Mathf.Min(desired, left * 0.5f);                                  // ease up to the end
                        if (left < 3.5f) { Arrived = true; goal = Goal.Park; }
                    }
                    break;
                }
                case Goal.Chase:
                {
                    if (!target) break;
                    var tb = target.GetComponentInParent<Rigidbody>();
                    var tv = tb && !tb.isKinematic ? tb.linearVelocity : Vector3.zero;
                    float dist = Flat(target.position - pos).magnitude;
                    float lead = Mathf.Clamp(dist / Mathf.Max(8f, Mathf.Abs(speed)), 0f, 1.6f);
                    aim = target.position + tv * lead;
                    desired = chaseSpeed;
                    ram = !avoidTarget && dist < 25f;
                    if (avoidTarget && dist < 12f) desired = tv.magnitude;
                    break;
                }
                case Goal.Follow:
                {
                    if (!leader || !leader.enabled) { goal = Goal.Park; break; }
                    aim = leader.transform.TransformPoint(slot);
                    float err = Vector3.Dot(aim - pos, transform.forward);
                    desired = Mathf.Max(0f, leader.Vehicle.ForwardSpeed + Mathf.Clamp(err * 0.6f, -6f, 8f));
                    if (Flat(aim - pos).magnitude < 3f) desired = Mathf.Min(desired, leader.Vehicle.ForwardSpeed);
                    break;
                }
                case Goal.Escort:
                {
                    // a companion driving behind the player: ~11 m back along their heading, matching their pace
                    if (!target) break;
                    var tb = target.GetComponentInParent<Rigidbody>();
                    var tv = tb && !tb.isKinematic ? tb.linearVelocity : Vector3.zero;
                    var back = tv.sqrMagnitude > 4f ? tv.normalized : (pos - target.position).normalized;
                    aim = target.position - back * 11f;
                    float gap = Flat(aim - pos).magnitude;
                    desired = Mathf.Clamp(tv.magnitude + (gap - 2f) * 0.5f, 0f, chaseSpeed);
                    if (Flat(target.position - pos).magnitude < 9f) desired = Mathf.Min(desired, 1.5f);
                    break;
                }
                case Goal.Flank:
                {
                    // bikers: ride alongside the target, 5 m off its flank, matching its pace (and a bit ahead)
                    if (!target) break;
                    var tb = target.GetComponentInParent<Rigidbody>();
                    var tv = tb && !tb.isKinematic ? tb.linearVelocity : Vector3.zero;
                    var heading = tv.sqrMagnitude > 4f ? tv.normalized : target.forward;
                    var right = Vector3.Cross(Vector3.up, heading);
                    aim = target.position + right * flankSide * 5f + heading * (4f + tv.magnitude * 0.4f);
                    float along = Vector3.Dot(aim - pos, heading);
                    desired = Mathf.Clamp(tv.magnitude + along * 0.6f, 0f, chaseSpeed);
                    break;
                }
                case Goal.Circle:
                {
                    if (!target) break;
                    var c = target.position;
                    var off = Flat(pos - c);
                    float a = Mathf.Atan2(off.z, off.x) + circleSide * 0.6f;
                    aim = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * circleRadius;
                    desired = 9f;
                    break;
                }
            }

            var local = transform.InverseTransformPoint(aim);
            float ang = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float steer = Mathf.Clamp(ang / 32f, -1f, 1f);
            if (goal != Goal.Chase || !ram) desired *= Mathf.Clamp(1f - Mathf.Abs(ang) / 110f, 0.3f, 1f);   // slow into bends

            // obstacles ahead (not the ground, not ourselves, not what we mean to ram)
            if (Mathf.Abs(speed) > 1f && desired > 0.5f)
            {
                var origin = pos + Vector3.up * 0.9f + transform.forward * 1.5f;
                float reach = 3f + Mathf.Abs(speed) * 1.1f;
                int n = Physics.SphereCastNonAlloc(origin, 0.9f, transform.forward, hits, reach, ~0, QueryTriggerInteraction.Ignore);
                float nearest = reach; Vector3 hp = Vector3.zero;
                for (int i = 0; i < n; i++)
                {
                    var c = hits[i].collider;
                    if (c.transform.IsChildOf(transform) || c.GetComponentInParent<DeformableTerrain>()) continue;
                    if (ram && target && c.transform.IsChildOf(target.root)) continue;
                    if (hits[i].distance < nearest) { nearest = hits[i].distance; hp = hits[i].point; }
                }
                if (nearest < reach)
                {
                    float side = Vector3.Dot(hp - pos, transform.right) >= 0f ? -1f : 1f;
                    steer = Mathf.Clamp(steer + side * (1f - nearest / reach) * 1.6f, -1f, 1f);
                    desired = Mathf.Min(desired, 4f + nearest);
                }
            }

            // stuck against something: back out with the wheels turned the other way
            if (reverseT > 0f) reverseT -= dt;
            else if (desired > 1f && Mathf.Abs(speed) < 0.6f) { if ((stuckT += dt) > 2f) { stuckT = 0f; reverseT = 1.6f; } }
            else stuckT = 0f;
            // target right behind at low speed: turn around by backing up
            if (reverseT <= 0f && goal == Goal.Chase && Mathf.Abs(ang) > 120f && Mathf.Abs(speed) < 4f && Flat(aim - pos).magnitude < 25f) reverseT = 1.2f;

            if (reverseT > 0f)
            {
                v.throttleInput = 0f; v.brakeInput = 1f; v.steerInput = -steer; v.handbrake = false;
                return;
            }
            float e = desired - speed;
            v.steerInput = steer;
            v.throttleInput = desired < 0.3f ? 0f : Mathf.Clamp01(e * 0.3f + 0.2f);
            v.brakeInput = speed > 0.5f ? Mathf.Clamp01(-e * 0.25f) : 0f;
            v.handbrake = desired < 0.3f && Mathf.Abs(speed) < 1f;
            if (v.brakeInput > 0.05f) v.throttleInput = 0f;
        }

        static Vector3 Flat(Vector3 d) { d.y = 0f; return d; }
    }
}
