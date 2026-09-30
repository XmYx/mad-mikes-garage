using MadMax.Animals;
using MadMax.Building;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game
{
    // S12 THE DOG AT PLATFORM THREE: the abandoned station (a low stone platform, a canopy, the old track, a PLATFORM 3
    // sign, a bench where Etta leaves scraps, a notice board) and Walt's niece's porch. Penny is a calm kept-style dog
    // (she takes food only from the offer on her, S12DogCare) with a lame paw held until it is treated; her routine runs
    // between the bench and the platform end. The ending moves her: led to Walt's porch, left at her platform, or adopted.
    public partial class WastelandGame
    {
        const string S12Key = "s12:penny";
        static readonly Vector3 S12Bench = new Vector3(1.6f, 0f, 0.9f), S12Edge = new Vector3(-7.2f, 0f, -2.2f);
        Animal s12Dog;
        bool s12Hooked, s12AtEdge;
        float s12T, s12Watch, s12Routine;

        partial void Scene_S12()
        {
            Q5ResetPayoff("S12");
            if (!StoryAnchors.Has("etta") || !Build || !Build.Structures) return;
            // the station: a low stone platform along the old track, a canopy, the sign, a bench, a dead lamp
            for (int i = -2; i <= 2; i++) Q5Put("etta", "foundation_stone", new Vector3(i * 2f, 0f, -3f), 0f, 0.2f);
            for (int i = -5; i <= 4; i++) Q5Put("etta", "mine_rail", new Vector3(i * 2f + 1f, 0f, -5.2f), 90f);
            Q5Put("etta", "porch_awning", new Vector3(-1f, 0f, -3f), 0f, 0.36f);
            Q5Put("etta", "porch_awning", new Vector3(2.4f, 0f, -3f), 0f, 0.36f);
            Q5Put("etta", "sign_direction", new Vector3(-5.6f, 0f, -1.4f), 0f);
            Q5Put("etta", "bench", S12Bench + new Vector3(0f, 0f, -0.8f), 0f);
            Q5Put("etta", "lamppost", new Vector3(5.8f, 0f, -1.4f), 0f);
            Q5Put("s12_board", "sign", Vector3.zero, 180f);
            // Walt's niece's porch up the road
            if (StoryAnchors.Has("s12_home"))
            {
                Q5Put("s12_home", "porch_awning", new Vector3(0f, 0f, -1.6f), 0f);
                Q5Put("s12_home", "armchair", new Vector3(-1.2f, 0f, -1.5f), 0f);
                Q5Put("s12_home", "flower_pot", new Vector3(1.8f, 0f, -0.6f), 0f);
                Q5Put("s12_home", "barrel", new Vector3(-2.6f, 0f, -2.2f), 0f);
                Q5Put("s12_home", "rug", new Vector3(0.6f, 0f, -1.4f), 0f);
            }
            S12Spawn(Q5At("etta", S12Bench));
        }

        /// <summary>Penny: a calm dog (kept-style, so she neither bolts nor bites), lame on a forepaw until treated.</summary>
        Animal S12Spawn(Vector3 at)
        {
            var def = AnimalLibrary.Get("dog");
            if (def == null) return null;
            s12Dog = Animal.Spawn(def, at, StoryAnchors.Yaw("etta") + 180f, propMaterial, S12Key);
            s12Dog.town = 0;
            s12Dog.home = at;
            if (!MadMax.Story.Story.Flag("s12:treated")) { s12Dog.limp = 0.6f; s12Dog.health = def.health * 0.6f; }
            s12Dog.gameObject.AddComponent<S12DogCare>();
            return s12Dog;
        }

        void S12Treated(Animal a, string med)
        {
            if (!this || !a || a != s12Dog) return;
            MadMax.Story.Story.SetFlag("s12:treated");
            a.limp = 0f;
            Toast("PENNY PUTS HER WEIGHT ON THE PAW AGAIN. A SHARD OF GLASS, OUT. SHE LICKS YOUR HAND ONCE, FORMALLY");
            Q5Note("s12:treated");
        }

        partial void Tick_S12()
        {
            float dt = Time.deltaTime;
            if (!s12Hooked) { s12Hooked = true; Animal.Treated += S12Treated; }
            if ((s12T += dt) < 0.25f) return;
            float step = s12T; s12T = 0f;
            bool adopted = MadMax.Story.Story.Flag("s12:adopted"), home = MadMax.Story.Story.Flag("s12:home");
            if (!s12Dog && !adopted)
            {
                foreach (var a in Animal.All) if (a && a.key == S12Key) { s12Dog = a; break; }
                if (!s12Dog && StoryAnchors.Has("etta")) S12Spawn(home ? Q5At("s12_home", new Vector3(0.8f, 0f, -0.6f)) : Q5At("etta", S12Bench));
            }
            var dog = s12Dog;
            if (!dog || !dog.Alive) return;
            // until treated the paw stays lame (it would heal over days otherwise)
            if (!MadMax.Story.Story.Flag("s12:treated")) { dog.limp = Mathf.Max(dog.limp, 0.6f); dog.health = Mathf.Min(dog.health, dog.Def.health * 0.6f); }
            // her routine: the bench where Etta leaves scraps, then the platform end, looking down the line
            if (!dog.owned && !home && StoryAnchors.Has("etta") && (s12Routine -= step) <= 0f)
            {
                s12Routine = 12f;
                s12AtEdge = !s12AtEdge;
                dog.home = Q5At("etta", s12AtEdge ? S12Edge : S12Bench);
            }
            // watching her a while
            if (!MadMax.Story.Story.StepDone("S12", "watch") && !Current && Q5Flat(Player.transform.position, dog.transform.position) < 25f && (s12Watch += step) >= 15f)
            {
                Journal.Add("JOB", "THE DOG TROTS TO THE PLATFORM END WHENEVER THE WIND RATTLES THE SIGNAL WIRE, LOOKS DOWN THE LINE, AND LIMPS BACK TO THE BENCH WHERE ETTA LEAVES SCRAPS. SHE FAVOURS HER LEFT FOREPAW.");
                Toast("SHE'S WAITING FOR A TRAIN. AND SHE'S LIMPING ON HER LEFT FOREPAW");
                Q5Note("s12:watched");
            }
            if (MadMax.Story.Story.StepDone("S12", "clue") && Q5Route("S12", "clue", "READ") && !MadMax.Story.Story.Flag("s12:board"))
            {
                MadMax.Story.Story.SetFlag("s12:board");
                Journal.Add("JOB", "A NOTICE ON THE BOARD, WEATHERED: 'W. OBER, SIGNALMAN, RETIRED. FORWARD POST TO HIS NIECE'S, UP THE ROAD.' A DRAWING OF A DOG IN THE CORNER.");
            }
            // the decision has landed: move Penny
            if (MadMax.Story.Story.StepDone("S12", "decide") && !MadMax.Story.Story.StepDone("S12", "settle")) S12Settle(dog);
        }

        void S12Settle(Animal dog)
        {
            if (Q5Route("S12", "decide", "SHE'S COMING HOME"))
            {
                // she follows you to Walt's porch on foot
                if (!dog.owned) { dog.owned = true; dog.order = 1; dog.town = -1; Toast("FETCH PENNY FROM THE STATION: SHE'LL FOLLOW YOU TO WALT'S PORCH"); }
                if (StoryAnchors.Has("s12_home") && Q5Flat(dog.transform.position, StoryAnchors.Get("s12_home")) < 9f)
                {
                    dog.owned = false; dog.order = 0; dog.town = 0; dog.home = Q5At("s12_home", new Vector3(0.8f, 0f, -0.6f));
                    MadMax.Story.Story.SetFlag("s12:home");
                    Q5Payoff("S12", "PENNY STOPPED WAITING AT PLATFORM THREE. SHE SLEEPS BY WALT'S STOVE NOW, AND HIS NIECE PRETENDS NOT TO FEED HER.");
                    Toast("PENNY GOES STRAIGHT TO THE WARM SPOT BY THE STOVE");
                    Q5Note("s12:home");
                }
            }
            else if (Q5Route("S12", "decide", "SHE KEEPS HER PLATFORM"))
            {
                MadMax.Story.Story.SetFlag("s12:shared");
                Q5Payoff("S12", "PENNY STILL MEETS EVERY TRAIN THAT NEVER COMES, BUT NOW SHE HAS ETTA'S SCRAPS ON WEEKDAYS AND WALT'S PORCH ON SUNDAYS.");
                Q5Note("s12:shared");
            }
            else if (Q5Route("S12", "decide", "THEN SHE COMES WITH ME"))
            {
                dog.town = -1;
                dog.owned = true; dog.order = 1; dog.home = dog.transform.position;
                dog.born = MadMax.World.DayNight.Day - 100; dog.fedDay = MadMax.World.DayNight.Day;
                MadMax.Animals.AnimalDirector.Instance?.Adopt(dog);
                MadMax.Story.Story.SetFlag("s12:adopted");
                Inventory.TakeItem("story_penny_leash");
                Q5Payoff("S12", "PENNY STOPPED WAITING AT PLATFORM THREE. SHE WAITS AT YOUR DOORS NOW, WITH WALT'S BLESSING, AND YOU TAKE HER BY HIS PORCH ON SUNDAYS.");
                Toast("PENNY IS YOURS, WITH WALT'S BLESSING: FETCH HER FROM THE STATION ([T] ON HER: ORDERS)");
                Q5Note("s12:adopted");
            }
        }
    }
}
