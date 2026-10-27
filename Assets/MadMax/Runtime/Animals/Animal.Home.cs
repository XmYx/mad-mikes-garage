using MadMax.Building;
using MadMax.Game;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Animals
{
    /// <summary>Life about the yard (2026-10-26). Eggs in the straw: a fed hen's egg for the day is laid next morning —
    /// she walks to a nest box near home, hops into a free compartment, sits a while and leaves the egg there with a
    /// cackle (no box: in the stable straw, as a loose egg); more on fresh bedding, fewer after a night of the dog
    /// barking. The yard dog: a name the player gives it (<see cref="petName"/>, saved), afternoon naps on its blanket
    /// (<see cref="DogBed"/>), and a welcome when the player comes home after a while away (<see cref="Welcome"/>).</summary>
    public partial class Animal
    {
        /// <summary>The name the player gave it (null: none).</summary>
        public string petName;

        public const float LayFrom = 5.5f, LayTo = 11f, LaySeconds = 9f, GreetSeconds = 7f;
        /// <summary>Eggs laid (tests) and where the last one went.</summary>
        public static int EggsLaid;
        public static string LastLaid, LastWelcome;
        static float layToastAt = -99f;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetHome() { EggsLaid = 0; LastLaid = null; LastWelcome = null; layToastAt = -99f; }

        int layPending, nestSlot = -1;
        bool nesting, napping;
        NestBox nestBox;
        Stable layStable;
        float sitUntil, layBest = float.MaxValue, layProgressAt, greetUntil, greetAng, napBest = float.MaxValue, napProgressAt;
        Transform greetWho;

        /// <summary>Eggs she still has to lay this morning (tests).</summary>
        public int LayPending { get => layPending; set => layPending = value; }
        /// <summary>Sitting in a nest box / on the straw to lay (tests).</summary>
        public bool Nesting => nesting;
        /// <summary>Asleep on its blanket (tests).</summary>
        public bool Napping => napping;
        /// <summary>Running round the player who just came home (tests).</summary>
        public bool Greeting => greetWho && Time.time < greetUntil;
        public bool IsDog => Def != null && (Def.guard || Def.id == "dog");

        /// <summary>From the daily tick: the day's eggs wait for the morning when there is a nest box or a stable to lay in
        /// (false: none, they go to <see cref="stock"/> as before). Yesterday's unlaid eggs are put in place now.</summary>
        public bool QueueEggs(int n)
        {
            var box = NestBox.Near(home, NestBox.Reach);
            var st = Stable.Near(home, BeddingReach);
            if (!box && !st) return false;
            if (layPending > 0) { if (box) box.Lay(layPending); else LooseEgg(st, layPending); layPending = 0; }
            if (st)
            {
                if (st.Bedding > 0.5f && Random.value < 0.4f) n++;                                    // fresh straw
                if (DayNight.TotalDays - st.disturbedAt < 0.75f && Random.value < 0.6f) n--;         // the dog barked half the night
            }
            layPending = Mathf.Max(0, n);
            return true;
        }

        void LooseEgg(Stable st, int n)
        {
            var g = WastelandGame.Instance;
            for (int i = 0; i < n; i++)
            {
                var p = st.BedSpot(BedIndex + 3 + i);
                WorldItem.Create("food_egg", 1, 0.5f, g ? g.propMaterial : null, p, Quaternion.Euler(0f, Random.value * 360f, 0f));
            }
        }

        bool HomeThink(WastelandGame g)
        {
            if (!owned) return false;
            if (Hen) return LayThink(g);
            if (IsDog) return GreetThink() || NapThink();
            return false;
        }

        bool LayThink(WastelandGame g)
        {
            float h = DayNight.Hours;
            if (layPending <= 0 || h < LayFrom || h > LayTo) { EndNest(); return false; }
            if (!nestBox && !layStable)
            {
                nestBox = NestBox.Near(home, NestBox.Reach);
                if (!nestBox) layStable = Stable.Near(home, BeddingReach);
                if (!nestBox && !layStable) { stock = Mathf.Min(Def.productPerDay * 3, stock + layPending); layPending = 0; return false; }
                layBest = float.MaxValue; layProgressAt = Time.time;
            }
            var me = transform.position;
            Vector3 spot;
            if (nestBox)
            {
                nestSlot = nestBox.Claim(this);
                if (nestSlot < 0)                                                                    // all three taken: wait by the box
                {
                    var wait = nestBox.Approach(1) + nestBox.transform.right * (BedIndex % 2 == 0 ? 0.9f : -0.9f);
                    state = State.Idle; stateUntil = Time.time + 2f;
                    SetMove(Flat(wait - me).magnitude > 0.4f ? wait - me : Vector3.zero, Def.walk);
                    return true;
                }
                spot = nestBox.SitSpot(nestSlot);
            }
            else spot = layStable.BedSpot(BedIndex + 3);
            float d = Flat(spot - me).magnitude;
            if (!nesting)
            {
                if (d < layBest - 0.1f) { layBest = d; layProgressAt = Time.time; }
                bool stuck = Time.time - layProgressAt > 4f && d < 1.6f;
                if (d > 0.3f && !stuck)
                {
                    state = State.Wander; goal = spot; stateUntil = Time.time + 4f;
                    SetMove(spot - me, Def.walk);
                    return true;
                }
                if (stuck) { var p = spot; p.y = me.y; transform.position = p; }                     // a flutter up into the box
                nesting = true; sitUntil = Time.time + LaySeconds;
                Say(0);
            }
            state = State.Sleep; stateUntil = Time.time + 2f;
            SetMove(Vector3.zero, 0f);
            if (nestBox) transform.rotation = Quaternion.LookRotation(nestBox.transform.forward);
            if (Time.time < sitUntil) return true;
            layPending--;
            EggsLaid++;
            if (nestBox) { nestBox.Lay(1); LastLaid = "NEST BOX"; }
            else { LooseEgg(layStable, 1); LastLaid = "STRAW"; }
            callT = 0f; Say(0);                                                                      // the cackle
            if (g && g.Player && Time.time > layToastAt && (g.Player.transform.position - me).sqrMagnitude < 25f * 25f)
            {
                layToastAt = Time.time + 60f;
                g.Toast(nestBox ? "A HEN CACKLES IN THE NEST BOX: A FRESH EGG" : "A HEN CACKLES IN THE STABLE: AN EGG IN THE STRAW");
            }
            EndNest();
            return false;
        }

        void EndNest()
        {
            if (nestBox) nestBox.Free(this);
            nestBox = null; layStable = null; nesting = false; nestSlot = -1;
        }

        /// <summary>The player came home: run out and meet them for a few seconds.</summary>
        public void Welcome(Transform who)
        {
            if (!who || !Alive) return;
            greetWho = who; greetUntil = Time.time + GreetSeconds; greetAng = Random.value * 360f;
            napping = false; napBest = float.MaxValue;
            LastWelcome = (petName ?? "THE DOG") + " RUNS OUT TO MEET YOU, TAIL GOING";
        }

        bool GreetThink()
        {
            if (!Greeting) { greetWho = null; return false; }
            var me = transform.position;
            var p = greetWho.position;
            float d = Flat(p - me).magnitude;
            state = State.Follow; stateUntil = Time.time + 1f;
            if (d > 2.4f) { SetMove(p - me, Def.run); return true; }
            greetAng += Time.deltaTime * 240f;                                                       // bounding round them
            var round = p + Quaternion.Euler(0f, greetAng, 0f) * Vector3.forward * 1.3f;
            SetMove(round - me, Def.run * 0.6f);
            if (Random.value < Time.deltaTime * 1.2f) { callT = 0f; Say(0); }
            return true;
        }

        /// <summary>Afternoons (and nights without a stable door to keep) on its blanket near home.</summary>
        bool NapThink()
        {
            float h = DayNight.Hours;
            bool nap = h >= 12.5f && h <= 16f || DayNight.Darkness > 0.6f;
            var bed = nap && order == 0 ? DogBed.Near(home, DogBed.NapReach) : null;
            if (!bed) { napping = false; napBest = float.MaxValue; return false; }
            var me = transform.position;
            var spot = bed.LieSpot;
            float d = Flat(spot - me).magnitude;
            if (!napping)
            {
                if (napBest == float.MaxValue || d < napBest - 0.1f) { napBest = d; napProgressAt = Time.time; }
                bool stuck = Time.time - napProgressAt > 3f && d < 1.6f;                             // nosing round it (a car, a post beside it)
                if (d > 0.45f && !stuck)
                {
                    state = State.Wander; goal = spot; stateUntil = Time.time + 4f;
                    SetMove(spot - me, Def.walk);
                    return true;
                }
                if (stuck) { var p = spot; p.y = me.y; transform.position = p; }                     // a scramble onto it
                napBest = float.MaxValue;
                transform.rotation = Quaternion.LookRotation(bed.transform.right);                   // curled up across it
            }
            napping = true;
            state = State.Sleep; stateUntil = Time.time + 30f;
            SetMove(Vector3.zero, 0f);
            return true;
        }

        /// <summary>Three names to offer for a dog, by its key.</summary>
        public string[] NameIdeas()
        {
            int h = (key ?? "").GetHashCode() & 0x7fffffff;
            var list = DogNames;
            return new[] { list[h % list.Length], list[(h / 7 + 5) % list.Length], list[(h / 53 + 11) % list.Length] };
        }

        static readonly string[] DogNames = { "REX", "BLUE", "DUST", "BONES", "SKIP", "RUSTY", "MAX", "SHADOW", "PATCH", "DIESEL", "SCOUT", "GRIT",
            "BANDIT", "SMOKE", "TICK", "ROO", "JUNO", "BOLT", "HOBO", "SARGE" };
    }
}
