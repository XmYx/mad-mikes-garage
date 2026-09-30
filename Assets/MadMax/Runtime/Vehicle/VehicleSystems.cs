using System;
using System.Collections.Generic;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    [Flags]
    public enum Fault
    {
        None = 0, NoEngine = 1, NoFuel = 2, LowFuel = 4, LowOil = 8, NoOil = 16, Overheat = 32,
        CoolantLeak = 64, OilLeak = 128, FuelLeak = 256, Seized = 512, LowCoolant = 1024, NoRadiator = 2048, Flooded = 4096, OnFire = 8192, WrongFuel = 16384,
        ServiceDue = 32768, Clogged = 65536, Misfire = 131072, Labouring = 262144, EngineOff = 524288
    }

    /// <summary>Engine fluids and health. Fuel burns with load; oil keeps the engine alive; coolant carries heat away.
    /// Damage causes leaks; running low produces faults with consequences: sputter/stall (fuel), wear → seizure (oil),
    /// power loss → head damage (heat). Two-strokes mix oil into fuel; air-cooled engines have no coolant.</summary>
    [RequireComponent(typeof(VehicleDriver))]
    public class VehicleSystems : MonoBehaviour
    {
        public float fuelCapacity = 60f, oilCapacity = 5f, coolantCapacity = 8f;
        public float fuel = 40f, oil = 5f, coolant = 8f;
        /// <summary>Litres of fuel treated with additive still in the tank (burns 25 % leaner).</summary>
        public float additive;
        /// <summary>What is in the tank: petrol (Fuel) or Diesel; None while empty. Set by whatever goes in first.</summary>
        public ResourceType tankKind = ResourceType.None;

        /// <summary>The fuel the mounted engine burns: diesel engines take Diesel, the rest petrol (Fuel; Ethanol works too).</summary>
        public ResourceType FuelKind
        {
            get
            {
                var e = driver ? driver.Engine : null;
                var p = e ? e.GetComponent<VehiclePart>() : null;
                return p && p.partId.Contains("diesel") ? ResourceType.Diesel : ResourceType.Fuel;
            }
        }
        /// <summary>The tank holds the other kind of fuel: the engine won't run until it is siphoned out.</summary>
        public bool WrongFuel => fuel > 0.5f && tankKind != ResourceType.None && tankKind != FuelKind && driver && driver.Engine;
        /// <summary>Can this fuel go in (ethanol counts as petrol)? An empty tank takes anything.</summary>
        public bool Accepts(ResourceType t) => fuel < 0.5f || tankKind == ResourceType.None || tankKind == (t == ResourceType.Ethanol ? ResourceType.Fuel : t);
        /// <summary>Pour fuel in (litres), remembering its kind.</summary>
        public bool AddFuel(ResourceType t, float litres)
        {
            if (!Accepts(t)) return false;
            if (fuel < 0.5f || tankKind == ResourceType.None) tankKind = t == ResourceType.Ethanol ? ResourceType.Fuel : t;
            fuel = Mathf.Min(fuelCapacity, fuel + litres);
            if (litres > 0.5f) MadMax.Game.WastelandGame.StarterNote("fuel");
            return true;
        }
        public bool usesCoolant = true;
        [Tooltip("Two-stroke: oil is mixed into the fuel, no separate oil system.")]
        public bool oilInFuel;
        [Tooltip("Fuel burn multiplier (1 = realistic, 3 = game pace: a tank lasts ~1 h of driving).")]
        public float consumption = 3f;
        [System.NonSerialized] public float fuelMultiplier = 1f;   // game rules x driver's Survival skill (set each frame)

        /// <summary>Maintenance (roadmap 19), 1 fresh .. 0 worn out: the oil breaks down with running, the air filter
        /// clogs with dust (dry loose ground at speed, dust storms), spark plugs wear (petrol engines).</summary>
        public float oilLife = 1f, airFilter = 1f, plugs = 1f;
        public float hours;                  // engine running hours
        public bool UsesPlugs => FuelKind != ResourceType.Diesel;

        public float Temperature { get; private set; } = 25f;
        public float PowerFactor { get; private set; } = 1f;
        public Fault Faults { get; private set; }
        public bool HasEngine => driver && driver.Engine;

        // ---- engine start (user additions): off until cranked; the throttle cranks it; the chance to catch falls with
        // the engine's condition, cold, worn plugs, a clogged filter and low oil. AI drivers' engines just run.
        /// <summary>The engine is running (started and not stalled).</summary>
        public bool Started { get; private set; }
        public bool Cranking => Time.time < crankUntil;
        float crankUntil = -1f, nextCrank;
        bool crankCatches;
        /// <summary>Raised when a crank ends: true = it caught.</summary>
        public event System.Action<bool> CrankResult;

        /// <summary>0..1 chance that a crank catches right now.</summary>
        public float StartChance
        {
            get
            {
                var e = driver ? driver.Engine : null;
                if (!e) return 0f;
                var ep = e.GetComponent<VehiclePart>();
                // condition: a sound engine (75 %+) always catches; below that the chance falls gently to ~80 % at half
                // condition, then steeply towards a wreck
                float cond = 1f - (ep ? ep.damage : 0f);
                float c = cond >= 0.75f ? 1f : cond >= 0.5f ? Mathf.Lerp(0.8f, 1f, (cond - 0.5f) / 0.25f) : Mathf.Lerp(0.12f, 0.8f, cond / 0.5f);
                c += ep ? (ep.quality - 1) * 0.04f : 0f;
                float ambient = MadMax.World.Weather.Temperature;
                if (Temperature < 45f) c -= ambient < -10f ? 0.25f : ambient < 0f ? 0.1f : 0f;                   // a cold engine is stubborn
                if (UsesPlugs && plugs < 0.3f) c -= 0.2f;
                if (airFilter < 0.3f) c -= 0.1f;
                if (!oilInFuel && OilFraction < 0.25f) c -= 0.1f;
                return Mathf.Clamp(c, 0.05f, 1f);
            }
        }

        /// <summary>Turn the key: a second or so on the starter, then it catches or it doesn't.</summary>
        /// <summary>A sealed hull breathes through its snorkel (submarine): no flooding inside; <see cref="noAir"/> = the
        /// snorkel is under water, so the diesel can't start (and stops).</summary>
        [System.NonSerialized] public bool sealedHull, noAir;

        /// <summary>A loose battery lead (the story's stranded car): the starter doesn't even click until someone with a
        /// wrench reconnects it (<see cref="Reconnect"/>, on the service key). Saved with the vehicle.</summary>
        public bool disconnected;

        public void Reconnect() { disconnected = false; nextCrank = 0f; }

        public void Crank()
        {
            if (Started || Cranking || Time.time < nextCrank || !driver || !driver.Engine) return;
            if (disconnected)
            {
                nextCrank = Time.time + 2f;
                if (driver.Occupied) MadMax.Game.WastelandGame.Instance?.Toast("NOTHING. NOT EVEN A CLICK: A BATTERY LEAD HANGS LOOSE (" + MadMax.Game.Controls.Name(MadMax.Game.Controls.Act.Service) + " WITH A WRENCH)");
                return;
            }
            if (noAir) { if (driver.Occupied) MadMax.Game.WastelandGame.Instance?.Toast("NO AIR FOR THE DIESEL: SURFACE TO RUN IT"); nextCrank = Time.time + 2f; return; }
            var ep = driver.Engine.GetComponent<VehiclePart>();
            if (ep && ep.partId == "engine_pedals") { Started = true; return; }
            crankUntil = Time.time + UnityEngine.Random.Range(0.7f, 1.5f);
            nextCrank = crankUntil + 0.6f;
            crankCatches = fuel > 0f && !WrongFuel && !(ep && ep.damage >= 1f) && UnityEngine.Random.value < StartChance;
        }

        /// <summary>Running without a crank (the title film's car, a vehicle handed over already running).</summary>
        public void ForceStart() { Started = true; crankUntil = -1f; }

        /// <summary>Switch off (the driver got out, or it stalled).</summary>
        public void Stop() { Started = false; crankUntil = -1f; }

        VehicleDriver driver;
        VehicleDamage damage;
        BikeBalance bike;
        VehicleChassis chassis;
        bool hasRadiatorSocket;
        MountSocket radiatorSocket;
        float fxTimer, snorkelCheck;
        Snorkel snorkel;
        readonly System.Collections.Generic.List<MountSocket> exhaustSockets = new System.Collections.Generic.List<MountSocket>();
        Vector3 tailpipe;                    // local outlet when no exhaust part is mounted (under the rear bumper)
        bool tailpipeKnown, wasStarted;
        float smokeAcc;
        int smokeRound;

        /// <summary>Exhaust smoke right now, 0 clean .. 1 thick black (engine wear, worn plugs, clogged filter, old or low
        /// oil, diesel under load). Diagnostics and the dashboard read it.</summary>
        public float ExhaustDarkness { get; private set; }
        /// <summary>Where the last exhaust puff left (world).</summary>
        public Vector3 LastOutlet { get; private set; }
        /// <summary>Exhaust puffs per second right now.</summary>
        public float ExhaustRate { get; private set; }

        const float Ambient = 25f, HotLimit = 110f, CriticalLimit = 125f;

        void Awake()
        {
            driver = GetComponent<VehicleDriver>();
            damage = GetComponent<VehicleDamage>();
            bike = GetComponent<BikeBalance>();
            chassis = GetComponent<VehicleChassis>();
            foreach (var s in chassis.Sockets)
            {
                if (s.accepts == PartCategory.Radiator) radiatorSocket = s;
                if (s.accepts == PartCategory.Exhaust) exhaustSockets.Add(s);
            }
            hasRadiatorSocket = radiatorSocket;
        }

        public float FuelFraction => fuelCapacity > 0 ? fuel / fuelCapacity : 0f;
        public float OilFraction => oilInFuel || oilCapacity <= 0 ? 1f : oil / oilCapacity;
        public float CoolantFraction => !usesCoolant || coolantCapacity <= 0 ? 1f : coolant / coolantCapacity;

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            var engine = driver.Engine;
            // ignition: AI drivers' engines run; a player's cranks on the throttle
            if (driver.aiDriven && engine) Started = true;
            else if (!Started && driver.Occupied && engine && (driver.throttleInput > 0.1f || driver.brakeInput > 0.1f)) Crank();
            if (crankUntil > 0f && Time.time >= crankUntil)
            {
                crankUntil = -1f;
                Started = crankCatches;
                CrankResult?.Invoke(crankCatches);
                if (!crankCatches && driver.Occupied && !driver.aiDriven)
                {
                    var eng = driver.Engine ? driver.Engine.GetComponent<VehiclePart>() : null;
                    string why = fuel <= 0f ? "NO FUEL IN THE TANK" : WrongFuel ? "WRONG FUEL IN THE TANK: SIPHON IT" : eng && eng.damage >= 1f ? "THE ENGINE IS SEIZED"
                               : UsesPlugs && plugs < 0.3f ? "WORN PLUGS: TRY AGAIN, OR REPLACE THEM" : Temperature < 45f && MadMax.World.Weather.Temperature < 0f ? "COLD ENGINE: TRY AGAIN"
                               : "IT DIDN'T CATCH: TRY AGAIN (ENGINE " + Mathf.RoundToInt(StartChance * 100f) + "%)";
                    MadMax.Game.WastelandGame.Instance?.Toast(why);
                }
            }
            var f = Fault.None;
            var ep = engine ? engine.GetComponent<VehiclePart>() : null;
            float engineDamage = ep ? ep.damage : 0f;
            float speed = Mathf.Abs(driver.ForwardSpeed);

            // leaks happen whether or not the engine runs
            if (ep && !oilInFuel && engineDamage > 0.4f) { oil = Mathf.Max(0f, oil - (engineDamage - 0.4f) * 0.01f * dt); f |= Fault.OilLeak; }
            // the radiator holds the coolant: missing = it pours out, damaged = it leaks and cools worse
            VehiclePart radiator = null;
            if (usesCoolant)
            {
                radiator = radiatorSocket ? radiatorSocket.Current : null;
                if (hasRadiatorSocket && !radiator) { coolant = Mathf.Max(0f, coolant - 0.6f * dt); f |= Fault.CoolantLeak | Fault.NoRadiator; }
                else if (radiator && radiator.damage > 0.2f) { coolant = Mathf.Max(0f, coolant - (radiator.damage - 0.2f) * 0.05f * dt); f |= Fault.CoolantLeak; }
            }
            if (damage && damage.FrameDamage > 0.5f && fuel > 0f) { fuel = Mathf.Max(0f, fuel - (damage.FrameDamage - 0.5f) * 0.02f * dt); f |= Fault.FuelLeak; }

            if (fuel < 0.5f) tankKind = ResourceType.None;
            else if (tankKind == ResourceType.None && engine) tankKind = FuelKind;      // factory fill matches the engine
            if (!engine) { Faults = f | Fault.NoEngine; PowerFactor = 0f; Cool(dt, speed); return; }
            if (ep && ep.partId == "engine_pedals")
            {
                // a bicycle: no fuel, no oil, no heat — the rider's legs. Cruising is free; Shift = out of the
                // saddle, full power for stamina (NPCs never tire)
                float legs = driver.Occupied ? 1f : 0f;
                var game = MadMax.Game.WastelandGame.Instance;
                if (legs > 0f && game && game.Current == driver && game.Vitals)
                {
                    bool sprint = bike && bike.leanBack && driver.DriveCommand > 0.1f && !game.Vitals.Exhausted;
                    if (sprint) game.Vitals.Spend(driver.DriveCommand * 9f * dt);
                    game.Stats?.Practice(MadMax.RPG.Skill.Athletics, driver.DriveCommand * dt * (sprint ? 0.08f : 0.02f));
                    legs = game.Vitals.Exhausted ? 0.35f : sprint ? 1f : 0.6f;
                }
                PowerFactor = legs; Faults = f & (Fault.FuelLeak | Fault.OnFire); Cool(dt, speed);
                return;
            }
            bool seized = engineDamage >= 1f;
            bool wrong = WrongFuel;
            if (wrong) f |= Fault.WrongFuel;
            if (seized) f |= Fault.Seized;
            if (fuel <= 0f) f |= Fault.NoFuel; else if (FuelFraction < 0.1f) f |= Fault.LowFuel;
            if (!oilInFuel) { if (oil <= 0f) f |= Fault.NoOil; else if (OilFraction < 0.25f) f |= Fault.LowOil; }
            if (usesCoolant && CoolantFraction < 0.3f) f |= Fault.LowCoolant;

            // intake under water: the engine drowns (and hydrolocks if it keeps turning)
            var terrain = MadMax.World.DeformableTerrain.Instance;
            var ePos = engine.transform.position;
            float lvl = terrain ? terrain.WaterLevel(ePos.x, ePos.z) : float.NaN;
            if ((snorkelCheck -= dt) <= 0f) { snorkelCheck = 1f; snorkel = GetComponentInChildren<Snorkel>(); }
            float intake = snorkel && snorkel.Mounted ? Mathf.Max(ePos.y + 0.2f, snorkel.Intake.y) : ePos.y + 0.2f;      // a snorkel breathes high
            bool flooded = !sealedHull && !float.IsNaN(lvl) && intake < lvl;
            if (noAir && Started) Stop();
            if (flooded) { f |= Fault.Flooded; if (driver.Occupied && driver.DriveCommand > 0.1f) ep.damage += 0.04f * dt; }
            UpdateFire(dt, ePos, ref f);
            if (Started && (fuel <= 0f || seized || flooded || wrong)) Started = false;                           // stalled
            if (!Started) f |= Fault.EngineOff;
            if (Started && !wasStarted) smokeAcc += 6f + 14f * engineDamage;                                       // the cough as it catches
            wasStarted = Started;
            bool running = driver.Occupied && Started && fuel > 0f && !seized && !flooded && !wrong;
            float power = running ? 1f : 0f;
            if (running)
            {
                float load = driver.DriveCommand;
                float rpmFrac = driver.Rpm / engine.maxRpm;
                float burn = consumption * fuelMultiplier * (0.12f + 0.88f * load) * (0.3f + rpmFrac) * engine.maxTorque / 500f * 0.004f * dt;
                if (additive > 0f) { burn *= 0.75f; additive = Mathf.Max(0f, additive - burn); }
                fuel = Mathf.Max(0f, fuel - burn);
                if (!oilInFuel) oil = Mathf.Max(0f, oil - 0.00008f * load * dt);

                // heat in, heat out. Pinned against something at full throttle the rev limiter bounces (less heat than
                // a real pull) and the engine labours; the thermal mass gives about a minute and a half before trouble
                bool pinned = speed < 1.5f && load > 0.8f && rpmFrac > 0.93f;
                float heat = (0.25f + load * rpmFrac * (pinned ? 0.6f : 1f)) * 4.4f * (TryGetComponent<VehicleTuning>(out var tune) ? tune.HeatFactor : 1f);   // hot engine maps, boost, nitrous
                float radiatorEff = !hasRadiatorSocket ? 1f : radiator ? (1f - 0.6f * Mathf.Clamp01(radiator.damage)) * MetalPartFunctions.Cooling(radiator.partId) : 0f;
                float cooling = usesCoolant ? CoolantFraction * radiatorEff * (0.72f + 0.28f * Mathf.Clamp01(speed / 20f)) : 0.6f + Mathf.Clamp01(speed / 25f) * 0.4f;   // fan at a standstill
                Temperature += (heat - (Temperature - Ambient) * 0.08f * Mathf.Max(cooling, 0.05f)) * 0.45f * dt;   // ~30 s time constant
                if (pinned && Temperature > 95f) f |= Fault.Labouring;

                if (FuelFraction < 0.03f && Mathf.PerlinNoise(Time.time * 3f, 0.5f) < 0.4f) power = 0f;          // sputtering
                if (Temperature > HotLimit) { f |= Fault.Overheat; power *= Mathf.Lerp(1f, 0.4f, (Temperature - HotLimit) / 20f); }
                if (Temperature > CriticalLimit) ep.damage += 0.02f * dt;                                          // head gasket
                if (!oilInFuel && oil <= 0f) ep.damage += 0.05f * dt;                                                // seizing
                else if (!oilInFuel && OilFraction < 0.25f) ep.damage += 0.002f * load * dt;                        // wear
                // maintenance: old oil wears the engine, a clogged filter starves it, worn plugs misfire
                hours += dt / 3600f;
                if (!oilInFuel) oilLife = Mathf.Max(0f, oilLife - dt * (0.00002f + 0.0001f * load) * (Temperature > 100f ? 2f : 1f) * (radiator ? MetalPartFunctions.OilWear(radiator.partId) : 1f));
                var ground = terrain ? terrain.SurfaceAt(ePos.x, ePos.z) : default;
                float dust = (1f - ground.wet) * (1f - ground.road) * Mathf.Clamp01(ground.softness * 1.5f + 0.2f) * Mathf.Clamp01(speed / 12f) + MadMax.World.Storms.Dust * 2f;
                airFilter = Mathf.Max(0f, airFilter - dt * (0.00001f + 0.00012f * dust));
                if (UsesPlugs) plugs = Mathf.Max(0f, plugs - dt * 0.000012f * (0.4f + rpmFrac));
                if (!oilInFuel && oilLife < 0.25f) { f |= Fault.ServiceDue; ep.damage += 0.0008f * (0.3f + load) * dt; }
                if (airFilter < 0.3f) { f |= Fault.Clogged; power *= 0.7f + airFilter; }
                if (UsesPlugs && plugs < 0.3f) { f |= Fault.Misfire; if (Mathf.PerlinNoise(Time.time * 9f, 3.3f) < 0.35f - plugs) power *= 0.4f; }
                power *= 1f - 0.5f * Mathf.Clamp01(engineDamage - 0.5f) * 2f * (0.5f + 0.5f * Mathf.PerlinNoise(Time.time * 6f, 1.7f)); // misfires
            }
            else Cool(dt, speed);
            PowerFactor = power;
            Faults = f;
            Effects(engine, running, engineDamage);
            Exhaust(engine, running, engineDamage, dt);
        }

        /// <summary>Tailpipe smoke: a faint haze when healthy that thickens with load; worn engines, misfiring plugs, a
        /// choked filter and burning oil make it denser and blacker, diesels soot up under load. Emitted from each
        /// mounted exhaust part's outlet, else from under the rear bumper.</summary>
        void Exhaust(EngineStats engine, bool running, float engineDamage, float dt)
        {
            if (!running) { ExhaustRate = 0f; ExhaustDarkness = 0f; smokeAcc = Mathf.Min(smokeAcc, 20f); if (!Started) return; }
            float load = running ? driver.DriveCommand : 0f;
            float rpm = running ? Mathf.Clamp01(driver.Rpm / Mathf.Max(1f, engine.maxRpm)) : 0f;
            float wear = engineDamage;
            if (UsesPlugs) wear = Mathf.Max(wear, (0.4f - plugs) * 1.5f);
            wear = Mathf.Max(wear, (0.35f - airFilter) * 1.4f);
            if (!oilInFuel) wear = Mathf.Max(wear, Mathf.Max((0.3f - oilLife) * 1.2f, (0.25f - OilFraction) * 1.6f));
            else wear = Mathf.Max(wear, 0.25f);                                                          // two-strokes burn their oil
            wear = Mathf.Clamp01(wear);
            bool diesel = FuelKind == ResourceType.Diesel;
            float dark = Mathf.Clamp01(0.08f + wear * 0.95f + (diesel ? 0.35f * load * (0.5f + rpm) : 0f));
            float rate = running ? (1.2f + 6f * load * (0.4f + rpm)) * (1f + 3f * wear) + (diesel ? 4f * load : 0f) : 0f;
            ExhaustDarkness = dark; ExhaustRate = rate;

            // only near the player (the isometric camera itself hangs far off)
            var game = MadMax.Game.WastelandGame.Instance;
            var near = game ? (game.Current ? game.Current.transform : game.Player ? game.Player.transform : null) : null;
            if (near && (near.position - transform.position).sqrMagnitude > 140f * 140f) { smokeAcc = 0f; return; }
            smokeAcc = Mathf.Min(smokeAcc + rate * dt, 30f);
            if (smokeAcc < 1f) return;
            if (!tailpipeKnown) FindTailpipe();
            var weather = MadMax.World.Weather.Temperature;
            // cold air: white vapour mixes into a healthy engine's plume
            Color clean = weather < 5f ? new Color(0.92f, 0.93f, 0.95f, 0.5f) : new Color(0.72f, 0.72f, 0.74f, 0.28f);
            Color soot = new Color(0.05f, 0.045f, 0.04f, 1f);
            Color c = Color.Lerp(clean, soot, dark);
            float size = 0.3f + 0.9f * dark + 0.2f * load;
            float life = 1.4f + 2f * dark;
            Vector3 vel = driver.Body ? driver.Body.linearVelocity * 0.4f : Vector3.zero;
            int outlets = 0;
            while (smokeAcc >= 1f)
            {
                smokeAcc -= 1f;
                Vector3 at, dir;
                Outlet(smokeRound++, out at, out dir);
                LastOutlet = at;
                Vector3 v = vel + dir * (1.2f + 2.5f * load * rpm) + Vector3.up * (0.5f + 0.5f * dark) + UnityEngine.Random.insideUnitSphere * 0.2f;
                MadMax.World.Fx.Smoke(at, v, size * UnityEngine.Random.Range(0.8f, 1.25f), c, life);
                if (++outlets > 12) { smokeAcc = 0f; break; }
            }
        }

        void Outlet(int i, out Vector3 at, out Vector3 dir)
        {
            int n = exhaustSockets.Count;
            for (int k = 0; k < n; k++)
            {
                var s = exhaustSockets[(i + k) % n];
                var part = s ? s.Current : null;
                if (!part) continue;
                var t = part.transform;
                const float v = 0.08f;
                if (part.partId == "exhaust_stack") { at = t.TransformPoint(0f, 33 * v, -3 * v); dir = (t.up * 0.8f - t.forward * 0.4f).normalized; return; }
                if (part.partId == "exhaust_side_pipes") { at = t.TransformPoint(0.5f * v, 0.5f * v, -15.5f * v); dir = -t.forward; return; }
                if (MetalPartFunctions.Outlet(part, i, out at, out dir)) return;
                at = t.position; dir = -transform.forward; return;
            }
            at = transform.TransformPoint(tailpipe); dir = -transform.forward;
        }

        /// <summary>Under the rear bumper, a little right of centre, from the vehicle's mesh bounds (local space).</summary>
        void FindTailpipe()
        {
            tailpipeKnown = true;
            bool any = false;
            var b = new Bounds();
            foreach (var mf in GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.sharedMesh || mf.GetComponentInParent<VehiclePart>()) continue;
                var mb = mf.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    var p = transform.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            tailpipe = any ? new Vector3(b.center.x + b.extents.x * 0.4f, b.min.y + Mathf.Min(0.3f, b.size.y * 0.15f), b.min.z + 0.05f) : new Vector3(0.3f, 0.3f, -2f);
        }

        void Cool(float dt, float speed) => Temperature = Mathf.MoveTowards(Temperature, Ambient, (0.3f + speed * 0.05f) * dt);

        void Effects(EngineStats engine, bool running, float engineDamage)
        {
            var fx = DebrisSystem.Instance;
            if (!fx || (fxTimer -= Time.fixedDeltaTime) > 0f) return;
            fxTimer = 0.08f;
            var at = engine.transform.position + transform.up * 0.5f;
            if (Temperature > HotLimit)
                fx.EmitPuff(at + UnityEngine.Random.insideUnitSphere * 0.3f, new Color32(235, 230, 220, 255), 0.12f, Vector3.up * 1.8f + UnityEngine.Random.insideUnitSphere * 0.4f, 1.1f);
            if (running && engineDamage > 0.5f)
                fx.EmitPuff(at + UnityEngine.Random.insideUnitSphere * 0.2f, new Color32(30, 26, 24, 255), 0.14f, Vector3.up * 1.2f + UnityEngine.Random.insideUnitSphere * 0.3f, 1.6f);
            // a leaking or missing radiator steams at the front while the engine is warm (and goes on a while after)
            if ((Faults & Fault.CoolantLeak) != 0 && (running || Temperature > 55f))
            {
                var rad = radiatorSocket ? radiatorSocket.transform.position : engine.transform.position + transform.forward * 0.6f;
                float hot = Mathf.Clamp01((Temperature - 40f) / 60f);
                for (int i = 0; i < 2; i++)
                    MadMax.World.Fx.Smoke(rad + transform.up * 0.2f + UnityEngine.Random.insideUnitSphere * 0.25f,
                        Vector3.up * (1.2f + hot) + transform.forward * 0.4f + UnityEngine.Random.insideUnitSphere * 0.3f,
                        0.3f + 0.35f * hot, new Color(0.95f, 0.96f, 0.97f, 0.35f + 0.3f * hot), 1.1f);
            }
            if ((Faults & (Fault.OilLeak | Fault.CoolantLeak | Fault.FuelLeak)) != 0 && UnityEngine.Random.value < 0.25f)
            {
                var col = (Faults & Fault.CoolantLeak) != 0 ? new Color32(80, 200, 150, 255) : (Faults & Fault.FuelLeak) != 0 ? new Color32(200, 170, 60, 255) : new Color32(40, 28, 16, 255);
                fx.EmitPuff(at - transform.up * 0.4f, col, 0.05f, Vector3.down * 2f, 0.4f);
            }
        }

        /// <summary>Top up fuel/oil/coolant from the inventory. Returns litres moved.</summary>
        public int Service(Inventory inv)
        {
            int moved = 0;
            var kind = FuelKind;
            if (Accepts(kind)) { int n = Fill(inv, kind, ref fuel, fuelCapacity); if (n > 0) { tankKind = kind; moved += n; } }
            if (!oilInFuel) moved += Fill(inv, ResourceType.Oil, ref oil, oilCapacity);
            if (usesCoolant) moved += Fill(inv, ResourceType.Coolant, ref coolant, coolantCapacity);
            return moved;
        }

        /// <summary>Drain fluids into the inventory (up to <paramref name="maxLitres"/> each). Returns litres moved.</summary>
        public int Siphon(Inventory inv, int maxLitres = 200)
        {
            int moved = 0;
            moved += Drain(inv, tankKind == ResourceType.Diesel ? ResourceType.Diesel : ResourceType.Fuel, ref fuel, maxLitres);
            if (fuel < 1f) fuel = 0f;                                                    // the dregs go on the ground
            moved += Drain(inv, ResourceType.Oil, ref oil, maxLitres);
            moved += Drain(inv, ResourceType.Coolant, ref coolant, maxLitres);
            return moved;
        }

        public bool NeedsService(Inventory inv) =>
            (fuel < fuelCapacity - 1f && inv.Get(FuelKind) > 0 && Accepts(FuelKind)) ||
            (!oilInFuel && oil < oilCapacity - 0.5f && inv.Get(ResourceType.Oil) > 0) ||
            (usesCoolant && coolant < coolantCapacity - 0.5f && inv.Get(ResourceType.Coolant) > 0);

        public float TotalFluids => fuel + oil + coolant;

        static int Fill(Inventory inv, ResourceType t, ref float level, float cap)
        {
            int n = Mathf.Min(inv.Get(t), Mathf.FloorToInt(cap - level));
            if (n <= 0) return 0;
            inv.TrySpend(t, n);
            level += n;
            return n;
        }

        static int Drain(Inventory inv, ResourceType t, ref float level, int max)
        {
            int n = Mathf.Min(max, Mathf.FloorToInt(level));
            if (n <= 0) return 0;
            level -= n;
            inv.Add(t, n);
            return n;
        }

        MadMax.World.Fire fire;
        float heatSoak;

        /// <summary>External heat (a fire next to or under the vehicle).</summary>
        public void Heat(float amount) { heatSoak += amount * (TryGetComponent<VehicleArmor>(out var a) ? a.FireFactor : 1f); }

        public bool Burning => fire;

        void UpdateFire(float dt, Vector3 enginePos, ref Fault f)
        {
            var damage = GetComponent<VehicleDamage>();
            float frame = damage ? damage.FrameDamage : 0f;
            heatSoak = Mathf.Max(0f, heatSoak - dt * 0.2f);
            // ignition: cooked engine, ruptured fuel system on a wreck, or outside heat
            if (!fire && (Temperature > CriticalLimit + 15f || (frame > 0.85f && fuel > 1f && UnityEngine.Random.value < dt * 0.05f) || heatSoak > 2f))
                fire = MadMax.World.Fire.Ignite(enginePos + Vector3.up * 0.3f, transform, 25f + fuel * 0.5f, 0.7f);
            if (!fire) return;
            f |= Fault.OnFire;
            if (fuel > 0f) { fuel = Mathf.Max(0f, fuel - 0.4f * dt); fire.fuel = Mathf.Max(fire.fuel, 5f); }
            foreach (var p in GetComponentsInChildren<VehiclePart>()) p.damage = Mathf.Min(1f, p.damage + 0.004f * dt);
            Temperature += 3f * dt;
        }

        public string FaultText()
        {
            var f = Faults;
            if ((f & Fault.OnFire) != 0) return "ON FIRE!";
            if ((f & Fault.Flooded) != 0) return "ENGINE FLOODED";
            if ((f & Fault.WrongFuel) != 0) return "WRONG FUEL IN THE TANK: SIPHON IT (K)";
            if ((f & Fault.NoEngine) != 0) return "NO ENGINE";
            if ((f & Fault.Seized) != 0) return "ENGINE SEIZED";
            if ((f & Fault.NoFuel) != 0) return "OUT OF FUEL";
            if ((f & Fault.EngineOff) != 0 && driver && driver.Occupied) return Cranking ? "CRANKING..." : "ENGINE OFF: THROTTLE TO START";
            if ((f & Fault.NoOil) != 0) return "NO OIL - ENGINE DAMAGE";
            if ((f & Fault.Overheat) != 0) return "OVERHEATING";
            if ((f & Fault.Labouring) != 0) return "ENGINE LABOURING: EASE OFF";
            if ((f & Fault.NoRadiator) != 0) return "NO RADIATOR";
            if ((f & Fault.CoolantLeak) != 0) return "RADIATOR LEAK";
            if ((f & Fault.OilLeak) != 0) return "OIL LEAK";
            if ((f & Fault.FuelLeak) != 0) return "FUEL LEAK";
            if ((f & Fault.LowFuel) != 0) return "LOW FUEL";
            if ((f & Fault.LowOil) != 0) return "LOW OIL";
            if ((f & Fault.LowCoolant) != 0) return "LOW COOLANT";
            if ((f & Fault.Misfire) != 0) return "MISFIRING: WORN SPARK PLUGS";
            if ((f & Fault.Clogged) != 0) return "AIR FILTER CLOGGED";
            if ((f & Fault.ServiceDue) != 0) return "OIL CHANGE DUE";
            return null;
        }

        // ------------------------------------------------------------------ maintenance (roadmap 19)
        /// <summary>A service part in the pack would help: filter + a sump of oil, an air filter, plugs.</summary>
        public bool CanMaintain(Inventory inv) =>
            (!oilInFuel && oilLife < 0.9f && inv.GetItem("use_oil_filter") > 0 && inv.Get(ResourceType.Oil) >= Mathf.CeilToInt(oilCapacity)) ||
            (airFilter < 0.9f && inv.GetItem("use_air_filter") > 0) ||
            (UsesPlugs && plugs < 0.9f && inv.GetItem("use_spark_plugs") > 0);

        /// <summary>Oil change, air filter, plugs — whatever is due and in the pack. Returns what was done (or null).</summary>
        public string Maintain(Inventory inv)
        {
            var done = new List<string>();
            int sump = Mathf.CeilToInt(oilCapacity);
            if (!oilInFuel && oilLife < 0.9f && inv.GetItem("use_oil_filter") > 0 && inv.Get(ResourceType.Oil) >= sump)
            { inv.TakeItem("use_oil_filter"); inv.TrySpend(ResourceType.Oil, sump); oil = oilCapacity; oilLife = 1f; done.Add("OIL CHANGED"); }
            if (airFilter < 0.9f && inv.TakeItem("use_air_filter")) { airFilter = 1f; done.Add("NEW AIR FILTER"); }
            if (UsesPlugs && plugs < 0.9f && inv.TakeItem("use_spark_plugs")) { plugs = 1f; done.Add("NEW PLUGS"); }
            return done.Count > 0 ? string.Join(", ", done) : null;
        }

        public string MaintenanceState() => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.###},{1:0.###},{2:0.###},{3:0.##}", oilLife, airFilter, plugs, hours);

        public void LoadMaintenance(string s)
        {
            var a = s.Split(',');
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            if (a.Length > 0) float.TryParse(a[0], System.Globalization.NumberStyles.Float, ci, out oilLife);
            if (a.Length > 1) float.TryParse(a[1], System.Globalization.NumberStyles.Float, ci, out airFilter);
            if (a.Length > 2) float.TryParse(a[2], System.Globalization.NumberStyles.Float, ci, out plugs);
            if (a.Length > 3) float.TryParse(a[3], System.Globalization.NumberStyles.Float, ci, out hours);
        }
    }
}
