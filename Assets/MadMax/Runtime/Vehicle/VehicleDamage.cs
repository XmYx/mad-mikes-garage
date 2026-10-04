using System;
using MadMax.Items;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Crash model. Collision impulses dent the body and mounted panels, damage parts (loose panels fall off,
    /// engines lose torque, hit wheels drag and wobble) and bend the frame (steering pull).</summary>
    [RequireComponent(typeof(VehicleDriver))]
    public class VehicleDamage : MonoBehaviour, IDamageable, ISalvageable
    {
        [Header("Impact")]
        [Tooltip("Velocity change (m/s) below which a hit does nothing.")]
        public float minImpactSpeed = 2.5f;
        public float cooldown = 0.06f;

        [Header("Dents")]
        public float dentPerMs = 0.045f;
        public float maxDent = 0.28f;
        public float maxDisplacement = 0.32f;

        [Header("Damage")]
        public float partDamagePerMs = 0.12f;
        public float wheelDamagePerMs = 0.1f;
        public float frameDamagePerMs = 0.015f;
        public float maxSteerPull = 6f;

        public float FrameDamage { get; private set; }

        [Header("Salvage")]
        [Tooltip("Scrap units left to strip; -1 = derive from body mass.")]
        public float salvagePool = -1f;
        /// <summary>Raised just before a fully stripped vehicle is removed.</summary>
        public static event Action<VehicleDriver> Scrapped;
        static readonly System.Collections.Generic.List<MadMax.World.DebrisSystem.Chunk> salvageChunks = new System.Collections.Generic.List<MadMax.World.DebrisSystem.Chunk>();
        /// <summary>Raised with the effective impact speed (m/s above threshold).</summary>
        public event Action<float, Vector3> Impact;

        VehicleDriver driver;
        VehicleChassis chassis;
        VehicleArmor armour;
        VehicleBreakables breakables;
        Rigidbody rb;
        float nextScrape, lastScrape = -9f;
        float lastHit, pullSign = 1f;

        void Awake()
        {
            driver = GetComponent<VehicleDriver>();
            chassis = GetComponent<VehicleChassis>();
            rb = GetComponent<Rigidbody>();
            if (!TryGetComponent(out armour) && transform.Find("Body")) armour = gameObject.AddComponent<VehicleArmor>();
            if (!GetComponent<VehicleTuning>() && driver && driver.driveable) gameObject.AddComponent<VehicleTuning>();
            if (!GetComponent<VehicleGrime>() && transform.Find("Body")) gameObject.AddComponent<VehicleGrime>();
            if (!TryGetComponent(out breakables) && transform.Find("Body")) breakables = gameObject.AddComponent<VehicleBreakables>();
            if (!GetComponent<VehicleBurn>()) gameObject.AddComponent<VehicleBurn>();
            if (!GetComponent<VehicleIgnition>() && driver && driver.driveable) gameObject.AddComponent<VehicleIgnition>();
            PassengerSeat.For(driver);
            VehicleStorage.For(driver);                                                  // trunk, glovebox, back seat...
        }

        void OnCollisionEnter(Collision c) => Handle(c);

        /// <summary>Velocity at the start of the last physics step, before its contacts: what an occupant thrown out in
        /// a crash keeps going with.</summary>
        public Vector3 PreImpactVelocity { get; private set; }

        void FixedUpdate() { if (rb && driver && driver.Occupied) PreImpactVelocity = rb.linearVelocity; }

        /// <summary>Scraping along another vehicle, a wall or a rock: sparks (dust off stone, splinters off wood), a
        /// grinding loop, and paint worn to bare metal where the body rubs. Ground contacts under the wheels don't count.</summary>
        void OnCollisionStay(Collision c)
        {
            if (Time.time < nextScrape || c.contactCount == 0 || Time.time < graceUntil) return;
            var cp = c.GetContact(0);
            if (c.collider is MeshCollider && !c.rigidbody && cp.normal.y > 0.6f) return;          // resting on terrain / a deck
            var slide = Vector3.ProjectOnPlane(c.relativeVelocity, cp.normal);
            float speed = slide.magnitude;
            if (speed < 1.2f) return;
            nextScrape = Time.time + 0.07f;
            lastScrape = Time.time;
            bool metal = c.rigidbody && c.rigidbody.GetComponent<VehicleDriver>() || c.collider.GetComponentInParent<VehiclePart>();
            var prop = c.collider.GetComponentInParent<MadMax.World.DestructibleVoxels>();
            bool wood = !metal && (c.collider.name.Contains("Wood") || c.collider.name.Contains("Tree") || c.collider.name.Contains("Fence"));
            var along = -slide.normalized * Mathf.Min(6f, speed * 0.5f) + cp.normal * 0.8f;
            if (metal || !wood) MadMax.World.Fx.Sparks(cp.point, along, Mathf.Clamp(Mathf.RoundToInt(speed / 3f), 1, 5), new Color(1f, 0.72f, 0.3f));
            if (!metal && MadMax.World.DebrisSystem.Instance)
                MadMax.World.DebrisSystem.Instance.EmitPuff(cp.point, wood ? MadMax.Voxel.Pal.Wood[2] : prop ? MadMax.Voxel.Pal.Cream[1] : MadMax.Voxel.Pal.Metal[3], 0.05f, along * 0.5f + Vector3.up, 0.6f);
            if (breakables) breakables.Scrape(cp.point, cp.normal, Mathf.Clamp(0.12f + speed * 0.012f, 0.12f, 0.3f));
            MadMax.Audio.Sfx.Loop(this, "scratch", Mathf.Clamp01(speed / 10f) * 0.8f, 0.8f + Mathf.Min(0.5f, speed * 0.03f), 40f);
        }

        void Update()
        {
            if (lastScrape > 0f && Time.time - lastScrape > 0.15f) { lastScrape = -9f; MadMax.Audio.Sfx.Loop(this, "scratch", 0f, 1f, 40f); }
        }

        /// <summary>True while this vehicle is sliding against something (sparks, the grinding loop).</summary>
        public bool Scraping => Time.time - lastScrape < 0.2f;

        /// <summary>Dent depth multiplier from the CAR DEFORMATION setting (0 = no visible dents).</summary>
        public static float DeformationScale => MadMax.Game.GameSettings.Current.DeformationScale;

        /// <summary>Settling grace: no damage from contacts until then (spawned or woken wrecks dropping into place,
        /// depenetration shoves).</summary>
        [System.NonSerialized] public float graceUntil;

        void Handle(Collision c)
        {
            if (Time.time - lastHit < cooldown || c.contactCount == 0 || Time.time < graceUntil) return;
            float dv = c.impulse.magnitude / rb.mass;
            if (dv < minImpactSpeed) return;

            Vector3 point = Vector3.zero, normal = Vector3.zero;
            for (int i = 0; i < c.contactCount; i++) { var cp = c.GetContact(i); point += cp.point; normal += cp.normal; }
            point /= c.contactCount;
            normal = normal.normalized;
            // landing on terrain is suspension's job, not a crash
            if (c.collider is MeshCollider && !c.rigidbody && normal.y > 0.6f) return;
            lastHit = Time.time;

            float s = dv - minImpactSpeed;
            MadMax.Audio.Sfx.Play(s > 6f ? "crash_big" : "crash_small", point, Mathf.Clamp01(0.35f + s * 0.08f), UnityEngine.Random.Range(0.9f, 1.1f), 60f, 0.15f);
            // armour near the impact soaks part of it; spikes and rams hit back
            var shield = ShieldAt(point, out var armor);
            if (armour) s *= 1f - armour.Soak(point, s, false);                             // welded-on plates first
            if (shield)
            {
                shield.damage += s * partDamagePerMs * 0.5f / shield.sizeClass * shield.Toughness;
                s *= 1f - armor.absorb;
                if (c.rigidbody && (armor.spikes > 0f || armor.ram > 0f) && c.rigidbody.TryGetComponent<VehicleDamage>(out var victim))
                    victim.ApplyHit(point, -normal, (armor.spikes * 1.5f + armor.ram) * dv * 0.15f, 0.5f, gameObject);
                if (shield.damage >= 1f) { foreach (var sk in chassis.Sockets) if (sk.Current == shield) { Break(sk, normal); break; } }
            }
            float rawDepth = Mathf.Min(maxDent, s * dentPerMs);                                // what peers receive: each applies its own setting
            float depth = rawDepth * DeformationScale;
            float radius = Mathf.Clamp(0.35f + s * 0.06f, 0.35f, 1.1f);

            foreach (var mf in GetComponentsInChildren<MeshFilter>())
            {
                if (mf.name == "Driver" || mf.name == "Dashboard" || MadMax.Rendering.HDModel.IsLod(mf.transform) || MadMax.Rendering.HDModel.IsBelt(mf.transform) || mf.GetComponent<MadMax.Building.Placeable>()) continue;
                var part = mf.GetComponentInParent<VehiclePart>();
                if (part && part.category == PartCategory.Wheel) continue;
                if (!mf.TryGetComponent<DeformableMesh>(out var dm)) dm = mf.gameObject.AddComponent<DeformableMesh>();
                if (depth > 0.001f) dm.Dent(point, normal, depth, radius, maxDisplacement * Mathf.Max(1f, DeformationScale));
            }
            if (breakables && s > 2f) breakables.Smash(point, radius * 0.8f, s);

            // parts near the impact
            foreach (var socket in chassis.Sockets)
            {
                var part = socket.Current;
                if (!part) continue;
                var r = part.GetComponent<Renderer>();
                if (!r) continue;
                float d = Mathf.Sqrt(r.bounds.SqrDistance(point));
                if (part.category == PartCategory.Wheel)
                {
                    if (Vector3.Distance(r.bounds.center, point) > part.radius + 0.3f) continue;
                    part.damage += s * wheelDamagePerMs * part.Toughness;           // bent rim / rubbing tyre: drag + wobble
                }
                else
                {
                    if (d > radius) continue;
                    part.damage += s * partDamagePerMs * (1f - d / radius) / part.sizeClass * part.Toughness;
                }
                if (part.damage >= 1f && part.category != PartCategory.Engine) Break(socket, normal);
            }

            FrameDamage = Mathf.Min(1f, FrameDamage + s * frameDamagePerMs);
            if (FrameDamage > 0f && Mathf.Abs(transform.InverseTransformPoint(point).x) > 0.3f)
                pullSign = Mathf.Sign(transform.InverseTransformPoint(point).x);
            driver.steerPull = FrameDamage * maxSteerPull * pullSign;
            MadMax.Net.NetSession.Instance?.SendImpact(driver, point, normal, rawDepth, radius);
            Impact?.Invoke(s, point);
            if (driver && driver.Occupied) MadMax.Game.WastelandGame.Instance?.CrashEject(driver, dv, PreImpactVelocity, rb.linearVelocity);   // unbelted: over the bars / through the screen
        }

        /// <summary>Nearest mounted armour part within 0.9 m of a point.</summary>
        public VehiclePart ShieldAt(Vector3 point, out ArmorStats.Stats stats)
        {
            stats = default;
            VehiclePart best = null; float bd = 0.9f;
            foreach (var socket in chassis.Sockets)
            {
                var part = socket.Current;
                if (!part || !ArmorStats.Get(part.partId, out var st)) continue;
                var r = part.GetComponent<Renderer>();
                if (!r) continue;
                float d = Mathf.Sqrt(r.bounds.SqrDistance(point));
                if (d < bd) { bd = d; best = part; stats = st; }
            }
            return best;
        }

        /// <summary>Carving multiplier for props hit at this point (rams, plows, blades).</summary>
        public float RamFactor(Vector3 point) => ShieldAt(point, out var st) ? Mathf.Max(1f, st.ram) : 1f;

        /// <summary>Tool / explosion hit: dents and damages parts near the point (a hammer can knock panels off).</summary>
        public void ApplyHit(Vector3 point, Vector3 direction, float power, float radius, GameObject source)
        {
            if (armour) power *= 1f - armour.Soak(point, power * 2.5f, true);
            if (power < 0.02f) { Impact?.Invoke(power, point); return; }
            foreach (var mf in GetComponentsInChildren<MeshFilter>())
            {
                if (mf.name == "Driver" || mf.name == "Dashboard" || MadMax.Rendering.HDModel.IsLod(mf.transform) || MadMax.Rendering.HDModel.IsBelt(mf.transform)) continue;
                var p = mf.GetComponentInParent<VehiclePart>();
                if (p && p.category == PartCategory.Wheel) continue;
                if (!mf.TryGetComponent<DeformableMesh>(out var dm)) dm = mf.gameObject.AddComponent<DeformableMesh>();
                if (DeformationScale > 0f) dm.Dent(point, direction, 0.04f * power * DeformationScale, radius + 0.15f, maxDisplacement * Mathf.Max(1f, DeformationScale));
            }
            if (breakables) breakables.Smash(point, radius + 0.15f, power * 3f);                  // a bullet or a club through the glass
            foreach (var socket in chassis.Sockets)
            {
                var part = socket.Current;
                if (!part || !part.TryGetComponent<Renderer>(out var r)) continue;
                if (Mathf.Sqrt(r.bounds.SqrDistance(point)) > radius + 0.1f) continue;
                part.damage += 0.2f * power / part.sizeClass * part.Toughness;
                if (part.damage >= 1f && part.category != PartCategory.Engine) Break(socket, -direction);
            }
            Impact?.Invoke(power, point);
        }

        void Break(MountSocket socket, Vector3 normal)
        {
            var net = MadMax.Net.NetSession.Instance;
            if (net && socket.Current) { socket.Current.netId = net.NewEntityId(); net.SendPartDetached(driver, socket.name, socket.Current, 0); }
            var part = socket.Detach();
            if (!part || !part.TryGetComponent<Rigidbody>(out var body)) return;
            body.linearVelocity = rb.linearVelocity;
            body.AddForce(-normal * 2f + Vector3.up * 1.5f, ForceMode.VelocityChange);
        }

        /// <summary>Dent every mesh of this vehicle around a point (used for pre-damaged wrecks).</summary>
        public void DentAt(Vector3 point, Vector3 direction, float depth, float radius)
        {
            foreach (var mf in GetComponentsInChildren<MeshFilter>())
            {
                if (mf.name == "Driver" || mf.name == "Dashboard" || MadMax.Rendering.HDModel.IsLod(mf.transform) || MadMax.Rendering.HDModel.IsBelt(mf.transform) || mf.GetComponent<MadMax.Building.Placeable>()) continue;
                var p = mf.GetComponentInParent<VehiclePart>();
                if (p && p.category == PartCategory.Wheel) continue;
                if (!mf.TryGetComponent<DeformableMesh>(out var dm)) dm = mf.gameObject.AddComponent<DeformableMesh>();
                if (DeformationScale > 0f) dm.Dent(point, direction, depth * DeformationScale, radius, maxDisplacement * Mathf.Max(1f, DeformationScale));
            }
        }

        public float SalvageLeft => salvagePool < 0f ? chassis.bodyMass / 25f : salvagePool;

        /// <summary>Cut material off the body: scrap (+ rubber near wheels, glass near windows). Stripped bare, the
        /// vehicle falls apart: parts drop loose and the shell is removed.</summary>
        public bool Salvage(Vector3 point, float amount, GameObject source)
        {
            if (driver.Occupied && !MadMax.Net.NetSession.Applying) return false;
            if (salvagePool < 0f) salvagePool = chassis.bodyMass / 25f;
            MadMax.Net.NetSession.Instance?.SendSalvage(driver, point, amount);
            int units = Mathf.Min(Mathf.CeilToInt(amount * 3f), Mathf.CeilToInt(salvagePool));
            salvagePool -= units;
            var pickups = MadMax.Net.NetSession.Applying ? null : MadMax.World.PickupSystem.Instance;   // yields go to the cutter only
            var up = Vector3.up * 2.5f;
            if (pickups)
            {
                pickups.Spawn(ResourceType.Scrap, units, point + up * 0.1f, UnityEngine.Random.insideUnitSphere * 1.5f + up);
                foreach (var s in chassis.Sockets)
                    if (s.Current && s.Current.category == PartCategory.Wheel && Vector3.Distance(s.Current.transform.position, point) < 0.8f)
                    { pickups.Spawn(ResourceType.Rubber, 1, point, up); break; }
                var glass = transform.Find("Body/Glass");
                if (glass && glass.TryGetComponent<Renderer>(out var gr) && gr.bounds.SqrDistance(point) < 0.25f && UnityEngine.Random.value < 0.5f)
                    pickups.Spawn(ResourceType.Glass, 1, point, up);
            }
            DentAt(point, (transform.position + Vector3.up - point).normalized, 0.05f, 0.35f);
            if (MadMax.World.DebrisSystem.Instance)
            {
                salvageChunks.Clear();
                for (int i = 0; i < 8; i++)
                    salvageChunks.Add(new MadMax.World.DebrisSystem.Chunk { position = point + UnityEngine.Random.insideUnitSphere * 0.15f, color = UnityEngine.Random.value < 0.5f ? new Color32(150, 80, 40, 255) : new Color32(90, 90, 96, 255) });
                MadMax.World.DebrisSystem.Instance.Emit(salvageChunks, 0.06f, Vector3.up * 1.5f);
                for (int i = 0; i < 4; i++) MadMax.World.DebrisSystem.Instance.EmitPuff(point, new Color32(255, 220, 120, 255), 0.03f, UnityEngine.Random.insideUnitSphere * 3f, 0.25f); // sparks
            }
            if (salvagePool <= 0f) ScrapVehicle();
            return true;
        }

        void ScrapVehicle()
        {
            int si = 0;
            foreach (var s in chassis.Sockets)
            {
                var p = s.Detach(true);
                if (p) p.netId = 0x80000000u | ((uint)driver.netId << 8) | (uint)(si & 0xFF);   // same id on every peer
                si++;
            }
            if (MadMax.World.DebrisSystem.Instance)
            {
                var mf = transform.Find("Body")?.GetComponent<MeshFilter>();
                if (mf && mf.sharedMesh)
                {
                    var v = mf.sharedMesh.vertices; var c = mf.sharedMesh.colors32;
                    salvageChunks.Clear();
                    for (int i = 0; i < 120; i++) { int k = UnityEngine.Random.Range(0, v.Length); salvageChunks.Add(new MadMax.World.DebrisSystem.Chunk { position = mf.transform.TransformPoint(v[k]), color = c[k] }); }
                    MadMax.World.DebrisSystem.Instance.Emit(salvageChunks, 0.08f, Vector3.up);
                }
            }
            Scrapped?.Invoke(driver);
            Destroy(gameObject);
        }

        public void AddFrameDamage(float amount, float side)
        {
            FrameDamage = Mathf.Clamp01(FrameDamage + amount);
            pullSign = side >= 0 ? 1f : -1f;
            driver.steerPull = FrameDamage * maxSteerPull * pullSign;
        }

        /// <summary>Welder: take some bend out of the frame (less steering pull).</summary>
        public void StraightenFrame(float amount)
        {
            FrameDamage = Mathf.Max(0f, FrameDamage - amount);
            driver.steerPull = FrameDamage * maxSteerPull * pullSign;
        }

        /// <summary>Undo dents and damage on everything still mounted.</summary>
        [ContextMenu("Repair")]
        public void Repair()
        {
            foreach (var dm in GetComponentsInChildren<DeformableMesh>()) dm.Repair();
            foreach (var p in chassis.Parts) p.damage = 0f;
            FrameDamage = 0f;
            driver.steerPull = 0f;
        }
    }
}
