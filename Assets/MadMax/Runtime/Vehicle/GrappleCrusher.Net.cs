using System.Collections.Generic;
using MadMax.Net;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Network side of the grapple and crusher (protocol 6, <c>NetSession.Tools</c>): the arm pose and jaws from
    /// the simulating peer, and a crush cycle another peer started (<see cref="RemoteCrush"/>): the same press runs here
    /// as a show — no parts popped, no fluids drained, no bale (those arrive as their own events) — and the car is gone
    /// at the end.</summary>
    public partial class GrappleCrusher
    {
        bool remoteCrush;

        static void A(NetWriter w, float deg) => w.Short((short)Mathf.Clamp(Mathf.RoundToInt(deg * 100f), short.MinValue, short.MaxValue));

        public void NetWrite(NetWriter w)
        {
            w.Yaw(slew); A(w, luff); A(w, knuckle);
            w.Byte((byte)Mathf.Clamp(Mathf.RoundToInt(jawTarget * 4f), 0, 255));
        }

        public void NetRead(NetReader r)
        {
            float s = r.Yaw(), l = r.Short() / 100f, k = r.Short() / 100f, j = r.Byte() / 4f;
            if (r.Failed || !Part) return;
            slew = s; luff = l; knuckle = k; slewSpeed = 0f;
            jawTarget = j; closing = false; reopenAt = -1f;
            Pose();
        }

        /// <summary>Another peer started a crush of <paramref name="car"/> (and loose <paramref name="bits"/>) in this truck's bay.</summary>
        public void RemoteCrush(VehicleDriver car, List<Rigidbody> bits)
        {
            if (cycle != Cycle.Idle) { if (remoteCrush) RemoteBale(); cycle = Cycle.Idle; lidAngle = LidOpen; press = 0f; PoseLid(); }
            if (!Part || !hasBay)
            {
                victim = car; scraps.Clear(); if (bits != null) scraps.AddRange(bits);
                RemoteBale();
                return;
            }
            if (load && ((car && load == car.Body) || (bits != null && bits.Contains(load)))) Drop();
            victim = car;
            scraps.Clear();
            if (bits != null) foreach (var b in bits) if (b) scraps.Add(b);
            if (!victim && scraps.Count == 0) return;
            foreach (var b in scraps) if (b.TryGetComponent<NetReplica>(out var br)) br.enabled = false;
            remoteCrush = true;
            Bay(out var c, out var h);
            BeginCycle(c, h);
        }

        /// <summary>The end of a remote press: the car and the bits are gone (the bale comes from the crushing peer).</summary>
        void RemoteBale()
        {
            if (victim)
            {
                MadMax.Game.WastelandGame.Instance?.ForgetVehicle(victim);
                Destroy(victim.gameObject);
            }
            foreach (var s in scraps) if (s) Destroy(s.gameObject);
            scraps.Clear();
            victim = null;
        }
    }
}
