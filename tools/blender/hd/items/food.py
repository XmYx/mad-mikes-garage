"""HD food and drink items (FoodLibrary: every food_* / drink_* id) as world / held models at real size. Origin =
resting point (bottom centre). The game's icon colour (FoodDef.color) drives the dominant colour of each model.

blender -b -P tools/blender/hd/items/food.py [-- ids]"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "parts_all"))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pa  # noqa: E402
from pa import M  # noqa: E402
import ishapes as I  # noqa: E402

F = "food"

FOODS = {  # id: (name, game colour) from FoodLibrary
    "drink_bark_tea": ("WILLOW-BARK TEA", "6a4a24"), "drink_beer": ("BEER", "d8a030"), "drink_cider": ("CIDER", "e0c060"),
    "drink_mead": ("MEAD", "d8a040"), "drink_milk": ("MILK", "fbf8ee"), "drink_soda": ("OLD SODA", "b02818"), "drink_tea": ("HERB TEA", "8a6a2a"),
    "drink_water": ("BOTTLED WATER", "8cbcd6"), "food_apple": ("APPLE", "c02c20"), "food_apple_pie": ("APPLE PIE", "c88040"),
    "food_beet": ("SUGAR BEET", "7a2440"), "food_berries": ("BERRIES", "6a1c50"), "food_bread": ("BREAD", "c89050"),
    "food_bugmeat": ("BUG MEAT", "7a8a4a"), "food_bug_skewer": ("FRIED BUG SKEWER", "8a6a2a"), "food_cabbage": ("CABBAGE", "78a040"),
    "food_can": ("CANNED BEANS", "b0a080"), "food_can_fish": ("TINNED FISH", "b0b8c0"), "food_can_fruit": ("TINNED FRUIT", "d06040"),
    "food_can_meat": ("TINNED MEAT", "a05a3a"), "food_can_stew": ("TINNED STEW", "9a6a3a"), "food_can_veg": ("TINNED VEGETABLES", "7a9a3a"),
    "food_carrot": ("CARROT", "e07020"), "food_cheese": ("CHEESE", "e0ac40"), "food_cheese_smoked": ("SMOKED CHEESE", "b07a30"),
    "food_coconut": ("COCONUT", "6a4a2a"), "food_corn": ("CORN", "e0c040"), "food_cornbread": ("CORNBREAD", "e0b040"),
    "food_corn_roast": ("ROAST CORN", "c8902a"), "food_dried_corn": ("DRIED CORN", "c8a030"), "food_dried_fruit": ("DRIED FRUIT", "8a3a24"),
    "food_dried_herbs": ("DRIED HERBS", "5a6a30"), "food_dried_mushroom": ("DRIED MUSHROOMS", "8a7a5a"),
    "food_dried_tomato": ("SUN-DRIED TOMATOES", "8a2418"), "food_egg": ("EGG", "eeeadc"), "food_egg_fried": ("FRIED EGGS", "f0d060"),
    "food_fish_cooked": ("GRILLED FISH", "c09060"), "food_fish_dried": ("STOCKFISH", "b8a888"), "food_fish_glow": ("GLOWING FISH", "8aff5a"),
    "food_fish_raw": ("RAW FISH", "9ab0b8"), "food_fish_salted": ("SALT FISH", "c8b898"), "food_fish_smoked": ("SMOKED FISH", "a06a3a"),
    "food_fish_soup": ("FISH SOUP", "c0a070"), "food_flatbread": ("FLATBREAD", "d8b070"), "food_fruit": ("FRUIT BOWL", "c04a3a"),
    "food_glowfish_cooked": ("GRILLED GLOWFISH", "a0c050"), "food_hempseed": ("HEMP SEEDS", "4a4a2a"), "food_herbs": ("HERBS", "4a8030"),
    "food_honey": ("JAR OF HONEY", "e0a030"), "food_jam": ("APPLE JAM", "8a2030"), "food_jar_pumpkin": ("JARRED PUMPKIN", "d07820"),
    "food_jar_tomato": ("JARRED TOMATOES", "b02818"), "food_jerky": ("JERKY", "5a2a1a"), "food_meat_cooked": ("COOKED MEAT", "8a4a2a"),
    "food_meat_pie": ("MEAT PIE", "b07030"), "food_meat_raw": ("RAW MEAT", "a83a3a"), "food_meat_salted": ("SALT MEAT", "9a4a3e"),
    "food_meat_smoked": ("SMOKED MEAT", "6a3a24"), "food_meat_stew": ("MEAT STEW", "7a3a1a"), "food_mushroom": ("MUSHROOMS", "b8a888"),
    "food_mush_skewer": ("MUSHROOM SKEWER", "9a7a50"), "food_mushsoup": ("MUSHROOM SOUP", "8a7050"), "food_pancakes": ("PANCAKES", "e0b060"),
    "food_pickled_beet": ("PICKLED BEETS", "7a1c3a"), "food_pickled_egg": ("PICKLED EGGS", "e0d0a0"), "food_pickled_fish": ("PICKLED FISH", "c0c8c0"),
    "food_pickles": ("PICKLED VEGETABLES", "8a9a3a"), "food_pie": ("PUMPKIN PIE", "d09040"), "food_porridge": ("WHEAT PORRIDGE", "d0b080"),
    "food_potato": ("RAW POTATO", "a88050"), "food_potato_baked": ("BAKED POTATO", "b07a40"), "food_pumpkin": ("PUMPKIN", "e08020"),
    "food_ration": ("RATION PACK", "6a7040"), "food_roast_dinner": ("ROAST DINNER", "8a4a24"), "food_rotten": ("ROTTEN FOOD", "4a4a2a"),
    "food_salad": ("GARDEN SALAD", "6a9a3a"), "food_sauerkraut": ("SAUERKRAUT", "c8c070"), "food_sausage": ("SMOKED SAUSAGE", "7a2a1a"),
    "food_soup": ("TOMATO SOUP", "a83020"), "food_stew": ("VEGETABLE STEW", "8a5a2a"), "food_sugar": ("SUGAR", "eeeadc"),
    "food_sunseeds": ("SUNFLOWER SEEDS", "3a3020"), "food_tomato": ("TOMATO", "c83020"), "food_trailmix": ("TRAIL MIX", "6a4a2a"),
}


def build(fid, name, c):
    n = fid
    if fid == "drink_water":
        return I.bottle(n, M["glass_clear"], label="3a6aa0", liquid="8cbcd6", h=0.26, r=0.034, cap="plastic_blue", neck=0.3)
    if fid == "drink_soda":
        return I.bottle(n, M["glass_clear"], label=c, liquid="3a1a10", h=0.22, r=0.032, cap="tin")
    if fid == "drink_beer":
        return I.bottle(n, M["glass_brown"], label="e0c060", liquid=None, h=0.24, r=0.032, cap="tin", neck=0.45)
    if fid == "drink_cider":
        return I.bottle(n, M["glass_green"], label=c, h=0.26, r=0.036, cap="tin", neck=0.4)
    if fid == "drink_milk":
        return I.bottle(n, M["glass_clear"], liquid="fbf8ee", h=0.22, r=0.04, cap="paper", neck=0.25)
    if fid == "drink_mead":
        return I.jug(n, "c4a070")
    if fid in ("drink_tea", "drink_bark_tea"):
        return I.mug(n, c)
    if fid.startswith("food_can"):
        label = {"food_can": "a8572a", "food_can_fish": "3a6aa0", "food_can_fruit": c, "food_can_meat": c, "food_can_stew": c, "food_can_veg": c}[fid]
        if fid == "food_can_fish":
            return I.tin_box(n, "3a6aa0", 0.12, 0.08, 0.03)
        return I.can(n, label, 0.038, 0.11, "pull" if fid != "food_can" else "tin")
    if fid == "food_ration":
        return I.box(n, I.col(c, 0.4), (0.16, 0.1, 0.04), label=M["label_cream"])
    if fid in ("food_jam", "food_jar_pumpkin", "food_jar_tomato", "food_pickled_beet", "food_pickled_egg", "food_pickled_fish", "food_pickles", "food_sauerkraut"):
        return I.jar(n, c, cloth=("e0e0d0" if fid == "food_jam" else None), h=0.13 if "pickle" in fid or "sauer" in fid else 0.1)
    if fid == "food_honey":
        return I.jar(n, c, cloth="c4b080", h=0.11, r=0.045)
    if fid in ("food_apple",):
        return I.sphere_fruit(n, c, 0.038, 0.9, True, True)
    if fid == "food_tomato":
        return I.sphere_fruit(n, c, 0.036, 0.8, False, False, ribs=6, calyx=True)
    if fid == "food_pumpkin":
        return I.pumpkin(n, c)
    if fid == "food_cabbage":
        return I.cabbage(n, c)
    if fid == "food_carrot":
        return I.carrot(n, c)
    if fid == "food_corn":
        return I.corn(n, c)
    if fid == "food_corn_roast":
        return I.corn(n, c, roast=True)
    if fid == "food_dried_corn":
        return I.corn(n, c, husk="c8b070")
    if fid == "food_beet":
        return I.beet(n, c)
    if fid == "food_berries":
        return I.berries(n, c)
    if fid in ("food_mushroom", "food_dried_mushroom"):
        return I.mushrooms(n, c, 3, dried=fid.startswith("food_dried"))
    if fid in ("food_herbs", "food_dried_herbs"):
        return I.bundle(n, c)
    if fid == "food_potato":
        return I.blob(n, c, 0.045, 0.7, 4)
    if fid == "food_potato_baked":
        return I.blob(n, c, 0.05, 0.65, 5, 0.8)
    if fid == "food_rotten":
        return I.blob(n, c, 0.06, 0.4, 11, 0.95)
    if fid == "food_egg":
        return I.egg(n, c)
    if fid == "food_coconut":
        return I.coconut(n, c)
    if fid == "food_fruit":
        return I.bowl(n, "c04a3a", 0.1, chunks=[c, "e08020", "e0c040", "6a1c50"])
    if fid in ("food_soup", "food_stew", "food_porridge", "food_salad", "food_fish_soup", "food_mushsoup", "food_meat_stew"):
        ch = {"food_stew": ["e07020", "a88050", "78a040"], "food_meat_stew": ["8a4a2a", "e07020"], "food_salad": ["78a040", "c83020", "e07020"],
              "food_fish_soup": ["eeeadc", "e07020"], "food_mushsoup": ["b8a888"]}.get(fid)
        return I.bowl(n, c, 0.07, chunks=ch)
    if fid == "food_bread":
        return I.loaf(n, c)
    if fid == "food_cornbread":
        return I.box(n, I.col(c, 0.7, 0.6), (0.12, 0.08, 0.05))
    if fid == "food_flatbread":
        return I.disc(n, c, 0.11, 0.012)
    if fid == "food_pancakes":
        return I.disc(n, c, 0.08, 0.01, 5)
    if fid in ("food_pie", "food_apple_pie", "food_meat_pie"):
        return I.pie(n, c, {"food_pie": "e08020", "food_apple_pie": "e0c060", "food_meat_pie": "6a2a1a"}[fid])
    if fid in ("food_meat_raw", "food_meat_cooked", "food_meat_salted", "food_meat_smoked", "food_bugmeat"):
        return I.steak(n, c, fat=("c8e0a0" if fid == "food_bugmeat" else "eeeadc"), bone=fid != "food_bugmeat")
    if fid == "food_jerky":
        return I.jerky(n, c)
    if fid == "food_sausage":
        return I.sausages(n, c)
    if fid in ("food_bug_skewer", "food_mush_skewer"):
        return I.skewer(n, c)
    if fid.startswith("food_fish") or fid == "food_glowfish_cooked":
        return I.fish(n, c, belly="eeeadc" if fid == "food_fish_raw" else None, flat=fid in ("food_fish_dried", "food_fish_salted"),
                      glow=fid == "food_fish_glow", stick=fid in ("food_fish_cooked", "food_glowfish_cooked"))
    if fid == "food_cheese":
        return I.cheese(n, c, wax="c02c20")
    if fid == "food_cheese_smoked":
        return I.cheese(n, c)
    if fid == "food_egg_fried":
        p = I.Part(n)
        I.plate(p, 0.1)
        for x in (-0.03, 0.03):
            p.cyl(I.col("fbf8ee", 0.4), 0.035, 0.004, (x, 0, 0.008), "Z", 16, bevel=0.001)
            p.sphere(I.col(c, 0.3), 0.014, (x, 0, 0.012), 10, 6, scale=(1, 1, 0.5))
        return p.build()
    if fid == "food_roast_dinner":
        p = I.Part(n)
        I.plate(p, 0.13)
        p.add(I.kit.bm_rock(0.04, 2, 0.5, 2), I.col(c, 0.6), (0.03, 0.0, 0.02))
        for k, h in enumerate(("a88050", "a88050", "e07020")):
            p.add(I.kit.bm_rock(0.018, k, 0.7, 1), I.col(h, 0.6), (-0.05, -0.04 + k * 0.035, 0.018))
        return p.build()
    if fid in ("food_sugar",):
        return I.paper_bag(n, label="3a6aa0")
    if fid in ("food_hempseed", "food_sunseeds", "food_trailmix", "food_dried_fruit", "food_dried_tomato"):
        return I.pouch(n, M["burlap"], c, 0.1, 0.12)
    raise KeyError(fid)


for i, (fid, (name, c)) in enumerate(sorted(FOODS.items())):
    pa.add(fid, (lambda f=fid, nm=name, cc=c: build(f, nm, cc)), F, kind="item", category="Food", label=name.title(), fit=False,
           origin="rest", game_icon_colour=c, world_scale_note="WorldItemModels shows icons x0.35; this model is real size",
           tile="food_%02d" % (i // 12 + 1))

if __name__ == "__main__":
    pa.run(F, out_sub="items", cols=4, gap=0.06)
