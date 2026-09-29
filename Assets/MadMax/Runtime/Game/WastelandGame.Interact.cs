using MadMax.Building;
using MadMax.Items;
using MadMax.Vehicles;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game
{
    /// <summary>Enter/exit (vehicles and walk-in interiors), taking/mounting parts, towing.
    /// F enter/exit/drive · E take or mount part · Q drop carried part · J hitch/unhitch.</summary>
    public partial class WastelandGame
    {
        /// <summary>Context prompt for the HUD, e.g. "[E] TAKE WHEEL_STREET".</summary>
        public string Prompt { get; private set; }

        const float Reach = 1.6f;

        /// <summary>Take the wheel. Online this asks for ownership first (the server may refuse an occupied vehicle).</summary>
        public void Enter(VehicleDriver car)
        {
            if (car && car != Current) MadMax.Audio.Sfx.Play("car_door", car.transform.position, 0.6f, 1f, 25f, 0.3f);
            var net = MadMax.Net.NetSession.Instance;
            if (net && net.Online && !net.Simulates(car) && car.owner != net.LocalId)
            {
                if (car.owner != 0) { Toast("OCCUPIED"); return; }
                net.RequestVehicle(car, true);
                if (net.IsClient) return;                       // EnterGranted follows
            }
            else if (net && net.IsServer) net.RequestVehicle(car, true);
            EnterLocal(car);
        }

        void EnterLocal(VehicleDriver car)
        {
            if (Current) Current.Occupied = false;
            if (Build) Build.SetActive(false);
            Current = car;
            car.Occupied = true;
            if (car.TryGetComponent<VehicleDamage>(out var d)) { d.Impact -= OnImpact; d.Impact += OnImpact; }
            Player.gameObject.SetActive(true);
            Player.SitIn(car);
            terrain.focus = car.transform;
            if (cameraRig) cameraRig.SetTarget(car.transform);
        }

        void OnImpact(float strength, Vector3 point)
        {
            if (Current && strength > 2.5f && Vector3.Distance(Current.transform.position, point) < 8f) Vitals?.Hurt((strength - 2.5f) * 5f, "CRASH");
            if (cameraRig && Current && Vector3.Distance(Current.transform.position, point) < 8f) cameraRig.Shake(strength);
        }

        public void Exit()
        {
            var car = Current;
            if (car) MadMax.Audio.Sfx.Play("car_door", car.transform.position, 0.6f, 0.95f, 25f, 0.3f);
            if (!car) return;
            MadMax.Net.NetSession.Instance?.RequestVehicle(car, false);
            Current = null;
            car.Occupied = false;
            car.throttleInput = car.brakeInput = car.steerInput = 0f;
            car.handbrake = true;
            Player.Unseat();
            var interior = car.GetComponent<InteriorSpace>();
            if (interior) Player.EnterInterior(interior, interior.stand);                 // stand up inside
            else Player.Teleport(ExitPoint(car), car.transform.eulerAngles.y + 90f);
            terrain.focus = Player.transform;
            if (cameraRig) cameraRig.SetTarget(Player.transform);
        }

        void EnterInterior(InteriorSpace space, int door)
        {
            Player.EnterInterior(space, space.doors[door].inside);
            if (cameraRig) cameraRig.SetTarget(Player.transform);
        }

        void LeaveInterior(int door)
        {
            var space = Player.Interior;
            var w = space.transform.TransformPoint(space.doors[door].outside);
            w.y = terrain.Height(w.x, w.z) + 0.05f;
            Player.ExitInterior(w);
            if (cameraRig) cameraRig.SetTarget(Player.transform);
        }

        Vector3 ExitPoint(VehicleDriver car)
        {
            var eye = car.transform.Find("DriverEye");
            float z = eye ? eye.localPosition.z : 0f;
            float w = HalfExtents(car.gameObject).x + 0.6f;
            var candidates = new[] { new Vector3(eye && eye.localPosition.x < 0 ? -w : w, 0, z), new Vector3(eye && eye.localPosition.x < 0 ? w : -w, 0, z), new Vector3(0, 0, -HalfExtents(car.gameObject).z - 0.8f) };
            foreach (var c in candidates)
            {
                var p = car.transform.TransformPoint(c);
                p.y = terrain.Height(p.x, p.z) + 0.05f;
                if (!Physics.CheckCapsule(p + Vector3.up * 0.5f, p + Vector3.up * 1.6f, 0.3f, ~0, QueryTriggerInteraction.Ignore)) return p;
            }
            return car.transform.position + Vector3.up * 3f;
        }

        static readonly RaycastHit[] aimHits = new RaycastHit[16];

        /// <summary>Where the driver aims mounted weapons: under the mouse in top-down views, the screen centre in third /
        /// first person. Skips the vehicle itself; top-down aims chest-high above the ground.</summary>
        public Vector3 VehicleAim()
        {
            var fallback = Current ? Current.transform.position + Current.transform.forward * 30f : Vector3.zero;
            if (!cameraRig || !cameraRig.pixel) return fallback;
            var cam = cameraRig.pixel.GetComponent<Camera>();
            bool topDown = cameraRig.mode == ViewMode.Isometric || cameraRig.mode == ViewMode.TiltShift;
            Ray ray;
            if (!topDown) ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            else
            {
                var m = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width / 2f, Screen.height / 2f);
                ray = cam.ViewportPointToRay(new Vector3(m.x / Screen.width, m.y / Screen.height, 0f));
            }
            int n = Physics.RaycastNonAlloc(ray, aimHits, 300f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; var p = fallback;
            for (int i = 0; i < n; i++)
            {
                if (Current && aimHits[i].collider.transform.IsChildOf(Current.transform)) continue;
                if (aimHits[i].distance < best) { best = aimHits[i].distance; p = aimHits[i].point; }
            }
            if (best == float.MaxValue)
            {
                // nothing with a collider under the cursor: meet the ground plane at the vehicle's height
                float y = Current ? Current.transform.position.y : 0f;
                if (Mathf.Abs(ray.direction.y) > 0.01f) { float t = (y - ray.origin.y) / ray.direction.y; if (t > 0f) p = ray.origin + ray.direction * t; }
                else p = ray.origin + ray.direction * 80f;
            }
            if (topDown) p += Vector3.up * 0.9f;
            return p;
        }

        VehicleDriver FindNearby(float maxDist)
        {
            VehicleDriver best = null; float bestD = maxDist;
            var p = Player.transform.position + Vector3.up;
            foreach (var c in cars)
            {
                if (!c || c.aiDriven || (c.transform.position - p).sqrMagnitude > 400f) continue;
                foreach (var col in c.GetComponentsInChildren<Collider>())
                {
                    if (!col.enabled || col is MeshCollider) continue;
                    float d = Vector3.Distance(col.ClosestPoint(p), p);
                    if (d < bestD) { bestD = d; best = c; }
                }
            }
            return best;
        }

        void UpdateInteraction(Keyboard kb, Gamepad pad)
        {
            bool F = KeyDown(kb, Key.F) || (pad != null && pad.buttonEast.wasPressedThisFrame);
            bool E = KeyDown(kb, Key.E) || (pad != null && pad.rightShoulder.wasPressedThisFrame);
            bool Q = KeyDown(kb, Key.Q) || (pad != null && pad.leftShoulder.wasPressedThisFrame);
            bool J = KeyDown(kb, Key.J) || (pad != null && pad.dpad.right.wasPressedThisFrame);
            Prompt = null;
            NearbyVehicle = null;

            if (Current)
            {
                Prompt = Current.GetComponent<InteriorSpace>() ? "[F] STAND UP" : "[F] EXIT";
                var tow = TowTargetFor(Current, out var towText);
                if (towText != null) Prompt += "   " + towText;
                if (F) Exit();
                else if (J) DoTow(Current, tow);
                return;
            }

            // ---- sitting on furniture (or standing on a roof): get up / down, use what is in reach
            if (Player.Sitting)
            {
                bool T0 = KeyDown(kb, Key.T) || (pad != null && pad.buttonNorth.wasPressedThisFrame);
                string near = PieceInteraction(E, T0);
                Prompt = (Player.SeatedOn && Player.SeatedOn.standing ? "[F] CLIMB DOWN" : "[F] STAND UP") + (near != null ? "   " + near : "");
                if (F) Player.StandUp();
                return;
            }

            bool G = KeyDown(kb, Key.G);
            bool K = KeyDown(kb, Key.K);

            // ---- on foot: workbench, then parts
            bool T = KeyDown(kb, Key.T) || (pad != null && pad.buttonNorth.wasPressedThisFrame);
            string partText = Player.Carried ? null : PieceInteraction(E, T);
            if (partText == null) partText = PartInteraction(E, Q);

            // ---- interiors and vehicles
            string enterText = null;
            var space = Player.Interior;
            if (space)
            {
                var local = Player.transform.localPosition;
                var seat = new Vector2(space.seat.x, space.seat.z);
                int door = space.NearestDoorInside(local, 1.4f);
                var drv = space.GetComponent<VehicleDriver>();
                if (Vector2.Distance(new Vector2(local.x, local.z), seat) < 1.6f && drv && drv.driveable)
                {
                    enterText = "[F] DRIVE";
                    if (F) Enter(drv);
                }
                else if (door >= 0)
                {
                    enterText = "[F] EXIT " + Name(drv);
                    if (F) LeaveInterior(door);
                }
            }
            else
            {
                NearbyVehicle = FindNearby(enterDistance);
                if (NearbyVehicle)
                {
                    var interior = NearbyVehicle.GetComponent<InteriorSpace>();
                    if (interior)
                    {
                        int door = interior.NearestDoorOutside(Player.transform.position, 99f);
                        enterText = "[F] ENTER " + Name(NearbyVehicle);
                        if (F && door >= 0) EnterInterior(interior, door);
                    }
                    else
                    {
                        enterText = "[F] DRIVE " + Name(NearbyVehicle);
                        if (F) Enter(NearbyVehicle);
                    }
                }
            }

            string towPrompt = FootTow(J);
            string fluidText = FluidInteraction(G, K);
            Prompt = Join(partText, enterText, towPrompt, fluidText);
        }

        static string Join(params string[] parts)
        {
            string s = null;
            foreach (var p in parts) if (!string.IsNullOrEmpty(p)) s = s == null ? p : s + "   " + p;
            return s;
        }

        public static string Name(Component c) => c ? c.name.Replace("(Clone)", "").Replace("Wreck ", "").ToUpperInvariant() : "";

        CraftingStation NearestStation(float max)
        {
            CraftingStation best = null; float bd = max;
            foreach (var s in CraftingStation.All)
            {
                if (!s) continue;
                float d = Vector3.Distance(s.transform.position, Player.transform.position);
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        // ------------------------------------------------------------------ fluids
        string FluidInteraction(bool G, bool K)
        {
            var v = FindNearby(enterDistance + 0.5f);
            if (!v && Player.Interior) v = Player.Interior.GetComponent<VehicleDriver>();
            if (!v || !v.TryGetComponent<VehicleSystems>(out var sys)) return null;
            string text = null;
            if (v.TryGetComponent<FuelTanker>(out var tanker))
            {
                // tanker pump: G = tanker -> partner, K = partner -> tanker (the partner is the tow vehicle or the nearest one)
                var partner = tanker.Pumping != FuelTanker.Mode.Off ? tanker.Partner : tanker.FindPartner();
                string fuel = $"TANKER {Mathf.FloorToInt(sys.fuel)}/{Mathf.RoundToInt(sys.fuelCapacity)} L";
                if (!partner) return fuel + "  (PARK A VEHICLE BY THE HOSE)";
                string pn = Name(partner);
                if (tanker.Pumping == FuelTanker.Mode.Fill) text = $"PUMPING INTO {pn} {Mathf.RoundToInt(tanker.Moved)} L  [G] STOP";
                else if (tanker.Pumping == FuelTanker.Mode.Drain) text = $"DRAINING {pn} {Mathf.RoundToInt(tanker.Moved)} L  [K] STOP";
                else text = $"{fuel}  [G] PUMP INTO {pn}  [K] DRAIN {pn}";
                if (G) tanker.Toggle(FuelTanker.Mode.Fill);
                if (K) tanker.Toggle(FuelTanker.Mode.Drain);
                return text;
            }
            if (sys.NeedsService(Inventory))
            {
                text = "[G] REFUEL/SERVICE";
                if (G) { if (Refuelling) StopRefuel("STOPPED"); else if (sys.fuel < sys.fuelCapacity - 1f && (Inventory.Get(ResourceType.Fuel) > 0 || Inventory.Get(ResourceType.Ethanol) > 0)) StartRefuel(v, null); else { Stats.Practice(MadMax.RPG.Skill.Mechanics, 3f); int n = sys.Service(Inventory); Toast($"SERVICED {Name(v)}: {n} L"); MadMax.Net.NetSession.Instance?.SendVehicleMeta(v); } }
            }
            if (sys.TotalFluids >= 1f)
            {
                text = Join(text, $"[K] SIPHON {Mathf.FloorToInt(sys.TotalFluids)} L");
                if (K) { Stats.Practice(MadMax.RPG.Skill.Survival, 2f); int n = sys.Siphon(Inventory); Toast($"SIPHONED {n} L"); MadMax.Net.NetSession.Instance?.SendVehicleMeta(v); }
            }
            return text;
        }

        // ------------------------------------------------------------------ parts
        string PartInteraction(bool E, bool Q)
        {
            var reach = Player.transform.position + Vector3.up * 1.0f + Player.transform.forward * 0.7f;
            var carried = Player.Carried;
            if (carried)
            {
                MountSocket best = null; float bd = Reach;
                foreach (var v in vehicles)
                {
                    if ((v.transform.position - reach).sqrMagnitude > 225f) continue;
                    foreach (var s in v.GetComponent<VehicleChassis>().Sockets)
                    {
                        if (!s.IsFree || !s.CanAccept(carried)) continue;
                        if (!PartVisible(Player.Eye.position, s.transform.position, carried)) continue;
                        float d = Vector3.Distance(s.transform.position, reach);
                        if (d < bd) { bd = d; best = s; }
                    }
                }
                if (Q)
                {
                    var part = Player.Carried;
                    Player.DropCarried();
                    MadMax.Net.NetSession.Instance?.SendPartDropped(part, Player.transform.forward * 1.5f);
                    return null;
                }
                if (best)
                {
                    if (!Holding(ItemIds.Wrench)) return Inventory.GetItem(ItemIds.Wrench) > 0 ? "EQUIP THE WRENCH TO MOUNT   [Q] DROP" : "CRAFT A WRENCH TO MOUNT   [Q] DROP";
                    if (NeedsJack(carried)) return "HEAVY WHEEL: NEED A JACK IN THE PACK   [Q] DROP";
                    if (E)
                    {
                        var part = Player.TakeCarried();
                        best.Attach(part);
                        MadMax.Audio.Sfx.Play("ratchet", best.transform.position, 0.8f);
                        Stats.Practice(MadMax.RPG.Skill.Mechanics, 8f);
                        MadMax.Net.NetSession.Instance?.SendPartMounted(best.GetComponentInParent<VehicleDriver>(), best.name, part);
                    }
                    return $"[E] MOUNT {carried.partId.ToUpperInvariant()} > {best.name.ToUpperInvariant()}   [Q] DROP";
                }
                return $"CARRYING {carried.partId.ToUpperInvariant()}   [Q] DROP";
            }

            VehiclePart target = null; float best2 = Reach;
            var eye = Player.Eye.position;
            foreach (var part in VehiclePart.Registry)
            {
                if (!part || part.transform.IsChildOf(Player.transform)) continue;
                var r = part.GetComponent<Renderer>();
                if (!r) continue;
                if (!PartVisible(eye, r.bounds.center, part)) continue;       // e.g. the engine is hidden until the hood comes off
                float d = Mathf.Sqrt(r.bounds.SqrDistance(reach));
                if (d < best2) { best2 = d; target = part; }
            }
            if (!target) return null;
            if (target.Socket && !Holding(ItemIds.Wrench)) return $"{target.partId.ToUpperInvariant()}: " + (Inventory.GetItem(ItemIds.Wrench) > 0 ? "EQUIP THE WRENCH" : "CRAFT A WRENCH");
            if (target.Socket && NeedsJack(target)) return $"{target.partId.ToUpperInvariant()}: NEED A JACK TO LIFT IT";
            if (target.Socket && E) WearTool(ItemIds.Wrench, 0.01f);
            if (E)
            {
                var net = MadMax.Net.NetSession.Instance;
                if (target.Socket)
                {
                    var owner = target.Socket.GetComponentInParent<VehicleDriver>();
                    if (net) { target.netId = net.NewEntityId(); net.SendPartDetached(owner, target.Socket.name, target, net.LocalId); }
                    MadMax.Audio.Sfx.Play("ratchet", target.transform.position, 0.8f);
                    target.Socket.Detach(false);
                    Stats.Practice(MadMax.RPG.Skill.Mechanics, 6f);
                }
                else net?.SendPartCarried(target);
                if (target.TryGetComponent<MadMax.Net.NetReplica>(out var rep)) Destroy(rep);
                Player.Carry(target);
            }
            string state = target.damage > 0.05f ? $" {Mathf.RoundToInt((1f - Mathf.Clamp01(target.damage)) * 100f)}%" : "";
            return $"[E] TAKE {target.partId.ToUpperInvariant()}{state}";
        }

        bool Holding(string toolId) => Player.Tool && Player.Tool.id == toolId;

        static readonly RaycastHit[] losHits = new RaycastHit[16];

        /// <summary>Line of sight from the eye to a part: other parts and props block it; the vehicle's own body shell does not.</summary>
        bool PartVisible(Vector3 eye, Vector3 point, VehiclePart part)
        {
            var d = point - eye;
            int n = Physics.RaycastNonAlloc(eye, d.normalized, losHits, d.magnitude, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = losHits[i].collider;
                if (c.transform.IsChildOf(part.transform) || c.transform.IsChildOf(Player.transform)) continue;
                var other = c.GetComponentInParent<VehiclePart>();
                if (!other && c.GetComponentInParent<VehicleChassis>()) continue;    // body box / shell colliders
                if (losHits[i].distance < d.magnitude - 0.05f) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ towing
        TowCoupling TowTargetFor(VehicleDriver tower, out string text)
        {
            text = null;
            var hitch = TowCoupling.HitchOf(tower);
            if (!hitch) return null;
            foreach (var t in trailers)
            {
                var tc = t.GetComponent<TowCoupling>();
                if (tc && tc.Tower == tower) { text = "[J] UNHITCH " + Name(t); return tc; }
            }
            foreach (var t in trailers)
            {
                var tc = t.GetComponent<TowCoupling>();
                if (!tc || tc.Tower || !tc.Coupler) continue;
                if (Vector3.Distance(tc.Coupler.position, hitch.position) < 3.5f) { text = "[J] HITCH " + Name(t); return tc; }
            }
            return null;
        }

        void DoTow(VehicleDriver tower, TowCoupling tc)
        {
            if (!tc) return;
            if (tc.Tower) { tc.Uncouple(); MadMax.Net.NetSession.Instance?.SendCouple(tc.GetComponent<VehicleDriver>(), null); }
            else if (tc.Couple(tower)) MadMax.Net.NetSession.Instance?.SendCouple(tc.GetComponent<VehicleDriver>(), tower);
        }

        string FootTow(bool J)
        {
            foreach (var t in trailers)
            {
                var tc = t.GetComponent<TowCoupling>();
                if (!tc || !tc.Coupler || Vector3.Distance(tc.Coupler.position, Player.transform.position + Vector3.up) > 2.2f) continue;
                if (tc.Tower)
                {
                    if (J) { tc.Uncouple(); MadMax.Net.NetSession.Instance?.SendCouple(t, null); }
                    return "[J] UNHITCH " + Name(t);
                }
                VehicleDriver best = null; float bd = 4f;
                foreach (var v in cars)
                {
                    var h = TowCoupling.HitchOf(v);
                    if (!h) continue;
                    float d = Vector3.Distance(h.position, tc.Coupler.position);
                    if (d < bd) { bd = d; best = v; }
                }
                if (!best) return "BACK A VEHICLE UP TO THE " + Name(t);
                if (J && tc.Couple(best)) MadMax.Net.NetSession.Instance?.SendCouple(t, best);
                return $"[J] HITCH {Name(t)} TO {Name(best)}";
            }
            return null;
        }
    }
}
