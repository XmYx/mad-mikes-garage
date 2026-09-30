using System.Collections.Generic;
using MadMax.Voxel;
using MadMax.World;

namespace MadMax.Animals
{
    public static partial class AnimalLibrary
    {
        /// <summary>More of the wastes' wildlife (user additions): jackrabbits, coyotes, deer, bears, armadillos,
        /// raccoons; lizards (quick desert lizards, venomous gila monsters, the huge rad-lizard); cottonmouths and
        /// pythons; crows and wild turkeys; and the arthropods — scorpions and their glowing rad-scorpion cousins,
        /// tarantulas, giant cave spiders, rad-roaches and dung beetles. Venomous bites (feature "venom") make the
        /// player sick until it passes or antivenom is taken; chitin, silk, venom, shells and trophies feed crafting.</summary>
        static IEnumerable<AnimalDef> Wildlife()
        {
            var meat = "food_meat_raw"; var hide = "res:" + (int)MadMax.Items.ResourceType.Hide;
            var any = new[] { Biome.Desert, Biome.Forest, Biome.Tropical, Biome.Village };
            // ---- mammals
            yield return new AnimalDef { id = "jackrabbit", name = "JACKRABBIT", nature = Nature.Prey, len = 6, depth = 4, width = 3, leg = 3, neck = 1, head = 3, snout = 1, tail = 1, scale = 0.8f,
                Coat = R(Pal.Sand, 2), Belly = R(Pal.Cream, 2), Accent = R(Pal.Black, 1), features = "ears_tall",
                walk = 1f, run = 11f, health = 6f, mass = 3f, sight = 25f, hearing = 45f, smell = 30f, herdMin = 1, herdMax = 2,
                biomes = new[] { Biome.Desert, Biome.Forest, Biome.Village }, density = 0.9f, drops = new[] { (meat, 1, 1), (hide, 0, 1) },
                young = "animal_rabbit", adultDays = 2f, feedPerDay = 0.2f, likes = new[] { "food_carrot", "food_cabbage", "crop_wheat", "food_beet" } };   // a live one from a cage trap can be kept
            yield return new AnimalDef { id = "coyote", name = "COYOTE", pest = "dog", nature = Nature.Predator, len = 11, depth = 4, width = 3, leg = 7, neck = 3, head = 3, snout = 3, tail = 5, scale = 0.85f,
                Coat = R(Pal.Sand, 1), Belly = R(Pal.Cream, 2), Accent = R(Pal.Fur, 1), features = "ears_up",
                walk = 1.4f, run = 10f, health = 30f, bite = 8f, mass = 15f, sight = 35f, hearing = 55f, smell = 70f, herdMin = 2, herdMax = 4,
                biomes = new[] { Biome.Desert }, density = 0.3f, nocturnal = true, drops = new[] { (meat, 1, 2), (hide, 1, 1) } };
            yield return new AnimalDef { id = "deer", name = "MULE DEER", nature = Nature.Prey, len = 16, depth = 6, width = 5, leg = 12, neck = 6, head = 4, snout = 3, tail = 2,
                Coat = R(Pal.Wood, 2), Belly = R(Pal.Cream, 2), Accent = R(Pal.Cream, 3), features = "antlers ears_up",
                walk = 1.3f, run = 12f, health = 50f, mass = 80f, sight = 40f, hearing = 55f, smell = 65f, herdMin = 2, herdMax = 5,
                biomes = new[] { Biome.Forest, Biome.Village, Biome.Tundra }, density = 0.4f, drops = new[] { (meat, 4, 6), (hide, 1, 2), ("misc_bone", 1, 2), ("trophy_antlers", 0, 1) } };
            yield return new AnimalDef { id = "bear", name = "BLACK BEAR", nature = Nature.Charger, len = 20, depth = 11, width = 9, leg = 7, neck = 2, head = 6, snout = 3, tail = 1,
                Coat = R(Pal.Black, 2), Belly = R(Pal.Fur, 1), Accent = R(Pal.Wood, 2), features = "ears_up",
                walk = 1.1f, run = 9f, health = 260f, bite = 34f, mass = 300f, reach = 1.8f, sight = 25f, hearing = 40f, smell = 90f, herdMin = 1, herdMax = 1,
                biomes = new[] { Biome.Forest, Biome.Tundra }, density = 0.07f, drops = new[] { (meat, 8, 12), (hide, 4, 6), ("misc_bone", 2, 4), ("trophy_bearskin", 1, 1) } };
            yield return new AnimalDef { id = "armadillo", name = "ARMADILLO", nature = Nature.Prey, len = 8, depth = 4, width = 4, leg = 2, neck = 1, head = 2, snout = 2, tail = 4, scale = 0.8f,
                Coat = R(Pal.Cream, 1), Belly = R(Pal.Pink, 2), Accent = R(Pal.Metal, 3), features = "shell ears_up",
                walk = 0.6f, run = 3.5f, health = 12f, mass = 5f, sight = 8f, hearing = 20f, smell = 40f, herdMin = 1, herdMax = 1,
                biomes = new[] { Biome.Desert, Biome.Tropical }, density = 0.45f, nocturnal = true, drops = new[] { (meat, 1, 1), ("misc_shell", 1, 1) } };
            yield return new AnimalDef { id = "raccoon", name = "RACCOON", nature = Nature.Vermin, len = 8, depth = 4, width = 4, leg = 3, neck = 1, head = 3, snout = 2, tail = 5, scale = 0.8f,
                Coat = R(Pal.Metal, 2), Belly = R(Pal.Cream, 1), Accent = R(Pal.Black, 1), features = "mask rings ears_up",
                walk = 0.9f, run = 5f, health = 12f, bite = 5f, mass = 8f, reach = 0.8f, sight = 15f, hearing = 30f, smell = 50f, herdMin = 1, herdMax = 3,
                biomes = new[] { Biome.Forest, Biome.Village, Biome.Town }, density = 0.45f, nocturnal = true, drops = new[] { (meat, 1, 1), (hide, 1, 1) } };
            // ---- lizards
            yield return new AnimalDef { id = "lizard", name = "COLLARED LIZARD", nature = Nature.Prey, len = 7, depth = 2, width = 3, leg = 1, neck = 1, head = 2, snout = 2, tail = 8, scale = 0.6f,
                Coat = R(Pal.Olive, 2), Belly = R(Pal.Cream, 2), Accent = R(Pal.Ochre, 3), features = "lizard",
                walk = 0.9f, run = 6f, health = 3f, mass = 0.3f, sight = 14f, hearing = 12f, smell = 5f, herdMin = 1, herdMax = 1,
                biomes = new[] { Biome.Desert, Biome.Tropical, Biome.Nuclear }, density = 1.1f, drops = new[] { (meat, 0, 1) } };
            yield return new AnimalDef { id = "gila", name = "GILA MONSTER", nature = Nature.Lurker, len = 9, depth = 3, width = 4, leg = 1, neck = 1, head = 3, snout = 1, tail = 5, scale = 0.75f,
                Coat = R(Pal.Black, 1), Belly = R(Pal.Black, 2), Accent = R(Pal.Ochre, 3), features = "lizard beads venom",
                walk = 0.4f, run = 1.2f, health = 14f, bite = 8f, mass = 2f, reach = 0.9f, sight = 6f, hearing = 8f, smell = 12f, herdMin = 1, herdMax = 1,
                biomes = new[] { Biome.Desert }, density = 0.25f, drops = new[] { (meat, 1, 1), ("misc_venom", 0, 1) } };
            yield return new AnimalDef { id = "radlizard", name = "RAD-LIZARD", pest = "radlizard", nature = Nature.Predator, len = 22, depth = 6, width = 8, leg = 4, neck = 2, head = 6, snout = 5, tail = 18,
                Coat = R(Pal.Moss, 2), Belly = R(Pal.Cream, 2), Accent = R(Pal.Moss, 4), features = "lizard glow",
                walk = 1.1f, run = 8f, health = 140f, bite = 22f, mass = 150f, reach = 1.8f, sight = 30f, hearing = 30f, smell = 60f, herdMin = 1, herdMax = 2,
                biomes = new[] { Biome.Nuclear, Biome.Desert }, density = 0.1f, drops = new[] { (meat, 5, 8), (hide, 2, 3), ("misc_chitin", 1, 2), ("misc_bone", 1, 3) } };
            // ---- snakes
            yield return new AnimalDef { id = "cottonmouth", name = "COTTONMOUTH", nature = Nature.Lurker, plan = BodyPlan.Snake, len = 16, depth = 1, width = 1, leg = 0, head = 2, tail = 0,
                Coat = R(Pal.Olive, 1), Belly = R(Pal.Cream, 3), Accent = R(Pal.Black, 1), features = "venom",
                walk = 0.5f, run = 1.8f, health = 9f, bite = 9f, mass = 2f, reach = 1.3f, sight = 6f, hearing = 8f, smell = 0f,
                biomes = new[] { Biome.Forest, Biome.Tropical }, density = 0.35f, drops = new[] { (meat, 1, 1), (hide, 0, 1), ("misc_venom", 0, 1) } };
            yield return new AnimalDef { id = "python", name = "BURMESE PYTHON", nature = Nature.Lurker, plan = BodyPlan.Snake, len = 22, depth = 1, width = 1, leg = 0, head = 2, tail = 0, scale = 1.8f,
                Coat = R(Pal.Sand, 2), Belly = R(Pal.Cream, 3), Accent = R(Pal.Wood, 1),
                walk = 0.5f, run = 1.5f, health = 45f, bite = 12f, mass = 30f, reach = 1.6f, sight = 6f, hearing = 10f, smell = 0f,
                biomes = new[] { Biome.Tropical }, density = 0.15f, drops = new[] { (meat, 3, 4), (hide, 1, 2) } };
            // ---- birds
            yield return new AnimalDef { id = "crow", name = "CROW", pest = "crow", nature = Nature.Vermin, plan = BodyPlan.Bird, len = 5, depth = 3, width = 3, leg = 2, neck = 1, head = 2, snout = 2, tail = 3, scale = 0.8f,
                Coat = R(Pal.Black, 1), Belly = R(Pal.Black, 2), Accent = R(Pal.Navy, 0),
                walk = 0.7f, run = 8f, health = 4f, mass = 0.5f, sight = 30f, hearing = 35f, smell = 0f, flies = true, herdMin = 4, herdMax = 8,
                biomes = new[] { Biome.Desert, Biome.Forest, Biome.Village, Biome.Town, Biome.City, Biome.Nuclear }, density = 0.5f, drops = new[] { ("misc_feather", 1, 3), (meat, 0, 1) } };
            yield return new AnimalDef { id = "turkey", name = "WILD TURKEY", nature = Nature.Prey, plan = BodyPlan.Bird, len = 8, depth = 6, width = 5, leg = 5, neck = 4, head = 2, snout = 1, tail = 4,
                Coat = R(Pal.Wood, 1), Belly = R(Pal.Black, 2), Accent = R(Pal.Crimson, 2), features = "wattle fan",
                walk = 0.9f, run = 6f, health = 12f, mass = 8f, sight = 30f, hearing = 35f, smell = 0f, herdMin = 3, herdMax = 6,
                biomes = new[] { Biome.Forest, Biome.Village }, density = 0.3f, drops = new[] { (meat, 2, 4), ("misc_feather", 3, 6) } };
            // ---- arthropods
            yield return new AnimalDef { id = "scorpion", name = "BARK SCORPION", nature = Nature.Lurker, plan = BodyPlan.Arthropod, len = 6, depth = 2, width = 3, leg = 1, head = 1, tail = 5, scale = 0.55f,
                Coat = R(Pal.Ochre, 2), Belly = R(Pal.Sand, 3), Accent = R(Pal.Wood, 1), features = "pincers stinger venom",
                walk = 0.4f, run = 1.6f, health = 3f, bite = 5f, mass = 0.05f, reach = 0.6f, sight = 3f, hearing = 5f, smell = 0f, herdMin = 1, herdMax = 2,
                biomes = new[] { Biome.Desert, Biome.Nuclear }, density = 0.9f, nocturnal = true, drops = new[] { ("misc_venom", 0, 1) } };
            yield return new AnimalDef { id = "radscorpion", name = "RAD-SCORPION", pest = "radscorpion", nature = Nature.Charger, plan = BodyPlan.Arthropod, len = 14, depth = 4, width = 8, leg = 3, head = 2, tail = 12, scale = 1.25f,
                Coat = R(Pal.Rust, 1), Belly = R(Pal.Ochre, 2), Accent = R(Pal.Black, 1), features = "pincers stinger venom glow",
                walk = 0.9f, run = 6.5f, health = 120f, bite = 20f, mass = 90f, reach = 2f, sight = 16f, hearing = 30f, smell = 20f, herdMin = 1, herdMax = 2,
                biomes = new[] { Biome.Nuclear, Biome.Desert }, density = 0.12f, drops = new[] { ("misc_venom", 1, 2), ("misc_chitin", 2, 4), (meat, 2, 3) } };
            yield return new AnimalDef { id = "tarantula", name = "TARANTULA", nature = Nature.Lurker, plan = BodyPlan.Arthropod, len = 4, depth = 2, width = 3, leg = 2, head = 1, tail = 1, scale = 0.55f,
                Coat = R(Pal.Fur, 1), Belly = R(Pal.Black, 1), Accent = R(Pal.Ochre, 2), features = "hairy",
                walk = 0.3f, run = 1.2f, health = 3f, bite = 3f, mass = 0.05f, reach = 0.5f, sight = 3f, hearing = 5f, smell = 0f, herdMin = 1, herdMax = 1,
                biomes = new[] { Biome.Desert, Biome.Tropical }, density = 0.5f, nocturnal = true, drops = new[] { ("misc_silk", 0, 1) } };
            yield return new AnimalDef { id = "cavespider", name = "GIANT CAVE SPIDER", pest = "spider", nature = Nature.Predator, plan = BodyPlan.Arthropod, len = 10, depth = 5, width = 8, leg = 4, head = 2, tail = 1, scale = 1.2f,
                Coat = R(Pal.Black, 2), Belly = R(Pal.Black, 1), Accent = R(Pal.Crimson, 1), features = "hairy venom",
                walk = 1f, run = 7f, health = 80f, bite = 16f, mass = 40f, reach = 1.5f, sight = 14f, hearing = 35f, smell = 30f, herdMin = 1, herdMax = 2,
                biomes = new[] { Biome.Forest, Biome.Tropical, Biome.Nuclear }, density = 0.08f, nocturnal = true, drops = new[] { ("misc_silk", 2, 4), ("misc_chitin", 1, 2), ("misc_venom", 1, 1) } };
            yield return new AnimalDef { id = "radroach", name = "RAD-ROACH", pest = "rat", nature = Nature.Vermin, plan = BodyPlan.Arthropod, len = 8, depth = 2, width = 5, leg = 2, head = 1, tail = 1, scale = 0.8f,
                Coat = R(Pal.Wood, 1), Belly = R(Pal.Wood, 3), Accent = R(Pal.Rust, 2), features = "legs6 antennae",
                walk = 0.8f, run = 5.5f, health = 8f, bite = 3f, mass = 1f, reach = 0.7f, sight = 10f, hearing = 20f, smell = 30f, herdMin = 3, herdMax = 6,
                biomes = new[] { Biome.Town, Biome.City, Biome.Nuclear }, density = 0.6f, nocturnal = true, drops = new[] { ("food_bugmeat", 1, 1) } };
            yield return new AnimalDef { id = "crab", name = "SHORE CRAB", nature = Nature.Prey, plan = BodyPlan.Arthropod, len = 5, depth = 2, width = 6, leg = 1, head = 1, tail = 1, scale = 0.6f,
                Coat = R(Pal.Crimson, 2), Belly = R(Pal.Cream, 3), Accent = R(Pal.Rust, 1), features = "pincers",
                walk = 0.35f, run = 1.8f, health = 3f, bite = 1f, mass = 0.3f, reach = 0.4f, sight = 6f, hearing = 8f, smell = 0f, herdMin = 1, herdMax = 1,
                density = 0f, drops = new[] { ("food_fish_raw", 0, 1) } };                                                   // put on the beaches by World/SeaLife
            yield return new AnimalDef { id = "beetle", name = "DUNG BEETLE", nature = Nature.Prey, plan = BodyPlan.Arthropod, len = 3, depth = 2, width = 3, leg = 1, head = 1, tail = 1, scale = 0.5f,
                Coat = R(Pal.Black, 1), Belly = R(Pal.Black, 2), Accent = R(Pal.Navy, 1), features = "legs6 shell",
                walk = 0.25f, run = 0.8f, health = 1f, mass = 0.01f, sight = 2f, hearing = 3f, smell = 0f, herdMin = 1, herdMax = 1,
                biomes = any, density = 1f, drops = new[] { ("bait_insects", 1, 2) } };
        }
    }
}
