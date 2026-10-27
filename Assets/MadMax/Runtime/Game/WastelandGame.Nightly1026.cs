using System.Collections.Generic;
using MadMax.Animals;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Scheduled update 2026-10-26: bring something to supper (a dish or a bottle warms the neighbour and loosens
    /// a second tale; turning up empty-handed every time cools them), the yard dog's name (context menu on a kept dog;
    /// saved with the animal), its welcome when you come home, and dressed scarecrows in town fields (<see cref="TownScarecrows"/>).
    /// Eggs in the straw and the dog's blanket live in <c>Animals/Animal.Home</c>; the wash in the wind in <c>ClothesLine</c>.</summary>
    public partial class WastelandGame
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset1026() { LastDogName = null; }

        // ------------------------------------------------------------------ bring something to supper

        /// <summary>Neighbour id → suppers in a row the player came empty-handed.</summary>
        public readonly Dictionary<string, int> SupperEmpty = new Dictionary<string, int>();
        /// <summary>Empty-handed suppers in a row before the neighbour mentions it (and cools).</summary>
        public const int SupperEmptyCools = 3;

        /// <summary>The best thing in the pack to bring to a neighbour's table: a cooked dish or a bottle (null: nothing fit).</summary>
        public string SupperGift()
        {
            string best = null; float score = 0f;
            foreach (var kv in Inventory.Items)
            {
                if (kv.Value <= 0) continue;
                var id = kv.Key;
                bool bottle = id.StartsWith("brew_") || id.StartsWith("drink_") && id != "drink_water" && id != "drink_dirty_water";
                var f = FoodLibrary.Get(id);
                if (!bottle && (f == null || id == "food_rotten" || id.StartsWith("bait_") || id.Contains("raw") || f.hunger < 25f)) continue;
                float s = (f != null ? f.hunger + f.thirst * 0.5f : 20f) + (bottle ? 15f : 0f);
                if (s > score) { score = s; best = id; }
            }
            return best;
        }

        /// <summary>How much warmer supper leaves the neighbour (disposition), whether they talk, and what they say.</summary>
        int SupperWarmth(string key, string gift, out bool talk, out string line)
        {
            SupperEmpty.TryGetValue(key, out int empty);
            talk = true; line = null;
            if (gift != null)
            {
                SupperEmpty[key] = 0;
                bool bottle = gift.StartsWith("brew_") || gift.StartsWith("drink_");
                line = bottle ? "OH, YOU SHOULDN'T HAVE. FETCH THE GOOD CUPS." : "YOU COOKED? SIT DOWN, SIT DOWN - WE'LL HAVE IT WITH THE STEW.";
                return 10;
            }
            SupperEmpty[key] = ++empty;
            if (empty < SupperEmptyCools) return 4;
            talk = empty % 2 == 1;                                                                   // less to say over a bare table
            line = "NEXT TIME BRING SOMETHING FOR THE POT, EH?";
            return -2;
        }

        // ------------------------------------------------------------------ the yard dog

        /// <summary>The last naming (tests).</summary>
        public static string LastDogName;
        /// <summary>Away from home (m) for at least <see cref="WelcomeAfterHours"/> game hours → the dog welcomes the player back within <see cref="WelcomeWithin"/> m.</summary>
        public const float AwayFrom = 120f, WelcomeWithin = 30f, WelcomeAfterHours = 2f;
        readonly Dictionary<Animal, float> dogAwaySince = new Dictionary<Animal, float>();
        float dogT;

        void DogOptions(ContextTarget t, List<ContextOption> into)
        {
            if (!(t.target is Animal a) || !a.Alive || !a.owned || !a.IsDog) return;
            if (a.petName == null)
            {
                foreach (var n in a.NameIdeas()) into.Add(Opt("NAME THE DOG " + n, () => NameDog(a, n)));
            }
            else into.Add(Opt("CALL " + a.petName, () => { a.Welcome(Player.transform); Toast(a.petName + " COMES BOUNDING OVER"); }));
        }

        /// <summary>Give the yard dog a name (a crew member nearby has a word about it).</summary>
        public void NameDog(Animal a, string name)
        {
            if (!a || string.IsNullOrEmpty(name)) return;
            a.petName = name;
            a.Welcome(Player ? Player.transform : a.transform);
            LastDogName = "THE DOG IS " + name + " NOW";
            Toast(LastDogName + ": IT WAGS AT THE SOUND OF IT");
            Journal.Add("HOME", "NAMED THE YARD DOG " + name);
            foreach (var n in MadMax.Npc.Companions.Live)
                if (n && n.Alive && Player && Flat(n.transform.position - Player.transform.position) < 15f)
                {
                    Remark(n, name + "? " + (name.Length <= 4 ? "SHORT AND SHOUTABLE. GOOD." : "BIT OF A MOUTHFUL - IT'LL ANSWER TO IT THOUGH."), 4f);
                    break;
                }
        }

        /// <summary>Kept dogs remember when the player left home and run out to meet them when they come back.</summary>
        void UpdateDogs()
        {
            if (Time.time < dogT || !Player) return;
            dogT = Time.time + 1f;
            var me = Player.transform.position;
            float now = DayNight.TotalDays * 24f;
            foreach (var a in Animal.All)
            {
                if (!a || !a.Alive || !a.owned || !a.IsDog || !a.Adult || a.order != 0) continue;
                float d = Flat(me - a.home);
                bool away = dogAwaySince.TryGetValue(a, out float since);
                if (d > AwayFrom) { if (!away) dogAwaySince[a] = now; continue; }
                if (!away || d > WelcomeWithin) continue;
                dogAwaySince.Remove(a);
                if (now - since < WelcomeAfterHours || Current) continue;
                a.Welcome(Player.transform);
                Toast(Animal.LastWelcome);
            }
        }

        // ------------------------------------------------------------------ tick

        TownScarecrows townScarecrows;

        void UpdateNightly1026()
        {
            if (!ContextActions.Has("home.dog")) ContextActions.Register("home.dog", (g, t, into) => { if (t.item == null) g.DogOptions(t, into); });
            if (!townScarecrows && World != null) townScarecrows = new GameObject("TownScarecrows").AddComponent<TownScarecrows>();
            UpdateDogs();
        }
    }
}
