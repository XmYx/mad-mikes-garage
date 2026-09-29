using MadMax.Items;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Fuel tanker pump (Tanker, TankerSmall). Pumps its own tank into a partner vehicle, or drains the partner
    /// into the tank, through a hose. Partner = the vehicle towing it, else the nearest vehicle within
    /// <see cref="Reach"/>. Stops when full/empty or when the hose would stretch too far.</summary>
    [RequireComponent(typeof(VehicleSystems))]
    public class FuelTanker : MonoBehaviour
    {
        public enum Mode { Off, Fill, Drain }

        public float flowLps = 25f;
        public const float Reach = 9f;

        public Mode Pumping { get; private set; }
        public VehicleDriver Partner { get; private set; }
        public VehicleSystems Tank { get; private set; }
        public float Moved { get; private set; }

        LineRenderer hose;
        TowCoupling tow;
        float extent = -1f;

        void Awake() { Tank = GetComponent<VehicleSystems>(); tow = GetComponent<TowCoupling>(); }

        Vector3 Outlet => transform.TransformPoint(0f, 0.7f, -Extent());

        float Extent()
        {
            if (extent < 0f) { var b = new Bounds(transform.position, Vector3.zero); foreach (var r in GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds); extent = b.extents.z; }
            return extent;
        }

        /// <summary>The vehicle the hose would go to: the tow vehicle, else the nearest vehicle with a fuel tank.</summary>
        public VehicleDriver FindPartner()
        {
            if (tow && tow.Tower) return tow.Tower;
            VehicleDriver best = null; float bd = Reach;
            var game = MadMax.Game.WastelandGame.Instance;
            if (!game) return null;
            foreach (var v in game.AllVehicles)
            {
                if (!v || v.gameObject == gameObject || !v.TryGetComponent<VehicleSystems>(out var s) || s.fuelCapacity <= 0f) continue;
                float d = Vector3.Distance(v.transform.position, Outlet);
                if (d < bd) { bd = d; best = v; }
            }
            return best;
        }

        /// <summary>Starts the pump in a mode, or stops it when that mode is already running.</summary>
        public void Toggle(Mode mode)
        {
            if (Pumping == mode || mode == Mode.Off) { if (Pumping != Mode.Off) Stop($"PUMP OFF: {Mathf.RoundToInt(Moved)} L"); return; }
            var p = FindPartner();
            if (!p) { MadMax.Game.WastelandGame.Instance?.Toast("NO VEHICLE WITHIN HOSE REACH"); return; }
            Partner = p; Pumping = mode; Moved = 0f;
        }

        void Stop(string toast)
        {
            if (toast != null) MadMax.Game.WastelandGame.Instance?.Toast(toast);
            var net = MadMax.Net.NetSession.Instance;
            if (net && Partner) net.SendVehicleMeta(Partner);
            if (net) net.SendVehicleMeta(GetComponent<VehicleDriver>());
            Pumping = Mode.Off; Partner = null;
            if (hose) hose.enabled = false;
        }

        void Update()
        {
            if (Pumping == Mode.Off) return;
            if (!Partner || !Partner.TryGetComponent<VehicleSystems>(out var other)) { Stop("PUMP OFF"); return; }
            var inlet = Partner.transform.position + Vector3.up * 0.7f;
            if (Vector3.Distance(Outlet, inlet) > Reach * 1.4f) { Stop("HOSE PULLED OFF"); return; }

            float want = flowLps * Time.deltaTime;
            float moved;
            // one kind of fuel per tank: the hose only runs between tanks that take what flows
            var from = Pumping == Mode.Fill ? Tank : other; var to = Pumping == Mode.Fill ? other : Tank;
            var kind = from.tankKind != ResourceType.None ? from.tankKind : from.FuelKind;
            if (!to.Accepts(kind)) { Stop("WRONG FUEL: " + ResourceInfo.Name(kind) + " INTO " + ResourceInfo.Name(to.tankKind)); return; }
            moved = Mathf.Min(want, from.fuel, to.fuelCapacity - to.fuel);
            from.fuel -= moved;
            if (moved > 0f) to.AddFuel(kind, moved);
            Moved += moved;
            MadMax.Audio.Sfx.Loop(this, "pour", 0.5f, 0.9f, 15f);
            DrawHose(Outlet, inlet);
            if (moved <= 1e-4f)
                Stop((Pumping == Mode.Fill ? (Tank.fuel <= 0.01f ? "TANKER EMPTY" : "TANK FULL") : (other.fuel <= 0.01f ? "DRAINED" : "TANKER FULL")) + $": {Mathf.RoundToInt(Moved)} L");
        }

        void DrawHose(Vector3 a, Vector3 b)
        {
            if (!hose)
            {
                hose = new GameObject("Hose").AddComponent<LineRenderer>();
                hose.transform.SetParent(transform, false);
                hose.sharedMaterial = MadMax.World.Fx.TransparentMaterial(null);
                hose.widthMultiplier = 0.06f; hose.positionCount = 8;
                hose.startColor = hose.endColor = new Color(0.08f, 0.08f, 0.08f, 1f);
            }
            hose.enabled = true;
            float sag = Mathf.Max(0.2f, 1.2f - Vector3.Distance(a, b) * 0.08f);
            for (int i = 0; i < 8; i++)
            {
                float t = i / 7f;
                hose.SetPosition(i, Vector3.Lerp(a, b, t) + Vector3.down * sag * 4f * t * (1f - t));
            }
        }
    }
}
