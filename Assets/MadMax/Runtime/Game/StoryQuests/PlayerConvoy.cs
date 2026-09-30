using System.Collections.Generic;
using MadMax.Npc;
using MadMax.Story;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Story system "player_convoy" (storyline C5, the finale's run): a convoy the player leads. Each member is a
    /// story-tagged vehicle with an allied AI driver (<see cref="AiDriver.Goal.Escort"/>) that follows the vehicle ahead
    /// of it (the first one follows the player's), so the column keeps its order and spacing, slows when the one ahead
    /// slows and stops behind anything that stops: a boom, a cart, a truck that broke down. A member can be held (it
    /// waits where it is; the ones behind wait with it) or dropped (towed, or left behind: out of the column). One that
    /// falls far behind while the player is well away from it is brought up behind the one ahead, so allies keep
    /// formation without babysitting. A member the player climbs into is theirs to drive; the AI takes it back when they
    /// leave it. AI-driven vehicles aren't saved: after a load the quest calls <see cref="Form"/> again with a respawn
    /// callback that puts the missing trucks back behind the leader.</summary>
    public class PlayerConvoy
    {
        public class Member
        {
            public string tag, driver, name;
            public VehicleDriver v;
            public AiDriver ai;
            public bool held, dropped;
            public float farFor;
        }

        public readonly List<Member> members = new List<Member>();
        public bool Formed { get; private set; }
        /// <summary>The vehicle the player leads in (the last one they drove while the convoy rolls).</summary>
        public VehicleDriver Leader;
        /// <summary>Top speed the members chase at (m/s).</summary>
        public float cruise = 16f;
        /// <summary>A member farther than this behind the one ahead, with the player at least <see cref="CatchUpUnseen"/> away, is brought up.</summary>
        public float CatchUpGap = 110f, CatchUpUnseen = 70f;

        public Member Add(string tag, string driver, string name)
        {
            var m = Find(tag);
            if (m != null) return m;
            m = new Member { tag = tag, driver = driver, name = name };
            members.Add(m);
            return m;
        }

        public Member Find(string tag) { foreach (var m in members) if (m.tag == tag) return m; return null; }

        public static VehicleDriver Tagged(string tag)
        {
            var t = StoryTag.Find(tag);
            return t ? t.GetComponent<VehicleDriver>() : null;
        }

        /// <summary>Hand every member's wheel to its AI driver (respawning missing trucks through <paramref name="respawn"/>).
        /// Idempotent: call it again after a load or when a member came back into the column.</summary>
        public void Form(WastelandGame g, System.Func<Member, VehicleDriver> respawn = null)
        {
            if (g && g.Current) Leader = g.Current;
            foreach (var m in members)
            {
                if (m.dropped) continue;
                if (!m.v) m.v = Tagged(m.tag);
                if (!m.v && respawn != null) m.v = respawn(m);
                if (!m.v) continue;
                Drive(g, m);
            }
            Formed = true;
        }

        void Drive(WastelandGame g, Member m)
        {
            if (!m.v || (g && g.Current == m.v)) return;
            if (!m.v.TryGetComponent<AiDriver>(out m.ai)) m.ai = m.v.gameObject.AddComponent<AiDriver>();
            m.ai.enabled = true;
            m.v.aiDriven = true; m.v.bakedDriver = true; m.v.Occupied = true; m.v.handbrake = false;
            if (m.v.Body && m.v.Body.isKinematic) m.v.Body.isKinematic = false;
            m.ai.goal = AiDriver.Goal.Escort;
            m.ai.chaseSpeed = cruise;
        }

        /// <summary>Hold a member where it is (true) or let it rejoin (false).</summary>
        public void Hold(Member m, bool on) { if (m != null) m.held = on; }

        /// <summary>Take a member out of the column (towed, left behind): its AI lets go, it is an ordinary vehicle again.</summary>
        public void Drop(Member m)
        {
            if (m == null || m.dropped) return;
            m.dropped = true;
            if (m.ai) m.ai.Release();
        }

        /// <summary>Every frame while rolling: targets down the column, holds, catching up.</summary>
        public void Update(WastelandGame g)
        {
            if (!Formed || !g) return;
            // the column follows the vehicle the player leads in; while they are out on foot it waits behind that vehicle
            if (g.Current) Leader = g.Current;
            Transform ahead = Leader ? Leader.transform : null;
            var focus = g.Current ? g.Current.transform.position : g.Player ? g.Player.transform.position : Vector3.zero;
            foreach (var m in members)
            {
                if (m.dropped) continue;
                if (!m.v) { m.v = Tagged(m.tag); if (!m.v) { m.dropped = true; continue; } }       // destroyed: out of the column
                if (g.Current == m.v) { ahead = m.v.transform; continue; }                        // the player drives this one
                if (m.held)
                {
                    // waits where it is (its AI parks it; a truck whose AI let go is free to be towed)
                    if (m.ai && m.ai.enabled) { m.ai.goal = AiDriver.Goal.Park; m.ai.target = null; }
                }
                else
                {
                    if (!m.ai || !m.ai.enabled) Drive(g, m);
                    if (!m.ai) continue;
                    if (!ahead) { m.ai.goal = AiDriver.Goal.Park; m.ai.target = null; ahead = m.v.transform; continue; }
                    m.ai.goal = AiDriver.Goal.Escort; m.ai.target = ahead;
                    if (ahead && Flat(m.v.transform.position, ahead.position) > CatchUpGap && Flat(m.v.transform.position, focus) > CatchUpUnseen)
                    {
                        if ((m.farFor += Time.deltaTime) > 5f) { BringUp(m, ahead); m.farFor = 0f; }
                    }
                    else m.farFor = 0f;
                }
                ahead = m.v.transform;                                                                // the next one follows this one, held or not
            }
        }

        /// <summary>Put a straggler back 14 m behind the vehicle it follows, facing the same way, at its speed.</summary>
        public static void BringUp(Member m, Transform ahead)
        {
            if (m == null || !m.v || !ahead || !m.v.Body) return;
            var t = MadMax.World.DeformableTerrain.Instance;
            var back = ahead.forward; back.y = 0f; back = back.sqrMagnitude > 0.01f ? back.normalized : Vector3.forward;
            var p = ahead.position - back * 14f;
            if (t) p.y = t.Height(p.x, p.z) + 0.8f;
            m.v.Body.position = p;
            m.v.Body.rotation = Quaternion.LookRotation(back, Vector3.up);
            var rb = ahead.GetComponentInParent<Rigidbody>();
            m.v.Body.linearVelocity = rb && !rb.isKinematic ? rb.linearVelocity : Vector3.zero;
            m.v.Body.angularVelocity = Vector3.zero;
            m.v.Body.WakeUp();
        }

        /// <summary>The column's trucks still in it (not dropped) are all within <paramref name="r"/> m of <paramref name="p"/>.</summary>
        public bool AllWithin(Vector3 p, float r)
        {
            foreach (var m in members) if (!m.dropped && (!m.v || Flat(m.v.transform.position, p) > r)) return false;
            return true;
        }

        public int Count { get { int n = 0; foreach (var m in members) if (!m.dropped) n++; return n; } }

        /// <summary>The convoy stops being driven: every member's AI lets go, the trucks park where they are.</summary>
        public void Release()
        {
            foreach (var m in members) if (m.ai) m.ai.Release();
            Formed = false;
        }

        /// <summary>One line for the HUD: how many trucks, who is held, who is lagging.</summary>
        public string Status(WastelandGame g)
        {
            int n = 0; string held = null, far = null;
            var focus = g && g.Current ? g.Current.transform.position : g && g.Player ? g.Player.transform.position : Vector3.zero;
            foreach (var m in members)
            {
                if (m.dropped) continue;
                n++;
                if (m.held) held = held ?? m.name;
                else if (m.v && Flat(m.v.transform.position, focus) > 90f) far = far ?? m.name;
            }
            return "CONVOY: " + n + (n == 1 ? " TRUCK" : " TRUCKS") + (held != null ? ", " + held + " HAS STOPPED" : far != null ? ", " + far + " IS FALLING BEHIND" : ", ALL IN LINE");
        }

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
    }
}
