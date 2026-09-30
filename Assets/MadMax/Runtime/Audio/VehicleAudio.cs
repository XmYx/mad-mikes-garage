using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Audio
{
    /// <summary>Procedural vehicle sound: an <see cref="EngineSynth"/> voice at the exhaust (character from the mounted
    /// engine and exhaust parts), a <see cref="TyreSynth"/> voice at the axles (squeal, road, gravel, mud, wind), and a fire
    /// crackle while burning. Voices only run within <see cref="Range"/> of the listener.</summary>
    public class VehicleAudio : MonoBehaviour
    {
        public const float Range = 80f;

        VehicleDriver car;
        VehicleSystems sys;
        VehicleChassis chassis;
        SynthVoice engineVoice, tyreVoice;
        EngineSynth engine;
        TyreSynth tyres;
        string fitted;
        float silentFor = 99f;

        void Awake()
        {
            car = GetComponent<VehicleDriver>(); sys = GetComponent<VehicleSystems>(); chassis = GetComponent<VehicleChassis>();
            if (sys) sys.CrankResult += caught =>
            {
                if (engine == null) return;
                if (caught) Sfx.Play("engine_catch", transform.position, 0.8f, Random.Range(0.92f, 1.05f), 40f);
                else Sfx.Play("sputter", transform.position, 0.7f, Random.Range(0.9f, 1.1f), 30f);
                var g = MadMax.Game.WastelandGame.Instance;
                if (!caught && g && g.Current == car) g.Toast("WON'T START (ENGINE " + Mathf.RoundToInt(sys.StartChance * 100f) + "%) - TRY AGAIN");
            };
            if (chassis) chassis.Changed += Refit;
        }

        void OnDestroy() { if (chassis) chassis.Changed -= Refit; }
        void OnDisable() { if (engineVoice) engineVoice.SetActive(false); if (tyreVoice) tyreVoice.SetActive(false); }

        void Refit() => fitted = null;

        void Fit()
        {
            var eng = car.Engine;
            string engineId = eng && eng.TryGetComponent<VehiclePart>(out var ep) ? ep.partId : null;
            string exhaustId = null; bool exhaustSocket = false; Transform exhaust = null;
            if (chassis)
                foreach (var s in chassis.Sockets)
                    if (s.accepts == PartCategory.Exhaust) { exhaustSocket = true; if (s.Current) { exhaustId = s.Current.partId; exhaust = s.Current.transform; } }
            string key = engineId + "|" + exhaustId + "|" + exhaustSocket;
            if (key == fitted && engineVoice && tyres != null) return;
            fitted = key;

            uint seed = (uint)GetHashCode();
            engine = engineId == null || engineId == "engine_pedals" ? null : new EngineSynth(EngineProfile.For(engineId, exhaustId, exhaustSocket), seed);   // pedals: silent
            Transform at = exhaust ? exhaust : eng ? eng.transform : transform;
            var local = transform.InverseTransformPoint(at.position);
            if (!engineVoice) engineVoice = SynthVoice.Create(transform, "EngineAudio", local, engine, 120f);
            else { engineVoice.transform.localPosition = local; engineVoice.synth = engine; }
            if (tyres == null) tyres = new TyreSynth(seed * 31u);        // also after a play-mode script reload
            if (!tyreVoice) tyreVoice = SynthVoice.Create(transform, "TyreAudio", new Vector3(0f, 0.3f, 0f), tyres, 70f);
            else tyreVoice.synth = tyres;
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
            Sfx.Loop(this, "starter", sys && sys.Cranking && engine != null ? 0.7f : 0f, 1f, 30f);
            if (engine != null && eng)
            {
                engine.maxRpm = eng.maxRpm; engine.idleRpm = eng.idleRpm;
                engine.rpm = car.Rpm;
                engine.load = Mathf.Clamp01(car.throttleInput * (sys ? sys.PowerFactor : 1f));
                engine.running = running;
            }
            silentFor = running ? 0f : silentFor + Time.deltaTime;
            engineVoice.gain = 1.3f * master;
            engineVoice.Source.spatialBlend = inside ? 0.5f : 1f;
            engineVoice.SetActive(engine != null && silentFor < 3f);   // let the engine spin down, then free the voice

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
