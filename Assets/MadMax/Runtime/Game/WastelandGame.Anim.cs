using System.Collections.Generic;
using MadMax.Items;
using MadMax.RPG;
using MadMax.Vehicles;
using MadMax.Voxel;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>What a timed job at a vehicle does (setting WORK ANIMATION).</summary>
    public enum WorkKind { Service, Refuel, Siphon, Battery, Take, Mount, Repair, Weld, Armour, Salvage, Jack }

    /// <summary>How the last timed job ended (automation checks).</summary>
    public enum WorkOutcome { None, Done, Cancelled }

    /// <summary>Timed work at vehicles (Anim block, setting WORK ANIMATION). The player walks round the vehicle to the
    /// spot — the engine bay for a service or the battery lead (hood lifted), the filler for fuel and siphoning, beside
    /// the part's socket for the wrench, the nearest body point for the welder, the cutter and the jack —, faces it and
    /// works in a pose (lean in, pour, kneel, reach up, weld, pump) with the tool or a prop in hand, sparks, arc light
    /// and ratchet sounds. The effect lands at the end (shorter with Mechanics, Salvaging for the cutter); walking off,
    /// the key again, getting in or the vehicle moving cancel it without effect. Setting off: every action is instant.</summary>
    public partial class WastelandGame
    {
        enum WorkPose { LeanIn, Pour, Kneel, Stand, Reach, GrindLow, Grind, GrindHigh, Pump, Siphon }

        sealed class WorkJob
        {
            public WorkKind kind;
            public VehicleDriver v;
            public Transform target;                 // part or socket (may be null)
            public VehiclePart part;                 // Take / Repair: the part worked on; Mount: the carried part
            public MountSocket socket;               // Take / Repair: its socket at the start; Mount: where it goes
            public VehiclePart parked;               // Mount: the carried part, held up to the socket while working
            public System.Action done;
            public Vector3 spotLocal, pointLocal, startPos, stuckAt;
            public Bounds box;                       // vehicle-local extent of every mesh on it
            public bool route, inPlace, working, cancelPending, prop, click;
            public float duration, t, age, stuckT, approachLimit, fxT, hoodAngle, movingT;
            public int startFrame, beats;
            public Controls.Act? key;
            public string label, prompt, debug;
            public WorkPose pose;
            public Transform hood;
            public GameObject groundProp;
            public bool propSet;                     // propKey overrides the kind's prop (null = the tool in hand: a fluid container)
            public string propKey;
        }

        const float WorkGap = 0.52f;                 // spot distance from the vehicle's extent (capsule radius + elbow room)
        const float HoodOpen = 55f;
        const int WeldPasses = 4, SalvageCutCount = 3;

        WorkJob job;
        bool applyingWork;
        readonly List<Transform> hoodsClosing = new List<Transform>();
        readonly List<float> hoodAngles = new List<float>();
        static Mesh oilMesh;
        static readonly Vector3[] spotCands = new Vector3[4];
        static readonly float[] spotScores = new float[4];
        static readonly Collider[] spotHits = new Collider[16];
        static readonly RaycastHit[] groundHits = new RaycastHit[16];
        static readonly List<MeshFilter> workMeshes = new List<MeshFilter>();
        static readonly List<Collider> workColliders = new List<Collider>();

        /// <summary>A timed job at a vehicle is running (walking up to it or working).</summary>
        public bool Working => job != null;
        /// <summary>Still walking to the spot.</summary>
        public bool WorkApproaching => job != null && !job.working;
        /// <summary>0..1 of the work itself (0 while walking up).</summary>
        public float WorkProgress => job == null || !job.working ? 0f : Mathf.Clamp01(job.t / Mathf.Max(0.01f, job.duration));
        public string WorkLabel => job != null ? job.label : null;
        public WorkKind? WorkNow => job != null ? job.kind : (WorkKind?)null;
        /// <summary>Seconds the work takes once at the spot.</summary>
        public float WorkDuration => job != null ? job.duration : 0f;
        /// <summary>Where the player works from / what they face (world).</summary>
        public Vector3 WorkSpot => job != null && job.v ? job.v.transform.TransformPoint(job.spotLocal) : Vector3.zero;
        public Vector3 WorkPoint => job != null && job.v ? job.v.transform.TransformPoint(job.pointLocal) : Vector3.zero;
        public WorkOutcome LastWork { get; private set; }
        public float LastWorkProgress { get; private set; }
        /// <summary>Why the last job stopped (cancel reason) or why a verb ran instantly; null after a finished job.</summary>
        public string LastWorkNote { get; private set; }
        /// <summary>How the running (or last) job was set up: spot choice, distances (automation diagnostics).</summary>
        public string WorkDebug { get; private set; }

        // ------------------------------------------------------------------ the vehicle verbs (G, K, E, repair kit, armour, LMB tools)

        /// <summary>G at a vehicle when service parts are due: oil change, air filter, plugs at the engine bay (timed). Fuel,
        /// oil and coolant top-ups go through a container (<see cref="UseCanAt"/>).</summary>
        public void ServiceVehicle(VehicleDriver v)
        {
            if (!v || !v.TryGetComponent<VehicleSystems>(out var sys)) return;
            if (Refuelling) { StopRefuel("STOPPED"); return; }
            if (!sys.CanMaintain(Inventory)) { Toast("NOTHING TO SERVICE: FLUIDS GO IN WITH A CAN"); return; }
            Timed(v, WorkKind.Service, null, () =>
            {
                Stats.Practice(Skill.Mechanics, 3f);
                string m = sys.Maintain(Inventory);                                       // oil change, filters, plugs (roadmap 19); fluids only through a container
                if (m != null) Stats.Practice(Skill.Mechanics, 4f);
                Toast($"SERVICED {Name(v)}" + (m != null ? ": " + m : ""));
                MadMax.Net.NetSession.Instance?.SendVehicleMeta(v);
            });
        }

        /// <summary>G with a wrench at a car with a loose battery lead: tightened at the engine bay.</summary>
        public void ReconnectBattery(VehicleDriver v)
        {
            if (!v || !v.TryGetComponent<VehicleSystems>(out var sys)) return;
            Timed(v, WorkKind.Battery, null, () =>
            {
                sys.Reconnect();
                Stats.Practice(Skill.Mechanics, 4f);
                MadMax.Audio.Sfx.Play("ratchet", v.transform.position, 0.6f);
                Toast("BATTERY LEAD TIGHTENED: " + Name(v) + " WILL CRANK NOW");
                MadMax.Story.Story.Note("reconnected");
                MadMax.Net.NetSession.Instance?.SendVehicleMeta(v);
            });
        }

        /// <summary>E on a part: a mounted one is unbolted with the wrench beside its socket, then carried; a loose one
        /// is picked up at once.</summary>
        public void TakePart(VehiclePart target)
        {
            if (!target) return;
            if (job != null && !applyingWork) { KeyStopsWork(); return; }
            if (target.Socket && !Holding(ItemIds.Wrench)) { Toast("EQUIP THE WRENCH"); return; }
            var owner = target.Socket ? target.Socket.GetComponentInParent<VehicleDriver>() : null;
            Timed(owner, WorkKind.Take, target.transform, () => TakeNow(target));
        }

        void TakeNow(VehiclePart target)
        {
            if (!target) return;
            if (target.Socket) WearTool(ItemIds.Wrench, 0.01f);
            var net = MadMax.Net.NetSession.Instance;
            if (target.Socket)
            {
                var owner = target.Socket.GetComponentInParent<VehicleDriver>();
                if (net) { target.netId = net.NewEntityId(); net.SendPartDetached(owner, target.Socket.name, target, net.LocalId); }
                MadMax.Audio.Sfx.Play("ratchet", target.transform.position, 0.8f);
                target.Socket.Detach(false);
                Stats.Practice(Skill.Mechanics, 6f);
                StarterNote("take");
            }
            else net?.SendPartCarried(target);
            if (target.TryGetComponent<MadMax.Net.NetReplica>(out var rep)) Destroy(rep);
            Player.Carry(target);
        }

        /// <summary>E with a carried part at a free socket: held up to it and bolted on with the wrench.</summary>
        public void MountCarried(MountSocket best)
        {
            if (job != null && !applyingWork) { KeyStopsWork(); return; }
            if (!best || !Player.Carried) return;
            Timed(best.GetComponentInParent<VehicleDriver>(), WorkKind.Mount, best.transform, () =>
            {
                var part = Player.TakeCarried();
                best.Attach(part);
                MadMax.Audio.Sfx.Play("ratchet", best.transform.position, 0.8f);
                Stats.Practice(Skill.Mechanics, 8f);
                StarterNote("mount");
                MadMax.Net.NetSession.Instance?.SendPartMounted(best.GetComponentInParent<VehicleDriver>(), best.name, part);
            });
        }

        /// <summary>LMB with the welder, the salvage cutter or the jack at a vehicle in reach: a timed job at the nearest
        /// body point (several weld passes / cuts, or jacking a vehicle on its side back over) instead of one swing.
        /// False = nothing for it here (the normal swing plays).</summary>
        public bool ToolWork()
        {
            if (!Player || Current || job != null || !Player.Tool || Player.Swinging || Player.Carried || !GameSettings.Current.workAnimation) return false;
            var tool = Player.Tool;
            WorkKind kind;
            if (tool is WelderTool) kind = WorkKind.Weld;
            else if (tool is JackTool) kind = WorkKind.Jack;
            else if (tool is MeleeTool cutter && cutter.salvage && !(tool is LightTool)) kind = WorkKind.Salvage;
            else return false;
            var chest = Player.transform.position + Vector3.up * 1.1f;
            var facing = cameraRig && cameraRig.CrosshairView ? Quaternion.Euler(0f, Player.viewYaw, 0f) * Vector3.forward : Player.transform.forward;
            VehicleDriver best = null; var bp = Vector3.zero; float bd = kind == WorkKind.Jack ? 2.4f : 1.8f;
            foreach (var v in vehicles)
            {
                if (!v || v.aiDriven || (v.transform.position - chest).sqrMagnitude > 400f) continue;
                var p = ClosestBodyPoint(v, chest, out float d);
                var to = p - chest; to.y = 0f;
                if (d > 0.8f && Vector3.Dot(to.normalized, facing) < 0.3f) continue;                // behind you: the swing goes where you face
                if (d < bd) { bd = d; best = v; bp = p; }
            }
            if (!best || !WorkAnimated(best)) return false;
            if (kind == WorkKind.Jack && best.transform.up.y > 0.7f) return false;                  // on its wheels: the swing says so
            if (kind == WorkKind.Salvage && (best.Occupied || !best.GetComponent<VehicleDamage>())) return false;
            if (kind == WorkKind.Weld && Inventory.Get(ResourceType.Scrap) <= 0) return false;       // the swing asks for rods
            var local = best.transform.InverseTransformPoint(bp);
            System.Action done;
            if (kind == WorkKind.Weld) done = () => { if (best && Player.Tool is WelderTool w) w.WeldAt(Player, best.transform.TransformPoint(local), WeldPasses); };
            else if (kind == WorkKind.Jack) done = () => { if (Player.Tool is JackTool jt) jt.Strike(Player); };
            else done = () => SalvageCuts(best, local);
            return Begin(best, kind, null, bp, done);
        }

        void SalvageCuts(VehicleDriver v, Vector3 local)
        {
            if (!v || !(Player.Tool is MeleeTool tool) || !v.TryGetComponent<VehicleDamage>(out var vd)) return;
            var p = v.transform.TransformPoint(local);
            float amount = tool.salvageAmount * Stats.SalvageYield * GameRules.Current.yield;
            int cuts = 0;
            for (int k = 0; k < SalvageCutCount; k++)
            {
                float left = vd.SalvageLeft;
                if (!vd.Salvage(p + Random.insideUnitSphere * 0.12f, amount, Player.gameObject)) break;
                cuts++;
                if (Mathf.CeilToInt(amount * 3f) >= left) break;                                     // stripped bare: the shell is gone
            }
            if (cuts == 0) { Toast("CAN'T CUT " + Name(v) + " NOW"); return; }
            MadMax.Audio.Sfx.Play("grinder", p, 0.7f, 1f, 30f, 0.3f);
            Stats.Practice(Skill.Salvaging, 3f * cuts);
            for (int k = 0; k < cuts; k++) RollLoot(0.03f);
            WearTool(tool.id, tool.wearPerHit * cuts);
        }

        /// <summary>WeldArmour: weld on / cut off at the zone first. True = started (or a running job stopped).</summary>
        bool DeferArmour(VehicleArmor a, ArmorZone z, ArmorMat m, System.Action act)
        {
            var v = a ? a.GetComponent<VehicleDriver>() : null;
            if (applyingWork) return false;
            if (job != null) { KeyStopsWork(); return true; }
            if (!WorkAnimated(v) || !Begin(v, WorkKind.Armour, null, ZonePoint(v, z), act)) return false;
            int zi = (int)z;
            job.label = m == ArmorMat.None ? "CUTTING OFF THE " + VehicleArmor.ZoneNames[zi] : "WELDING " + VehicleArmor.MatNames[(int)m] + " ON THE " + VehicleArmor.ZoneNames[zi];
            job.duration = Mathf.Max(1.2f, (m == ArmorMat.None ? 2.5f : 3.5f + Mathf.Min(3f, a.Voxels(z) / 400f)) * SkillPace(Skill.Mechanics));
            if (Menus && Menus.IsOpen) Menus.Close();
            return true;
        }

        /// <summary>UseRepairKit: patched at the part first, the kit is used at the end. True = started (or stopped).</summary>
        bool DeferRepairKit(VehicleDriver v, VehiclePart worst)
        {
            if (applyingWork || Current) return false;
            if (job != null) { KeyStopsWork(); return true; }
            if (!WorkAnimated(v) || !Begin(v, WorkKind.Repair, worst.transform, null, () => { if (Inventory.GetItem("use_repair_kit") > 0 && UseRepairKit(v)) Inventory.TakeItem("use_repair_kit"); }))
                return false;
            if (Menus && Menus.IsOpen) Menus.Close();
            return true;
        }

        // ------------------------------------------------------------------ core

        /// <summary>Start timed work at <paramref name="v"/>: walk to the spot for <paramref name="kind"/> at
        /// <paramref name="target"/> (a part or socket; null = the kind's usual place), work, then run
        /// <paramref name="done"/>. False when it can't start (in a vehicle, another job running, out of reach).</summary>
        public bool Work(VehicleDriver v, WorkKind kind, Transform target, System.Action done) => Begin(v, kind, target, null, done);

        /// <summary>Stop the running job without its effect.</summary>
        public void CancelWork(string why = "STOPPED")
        {
            var j = job;
            if (j == null) return;
            LastWork = WorkOutcome.Cancelled;
            LastWorkProgress = j.working ? Mathf.Clamp01(j.t / Mathf.Max(0.01f, j.duration)) : 0f;
            bool quiet = string.IsNullOrEmpty(why) || why[0] == '~';
            LastWorkNote = quiet ? (why ?? "").TrimStart('~') : why;
            WorkDebug = j.debug + " | cancelled at t " + j.t.ToString("0.00") + ", age " + j.age.ToString("0.00") + ": " + LastWorkNote;
            Debug.Log("[work] " + j.kind + " stopped: " + WorkDebug);
            EndJob(j);
            if (!quiet) Toast(why);
        }

        /// <summary>A verb's key while a job runs stops it — unless the job began this frame (the same press, e.g. a menu
        /// confirm, reaching another handler).</summary>
        void KeyStopsWork() { if (job != null && job.startFrame != Time.frameCount) { job.debug += " [a verb key again]"; CancelWork("STOPPED"); } }

        bool WorkAnimated(VehicleDriver v) => GameSettings.Current.workAnimation && v && Player && !Current && !Boarding && !Player.SeatedIn
            && !Player.Sitting && !Player.Swimming && !Player.Traversing && !Player.Ragdolled && !v.GetComponent<BoatModel>();

        /// <summary>Run <paramref name="done"/> now, or as timed work when the setting is on (the key again stops it).</summary>
        void Timed(VehicleDriver v, WorkKind kind, Transform target, System.Action done)
        {
            if (applyingWork) { done(); return; }
            if (job != null) { KeyStopsWork(); return; }
            if (WorkAnimated(v) && Begin(v, kind, target, null, done)) return;
            if (GameSettings.Current.workAnimation) { LastWorkNote = kind + " ran instantly: " + (v ? WorkDebug : "no vehicle"); Debug.Log("[work] " + LastWorkNote); }
            done();
        }

        float SkillPace(Skill s) => 1f / (1f + 0.08f * Stats.Level(s));

        bool Begin(VehicleDriver v, WorkKind kind, Transform target, Vector3? at, System.Action done)
        {
            if (!v || !Player || Current || job != null || Player.SeatedIn || Player.Sitting)
            {
                WorkDebug = "not started: " + (!v ? "no vehicle" : !Player ? "no player" : Current ? "in a vehicle" : job != null ? "another job" : "seated");
                return false;
            }
            var t = v.transform;
            var j = new WorkJob { kind = kind, v = v, target = target, done = done, startFrame = Time.frameCount, startPos = t.position };
            j.part = kind == WorkKind.Mount ? Player.Carried : target ? target.GetComponent<VehiclePart>() : null;
            j.socket = kind == WorkKind.Mount ? (target ? target.GetComponent<MountSocket>() : null) : j.part ? j.part.Socket : null;
            j.box = LocalBox(v);
            var point = at ?? WorkTarget(v, j);
            j.pointLocal = t.InverseTransformPoint(point);
            if (!FindSpot(v, j, point, out var spot))
            {
                if (Vector3.Distance(Player.transform.position + Vector3.up, point) > 2.6f) { WorkDebug = "not started: " + j.debug; return false; }
                j.inPlace = true;                                                                   // boxed in: work from where you stand
            }
            j.spotLocal = t.InverseTransformPoint(spot);
            var cat = j.part ? j.part.category : (PartCategory?)null;
            bool bay = kind == WorkKind.Service || kind == WorkKind.Battery || cat == PartCategory.Engine || cat == PartCategory.Radiator;
            j.pose = PoseFor(kind, cat, point.y - spot.y, bay && kind != WorkKind.Weld && kind != WorkKind.Armour && kind != WorkKind.Salvage);
            j.duration = Duration(v, kind, j.part);
            j.label = WorkName(v, kind, j.part);
            j.key = kind == WorkKind.Service || kind == WorkKind.Refuel || kind == WorkKind.Battery ? Controls.Act.Service
                  : kind == WorkKind.Siphon ? Controls.Act.Siphon : kind == WorkKind.Take || kind == WorkKind.Mount ? Controls.Act.Use : (Controls.Act?)null;
            j.click = kind == WorkKind.Weld || kind == WorkKind.Salvage || kind == WorkKind.Jack;
            j.approachLimit = 4f + Vector3.Distance(Player.transform.position, spot) * 2f;          // round the vehicle is longer than straight
            j.stuckAt = Player.transform.position;
            j.debug = $"{kind} {Name(v)}{(j.part ? " " + j.part.partId : "")}: {j.debug}, walk {Flat(Player.transform.position, spot):0.00} m{(j.inPlace ? " (in place)" : "")}, {j.duration:0.0} s";
            WorkDebug = j.debug;
            if (Refuelling) StopRefuel(null);
            Player.AutoWalk = Vector3.zero;
            job = j;
            if (!ExternalInput) Hints.Show("work_at_vehicle", "WORK TAKES A MOMENT: MOVE OR PRESS THE KEY AGAIN TO STOP (SETTINGS: WORK ANIMATION)");
            return true;
        }

        partial void AnimUpdate()
        {
            float dt = Time.deltaTime;
            CloseHoods(dt);
            var mouse = UnityEngine.InputSystem.Mouse.current; var pad = UnityEngine.InputSystem.Gamepad.current;
            bool click = !ExternalInput && ((mouse != null && mouse.leftButton.wasPressedThisFrame && !LootOverlay.ConsumesMouse) || (pad != null && pad.rightTrigger.wasPressedThisFrame));
            if (job == null)
            {
                // LMB with a welder / cutter / jack at a vehicle: timed work there (the swing that would follow is held off)
                if (click && !Current && cameraRig && !(Build && Build.Active) && !RadialOpen && !Aiming) ToolWork();
                return;
            }
            var j = job;
            j.age += dt;
            string why = WorkBroken(j, click);
            if (why != null) { CancelWork(why); return; }
            if (!j.working) Approach(j, dt);
            else Labour(j, dt);
            if (job != j) return;
            if (j.prompt == null) j.prompt = j.label + (j.key.HasValue ? "   [" + Controls.Name(j.key.Value) + "] STOP" : "   MOVE TO STOP");
            Prompt = j.prompt;
        }

        /// <summary>Why the job can't go on (null = fine; a leading '~' = stop without a toast).</summary>
        string WorkBroken(WorkJob j, bool click)
        {
            if (j.cancelPending) return "STOPPED";
            if (!Player || !j.v) return "~GONE";
            if (Current || Player.SeatedIn || Player.Sitting || Player.Ragdolled || (Vitals && Vitals.Dead)) return "~PLAYER " + (Current ? "DRIVING" : Player.SeatedIn || Player.Sitting ? "SEATED" : "DOWN");
            // driven or pushed off (not a bounce on the springs after a part change): horizontal drift or sustained speed
            var body = j.v.Body;
            var vel = body && !body.isKinematic ? body.linearVelocity : Vector3.zero; vel.y = 0f;
            j.movingT = vel.sqrMagnitude > 2.25f ? j.movingT + Time.deltaTime : 0f;
            if (j.movingT > 0.25f || Flat(j.v.transform.position, j.startPos) > 0.5f) return "THE " + Name(j.v) + " MOVED";
            if (j.age > 0.3f && Player.moveInput.sqrMagnitude > 0.1f) { j.debug += " [walked off]"; return "STOPPED"; }
            if (Time.frameCount > j.startFrame)
            {
                if (j.key.HasValue && Controls.Down(j.key.Value)) { j.debug += " [" + j.key.Value + " again]"; return "STOPPED"; }
                if (j.click && click) { j.cancelPending = true; return null; }                       // next frame: this click's swing stays held off
            }
            switch (j.kind)
            {
                case WorkKind.Take:
                case WorkKind.Repair:
                    if (!j.part || j.part.Socket != j.socket) return "THE PART IS GONE";
                    break;
                case WorkKind.Mount:
                    if (!j.socket || !j.socket.IsFree) return "THE SOCKET IS TAKEN";
                    if (!j.parked && !Player.Carried) return "~THE PART LEFT THE HANDS";
                    break;
            }
            if (j.working && !j.inPlace && Flat(Player.transform.position, SpotWorld(j)) > 1.5f) { j.debug += " [pushed off the spot]"; return "STOPPED"; }
            return null;
        }

        Vector3 SpotWorld(WorkJob j) => j.v.transform.TransformPoint(j.spotLocal);
        Vector3 PointWorld(WorkJob j) => j.v.transform.TransformPoint(j.pointLocal);
        static float Flat(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }

        // ------------------------------------------------------------------ walking up

        void Approach(WorkJob j, float dt)
        {
            if (j.inPlace || Player.Interior) { StartLabour(j); return; }
            var t = j.v.transform;
            var feet = Player.transform.position;
            float dist = Flat(feet, SpotWorld(j));
            if (dist < 0.2f) { StartLabour(j); return; }
            if (Flat(feet, j.stuckAt) > 0.1f) { j.stuckAt = feet; j.stuckT = 0f; } else j.stuckT += dt;   // walking into something
            if (j.stuckT > 1.2f || j.age > j.approachLimit)
            {
                // blocked on the way: work from here if the job is within reach, else give up
                if (dist < 1f || Vector3.Distance(feet + Vector3.up, PointWorld(j)) < 1.9f) { j.inPlace = true; StartLabour(j); }
                else CancelWork("CAN'T GET TO THE " + (j.part ? PartName(j.part) : Name(j.v)));
                return;
            }
            var pl = t.InverseTransformPoint(feet);
            var a = new Vector2(pl.x, pl.z);
            var s = new Vector2(j.spotLocal.x, j.spotLocal.z);
            var goal = s;
            bool direct = !j.route || !RouteAround(a, s, j.box, out goal);
            var d = t.TransformDirection(new Vector3(goal.x - a.x, 0f, goal.y - a.y));
            d.y = 0f;
            float speed = direct ? Mathf.Clamp(dist / 0.6f, 0.3f, 1f) : 1f;                          // ease into the spot
            Player.AutoWalk = d.sqrMagnitude > 1e-6f ? d.normalized * speed : Vector3.zero;
        }

        /// <summary>Round the vehicle's extent (vehicle-local x/z): out of it first when hugging it, then via the corner
        /// that makes the shortest way when the straight line crosses it. False = straight there.</summary>
        static bool RouteAround(Vector2 a, Vector2 s, Bounds b, out Vector2 goal)
        {
            goal = s;
            var inMin = new Vector2(b.min.x - 0.3f, b.min.z - 0.3f); var inMax = new Vector2(b.max.x + 0.3f, b.max.z + 0.3f);
            if (!SegmentHitsBox(a, s, inMin, inMax)) return false;
            if (a.x > inMin.x && a.x < inMax.x && a.y > inMin.y && a.y < inMax.y)
            {
                // against the body: step straight out through the nearest face
                float l = a.x - inMin.x, r = inMax.x - a.x, bk = a.y - inMin.y, f = inMax.y - a.y, m = Mathf.Min(Mathf.Min(l, r), Mathf.Min(bk, f));
                goal = m == l ? new Vector2(inMin.x - 0.15f, a.y) : m == r ? new Vector2(inMax.x + 0.15f, a.y) : m == bk ? new Vector2(a.x, inMin.y - 0.15f) : new Vector2(a.x, inMax.y + 0.15f);
                return true;
            }
            var vMin = inMin + Vector2.one * 0.05f; var vMax = inMax - Vector2.one * 0.05f;
            float best = float.MaxValue; bool found = false;
            for (int k = 0; k < 4; k++)
            {
                var c = new Vector2((k & 1) != 0 ? b.max.x + 0.55f : b.min.x - 0.55f, (k & 2) != 0 ? b.max.z + 0.55f : b.min.z - 0.55f);
                if ((c - a).sqrMagnitude < 0.04f || SegmentHitsBox(a, c, vMin, vMax)) continue;       // standing on it / behind the body
                float cost = (c - a).magnitude + (s - c).magnitude;
                if (cost < best) { best = cost; goal = c; found = true; }
            }
            return found;
        }

        static bool SegmentHitsBox(Vector2 a, Vector2 b, Vector2 min, Vector2 max)
        {
            float t0 = 0f, t1 = 1f;
            var d = b - a;
            bool Clip(float p, float q)
            {
                if (Mathf.Abs(p) < 1e-6f) return q >= 0f;
                float r = q / p;
                if (p < 0f) { if (r > t1) return false; if (r > t0) t0 = r; }
                else { if (r < t0) return false; if (r < t1) t1 = r; }
                return true;
            }
            return Clip(-d.x, a.x - min.x) && Clip(d.x, max.x - a.x) && Clip(-d.y, a.y - min.y) && Clip(d.y, max.y - a.y) && t0 <= t1;
        }

        // ------------------------------------------------------------------ working

        void StartLabour(WorkJob j)
        {
            j.working = true; j.t = 0f; j.fxT = 0f; j.beats = 0;
            Player.AutoWalk = Vector3.zero;
            Face(j, 1f);
            string propKey = WorkPropKey(j);
            var mesh = WorkPropMesh(propKey);
            bool hdProp = MadMax.Rendering.HDBits.On && (propKey == "oil" || propKey == "can");
            if (mesh) { Player.HoldProp(mesh, hdProp ? MadMax.Rendering.HDShapes.Solid : propMaterial); j.prop = true; }
            MadMax.Net.NetSession.Instance?.SendWorkPose(true, (byte)j.pose, mesh ? propKey : null);   // remote peers see the pose
            var cat = j.part ? j.part.category : (PartCategory?)null;
            if (j.kind == WorkKind.Service || j.kind == WorkKind.Battery || ((j.kind == WorkKind.Take || j.kind == WorkKind.Mount || j.kind == WorkKind.Repair) && (cat == PartCategory.Engine || cat == PartCategory.Radiator)))
            {
                j.hood = HoodOver(j);
                if (j.hood) MadMax.Audio.Sfx.Play("door_open", j.hood.position, 0.45f, 0.8f, 20f, 0.2f);
            }
            if (j.kind == WorkKind.Mount && Player.Carried)
            {
                // the part is offered up to its socket and held there while it's bolted on
                j.parked = Player.TakeCarried();
                j.parked.transform.SetParent(null, true);
                foreach (var c in j.parked.GetComponentsInChildren<Collider>(true)) c.enabled = false;   // held in the body: never a static collider in it
                Park(j);
                MadMax.Audio.Sfx.Play("hit_metal", j.parked.transform.position, 0.35f, 0.8f, 20f, 0.2f);
            }
            if ((j.kind == WorkKind.Take || j.kind == WorkKind.Mount) && cat == PartCategory.Wheel && Inventory.GetItem("tool_jack") > 0) j.groundProp = JackProp(j);
            if (j.duration <= 0f) Finish(j);
        }

        void Labour(WorkJob j, float dt)
        {
            j.t += dt;
            Face(j, dt);
            Player.PoseOverride = PoseAt(j.pose, j.t);
            Player.ActionClip = j.pose == WorkPose.LeanIn ? "wrench" : j.pose == WorkPose.Pour ? "pour" : j.pose == WorkPose.Kneel ? "weld" : null;
            if (j.parked) Park(j);
            if (j.hood)
            {
                if (!j.hood.parent || !j.hood.parent.GetComponent<MountSocket>()) j.hood = null;
                else { j.hoodAngle = Mathf.MoveTowards(j.hoodAngle, HoodOpen, dt * 150f); j.hood.localRotation = Quaternion.Euler(-j.hoodAngle, 0f, 0f); }
            }
            WorkEffects(j, dt);
            if (j.t >= j.duration) Finish(j);
        }

        void Face(WorkJob j, float dt)
        {
            if (Player.Interior) return;
            var look = PointWorld(j) - Player.transform.position; look.y = 0f;
            if (look.sqrMagnitude > 0.01f) Player.transform.rotation = Quaternion.Slerp(Player.transform.rotation, Quaternion.LookRotation(look), 1f - Mathf.Exp(-12f * dt));
        }

        void Park(WorkJob j)
        {
            var s = j.socket.transform;
            j.parked.transform.SetPositionAndRotation(s.position, s.rotation);
            j.parked.transform.localScale = new Vector3(s.lossyScale.x < 0f ? -1f : 1f, 1f, 1f);   // left sockets mirror the part
        }

        void Finish(WorkJob j)
        {
            LastWork = WorkOutcome.Done; LastWorkProgress = 1f; LastWorkNote = null;
            var done = j.done;
            EndJob(j);
            applyingWork = true;
            try { done?.Invoke(); }
            finally { applyingWork = false; }
        }

        void EndJob(WorkJob j)
        {
            if (job == j) job = null;
            if (Player)
            {
                Player.AutoWalk = null;
                if (j.working) { Player.PoseOverride = null; Player.ActionClip = null; MadMax.Net.NetSession.Instance?.SendWorkPose(false, 0, null); }
                if (j.prop) Player.DropProp();
                if (j.parked)
                {
                    if (!Player.SeatedIn && !Player.Ragdolled) Player.Carry(j.parked);                // back in the arms (mounting takes it from there)
                    else
                    {
                        j.parked.transform.localScale = Vector3.one;
                        foreach (var c in j.parked.GetComponentsInChildren<Collider>()) c.enabled = true;
                        if (!j.parked.GetComponent<Rigidbody>()) j.parked.gameObject.AddComponent<Rigidbody>().mass = j.parked.mass;
                    }
                }
            }
            if (j.hood && j.hoodAngle > 0.5f)
            {
                hoodsClosing.Add(j.hood); hoodAngles.Add(j.hoodAngle);
                MadMax.Audio.Sfx.Play("door_close", j.hood.position, 0.45f, 0.85f, 20f, 0.2f);
            }
            if (j.groundProp) Destroy(j.groundProp);
        }

        void CloseHoods(float dt)
        {
            for (int i = hoodsClosing.Count - 1; i >= 0; i--)
            {
                var h = hoodsClosing[i];
                if (!h || !h.parent || !h.parent.GetComponent<MountSocket>() || (job != null && job.hood == h)) { hoodsClosing.RemoveAt(i); hoodAngles.RemoveAt(i); continue; }
                float a = Mathf.MoveTowards(hoodAngles[i], 0f, dt * 140f);
                h.localRotation = Quaternion.Euler(-a, 0f, 0f);
                if (a <= 0f) { hoodsClosing.RemoveAt(i); hoodAngles.RemoveAt(i); }
                else hoodAngles[i] = a;
            }
        }

        /// <summary>Sparks and the arc for the welder, grinder sparks for the cutter, ratchet clicks for the wrench, the
        /// pump for the jack, pouring for fluids.</summary>
        void WorkEffects(WorkJob j, float dt)
        {
            var p = FxPoint(j);
            float period = 0.63f;
            switch (j.kind)
            {
                case WorkKind.Weld:
                case WorkKind.Armour:
                    MadMax.Audio.Sfx.Loop(Player, "sizzle", 0.7f, 1.4f, 30f);
                    if ((j.fxT -= dt) <= 0f)
                    {
                        j.fxT = 0.06f;
                        Fx.Sparks(p, Vector3.up + Random.insideUnitSphere * 0.6f, 3, new Color(1f, 0.9f, 0.6f));
                        Fx.Flash(p + Vector3.up * 0.1f, new Color(0.75f, 0.85f, 1f), 4f, Random.Range(2.5f, 6f), 0.08f);
                    }
                    return;
                case WorkKind.Salvage:
                    MadMax.Audio.Sfx.Loop(Player, "grinder", 0.75f, 1f, 35f);
                    if ((j.fxT -= dt) <= 0f)
                    {
                        j.fxT = 0.05f;
                        var away = Player.transform.position - p; away.y = 0f;
                        Fx.Sparks(p, Vector3.up * 0.6f + Vector3.Cross(away.normalized, Vector3.up), 4, new Color(1f, 0.72f, 0.3f));
                    }
                    return;
                case WorkKind.Service:
                case WorkKind.Siphon:
                    if (j.t > 0.5f && j.t < j.duration - 0.3f) MadMax.Audio.Sfx.Loop(Player, "pour", 0.45f, j.kind == WorkKind.Siphon ? 0.8f : 1f, 20f);
                    return;
                case WorkKind.Jack:
                    period = 0.9f;
                    break;
            }
            int beat = Mathf.FloorToInt(j.t / period);
            if (beat == j.beats) return;
            j.beats = beat;
            if (j.kind == WorkKind.Jack) MadMax.Audio.Sfx.Play("hydraulic", p, 0.5f, 1.25f, 25f, 0.3f);
            else
            {
                MadMax.Audio.Sfx.Play("ratchet", p, 0.45f, Random.Range(0.9f, 1.15f), 20f, 0.2f);
                if (beat % 3 == 2) MadMax.Audio.Sfx.Play("hit_metal", p, 0.18f, 1.5f, 15f, 0.2f);
            }
        }

        /// <summary>Where the effects go: the tool's tip when it is at the work, else the work point.</summary>
        Vector3 FxPoint(WorkJob j)
        {
            var point = PointWorld(j);
            var tip = Player.Tool is WelderTool w ? w.tip : Player.Tool is MeleeTool m ? m.tip : null;
            return !j.prop && tip && tip.gameObject.activeInHierarchy && Vector3.Distance(tip.position, point) < 0.8f ? tip.position : point;
        }

        // ------------------------------------------------------------------ where, how long, which pose

        /// <summary>The point worked on (world): the engine bay, the filler, the part, the socket, else the body point
        /// nearest the player.</summary>
        Vector3 WorkTarget(VehicleDriver v, WorkJob j)
        {
            switch (j.kind)
            {
                case WorkKind.Service:
                case WorkKind.Battery:
                    return EngineBay(v, j.box);
                case WorkKind.Refuel:
                case WorkKind.Siphon:
                    return Filler(v, Player.transform.position);
                case WorkKind.Take:
                case WorkKind.Repair:
                    if (j.part) return j.part.TryGetComponent<Renderer>(out var r) ? r.bounds.center : j.part.transform.position;
                    break;
                case WorkKind.Mount:
                    if (j.socket) return j.socket.transform.position;
                    break;
            }
            if (j.target) return j.target.position;
            return ClosestBodyPoint(v, Player.transform.position + Vector3.up * 1.1f, out _);
        }

        static Vector3 EngineBay(VehicleDriver v, Bounds b)
        {
            if (v.TryGetComponent<VehicleChassis>(out var ch))
            {
                Transform radiator = null;
                foreach (var s in ch.Sockets)
                {
                    if (s.accepts == PartCategory.Engine) return s.transform.position;
                    if (s.accepts == PartCategory.Radiator) radiator = s.transform;
                }
                if (radiator) return radiator.position;
            }
            return v.transform.TransformPoint(new Vector3(b.center.x, b.center.y, b.max.z - 0.4f));
        }

        /// <summary>Armour zone to weld at (world): the face of the vehicle's extent, the roof, the wheel or window nearest
        /// the player.</summary>
        Vector3 ZonePoint(VehicleDriver v, ArmorZone z)
        {
            var t = v.transform; var b = LocalBox(v);
            var pl = t.InverseTransformPoint(Player.transform.position);
            float mid = Mathf.Lerp(b.min.y, b.max.y, 0.45f);
            float side = pl.x >= b.center.x ? b.max.x : b.min.x;
            switch (z)
            {
                case ArmorZone.Front: return t.TransformPoint(new Vector3(b.center.x, mid, b.max.z));
                case ArmorZone.Rear: return t.TransformPoint(new Vector3(b.center.x, mid, b.min.z));
                case ArmorZone.Left: return t.TransformPoint(new Vector3(b.min.x, mid, b.center.z));
                case ArmorZone.Right: return t.TransformPoint(new Vector3(b.max.x, mid, b.center.z));
                case ArmorZone.Roof: return t.TransformPoint(new Vector3(b.center.x, b.max.y, b.center.z));
                case ArmorZone.Windows:
                    var eye = t.Find("DriverEye");
                    return t.TransformPoint(new Vector3(side, b.max.y - 0.35f, eye ? eye.localPosition.z : b.center.z));
            }
            // wheels: the nearest one
            Vector3 best = t.TransformPoint(new Vector3(side, b.min.y + 0.35f, b.center.z)); float bd = float.MaxValue;
            if (v.TryGetComponent<VehicleChassis>(out var ch))
                foreach (var s in ch.Sockets)
                {
                    if (s.accepts != PartCategory.Wheel || !s.Current) continue;
                    float d = (s.transform.position - Player.transform.position).sqrMagnitude;
                    if (d < bd) { bd = d; best = s.Current.TryGetComponent<Renderer>(out var r) ? r.bounds.center : s.transform.position; }
                }
            return best;
        }

        /// <summary>The spot to work from: beside the vehicle's extent at the edge nearest the point (the engine end for
        /// the engine bay), on the ground, clear of other things; straight out from the point for a vehicle on its side.</summary>
        bool FindSpot(VehicleDriver v, WorkJob j, Vector3 point, out Vector3 spot)
        {
            var t = v.transform;
            var feet = Player.transform.position;
            spot = feet;
            if (Player.Interior) { j.inPlace = true; j.debug = "inside"; return true; }
            if (t.up.y < 0.5f)
            {
                var away = feet - point; away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = Vector3.ProjectOnPlane(-t.forward, Vector3.up);
                var w = Ground(point + away.normalized * 0.65f, v, point.y);
                bool clear = Clear(w, out var blocker);
                j.debug = "on its side, spot " + (clear ? "clear" : "blocked by " + blocker);
                if (!clear) return false;
                spot = w;
                return true;
            }
            j.route = true;
            var b = j.box;
            var lp = t.InverseTransformPoint(point); var pl = t.InverseTransformPoint(feet);
            float zc = Mathf.Clamp(lp.z, b.min.z + 0.25f, b.max.z - 0.25f), xc = Mathf.Clamp(lp.x, b.min.x + 0.2f, b.max.x - 0.2f);
            spotCands[0] = new Vector3(b.max.x + WorkGap, b.min.y, zc);
            spotCands[1] = new Vector3(b.min.x - WorkGap, b.min.y, zc);
            spotCands[2] = new Vector3(xc, b.min.y, b.max.z + WorkGap);
            spotCands[3] = new Vector3(xc, b.min.y, b.min.z - WorkGap);
            var cat = j.part ? j.part.category : (PartCategory?)null;
            bool bay = j.kind == WorkKind.Service || j.kind == WorkKind.Battery || cat == PartCategory.Engine || cat == PartCategory.Radiator;
            int end = bay ? (lp.z >= b.center.z ? 2 : 3) : -1;                                   // over the engine bay: from its end
            for (int i = 0; i < 4; i++)
            {
                var c = spotCands[i];
                spotScores[i] = Vector2.Distance(new Vector2(lp.x, lp.z), new Vector2(c.x, c.z)) + 0.25f * Vector2.Distance(new Vector2(pl.x, pl.z), new Vector2(c.x, c.z)) + (i == end ? -20f : 0f);
            }
            float groundRef = t.TransformPoint(new Vector3(0f, b.min.y, 0f)).y;
            int first = -1;
            var sb = new System.Text.StringBuilder("spots");
            for (int n = 0; n < 4; n++)
            {
                int best = -1;
                for (int i = 0; i < 4; i++) if (spotScores[i] < float.MaxValue && (best < 0 || spotScores[i] < spotScores[best])) best = i;
                if (best < 0) break;
                spotScores[best] = float.MaxValue;
                if (first < 0) first = best;
                var w = Ground(t.TransformPoint(spotCands[best]), v, groundRef);
                bool clear = Clear(w, out var blocker);
                sb.Append(' ').Append(SpotNames[best]).Append(clear ? " clear" : " blocked by " + blocker);
                if (clear) { spot = w; j.debug = sb.ToString(); return true; }
            }
            // nothing clear (a loose part, a crate): head for the best side anyway, the walk gives up or works from there
            spot = Ground(t.TransformPoint(spotCands[first]), v, groundRef);
            j.debug = sb.Append(", going for ").Append(SpotNames[first]).ToString();
            return true;
        }

        static readonly string[] SpotNames = { "right", "left", "front", "rear" };

        /// <summary>The walkable surface under <paramref name="p"/> (floor, deck or terrain), not the vehicle itself.</summary>
        Vector3 Ground(Vector3 p, VehicleDriver v, float reference)
        {
            float y = terrain ? terrain.Height(p.x, p.z) : reference;
            var from = new Vector3(p.x, Mathf.Max(reference, y) + 1.2f, p.z);
            int n = Physics.RaycastNonAlloc(from, Vector3.down, groundHits, 6f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = groundHits[i].collider;
                if (c.transform.IsChildOf(v.transform) || c.transform.IsChildOf(Player.transform)) continue;
                if (groundHits[i].distance < best) { best = groundHits[i].distance; y = groundHits[i].point.y; }
            }
            return new Vector3(p.x, y, p.z);
        }

        bool Clear(Vector3 w, out string blocker)
        {
            blocker = null;
            int n = Physics.OverlapCapsuleNonAlloc(w + Vector3.up * 0.45f, w + Vector3.up * 1.55f, 0.26f, spotHits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = spotHits[i];
                if (c.transform.IsChildOf(Player.transform) || c.name.StartsWith("Chunk") || (job != null && job.parked && c.transform.IsChildOf(job.parked.transform))) continue;
                blocker = c.name + " (" + c.transform.root.name + ")";
                return false;
            }
            return true;
        }

        /// <summary>Vehicle-local extent of every mesh on the vehicle (body, parts, pieces built on it).</summary>
        Bounds LocalBox(VehicleDriver v)
        {
            var inv = v.transform.worldToLocalMatrix;
            var b = new Bounds(); bool any = false;
            v.GetComponentsInChildren(false, workMeshes);
            foreach (var mf in workMeshes)
            {
                if (!mf.sharedMesh || mf.name == "Beam" || mf.name == "SoilHeap" || (Player && mf.transform.IsChildOf(Player.transform))) continue;   // not the light cones
                var mb = mf.sharedMesh.bounds;
                var m = inv * mf.transform.localToWorldMatrix;
                for (int k = 0; k < 8; k++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) != 0 ? 1f : -1f, (k & 2) != 0 ? 1f : -1f, (k & 4) != 0 ? 1f : -1f));
                    var p = m.MultiplyPoint3x4(c);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            workMeshes.Clear();
            // and its colliders (a body box may reach past the meshes)
            v.GetComponentsInChildren(false, workColliders);
            foreach (var c in workColliders)
            {
                if (!c.enabled || c.isTrigger || (Player && c.transform.IsChildOf(Player.transform))) continue;
                Vector3 centre, ext; Matrix4x4 m;
                if (c is BoxCollider bc) { centre = bc.center; ext = bc.size * 0.5f; m = inv * c.transform.localToWorldMatrix; }
                else if (c is MeshCollider mc && mc.sharedMesh) { centre = mc.sharedMesh.bounds.center; ext = mc.sharedMesh.bounds.extents; m = inv * c.transform.localToWorldMatrix; }
                else { centre = c.bounds.center; ext = c.bounds.extents; m = inv; }
                for (int k = 0; k < 8; k++)
                {
                    var p = m.MultiplyPoint3x4(centre + Vector3.Scale(ext, new Vector3((k & 1) != 0 ? 1f : -1f, (k & 2) != 0 ? 1f : -1f, (k & 4) != 0 ? 1f : -1f)));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            workColliders.Clear();
            return any ? b : new Bounds(new Vector3(0f, 0.75f, 0f), new Vector3(2f, 1.5f, 4.5f));
        }

        /// <summary>The point of the vehicle's colliders nearest <paramref name="from"/>.</summary>
        Vector3 ClosestBodyPoint(VehicleDriver v, Vector3 from, out float dist)
        {
            var best = v.transform.position; dist = float.MaxValue;
            v.GetComponentsInChildren(false, workColliders);
            foreach (var c in workColliders)
            {
                if (!c.enabled || c.isTrigger || (Player && c.transform.IsChildOf(Player.transform))) continue;
                var p = c is MeshCollider mc && !mc.convex ? c.bounds.ClosestPoint(from) : c.ClosestPoint(from);
                float d = Vector3.Distance(p, from);
                if (d < dist) { dist = d; best = p; }
            }
            workColliders.Clear();
            return best;
        }

        Transform HoodOver(WorkJob j)
        {
            if (!j.v.TryGetComponent<VehicleChassis>(out var ch)) return null;
            var t = j.v.transform; float cz = j.box.center.z;
            foreach (var s in ch.Sockets)
            {
                if (s.accepts != PartCategory.Hood || !s.Current || s.Current == j.part) continue;
                float hz = t.InverseTransformPoint(s.transform.position).z - cz;
                if (Mathf.Sign(hz) == Mathf.Sign(j.pointLocal.z - cz)) return s.Current.transform;      // the hood over the end being worked on
            }
            return null;
        }

        float Duration(VehicleDriver v, WorkKind kind, VehiclePart p)
        {
            float d;
            switch (kind)
            {
                case WorkKind.Refuel: return 0f;                                                        // the pour times itself
                case WorkKind.Service: d = 3.2f + (v.TryGetComponent<VehicleSystems>(out var sys) && sys.CanMaintain(Inventory) ? 1.2f : 0f); break;
                case WorkKind.Siphon: d = 2.6f; break;
                case WorkKind.Battery: d = 2.2f; break;
                case WorkKind.Repair: d = 3.5f; break;
                case WorkKind.Weld: d = 2.8f; break;
                case WorkKind.Armour: d = 3.5f; break;
                case WorkKind.Salvage: d = 2.7f; break;
                case WorkKind.Jack: d = 3.2f; break;
                default:
                    d = !p ? 2f : p.category == PartCategory.Wheel ? 2.6f + (p.sizeClass >= 3 ? 1.2f : 0f) : p.category == PartCategory.Engine ? 6f
                      : p.category == PartCategory.Radiator ? 3.5f : p.category == PartCategory.Door || p.category == PartCategory.Hood ? 3f : 1.2f + 0.5f * p.sizeClass;
                    if (kind == WorkKind.Mount) d *= 1.1f;
                    break;
            }
            return Mathf.Max(1.2f, d * SkillPace(kind == WorkKind.Salvage ? Skill.Salvaging : Skill.Mechanics));
        }

        static string PartName(VehiclePart p) => p ? p.partId.Replace('_', ' ').ToUpperInvariant() : "PART";

        static string WorkName(VehicleDriver v, WorkKind kind, VehiclePart p)
        {
            switch (kind)
            {
                case WorkKind.Service: return "SERVICING " + Name(v);
                case WorkKind.Refuel: return "TO THE FILLER";
                case WorkKind.Siphon: return "SIPHONING " + Name(v);
                case WorkKind.Battery: return "TIGHTENING THE BATTERY LEAD";
                case WorkKind.Take: return "UNBOLTING " + PartName(p);
                case WorkKind.Mount: return "BOLTING ON " + PartName(p);
                case WorkKind.Repair: return "PATCHING " + PartName(p);
                case WorkKind.Weld: return "WELDING " + Name(v);
                case WorkKind.Salvage: return "CUTTING UP " + Name(v);
                case WorkKind.Jack: return "JACKING " + Name(v) + " OVER";
            }
            return "WELDING " + Name(v);
        }

        static WorkPose PoseFor(WorkKind kind, PartCategory? cat, float height, bool bay)
        {
            switch (kind)
            {
                case WorkKind.Service: return WorkPose.Pour;
                case WorkKind.Battery: return WorkPose.LeanIn;
                case WorkKind.Siphon: return WorkPose.Siphon;
                case WorkKind.Jack: return WorkPose.Pump;
                case WorkKind.Weld:
                case WorkKind.Armour:
                case WorkKind.Salvage:
                    return height < 0.7f ? WorkPose.GrindLow : height < 1.5f ? WorkPose.Grind : WorkPose.GrindHigh;
            }
            if (bay) return WorkPose.LeanIn;
            if (cat == PartCategory.Wheel) return WorkPose.Kneel;
            return height < 0.7f ? WorkPose.Kneel : height < 1.5f ? WorkPose.Stand : WorkPose.Reach;
        }

        /// <summary>The working pose at <paramref name="t"/> seconds: ratchet wrist turns, a slow sway, grinder jitter.</summary>
        static ToolPose PoseAt(WorkPose pose, float t)
        {
            float ratchet = Mathf.Sin(t * 10f), slow = Mathf.Sin(t * 2.3f);
            ToolPose p;
            switch (pose)
            {
                case WorkPose.LeanIn:     // bent over the engine bay, both hands in it
                    p = new ToolPose { chestX = 40f + slow * 3f, chestY = slow * 4f, armRX = -8f + ratchet * 5f, foreR = -28f, handRZ = ratchet * 45f, armLX = -4f, armLZ = 8f, foreL = -22f, knees = 10f };
                    break;
                case WorkPose.Pour:       // leaning in, the jug tipped into the filler, the other hand on the wing
                    p = new ToolPose { chestX = 36f, chestY = -6f, armRX = -14f + slow * 3f, armRZ = 6f, foreR = -40f, handRZ = 70f + slow * 8f, armLX = -6f, armLZ = -4f, foreL = -15f, knees = 10f };
                    break;
                case WorkPose.Kneel:      // down at a wheel or a low part
                    p = new ToolPose { chestX = 42f + slow * 3f, armRX = 10f + ratchet * 4f, foreR = -35f, handRZ = ratchet * 45f, armLX = 6f, armLZ = 10f, foreL = -38f, knees = 108f };
                    break;
                case WorkPose.Reach:      // arms up at a roof part
                    p = new ToolPose { chestX = -6f, armRX = -150f + ratchet * 5f, foreR = -24f, handRZ = ratchet * 45f, armLX = -142f, armLZ = 10f, foreL = -30f, knees = 0f };
                    break;
                case WorkPose.GrindLow:
                    p = new ToolPose { chestX = 40f, armRX = 8f + slow * 4f, armRY = slow * 10f, foreR = -40f, armLX = 4f, armLZ = 24f, foreL = -48f, knees = 104f };
                    break;
                case WorkPose.Grind:
                    p = new ToolPose { chestX = 22f, armRX = -40f + slow * 4f, armRY = slow * 10f, foreR = -35f, armLX = -36f, armLZ = 26f, foreL = -45f, knees = 20f };
                    break;
                case WorkPose.GrindHigh:
                    p = new ToolPose { chestX = -4f, armRX = -120f + slow * 4f, armRY = slow * 8f, foreR = -25f, armLX = -114f, armLZ = 24f, foreL = -30f, knees = 0f };
                    break;
                case WorkPose.Pump:       // crouched at the jack, working its handle
                    float pump = 0.5f + 0.5f * Mathf.Sin(t * 7f);
                    p = new ToolPose { chestX = 34f + pump * 6f, armRX = 4f - pump * 40f, foreR = -30f + pump * 15f, armLX = -4f, armLZ = 10f, foreL = -40f, knees = 106f };
                    break;
                case WorkPose.Siphon:     // crouched at the filler with the can, drawing on the hose
                    p = new ToolPose { chestX = 26f + slow * 2f, armRX = -10f, foreR = -55f, handRZ = 20f, armLX = -6f, armLZ = 8f, foreL = -70f + slow * 8f, knees = 100f };
                    break;
                default:                  // standing at a door, a bumper, a lamp
                    p = new ToolPose { chestX = 16f + slow * 2f, armRX = -34f + ratchet * 5f, foreR = -30f, handRZ = ratchet * 45f, armLX = -30f, armLZ = 10f, foreL = -36f, knees = 14f };
                    break;
            }
            if (pose == WorkPose.GrindLow || pose == WorkPose.Grind || pose == WorkPose.GrindHigh)
            {
                float jitter = Mathf.Sin(Time.time * 90f) * 1.6f;                                      // the tool bucks in the hands
                p.armRX += jitter; p.armLX += jitter; p.chestX += jitter * 0.3f;
            }
            return p;
        }

        // ------------------------------------------------------------------ props

        /// <summary>What goes in the hand for the job: "oil" (jug), "can" (jerry can) or a tool id; null = the tool in hand.</summary>
        string WorkPropKey(WorkJob j)
        {
            if (j.propSet) return j.propKey;
            switch (j.kind)
            {
                case WorkKind.Service: return "oil";
                case WorkKind.Siphon: return "can";
                case WorkKind.Battery:
                case WorkKind.Repair:
                case WorkKind.Take:
                case WorkKind.Mount:
                    return Holding(ItemIds.Wrench) ? null : ItemIds.Wrench;
                case WorkKind.Armour:
                    return Player.Tool is WelderTool ? null : "tool_welder";
            }
            return null;                                                                             // welder, cutter, jack: the tool in hand
        }

        /// <summary>The prop mesh for a <see cref="WorkPropKey"/> (also for remote players' avatars).</summary>
        public static Mesh WorkPropMesh(string key) => string.IsNullOrEmpty(key) ? null : key == "oil" ? OilJug() : key == "can" ? JerryCan() : ToolLibrary.MeshFor(key);

        /// <summary>The working pose by index (<see cref="WorkPose"/>) at <paramref name="t"/> seconds, for remote avatars.</summary>
        public static ToolPose WorkPoseAt(byte pose, float t) => PoseAt((WorkPose)Mathf.Min(pose, (byte)WorkPose.Siphon), t);

        static Mesh OilJug()
        {
            if (MadMax.Rendering.HDBits.On) return MadMax.Rendering.HDBits.OilJug();
            if (oilMesh) return oilMesh;
            var g = new VoxelGrid();
            g.Box(-1, -6, -1, 1, -1, 1, Pal.Weathered(Pal.Ochre, 0.15f, 1410, 2, 0));               // plastic jug
            g.Box(-1, -4, 1, 1, -3, 1, Pal.Ramp(Pal.Cream, 2));                                       // label
            g.Box(-1, 0, 0, 1, 0, 0, Pal.Ramp(Pal.Black, 1));                                         // handle
            g.Set(1, 1, 0, Pal.Ramp(Pal.Black, 2));                                                   // spout
            g.Bevel();
            return oilMesh = VoxelMesher.Build(g, "OilJug");
        }

        static Mesh JerryCan()
        {
            if (MadMax.Rendering.HDBits.On) return MadMax.Rendering.HDBits.JerryCan();
            if (canMesh) return canMesh;
            var g = new VoxelGrid();
            g.Box(-2, -7, -1, 2, 0, 1, Pal.Weathered(Pal.Olive, 0.3f, 1400, 2, 0));
            g.Box(-1, 1, 0, 1, 1, 0, Pal.Ramp(Pal.Black, 1)); g.Box(2, 0, 0, 3, 2, 0, Pal.Ramp(Pal.Metal, 2));   // handle, spout
            g.Bevel();
            return canMesh = VoxelMesher.Build(g, "JerryCan");
        }

        /// <summary>A bottle jack under the sill next to the wheel being changed.</summary>
        GameObject JackProp(WorkJob j)
        {
            var s = j.socket ? j.socket.transform : j.target;
            if (!s) return null;
            var t = j.v.transform; var b = j.box;
            var wl = t.InverseTransformPoint(s.position);
            var local = new Vector3(Mathf.Clamp(wl.x, b.min.x + 0.15f, b.max.x - 0.15f), b.min.y, wl.z + Mathf.Sign(b.center.z - wl.z) * 0.6f);
            var w = Ground(t.TransformPoint(local), j.v, t.TransformPoint(local).y);
            var go = new GameObject("WorkJack", typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = ToolLibrary.MeshFor("tool_jack");
            go.GetComponent<MeshRenderer>().sharedMaterial = propMaterial;
            go.transform.SetPositionAndRotation(w, Quaternion.Euler(0f, t.eulerAngles.y + 90f, 0f) * Quaternion.Euler(180f, 0f, 0f));   // saddle up
            go.transform.localScale = Vector3.one * 0.55f;
            MadMax.Rendering.HDVisual.Dress(go, MadMax.Rendering.HDDomain.Tool, "tool_jack");
            return go;
        }
    }
}
