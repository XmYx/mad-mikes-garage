using MadMax.Items;
using MadMax.Net;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Network pose of a machine (protocol 6, <c>NetSession.Tools</c>): hinge angles in centidegrees, loads in
    /// litres and their soil type, sent by whoever simulates it so the arm, the bucket heap, the blade windrow and the
    /// tipper bed look the same on every peer.</summary>
    public partial class Machine
    {
        /// <summary>Tipper bed fill from the network (the bed's container lives on the simulating peer only).</summary>
        bool netBed;

        static void A(NetWriter w, float deg) => w.Short((short)Mathf.Clamp(Mathf.RoundToInt(deg * 100f), short.MinValue, short.MaxValue));
        static float A(NetReader r) => r.Short() / 100f;
        static void L(NetWriter w, float m3, ResourceType t) { w.UShort((ushort)Mathf.Clamp(Mathf.RoundToInt(m3 * 1000f), 0, ushort.MaxValue)); w.Byte((byte)t); }
        static float L(NetReader r, out ResourceType t) { float v = r.UShort() / 1000f; t = (ResourceType)r.Byte(); return v; }

        public void NetWrite(NetWriter w)
        {
            w.Byte((byte)kind);
            switch (kind)
            {
                case Kind.Excavator: A(w, slew); A(w, boom); A(w, stick); A(w, bucket); L(w, load, loadType); break;
                case Kind.Backhoe: A(w, arms); A(w, tilt); A(w, hoeSwing); A(w, hoeBoom); A(w, hoeStick); A(w, hoeBucket); L(w, load, loadType); L(w, rearLoad, rearType); break;
                case Kind.Dozer: A(w, bladeLift); A(w, bladePitch); A(w, bladeAngle); L(w, load, loadType); break;
                case Kind.DumpTruck: A(w, bed); L(w, bedFill, bedType); break;
                case Kind.Paver: A(w, screed); w.Bool(active); break;
                case Kind.Tractor: A(w, hitch); break;
            }
        }

        public void NetRead(NetReader r)
        {
            var k = (Kind)r.Byte();
            if (k != kind) return;
            switch (kind)
            {
                case Kind.Excavator: slew = A(r); boom = A(r); stick = A(r); bucket = A(r); load = L(r, out loadType); break;
                case Kind.Backhoe: arms = A(r); tilt = A(r); hoeSwing = A(r); hoeBoom = A(r); hoeStick = A(r); hoeBucket = A(r); load = L(r, out loadType); rearLoad = L(r, out rearType); break;
                case Kind.Dozer: bladeLift = A(r); bladePitch = A(r); bladeAngle = A(r); load = L(r, out loadType); break;
                case Kind.DumpTruck: bed = A(r); bedFill = L(r, out bedType); netBed = true; break;
                case Kind.Paver: screed = A(r); active = r.Bool(); break;
                case Kind.Tractor: hitch = A(r); break;
            }
        }

        /// <summary>The tipper bed is read from its container here (not while the network poses it).</summary>
        bool ScansBed { get { if (netBed && Simulated) netBed = false; return !netBed; } }
    }
}
