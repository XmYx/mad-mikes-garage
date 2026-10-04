using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Audio
{
    /// <summary>Procedural vehicle sound: an <see cref="EngineSynth"/> heard from two places — its exhaust voice at the
    /// tailpipe and its mechanical / intake voice at the engine (character from the mounted engine's
    /// <see cref="EngineSpec"/> and the exhaust part, condition from the engine, oil, plugs, filter, fuel blend, engine
    /// temperature and the exhaust's damage) — a <see cref="TyreSynth"/> voice at the axles (squeal, road, gravel, mud,
    /// wind), and a fire crackle while burning. Voices only run within <see cref="Range"/> of the listener.</summary>
    public class VehicleAudio : MonoBehaviour
    {
        public const float Range = 80f;

        VehicleDriver car;
        VehicleSystems sys;
        VehicleChassis chassis;
        SynthVoice exhaustVoice, bayVoice, tyreVoice;
        EngineSynth engine;
        TyreSynth tyres;
        VehiclePart exhaustPart, enginePart;
        string fitted;
        float silentFor = 99f;

        /// <summary>The engine sound simulation (tests, previews).</summary>
        public EngineSynth Engine => engine;

        void Awake()
        {
            car = GetComponent<VehicleDriver>(); sys = GetComponent<VehicleSystems>(); chassis = GetComponent<VehicleChassis>();
            if (sys) sys.CrankResult += caught =>
            {
                var g = MadMax.Game.WastelandGame.Instance;
                if (!caught && engine != null && g && g.Current == car) g.Toast("WON'T START - TRY AGAIN");
            };
            if (chassis) chassis.Changed += Refit;
        }

        void OnDestroy() { if (chassis) chassis.Changed -= Refit; }
        void OnDisable() { if (exhaustVoice) exhaustVoice.SetActive(false); if (bayVoice) bayVoice.SetActive(false); if (tyreVoice) tyreVoice.SetActive(false); }

        void Refit() => fitted = null;

        void Fit()
        {
            var eng = car.Engine;
            enginePart = eng ? eng.GetComponent<VehiclePart>() : null;
            string engineId = enginePart ? enginePart.partId : null;
            string exhaustId = null; bool exhaustSocket = false; Transform exhaust = null; exhaustPart = null;
            if (chassis)
                foreach (var s in chassis.Sockets)
                    if (s.accepts == PartCategory.Exhaust) { exhaustSocket = true; if (s.Current) { exhaustId = s.Current.partId; exhaust = s.Current.transform; exhaustPart = s.Current; } }
            string key = engineId + "|" + exhaustId + "|" + exhaustSocket;
            if (key == fitted && exhaustVoice && tyres != null) return;
            fitted = key;

            uint seed = (uint)GetHashCode();
            engine = engineId == null || engineId == "engine_pedals" ? null
                   : new EngineSynth(EngineProfile.For(engineId, exhaustId, exhaustSocket, eng ? eng.Spec : null), seed);   // pedals: silent
            // the exhaust voice at the tailpipe (the exhaust part, else the rear of the body), the bay voice at the engine
            Vector3 tail;
            if (exhaust) tail = transform.InverseTransformPoint(exhaust.position);
            else if (sys && sys.LastOutlet != Vector3.zero) tail = transform.InverseTransformPoint(sys.LastOutlet);
            else { var b = BodyBounds(); tail = new Vector3(b.center.x + b.extents.x * 0.5f, b.min.y + 0.2f, b.min.z); }
            var bay = transform.InverseTransformPoint(eng ? eng.transform.position : transform.position);
            ISynth exSynth = engine != null ? engine.Exhaust : null, baySynth = engine != null ? engine.Bay : null;
            if (!exhaustVoice) exhaustVoice = SynthVoice.Create(transform, "EngineExhaust", tail, exSynth, 120f);
            else { exhaustVoice.transform.localPosition = tail; exhaustVoice.synth = exSynth; }
            if (!bayVoice) bayVoice = SynthVoice.Create(transform, "EngineBay", bay, baySynth, 60f);
            else { bayVoice.transform.localPosition = bay; bayVoice.synth = baySynth; }
            if (tyres == null) tyres = new TyreSynth(seed * 31u);        // also after a play-mode script reload
            if (!tyreVoice) tyreVoice = SynthVoice.Create(transform, "TyreAudio", new Vector3(0f, 0.3f, 0f), tyres, 70f);
            else tyreVoice.synth = tyres;
        }

        Bounds BodyBounds()
        {
            var body = transform.Find("Body");
            var mf = body ? body.GetComponent<MeshFilter>() : null;
            return mf && mf.sharedMesh ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, new Vector3(1.8f, 1.4f, 4.2f));
        }

        /// <summary>Feed the simulation: what the engine does and what shape it is in.</summary>
        void Feed(EngineStats eng, bool running)
        {
            engine.maxRpm = eng.maxRpm; engine.idleRpm = eng.idleRpm;
            engine.rpm = car.Rpm;
            engine.load = Mathf.Clamp01(car.throttleInput * (sys ? sys.PowerFactor : 1f));
            engine.running = running;
            engine.cranking = sys && sys.Cranking;
            engine.crankCatches = sys && sys.CrankCatches;
            engine.crankProgress = sys ? sys.CrankProgress : 0f;
            engine.fuelled = !sys || (sys.fuel > 0f && !sys.WrongFuel);
            engine.wear = enginePart ? Mathf.Clamp01(enginePart.damage) : 0f;
            if (sys)
            {
                var blend = sys.Blend;
                engine.oil = sys.oilInFuel ? 1f : Mathf.Clamp01(Mathf.Min(sys.OilFraction * 2.5f, 0.4f + 0.6f * sys.OilProtection) * (0.7f + 0.3f * sys.oilLife));
                engine.misfire = Mathf.Clamp01(blend.misfire + (sys.UsesPlugs ? Mathf.Max(0f, 0.45f - sys.plugs) : 0f) + (sys.FuelFraction < 0.03f ? 0.4f : 0f));
                engine.ping = sys.UsesPlugs ? Mathf.Clamp01((blend.power < 0.97f ? (1f - blend.power) * 3f : 0f) + Mathf.Max(0f, sys.Temperature - 100f) / 25f) : 0f;
                engine.cold = Mathf.Clamp01((60f - sys.Temperature) / 50f);
                engine.clog = Mathf.Clamp01(1f - sys.airFilter * 1.4f);
            }
            float exDamage = exhaustPart ? Mathf.Clamp01(exhaustPart.damage) : 0f;
            engine.leak = Mathf.Clamp01(exDamage * 1.2f - 0.1f);                                     // a blowing joint grows with the dents
            engine.mufflerDamage = exDamage;
        }

        void Update()
        {
            if (!car || Application.isBatchMode) return;
            bool near = Sfx.HasListener && (Sfx.ListenerPosition - transform.position).sqrMagnitude < Range * Range && Time.timeScale > 0f;
            Sfx.Loop(this, "fire", sys && sys.Burning ? 0.8f : 0f, 1f, 30f);
            if (!near) { OnDisable(); return; }
            Fit();

            float master = MadMax.Game.GameSettings.Current.sfxVolume * MadMax.Game.GameSettings.Current.vehicleVolume;
            var game = MadMax.Game.WastelandGame.Instance;
            bool inside = game && game.Current == car;
            var eng = car.Engine;
            var net = MadMax.Net.NetSession.Instance;
            bool started = !sys || sys.Started || (net && net.Online && !net.Simulates(car));          // remote cars: assume running
            bool running = eng && started && (car.Occupied || car.throttleInput > 0.01f) && (!sys || sys.fuel > 0f);
            if (engine != null && eng) Feed(eng, running);
            bool busy = running || (sys && sys.Cranking);
            silentFor = busy ? 0f : silentFor + Time.deltaTime;
            bool voice = engine != null && silentFor < 3f;                                           // let the engine coast down, then free the voices
            exhaustVoice.gain = 1.3f * master;
            exhaustVoice.Source.spatialBlend = inside ? 0.5f : 1f;
            exhaustVoice.SetActive(voice);
            bayVoice.gain = (inside ? 0.9f : 1.1f) * master;
            bayVoice.Source.spatialBlend = inside ? 0.4f : 1f;
            bayVoice.SetActive(voice);

            float speed = Mathf.Abs(car.ForwardSpeed);
            tyres.squeal = car.TyreSqueal;
            tyres.roll = Mathf.Clamp01(car.TyreRoll / 25f);
            tyres.loose = Mathf.Clamp01(car.TyreLoose / 12f);
            tyres.mud = Mathf.Clamp01(car.TyreMud / 6f);
            float a = Mathf.Clamp01(speed / 40f); tyres.air = a * a;
            tyreVoice.gain = 0.55f * master;
            tyreVoice.Source.spatialBlend = inside ? 0.5f : 1f;
            tyreVoice.SetActive(speed > 0.3f || car.TyreSqueal > 0.01f || car.TyreMud > 0.1f);
        }
    }
}
