using System.Collections.Generic;
using System.Text.RegularExpressions;
using MadMax.Building;
using MadMax.Items;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game
{
    /// <summary>Context block: right-click menus on everything, the loot window's floor and searched spots, and the
    /// respawn chooser.
    /// <para>RMB rule: a quick RMB tap (released within 0.3 s, the mouse barely moved) opens the context menu on what the
    /// cursor (top-down views) or the crosshair (first / third person) is on; holding or dragging RMB still turns the
    /// camera. RMB stays the aim with a gun, grappling hook or binoculars in hand, and cancels in build mode and while
    /// placing an item — there the context key (<see cref="Controls.Act.Context"/>, default `) opens the menu instead.
    /// In a vehicle the menu is the vehicle's own (get out, lights, 4WD, hitch...).</para>
    /// <para>Options come from <see cref="ContextActions"/> providers and run the same calls as the keys and pages:
    /// a piece's [E]/[T] (<see cref="IInteractable.Use"/>), Enter/Exit, TakePart, ServiceVehicle, Build.Repair/Dismantle,
    /// UseItem, DropFromPack... Out of reach, the player walks up first (any movement key stops it).</para></summary>
    public partial class WastelandGame
    {
        const float ContextReach = 2.1f;

        ContextTarget contextTarget;
        ContextOption walkOption; ContextTarget walkTarget; float walkUntil, walkStuck; Vector3 walkLast;
        Controls.Act? injectNext; int injectAfter;
        readonly List<System.Action> deferred = new List<System.Action>();
        float rmbAt, rmbTravel; bool rmbArmed;
        float respawnOpenedAt = -1f;
        string lastRespawn;
        static readonly RaycastHit[] ctxHits = new RaycastHit[24];
        static readonly Collider[] ctxCols = new Collider[24];
        static readonly HitOrder hitOrder = new HitOrder();
        class HitOrder : IComparer<RaycastHit> { public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance); }

        /// <summary>The storage under the cursor (top-down) or in front ([E] focus): its contents are previewed on the HUD.</summary>
        public Component ContextHover { get; private set; }
        public bool ContextHoverFromCursor { get; private set; }
        /// <summary>Walking up to a context menu's target.</summary>
        public bool ContextWalking => walkOption != null;
        /// <summary>Dead and the respawn list is up.</summary>
        public bool AwaitingRespawn => dying && respawnOpenedAt >= 0f;
        /// <summary>The target of the last context menu.</summary>
        public ContextTarget ContextTargetNow => contextTarget;

        /// <summary>Run <paramref name="a"/> at the start of the next frame (after the click that chose it is over).</summary>
        public void Defer(System.Action a) { if (a != null) deferred.Add(a); }

        void UpdateContext(Keyboard kb, Mouse mouse, Gamepad pad)
        {
            if (deferred.Count > 0) { var run = deferred.ToArray(); deferred.Clear(); foreach (var a in run) a(); }
            if (injectNext.HasValue && Time.frameCount > injectAfter) { Controls.Inject(injectNext.Value); injectNext = null; }
            if (dying && respawnOpenedAt >= 0f)
            {
                if (Menus.Current != MenuSystem.Page.Respawn) Menus.Open(MenuSystem.Page.Respawn);     // nothing else while dead
                if (Time.unscaledTime > respawnOpenedAt + 60f) { var pts = RespawnPoints(); Menus.Close(); RespawnAt(pts[0]); }   // nobody chose: the default
                return;
            }
            UpdateContextWalk();
            ContextHover = null; ContextHoverFromCursor = false;
            if (Menus.IsOpen || TitleSequence.Playing || Dedicated || !Player || !Player.gameObject.activeSelf) { rmbArmed = false; return; }
            UpdateContextHover(mouse);
            if (mouse != null)
            {
                if (mouse.rightButton.wasPressedThisFrame) { rmbAt = Time.unscaledTime; rmbTravel = 0f; rmbArmed = ContextFree(true); }
                if (mouse.rightButton.isPressed) rmbTravel += mouse.delta.ReadValue().magnitude;
                if (mouse.rightButton.wasReleasedThisFrame && rmbArmed && ContextFree(true) && Time.unscaledTime - rmbAt < 0.3f && rmbTravel < 10f) OpenContextAtView(mouse);
                if (!mouse.rightButton.isPressed) rmbArmed = false;
            }
            if (Controls.Down(Controls.Act.Context) && ContextFree(false)) OpenContextAtView(mouse);
        }

        /// <summary>Whether a context menu may open now (<paramref name="rmb"/>: by RMB, which aiming tools keep).</summary>
        bool ContextFree(bool rmb)
        {
            if (Boarding || RadialOpen || (Build && Build.RadialOpen) || dying || (Vitals && Vitals.Dead)) return false;
            if (Current) return true;
            if (Player.Ragdolled || (Build && Build.Active) || PlacingItem) return false;
            if (Player.Tool is FishingRodTool rod && rod.Busy) return false;
            if (rmb && (Player.Tool is RangedTool || Player.Tool is GrappleTool || Player.Tool is BinocularsTool)) return false;   // RMB aims / looks
            return true;
        }

        Vector2 CursorViewport(Mouse mouse) => cameraRig && cameraRig.TopDownView && mouse != null
            ? new Vector2(mouse.position.ReadValue().x / Screen.width, mouse.position.ReadValue().y / Screen.height) : new Vector2(0.5f, 0.5f);

        static Vector2Int ViewToCanvas(Vector2 vp)
        {
            var c = PixelHud.Canvas;
            return c == null ? new Vector2Int(160, 90) : new Vector2Int(Mathf.RoundToInt(vp.x * c.w), Mathf.RoundToInt((1f - vp.y) * c.h));
        }

        void OpenContextAtView(Mouse mouse)
        {
            var vp = CursorViewport(mouse);
            var anchor = ViewToCanvas(vp);
            if (!Current && cameraRig && cameraRig.TopDownView && HotbarSlotAt(anchor, out int slot))
            {
                if (Hotbar[slot] != null) OpenContext(new ContextTarget { item = Hotbar[slot], title = ItemCatalog.Name(Hotbar[slot]) }, anchor);
                return;
            }
            var t = Current ? new ContextTarget { target = Current, point = Current.transform.position, title = Name(Current) } : PickContext(vp, false);
            OpenContext(t, anchor);
        }

        /// <summary>The HUD hotbar slot under a canvas point (the geometry of PixelHud.DrawToolbar).</summary>
        static bool HotbarSlotAt(Vector2Int p, out int slot)
        {
            slot = -1;
            var c = PixelHud.Canvas;
            if (c == null) return false;
            const int size = 22;
            int x0 = (c.w - HotbarSize * (size + 2)) / 2, y = c.h - size - 4;
            if (p.y < y || p.y >= y + size || p.x < x0) return false;
            int i = (p.x - x0) / (size + 2);
            if (i >= HotbarSize || (p.x - x0) % (size + 2) >= size) return false;
            slot = i;
            return true;
        }

        /// <summary>Open the context menu for <paramref name="t"/> at a canvas point.</summary>
        public void OpenContext(ContextTarget t, Vector2Int anchor)
        {
            if (t == null) return;
            contextTarget = t;
            Menus.OpenPopup(t.title ?? "HERE", ContextActions.Gather(this, t), anchor);
        }

        /// <summary>Automation: the options a context menu on <paramref name="c"/> would offer (and it becomes the target).</summary>
        public List<ContextOption> ContextOptionsFor(Component c)
        {
            contextTarget = TargetOf(c);
            return ContextActions.Gather(this, contextTarget);
        }

        /// <summary>Automation: open the context menu on <paramref name="c"/> as RMB on it does.</summary>
        public bool OpenContextFor(Component c)
        {
            if (!c) return false;
            var t = TargetOf(c);
            var anchor = new Vector2Int(160, 90);
            if (cameraRig && cameraRig.pixel)
            {
                var vp = cameraRig.pixel.GetComponent<Camera>().WorldToViewportPoint(t.point);
                if (vp.z > 0f) anchor = ViewToCanvas(new Vector2(Mathf.Clamp01(vp.x), Mathf.Clamp01(vp.y)));
            }
            OpenContext(t, anchor);
            return Menus.PopupOpen;
        }

        ContextTarget TargetOf(Component c)
        {
            var col = c ? c.GetComponent<Collider>() : null;
            var p = col && col.enabled ? col.bounds.center : c ? c.transform.position : Vector3.zero;
            return new ContextTarget { target = c, point = p, title = TitleOf(c) };
        }

        /// <summary>What a ray through the viewport point <paramref name="vp"/> is on (the context menu's target).</summary>
        public ContextTarget PickContext(Vector2 vp, bool hoverOnly)
        {
            Vector3? ground = null;
            if (cameraRig && cameraRig.pixel)
            {
                var cam = cameraRig.pixel.GetComponent<Camera>();
                var ray = cam.ViewportPointToRay(new Vector3(vp.x, vp.y, 0f));
                int n = Physics.RaycastNonAlloc(ray, ctxHits, 150f, ~0, QueryTriggerInteraction.Collide);
                System.Array.Sort(ctxHits, 0, n, hitOrder);
                var fade = OccluderFade.Active;
                for (int i = 0; i < n; i++)
                {
                    var c = ctxHits[i].collider;
                    if (!c || c.transform.IsChildOf(Player.transform)) continue;
                    if (fade && fade.Hides(c, ctxHits[i].point)) continue;                          // clipped roofs and walls
                    var comp = ResolveContext(c);
                    if (comp) return new ContextTarget { target = comp, point = ctxHits[i].point, title = TitleOf(comp) };
                    if (c.isTrigger) continue;
                    ground = ctxHits[i].point;
                    break;
                }
            }
            if (hoverOnly) return null;
            if (ground.HasValue)
            {
                // small things beside where the ray landed (top-down clicks are coarse)
                int m = Physics.OverlapSphereNonAlloc(ground.Value, 0.6f, ctxCols, ~0, QueryTriggerInteraction.Collide);
                Component best = null; float bd = float.MaxValue;
                for (int i = 0; i < m; i++)
                {
                    if (ctxCols[i].transform.IsChildOf(Player.transform)) continue;
                    var comp = ResolveContext(ctxCols[i]);
                    if (!comp || comp is VehicleDriver) continue;
                    float d = (ctxCols[i].bounds.center - ground.Value).sqrMagnitude;
                    if (d < bd) { bd = d; best = comp; }
                }
                if (best) return new ContextTarget { target = best, point = ground.Value, title = TitleOf(best) };
            }
            if (cameraRig && cameraRig.CrosshairView && Focused is Component f && f) return TargetOf(f);                // what [E] would use
            return new ContextTarget { ground = true, point = ground ?? Player.transform.position, title = "HERE" };
        }

        /// <summary>The thing a collider belongs to, as far as context menus go (null: nothing to do with it).</summary>
        static Component ResolveContext(Collider c)
        {
            var wi = c.GetComponentInParent<WorldItem>(); if (wi) return wi;
            var loot = c.GetComponentInParent<Lootable>(); if (loot) return loot;
            var npc = c.GetComponentInParent<MadMax.Npc.Npc>(); if (npc) return npc;
            var animal = c.GetComponentInParent<MadMax.Animals.Animal>(); if (animal) return animal;
            var part = c.GetComponentInParent<VehiclePart>();
            var it = c.GetComponentInParent<IInteractable>() as Component;
            if (part) return it && it.transform.IsChildOf(part.transform) ? it : part;
            if (it) return it;
            var pl = c.GetComponentInParent<Placeable>(); if (pl) return pl;
            var v = c.GetComponentInParent<VehicleDriver>(); if (v) return v;
            return null;
        }

        static string TitleOf(Component c)
        {
            if (!c) return "HERE";
            switch (c)
            {
                case WorldItem w: return w.Label;
                case VehiclePart p: return p.partId.Replace('_', ' ').ToUpperInvariant();
                case MadMax.Npc.Npc n: return n.Profile != null ? n.Profile.Name.ToUpperInvariant() : "SOMEONE";
                case MadMax.Animals.Animal a: return a.Def != null ? a.Def.name.ToUpperInvariant() : "ANIMAL";
                case Lootable l: return l.title;
                case Container k: return k.title;
                case VehicleDriver v: return Name(v);
            }
            var pl = c.GetComponentInParent<Placeable>();
            var def = pl ? FurnitureLibrary.Get(pl.id) : null;
            return def != null ? def.name.ToUpperInvariant() : Name(c);
        }

        void UpdateContextHover(Mouse mouse)
        {
            if (Current) return;
            if (cameraRig && cameraRig.TopDownView && mouse != null && !Aiming && !(Build && Build.Active))
            {
                var t = PickContext(CursorViewport(mouse), true);
                if (t != null && (t.target is Container || (t.target is Lootable l && l.HasLeftovers))) { ContextHover = t.target; ContextHoverFromCursor = true; return; }
            }
            if (Focused is Container fc && fc) ContextHover = fc;
            else if (Focused is Lootable fl && fl && fl.HasLeftovers) ContextHover = fl;
        }

        // ------------------------------------------------------------------ running an option
        /// <summary>Run a context option: at once when in reach (or it needs none), else after walking up to the target.</summary>
        public void RunContext(ContextOption o)
        {
            if (o == null || o.run == null) return;
            var t = contextTarget;
            if (o.reach && t != null && !Current && Player && !Player.Interior && !InReach(t)) { StartWalk(o, t); return; }
            o.run();
        }

        Vector3 ReachPoint(ContextTarget t)
        {
            if (t.ground || !t.target || t.target is VehicleDriver) return t.point;
            var col = t.target.GetComponent<Collider>();
            var eye = Player.Eye.position;
            if (col && col.enabled && col.gameObject.activeInHierarchy && !(col is CharacterController) && (!(col is MeshCollider mc) || mc.convex)) return col.ClosestPoint(eye);
            if (col && col.enabled) return col.bounds.ClosestPoint(eye);
            return t.target.transform.position + (t.target is MadMax.Npc.Npc || t.target is MadMax.Animals.Animal ? Vector3.up : Vector3.zero);
        }

        bool InReach(ContextTarget t)
        {
            var p = ReachPoint(t);
            if (t.ground) { var d = p - Player.transform.position; d.y = 0f; return d.magnitude < 0.7f; }
            return Vector3.Distance(p, Player.Eye.position) < ContextReach;
        }

        void StartWalk(ContextOption o, ContextTarget t)
        {
            walkOption = o; walkTarget = t;
            float d = Vector3.Distance(Player.transform.position, ReachPoint(t));
            walkUntil = Time.time + d / 1.2f + 4f;
            walkLast = Player.transform.position; walkStuck = 0f;
        }

        void StopWalk()
        {
            walkOption = null; walkTarget = null;
            if (Player && job == null) Player.AutoWalk = null;
        }

        void UpdateContextWalk()
        {
            if (walkOption == null) return;
            var t = walkTarget;
            bool gone = !t.ground && !t.target;
            if (gone || !Player || Current || Player.Sitting || Player.Ragdolled || job != null || dying || Player.Interior
                || (Menus.IsOpen && Menus.Current != MenuSystem.Page.Context) || Player.moveInput.sqrMagnitude > 0.1f) { StopWalk(); return; }
            if (InReach(t))
            {
                var o = walkOption;
                StopWalk();
                var look = ReachPoint(t) - Player.transform.position; look.y = 0f;
                if (look.sqrMagnitude > 0.01f) Player.transform.rotation = Quaternion.LookRotation(look);
                o.run();
                return;
            }
            var pos = Player.transform.position;
            walkStuck = (pos - walkLast).sqrMagnitude < 0.0004f ? walkStuck + Time.deltaTime : 0f;
            walkLast = pos;
            if (Time.time > walkUntil || walkStuck > 1.5f) { StopWalk(); Toast("CAN'T GET THERE"); return; }
            var dir = ReachPoint(t) - pos; dir.y = 0f;
            Player.AutoWalk = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.zero;
        }

        void InjectNext(Controls.Act a) { injectNext = a; injectAfter = Time.frameCount + 1; }

        static ContextOption Opt(string label, System.Action run, bool reach = true, string blocked = null, Controls.Act? key = null) =>
            new ContextOption { label = label, run = run, reach = reach, blocked = blocked, key = key.HasValue ? Controls.Name(key.Value) : null };

        // ------------------------------------------------------------------ providers
        /// <summary>Registers the built-in providers once (ContextActions calls it).</summary>
        public void EnsureContextProviders()
        {
            if (ContextActions.Has("world.interact")) return;
            ContextActions.Register("world.interact", (g, t, into) => g.InteractOptions(t, into));
            ContextActions.Register("world.vehicle", (g, t, into) => g.VehicleOptions(t, into));
            ContextActions.Register("world.part", (g, t, into) => g.PartOptions(t, into));
            ContextActions.Register("world.piece", (g, t, into) => g.PieceOptions(t, into));
            ContextActions.Register("world.item", (g, t, into) => g.WorldItemOptions(t, into));
            ContextActions.Register("world.ground", (g, t, into) => g.GroundOptions(t, into));
            ContextActions.Register("pack.item", (g, t, into) => { if (t.item != null) into.AddRange(g.ItemUseOptions(t.item)); });
        }

        static readonly Regex CtxToken = new Regex(@"\[([^\]]+)\]\s*([^\[]*)");

        /// <summary>Every [E]/[T] a target's interactables offer (IInteractable.Use, as the keys do); other keys in the
        /// prompt are pressed for you after walking up.</summary>
        void InteractOptions(ContextTarget t, List<ContextOption> into)
        {
            if (!t.target || t.item != null) return;
            if (Current && t.target.GetComponentInParent<VehicleDriver>() == Current) return;     // driving: the vehicle's own menu
            if (t.target is VehiclePart vp && vp.Socket && t.target.GetComponent<IInteractable>() == null) return;
            foreach (var it in t.target.GetComponents<IInteractable>())
            {
                string pr;
                try { pr = it.Prompt(this); } catch (System.Exception) { pr = null; }
                if (string.IsNullOrEmpty(pr)) continue;
                bool any = false;
                foreach (Match m in CtxToken.Matches(pr))
                {
                    string k = m.Groups[1].Value.Trim(), label = m.Groups[2].Value.Trim().TrimEnd(',', '.', ' ');
                    if (label.Length == 0) continue;
                    var use = it; var comp = t.target;
                    if (k == "E" || k == "T")
                    {
                        bool sec = k == "T";
                        into.Add(Opt(label, () => { use.Use(this, sec); if (comp && comp.TryGetComponent<Placeable>(out var pl)) pl.Dirty(); }, true, null, sec ? Controls.Act.Second : Controls.Act.Use));
                        any = true;
                    }
                    else
                    {
                        var act = Controls.FromToken(Controls.Localize("[" + k + "]").Trim('[', ']'));
                        if (act.HasValue) { var a = act.Value; into.Add(Opt(label, () => InjectNext(a), true, null, a)); any = true; }
                    }
                }
                if (!any) into.Add(new ContextOption { label = pr.Trim(), blocked = pr.Trim() });           // information only ("CUPBOARD (EMPTY)")
            }
            if (t.target is Lootable l && l && (!Lootable.Searched.Contains(l.key) || l.HasLeftovers)) into.Add(Opt("LOOK INSIDE", () => Menus.OpenLoot(l)));
        }

        void VehicleOptions(ContextTarget t, List<ContextOption> into)
        {
            var v = t.target ? t.target.GetComponentInParent<VehicleDriver>() : null;
            if (!v || t.item != null || t.target is WorldItem || t.target.GetComponentInParent<Placeable>() || (t.target is VehiclePart lp && !lp.Socket)) return;
            if (Current == v)
            {
                into.Add(Opt(v.GetComponent<InteriorSpace>() ? "STAND UP" : "GET OUT", () => ExitAnimated(), false, Boarding ? "NOT NOW" : null, Controls.Act.Enter));
                if (v.GetComponent<VehicleLights>()) into.Add(Opt("LIGHTS", () => InjectNext(Controls.Act.Lights), false, null, Controls.Act.Lights));
                if (v.awdSelectable) into.Add(Opt(v.FourWheelDrive ? "2WD" : "4WD", () => InjectNext(Controls.Act.FourWheel), false, null, Controls.Act.FourWheel));
                if (v.hasDiffLock) into.Add(Opt(v.diffLocked ? "OPEN THE DIFF" : "LOCK THE DIFF", () => InjectNext(Controls.Act.DiffLock), false, null, Controls.Act.DiffLock));
                var tc = TowTargetFor(v, out var towText);
                if (towText != null) into.Add(Opt(towText.Replace("[J] ", ""), () => DoTow(v, tc), false, null, Controls.Act.Hitch));
                into.Add(Opt("RECOVER / PARLEY", () => InjectNext(Controls.Act.Recover), false, null, Controls.Act.Recover));
                into.Add(Opt("RADIO ON / OFF", () => InjectNext(Controls.Act.RadioPower), false, null, Controls.Act.RadioPower));
                if (v.GetComponent<VehicleClimate>()) into.Add(Opt("CLIMATE", () => InjectNext(Controls.Act.Climate), false, null, Controls.Act.Climate));
                return;
            }
            if (Current) return;
            var interior = v.GetComponent<InteriorSpace>();
            if (interior && Player.Interior == interior)
            {
                if (v.driveable) into.Add(Opt("DRIVE " + Name(v), () => EnterAnimated(v), false, null, Controls.Act.Enter));
                int door = interior.NearestDoorInside(Player.transform.localPosition, 99f);
                if (door >= 0) into.Add(Opt("LEAVE " + Name(v), () => LeaveInterior(door), false));
            }
            else if (interior) into.Add(Opt("ENTER " + Name(v), () => { int d = interior.NearestDoorOutside(Player.transform.position, 99f); if (d >= 0) EnterInterior(interior, d); }, true, null, Controls.Act.Enter));
            else if (v.driveable && cars.Contains(v)) into.Add(Opt("DRIVE " + Name(v), () => EnterAnimated(v), true, v.aiDriven ? "SOMEONE IS AT THE WHEEL" : null, Controls.Act.Enter));
            if (v.TryGetComponent<VehicleSystems>(out var sys))
            {
                if (v.TryGetComponent<FuelTanker>(out var tanker))
                {
                    var partner = tanker.FindPartner();
                    if (partner)
                    {
                        into.Add(Opt("PUMP FUEL INTO " + Name(partner), () => tanker.Toggle(FuelTanker.Mode.Fill), true, null, Controls.Act.Service));
                        into.Add(Opt("DRAIN " + Name(partner) + " INTO THE TANKER", () => tanker.Toggle(FuelTanker.Mode.Drain), true, null, Controls.Act.Siphon));
                    }
                }
                else if (sys.disconnected) into.Add(Opt("RECONNECT THE BATTERY", () => ReconnectBattery(v), true, Inventory.GetItem(ItemIds.Wrench) > 0 ? null : "NEEDS A WRENCH", Controls.Act.Service));
                else
                {
                    if (sys.NeedsService(Inventory) || sys.CanMaintain(Inventory)) into.Add(Opt("REFUEL / SERVICE", () => ServiceVehicle(v), true, null, Controls.Act.Service));
                    if (sys.TotalFluids >= 1f) into.Add(Opt("SIPHON " + Mathf.FloorToInt(sys.TotalFluids) + " L", () => SiphonVehicle(v), true, null, Controls.Act.Siphon));
                }
            }
            if (Inventory.GetItem("tool_welder") > 0 && v.TryGetComponent<VehicleArmor>(out var armour)) into.Add(Opt("WELD ARMOUR", () => Menus.OpenArmour(armour), true, null, Controls.Act.Armour));
            if (v.TryGetComponent<Container>(out var cargo)) into.Add(Opt("OPEN " + cargo.title, () => Menus.OpenContainer(cargo)));
            var carried = Player.Carried;
            if (carried && v.TryGetComponent<VehicleChassis>(out var chassis))
            {
                MountSocket best = null; float bd = float.MaxValue;
                foreach (var s in chassis.Sockets)
                {
                    if (!s.IsFree || !s.CanAccept(carried)) continue;
                    float d = (s.transform.position - t.point).sqrMagnitude;
                    if (d < bd) { bd = d; best = s; }
                }
                if (best)
                {
                    string why = Inventory.GetItem(ItemIds.Wrench) <= 0 ? "NEEDS A WRENCH" : NeedsJack(carried) ? "HEAVY WHEEL: NEEDS A JACK" : null;
                    var socket = best;
                    into.Add(Opt("MOUNT " + carried.partId.Replace('_', ' ').ToUpperInvariant() + " > " + best.name.ToUpperInvariant(), () => { HoldWrench(); MountCarried(socket); }, true, why, Controls.Act.Use));
                }
            }
        }

        void HoldWrench()
        {
            if (!Holding(ItemIds.Wrench) && Inventory.GetItem(ItemIds.Wrench) > 0) Player.Equip(ToolLibrary.Create(ItemIds.Wrench, propMaterial));
        }

        void PartOptions(ContextTarget t, List<ContextOption> into)
        {
            if (!(t.target is VehiclePart p) || !p || Current) return;
            string name = p.partId.Replace('_', ' ').ToUpperInvariant() + (p.damage > 0.05f ? " " + Mathf.RoundToInt((1f - Mathf.Clamp01(p.damage)) * 100f) + "%" : "");
            string hands = Player.Carried ? "YOUR HANDS ARE FULL" : null;
            if (p.Socket)
            {
                string why = Inventory.GetItem(ItemIds.Wrench) <= 0 ? "NEEDS A WRENCH" : NeedsJack(p) ? "NEEDS A JACK TO LIFT IT" : hands;
                into.Add(Opt("TAKE OFF " + name, () => { HoldWrench(); TakePart(p); }, true, why, Controls.Act.Use));
            }
            else into.Add(Opt("PICK UP " + name, () => TakePart(p), true, hands, Controls.Act.Use));
        }

        void PieceOptions(ContextTarget t, List<ContextOption> into)
        {
            var pl = t.target ? t.target.GetComponentInParent<Placeable>() : null;
            if (!pl || Current || !OwnsPiece(pl) || IsStoryProp(pl) || !Build) return;
            var def = FurnitureLibrary.Get(pl.id);
            if (def == null) return;
            string hammer = Inventory.GetItem(ItemIds.ClawHammer) > 0 ? null : "NEEDS A CLAW HAMMER";
            if (pl.hits < def.hits) into.Add(Opt("REPAIR " + pl.hits + "/" + def.hits, () => Build.Repair(pl), true, hammer, Controls.Act.BuildRepair));
            var up = def.upgrade != null ? FurnitureLibrary.Get(def.upgrade) : null;
            if (up != null) into.Add(Opt("UPGRADE TO " + up.name, () => Build.Upgrade(pl), true, hammer, Controls.Act.BuildUpgrade));
            into.Add(Opt("DISMANTLE " + def.name, () => Build.Dismantle(pl), true, hammer, Controls.Act.BuildDismantle));
        }

        void WorldItemOptions(ContextTarget t, List<ContextOption> into)
        {
            if (!(t.target is WorldItem w) || !w || Current) return;
            var net = MadMax.Net.NetSession.Instance;
            if (w.count > 1 && !(net && net.IsClient && w.netId != 0)) into.Add(Opt("TAKE ONE", () => { if (w) TakeFromWorldItem(w, 1); }));
            into.Add(Opt("LOOK THROUGH THE FLOOR", () => Menus.OpenFloor()));
        }

        void GroundOptions(ContextTarget t, List<ContextOption> into)
        {
            if (!t.ground || Current) return;
            if (cameraRig && cameraRig.TopDownView && !Player.Interior) into.Add(Opt("WALK HERE", () => { }));
            if (Player.Carried)
                into.Add(Opt("DROP " + Player.Carried.partId.Replace('_', ' ').ToUpperInvariant(), () =>
                {
                    var part = Player.Carried;
                    if (!part) return;
                    Player.DropCarried();
                    MadMax.Net.NetSession.Instance?.SendPartDropped(part, Player.transform.forward * 1.5f);
                }, false, null, Controls.Act.Drop));
            if (FloorStacks(Player.transform.position, MenuSystem.FloorReach).Count > 0) into.Add(Opt("LOOK THROUGH THE FLOOR", () => Menus.OpenFloor(), false));
        }

        /// <summary>The pack entry's own options: use / eat / equip / wear, hotbar, drop, place.</summary>
        public List<ContextOption> ItemUseOptions(string key)
        {
            var l = new List<ContextOption>();
            int have = PackCount(key);
            if (have <= 0) return l;
            bool res = IsResKey(key, out _);
            if (!res)
            {
                var cat = ItemCatalog.Category(key);
                bool inHand = Player && Player.Tool && Player.Tool.id == key;
                string verb = cat switch
                {
                    ItemCategory.Tool or ItemCategory.Weapon => inHand ? "PUT AWAY" : "EQUIP",
                    ItemCategory.Media => key.StartsWith("vhs_") ? "WATCH" : "READ",
                    ItemCategory.Food => key.StartsWith("drink") ? "DRINK" : "EAT",
                    ItemCategory.Throwable => "THROW",
                    ItemCategory.Consumable => "USE",
                    ItemCategory.Seed => key.StartsWith("sapling_") ? "PLANT" : "SOW WITH THIS",
                    ItemCategory.Kit => "BUILD IT",
                    ItemCategory.Clothing => Wearing(ClothingLibrary.Get(key)?.id) ? "TAKE OFF" : "WEAR",
                    ItemCategory.Ammo => key.StartsWith("bait_") ? "USE AS BAIT" : null,
                    ItemCategory.Other => key.StartsWith("animal_") ? "RELEASE" : null,
                    _ => null
                };
                bool handsOnly = cat == ItemCategory.Tool || cat == ItemCategory.Weapon || cat == ItemCategory.Throwable;
                if (verb != null)
                {
                    if (cat == ItemCategory.Clothing) l.Add(Opt(verb, () => ToggleWear(key), false));
                    else l.Add(Opt(verb, () => UseItem(key), false, handsOnly && Current ? "NOT WHILE DRIVING" : null));
                }
                if (HotbarItem(key) && System.Array.IndexOf(Hotbar, key) < 0)
                {
                    int free = System.Array.IndexOf(Hotbar, null), slot = free >= 0 ? free : HotbarSize - 1;
                    l.Add(Opt("PUT ON HOTBAR " + (slot + 1), () => AssignHotbar(slot, key), false));
                }
            }
            l.Add(Opt(res ? "DROP " + Mathf.Min(5, have) : "DROP ONE", () => DropFromPack(key, res ? Mathf.Min(5, have) : 1), false, null, Controls.Act.Drop));
            if (have > 1 && !(res && have <= 5)) l.Add(Opt("DROP ALL (" + have + ")", () => DropFromPack(key, have), false));
            string why = CanPlaceItem(key, out var w) ? null : w;
            l.Add(Opt("PLACE" + (res ? " " + Mathf.Min(10, have) : ""), () => { Menus.Close(); Defer(() => BeginPlaceItem(key)); }, false, why, Controls.Act.Build));
            return l;
        }

        /// <summary>Put a garment on (replacing what is worn in its slot) or take it off.</summary>
        public void ToggleWear(string key)
        {
            var cd = ClothingLibrary.Get(key);
            if (cd == null || !Player || Inventory.GetItem(ClothingLibrary.ItemId(cd)) <= 0) return;
            var rig = Player.Rig;
            if (rig.outfit.Remove(cd.id)) { Player.RebuildBody(); Toast("TOOK OFF " + cd.name); return; }
            foreach (var id in new List<string>(rig.outfit)) { var d = ClothingLibrary.Get(id); if (d != null && d.slot == cd.slot) rig.outfit.Remove(id); }
            rig.outfit.Add(cd.id);
            Player.RebuildBody();
            Toast("WEARING " + cd.name);
        }

        // ------------------------------------------------------------------ loot window helpers
        /// <summary>The last one of an item left the pack (out of the hand, off the body, reading stops).</summary>
        public void PackReleased(string key) => LetGoOf(key);

        /// <summary>Search a spot into its own leftovers (the loot window shows them).</summary>
        public bool SearchInto(Lootable l) => l && l.Search(this, l.Leftovers(true));

        /// <summary>World items within <paramref name="r"/> of a point, summed per item id / "res:N" (nearest first).</summary>
        public List<KeyValuePair<string, int>> FloorStacks(Vector3 around, float r)
        {
            var order = new List<string>(); var sum = new Dictionary<string, int>();
            foreach (var w in WorldItem.All)
            {
                if (!w || w.count <= 0 || (w.transform.position - around).sqrMagnitude > r * r) continue;
                if (!sum.ContainsKey(w.key)) { order.Add(w.key); sum[w.key] = 0; }
                sum[w.key] += w.count;
            }
            var l = new List<KeyValuePair<string, int>>();
            foreach (var k in order) l.Add(new KeyValuePair<string, int>(k, sum[k]));
            return l;
        }

        public int FloorCount(string key, Vector3 around, float r)
        {
            int n = 0;
            foreach (var w in WorldItem.All) if (w && w.key == key && w.count > 0 && (w.transform.position - around).sqrMagnitude <= r * r) n += w.count;
            return n;
        }

        /// <summary>Up to <paramref name="n"/> of a key from the floor into the pack: whole stacks through PickUpItem (online
        /// a client's take is granted by the host later and counts 0 here), part of a stack split off (host / offline).</summary>
        public int TakeFromFloor(string key, int n, Vector3 around, float r)
        {
            var net = MadMax.Net.NetSession.Instance;
            bool client = net && net.IsClient;
            var near = new List<WorldItem>();
            foreach (var w in WorldItem.All) if (w && w.key == key && w.count > 0 && (w.transform.position - around).sqrMagnitude <= r * r) near.Add(w);
            near.Sort((a, b) => (a.transform.position - around).sqrMagnitude.CompareTo((b.transform.position - around).sqrMagnitude));
            int moved = 0;
            foreach (var w in near)
            {
                if (moved >= n) break;
                int want = n - moved;
                bool async = client && w.netId != 0;
                if (want >= w.count || async) { int c = w.count; if (PickUpItem(w) && !async) moved += c; }
                else moved += TakeFromWorldItem(w, want);
            }
            return moved;
        }

        /// <summary>Split <paramref name="n"/> off a world stack into the pack (the rest stays; the new count replicates).</summary>
        public int TakeFromWorldItem(WorldItem w, int n)
        {
            if (!w || n <= 0) return 0;
            if (n >= w.count) { int c = w.count; return PickUpItem(w) ? c : 0; }
            var net = MadMax.Net.NetSession.Instance;
            if (net && net.IsClient && w.netId != 0) return 0;                                    // the host hands out whole stacks only
            using (Inventory.Source("PICKED UP")) GiveToPack(w.key, n, w.quality);
            w.count -= n; w.Refresh();
            MadMax.Audio.Sfx.Play("pickup", w.transform.position, 0.5f, 1.1f, 20f);
            net?.SendItemSpawn(w, Vector3.zero);
            return n;
        }

        // ------------------------------------------------------------------ respawn chooser
        /// <summary>A place to wake up after dying.</summary>
        public struct RespawnPoint { public string label, kind; public Vector3 at; public uint piece; }

        /// <summary>Where you can wake up, the default first: your beds (the one you set as spawn, then the homestead's,
        /// then by distance), a spawn point set elsewhere, your claims, the first fleet car, the start road.</summary>
        public List<RespawnPoint> RespawnPoints()
        {
            var list = new List<RespawnPoint>();
            var here = Player ? Player.transform.position : WorldSpawn;
            var beds = new List<(int rank, float d, RespawnPoint p)>();
            bool spawnIsBed = false;
            foreach (var pl in Placeable.All)
            {
                if (!pl || !(pl.owner == Stats.name || IsHomestead(pl))) continue;
                var bed = pl.GetComponentInChildren<Bed>();
                if (!bed) continue;
                var at = bed.transform.position + bed.transform.forward * 1.2f + Vector3.up * 0.2f;
                bool chosen = spawnPoint.HasValue && (spawnPoint.Value - at).sqrMagnitude < 0.36f;
                spawnIsBed |= chosen;
                string label = "BED " + (IsHomestead(pl) ? "AT THE HOMESTEAD" : PlaceName(at));
                beds.Add((chosen ? 0 : IsHomestead(pl) ? 1 : 2, (at - here).sqrMagnitude, new RespawnPoint { label = label, kind = "bed", at = at, piece = pl.Id }));
            }
            beds.Sort((a, b) => a.rank != b.rank ? a.rank.CompareTo(b.rank) : a.d.CompareTo(b.d));
            foreach (var b in beds) list.Add(b.p);
            if (spawnPoint.HasValue && !spawnIsBed) list.Insert(beds.Count > 0 ? 1 : 0, new RespawnPoint { label = "YOUR SPAWN POINT " + PlaceName(spawnPoint.Value), kind = "spawn", at = spawnPoint.Value });
            if (beds.Count == 0)
                foreach (var pl in Placeable.All)
                    if (pl && IsHomestead(pl) && pl.id == "workbench") { list.Add(new RespawnPoint { label = "THE HOMESTEAD", kind = "homestead", at = Ground(pl.transform.position + pl.transform.forward * 1.5f), piece = pl.Id }); break; }
            foreach (var f in ClaimFlag.All)
                if (f && f.Owner == Stats.name) list.Add(new RespawnPoint { label = "CLAIM " + PlaceName(f.transform.position), kind = "claim", at = f.transform.position + f.transform.forward * 1.5f + Vector3.up * 0.2f });
            var home = fleet.Count > 0 && fleet[0] ? fleet[0] : null;
            if (home) list.Add(new RespawnPoint { label = "BY THE " + Name(home), kind = "fleet", at = ExitPoint(home) });
            list.Add(new RespawnPoint { label = "THE START ROAD", kind = "start", at = Ground(WorldSpawn) });
            var seen = new HashSet<string>();
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                for (int k = 2; !seen.Add(p.label); k++) p.label = list[i].label + " " + k;
                list[i] = p;
            }
            return list;
        }

        Vector3 Ground(Vector3 p) { if (terrain) p.y = terrain.Height(p.x, p.z) + 0.1f; return p; }

        static readonly string[] Compass = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        /// <summary>"IN DUSTWATER", "1.2 KM NE OF DUSTWATER" (the nearest settlement).</summary>
        public string PlaceName(Vector3 at)
        {
            if (World == null || World.settlements.Count == 0) return Mathf.RoundToInt(at.x) + "," + Mathf.RoundToInt(at.z);
            Settlement best = null; float bd = float.MaxValue;
            var p = new Vector2(at.x, at.z);
            foreach (var s in World.settlements) { float d = Vector2.Distance(s.pos, p); if (d < bd) { bd = d; best = s; } }
            string town = MadMax.Npc.Market.TownName(best);
            if (bd < best.radius + 30f) return "IN " + town;
            var dir = p - best.pos;
            int c = Mathf.RoundToInt(Mathf.Repeat(Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, 360f) / 45f) % 8;
            return (bd < 1000f ? Mathf.RoundToInt(bd / 10f) * 10 + " M " : (bd / 1000f).ToString("0.0") + " KM ") + Compass[c] + " OF " + town;
        }

        /// <summary>Dead: show the respawn list (4 s after the fall). Without a choice within a minute the default is taken.</summary>
        void RespawnChoice()
        {
            if (!dying) return;
            if (Dedicated || !Menus) { RespawnAt(RespawnPoints()[0]); return; }
            respawnOpenedAt = Time.unscaledTime;
            Menus.ClosePopup();
            Menus.Open(MenuSystem.Page.Respawn);
        }

        /// <summary>Wake up at a respawn point (the old rules: half health, open wounds bandaged). False when not dead.</summary>
        public bool RespawnAt(RespawnPoint p)
        {
            if (!dying) return false;
            respawnOpenedAt = -1f;
            lastRespawn = p.label;
            if (Menus && Menus.Current == MenuSystem.Page.Respawn) Menus.Close();
            WakeUpAt(p.at);
            Toast("YOU COME TO: " + p.label);
            return true;
        }

        /// <summary>Label of the last chosen respawn point (saved).</summary>
        public string LastRespawn => lastRespawn;

        // ------------------------------------------------------------------ block hooks (SaveData.blockContext)
        partial void ContextSave(SaveData d)
        {
            d.blockContext = new List<string>();
            foreach (var kv in Lootable.Left)
                if (kv.Value != null && Lootable.HasAny(kv.Value)) d.blockContext.Add("L\t" + kv.Key + "\t" + InventoryCodec.Encode(kv.Value));
            if (lastRespawn != null) d.blockContext.Add("R\t" + lastRespawn);
        }

        partial void ContextLoad(SaveData d)
        {
            Lootable.Left.Clear(); lastRespawn = null;
            if (d.blockContext == null) return;
            foreach (var line in d.blockContext)
            {
                var f = line.Split('\t');
                if (f.Length == 3 && f[0] == "L") { var inv = new Inventory(); InventoryCodec.Decode(inv, f[2]); Lootable.Left[f[1]] = inv; }
                else if (f.Length == 2 && f[0] == "R") lastRespawn = f[1];
            }
        }

        partial void ContextNewGame() { Lootable.Left.Clear(); lastRespawn = null; }
    }
}
