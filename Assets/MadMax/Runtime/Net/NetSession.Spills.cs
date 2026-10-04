using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Net
{
    public partial class NetSession
    {
        // ------------------------------------------------------------------ ground spills (protocol 5)
        /// <summary>Liquid poured, drained or leaked onto the ground: every peer simulates its own copy of the spill
        /// (<see cref="Spills"/>), so only the pour travels (the server relays).</summary>
        public void SendSpill(Vector3 at, FluidMix mix, float litres)
        {
            if (!ShouldSend || mix == null || mix.Empty || litres <= 0f) return;
            string m = mix.Save();
            Reliable(Msg.Spill, w => { w.Pos(at); w.Float(litres); w.String(m); });
        }

        void ReadSpill(NetReader r, Peer from)
        {
            var at = r.Pos(); float litres = r.Float(); string m = r.String();
            if (r.Failed) return;
            var mix = new FluidMix();
            if (!mix.Load(m)) return;
            Applying = true;
            try { Spills.Pour(at, mix, litres, false); }
            finally { Applying = false; }
            if (IsServer) Reliable(Msg.Spill, w => { w.Pos(at); w.Float(litres); w.String(m); }, except: from);
        }
    }
}
