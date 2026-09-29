using System.Collections.Generic;
using MadMax.Voxel;
using MadMax.World;

namespace MadMax.Animals
{
    public enum BodyPlan { Quadruped, Bird, Snake }
    /// <summary>How a species reacts to people: prey flee, chargers stand and charge, predators hunt in packs,
    /// scavengers circle the dead, livestock stay calm, vermin scatter, lurkers lie still and strike.</summary>
    public enum Nature { Prey, Charger, Predator, Scavenger, Livestock, Vermin, Lurker }

    /// <summary>A colour ramp picked per voxel (wraps a Pal ramp).</summary>
    public struct Color32Ramp
    {
        public UnityEngine.Color32[] ramp; public int bias;
        public Color32Ramp(UnityEngine.Color32[] r, int b = 1) { ramp = r; bias = b; }
        public VoxMat Mat(int seed) => Pal.Ramp(ramp, bias, seed);
    }

    /// <summary>One species (data). Proportions are in voxels of 0.08 m × <see cref="scale"/>.</summary>
    public class AnimalDef
    {
        public string id, name, pest;
        public BodyPlan plan = BodyPlan.Quadruped;
        public Nature nature;
        // body: torso length / depth / width, leg length, neck length, head size, snout, tail
        public int len = 14, depth = 6, width = 5, leg = 8, neck = 4, head = 4, snout = 3, tail = 4;
        public float scale = 1f;
        public Color32Ramp Coat, Belly, Accent;
        public string features = "";         // horns tusks mane ears_long ears_up comb wattle udder spots beard glow rattle curl bald
        public float walk = 1.2f, run = 6f, health = 20f, bite = 6f, mass = 50f, reach = 1.2f;
        public float sight = 30f, hearing = 40f, smell = 50f;
        public int herdMin = 1, herdMax = 1;
        public Biome[] biomes = new Biome[0];
        public float density;                // herds per 200 m cell
        public bool nocturnal, flies;
        public (string id, int min, int max)[] drops = new (string, int, int)[0];
        public string product; public int productPerDay;             // eggs, milk
        public bool tameable, rideable, guard;
        public float adultDays = 3f, feedPerDay = 1f;                 // growth; trough feed eaten per day
        public string[] likes = new string[0];                       // hand-fed treats (taming, care)
        public string young;                                          // inventory item that releases a young one
        public bool Has(string f) => features.Contains(f);
        public bool Livestock => nature == Nature.Livestock;
        /// <summary>Shoulder height (m).</summary>
        public float Height => (leg + depth) * VoxelMesher.DefaultSize * scale;
    }

    /// <summary>The species of the wastes (roadmap 23): wild (dogs, wolves, boars and rad-boars, antelope, wild horses,
    /// vultures, snakes, rats) and farm (chickens, goats, cows, pigs, guard dogs).</summary>
    public static class AnimalLibrary
    {
        static Dictionary<string, AnimalDef> defs;

        public static AnimalDef Get(string id) { Ensure(); return id != null && defs.TryGetValue(id, out var d) ? d : null; }
        public static IEnumerable<AnimalDef> All { get { Ensure(); return defs.Values; } }

        static Color32Ramp R(UnityEngine.Color32[] r, int b = 1) => new Color32Ramp(r, b);

        static void Ensure()
        {
            if (defs != null) return;
            defs = new Dictionary<string, AnimalDef>();
            var meat = "food_meat_raw"; var hide = "res:" + (int)MadMax.Items.ResourceType.Hide;
            foreach (var d in new[]
            {
                // ---- wild
                new AnimalDef { id = "horse", name = "WILD HORSE", nature = Nature.Prey, len = 22, depth = 8, width = 6, leg = 12, neck = 9, head = 5, snout = 4, tail = 9,
                    Coat = R(Pal.Wood, 2), Belly = R(Pal.Wood, 3), Accent = R(Pal.Black, 1), features = "mane ears_up",
                    walk = 1.6f, run = 12f, health = 110f, bite = 10f, mass = 450f, sight = 40f, hearing = 45f, smell = 55f, herdMin = 3, herdMax = 6,
                    biomes = new[] { Biome.Desert, Biome.Forest }, density = 0.25f, drops = new[] { (meat, 6, 9), (hide, 3, 4), ("misc_bone", 2, 4) },
                    tameable = true, rideable = true, likes = new[] { "food_apple", "food_carrot", "food_sugar", "food_corn", "crop_wheat", "food_beet" }, feedPerDay = 2f },
                new AnimalDef { id = "antelope", name = "ANTELOPE", nature = Nature.Prey, len = 14, depth = 5, width = 4, leg = 10, neck = 6, head = 4, snout = 3, tail = 2,
                    Coat = R(Pal.Sand, 3), Belly = R(Pal.Cream, 2), Accent = R(Pal.Black, 2), features = "horns ears_up",
                    walk = 1.3f, run = 13f, health = 40f, mass = 60f, sight = 45f, hearing = 50f, smell = 60f, herdMin = 4, herdMax = 8,
                    biomes = new[] { Biome.Desert, Biome.Tropical, Biome.Forest }, density = 0.45f, drops = new[] { (meat, 3, 5), (hide, 1, 2), ("misc_bone", 1, 2), ("trophy_horns", 0, 1) } },
                new AnimalDef { id = "boar", name = "BOAR", pest = "boar", nature = Nature.Charger, len = 14, depth = 7, width = 6, leg = 5, neck = 1, head = 5, snout = 3, tail = 2,
                    Coat = R(Pal.Metal, 2), Belly = R(Pal.Black, 2), Accent = R(Pal.Cream, 3), features = "tusks ears_up",
                    walk = 1.1f, run = 7.5f, health = 70f, bite = 16f, mass = 110f, reach = 1.4f, sight = 22f, hearing = 35f, smell = 50f, herdMin = 1, herdMax = 3,
                    biomes = new[] { Biome.Forest, Biome.Tropical }, density = 0.35f, drops = new[] { (meat, 4, 6), (hide, 1, 2), ("misc_bone", 1, 2), ("trophy_tusks", 0, 1) } },
                new AnimalDef { id = "radboar", name = "RAD-BOAR", pest = "boar", nature = Nature.Charger, len = 16, depth = 8, width = 7, leg = 5, neck = 1, head = 6, snout = 3, tail = 2, scale = 1.15f,
                    Coat = R(Pal.Moss, 1), Belly = R(Pal.Black, 2), Accent = R(Pal.Cream, 4), features = "tusks ears_up glow",
                    walk = 1.1f, run = 8f, health = 130f, bite = 24f, mass = 180f, reach = 1.6f, sight = 22f, hearing = 35f, smell = 50f, herdMin = 1, herdMax = 2,
                    biomes = new[] { Biome.Nuclear }, density = 0.5f, drops = new[] { (meat, 4, 7), (hide, 2, 3), ("misc_bone", 1, 3), ("trophy_tusks", 1, 1) } },
                new AnimalDef { id = "dog", name = "WILD DOG", pest = "dog", nature = Nature.Predator, len = 10, depth = 4, width = 3, leg = 6, neck = 3, head = 3, snout = 3, tail = 5, scale = 0.85f,
                    Coat = R(Pal.Sand, 2), Belly = R(Pal.Cream, 1), Accent = R(Pal.Black, 1), features = "ears_up",
                    walk = 1.4f, run = 9f, health = 35f, bite = 9f, mass = 25f, sight = 35f, hearing = 50f, smell = 70f, herdMin = 3, herdMax = 6,
                    biomes = new[] { Biome.Desert, Biome.Town, Biome.City, Biome.Nuclear }, density = 0.3f, nocturnal = true, drops = new[] { (meat, 1, 2), (hide, 1, 1), ("misc_bone", 1, 1) },
                    tameable = true, guard = true, likes = new[] { "food_meat_raw", "food_meat_cooked", "food_jerky", "misc_bone" } },
                new AnimalDef { id = "wolf", name = "WOLF", pest = "wolf", nature = Nature.Predator, len = 13, depth = 5, width = 4, leg = 7, neck = 3, head = 4, snout = 3, tail = 6,
                    Coat = R(Pal.Fur, 2), Belly = R(Pal.Cream, 1), Accent = R(Pal.Black, 1), features = "ears_up",
                    walk = 1.5f, run = 10f, health = 50f, bite = 13f, mass = 45f, sight = 40f, hearing = 60f, smell = 80f, herdMin = 3, herdMax = 5,
                    biomes = new[] { Biome.Forest }, density = 0.3f, nocturnal = true, drops = new[] { (meat, 2, 3), (hide, 1, 2), ("misc_bone", 1, 2), ("trophy_pelt", 0, 1) } },
                new AnimalDef { id = "vulture", name = "VULTURE", nature = Nature.Scavenger, plan = BodyPlan.Bird, len = 8, depth = 5, width = 5, leg = 3, neck = 3, head = 2, snout = 2, tail = 3,
                    Coat = R(Pal.Black, 2), Belly = R(Pal.Metal, 2), Accent = R(Pal.Pink, 2), features = "bald",
                    walk = 0.8f, run = 9f, health = 15f, mass = 8f, sight = 28f, hearing = 30f, smell = 0f, flies = true, drops = new[] { (meat, 1, 1), ("misc_feather", 3, 6) } },
                new AnimalDef { id = "snake", name = "RATTLESNAKE", nature = Nature.Lurker, plan = BodyPlan.Snake, len = 18, depth = 1, width = 1, leg = 0, head = 2, tail = 0,
                    Coat = R(Pal.Sand, 1), Belly = R(Pal.Cream, 2), Accent = R(Pal.Olive, 1), features = "rattle",
                    walk = 0.5f, run = 1.6f, health = 8f, bite = 7f, mass = 2f, reach = 1.3f, sight = 6f, hearing = 8f, smell = 0f, herdMin = 1, herdMax = 1,
                    biomes = new[] { Biome.Desert, Biome.Tropical }, density = 0.6f, drops = new[] { (meat, 1, 1), (hide, 0, 1) } },
                new AnimalDef { id = "rat", name = "GIANT RAT", pest = "rat", nature = Nature.Vermin, len = 7, depth = 3, width = 3, leg = 2, neck = 1, head = 2, snout = 2, tail = 7, scale = 0.7f,
                    Coat = R(Pal.Metal, 2), Belly = R(Pal.Fur, 3), Accent = R(Pal.Pink, 1), features = "ears_up",
                    walk = 0.9f, run = 5f, health = 8f, bite = 4f, mass = 3f, reach = 0.8f, sight = 12f, hearing = 20f, smell = 25f, herdMin = 3, herdMax = 7,
                    biomes = new[] { Biome.Town, Biome.City, Biome.Nuclear }, density = 0.7f, nocturnal = true, drops = new[] { (meat, 0, 1) } },
                // ---- farm
                new AnimalDef { id = "chicken", name = "CHICKEN", nature = Nature.Livestock, plan = BodyPlan.Bird, len = 6, depth = 5, width = 4, leg = 3, neck = 2, head = 2, snout = 1, tail = 3,
                    Coat = R(Pal.Cream, 3), Belly = R(Pal.Cream, 2), Accent = R(Pal.Crimson, 3), features = "comb wattle",
                    walk = 0.8f, run = 3.5f, health = 8f, mass = 2.5f, sight = 12f, hearing = 15f, herdMin = 3, herdMax = 6, drops = new[] { (meat, 1, 1), ("misc_feather", 2, 4) },
                    product = "food_egg", productPerDay = 1, adultDays = 2f, feedPerDay = 0.25f, young = "animal_chick", likes = new[] { "food_corn", "crop_wheat", "food_sunseeds", "bait_insects" } },
                new AnimalDef { id = "goat", name = "GOAT", nature = Nature.Livestock, len = 11, depth = 5, width = 4, leg = 7, neck = 4, head = 3, snout = 2, tail = 2,
                    Coat = R(Pal.Cream, 2), Belly = R(Pal.Cream, 1), Accent = R(Pal.Metal, 2), features = "horns beard ears_long udder",
                    walk = 1.1f, run = 6f, health = 35f, mass = 45f, sight = 20f, hearing = 25f, herdMin = 2, herdMax = 4, drops = new[] { (meat, 2, 3), (hide, 1, 1), ("misc_bone", 1, 1) },
                    product = "drink_milk", productPerDay = 1, adultDays = 3f, feedPerDay = 0.6f, young = "animal_kid", likes = new[] { "food_cabbage", "food_carrot", "food_apple", "crop_wheat", "food_beet" } },
                new AnimalDef { id = "cow", name = "COW", nature = Nature.Livestock, len = 20, depth = 9, width = 8, leg = 9, neck = 3, head = 5, snout = 3, tail = 7,
                    Coat = R(Pal.Cream, 3), Belly = R(Pal.Cream, 2), Accent = R(Pal.Black, 1), features = "horns spots udder ears_long",
                    walk = 1f, run = 5f, health = 120f, bite = 8f, mass = 550f, sight = 20f, hearing = 25f, herdMin = 2, herdMax = 4, drops = new[] { (meat, 7, 10), (hide, 3, 4), ("misc_bone", 2, 4), ("trophy_skull", 0, 1) },
                    product = "drink_milk", productPerDay = 3, adultDays = 5f, feedPerDay = 2f, young = "animal_calf", likes = new[] { "crop_wheat", "food_corn", "food_cabbage", "food_beet", "food_pumpkin" } },
                new AnimalDef { id = "pig", name = "PIG", nature = Nature.Livestock, len = 13, depth = 7, width = 6, leg = 4, neck = 1, head = 4, snout = 2, tail = 2,
                    Coat = R(Pal.Pink, 2), Belly = R(Pal.Pink, 3), Accent = R(Pal.Pink, 1), features = "ears_long curl",
                    walk = 0.9f, run = 5f, health = 60f, mass = 120f, sight = 15f, hearing = 20f, herdMin = 2, herdMax = 3, drops = new[] { (meat, 6, 9), (hide, 1, 2), ("misc_bone", 1, 2) },
                    adultDays = 3f, feedPerDay = 1f, young = "animal_piglet", likes = new[] { "food_rotten", "food_potato", "food_pumpkin", "food_corn", "food_apple" } },
                new AnimalDef { id = "guarddog", name = "GUARD DOG", nature = Nature.Livestock, len = 11, depth = 5, width = 4, leg = 7, neck = 3, head = 4, snout = 3, tail = 5, scale = 0.9f,
                    Coat = R(Pal.Black, 3), Belly = R(Pal.Olive, 3), Accent = R(Pal.Olive, 2), features = "ears_up",
                    walk = 1.5f, run = 9.5f, health = 70f, bite = 14f, mass = 35f, sight = 40f, hearing = 60f, smell = 80f, drops = new[] { (meat, 1, 2), (hide, 1, 1) },
                    guard = true, adultDays = 3f, feedPerDay = 0.5f, young = "animal_puppy", likes = new[] { "food_meat_raw", "food_meat_cooked", "food_jerky", "misc_bone" } },
            }) defs[d.id] = d;
        }

        /// <summary>Wild species that live in a biome.</summary>
        public static void WildIn(Biome b, List<AnimalDef> into)
        {
            Ensure();
            into.Clear();
            foreach (var d in defs.Values)
                if (!d.Livestock && d.density > 0f && System.Array.IndexOf(d.biomes, b) >= 0) into.Add(d);
        }

        /// <summary>The species a young-animal item releases.</summary>
        public static AnimalDef ForYoung(string item)
        {
            Ensure();
            foreach (var d in defs.Values) if (d.young == item) return d;
            return null;
        }
    }
}
