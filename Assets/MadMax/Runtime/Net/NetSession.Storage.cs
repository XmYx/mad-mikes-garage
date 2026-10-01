using MadMax.Vehicles;

namespace MadMax.Net
{
    public partial class NetSession
    {
        // ------------------------------------------------------------------ vehicle compartments (trunk, glovebox, ...)
        /// <summary>Whoever changed a vehicle's compartments sends their contents (the server relays); last write wins.</summary>
        public void SendVehicleStore(VehicleDriver v, string state)
        {
            if (!Online || Applying || !v || v.netId == 0) return;
            ushort id = v.netId; string s = state ?? "";
            Reliable(Msg.VehicleStore, w => { w.UShort(id); w.String(s); });
        }

        void ReadVehicleStore(NetReader r, Peer from)
        {
            ushort id = r.UShort(); string state = r.String();
            if (r.Failed) return;
            var v = Vehicle(id);
            if (!v || !v.TryGetComponent<VehicleStorage>(out var st)) return;
            Applying = true;
            try { st.LoadState(state); }
            finally { Applying = false; }
            if (IsServer) Reliable(Msg.VehicleStore, w => { w.UShort(id); w.String(state); }, except: from);
        }
    }
}
