using MadMax.Designs;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game
{
    /// <summary>Cinematic title: live in-game shots (convoy flyover, a car tearing down the road, the machine yard, a
    /// climb into the darkening sky), then the Moon (user additions): Mad Mike's scrap rocket crash-landed in a fresh
    /// crater, the flickering neon sign planted in the regolith, the car doing a dusty, sparking burnout and the planet
    /// hanging in the black sky (<see cref="MoonArt"/>) — the slanted 3D arcade logo pops in and the main menu fades in
    /// over it. No HUD. Any key skips to the finale.</summary>
    public class TitleSequence : MonoBehaviour
    {
        public static bool Playing { get; private set; }
        public static TitleSequence Instance { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { Playing = false; Instance = null; SkipToFinale = false; OnMoon = false; SpaceFade = 0f; }
        /// <summary>The finale plays on the Moon: sky, weather, clouds and ambience stand down.</summary>
        public static bool OnMoon { get; private set; }
        /// <summary>0..1 while the camera climbs out of the sky before the Moon (stars come out past half).</summary>
        public static float SpaceFade { get; private set; }
        /// <summary>The pre-rendered intro already played in the boot scene: start straight at the live finale (menu).</summary>
        public static bool SkipToFinale;

        // a scene reload (new game, load) destroys the sequence without Finish(): never leave the HUD hidden
        void OnDestroy() { if (Instance == this) { Playing = false; Instance = null; OnMoon = false; SpaceFade = 0f; } }

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
        // the lunar set, far above the start (beyond every camera's far plane from the ground)
        const float MoonAltitude = 2600f, MoonCurve = 0.5f / 700f;
        GameObject moonSet, earth, earthClouds;
        Material moonMat, earthMat;
        Mesh[] moonMeshes;
        System.Threading.Tasks.Task<MoonArt.SurfaceData> surfaceJob;
        Quaternion setRot;
        Light sunLight;
        float sunIntensity = 1f;
        Color skyColour;
        static readonly int CurveId = Shader.PropertyToID("_MadMaxCurve"), CloudsId = Shader.PropertyToID("_MadMaxClouds"), NightId = Shader.PropertyToID("_MadMaxNight");

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
            skyColour = cam.backgroundColor;
            var dn = FindAnyObjectByType<DayNight>();
            sunLight = dn ? dn.sun : RenderSettings.sun;
            if (sunLight) sunIntensity = dn && dn.SunBase > 0f ? dn.SunBase : Mathf.Max(sunLight.intensity, 1f);
            MoonArt.Prepare();
            surfaceJob = System.Threading.Tasks.Task.Run(MoonArt.BuildSurface);                // the regolith heightfield, ready by the finale
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
                if (car.TryGetComponent<VehicleSystems>(out var ign)) ign.ForceStart();          // the film opens with the engine running
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

        void LateUpdate()
        {
            if (!Playing || !cam) return;
            cam.orthographic = false;
            if (!OnMoon) return;
            // space: black sky, no haze, a small world's sharp horizon, no clouds; a hard low sun and faint earthshine
            cam.backgroundColor = new Color(0.004f, 0.004f, 0.009f);
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 420f;
            RenderSettings.fog = false;
            Shader.SetGlobalFloat(CurveId, MoonCurve);
            Shader.SetGlobalVector(CloudsId, Vector4.zero);
            Shader.SetGlobalFloat(NightId, 0f);
            RenderSettings.ambientLight = new Color(0.09f, 0.1f, 0.14f);
            if (sunLight)
            {
                sunLight.transform.rotation = setRot * Quaternion.LookRotation(new Vector3(0.55f, -0.24f, 0.8f));
                sunLight.intensity = sunIntensity * 1.2f;
                sunLight.color = new Color(1f, 0.98f, 0.94f);
            }
            PlaceEarth();
        }

        /// <summary>The planet hangs in the upper left of the frame (it rides with the camera's heading, so the swaying
        /// push-in never slides it off screen or behind the menu), spinning under its clouds.</summary>
        void PlaceEarth()
        {
            if (!earth) return;
            var heading = Vector3.ProjectOnPlane(ct.forward, Vector3.up);
            var eDir = Quaternion.LookRotation(heading.sqrMagnitude > 1e-4f ? heading : side) * new Vector3(-0.46f, 0.24f, 1f).normalized;
            var ep = ct.position + eDir * 80f;
            float dx = ep.x - ct.position.x, dz = ep.z - ct.position.z;
            ep.y += (dx * dx + dz * dz) * MoonCurve;                                         // the shader's horizon curve would sink it
            float spin = (t - FinaleAt) * 4f;
            var tilt = setRot * Quaternion.Euler(-24f, 0f, 14f);
            earth.transform.SetPositionAndRotation(ep, tilt * Quaternion.Euler(0f, 180f - spin, 0f));
            earth.transform.localScale = Vector3.one * 8.5f;
            if (earthClouds) { earthClouds.transform.SetPositionAndRotation(ep, tilt * Quaternion.Euler(0f, 150f - spin * 1.25f, 0f)); earthClouds.transform.localScale = Vector3.one * 8.5f; }
        }


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
                // crane up from behind the braking car, tilting into a sky that fades to black and fills with stars:
                // off to the Moon
                if (car) Drive(0f, 1f, false);
                float k = Smooth((t - 11f) / 4f), up = Smooth((t - 12.3f) / 2.5f);
                var c = car ? car.transform.position : start;
                var fwd = car ? Vector3.ProjectOnPlane(car.transform.forward, Vector3.up).normalized : dir;
                var pos = c - fwd * 9f + Vector3.Cross(Vector3.up, fwd) * 3.5f + Vector3.up * (2.6f + k * k * 42f);
                var look = Quaternion.LookRotation(c + fwd * 5f + Vector3.up * 0.6f - pos);
                cam.fieldOfView = 50f;
                ct.SetPositionAndRotation(pos, Quaternion.Slerp(look, Quaternion.LookRotation(Vector3.up * 4f + fwd), up));
                SpaceFade = up;
                cam.backgroundColor = Color.Lerp(skyColour, Color.black, up);
                RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, Color.black, up * 0.5f);
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
            BuildMoon();
            var faceCam = -side;                                            // the sign faces back towards the camera
            var signPos = OnSet(0f, 0f);
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
            // the car: side-on in front of the sign on the flat stage, locked up for a burnout
            carSpot = OnSet(0f, -6.5f) + Vector3.up * 0.7f;
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

        Vector3 OnSet(float x, float z) => moonSet.transform.TransformPoint(new Vector3(x, MoonArt.Height(x, z), z));

        readonly System.Collections.Generic.List<Material> moonMats = new System.Collections.Generic.List<Material>();
        bool fogWas;
        float nearWas, farWas;

        /// <summary>Airless light: shadows go cold and near-black (the desert's warm shadow tint would read maroon).</summary>
        Material MoonMaterial(bool outline, bool space)
        {
            var m = new Material(game.propMaterial);
            if (!outline) m.SetFloat("_OutlinePx", 0f);
            if (space) m.SetFloat("_NoFog", 1f);
            m.SetFloat("_SnowMask", 0f);
            m.SetColor("_ShadowTint", space ? new Color(0.03f, 0.04f, 0.09f) : new Color(0.15f, 0.16f, 0.22f));
            m.SetColor("_Ambient", space ? new Color(0.01f, 0.012f, 0.02f) : new Color(0.03f, 0.034f, 0.05f));
            moonMats.Add(m);
            return m;
        }

        GameObject Piece(Mesh m, string name, Transform parent, Vector3 at, Quaternion rot, Material mat, bool shadows = true)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            if (parent) go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(at, rot);
            go.GetComponent<MeshFilter>().sharedMesh = m;
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = mat;
            if (!shadows) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
            return go;
        }

        /// <summary>The lunar set, high above the start: regolith, boulders, the scrap rocket, the old lander, the flat
        /// stage (a deck the car's wheels stand on), and the planet with its clouds.</summary>
        void BuildMoon()
        {
            OnMoon = true;
            fogWas = RenderSettings.fog; nearWas = cam.nearClipPlane; farWas = cam.farClipPlane;
            DayNight.SetHours(12f);
            setRot = Quaternion.LookRotation(side);
            moonSet = new GameObject("MoonSet");
            moonSet.transform.SetPositionAndRotation(Ground(stage) + Vector3.up * MoonAltitude, setRot);
            var set = moonSet.transform;
            Mesh earthMesh = MoonArt.Earth(game.World, out var cloudMesh);
            var ground = surfaceJob != null ? surfaceJob.Result : null;                     // waits only if skipped straight here
            moonMeshes = new[] { MoonArt.Surface(ground), MoonArt.Rocks(), MoonArt.Rocket(), MoonArt.Lander(), earthMesh, cloudMesh };
            var props = MoonMaterial(true, false);
            Piece(moonMeshes[0], "Regolith", set, Vector3.zero, Quaternion.identity, MoonMaterial(false, false));
            Piece(moonMeshes[1], "Boulders", set, Vector3.zero, Quaternion.identity, props);
            var r = MoonArt.RocketAt;
            Piece(moonMeshes[2], "ScrapRocket", set, new Vector3(r.x, MoonArt.Height(r.x, r.z) + r.y, r.z), MoonArt.RocketTilt, props);
            var l = MoonArt.LanderAt;
            Piece(moonMeshes[3], "OldLander", set, new Vector3(l.x, MoonArt.Height(l.x, l.z) - 0.05f, l.z), Quaternion.Euler(0f, 30f, 2f), props);
            // the stage carries the car: wheels sample decks after the terrain (which lies far below)
            var deck = new GameObject("Stage", typeof(BoxCollider));
            deck.transform.SetParent(set, false);
            float hz = (MoonArt.StageZ1 - MoonArt.StageZ0) * 0.5f;
            deck.transform.localPosition = new Vector3(0f, 0f, MoonArt.StageZ0 + hz);
            var bc = deck.GetComponent<BoxCollider>(); bc.size = new Vector3(MoonArt.StageX * 2f, 0.2f, hz * 2f); bc.center = new Vector3(0f, -0.1f, 0f);
            MadMax.Building.StructureGround.AddDeck(moonSet, deck.transform, bc, new Vector4(MoonArt.StageX, hz, 0f, 0f));
            // the planet: this world's continents and seas, lit by the same sun (a gibbous Earth)
            var em = MoonMaterial(false, true);
            earth = Piece(earthMesh, "Earth", null, Vector3.zero, Quaternion.identity, em, false);
            earthClouds = Piece(cloudMesh, "EarthClouds", null, Vector3.zero, Quaternion.identity, em, false);
            PlaceEarth();
        }

        void ClearMoon()
        {
            SpaceFade = 0f;
            if (!OnMoon && !moonSet) return;
            OnMoon = false;
            if (moonSet) { MadMax.Building.StructureGround.Remove(moonSet); Destroy(moonSet); }
            if (earth) Destroy(earth);
            if (earthClouds) Destroy(earthClouds);
            if (moonMeshes != null) foreach (var m in moonMeshes) if (m) Destroy(m);
            moonMeshes = null;
            foreach (var m in moonMats) if (m) Destroy(m);
            moonMats.Clear();
            if (cam) { cam.backgroundColor = skyColour; cam.nearClipPlane = nearWas; cam.farClipPlane = farWas; }
            RenderSettings.fog = fogWas;
        }

        /// <summary>Back to the first shot (used when recording the boot film).</summary>
        public void Restart()
        {
            t = 0f; finale = false;
            if (sign) Destroy(sign); if (logo) Destroy(logo);
            ClearMoon();
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
            DayNight.SetHours(12f);                                        // the lunar day holds still
            cam.fieldOfView = 46f;
            // slow push-in, low angle, sign and car framed with the logo above and the planet in the sky
            var target = carSpot + Vector3.up * 1.6f - side * 0.5f + dir * 2.8f;     // car sits right of centre, menu on the left
            var back = -side * Mathf.Lerp(17f, 12.5f, Smooth(ft / 8f));
            var pos = carSpot + back + dir * (2.5f + Mathf.Sin(ft * 0.15f) * 1.2f) + Vector3.up * 1.4f;
            ct.SetPositionAndRotation(pos, Quaternion.LookRotation(target - pos));
            // neon: stutter on, then hum with the odd flicker
            bool lit = ft > 1.2f ? Mathf.PerlinNoise(t * 7f, 3.3f) > 0.07f : (Mathf.Repeat(ft * 9f, 1f) > 0.55f && ft > 0.3f);
            if (neon) neon.enabled = lit;
            if (pinkLight) pinkLight.enabled = lit;
            if (cyanLight) cyanLight.enabled = lit;
            // burnout: throttle + brake (line lock); no air to burn, so grey regolith dust and sparks off the rims
            if (car)
            {
                Drive(1f, 1f, false);
                if (Vector3.Distance(car.transform.position, carSpot) > 1.5f) { car.Body.position = Vector3.Lerp(car.Body.position, carSpot, 0.1f); }
                var rear = car.transform.TransformPoint(new Vector3(0f, 0.25f, -1.5f));
                for (int s = -1; s <= 1; s += 2)
                {
                    var wheel = rear + car.transform.right * s * 0.8f;
                    if (Random.value < 0.4f) Fx.Smoke(wheel, -car.transform.forward * 3.4f + Vector3.up * 0.5f + Random.insideUnitSphere * 0.6f, Random.Range(0.5f, 1.1f), new Color(0.6f, 0.58f, 0.55f, 0.7f), 1.4f);   // dust sprays low and settles
                    if (ft > 2.5f && Random.value < 0.3f) Fx.Sparks(wheel, -car.transform.forward + Vector3.up * 1.5f, 3, new Color(1f, 0.8f, 0.45f));
                    else if (Random.value < 0.2f) Fx.Sparks(wheel, -car.transform.forward + Vector3.up, 2, new Color(1f, 0.75f, 0.35f));
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
            ClearMoon();
            DayNight.SetHours(savedHours);
            rig.enabled = true;
            var fade = rig.GetComponent<OccluderFade>(); if (fade) fade.enabled = true;
            if (game.Current) rig.SetTarget(game.Current.transform);
            else if (game.Player) rig.SetTarget(game.Player.transform);
            Destroy(this);
        }
    }
}
