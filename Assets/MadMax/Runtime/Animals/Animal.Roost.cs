using MadMax.Building;
using MadMax.Game;
using MadMax.World;
using UnityEngine;

namespace MadMax.Animals
{
    /// <summary>Hens roost, dogs keep the door (2026-10-23): after dark kept hens walk into the nearest stable, hop up
    /// onto the roost bar along its back wall and sleep there till morning (hopping down at first light); a kept guard
    /// dog lies across the stable mouth facing out and barks at anyone hostile or any prowling predator within
    /// <see cref="BarkReach"/> m (it still goes for them inside its 16 m guard range).</summary>
    public partial class Animal
    {
        public const float BarkReach = 30f;
        /// <summary>The last time a door dog barked at something, and what (tests, the raid wake-up).</summary>
        public static float LastBarkAt = -99f;
        public static string LastBarkAtWhat;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRoost() { LastBarkAt = -99f; LastBarkAtWhat = null; }

        float perchLift;
        bool roosting, atDoor;
        Stable roostStable;
        float roostBest = float.MaxValue, roostProgressAt, toastAt, barkT;

        float PerchWanted => roosting ? Stable.RoostHeight : nesting ? NestBox.SitHeight : 0f;
        /// <summary>Up on the roost bar (tests).</summary>
        public bool Roosting => roosting && perchLift > Stable.RoostHeight * 0.9f;
        /// <summary>Lying across the stable mouth (tests).</summary>
        public bool AtDoor => atDoor;

        bool Hen => Def != null && Def.plan == BodyPlan.Bird && !Def.flies;
        bool DoorDog => Def != null && Def.guard && Adult;

        /// <summary>From <c>ThinkKept</c> (after the guard tick): true while roosting or keeping the door.</summary>
        bool RoostThink(WastelandGame g)
        {
            if (!owned || (!Hen && !DoorDog) || DayNight.Darkness < 0.6f || state == State.Ridden)
            {
                roosting = false; atDoor = false; roostStable = null;
                return false;
            }
            if (!roostStable) { roostStable = Stable.Near(home, BeddingReach); roostBest = float.MaxValue; roostProgressAt = Time.time; }
            if (!roostStable) return false;
            var me = transform.position;
            if (Hen)
            {
                var spot = roostStable.RoostSpot(BedIndex);
                float d = Flat(spot - me).magnitude;
                if (d < roostBest - 0.1f) { roostBest = d; roostProgressAt = Time.time; }
                bool stuck = Time.time - roostProgressAt > 3f;
                if (!roosting && d > 0.45f && !(stuck && roostStable.Inside(me + Vector3.up * 0.3f)))
                {
                    state = State.Wander; goal = spot; stateUntil = Time.time + 4f;
                    SetMove(spot - me, Def.walk);
                    return true;
                }
                if (!roosting) { roostStable.ShowRoost(); Say(0); }
                roosting = true;
                state = State.Sleep; stateUntil = Time.time + 30f;
                SetMove(Vector3.zero, 0f);
                transform.rotation = Quaternion.LookRotation(roostStable.Outward);
                return true;
            }
            // the dog: lie across the mouth, bark at what comes in the night
            var door = DogBed.Near(roostStable.DoorSpot, DogBed.DoorReach) is DogBed bed ? bed.LieSpot : roostStable.DoorSpot;   // its blanket by the door (Animal.Home)
            var prowler = Prowler(roostStable.transform.position, out float pd);
            if (prowler)
            {
                atDoor = Flat(door - me).magnitude < 2f;
                state = State.Alert; stateUntil = Time.time + 2f;
                SetMove(Vector3.zero, 0f);
                var to = prowler.transform.position - me; to.y = 0f;
                if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 60f);
                Bark(g, prowler);
                return true;
            }
            float dd = Flat(door - me).magnitude;
            if (!atDoor && dd > 0.6f)
            {
                state = State.Wander; goal = door; stateUntil = Time.time + 4f;
                SetMove(door - me, Def.walk);
                return true;
            }
            atDoor = true;
            state = State.Sleep; stateUntil = Time.time + 30f;
            SetMove(Vector3.zero, 0f);
            transform.rotation = Quaternion.LookRotation(roostStable.Outward);
            return true;
        }

        /// <summary>The nearest hostile person or wild predator within <see cref="BarkReach"/> of <paramref name="at"/>.</summary>
        Component Prowler(Vector3 at, out float dist)
        {
            Component best = null; float bd = BarkReach * BarkReach;
            foreach (var n in MadMax.Npc.Npc.All)
            {
                if (!n || !n.Alive || !n.Hostile) continue;
                float d = (n.transform.position - at).sqrMagnitude;
                if (d < bd) { bd = d; best = n; }
            }
            foreach (var a in All)
            {
                if (!a || !a.Alive || a.owned || a.town >= 0 || (a.Def.nature != Nature.Predator && a.Def.nature != Nature.Lurker && a.Def.nature != Nature.Charger)) continue;
                float d = (a.transform.position - at).sqrMagnitude;
                if (d < bd) { bd = d; best = a; }
            }
            dist = Mathf.Sqrt(bd);
            return best;
        }

        void Bark(WastelandGame g, Component at)
        {
            if (Time.time < barkT) return;
            barkT = Time.time + Random.Range(0.8f, 1.6f);
            callT = 0f;                                                                              // barking overrides the call cooldown
            Say(0);
            LastBarkAt = Time.time;
            if (roostStable) roostStable.disturbedAt = DayNight.TotalDays;
            LastBarkAtWhat = at is Animal a ? a.Def.name : at is MadMax.Npc.Npc n ? n.KnownName : "SOMETHING";
            Attention.Ping(transform.position, Attention.Kind.Animal, 40f);
            if (g && g.Player && Time.time > toastAt && (g.Player.transform.position - transform.position).sqrMagnitude < 80f * 80f)
            {
                toastAt = Time.time + 45f;
                g.Toast("THE DOG IS BARKING AT THE STABLE DOOR: " + LastBarkAtWhat + " OUT IN THE DARK");
            }
        }

        /// <summary>A kept dog keeping a stable door inside <paramref name="claim"/> (the raid wake-up names it).</summary>
        public static bool DogKeepsDoor(ClaimFlag claim)
        {
            if (!claim) return false;
            foreach (var a in All)
                if (a && a.Alive && a.owned && a.Def.guard && a.Adult && Stable.Near(a.home, BeddingReach) is Stable s && claim.Inside(s.transform.position)) return true;
            return false;
        }
    }
}
