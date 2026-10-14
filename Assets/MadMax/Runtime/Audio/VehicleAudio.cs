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
        const int FitsPerFrame = 2;
        /// <summary>Vehicles whose synth voices run at once (the driven one always): the synths render on the audio
        /// thread, and too many of them starve the mixer (every sound stutters).</summary>
        public const int MaxVoicedVehicles = 4;
        static int fitFrame = -1, fitsThisFrame, rankFrame = -1;
        static readonly System.Collections.Generic.List<VehicleAudio> all = new System.Collections.Generic.List<VehicleAudio>();
        static readonly VehicleAudio[] voiced = new VehicleAudio[MaxVoicedVehicles];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { fitFrame = -1; fitsThisFrame = 0; rankFrame = -1; all.Clear(); System.Array.Clear(voiced, 0, voiced.Length); }

        void OnEnable() { if (!all.Contains(this)) all.Add(this); }

        /// <summary>Once a frame: the driven vehicle, then the nearest others, get the voices.</summary>
        static void Rank()
        {
            if (rankFrame == Time.frameCount) return;
            rankFrame = Time.frameCount;
            System.Array.Clear(voiced, 0, voiced.Length);
            var g = MadMax.Game.WastelandGame.Instance;
            var lp = Sfx.ListenerPosition;
            int n = 0;
            for (int i = all.Count - 1; i >= 0; i--) if (!all[i]) all.RemoveAt(i);
            if (g && g.Current) foreach (var a in all) if (a.car == g.Current) { voiced[n++] = a; break; }
            while (n < MaxVoicedVehicles)
            {
                VehicleAudio best = null; float bd = Range * Range;
                foreach (var a in all)
                {
                    if (!a.isActiveAndEnabled) continue;
                    bool taken = false;
                    for (int k = 0; k < n; k++) if (voiced[k] == a) { taken = true; break; }
                    if (taken) continue;
                    float d = (a.transform.position - lp).sqrMagnitude * (a.hadVoice ? 0.64f : 1f);   // the one already heard keeps it unless ~20 % farther
                    if (d < bd) { bd = d; best = a; }
                }
                if (!best) break;
                voiced[n++] = best;
            }
        }

        bool hadVoice;

        bool Voiced()
        {
            Rank();
            hadVoice = false;
            for (int k = 0; k < voiced.Length; k++) if (voiced[k] == this) { hadVoice = true; break; }
            return hadVoice;
        }

        VehicleDriver car;
        VehicleSystems sys;
        VehicleChassis chassis;
        SynthVoice exhaustVoice, bayVoice, tyreVoice, drivelineVoice;
        EngineSynth engine;
        TyreSynth tyres;
        DrivelineSynth driveline;
        int lastGear = -99; bool lastReverse; float lastDrive, lashAt;
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

        void OnDestroy() { if (chassis) chassis.Changed -= Refit; all.Remove(this); }
        void OnDisable() { if (exhaustVoice) exhaustVoice.StopNow(); if (bayVoice) bayVoice.StopNow(); if (tyreVoice) tyreVoice.StopNow(); if (drivelineVoice) drivelineVoice.StopNow(); }

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
            if (driveline == null) driveline = new DrivelineSynth(seed * 17u);
            if (!drivelineVoice) drivelineVoice = SynthVoice.Create(transform, "DrivelineAudio", new Vector3(0f, 0.35f, 0f), driveline, 35f);
            else drivelineVoice.synth = driveline;
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
                engine.frost = sys.FrostDrag;
                engine.clog = Mathf.Clamp01(1f - sys.airFilter * 1.4f);
            }
            float exDamage = exhaustPart ? Mathf.Clamp01(exhaustPart.damage) : 0f;
            // a battered engine blows one manifold port (always the same one for this engine); a dented exhaust
            // splits along the pipe and muffler — the two sound in different places
            float bay = enginePart ? Mathf.Clamp01((enginePart.damage - 0.45f) * 1.6f) : 0f;
            engine.leak = Mathf.Clamp01(bay + exDamage * 0.25f);
            engine.leakCyl = bay > 0f ? (int)((uint)(enginePart.partId.GetHashCode() ^ GetHashCode()) % (uint)Mathf.Max(1, eng.Spec != null ? eng.Spec.cylinders : 4)) : -1;
            engine.leakPipe = Mathf.Clamp01(exDamage * 1.2f - 0.1f);                                 // a blowing joint grows with the dents
            engine.mufflerDamage = exDamage;
        }

        /// <summary>Gearbox, diff and clutch (<see cref="DrivelineSynth"/>): whine with the shafts, judder while the
        /// clutch slips, clunks on a shift, on engaging drive / reverse and on the slack taken up when the throttle
        /// reverses at speed. Engine-driven road vehicles only (not aircraft, boats or pedals).</summary>
        void Driveline(EngineStats eng, bool running, float speed, bool inside, float master)
        {
            if (driveline == null || !drivelineVoice) return;
            bool has = eng && engine != null && !car.aircraft;
            float drive = car.DriveCommand > 0.02f ? car.DriveCommand : -Mathf.Clamp01(car.brakeInput + (speed > 2f && car.DriveCommand <= 0.02f ? 0.4f : 0f));
            if (has)
            {
                if (lastGear != -99 && (car.Gear != lastGear || car.Reversing != lastReverse) && running)
                    driveline.Clunk(car.Reversing != lastReverse ? (speed < 1f ? 0.7f : 1f) : car.manual ? 0.55f : 0.35f);
                if (speed > 3f && Mathf.Sign(drive) != Mathf.Sign(lastDrive) && Mathf.Abs(drive - lastDrive) > 0.5f && Time.time - lashAt > 0.6f)
                { lashAt = Time.time; driveline.Clunk(0.3f + (enginePart ? enginePart.damage * 0.3f : 0f)); }
            }
            lastGear = car.Gear; lastReverse = car.Reversing; lastDrive = drive;
            driveline.inputRpm = has && car.Coupling > 0.05f ? car.Rpm * Mathf.Clamp01(car.Coupling * 1.5f) : 0f;
            driveline.shaftRpm = has ? car.ShaftRpm : 0f;
            driveline.drive = drive;
            driveline.slip = has ? 1f - car.Coupling : 0f;
            driveline.reverse = car.Reversing; driveline.first = !car.Reversing && car.Gear == 1;
            driveline.wear = Mathf.Clamp01((car.steerPull != 0f ? 0.3f : 0f) + (enginePart ? enginePart.damage * 0.4f : 0f));
            drivelineVoice.gain = (inside ? 0.9f : 0.6f) * master;
            drivelineVoice.Source.spatialBlend = inside ? 0.4f : 1f;
            drivelineVoice.SetActive(has && (speed > 0.5f || running));
        }

        void Update()
        {
            if (!car || Application.isBatchMode) return;
            bool near = Sfx.HasListener && (Sfx.ListenerPosition - transform.position).sqrMagnitude < Range * Range && Time.timeScale > 0f && Voiced();
            Sfx.Loop(this, "fire", sys && sys.Burning ? 0.8f : 0f, 1f, 30f);
            if (!near) { OnDisable(); return; }
            if (!exhaustVoice)
            {                                                                                   // first fit: a few vehicles a frame (each voice rewires the DSP graph)
                if (fitFrame != Time.frameCount) { fitFrame = Time.frameCount; fitsThisFrame = 0; }
                if (fitsThisFrame >= FitsPerFrame) return;
                fitsThisFrame++;
            }
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
            Driveline(eng, running, speed, inside, master);
        }
    }
}
