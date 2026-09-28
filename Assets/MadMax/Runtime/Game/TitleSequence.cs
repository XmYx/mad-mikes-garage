using MadMax.Designs;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game
{
    /// <summary>Cinematic title: live in-game shots (convoy flyover, a car tearing down the road, the machine yard), then
    /// dusk at Mad Mike's Garage: a flickering neon sign, a car doing a smoking, burning-rubber burnout and the slanted 3D
    /// arcade logo — the main menu fades in over it. No HUD. Any key skips to the finale.</summary>
    public class TitleSequence : MonoBehaviour
    {
        public static bool Playing { get; private set; }
        public static TitleSequence Instance { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { Playing = false; Instance = null; SkipToFinale = false; }
        /// <summary>The pre-rendered intro already played in the boot scene: start straight at the live finale (menu).</summary>
        public static bool SkipToFinale;

        // a scene reload (new game, load) destroys the sequence without Finish(): never leave the HUD hidden
        void OnDestroy() { if (Instance == this) { Playing = false; Instance = null; } }

        WastelandGame game;
        CameraRig rig;
        Camera cam;
        Transform ct;
        float t, savedHours;
        bool finale;
        VehicleDriver car;
        GameObject sign, logo;
        Renderer neon;
        Material neonMat, logoMat;
        Light pinkLight, cyanLight;
        Vector3 start, dir, side, stage, carSpot;
        public const float FinaleAt = 15f;
        /// <summary>The boot film is this sequence recorded for 26 s: it ends 11 s into the finale.</summary>
        public const float FilmEndsAt = 26f;
        float trackSide = 1f;

        public static void Begin(WastelandGame game)
        {
            if (!game.cameraRig || Playing) return;
            var ts = game.gameObject.AddComponent<TitleSequence>();
            ts.game = game; ts.rig = game.cameraRig;
            ts.Setup();
        }

        void Setup()
        {
            Instance = this; Playing = true;
            cam = rig.pixel.GetComponent<Camera>();
            ct = rig.pixel.transform;
            rig.enabled = false;
            var fade = rig.GetComponent<OccluderFade>(); if (fade) fade.enabled = false;
            savedHours = DayNight.Hours;
            DayNight.SetHours(16.5f);
            start = game.WorldSpawn; dir = game.SpawnDir; side = Vector3.Cross(Vector3.up, dir);
            stage = start - side * 30f + dir * 6f;
            // the star car: a fresh Interceptor clone (not part of the player's fleet)
            GameObject prefab = null;
            foreach (var p in game.vehiclePrefabs) if (p && p.name == "Interceptor") prefab = p;
            if (prefab)
            {
                var pos = Ground(start + dir * RoadAhead() + side * 1.5f) + Vector3.up * 0.8f;
                car = Instantiate(prefab, pos, Quaternion.LookRotation(dir)).GetComponent<VehicleDriver>();
                car.name = "IntroCar";
                if (DeformableTerrain.Instance) DeformableTerrain.Instance.extraFoci.Add(car.transform);   // stream the road ahead of it
                car.Occupied = true;
                car.bakedDriver = true;
                if (car.TryGetComponent<VehicleSystems>(out var sys)) sys.fuel = sys.fuelCapacity;
            }
        }

        /// <summary>Clear road past the parked convoy.</summary>
        float RoadAhead()
        {
            float far = 20f;
            foreach (var v in game.AllVehicles) if (v) { var d = v.transform.position - start; if (Mathf.Abs(Vector3.Dot(d, side)) < 6f) far = Mathf.Max(far, Vector3.Dot(d, dir) + 14f); }
            return far;
        }

        Vector3 Ground(Vector3 p) { var tr = DeformableTerrain.Instance; p.y = tr ? tr.Height(p.x, p.z) : p.y; return p; }

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        void Update()
        {
            if (!Playing) return;
            t += Time.deltaTime;
            var kb = Keyboard.current;
            if (!finale && (SkipToFinale || t > FinaleAt || (t > 0.5f && kb != null && kb.anyKey.wasPressedThisFrame))) StartFinale();
            if (!finale) Shots(); else Finale();
        }

        void LateUpdate() { if (Playing && cam) { cam.orthographic = false; } }

        void Shots()
        {
            cam.orthographic = false;
            if (t < 5f)
            {
                // high orbit over the convoy
                float a = t * 0.12f + 0.6f;
                var centre = start + dir * 8f;
                var pos = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 34f + Vector3.up * (26f - t * 1.5f);
                cam.fieldOfView = 42f;
                ct.SetPositionAndRotation(pos, Quaternion.LookRotation(centre - pos));
                if (car) Drive(0f, 0f, true);
            }
            else if (t < 11f)
            {
                // low tracking shot: the car tears off down the road
                if (car)
                {
                    Drive(1f, 0f, false);
                    var c = car.transform;
                    var back = -c.forward * (1.5f + (t - 5f) * 0.6f);
                    var pos = c.position + c.right * trackSide * 5.5f + Vector3.up * 1.1f + back;
                    // never film from inside a building: swap sides, else rise into a chase view
                    if (Physics.Linecast(c.position + Vector3.up, pos, out _, ~0, QueryTriggerInteraction.Ignore))
                    {
                        var other = c.position - c.right * trackSide * 5.5f + Vector3.up * 1.1f + back;
                        if (!Physics.Linecast(c.position + Vector3.up, other, out _, ~0, QueryTriggerInteraction.Ignore)) { trackSide = -trackSide; pos = other; }
                        else pos = c.position - c.forward * 8f + Vector3.up * 4f;
                    }
                    cam.fieldOfView = 55f;
                    ct.SetPositionAndRotation(pos, Quaternion.LookRotation(c.position + c.forward * 3f + Vector3.up * 0.5f - pos));
                }
            }
            else
            {
                // dolly past the machine yard
                if (car) Drive(0f, 1f, false);
                float k = Smooth((t - 11f) / 4f);
                var yard = start + side * 22f + dir * 24f;
                var pos = yard - side * 12f + dir * Mathf.Lerp(-22f, 10f, k) + Vector3.up * 11f;
                if (Physics.Linecast(pos, yard + Vector3.up * 1.5f, out _, ~0, QueryTriggerInteraction.Ignore)) pos += Vector3.up * 8f;   // look over buildings
                cam.fieldOfView = 48f;
                ct.SetPositionAndRotation(pos, Quaternion.LookRotation(yard + Vector3.up * 1.5f - pos));
            }
        }

        void Drive(float throttle, float brake, bool hold)
        {
            car.throttleInput = throttle; car.brakeInput = brake; car.handbrake = hold; car.steerInput = 0f;
        }

        void StartFinale()
        {
            finale = true;
            t = FinaleAt;
            DayNight.SetHours(20.1f);                                       // dusk: neon time
            var tr = DeformableTerrain.Instance;
            var faceCam = -side;                                            // the sign faces back towards the road
            var signPos = Ground(stage);
            sign = new GameObject("NeonSign");
            sign.transform.SetPositionAndRotation(signPos - Vector3.up * 0.05f, Quaternion.LookRotation(faceCam));
            sign.transform.localScale = new Vector3(-1f, 1f, 1f);          // letters are authored for the back view
            var (frameMesh, neonMesh) = TitleArt.NeonSign();
            var fr = new GameObject("Frame", typeof(MeshFilter), typeof(MeshRenderer));
            fr.transform.SetParent(sign.transform, false);
            fr.GetComponent<MeshFilter>().sharedMesh = frameMesh; fr.GetComponent<MeshRenderer>().sharedMaterial = game.propMaterial;
            var ng = new GameObject("Neon", typeof(MeshFilter), typeof(MeshRenderer));
            ng.transform.SetParent(sign.transform, false);
            ng.transform.localPosition = new Vector3(0, 0, 0.02f);
            ng.GetComponent<MeshFilter>().sharedMesh = neonMesh;
            neonMat = new Material(game.propMaterial); neonMat.SetFloat("_Unlit", 1f); neonMat.SetFloat("_OutlinePx", 0f); neonMat.SetColor("_Tint", new Color(1.9f, 1.9f, 1.9f, 1f));
            neon = ng.GetComponent<MeshRenderer>(); neon.sharedMaterial = neonMat;
            pinkLight = Glow(sign.transform, new Vector3(-1.2f, 4.9f, 1.2f), new Color(1f, 0.25f, 0.8f));
            cyanLight = Glow(sign.transform, new Vector3(1.2f, 3.3f, 1.2f), new Color(0.25f, 0.95f, 1f));
            // the car: side-on in front of the sign, locked up for a burnout
            carSpot = Ground(signPos + faceCam * 6.5f) + Vector3.up * 0.7f;
            if (car)
            {
                car.Body.linearVelocity = Vector3.zero; car.Body.angularVelocity = Vector3.zero;
                car.Body.position = carSpot; car.Body.rotation = Quaternion.LookRotation(dir);          // side-on to the camera
                car.transform.SetPositionAndRotation(carSpot, Quaternion.LookRotation(dir));
                car.Body.isKinematic = false;
            }
            var key = new GameObject("KeyLight").AddComponent<Light>();                    // warm fill on the car from the camera side
            key.transform.SetParent(sign.transform, true);
            key.transform.position = carSpot - side * 4f + Vector3.up * 2.5f;
            key.type = LightType.Point; key.range = 10f; key.intensity = 22f; key.color = new Color(1f, 0.75f, 0.5f); key.shadows = LightShadows.None;
            // 3D logo floating in front of the camera
            logo = new GameObject("TitleLogo", typeof(MeshFilter), typeof(MeshRenderer));
            logo.GetComponent<MeshFilter>().sharedMesh = TitleArt.Logo();
            logoMat = new Material(game.propMaterial); logoMat.SetFloat("_Unlit", 0.6f); logoMat.SetColor("_Tint", new Color(1.4f, 1.4f, 1.4f, 1f));
            logo.GetComponent<MeshRenderer>().sharedMaterial = logoMat;
            logo.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            logo.transform.localScale = Vector3.zero;
            if (SkipToFinale) { t = FilmEndsAt; if (logo) logo.transform.localScale = Vector3.one * 0.62f; }   // continue exactly where the film stopped
            if (!IntroRecorder.Recording) game.Menus.Open(MenuSystem.Page.Main);
        }

        /// <summary>Back to the first shot (used when recording the boot film).</summary>
        public void Restart()
        {
            t = 0f; finale = false;
            if (sign) Destroy(sign); if (logo) Destroy(logo);
            game.Menus.Close();
            Playing = true; Instance = this;
            if (car) { var pos = Ground(start + dir * RoadAhead() + side * 1.5f) + Vector3.up * 0.8f; car.Body.position = pos; car.Body.rotation = Quaternion.LookRotation(dir); car.Body.linearVelocity = Vector3.zero; car.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir)); }
            DayNight.SetHours(16.5f);
        }

        static Light Glow(Transform parent, Vector3 at, Color c)
        {
            var l = new GameObject("NeonGlow").AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.localPosition = at;
            l.type = LightType.Point; l.color = c; l.range = 12f; l.intensity = 18f; l.shadows = LightShadows.None;
            return l;
        }

        void Finale()
        {
            float ft = t - FinaleAt;
            cam.fieldOfView = 46f;
            // slow push-in, low angle, sign and car framed with the logo above
            var target = carSpot + Vector3.up * 1.6f - side * 0.5f + dir * 2.8f;     // car sits right of centre, menu on the left
            var back = -side * Mathf.Lerp(17f, 12.5f, Smooth(ft / 8f));
            var pos = carSpot + back + dir * (2.5f + Mathf.Sin(ft * 0.15f) * 1.2f) + Vector3.up * 1.4f;
            ct.SetPositionAndRotation(pos, Quaternion.LookRotation(target - pos));
            // neon: stutter on, then hum with the odd flicker
            bool lit = ft > 1.2f ? Mathf.PerlinNoise(t * 7f, 3.3f) > 0.07f : (Mathf.Repeat(ft * 9f, 1f) > 0.55f && ft > 0.3f);
            if (neon) neon.enabled = lit;
            if (pinkLight) pinkLight.enabled = lit;
            if (cyanLight) cyanLight.enabled = lit;
            // burnout: throttle + brake (line lock), rubber smoke and the rear tyres catching fire
            if (car)
            {
                Drive(1f, 1f, false);
                if (Vector3.Distance(car.transform.position, carSpot) > 1.5f) { car.Body.position = Vector3.Lerp(car.Body.position, carSpot, 0.1f); }
                var rear = car.transform.TransformPoint(new Vector3(0f, 0.25f, -1.5f));
                for (int s = -1; s <= 1; s += 2)
                {
                    var wheel = rear + car.transform.right * s * 0.8f;
                    if (Random.value < 0.45f) Fx.Smoke(wheel, -car.transform.forward * 2.4f + Vector3.up * 0.9f + Random.insideUnitSphere * 0.5f, Random.Range(0.7f, 1.4f), new Color(0.88f, 0.88f, 0.86f, 0.6f), 3f);
                    if (ft > 2.5f && Random.value < 0.35f) Fx.Smoke(wheel + Vector3.up * 0.1f, Vector3.up * 1.6f, Random.Range(0.25f, 0.5f), Color.Lerp(new Color(1f, 0.8f, 0.3f, 0.95f), new Color(1f, 0.35f, 0.08f, 0.9f), Random.value), 0.4f);
                    if (Random.value < 0.25f) Fx.Sparks(wheel, -car.transform.forward + Vector3.up, 2, new Color(1f, 0.75f, 0.35f));
                }
            }
            // logo: pops in with an overshoot, then sways
            if (logo)
            {
                float k = Mathf.Clamp01((ft - 0.8f) / 0.7f);
                float pop = k < 1f ? 1f + Mathf.Sin(k * Mathf.PI) * 0.25f : 1f;
                logo.transform.localScale = Vector3.one * 0.62f * Smooth(k) * pop;
                var anchor = ct.position + ct.forward * 10f + ct.up * 2.1f + ct.right * 0.9f;
                logo.transform.SetPositionAndRotation(anchor, ct.rotation * Quaternion.Euler(-10f + Mathf.Sin(ft * 0.8f) * 2f, Mathf.Sin(ft * 0.5f) * 8f, 3f));
            }
        }

        /// <summary>Tear down the set and hand the camera back (a game starts or loads).</summary>
        public void Finish()
        {
            if (!Playing) return;
            Playing = false; Instance = null;
            if (car && DeformableTerrain.Instance) DeformableTerrain.Instance.extraFoci.Remove(car.transform);
            if (car) Destroy(car.gameObject);
            if (sign) Destroy(sign);
            if (logo) Destroy(logo);
            if (neonMat) Destroy(neonMat);
            if (logoMat) Destroy(logoMat);
            DayNight.SetHours(savedHours);
            rig.enabled = true;
            var fade = rig.GetComponent<OccluderFade>(); if (fade) fade.enabled = true;
            if (game.Current) rig.SetTarget(game.Current.transform);
            else if (game.Player) rig.SetTarget(game.Player.transform);
            Destroy(this);
        }
    }
}
