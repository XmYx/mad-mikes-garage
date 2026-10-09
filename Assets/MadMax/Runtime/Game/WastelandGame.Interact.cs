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
            if (car && car != Current && !car.GetComponent<BikeBalance>()) MadMax.Audio.Sfx.Play("car_door", car.transform.position, 0.6f, 1f, 25f, 0.3f);
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
            var sn = MadMax.Net.NetSession.Instance;
            var tow = car.GetComponent<TowCoupling>();
            if (car.Body && car.Body.isKinematic && !(sn && sn.Online && !sn.Simulates(car)) && !(tow && tow.Tower)) Wake(car.Body);   // the sleeper skips the driven car: wake it here
            if (car.TryGetComponent<BikeBalance>(out var bike)) bike.Remount();                // pick it up if it went down
            if (car.TryGetComponent<VehicleDamage>(out var d)) { d.Impact -= OnImpact; d.Impact += OnImpact; }
            Player.gameObject.SetActive(true);
            Player.SitIn(car);
            terrain.focus = car.transform;
            if (cameraRig) cameraRig.SetTarget(car.transform);
        }

        /// <summary>Δv (m/s) a belted occupant takes without harm: 7 (25 km/h) in a cab, 5 on an open seat.</summary>
        public const float CrashSafeClosed = 7f, CrashSafeOpen = 5f;

        /// <summary>Harm to the occupant from a crash of change of speed <paramref name="dv"/> (m/s): none under the safe
        /// change, then growing steeply — ~8 at 36 km/h (bruising), ~30 at 50 km/h (wounds), ~75 at 72 km/h.</summary>
        public static float CrashHarm(float dv, bool open)
        {
            float over = dv - (open ? CrashSafeOpen : CrashSafeClosed);
            return over <= 0f ? 0f : Mathf.Pow(over, 1.5f) * 1.6f;
        }

        void OnImpact(float strength, Vector3 point)
        {
            // strength = the impact's Δv less VehicleDamage.minImpactSpeed (2.5 m/s)
            float harm = Current ? CrashHarm(strength + 2.5f, OpenVehicle(Current)) : 0f;
            if (harm > 0f && Vector3.Distance(Current.transform.position, point) < 8f) Vitals?.Hurt(harm, "CRASH");
            if (cameraRig && Current && Vector3.Distance(Current.transform.position, point) < 8f) cameraRig.Shake(strength);
        }

        public void Exit()
        {
            var car = Current;
            if (car && !car.GetComponent<BikeBalance>()) MadMax.Audio.Sfx.Play("car_door", car.transform.position, 0.6f, 0.95f, 25f, 0.3f);
            if (!car) return;
            if (car.TryGetComponent<VehicleSystems>(out var ign)) ign.Stop();                 // keys out
            MadMax.Net.NetSession.Instance?.RequestVehicle(car, false);
            Current = null;
            car.Occupied = false;
            car.throttleInput = car.brakeInput = car.steerInput = 0f;
            car.handbrake = true;
            Player.Unseat();
            var interior = car.GetComponent<InteriorSpace>();
            if (interior && interior.FindExitSpot(InteriorRadius, out var spot)) Player.EnterInterior(interior, spot);   // stand up inside, where a door can be reached
            else if (interior && (interior.airtight || car.GetComponent<MadMax.Vehicles.BoatModel>())) Player.EnterInterior(interior, interior.stand);   // afloat / sealed: never out into the sea
            else if (interior) Player.Teleport(InteriorExitOutside(car, interior), car.transform.eulerAngles.y + 90f);   // boxed in: straight out of a door
            else Player.Teleport(ExitPoint(car), car.transform.eulerAngles.y + 90f);
            terrain.focus = Player.transform;
            if (cameraRig) cameraRig.SetTarget(Player.transform);
        }

        /// <summary>A bike crash (roadmap 24) or a hard landing: off the seat and limp through the air along the travel
        /// before the impact, hurt by the speed; back on your feet a few seconds later where you landed (unless it killed
        /// you). See <see cref="Eject"/>.</summary>
        public void ThrowRider(VehicleDriver bike, Vector3 velocity, float severity, string why)
        {
            if (Current != bike || !Player) return;
            var pre = bike.TryGetComponent<VehicleDamage>(out var vd) && vd.PreImpactVelocity.sqrMagnitude > velocity.sqrMagnitude ? vd.PreImpactVelocity : velocity;
            Eject(bike, EjectVelocity(pre, velocity), severity, why);
        }

        void GetUp()
        {
            if (dying || Vitals.Dead || !Player) return;
            var rd = Player.GetComponent<Ragdoll>();
            var at = Player.Rig.Bone(BodyPart.Pelvis).position;
            if (rd) rd.Restore();
            var cc = Player.GetComponent<CharacterController>();
            if (cc) cc.enabled = true;
            if (terrain) at.y = Mathf.Max(at.y - 0.9f, terrain.Height(at.x, at.z) + 0.05f);
            Player.Teleport(at, Player.transform.eulerAngles.y);
            if (cameraRig && !Current) cameraRig.SetTarget(Player.transform);                      // off the tumbling body, back on the player
        }

        /// <summary>Into a walk-in space through its first door (a docked submarine, from the base's collar).</summary>
        public void BoardInterior(InteriorSpace space) => EnterInterior(space, 0);

        void EnterInterior(InteriorSpace space, int door)
        {
            Player.EnterInterior(space, space.doors[door].inside);
            if (cameraRig) cameraRig.SetTarget(Player.transform);
        }

        void LeaveInterior(int door)
        {
            var space = Player.Interior;
            var w = space.transform.TransformPoint(space.doors[door].outside);
            if (!space.GetComponent<MadMax.Vehicles.BoatModel>()) w.y = terrain.Height(w.x, w.z) + 0.05f;   // boats: out onto the deck / the sail
            var collar = space.TryGetComponent<MadMax.Vehicles.Submarine>(out var sub) ? MadMax.Building.DockingCollar.Of(sub) : null;
            if (collar) w = collar.InsideWorld;                                                   // docked: down through the collar into the base
            Player.ExitInterior(w);
            if (cameraRig) cameraRig.SetTarget(Player.transform);
        }

        /// <summary>The walk-in player's radius (<see cref="PlayerCharacter"/> resolves interior moves with it).</summary>
        const float InteriorRadius = 0.26f;

        /// <summary>Outside a walk-in vehicle: the first door whose outside spot is clear, else beside the cab.</summary>
        Vector3 InteriorExitOutside(VehicleDriver car, InteriorSpace space)
        {
            if (space.doors != null)
                foreach (var d in space.doors)
                {
                    var w = car.transform.TransformPoint(d.outside);
                    w.y = terrain.Height(w.x, w.z) + 0.05f;
                    if (!Physics.CheckCapsule(w + Vector3.up * 0.5f, w + Vector3.up * 1.6f, 0.3f, ~0, QueryTriggerInteraction.Ignore)) return w;
                }
            return ExitPoint(car);
        }

        Vector3 ExitPoint(VehicleDriver car)
        {
            if (car.TryGetComponent<MadMax.Vehicles.BoatModel>(out var boat) && boat.deckAt != Vector3.zero)
                return car.transform.TransformPoint(boat.deckAt);                                  // afloat: stand up on the deck, not in the sea
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
            bool topDown = cameraRig.TopDownView;
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

        /// <summary>The vehicle whose collider is nearest the player within <paramref name="maxDist"/>. Trailers are
        /// never driven; with <paramref name="tankers"/> fuel tankers count too (their pump works on foot).</summary>
        VehicleDriver FindNearby(float maxDist, bool tankers = false)
        {
            VehicleDriver best = null; float bestD = maxDist;
            var p = Player.transform.position + Vector3.up;
            void Try(VehicleDriver c)
            {
                if (!c || c.aiDriven || (c.transform.position - p).sqrMagnitude > 400f) return;
                foreach (var col in c.GetComponentsInChildren<Collider>())
                {
                    if (!col.enabled || col is MeshCollider) continue;
                    float d = Vector3.Distance(col.ClosestPoint(p), p);
                    if (d < bestD) { bestD = d; best = c; }
                }
            }
            foreach (var c in cars) Try(c);
            if (tankers) foreach (var t in trailers) if (t && t.GetComponent<FuelTanker>()) Try(t);
            return best;
        }

        void UpdateInteraction(Keyboard kb, Gamepad pad)
        {
            bool F = Controls.Down(Controls.Act.Enter) || (pad != null && pad.buttonEast.wasPressedThisFrame);
            bool E = (Controls.Down(Controls.Act.Use) || (pad != null && pad.rightShoulder.wasPressedThisFrame)) && Menus.ClosedFrame != Time.frameCount;   // E that closed the crafting page doesn't reopen it
            bool Q = Controls.Down(Controls.Act.Drop) || (pad != null && pad.leftShoulder.wasPressedThisFrame);
            bool J = Controls.Down(Controls.Act.Hitch) || (pad != null && pad.dpad.right.wasPressedThisFrame);
            Prompt = null;
            NearbyVehicle = null;

            if (Current)
            {
                Prompt = Current.GetComponent<InteriorSpace>() ? "[F] STAND UP" : "[F] EXIT";
                var tow = TowTargetFor(Current, out var towText);
                if (towText != null) Prompt += "   " + towText;
                string wire = HotwireInteraction(Current);
                if (wire != null) Prompt += "   " + wire;
                if (F && !Boarding) ExitAnimated();
                else if (J) DoTow(Current, tow);
                return;
            }

            // ---- sitting on furniture (or standing on a roof): get up / down, use what is in reach
            if (Player.Sitting)
            {
                bool T0 = Controls.Down(Controls.Act.Second) || (pad != null && pad.buttonNorth.wasPressedThisFrame);
                string near = PieceInteraction(E, T0);
                Prompt = (Player.SeatedOn && Player.SeatedOn.ride != null ? "[F] DISMOUNT" : Player.SeatedOn && Player.SeatedOn.standing ? "[F] CLIMB DOWN" : "[F] STAND UP") + (near != null ? "   " + near : "");
                if (F) Player.StandUp();
                return;
            }

            bool G = Controls.Down(Controls.Act.Service) || (pad != null && pad.dpad.up.wasPressedThisFrame);
            bool K = Controls.Down(Controls.Act.Siphon) || (pad != null && pad.dpad.down.wasPressedThisFrame);

            // ---- on foot: workbench, then parts
            bool T = Controls.Down(Controls.Act.Second) || (pad != null && pad.buttonNorth.wasPressedThisFrame);
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
                float seatD = Vector2.Distance(new Vector2(local.x, local.z), seat);
                float doorD = door >= 0 ? Vector2.Distance(new Vector2(local.x, local.z), new Vector2(space.doors[door].inside.x, space.doors[door].inside.z)) : float.MaxValue;
                if (seatD < 1.6f && drv && drv.driveable && seatD <= doorD)                       // a door beside the seat (the bus) wins when nearer
                {
                    enterText = "[F] DRIVE";
                    if (F && !Boarding) EnterAnimated(drv);
                }
                else if (door >= 0)
                {
                    enterText = "[F] EXIT " + Name(drv);
                    if (F) LeaveInterior(door);
                }
                else if (space.Trapped(local, InteriorRadius))
                {
                    // furniture walled off every door: squeeze out through the nearest one
                    enterText = "[F] SQUEEZE OUT";
                    if (F) LeaveInterior(space.NearestDoorInside(local, float.MaxValue));
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
            string armourText = ArmourInteraction(Controls.Down(Controls.Act.Armour) && !Build.Active);
            Prompt = Join(partText, enterText, towPrompt, fluidText, armourText);
        }

        float hotwireT;
        /// <summary>Automation: hold the service key for the hotwire without a keyboard.</summary>
        public bool ForceHotwire;

        /// <summary>In the seat of a vehicle that won't start without its key: hold the service key to hotwire it (a
        /// timed job: anyone can, slowly and badly; Hotwiring makes it quick and sure). A failure blows a fuse (a short
        /// wait), may bite with a shock, and makes noise.</summary>
        string HotwireInteraction(VehicleDriver v)
        {
            if (!v || !v.TryGetComponent<VehicleIgnition>(out var ign) || ign.CanStart(Inventory)) { hotwireT = 0f; return null; }
            string key = Controls.Name(Controls.Act.Service);
            if (Time.time < ign.blownUntil) { hotwireT = 0f; return "FUSE BLOWN: WAIT " + Mathf.CeilToInt(ign.blownUntil - Time.time) + " S"; }
            int level = Stats.Level(MadMax.RPG.Skill.Hotwiring);
            float need = ign.HotwireSeconds(level);
            if (!(Controls.Held(Controls.Act.Service) || ForceHotwire) || Menus.IsOpen) { hotwireT = 0f; return "NO KEY  [HOLD " + key + "] TRY THE WIRES"; }
            hotwireT += Time.deltaTime;
            if (Random.value < Time.deltaTime * 3f) MadMax.Audio.Sfx.Play("ratchet", v.transform.position + Vector3.up, 0.25f, Random.Range(1.6f, 2.2f), 8f, 0.3f);
            if (hotwireT < need) return "HOTWIRING... " + Mathf.RoundToInt(hotwireT / need * 100f) + "%";
            hotwireT = 0f;
            if (Random.value < ign.HotwireChance(level))
            {
                ign.hotwired = true;
                Stats.Practice(MadMax.RPG.Skill.Hotwiring, 6f);
                MadMax.World.Fx.Sparks(v.transform.position + Vector3.up * 0.8f, Vector3.up, 4, new Color(0.7f, 0.85f, 1f));
                Toast("HOTWIRED: THE DASH LIGHTS UP");
                if (v.TryGetComponent<VehicleSystems>(out var sys)) sys.Crank();
            }
            else
            {
                ign.blownUntil = Time.time + 8f;
                Stats.Practice(MadMax.RPG.Skill.Hotwiring, 3f);
                MadMax.World.Fx.Sparks(v.transform.position + Vector3.up * 0.8f, Vector3.up, 8, new Color(0.7f, 0.85f, 1f));
                MadMax.Audio.Sfx.Play("pop", v.transform.position + Vector3.up, 0.5f, 1.8f, 30f);
                if (MadMax.Npc.NpcDirector.Instance) MadMax.Npc.NpcDirector.Instance.Noise(v.transform.position, 30f);
                if (Random.value < 0.3f) { Injure(3f, "BURNED"); Toast("ZAP! WRONG WIRES: A SHOCK AND A BLOWN FUSE"); }
                else Toast("WRONG WIRES: A BLOWN FUSE");
            }
            return null;
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

        // ------------------------------------------------------------------ armour
        string ArmourInteraction(bool U)
        {
            if (Inventory.GetItem("tool_welder") <= 0) return null;
            var v = FindNearby(enterDistance + 0.5f);
            if (!v || !v.TryGetComponent<VehicleArmor>(out var armour)) return null;
            if (U) Menus.OpenArmour(armour);
            return "[U] ARMOUR";
        }

        /// <summary>Weld a material onto a zone (or repair it, or strip it for part of the material back).</summary>
        public void WeldArmour(VehicleArmor a, ArmorZone z, ArmorMat m)
        {
            int i = (int)z;
            if (Inventory.GetItem("tool_welder") <= 0) { Toast("YOU NEED A WELDER"); return; }
            if (a.Voxels(z) == 0) return;
            if (m == ArmorMat.None)
            {
                if (a.mat[i] == ArmorMat.None) return;
                if (DeferArmour(a, z, m, () => WeldArmour(a, z, m))) return;                    // cut off at the zone first (WastelandGame.Anim)
                a.Cost(z, a.mat[i], 0.3f * a.condition[i], out var r1, out int n1, out var r2, out int n2);
                Inventory.Add(r1, n1); if (n2 > 0) Inventory.Add(r2, n2);
                a.Set(z, ArmorMat.None, 0f);
                MadMax.Audio.Sfx.Play("grinder", a.transform.position, 0.6f, 1f);
                Toast("CUT OFF THE " + VehicleArmor.ZoneNames[i] + ": +" + n1 + " " + ResourceInfo.Name(r1));
                return;
            }
            float share = a.mat[i] == m ? 1f - a.condition[i] : 1f;
            if (share < 0.02f) { Toast("THE " + VehicleArmor.ZoneNames[i] + " IS IN GOOD SHAPE"); return; }
            a.Cost(z, m, share, out var c1, out int k1, out var c2, out int k2);
            if (Inventory.Get(c1) < k1 || (k2 > 0 && Inventory.Get(c2) < k2) || Inventory.Get(ResourceType.Fuel) < 1)
            {
                Toast("NEED " + k1 + " " + ResourceInfo.Name(c1) + (k2 > 0 ? " + " + k2 + " " + ResourceInfo.Name(c2) : "") + " + 1 PETROL FOR THE TORCH");
                return;
            }
            if (DeferArmour(a, z, m, () => WeldArmour(a, z, m))) return;                        // welded on at the zone first (WastelandGame.Anim)
            Inventory.TrySpend(c1, k1); if (k2 > 0) Inventory.TrySpend(c2, k2);
            Inventory.TrySpend(ResourceType.Fuel, 1);
            if (a.mat[i] != ArmorMat.None && a.mat[i] != m) Inventory.Add(ResourceType.Scrap, Mathf.Max(1, a.Voxels(z) / 40));   // the old plates come off
            a.Set(z, m, 1f);
            MadMax.Audio.Sfx.Play("grinder", a.transform.position, 0.8f, 1.3f);
            if (MadMax.World.DebrisSystem.Instance)
                for (int s = 0; s < 10; s++) MadMax.World.DebrisSystem.Instance.EmitPuff(a.transform.position + Vector3.up + Random.insideUnitSphere, new Color32(255, 220, 120, 255), 0.03f, Random.insideUnitSphere * 3f, 0.25f);
            Stats.Practice(MadMax.RPG.Skill.Mechanics, 4f);
            WearTool("tool_welder", 0.03f);
            Toast("WELDED " + VehicleArmor.MatNames[(int)m] + " ON THE " + VehicleArmor.ZoneNames[i]);
        }

        // ------------------------------------------------------------------ tuning bench (roadmap 14)
        public void FitTuningKit(VehicleTuning t, string kit, bool fitted, System.Action fit)
        {
            if (fitted) return;
            if (Stats.Level(MadMax.RPG.Skill.Mechanics) < 4) { Toast("NEEDS MECHANICS 4"); return; }
            if (!Inventory.TakeItem(kit)) { Toast("NO " + ItemCatalog.Name(kit)); return; }
            fit(); t.Apply();
            MadMax.Audio.Sfx.Play("ratchet", t.transform.position, 0.8f, 1f);
            Stats.Practice(MadMax.RPG.Skill.Mechanics, 8f);
            Toast(ItemCatalog.Name(kit).Replace(" KIT", "") + " FITTED");
        }

        public void UpgradeBrakes(VehicleTuning t)
        {
            if (t.brakeLevel >= 2) return;
            if (Stats.Level(MadMax.RPG.Skill.Mechanics) < 2) { Toast("NEEDS MECHANICS 2"); return; }
            if (Inventory.Get(ResourceType.Iron) < 4 || Inventory.Get(ResourceType.Copper) < 2) { Toast("NEED 4 IRON + 2 COPPER"); return; }
            Inventory.TrySpend(ResourceType.Iron, 4); Inventory.TrySpend(ResourceType.Copper, 2);
            t.brakeLevel++; t.Apply();
            MadMax.Audio.Sfx.Play("ratchet", t.transform.position, 0.8f, 1.2f);
            Stats.Practice(MadMax.RPG.Skill.Mechanics, 5f);
            Toast("BRAKES UPGRADED");
        }

        /// <summary>Ballast in 25 kg steps of stone (taken from / given back to the pack).</summary>
        public void TuneBallast(VehicleTuning t, int dir)
        {
            if (dir > 0)
            {
                if (t.ballast >= 400f) return;
                if (!Inventory.TrySpend(ResourceType.Stone, 3)) { Toast("NEED 3 STONE PER 25 KG"); return; }
                t.ballast += 25f;
            }
            else if (t.ballast > 0f) { t.ballast = Mathf.Max(0f, t.ballast - 25f); Inventory.Add(ResourceType.Stone, 3); }
            t.Apply();
        }

        public void StripInterior(VehicleTuning t)
        {
            if (!t.stripped) { t.stripped = true; Inventory.Add(ResourceType.Scrap, 4); Inventory.Add(ResourceType.Cloth, 3); Toast("INTERIOR STRIPPED: +4 SCRAP, +3 CLOTH"); }
            else
            {
                if (Inventory.Get(ResourceType.Scrap) < 4 || Inventory.Get(ResourceType.Cloth) < 3) { Toast("REFIT NEEDS 4 SCRAP + 3 CLOTH"); return; }
                Inventory.TrySpend(ResourceType.Scrap, 4); Inventory.TrySpend(ResourceType.Cloth, 3);
                t.stripped = false; Toast("INTERIOR REFITTED");
            }
            MadMax.Audio.Sfx.Play("ratchet", t.transform.position, 0.6f, 0.9f);
            t.Apply();
        }

        // ------------------------------------------------------------------ fluids
        string FluidInteraction(bool G, bool K)
        {
            if (Player.Tool is FluidCanTool) return CanInteraction(G, K);                      // the container in hand (WastelandGame.Fluids)
            var v = FindNearby(enterDistance + 0.5f, true);
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
            if (sys.disconnected)
            {
                bool wrench = Inventory.GetItem(ItemIds.Wrench) > 0;
                text = wrench ? "[G] RECONNECT THE BATTERY LEAD" : "A BATTERY LEAD HANGS LOOSE (NEEDS A WRENCH)";
                if (G && wrench) ReconnectBattery(v);                                            // timed at the engine bay (WastelandGame.Anim)
                return text;
            }
            // fluids only through a container from the pack (UseCanAt); G services (filters, plugs, oil change) when due
            bool maintain = sys.CanMaintain(Inventory);
            text = maintain ? "[G] SERVICE" : "[G] POUR FROM A CAN";
            if (G) { if (maintain) ServiceVehicle(v); else UseCanAt(v, false); }
            if (sys.TotalFluids >= 1f)
            {
                text = Join(text, $"[K] SIPHON INTO A CAN ({Mathf.FloorToInt(sys.TotalFluids)} L)");
                if (K) UseCanAt(v, true);
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
                    if (E) MountCarried(best);                                                   // timed at the socket (WastelandGame.Anim)
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
            if (E) TakePart(target);                                                            // mounted parts: timed with the wrench (WastelandGame.Anim)
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
                var tc = t ? t.GetComponent<TowCoupling>() : null;
                if (tc && tc.Tower == tower) { text = "[J] UNHITCH " + Name(t); return tc; }
            }
            foreach (var t in trailers)
            {
                var tc = t ? t.GetComponent<TowCoupling>() : null;
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
                if (!t) continue;                                   // destroyed (scrapped, or removed by a test)
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
