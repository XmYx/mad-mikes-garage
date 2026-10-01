using MadMax.Items;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>A light that needs power (and a switch). Lights itself only at night when set to auto. Floodlights
    /// (<see cref="canSense"/>) have a motion SENSOR mode ([T]): dark until something moves within
    /// <see cref="SenseRange"/> m, then on for half a minute; raiders keep out of lit floodlights when they can.
    /// A wall <see cref="LightSwitch"/> can claim it (<see cref="Master"/>): then the switch decides, and an indoor light
    /// burns whenever it is switched on (outdoor ones keep their dusk-to-dawn sensor). A "Bulb" child glows (unlit
    /// material) whenever the lamp is lit, so far lamps outside the <see cref="MadMax.World.LightBudget"/> still read
    /// as lit. Lamps with a "Head" (<see cref="LampHead"/>) can be smashed: dark until re-glazed with one glass.</summary>
    public class PoweredLight : MonoBehaviour, IPlaceState, IInteractable
    {
        public const float SenseRange = 18f;
        public static readonly System.Collections.Generic.List<PoweredLight> All = new System.Collections.Generic.List<PoweredLight>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            if (broken) return "LAMP SMASHED  [E] RE-GLAZE (1 GLASS)";
            var m = Master;
            if (m) return (m.on ? "WALL SWITCH: ON  [E] SWITCH OFF" : "WALL SWITCH: OFF  [E] SWITCH ON") + (HasPower ? "" : "  NO POWER");
            return (on ? "[E] SWITCH OFF" : "[E] SWITCH ON") + (canSense ? (sensor ? "  [T] SENSOR: ON" : "  [T] SENSOR: OFF") : "") + (!HasPower ? "  NO POWER" : "");
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (!secondary && broken)
            {
                if (g.Inventory.TrySpend(ResourceType.Glass, 1)) { Repair(); g.Toast("LAMP RE-GLAZED"); }
                else g.Toast("NEED 1 GLASS");
                return;
            }
            if (!secondary && Master) { Master.Flip(); return; }
            if (!secondary) Toggle();
            else if (canSense) { sensor = !sensor; GetComponent<Placeable>()?.Dirty(); g.Toast(sensor ? "FLOODLIGHT: MOTION SENSOR" : "FLOODLIGHT: ALWAYS ON AT NIGHT"); }
        }

        public float watts = 60f;
        public bool on = true;
        public bool needsPower = true;
        public bool canSense, sensor;
        /// <summary>Smashed glass: dark until re-glazed ([E] with 1 glass).</summary>
        public bool broken;
        /// <summary>Outdoors (street lamps, masts): keeps its dusk-to-dawn sensor under a switch and is never claimed by a
        /// room or vehicle switch, only by one cabled to it.</summary>
        public bool outdoor;
        /// <summary>Fed from outside the utility network (a town grid, a building's old wiring): the owner sets
        /// <see cref="externalPower"/>.</summary>
        [System.NonSerialized] public bool external;
        [System.NonSerialized] public bool externalPower = true;
        /// <summary>Called when the lamp is smashed (world lamps remember it).</summary>
        public System.Action<PoweredLight> smashed;

        Light lamp;
        UtilityNode node;
        Renderer bulb;
        Material bulbBase;
        bool bulbLit, bulbShown = true;
        float scan, trippedUntil;
        LightSwitch master;
        float claimUntil;
        int claimRank;

        void Awake()
        {
            lamp = GetComponentInChildren<Light>(); node = GetComponent<UtilityNode>();
            var b = transform.Find("Bulb");
            if (b) SetBulb(b.GetComponent<Renderer>());
        }

        /// <summary>Lit right now (a raider in its beam is seen).</summary>
        public bool Lit => lamp && lamp.enabled;
        /// <summary>Burning (switched on, dark enough, fed, not smashed), whether or not the light budget gives it a
        /// point light this frame.</summary>
        public bool Glowing { get; private set; }
        /// <summary>Its supply is live (network node powered, or the external feed on; oil lamps always).</summary>
        public bool HasPower => external ? externalPower : !needsPower || (node && node.Powered);
        /// <summary>Lights a wall switch may run: on a network or an external feed (oil lamps are lit by hand), not floodlights.</summary>
        public bool Switchable => (needsPower || external) && !canSense;
        /// <summary>The wall switch running this light (null: its own [E] switch).</summary>
        public LightSwitch Master => master && Time.time < claimUntil ? master : null;

        /// <summary>A switch takes this light for its circuit (renewed every second; a cabled switch outranks a room or
        /// vehicle one; among equals the first keeps it).</summary>
        public bool Claim(LightSwitch s, int rank)
        {
            if (master && master != s && Time.time < claimUntil && claimRank >= rank) return false;
            master = s; claimRank = rank; claimUntil = Time.time + 2.5f;
            return true;
        }

        public void SetBulb(Renderer r) { bulb = r; bulbBase = r ? r.sharedMaterial : null; bulbLit = false; }

        void Update()
        {
            bool dark = MadMax.World.DayNight.Darkness > 0.25f;
            var m = Master;
            bool switched = m ? m.on : on;
            bool needDark = !m || outdoor;
            if (sensor && dark && switched && (scan -= Time.deltaTime) <= 0f) { scan = 0.5f; if (Motion()) trippedUntil = Time.time + 30f; }
            bool want = switched && !broken && (dark || !needDark) && (!sensor || Time.time < trippedUntil);
            if (node) node.demand = want ? watts : 0f;
            bool lit = want && HasPower;
            Glowing = lit;
            if (lamp) lamp.enabled = lit && MadMax.World.LightBudget.Allowed(lamp);
            ShowBulb(lit);
        }

        void ShowBulb(bool lit)
        {
            if (!bulb) return;
            if (bulbShown == broken) { bulbShown = !broken; bulb.enabled = !broken; }      // smashed: the glass is gone
            if (lit == bulbLit) return;
            bulbLit = lit;
            if (!bulbBase) bulbBase = bulb.sharedMaterial;
            bulb.sharedMaterial = lit ? LitMaterial(bulbBase) : bulbBase;
        }

        static readonly System.Collections.Generic.Dictionary<Material, Material> litMats = new System.Collections.Generic.Dictionary<Material, Material>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetMats() => litMats.Clear();

        /// <summary>Full-bright copy of a piece material for glowing bulbs (cached per source).</summary>
        public static Material LitMaterial(Material src)
        {
            if (!src) return null;
            if (litMats.TryGetValue(src, out var m) && m) return m;
            m = new Material(src) { name = src.name + "_Lit" };
            m.SetFloat("_Unlit", 1f);
            m.SetFloat("_LampOn", 1f);                                                       // HD bulbs: the baked glow too
            m.SetColor("_Tint", new Color(1.5f, 1.5f, 1.5f, 1f));
            return litMats[src] = m;
        }

        /// <summary>Break the glass: dark, shards on the ground.</summary>
        public void Smash(Vector3 at)
        {
            if (broken) return;
            broken = true;
            MadMax.World.Shards.Drop(at, MadMax.World.Shards.Kind.Clear);
            MadMax.Audio.Sfx.Play("glass_break", at, 0.6f, Random.Range(1.1f, 1.4f), 35f, 0.1f);
            smashed?.Invoke(this);
            GetComponent<Placeable>()?.Dirty();
        }

        public void Repair() { broken = false; GetComponent<Placeable>()?.Dirty(); }

        /// <summary>Someone or something moving within range: people, animals, a vehicle rolling.</summary>
        bool Motion()
        {
            var at = transform.position;
            float r2 = SenseRange * SenseRange;
            foreach (var n in MadMax.Npc.Npc.All) if (n && n.Alive && (n.transform.position - at).sqrMagnitude < r2) return true;
            foreach (var a in MadMax.Animals.Animal.All) if (a && (a.transform.position - at).sqrMagnitude < r2) return true;
            var g = MadMax.Game.WastelandGame.Instance;
            if (!g) return false;
            if (g.Player && !g.Current && (g.Player.transform.position - at).sqrMagnitude < r2) return true;
            foreach (var v in g.AllVehicles) if (v && v.Body && v.Body.linearVelocity.sqrMagnitude > 0.5f && (v.transform.position - at).sqrMagnitude < r2) return true;
            return false;
        }

        /// <summary>A lit floodlight within range of <paramref name="p"/> (raiders pick other targets).</summary>
        public static bool FloodlitAt(Vector3 p)
        {
            foreach (var l in All) if (l && l.canSense && l.Lit && (l.transform.position - p).sqrMagnitude < SenseRange * SenseRange) return true;
            return false;
        }

        public void Toggle() { on = !on; GetComponent<Placeable>()?.Dirty(); }
        public string SaveState() => (on ? "1" : "0") + (sensor ? "s" : "") + (broken ? "x" : "");
        public void LoadState(string s) { on = !string.IsNullOrEmpty(s) && s[0] != '0'; sensor = s != null && s.IndexOf('s') > 0; broken = s != null && s.IndexOf('x') > 0; }
    }
}
