using MadMax.Game;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Animals
{
    /// <summary>Scraps for the animals (2026-10-17): kept pigs, chickens, goats and dogs notice food lying on the ground
    /// near their home (scraps thrown down, rotten food in the yard, a dropped apple), trot over, root or peck at it for a
    /// few seconds and eat it. Enough of it counts as the day's feed (<see cref="ScrapsForDay"/>), so kitchen waste goes
    /// to the pigs instead of rotting into flies. Pigs and dogs take rotten food; the others only what they like or plain
    /// food. Host only (animals are simulated on the authority); the eaten item goes for everyone.</summary>
    public partial class Animal
    {
        public const float ScrapReach = 9f, ScrapEatSeconds = 3f;
        WorldItem scrap;
        float scrapEatUntil = -1f, scrapLookT;
        int scrapsToday, scrapDay = -1;
        /// <summary>Food items eaten off the ground this session (tests).</summary>
        public static int ScrapsEaten;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetScraps() { ScrapsEaten = 0; SlopEaten = 0; }

        /// <summary>Is this a kept scrap-eater?</summary>
        public bool ScrapEater => Def != null && (owned || town >= 0) && Adult && !Def.flies
            && (Def.id == "pig" || Def.id == "chicken" || Def.id == "goat" || Def.id == "dog");

        /// <summary>Items of ground food this animal needs to call it fed for the day.</summary>
        public int ScrapsForDay => Def.id == "chicken" ? 1 : Def.id == "pig" ? 3 : 2;

        /// <summary>Would this animal eat <paramref name="id"/> off the ground?</summary>
        public bool EatsScrap(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (Def.likes != null) foreach (var l in Def.likes) if (l == id) return true;
            if (id == "food_rotten") return Def.id == "pig" || Def.id == "dog";                      // pigs and dogs eat anything
            if (Def.id == "dog") return id.StartsWith("food_meat") || id == "food_jerky";
            if (Def.id == "chicken") return id.StartsWith("crop_") || id.StartsWith("seed_") || id == "food_bread";
            return id.StartsWith("food_") || id.StartsWith("crop_");
        }

        /// <summary>The food this animal is going for or eating (tests).</summary>
        public WorldItem ScrapTarget => scrap;
        public bool EatingScraps => (scrap || slopFrom) && scrapEatUntil > 0f;

        /// <summary>The slop bucket this animal is at (2026-10-18; tests).</summary>
        public MadMax.Building.SlopBucket SlopTarget => slopFrom;
        MadMax.Building.SlopBucket slopFrom;
        float slopBest = float.MaxValue, slopProgressAt;
        /// <summary>Distance to the slop bucket (tests).</summary>
        public float SlopDistance => slopFrom ? Flat(slopFrom.transform.position - transform.position).magnitude : -1f;
        /// <summary>Scraps eaten out of slop buckets this session (tests).</summary>
        public static int SlopEaten;

        /// <summary>At a slop bucket: walk up, nose in for <see cref="ScrapEatSeconds"/>, one scrap.</summary>
        bool SlopThink(Vector3 me)
        {
            if (!slopFrom || slopFrom.scraps <= 0) { slopFrom = null; scrapEatUntil = -1f; return false; }
            var at = slopFrom.transform.position;
            if (scrapEatUntil > 0f)
            {
                state = State.Graze; SetMove(Vector3.zero, 0f);
                if (Time.time >= scrapEatUntil)
                {
                    scrapEatUntil = -1f;
                    if (slopFrom.Take()) { CountScrap(1); SlopEaten++; MadMax.Audio.Sfx.Play("dig", at, 0.15f, Def.id == "chicken" ? 1.8f : 1.1f, 6f); Say(0); }
                    slopFrom = null;
                }
                return true;
            }
            float sd = Flat(at - me).magnitude;
            if (sd < slopBest - 0.1f) { slopBest = sd; slopProgressAt = Time.time; }
            bool blocked = sd < 2f && Time.time - slopProgressAt > 2f;                          // nose against something: eat from here
            if (sd < Def.reach + 1.1f || blocked) { slopBest = float.MaxValue; scrapEatUntil = Time.time + ScrapEatSeconds; state = State.Graze; SetMove(Vector3.zero, 0f); return true; }
            state = State.Wander; goal = at; stateUntil = Time.time + 6f;
            SetMove(at - me, Def.walk * 1.6f);
            return true;
        }

        void CountScrap(int n)
        {
            if (scrapDay != DayNight.Day) { scrapDay = DayNight.Day; scrapsToday = 0; }
            scrapsToday += n;
            if (scrapsToday >= ScrapsForDay && fedDay != DayNight.Day) { fedDay = DayNight.Day; hungryDays = 0; }
        }

        /// <summary>From <c>ThinkKept</c> (every ~0.2 s): go for food on the ground near home; true while busy with it.</summary>
        bool ScrapsThink(WastelandGame g)
        {
            if (!ScrapEater || state == State.Sleep) { scrap = null; slopFrom = null; return false; }
            var net = MadMax.Net.NetSession.Instance;
            if (net && net.IsClient) return false;
            var me = transform.position;
            if (slopFrom && SlopThink(me)) return true;                                         // the slop bucket (2026-10-18)
            if (scrap)
            {
                if (scrap.transform.parent || !scrap.gameObject.activeInHierarchy) { scrap = null; return false; }   // picked up
                var at = scrap.transform.position;
                float d = Flat(at - me).magnitude;
                if (scrapEatUntil > 0f)
                {
                    state = State.Graze; SetMove(Vector3.zero, 0f);                              // head down at it
                    if (Time.time >= scrapEatUntil) EatScrap();
                    return true;
                }
                if (d < Def.reach + 0.35f) { scrapEatUntil = Time.time + ScrapEatSeconds; state = State.Graze; SetMove(Vector3.zero, 0f); if (Random.value < 0.5f) Say(0); return true; }
                if (Flat(at - home).magnitude > ScrapReach + 4f) { scrap = null; return false; }
                state = State.Wander; goal = at; stateUntil = Time.time + 6f;
                SetMove(at - me, Def.walk * 1.6f);                                               // a brisk trot to it
                return true;
            }
            if ((scrapLookT -= 0.25f) > 0f) return false;
            scrapLookT = 1.5f + Random.value;
            if (scrapDay == DayNight.Day && scrapsToday >= ScrapsForDay * 2) return false;      // full up
            float best = ScrapReach * ScrapReach;
            foreach (var w in WorldItem.All)
            {
                if (!w || w.IsResource || w.OnVehicle || w.transform.parent || !EatsScrap(w.key) || Claimed(w)) continue;
                var p = w.transform.position;
                if (Mathf.Abs(p.y - me.y) > 1.5f || Flat(p - home).sqrMagnitude > (ScrapReach + 3f) * (ScrapReach + 3f)) continue;
                float d2 = Flat(p - me).sqrMagnitude;
                if (d2 < best) { best = d2; scrap = w; }
            }
            if (!scrap)
            {
                slopFrom = MadMax.Building.SlopBucket.Nearest(home, MadMax.Building.SlopBucket.Reach);
                slopBest = float.MaxValue; slopProgressAt = Time.time;
                if (slopFrom && Mathf.Abs(slopFrom.transform.position.y - me.y) > 1.5f) slopFrom = null;
                scrapEatUntil = -1f;
                return slopFrom && SlopThink(me);
            }
            scrapEatUntil = -1f;
            return true;
        }

        static bool Claimed(WorldItem w)
        {
            foreach (var a in All) if (a && a.scrap == w) return true;
            return false;
        }

        void EatScrap()
        {
            var w = scrap; scrap = null; scrapEatUntil = -1f;
            if (!w) return;
            CountScrap(Mathf.Max(1, w.count));
            ScrapsEaten++;
            MadMax.Audio.Sfx.Play("dig", transform.position, 0.15f, Def.id == "chicken" ? 1.8f : 1.2f, 6f);
            var net = MadMax.Net.NetSession.Instance;
            if (net && w.netId != 0) net.SendItemGone(w.netId);
            Object.Destroy(w.gameObject);
            Say(0);
        }
    }
}
