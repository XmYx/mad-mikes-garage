using MadMax.World;
using UnityEngine;

namespace MadMax.Net
{
    public partial class NetSession
    {
        // ------------------------------------------------------------------ breakaways (protocol 7)
        /// <summary>A pole snapped or a piece of street furniture knocked loose by a vehicle (<see cref="Breakaway"/>):
        /// the peer simulating the car sends it, every peer breaks its own copy (the fall is simulated locally).</summary>
        public void SendKnock(string key, Vector3 point, Vector3 velocity)
        {
            if (!ShouldSend || string.IsNullOrEmpty(key)) return;
            Reliable(Msg.Knock, w => { w.String(key); w.Pos(point); w.Pos(velocity); });
        }

        void ReadKnock(NetReader r, Peer from)
        {
            string key = r.String(); var at = r.Pos(); var vel = r.Pos();
            if (r.Failed) return;
            Applying = true;
            try { Breakaway.ApplyRemote(key, at, vel); }
            finally { Applying = false; }
            if (IsServer) Reliable(Msg.Knock, w => { w.String(key); w.Pos(at); w.Pos(vel); }, except: from);
        }
    }
}
