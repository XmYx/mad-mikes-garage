using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>A vehicle fire running its course (added by <see cref="VehicleDamage"/>). While
    /// <see cref="VehicleSystems"/> reports the engine fire, <see cref="burn"/> climbs over 90..180 s (heavier vehicles
    /// slower, a full tank faster); paint, upholstery and tyres keep the flames fed unless rain or water gets at them.
    /// When the fire reaches the tank (burn 0.5) with more than 5 L in it, there is one roll at
    /// <see cref="ExplodeChance"/>: the tank goes up a moment later (a blast scaled by the litres, doors and hood blown
    /// off). At burn 1 the vehicle is <see cref="charred"/>: black and ash-grey, glass and lamps gone, tyres burned to
    /// the rims, engine seized, every part ruined, fluids gone, undriveable — salvage only — and it smoulders for ten
    /// minutes. A fire put out at burn 0.3+ leaves the body scorched. Saved in <c>VehicleSave.burn</c>; the look reaches
    /// other players with the vehicle looks.</summary>
    public class VehicleBurn : MonoBehaviour
    {
        public const float ExplodeChance = 0.35f, TankLitres = 5f;

        /// <summary>0 untouched .. 1 burned out.</summary>
        public float burn;
        public bool charred, exploded;
        bool rolled;
        float explodeAt = -1f, smoulder, smokeAcc, hurtT;
        VehicleDriver driver;
        VehicleGrime grime;
        Rigidbody body;

        void Awake()
        {
            driver = GetComponent<VehicleDriver>();
            grime = GetComponent<VehicleGrime>();
            body = GetComponent<Rigidbody>();
        }

        /// <summary>Seconds a fire takes to burn the vehicle out at a steady blaze.</summary>
        public float Seconds => Mathf.Lerp(90f, 180f, Mathf.InverseLerp(800f, 8000f, body ? body.mass : 1500f));

        /// <summary>Scorch shown on the body (0..1): from burn 0.3 on, all the way when charred.</summary>
        public float Scorch => charred ? 1f : Mathf.Clamp01((burn - 0.3f) / 0.7f) * 0.8f;

        /// <summary>One physics step of the engine fire (<see cref="VehicleSystems"/>, on the simulating side).</summary>
        public void Feed(float dt, ref float fuel, Fire fire)
        {
            if (charred) return;
            float rate = (1f + Mathf.Min(fuel, 60f) / 60f) * (fire ? Mathf.Max(0.35f, fire.intensity) : 0.5f);
            burn = Mathf.Min(1f, burn + dt * rate / Seconds);
            bool wet = (Weather.Raining && !Weather.Snowing) || (driver && driver.InWater > 0.3f);
            if (fire && !wet && burn < 1f) fire.fuel = Mathf.Max(fire.fuel, 6f);              // paint, seats, tyres: it keeps burning
            if (!rolled && burn >= 0.5f)
            {
                rolled = true;
                if (fuel > TankLitres && Random.value < ExplodeChance) explodeAt = burn + Random.Range(0.01f, 0.08f);
            }
            if (!exploded && explodeAt > 0f && burn >= explodeAt) Explode(ref fuel, fire);
            HurtOccupant(dt);
            if (burn >= 1f) Char(true);
            ShowScorch();
        }

        /// <summary>Forces the tank roll (tests): true = it goes up when the fire reaches it.</summary>
        public void RigTank(bool explode) { rolled = true; explodeAt = explode ? 0.5f : -1f; }

        /// <summary>The player sitting in a burning vehicle gets burned (once per second, worse as it spreads).</summary>
        void HurtOccupant(float dt)
        {
            var game = MadMax.Game.WastelandGame.Instance;
            if (!game || game.Current != driver || (hurtT -= dt) > 0f) return;
            hurtT = 1f;
            game.Injure(2f + burn * 10f, "BURNED");
            if (burn > 0.25f) game.Toast("THE CABIN IS ON FIRE: GET OUT! [" + MadMax.Game.Controls.Name(MadMax.Game.Controls.Act.Enter) + "]");
        }

        void Explode(ref float fuel, Fire fire)
        {
            exploded = true;
            var b = Bounds();
            var at = b.center - transform.forward * b.extents.z * 0.4f + Vector3.down * b.extents.y * 0.3f;   // the tank sits low at the back
            float radius = Mathf.Clamp(3f + fuel * 0.08f, 3f, 8f), power = Mathf.Clamp(2f + fuel * 0.12f, 2f, 9f);
            var net = MadMax.Net.NetSession.Instance;
            Explosion.Blast(at, radius, power, 0.3f, gameObject, !(net && net.IsClient));
            fuel = 0f;
            if (fire) { fire.intensity = 1f; fire.fuel = Mathf.Max(fire.fuel, 20f); }
            burn = Mathf.Max(burn, 0.8f);                                                         // the blast spreads it through
            // doors and the hood blown off their hinges
            foreach (var p in GetComponentsInChildren<VehiclePart>())
            {
                if (!p.Socket || (p.category != PartCategory.Door && p.category != PartCategory.Hood)) continue;
                p.Socket.Detach(true);
                if (p.TryGetComponent<Rigidbody>(out var prb)) prb.linearVelocity = (p.transform.position - at).normalized * Random.Range(5f, 9f) + Vector3.up * Random.Range(3f, 6f);
            }
            if (body && !body.isKinematic) body.AddForceAtPosition(Vector3.up * body.mass * Random.Range(2.5f, 4f), at, ForceMode.Impulse);
            if (TryGetComponent<VehicleBreakables>(out var br)) br.Smash(b.center, b.extents.magnitude + 1f, 10f);
        }

        /// <summary>Burned out: the wreck it leaves (<paramref name="live"/> false on load / replication: no effects).</summary>
        public void Char(bool live)
        {
            if (charred) return;
            charred = true; burn = 1f;
            var sys = GetComponent<VehicleSystems>();
            if (sys) { sys.fuel = 0f; sys.oil = 0f; sys.coolant = 0f; sys.Stop(); }
            foreach (var p in GetComponentsInChildren<VehiclePart>())
            {
                if (!p.Socket) continue;
                p.damage = Mathf.Max(p.damage, p.category == PartCategory.Engine ? 1f : 0.95f);
                if (p.TryGetComponent<WheelStats>(out var w)) w.wear = 1.5f;                     // the rubber burned away: rims
            }
            if (TryGetComponent<VehicleBreakables>(out var br)) { var b = Bounds(); br.Smash(b.center, b.extents.magnitude + 1f, 10f); }
            var occupant = transform.Find("Body/Driver");
            if (occupant) occupant.gameObject.SetActive(false);
            if (driver) driver.driveable = false;
            smoulder = live ? 600f : 0f;
            ShowScorch();
            var game = MadMax.Game.WastelandGame.Instance;
            if (!live || !game) return;
            if (game.Current == driver) game.Exit();
            game.NoteBurnedOut(driver);
            if (System.Linq.Enumerable.Contains(game.Fleet, driver))
            {
                string n = MadMax.Game.WastelandGame.Name(driver);
                game.Toast(n + " BURNED OUT: ONLY SALVAGE LEFT");
                MadMax.Game.Journal.Add("FLEET", n + " BURNED OUT.");
            }
        }

        void ShowScorch() { if (grime) grime.scorch = Scorch; }

        void Update()
        {
            if (smoulder <= 0f) return;
            smoulder -= Time.deltaTime;
            smokeAcc += Time.deltaTime * Mathf.Lerp(0.6f, 4f, smoulder / 600f);
            if (smokeAcc < 1f) return;
            smokeAcc = 0f;
            var b = Bounds();
            var p = b.center + new Vector3(Random.Range(-b.extents.x, b.extents.x) * 0.6f, b.extents.y * 0.5f, Random.Range(-b.extents.z, b.extents.z) * 0.6f);
            float g = Random.Range(0.18f, 0.32f);
            Fx.Smoke(p, Vector3.up * 1.2f + Fx.Wind * 0.6f, Random.Range(0.6f, 1.3f), new Color(g, g, g, 0.5f), 5f);
        }

        Bounds Bounds()
        {
            var bt = transform.Find("Body");
            var r = bt ? bt.GetComponent<Renderer>() : null;
            return r ? r.bounds : new Bounds(transform.position + Vector3.up, new Vector3(2f, 1.5f, 4.5f));
        }

        // ------------------------------------------------------------------ save / replication
        static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;

        public string SaveState() => burn <= 0.001f ? null : burn.ToString("0.###", Inv) + (charred ? ",c" : "") + (exploded ? ",x" : "") + (rolled ? ",r" : "");

        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var f = s.Split(',');
            float.TryParse(f[0], System.Globalization.NumberStyles.Float, Inv, out burn);
            bool c = false;
            for (int i = 1; i < f.Length; i++) { if (f[i] == "x") exploded = true; else if (f[i] == "r") rolled = true; else if (f[i] == "c") c = true; }
            if (c) Char(false);
            ShowScorch();
        }

        /// <summary>The burn as part of the vehicle looks online (empty when untouched).</summary>
        public static string LooksOf(VehicleDriver v) => v && v.TryGetComponent<VehicleBurn>(out var b) ? b.SaveState() ?? "" : "";

        public static void ApplyLooks(VehicleDriver v, string s)
        {
            if (!v || string.IsNullOrEmpty(s)) return;
            var b = v.GetComponent<VehicleBurn>();
            if (!b) b = v.gameObject.AddComponent<VehicleBurn>();
            b.LoadState(s);
        }
    }
}
