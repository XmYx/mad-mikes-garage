using MadMax.Net;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Network pose of the magnet crane (protocol 6, <c>NetSession.Tools</c>): slew, luff, rope and the magnet
    /// switch from the simulating peer. Remote copies never pull anything up themselves: the loads arrive with their own
    /// snapshots, hanging under the disc.</summary>
    public partial class MagnetCrane
    {
        public void NetWrite(NetWriter w)
        {
            w.Yaw(slew);
            w.Byte((byte)Mathf.Clamp(Mathf.RoundToInt(luff * 3f), 0, 255));
            w.Byte((byte)Mathf.Clamp(Mathf.RoundToInt(rope * 25f), 0, 255));
            w.Bool(on);
        }

        public void NetRead(NetReader r)
        {
            float s = r.Yaw(), l = r.Byte() / 3f, rp = r.Byte() / 25f; bool live = r.Bool();
            if (r.Failed || !Part) return;
            slew = s; luff = l; rope = Mathf.Max(0.6f, rp); slewSpeed = 0f;
            Pose();
            if (live != on) Switch(live);
        }
    }
}
