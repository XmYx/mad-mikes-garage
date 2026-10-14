using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>How the body does a use (<see cref="UseAnim"/>). Append only: pieces may store or switch on it.</summary>
    public enum UseKind
    {
        None, Reach, FlipSwitch, Press, LiftLid, PullCord, Pump, Crank, TurnValve, Kneel, Search, PickUp, PickHigh, Pour, Stoke,
        Push, Drag, Milk, Shear, Hitch, Talk, Eat, Drink, Wave, LieDown, Climb, Wrench
    }

    /// <summary>The use animation for one [E]/[T] (default from the game's table by component type; a piece overrides it
    /// with <see cref="IUseAnimated"/>). The static events let a piece move its own parts in time with the hands: a lid
    /// opens on <see cref="Begin"/> + seconds, a lever flips on <see cref="Apply"/>, everything settles on <see cref="End"/>.</summary>
    public struct UseAnim
    {
        public UseKind kind;
        /// <summary>Seconds of the reach before Use() runs (0 = the kind's default; at most 1.5 s with the follow-through).</summary>
        public float seconds;
        /// <summary>World point the hands go to (null = the nearest point of the target).</summary>
        public Vector3? hand;
        /// <summary>Held in the right hand meanwhile: an item id or a <see cref="WastelandGame.WorkPropMesh"/> key ("can", "oil"); null = none.</summary>
        public string prop;
        /// <summary>Keep the hands on it while the page Use() opened stays up (also automatic for any page it opens).</summary>
        public bool hold;

        public UseAnim(UseKind kind, float seconds = 0f, string prop = null, bool hold = false) { this.kind = kind; this.seconds = seconds; this.prop = prop; this.hold = hold; hand = null; }
        public static UseAnim Of(UseKind kind, float seconds = 0f) => new UseAnim(kind, seconds);

        /// <summary>The player starts a use: (target component, kind, seconds until Use() runs).</summary>
        public static event System.Action<Component, UseKind, float> Begin;
        /// <summary>Use() has just run on the target (the hands are on it).</summary>
        public static event System.Action<Component, UseKind> Apply;
        /// <summary>The hands left it (follow-through over, page closed, or cancelled before Apply).</summary>
        public static event System.Action<Component, UseKind> End;
        /// <summary>The target of the use playing now (null = none), its kind and 0..1 of the reach (1 from Apply on).</summary>
        public static Component Active { get; private set; }
        public static UseKind ActiveKind { get; private set; }
        public static float ActiveT { get; internal set; }

        internal static void FireBegin(Component c, UseKind k, float s)
        {
            Active = c; ActiveKind = k; ActiveT = 0f;
            try { Begin?.Invoke(c, k, s); } catch (System.Exception e) { Debug.LogException(e); }
        }

        internal static void FireApply(Component c, UseKind k)
        {
            ActiveT = 1f;
            try { Apply?.Invoke(c, k); } catch (System.Exception e) { Debug.LogException(e); }
        }

        internal static void FireEnd(Component c, UseKind k)
        {
            if (Active == c) { Active = null; ActiveKind = UseKind.None; ActiveT = 0f; }
            try { End?.Invoke(c, k); } catch (System.Exception e) { Debug.LogException(e); }
        }

        internal static void ResetStatics() { Begin = null; Apply = null; End = null; Active = null; ActiveKind = UseKind.None; ActiveT = 0f; }
    }

    /// <summary>An interactable that picks its own use animation (else the game's table by type decides).</summary>
    public interface IUseAnimated { UseAnim Anim(bool secondary); }

    /// <summary>Use animations (setting WORK ANIMATION): every [E]/[T] on a piece, a loot spot, a person or an animal — and
    /// a few verbs of the game's own (drinking at a lake, hitching on foot, eating) — plays with the body first: walk up
    /// when more than 1.2 m off, face it, reach / kneel / pump / pull / lift the lid at the height of the thing, and Use()
    /// runs when the hands get there; a page it opens keeps the hands on it until it closes. Every use is under 1.5 s and
    /// any move stops it (before the contact: without effect). Off, seated, swimming or in a vehicle: instant.</summary>
    public partial class WastelandGame
    {
        sealed class UseJob
        {
            public Component target;
            public Transform rel;                    // the target's transform (the point rides along with it)
            public System.Action act;
            public UseAnim anim;
            public Vector3 pointLocal, pointWorld;
            public float reach, follow, t, age, walkT, stuckT, height, dist;
            public bool applied, walking, gesture, held, prop, npc;
            public int startFrame;
            public Vector3 stuckAt;
            public MenuSystem.Page pageBefore;
            public string clip;
            public IInteractable it;
            public bool secondary;
        }

        UseJob use;

        /// <summary>A use animation is playing (walking up, reaching, holding a page, following through).</summary>
        public bool UseRunning => use != null;
        /// <summary>Use() of the running animation has not run yet.</summary>
        public bool UsePending => use != null && !use.applied && !use.gesture;
        public UseKind UseNowKind => use != null ? use.anim.kind : UseKind.None;
        /// <summary>The last use animation: its kind, the seconds from the key to Use() running, and how it ended.</summary>
        public UseKind LastUseKind { get; private set; }
        public float LastUseDelay { get; private set; } = -1f;
        public WorkOutcome LastUseOutcome { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetUseAnimStatics() => UseAnim.ResetStatics();

        // ------------------------------------------------------------------ the table

        /// <summary>Default kind for [E] and [T] by the interactable's type name (pieces can override with IUseAnimated,
        /// other code can add rows with <see cref="SetUseRule"/>). None = instant (the piece has its own way: seats).</summary>
        static readonly Dictionary<string, (UseKind e, UseKind t)> useRules = new Dictionary<string, (UseKind, UseKind)>
        {
            // storage, stations, boards
            { "Container", (UseKind.LiftLid, UseKind.LiftLid) }, { "ColdStore", (UseKind.LiftLid, UseKind.Wrench) },
            { "CraftingStation", (UseKind.Reach, UseKind.Reach) }, { "PlayerStall", (UseKind.LiftLid, UseKind.Reach) },
            { "BountyBoard", (UseKind.Reach, UseKind.Press) }, { "Bookshelf", (UseKind.PickHigh, UseKind.PickHigh) },
            { "Wardrobe", (UseKind.Reach, UseKind.Reach) }, { "TrophyMount", (UseKind.PickHigh, UseKind.PickHigh) },
            { "Mirror", (UseKind.None, UseKind.None) }, { "OreCart", (UseKind.LiftLid, UseKind.Push) },
            // doors and gates
            { "Door", (UseKind.Reach, UseKind.Press) }, { "RollerDoor", (UseKind.Crank, UseKind.Press) }, { "MotorGate", (UseKind.Press, UseKind.Press) },
            // power and switches
            { "Generator", (UseKind.PullCord, UseKind.Pour) }, { "Climate", (UseKind.Press, UseKind.Stoke) },
            { "LightSwitch", (UseKind.FlipSwitch, UseKind.FlipSwitch) }, { "UtilitySwitch", (UseKind.FlipSwitch, UseKind.Press) },
            { "PowerBreaker", (UseKind.FlipSwitch, UseKind.FlipSwitch) }, { "PoweredLight", (UseKind.FlipSwitch, UseKind.Press) },
            { "IrrigationTimer", (UseKind.Press, UseKind.Press) }, { "BatteryRack", (UseKind.Kneel, UseKind.Kneel) },
            { "RadioSet", (UseKind.Press, UseKind.Press) }, { "AlarmBell", (UseKind.PickHigh, UseKind.PickHigh) },
            // water, fuel, air
            { "HandPump", (UseKind.Pump, UseKind.Pump) }, { "TransferPump", (UseKind.Crank, UseKind.Press) },
            { "WaterTap", (UseKind.TurnValve, UseKind.TurnValve) }, { "WaterOutlet", (UseKind.TurnValve, UseKind.TurnValve) },
            { "FluidStore", (UseKind.TurnValve, UseKind.TurnValve) }, { "FilterCartridge", (UseKind.Kneel, UseKind.Kneel) },
            { "SolarStill", (UseKind.Pour, UseKind.Kneel) }, { "BiogasDigester", (UseKind.Pour, UseKind.TurnValve) },
            { "Pumpjack", (UseKind.TurnValve, UseKind.TurnValve) }, { "GasPump", (UseKind.Reach, UseKind.Pour) },
            { "AirCompressor", (UseKind.TurnValve, UseKind.Press) }, { "O2Rack", (UseKind.Reach, UseKind.Reach) },
            // beds, seats (seats animate themselves: PlayerCharacter.SitOn)
            { "Bed", (UseKind.LieDown, UseKind.Press) }, { "ClinicBed", (UseKind.None, UseKind.Reach) }, { "SurgeryTable", (UseKind.None, UseKind.None) },
            { "Seat", (UseKind.None, UseKind.None) }, { "GunNest", (UseKind.None, UseKind.None) }, { "Watchtower", (UseKind.None, UseKind.None) },
            { "RoofAccess", (UseKind.None, UseKind.None) }, { "PassengerSeat", (UseKind.None, UseKind.None) }, { "Latrine", (UseKind.Kneel, UseKind.Kneel) },
            // garden, traps, animals, the hunt
            { "GardenPlot", (UseKind.Kneel, UseKind.Kneel) }, { "PlantedTree", (UseKind.PickHigh, UseKind.PickHigh) },
            { "Beehive", (UseKind.LiftLid, UseKind.LiftLid) }, { "FishTrap", (UseKind.Kneel, UseKind.Kneel) },
            { "SnareTrap", (UseKind.Kneel, UseKind.Kneel) }, { "SluiceBox", (UseKind.Kneel, UseKind.Pour) },
            { "ButcherTable", (UseKind.Reach, UseKind.Drag) }, { "Carcass", (UseKind.Kneel, UseKind.Drag) },
            { "CarcassDrag", (UseKind.Drag, UseKind.Drag) }, { "Trough", (UseKind.Pour, UseKind.Pour) },
            // the world
            { "WorldItem", (UseKind.PickUp, UseKind.Kneel) }, { "Lootable", (UseKind.Search, UseKind.Search) },
            { "Checkpoint", (UseKind.Press, UseKind.Press) }, { "TrailerDeck", (UseKind.Hitch, UseKind.Press) },
            { "Npc", (UseKind.Talk, UseKind.Talk) },
        };

        /// <summary>Set the default use animation of an interactable type (by its class name).</summary>
        public static void SetUseRule(string typeName, UseKind e, UseKind t) => useRules[typeName] = (e, t);

        /// <summary>The use animation for <paramref name="it"/> on [E] / [T] (<paramref name="prompt"/>: what it offers now).</summary>
        public static UseAnim UseAnimFor(IInteractable it, bool secondary, string prompt)
        {
            if (it == null) return UseAnim.Of(UseKind.None);
            if (it is IUseAnimated ua) return ua.Anim(secondary);
            string name = it.GetType().Name;
            if (name == "Animal") return UseAnim.Of(AnimalKind(prompt, secondary));
            if (useRules.TryGetValue(name, out var r)) return UseAnim.Of(secondary ? r.t : r.e);
            return UseAnim.Of(KindFromPrompt(prompt, secondary));
        }

        static UseKind AnimalKind(string prompt, bool secondary)
        {
            string p = Verb(prompt, secondary);
            if (p == null) return secondary ? UseKind.Wave : UseKind.Reach;
            if (p.Contains("RIDE")) return UseKind.None;                                              // the saddle hop (SitOn)
            if (p.Contains("MILK")) return UseKind.Milk;
            if (p.Contains("SHEAR")) return UseKind.Shear;
            if (p.Contains("EGGS") || p.Contains("TREAT")) return UseKind.Kneel;
            if (p.Contains("SADDLEBAGS") || p.Contains("SADDLE")) return UseKind.Reach;
            if (secondary) return UseKind.Wave;                                                       // orders: a hand signal
            return UseKind.Reach;                                                                     // offer, feed: a hand held out
        }

        static UseKind KindFromPrompt(string prompt, bool secondary)
        {
            string p = Verb(prompt, secondary);
            if (p == null) return UseKind.Reach;
            if (p.Contains("SIT") || p.Contains("RIDE") || p.Contains("CLIMB") || p.Contains("LIE DOWN") || p.Contains("MAN THE")) return UseKind.None;
            if (p.Contains("SWITCH")) return UseKind.FlipSwitch;
            if (p.Contains("PUMP")) return UseKind.Pump;
            if (p.Contains("CRANK")) return UseKind.Crank;
            if (p.Contains("SEARCH") || p.Contains("LOOT") || p.Contains("RUMMAGE")) return UseKind.Search;
            if (p.Contains("PICK UP")) return UseKind.PickUp;
            if (p.Contains("PICK")) return UseKind.PickHigh;
            if (p.Contains("FILL") || p.Contains("POUR") || p.Contains("FEED")) return UseKind.Pour;
            if (p.Contains("OPEN")) return UseKind.LiftLid;
            if (p.Contains("TALK") || p.Contains("PARLEY")) return UseKind.Talk;
            if (p.Contains("REPAIR") || p.Contains("FIX")) return UseKind.Wrench;
            if (p.Contains("PRESS") || p.Contains("RING") || p.Contains("PAY")) return UseKind.Press;
            return UseKind.Reach;
        }

        /// <summary>The part of a prompt after [E] (or [T]) up to the next key.</summary>
        static string Verb(string prompt, bool secondary)
        {
            if (string.IsNullOrEmpty(prompt)) return null;
            int i = prompt.IndexOf(secondary ? "[T]" : "[E]", System.StringComparison.Ordinal);
            if (i < 0) return secondary ? null : prompt;
            int j = prompt.IndexOf('[', i + 3);
            return j < 0 ? prompt.Substring(i + 3) : prompt.Substring(i + 3, j - i - 3);
        }

        // ------------------------------------------------------------------ entry points

        /// <summary>[E] / [T] on an interactable: through its use animation, or at once (setting off, seated, a vehicle,
        /// or a kind of None). A second press while the hands are on the way does it now.</summary>
        public void UseInteractable(Component comp, IInteractable it, bool secondary, string prompt = null)
        {
            if (it == null) return;
            if (Player && Player.BodyBusy) return;                                                    // lying down, sitting down: not now
            if (use != null && !use.applied && !use.gesture)
            {
                if (use.it == it && use.secondary == secondary && Time.frameCount > use.startFrame) ApplyUse(use);   // impatient: do it now
                return;
            }
            if (prompt == null) { try { prompt = it.Prompt(this); } catch (System.Exception) { prompt = null; } }
            var a = UseAnimFor(it, secondary, prompt);
            var target = it as Component ?? comp;
            System.Action act = () =>
            {
                it.Use(this, secondary);
                if (comp && comp.TryGetComponent<Placeable>(out var pl)) pl.Dirty();
            };
            if (a.kind == UseKind.LieDown && target && UseAnimatedNow(a.kind))
            {
                // a bed: lie down first, the night's sleep runs when lying, then up again beside it
                if (Player.LieOn(target.transform, act))
                {
                    LastUseKind = a.kind; LastUseOutcome = WorkOutcome.None;
                    UseAnim.FireBegin(target, a.kind, 1.4f);
                    return;
                }
            }
            if (!StartUse(target, a, act, prompt, it, secondary)) act();
        }

        /// <summary>A verb of the game's own with the body first (drinking at a lake, hitching on foot): <paramref name="act"/>
        /// runs when the hands reach <paramref name="point"/>, or now when it can't be animated.</summary>
        public void AnimatedUse(UseKind kind, Vector3 point, System.Action act, Component target = null, string prop = null)
        {
            if (use != null && !use.applied && !use.gesture) return;
            var a = new UseAnim(kind, 0f, prop) { hand = point };
            if (!StartUse(target, a, act, null, null, false)) act();
        }

        /// <summary>A gesture over whatever else happens (eating, drinking from a canteen, waving): no walking, no delay —
        /// the effect has run already. Interrupts nothing important; any move ends it.</summary>
        public void Gesture(UseKind kind, string prop = null, float seconds = 0f)
        {
            if (SeatedGestureHook(kind, prop)) return;                                                // seated: over the sitting pose (WastelandGame.BodyRest)
            if (!UseAnimatedNow(kind) || (use != null && !use.gesture)) return;
            if (use != null) EndUse(use, true);
            var j = NewJob(null, new UseAnim(kind, seconds, prop), null);
            j.gesture = true; j.applied = true;
            j.pointWorld = Player.transform.position + Player.transform.forward * 0.5f + Vector3.up * 1.4f;
            BeginPose(j);
            use = j;
        }

        bool UseAnimatedNow(UseKind kind) => kind != UseKind.None && GameSettings.Current.workAnimation && Player && !Current && job == null && !Boarding
            && !Player.SeatedIn && !Player.Sitting && !Player.Swimming && !Player.Traversing && !Player.Ragdolled && !Player.BodyBusy
            && Player.Climbing == null && !Player.Carried && !(Vitals && Vitals.Dead) && Player.gameObject.activeInHierarchy
            && Player.LowState == PlayerCharacter.Low.None && !Player.Puppeted;                         // low on the ground / boarding: at once

        UseJob NewJob(Component target, UseAnim a, System.Action act)
        {
            var j = new UseJob { target = target, rel = target ? target.transform : null, anim = a, act = act, startFrame = Time.frameCount };
            Durations(a.kind, out j.reach, out j.follow, out j.clip);
            if (a.seconds > 0f) j.reach = Mathf.Min(a.seconds, 1.2f);
            // eating and drinking take the clip's own time (eat 2.4 s, drink 2.2 s: two bites / gulps; its bite / sip
            // events chew and gulp, PlayerCharacter.ClipEvent); the rest of the uses stay under 1.5 s
            if ((a.kind == UseKind.Eat || a.kind == UseKind.Drink) && HumanClips.Enabled && HumanClips.Get(j.clip) is HumanClip food)
                j.follow = Mathf.Max(0.1f, food.length - j.reach);
            else if (j.reach + j.follow > 1.5f) j.follow = Mathf.Max(0.1f, 1.5f - j.reach);
            return j;
        }

        bool StartUse(Component target, UseAnim a, System.Action act, string prompt, IInteractable it, bool secondary)
        {
            if (!UseAnimatedNow(a.kind)) return false;
            if (use != null) EndUse(use, use.applied);                                                // a gesture still playing
            var j = NewJob(target, a, act);
            j.it = it; j.secondary = secondary;
            j.npc = target is MadMax.Npc.Npc;
            var point = a.hand ?? HandPoint(target);
            j.pointWorld = point;
            if (j.rel) j.pointLocal = j.rel.InverseTransformPoint(point);
            var feet = Player.transform.position;
            j.dist = Flat(feet, point);
            j.walking = !Player.Interior && j.dist > 1.2f && a.kind != UseKind.Talk;
            j.stuckAt = feet;
            j.pageBefore = Menus ? Menus.Current : MenuSystem.Page.None;
            use = j;
            LastUseKind = a.kind; LastUseOutcome = WorkOutcome.None; LastUseDelay = -1f;
            if (Refuelling) StopRefuel(null);
            if (!j.walking) BeginPose(j);
            else Player.AutoWalk = Vector3.zero;
            UseAnim.FireBegin(target, a.kind, j.reach + (j.walking ? Mathf.Max(0f, j.dist - 0.8f) / Player.walkSpeed : 0f));
            return true;
        }

        /// <summary>The point of the target the hands go to: the nearest point of its collider to the chest, the middle of
        /// its renderers, a person's chest.</summary>
        Vector3 HandPoint(Component target)
        {
            var chest = Player.transform.position + Vector3.up * 1.1f;
            if (!target) return Player.transform.position + Player.transform.forward * 0.6f + Vector3.up * 0.9f;
            if (target is MadMax.Npc.Npc || target is MadMax.Animals.Animal) return target.transform.position + Vector3.up * (target is MadMax.Npc.Npc ? 1.4f : 0.7f);
            var col = target.GetComponent<Collider>() ?? target.GetComponentInChildren<Collider>();
            if (col && col.enabled && col.gameObject.activeInHierarchy && !(col is CharacterController) && (!(col is MeshCollider mc) || mc.convex)) return col.ClosestPoint(chest);
            if (col && col.enabled) return col.bounds.ClosestPoint(chest);
            var r = target.GetComponentInChildren<Renderer>();
            return r ? r.bounds.ClosestPoint(chest) : target.transform.position;
        }

        static void Durations(UseKind k, out float reach, out float follow, out string clip)
        {
            clip = null;
            switch (k)
            {
                case UseKind.FlipSwitch: case UseKind.Press: reach = 0.35f; follow = 0.3f; break;
                case UseKind.LiftLid: reach = 0.45f; follow = 0.35f; break;
                case UseKind.PullCord: reach = 0.55f; follow = 0.45f; clip = "pull_cord"; break;
                case UseKind.Pump: reach = 1.05f; follow = 0.27f; clip = "pump"; break;
                case UseKind.Crank: reach = 0.8f; follow = 0.3f; break;
                case UseKind.TurnValve: reach = 0.5f; follow = 0.3f; break;
                case UseKind.Kneel: reach = 0.55f; follow = 0.55f; clip = "kneel_use"; break;
                case UseKind.Search: reach = 0.65f; follow = 0.65f; clip = "kneel_use"; break;
                case UseKind.Hitch: reach = 0.6f; follow = 0.6f; clip = "kneel_use"; break;
                case UseKind.PickUp: reach = 0.4f; follow = 0.35f; break;
                case UseKind.PickHigh: reach = 0.5f; follow = 0.35f; break;
                case UseKind.Pour: reach = 0.7f; follow = 0.4f; break;
                case UseKind.Stoke: reach = 0.7f; follow = 0.45f; break;
                case UseKind.Push: reach = 0.4f; follow = 0.6f; break;
                case UseKind.Drag: reach = 0.5f; follow = 0.3f; break;
                case UseKind.Milk: case UseKind.Shear: reach = 1.0f; follow = 0.4f; break;
                case UseKind.Talk: reach = 0.25f; follow = 0.2f; break;
                case UseKind.Eat: reach = 0.3f; follow = 0.7f; clip = "eat"; break;
                case UseKind.Drink: reach = 0.3f; follow = 0.7f; clip = "drink"; break;
                case UseKind.Wave: reach = 0.35f; follow = 1.05f; clip = "wave"; break;
                case UseKind.Wrench: reach = 0.7f; follow = 0.4f; clip = "wrench"; break;
                default: reach = 0.4f; follow = 0.35f; break;                                            // Reach and the rest
            }
        }

        void BeginPose(UseJob j)
        {
            j.walking = false;
            j.t = 0f;
            if (job == null && !j.gesture) Player.AutoWalk = Vector3.zero;                              // hold still while the hands work
            var feet = Player.transform.position;
            var p = UsePoint(j);
            j.height = Vector3.Dot(p - feet, Player.Interior ? Player.Interior.transform.up : Vector3.up);
            j.dist = Flat(feet, p);
            if (j.anim.kind == UseKind.Talk) { Player.AutoWalk = Vector3.zero; return; }
            TriggerPieceMotion(j);
            var mesh = UsePropMesh(j.anim.prop);
            if (mesh) { Player.HoldProp(mesh, PropMaterialFor(j.anim.prop)); j.prop = true; }
            MadMax.Net.NetSession.Instance?.SendWorkPose(true, (byte)NetPoseFor(j), mesh && WorkPropMesh(j.anim.prop) ? j.anim.prop : null);   // peers see a working pose
        }

        /// <summary>Pieces that start their own motion in Use() (or from the loot window): not triggered twice.</summary>
        static readonly HashSet<string> selfMoving = new HashSet<string> { "Container", "Beehive", "HandPump", "WaterOutlet", "AirCompressor", "TransferPump", "Latrine" };

        /// <summary>The piece's own parts move with the hands (Building/PieceMotion): lids lift, handles stroke, wheels and
        /// valves turn, the bell swings, over the whole use.</summary>
        void TriggerPieceMotion(UseJob j)
        {
            if (!j.target || j.gesture || j.it == null || selfMoving.Contains(j.it.GetType().Name)) return;
            string kind;
            switch (j.anim.kind)
            {
                case UseKind.PullCord: case UseKind.Crank: kind = "crank"; break;
                case UseKind.TurnValve: kind = "turn"; break;
                case UseKind.Pump: kind = "pump"; break;
                case UseKind.LiftLid: kind = "lid"; break;
                case UseKind.PickHigh: kind = j.it.GetType().Name == "AlarmBell" ? "ring" : null; break;
                default: kind = null; break;
            }
            if (kind != null) MadMax.Building.PieceMotion.Use(j.target.gameObject, kind, j.reach + j.follow + 0.3f);
        }

        Mesh UsePropMesh(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (key == "oil" || key == "can") return WorkPropMesh(key);
            if (ItemCatalog.Category(key) == ItemCategory.Tool || ItemCatalog.Category(key) == ItemCategory.Weapon) return ToolLibrary.MeshFor(key);
            return ItemModels.Get(key);
        }

        Material PropMaterialFor(string key) => MadMax.Rendering.HDBits.On && (key == "oil" || key == "can") ? MadMax.Rendering.HDShapes.Solid : propMaterial;

        WorkPose NetPoseFor(UseJob j)
        {
            switch (j.anim.kind)
            {
                case UseKind.Kneel: case UseKind.Search: case UseKind.Hitch: case UseKind.Milk: case UseKind.Stoke: case UseKind.PickUp: case UseKind.Drag: return WorkPose.Kneel;
                case UseKind.Pump: case UseKind.Crank: return WorkPose.Pump;
                case UseKind.Pour: return WorkPose.Pour;
                case UseKind.PickHigh: return WorkPose.Reach;
                case UseKind.Wrench: return WorkPose.LeanIn;
            }
            return j.height > 1.6f ? WorkPose.Reach : j.height < 0.55f ? WorkPose.Kneel : WorkPose.Stand;
        }

        Vector3 UsePoint(UseJob j) => j.rel ? j.rel.TransformPoint(j.pointLocal) : j.pointWorld;

        // ------------------------------------------------------------------ the tick (from AnimUpdate)

        void UseAnimUpdate()
        {
            var j = use;
            if (j == null) return;
            float dt = Time.deltaTime;
            j.age += dt;
            string why = UseBroken(j);
            if (why != null) { CancelUse(j, why); return; }
            if (j.walking) { UseWalk(j, dt); return; }
            j.t += dt;
            if (!j.applied)
            {
                UseAnim.ActiveT = Mathf.Clamp01(j.t / Mathf.Max(0.01f, j.reach));
                if (j.t >= j.reach) ApplyUse(j);
                if (use != j) return;
            }
            // a page Use() opened (loot, crafting, talk): the hands stay on it until it closes
            if (j.held)
            {
                if (Menus && Menus.IsOpen && Menus.Current != MenuSystem.Page.Context) { j.t = j.reach; PoseUse(j, dt); return; }
                j.held = false; j.t = j.reach;
                if (j.npc) { Player.Talking = false; if (Player.Animator != null) Player.Animator.LookAt = null; }
            }
            if (j.t >= j.reach + j.follow) { EndUse(j, true); return; }
            PoseUse(j, dt);
        }

        string UseBroken(UseJob j)
        {
            if (!Player || Player.Ragdolled || (Vitals && Vitals.Dead)) return "~DOWN";
            if (Current || Player.SeatedIn) return "~DRIVING";
            if (Player.Sitting && !j.applied) return "~SEATED";
            if (j.target == null && !j.applied && !ReferenceEquals(j.target, null)) return "~GONE";       // destroyed before the hands got there
            if (job != null) return "~WORK";
            bool moving = Player.moveInput.sqrMagnitude > 0.1f;
            if (moving && !j.gesture && (j.age > 0.15f || j.walking) && !j.held) return j.applied ? "~MOVED" : "STOPPED";   // gestures (eating) go on while walking
            return null;
        }

        void UseWalk(UseJob j, float dt)
        {
            var feet = Player.transform.position;
            var p = UsePoint(j);
            float d = Flat(feet, p);
            if (Flat(feet, j.stuckAt) > 0.08f) { j.stuckAt = feet; j.stuckT = 0f; } else j.stuckT += dt;
            j.walkT += dt;
            if (d <= 0.85f || j.stuckT > 0.5f || j.walkT > 1.5f) { Player.AutoWalk = Vector3.zero; BeginPose(j); return; }   // close enough, or blocked: from here
            var dir = p - feet; dir.y = 0f;
            Player.AutoWalk = dir.normalized * Mathf.Clamp(d / 0.8f, 0.4f, 1f);
        }

        void ApplyUse(UseJob j)
        {
            if (j.applied) return;
            j.applied = true;
            j.t = Mathf.Max(j.t, j.reach);
            LastUseDelay = j.age;
            var before = Menus ? Menus.Current : MenuSystem.Page.None;
            if (j.npc && j.target)
            {
                Player.Talking = true;
                if (Player.Animator != null) Player.Animator.LookAt = j.target.transform.position + Vector3.up * 1.6f;
            }
            try { j.act?.Invoke(); }
            catch (System.Exception e) { Debug.LogException(e); }
            if (use != j) { UseAnim.FireApply(j.target, j.anim.kind); return; }                       // the use handed over (timed vehicle work, a seat)
            UseAnim.FireApply(j.target, j.anim.kind);
            bool opened = Menus && Menus.IsOpen && Menus.Current != before && !Menus.Pauses && Menus.Current != MenuSystem.Page.Context;
            j.held = opened || (j.anim.hold && Menus && Menus.IsOpen);
            if (!j.held && j.npc) { Player.Talking = false; if (Player.Animator != null) Player.Animator.LookAt = null; }
            if (Player.Sitting || Player.BodyBusy) EndUse(j, true);                                    // Use() sat the player down: the seat takes over
        }

        void CancelUse(UseJob j, string why)
        {
            bool quiet = why == null || why.StartsWith("~");
            if (!j.applied) { LastUseOutcome = WorkOutcome.Cancelled; LastWorkNote = quiet ? why?.TrimStart('~') : why; }
            EndUse(j, j.applied);
        }

        void EndUse(UseJob j, bool done)
        {
            if (use == j) use = null;
            if (done && LastUseOutcome == WorkOutcome.None) LastUseOutcome = j.applied ? WorkOutcome.Done : WorkOutcome.Cancelled;
            if (Player)
            {
                Player.ActionT = -1f;                                                                 // a job's work clips loop on their own clock
                if (job == null)
                {
                    Player.PoseOverride = null; Player.ActionClip = null;
                    if (Player.AutoWalk.HasValue && walkOption == null) Player.AutoWalk = null;
                }
                if (j.prop) Player.DropProp();
                if (j.npc || Player.Talking) { Player.Talking = false; if (Player.Animator != null) Player.Animator.LookAt = null; }
                if (!j.walking && j.anim.kind != UseKind.Talk && job == null) MadMax.Net.NetSession.Instance?.SendWorkPose(false, 0, null);
            }
            UseAnim.FireEnd(j.target, j.anim.kind);
        }

        // ------------------------------------------------------------------ the pose

        void PoseUse(UseJob j, float dt)
        {
            var feet = Player.transform.position;
            var p = UsePoint(j);
            if (!Player.Interior)
            {
                var look = p - feet; look.y = 0f;
                if (look.sqrMagnitude > 0.01f && !j.gesture) Player.transform.rotation = Quaternion.Slerp(Player.transform.rotation, Quaternion.LookRotation(look), 1f - Mathf.Exp(-12f * dt));
            }
            if (j.anim.kind == UseKind.Talk) return;                                                    // facing them is all; the talk gestures run on the page
            float total = j.reach + j.follow;
            float u = Mathf.Clamp01(j.t / Mathf.Max(0.01f, j.reach));
            float f = j.held ? 0f : Mathf.Clamp01((j.t - j.reach) / Mathf.Max(0.01f, j.follow));
            Player.PoseOverride = UsePose(j.anim.kind, u, f, j.age, j.height, j.dist, Player.Rig ? Player.Rig.appearance.height : 1f);
            bool clip = j.clip != null && HumanClips.Enabled && HumanClips.Get(j.clip) != null;
            Player.ActionClip = clip ? j.clip : null;
            Player.ActionT = clip && !HumanClips.Get(j.clip).loop ? (j.held ? j.reach / total : Mathf.Clamp01(j.t / total)) : -1f;
            // pushing the ore cart: lean in and walk with it a few steps
            if (j.anim.kind == UseKind.Push && j.applied && !j.held) Player.AutoWalk = Player.transform.forward * 0.45f * (1f - f);
            else if (j.anim.kind != UseKind.Push && !j.gesture && job == null) Player.AutoWalk = Vector3.zero;
        }

        static readonly ToolPose RestPose = new ToolPose { armRX = -2f, armRZ = 9f, foreR = -14f, armLX = -2f, armLZ = -9f, foreL = -12f };

        /// <summary>The arm, chest and knee angles that put a hand at a point <paramref name="h"/> m up and
        /// <paramref name="dist"/> m out: kneel for the ground, bend for knee height, point the arm at it standing.</summary>
        static void ArmFor(float h, float dist, float body, out float arm, out float chest, out float knees)
        {
            if (h < 0.5f) { arm = -55f; chest = 38f; knees = 95f; return; }
            if (h < 0.95f) { arm = Mathf.Lerp(-55f, -68f, (h - 0.5f) / 0.45f); chest = Mathf.Lerp(34f, 20f, (h - 0.5f) / 0.45f); knees = Mathf.Lerp(30f, 8f, (h - 0.5f) / 0.45f); return; }
            float dy = h - 1.42f * body, dz = Mathf.Clamp(dist - 0.15f, 0.25f, 0.7f);
            arm = -Mathf.Clamp(90f + Mathf.Atan2(dy, dz) * Mathf.Rad2Deg, 30f, 165f);
            chest = h > 1.6f ? -5f : 6f; knees = 0f;
        }

        /// <summary>The use pose: from rest into the contact pose over the reach (u), out of it again over the follow (f).</summary>
        static ToolPose UsePose(UseKind k, float u, float f, float t, float h, float dist, float body)
        {
            ArmFor(h, dist, body, out float arm, out float chest, out float knees);
            float s = Mathf.Sin(t * Mathf.PI * 2f * 1.5f);
            ToolPose c, after;
            switch (k)
            {
                case UseKind.FlipSwitch:
                    c = new ToolPose { chestX = chest, knees = knees, armRX = arm, armRZ = 4f, foreR = Mathf.Lerp(-30f, -12f, u), handRZ = 0f, armLX = -2f, armLZ = -9f, foreL = -12f };
                    after = c; after.handRZ = 32f; after.foreR = -18f;                               // the flick
                    break;
                case UseKind.Press:
                    c = new ToolPose { chestX = chest, knees = knees, armRX = arm, armRZ = 2f, foreR = u < 0.75f ? -38f : Mathf.Lerp(-38f, -6f, (u - 0.75f) / 0.25f), armLX = -2f, armLZ = -9f, foreL = -12f };
                    after = c; after.foreR = -24f;
                    break;
                case UseKind.PickHigh:
                    c = new ToolPose { chestX = Mathf.Min(chest, 0f), knees = knees, armRX = Mathf.Min(arm, -95f), armLX = Mathf.Min(arm, -95f) + 6f, armLZ = 10f, armRZ = -4f, foreR = -24f, foreL = -28f, handRZ = Mathf.Sin(t * 14f) * 26f };
                    after = c; after.armRX += 40f; after.foreR = -80f;                                // bring it down to the chest
                    break;
                case UseKind.LiftLid:
                    c = new ToolPose { chestX = chest + 6f, knees = knees, armRX = arm, armRZ = 2f, foreR = -18f, armLX = arm + 2f, armLZ = 8f, foreL = -18f };
                    after = c; after.armRX -= 28f; after.armLX -= 28f; after.chestX -= 10f;         // the lid comes up
                    if (u >= 1f) c = after;
                    break;
                case UseKind.PullCord:
                    c = new ToolPose { chestX = 40f, knees = 70f, armRX = -35f, foreR = -15f, armLX = -60f, armLZ = 12f, foreL = -40f };
                    after = new ToolPose { chestX = 15f, chestY = -25f, knees = 30f, armRX = -40f, armRY = -20f, armRZ = 30f, foreR = -110f, armLX = -62f, armLZ = 16f, foreL = -36f };
                    break;
                case UseKind.Pump:
                    c = new ToolPose { chestX = 22f + 8f * s, knees = 20f + 10f * s, armRX = -62f + 22f * s, armRZ = -4f, foreR = -30f, armLX = -62f + 22f * s, armLZ = 12f, foreL = -30f };
                    after = c;
                    break;
                case UseKind.Crank:
                {
                    float w = t * Mathf.PI * 2f * 1.4f;
                    c = new ToolPose { chestX = Mathf.Max(chest, 10f), knees = knees, armRX = arm + 15f * Mathf.Sin(w), armRY = 15f * Mathf.Cos(w), foreR = -45f + 10f * Mathf.Cos(w), armLX = -20f, armLZ = 6f, foreL = -30f };
                    after = c;
                    break;
                }
                case UseKind.TurnValve:
                    c = new ToolPose { chestX = chest + 4f, knees = knees, armRX = arm + 6f, armRZ = -6f, foreR = -50f, armLX = arm + 6f, armLZ = 14f, foreL = -50f, handRZ = Mathf.Sin(t * 5f) * 50f };
                    after = c;
                    break;
                case UseKind.Kneel:
                case UseKind.Hitch:
                case UseKind.Search:
                {
                    float r = Mathf.Sin(t * (k == UseKind.Search ? 8f : 10f));
                    c = new ToolPose { chestX = 42f + 3f * Mathf.Sin(t * 2.3f), armRX = 10f + r * (k == UseKind.Search ? 14f : 4f), foreR = -35f, handRZ = r * 30f, armLX = 6f - (k == UseKind.Search ? r * 14f : 0f), armLZ = 10f, foreL = -38f, knees = 108f };
                    after = c;
                    break;
                }
                case UseKind.PickUp:
                    c = new ToolPose { chestX = 55f, knees = 70f, armRX = -40f, foreR = -10f, armLX = -10f, armLZ = -6f, foreL = -20f };
                    after = new ToolPose { chestX = 10f, knees = 10f, armRX = -20f, foreR = -100f, armLX = -2f, armLZ = -9f, foreL = -12f };
                    break;
                case UseKind.Pour:
                    c = new ToolPose { chestX = Mathf.Max(chest, 22f), knees = h < 0.6f ? 45f : 10f, armRX = arm - 8f, foreR = -40f, handRZ = 70f * u, armLX = -50f, armLZ = 24f, foreL = -60f };
                    after = c;
                    break;
                case UseKind.Stoke:
                    c = new ToolPose { chestX = 34f, knees = 85f, armRX = -50f + 14f * Mathf.Sin(t * 8f), foreR = -20f, armLX = -20f, armLZ = 10f, foreL = -60f };
                    after = c;
                    break;
                case UseKind.Push:
                    c = new ToolPose { chestX = 24f, knees = 18f, armRX = -78f, armRZ = -4f, foreR = -18f, armLX = -78f, armLZ = 8f, foreL = -18f };
                    after = c;
                    break;
                case UseKind.Drag:
                    c = new ToolPose { chestX = 45f, knees = 80f, armRX = -40f, foreR = -10f, armLX = -40f, armLZ = 8f, foreL = -10f };
                    after = new ToolPose { chestX = -14f, knees = 30f, armRX = -55f, foreR = -10f, armLX = -55f, armLZ = 8f, foreL = -10f };
                    break;
                case UseKind.Milk:
                {
                    float m = Mathf.Sin(t * 10f);
                    c = new ToolPose { chestX = 34f, knees = 105f, armRX = -22f + 12f * m, foreR = -60f, armLX = -22f - 12f * m, armLZ = 10f, foreL = -60f };
                    after = c;
                    break;
                }
                case UseKind.Shear:
                    c = new ToolPose { chestX = 40f, knees = 55f, armRX = -42f, armRY = 22f * Mathf.Sin(t * 5f), foreR = -30f, armLX = -35f, armLZ = 12f, foreL = -50f };
                    after = c;
                    break;
                case UseKind.Eat:
                    c = new ToolPose { chestX = 4f, armRX = -38f, armRZ = -18f, foreR = -124f, handRZ = -16f, armLX = -2f, armLZ = -9f, foreL = -12f };
                    after = c;
                    break;
                case UseKind.Drink:
                    c = new ToolPose { chestX = -6f, armRX = -64f, armRZ = -12f, foreR = -112f, handRZ = -40f, armLX = -2f, armLZ = -9f, foreL = -12f };
                    after = c;
                    break;
                case UseKind.Wave:
                    c = new ToolPose { chestX = -2f, armRX = -150f, armRZ = 30f, foreR = -20f + 25f * Mathf.Sin(t * 12f), armLX = -2f, armLZ = -9f, foreL = -12f };
                    after = c;
                    break;
                case UseKind.Wrench:
                {
                    float r = Mathf.Sin(t * 10f);
                    c = new ToolPose { chestX = Mathf.Max(chest, 16f), knees = knees, armRX = arm + r * 5f, foreR = -30f, handRZ = r * 45f, armLX = arm + 4f, armLZ = 10f, foreL = -36f };
                    after = c;
                    break;
                }
                default:                                                                              // Reach: a hand on it
                    c = new ToolPose { chestX = chest, knees = knees, armRX = arm, armRZ = 4f, foreR = -16f, armLX = -2f, armLZ = -9f, foreL = -12f };
                    after = c; after.foreR = -30f;
                    break;
            }
            if (f <= 0f) return ToolPose.Lerp(RestPose, c, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / 0.85f)));
            // follow-through: the contact's after-pose, then back to rest
            var mid = ToolPose.Lerp(c, after, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(f / 0.35f)));
            return ToolPose.Lerp(mid, RestPose, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((f - 0.35f) / 0.65f)));
        }
    }
}
