using MadMax.Building;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Construction machine behaviour, driven with keys 1 / 2 / 3 while seated:
    /// Excavator / Backhoe: 1 dig (hold), 2 dump (into a truck bed or container nearby, else on the ground).
    /// Dozer: 1 blade down / up, 3 cut depth. Dump truck: 2 tip / lower the bed (unloads behind). Paver: 1 paving on/off,
    /// 3 asphalt / concrete (from its hopper). Roller: compacts wet paving while driving.
    /// Terrain edits go through <see cref="DeformableTerrain.ApplyTerraform"/> and are replicated.</summary>
    public class Machine : MonoBehaviour
    {
        public enum Kind { Excavator, Backhoe, Dozer, DumpTruck, Paver, Roller }
        public Kind kind;
        public float load;                      // m³ of soil in the bucket / blade pile
        public ResourceType loadType = ResourceType.Sand;
        public bool active, concrete;
        public int cutDepth;
        public const float UnitsPerM3 = 10f;

        VehicleDriver driver;
        MountSocket toolSocket;
        Container store;
        Quaternion toolRest; bool haveRest;
        float pose, tick, travel;
        float boom = -18f, slew;                // excavator/backhoe arm: degrees (+ = down), turret swing
        Vector3 lastPos;

        public string Status { get; private set; }
        public float Capacity => kind == Kind.Excavator ? 1.4f : kind == Kind.Backhoe ? 0.8f : kind == Kind.Dozer ? 2.5f : 0f;
        public Container Store => store;

        void Awake()
        {
            driver = GetComponent<VehicleDriver>();
            foreach (var s in GetComponentsInChildren<MountSocket>()) if (s.name == "tool") toolSocket = s;
            if (kind == Kind.DumpTruck || kind == Kind.Paver)
            {
                store = gameObject.AddComponent<Container>();
                store.title = kind == Kind.DumpTruck ? "TIPPER BED" : "PAVER HOPPER";
                store.capacity = kind == Kind.DumpTruck ? 12000f : 4000f;
            }
            lastPos = transform.position;
        }

        Transform Tool => toolSocket && toolSocket.Current ? toolSocket.Current.transform : null;
        bool Simulated { get { var rep = GetComponent<MadMax.Net.NetReplica>(); return !(rep && rep.active); } }

        static float Terraform(DeformableTerrain.TerraOp op, Vector3 p, float r, float amount, byte extra = 0)
        {
            var t = DeformableTerrain.Instance;
            if (!t) return 0f;
            float moved = t.ApplyTerraform((byte)op, p, r, amount, extra);
            MadMax.Net.NetSession.Instance?.SendTerraform((byte)op, p, r, amount, extra);
            return moved;
        }

        /// <summary>Operator input (called by the game each frame while this machine is driven).
        /// Excavator/backhoe: hold 1 lower (digs on ground contact), hold 2 raise, 3 dump, Shift+1/2 swing the arm (excavator).</summary>
        public void Control(bool hold1, bool hold2, bool press1, bool press2, bool press3, bool shift)
        {
            var terrain = DeformableTerrain.Instance;
            if (!terrain) return;
            float dt = Time.deltaTime;
            switch (kind)
            {
                case Kind.Excavator:
                case Kind.Backhoe:
                {
                    bool ex = kind == Kind.Excavator;
                    if (shift && ex) slew = Mathf.Clamp(slew + ((hold2 ? 1f : 0f) - (hold1 ? 1f : 0f)) * 35f * dt, -150f, 150f);
                    else boom = Mathf.Clamp(boom + ((hold1 ? 1f : 0f) - (hold2 ? 1f : 0f)) * 30f * dt, ex ? -45f : -35f, ex ? 35f : 25f);
                    var tip = BucketPoint();
                    float ground = terrain.Height(tip.x, tip.z);
                    bool reach = tip.y - ground < 0.45f;                  // bucket touching (or in) the ground
                    if (hold1 && !shift && load < Capacity && reach && (tick -= dt) <= 0f)
                    {
                        tick = 0.25f;
                        var at = new Vector3(tip.x, ground, tip.z);
                        float depth = terrain.DugDepth(tip.x, tip.z);
                        if (kind == Kind.Backhoe && depth > 2f) { Status = "TOO DEEP FOR THE LOADER"; break; }
                        float moved = Terraform(DeformableTerrain.TerraOp.Dig, at, kind == Kind.Excavator ? 1.0f : 1.3f, kind == Kind.Excavator ? 0.14f : 0.08f);
                        if (moved > 0f)
                        {
                            loadType = terrain.SoilAt(tip.x, tip.z, depth);
                            load = Mathf.Min(Capacity, load + moved);
                            Fx.Smoke(at + Vector3.up * 0.3f, Vector3.up * 0.8f + Random.insideUnitSphere * 0.5f, 0.6f, DustColor(loadType), 1.5f);
                        }
                    }
                    if (press3 && load > 0.01f) Dump(tip);
                    Status = (ex ? "EXCAVATOR" : "LOADER") + "  BUCKET " + Mathf.RoundToInt(load / Capacity * 100) + "% " + ResourceInfo.Name(loadType) +
                             "  [1] LOWER/DIG  [2] RAISE  [3] DUMP" + (ex ? "  [SHIFT+1/2] SWING" : "") + (reach ? "  (ON GROUND)" : "");
                    break;
                }
                case Kind.Dozer:
                    if (press1) active = !active;
                    if (press3) cutDepth = (cutDepth + 1) % 3;
                    pose = Mathf.MoveTowards(pose, active ? 1f : 0f, dt * 2f);
                    Status = "BULLDOZER  BLADE " + (active ? "DOWN" : "UP") + "  CUT " + (cutDepth * 0.2f).ToString("0.0") + " M  PILE " + load.ToString("0.0") + " M3  [1] BLADE  [3] DEPTH";
                    break;
                case Kind.DumpTruck:
                    if (press2) active = !active;
                    if (press1) active = false;
                    pose = Mathf.MoveTowards(pose, active ? 1f : 0f, dt * 0.5f);
                    Status = "TIPPER  " + Mathf.RoundToInt(store.Weight) + " KG  [2] " + (active ? "LOWER" : "TIP") + " BED  [E] LOAD FROM OUTSIDE";
                    break;
                case Kind.Paver:
                    if (press1) active = !active;
                    if (press3) concrete = !concrete;
                    var mat = concrete ? ResourceType.Concrete : ResourceType.Asphalt;
                    Status = "PAVER  " + (active ? "PAVING " : "IDLE ") + ResourceInfo.Name(mat) + " " + store.inventory.Get(mat) + "  [1] ON/OFF  [3] MATERIAL";
                    break;
                case Kind.Roller:
                    Status = "ROLLER  COMPACTS WET PAVING";
                    break;
            }
        }

        static Color DustColor(ResourceType t) => ((Color)ResourceInfo.Color(t)) * new Color(1f, 1f, 1f, 0.7f);

        Vector3 BucketPoint()
        {
            var tool = Tool;
            if (!tool) return transform.position + transform.forward * 4f;
            return kind == Kind.Excavator ? tool.TransformPoint(new Vector3(0, -12, 43) * 0.08f) : tool.TransformPoint(new Vector3(0, -10, 18) * 0.08f);
        }

        /// <summary>Empty the bucket: into a dump truck bed / container in reach, else onto the ground.</summary>
        void Dump(Vector3 tip)
        {
            int units = Mathf.RoundToInt(load * UnitsPerM3);
            Container target = null; float best = kind == Kind.Excavator ? 7f : 4f;
            foreach (var c in Container.All)
            {
                if (!c || c == store) continue;
                float d = Vector3.Distance(c.transform.position, tip);
                if (d < best && c.Weight + units * ItemCatalog.ResourceWeight(loadType) <= c.capacity) { best = d; target = c; }
            }
            if (target && units > 0) target.inventory.Add(loadType, units);
            else
            {
                var t = DeformableTerrain.Instance;
                var at = new Vector3(tip.x, t.Height(tip.x, tip.z), tip.z);
                Terraform(DeformableTerrain.TerraOp.Dump, at, 1.2f, load / (Mathf.PI * 1.2f * 1.2f * 0.45f));
            }
            for (int i = 0; i < 8; i++) Fx.Smoke(tip + Random.insideUnitSphere * 0.6f, Vector3.down + Random.insideUnitSphere, 0.5f, DustColor(loadType), 1.2f);
            load = 0f;
        }

        void FixedUpdate()
        {
            if (!Simulated) return;
            var terrain = DeformableTerrain.Instance;
            if (!terrain) return;
            float dt = Time.fixedDeltaTime;
            float moved = Vector3.Distance(transform.position, lastPos);
            lastPos = transform.position;
            travel += moved;
            float speed = driver ? Mathf.Abs(driver.ForwardSpeed) : 0f;
            switch (kind)
            {
                case Kind.Dozer:
                    if (!active || speed < 0.3f || travel < 0.25f || !Tool) break;
                    travel = 0f;
                    {
                        var blade = Tool.TransformPoint(new Vector3(0, 0, 2) * 0.08f);
                        float target = terrain.Height(blade.x, blade.z) - cutDepth * 0.2f;
                        float cut = Terraform(DeformableTerrain.TerraOp.Flatten, blade, 1.6f, Mathf.Min(target, blade.y));
                        if (cut > 0f) load += cut;
                        if (load > 1.2f)                                            // spill the pile ahead of the blade
                        {
                            var ahead = blade + transform.forward * 1.6f;
                            Terraform(DeformableTerrain.TerraOp.Dump, new Vector3(ahead.x, terrain.Height(ahead.x, ahead.z), ahead.z), 1.3f, load / (Mathf.PI * 1.69f * 0.45f));
                            load = 0f;
                        }
                        Fx.Smoke(blade + Vector3.up * 0.4f, transform.forward + Vector3.up, 0.8f, DustColor(terrain.SoilAt(blade.x, blade.z, 0f)), 1.2f);
                    }
                    break;
                case Kind.DumpTruck:
                    if (pose < 0.7f || (tick -= dt) > 0f) break;
                    tick = 0.5f;
                    Unload(terrain);
                    break;
                case Kind.Paver:
                    if (!active || speed < 0.15f || speed > 4f || travel < 0.45f || !Tool) break;
                    travel = 0f;
                    {
                        var mat = concrete ? ResourceType.Concrete : ResourceType.Asphalt;
                        if (!store.inventory.TrySpend(mat, 1)) { active = false; break; }
                        var screed = Tool.TransformPoint(new Vector3(0, 0, -4) * 0.08f);
                        Terraform(DeformableTerrain.TerraOp.Pave, screed, 1.55f, 0f, (byte)(concrete ? 2 : 1));
                        Fx.Smoke(screed + Vector3.up * 0.3f, Vector3.up * 0.6f, 0.5f, new Color(0.35f, 0.33f, 0.32f, 0.5f), 1.5f);
                    }
                    break;
                case Kind.Roller:
                    if (speed < 0.2f || travel < 0.3f) break;
                    travel = 0f;
                    foreach (int z in new[] { 20, -20 })
                    {
                        var drum = transform.TransformPoint(new Vector3(0, 0, z) * 0.08f);
                        Terraform(DeformableTerrain.TerraOp.Roll, new Vector3(drum.x, terrain.Height(drum.x, drum.z), drum.z), 1.3f, 0f);
                    }
                    break;
            }
        }

        void Unload(DeformableTerrain terrain)
        {
            var inv = store.inventory;
            ResourceType pick = ResourceType.None;
            for (int t = 1; t < ResourceInfo.Count; t++) if (inv.Get((ResourceType)t) > 0 && !ResourceInfo.IsFluid((ResourceType)t)) { pick = (ResourceType)t; break; }
            if (pick == ResourceType.None) return;
            int n = Mathf.Min(10, inv.Get(pick));
            inv.TrySpend(pick, n);
            var behind = transform.TransformPoint(new Vector3(0, 0, -56) * 0.08f);
            Container target = null; float best = 4f;
            foreach (var c in Container.All)
            {
                if (!c || c == store || c.GetComponentInParent<VehicleDriver>()) continue;
                float d = Vector3.Distance(c.transform.position, behind);
                if (d < best) { best = d; target = c; }
            }
            if (target) target.inventory.Add(pick, n);
            else Terraform(DeformableTerrain.TerraOp.Dump, new Vector3(behind.x, terrain.Height(behind.x, behind.z), behind.z), 1.4f, n / UnitsPerM3 / (Mathf.PI * 1.96f * 0.45f));
            Fx.Smoke(behind + Vector3.up * 0.5f, Vector3.down, 0.9f, DustColor(pick), 1.5f);
        }

        void LateUpdate()
        {
            var tool = Tool;
            if (!tool) return;
            if (!haveRest) { toolRest = tool.localRotation; haveRest = true; }
            if (kind == Kind.Excavator || kind == Kind.Backhoe)
            {
                tool.localRotation = Quaternion.Euler(0f, slew, 0f) * toolRest * Quaternion.Euler(boom, 0f, 0f);
                return;
            }
            float angle = kind switch
            {
                Kind.Dozer => pose * 3f,
                Kind.DumpTruck => -pose * 48f,
                _ => 0f
            };
            tool.localRotation = toolRest * Quaternion.Euler(angle, 0f, 0f);
        }
    }
}
