"""Image-textured materials for the HD world / furniture sets (Blender 5, headless).

Textures are the tileable photos of gen_textures.py (textures/world/<name>.png + <name>_n.png). hdkit smart-projects
every mesh into its own UV islands, so the materials do not use UVs: they sample the photos tri-planar in OBJECT space
at real-world scale (index.json: metres per tile), and the exporter bakes the result into each asset's atlas.

    image_material(name, tex, tint, rough, scale, normal=True, grain="h", ...)   tiled surfaces
    decal_material(name, tex, centre, size, plane="xy", ...)                       one picture on a face (painting,
                                                                                    clock dial, label)
grain: which in-plane direction the photo's horizontal axis follows
    "h" = horizontal on walls (x on top faces), "y" = vertical (boards standing up), "x" / "z" = that game axis.
Game axes (x right, y up, z front) -> Blender object space (-x, -z, y) (hdkit.GP.build)."""
import json
import os

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
TEX = os.path.join(HERE, "world")
_INDEX = None


def index():
    global _INDEX
    if _INDEX is None:
        p = os.path.join(TEX, "index.json")
        _INDEX = json.load(open(p)) if os.path.exists(p) else {}
    return _INDEX


def available(tex):
    return os.path.exists(os.path.join(TEX, tex + ".png"))


def _image(tex, normal=False):
    path = os.path.join(TEX, tex + ("_n" if normal else "") + ".png")
    img = bpy.data.images.load(path, check_existing=True)
    img.colorspace_settings.name = "Non-Color" if normal else "sRGB"
    return img


# Blender axis index: 0 X (= -game x), 1 Y (= -game z), 2 Z (= game y)
_PLANES = {0: (1, 2), 1: (0, 2), 2: (0, 1)}          # default (u, v) of the faces whose normal is along the axis
_GRAIN = {"x": 0, "z": 1, "y": 2}


def _uv_axes(face_axis, grain):
    u, v = _PLANES[face_axis]
    if grain == "h":
        return u, v
    g = _GRAIN[grain]
    if g == face_axis:
        return u, v
    other = ({0, 1, 2} - {face_axis, g}).pop()
    return g, other


class _NT:
    def __init__(self, nt):
        self.nt = nt
        self.x = -1200

    def n(self, kind, **kw):
        node = self.nt.nodes.new(kind)
        node.location = (self.x, 0)
        for k, v in kw.items():
            setattr(node, k, v)
        return node

    def link(self, a, b):
        self.nt.links.new(a, b)

    def math(self, op, a, b=None, clamp=False):
        m = self.n("ShaderNodeMath", operation=op, use_clamp=clamp)
        for i, s in enumerate((a, b)):
            if s is None:
                continue
            if isinstance(s, (int, float)):
                m.inputs[i].default_value = s
            else:
                self.link(s, m.inputs[i])
        return m.outputs[0]

    def vmath(self, op, a, b=None, scale=None):
        m = self.n("ShaderNodeVectorMath", operation=op)
        for i, s in enumerate((a, b)):
            if s is None:
                continue
            if isinstance(s, (tuple, list)):
                m.inputs[i].default_value = s
            else:
                self.link(s, m.inputs[i])
        if scale is not None:
            if isinstance(scale, (int, float)):
                m.inputs["Scale"].default_value = scale
            else:
                self.link(scale, m.inputs["Scale"])
        return m.outputs["Vector"] if op != "DOT_PRODUCT" and op != "LENGTH" else m.outputs["Value"]


def _new(name):
    m = bpy.data.materials.get(name)
    if m is not None:
        return m, None
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    return m, m.node_tree


def image_material(name, tex, tint=(1.0, 1.0, 1.0), rough=0.8, scale=None, normal=True, grain="h", metal=0.0,
                   var=0.12, dust=0.0, normal_strength=1.0, offset=(0.0, 0.0, 0.0), sheen=0.0, emit=None):
    """Principled material sampling textures/world/<tex>.png (x tint, linear rgb) tri-planar in object space.
    scale = metres per tile (default from index.json); var = large soft brightness variation (hides the repeat);
    dust = settling dust low on the object (like kit.surface); emit = (rgb, strength): the photo glows tinted by rgb
    (lit curtains / lampshades: name the object Lamp_* so the game switches it)."""
    m, nt = _new(name)
    if nt is None:
        return m
    T = _NT(nt)
    b = nt.nodes["Principled BSDF"]
    info = index().get(tex, {})
    scale = scale or info.get("metres", 1.0)
    tc = T.n("ShaderNodeTexCoord")
    pos = T.vmath("ADD", tc.outputs["Object"], tuple(offset))
    sep = T.n("ShaderNodeSeparateXYZ")
    T.link(pos, sep.inputs[0])
    P = [sep.outputs["X"], sep.outputs["Y"], sep.outputs["Z"]]
    nabs = T.vmath("ABSOLUTE", tc.outputs["Normal"])
    nsep = T.n("ShaderNodeSeparateXYZ")
    T.link(nabs, nsep.inputs[0])
    pw = [T.math("POWER", nsep.outputs[k], 6.0) for k in "XYZ"]
    tot = T.math("ADD", T.math("ADD", pw[0], pw[1]), pw[2])
    w = [T.math("DIVIDE", p, tot) for p in pw]
    img, nimg = _image(tex), (_image(tex, True) if normal and os.path.exists(os.path.join(TEX, tex + "_n.png")) else None)
    col, pert = None, None
    for f in range(3):
        u, v = _uv_axes(f, grain)
        cu = T.math("DIVIDE", P[u], scale)
        cv = T.math("DIVIDE", P[v], scale)
        cb = T.n("ShaderNodeCombineXYZ")
        T.link(cu, cb.inputs[0])
        T.link(cv, cb.inputs[1])
        it = T.n("ShaderNodeTexImage", image=img, extension="REPEAT", interpolation="Linear")
        T.link(cb.outputs[0], it.inputs[0])
        c = T.vmath("SCALE", it.outputs["Color"], None, w[f])
        col = c if col is None else T.vmath("ADD", col, c)
        if nimg is not None:
            nt_ = T.n("ShaderNodeTexImage", image=nimg, extension="REPEAT", interpolation="Linear")
            T.link(cb.outputs[0], nt_.inputs[0])
            ns = T.n("ShaderNodeSeparateColor")
            T.link(nt_.outputs["Color"], ns.inputs[0])
            nx = T.math("SUBTRACT", T.math("MULTIPLY", ns.outputs[0], 2.0), 1.0)
            ny = T.math("SUBTRACT", T.math("MULTIPLY", ns.outputs[1], 2.0), 1.0)
            ax = [0.0, 0.0, 0.0]
            ax[u] = 1.0
            ay = [0.0, 0.0, 0.0]
            ay[v] = 1.0
            pv = T.vmath("ADD", T.vmath("SCALE", tuple(ax), None, nx), T.vmath("SCALE", tuple(ay), None, ny))
            pv = T.vmath("SCALE", pv, None, w[f])
            pert = pv if pert is None else T.vmath("ADD", pert, pv)
    # soft large-scale variation, tint
    if var > 0:
        nz = T.n("ShaderNodeTexNoise")
        nz.inputs["Scale"].default_value = 0.6 / max(scale, 0.2)
        nz.inputs["Detail"].default_value = 2.0
        T.link(pos, nz.inputs["Vector"])
        f = T.math("ADD", T.math("MULTIPLY", T.math("SUBTRACT", nz.outputs["Fac"], 0.5), var * 2.0), 1.0)
        col = T.vmath("SCALE", col, None, f)
    col = T.vmath("MULTIPLY", col, tuple(tint[:3]))
    if dust > 0:
        low = T.math("SUBTRACT", 0.45, P[2], True)
        dn = T.n("ShaderNodeTexNoise")
        dn.inputs["Scale"].default_value = 5.0
        T.link(pos, dn.inputs["Vector"])
        dm = T.math("MULTIPLY", T.math("ADD", T.math("MULTIPLY", low, 1.2), T.math("MULTIPLY", dn.outputs["Fac"], 0.5)), dust, True)
        dm = T.math("MULTIPLY", T.math("SUBTRACT", dm, 0.4, True), 1.6, True)
        mix = T.n("ShaderNodeMix", data_type="RGBA")
        ins = {s.identifier: s for s in mix.inputs}
        T.link(dm, ins["Factor_Float"])
        T.link(col, ins["A_Color"])
        ins["B_Color"].default_value = (0.48, 0.3, 0.16, 1.0)
        col = [s for s in mix.outputs if s.identifier == "Result_Color"][0]
    T.link(col, b.inputs["Base Color"])
    if emit is not None:
        T.link(T.vmath("MULTIPLY", col, tuple(emit[0][:3])), b.inputs["Emission Color"])
        b.inputs["Emission Strength"].default_value = emit[1]
    # roughness follows the photo a little (dark grain / grime rougher)
    bw = T.n("ShaderNodeRGBToBW")
    T.link(col, bw.inputs[0])
    r = T.math("ADD", T.math("MULTIPLY", T.math("SUBTRACT", 0.35, bw.outputs[0]), 0.25), rough, True)
    T.link(r, b.inputs["Roughness"])
    b.inputs["Metallic"].default_value = metal
    if sheen > 0 and "Sheen Weight" in b.inputs:
        b.inputs["Sheen Weight"].default_value = sheen
    if pert is not None:
        nn = T.vmath("ADD", tc.outputs["Normal"], T.vmath("SCALE", pert, None, 0.6 * normal_strength))
        nn = T.vmath("NORMALIZE", nn)
        vt = T.n("ShaderNodeVectorTransform", vector_type="NORMAL", convert_from="OBJECT", convert_to="WORLD")
        T.link(nn, vt.inputs[0])
        T.link(vt.outputs[0], b.inputs["Normal"])
    m["texture"] = tex
    return m


def decal_material(name, tex, centre, size, plane="xy", tint=(1.0, 1.0, 1.0), rough=0.6, flip_u=False, pivot=(0, 0, 0),
                   emit=0.0, metal=0.0):
    """One picture spanning `size` (w, h metres) around game point `centre` on a game plane: 'xy' (faces +-z, u along
    -x so it reads from the front), 'zy' (faces +-x, u along z), 'xz' (faces up, u along x, v along -z), 'wall' (FurnitureLibrary wall pieces: XZ plane, +Y out, +z up the wall:
    u along x, v along z).
    pivot = the GP pivot of the object carrying the faces (its object space starts there)."""
    m, nt = _new(name)
    if nt is None:
        return m
    T = _NT(nt)
    b = nt.nodes["Principled BSDF"]
    tc = T.n("ShaderNodeTexCoord")
    sep = T.n("ShaderNodeSeparateXYZ")
    T.link(tc.outputs["Object"], sep.inputs[0])
    gx = T.math("SUBTRACT", T.math("MULTIPLY", sep.outputs["X"], -1.0), centre[0] - pivot[0])
    gy = T.math("SUBTRACT", sep.outputs["Z"], centre[1] - pivot[1])
    gz = T.math("SUBTRACT", T.math("MULTIPLY", sep.outputs["Y"], -1.0), centre[2] - pivot[2])
    if plane == "xy":
        a, c = T.math("MULTIPLY", gx, -1.0), gy
    elif plane == "zy":
        a, c = gz, gy
    elif plane == "wall":
        a, c = gx, gz
    else:
        a, c = gx, T.math("MULTIPLY", gz, -1.0)
    if flip_u:
        a = T.math("MULTIPLY", a, -1.0)
    u = T.math("ADD", T.math("DIVIDE", a, size[0]), 0.5)
    v = T.math("ADD", T.math("DIVIDE", c, size[1]), 0.5)
    cb = T.n("ShaderNodeCombineXYZ")
    T.link(u, cb.inputs[0])
    T.link(v, cb.inputs[1])
    it = T.n("ShaderNodeTexImage", image=_image(tex), extension="EXTEND", interpolation="Linear")
    T.link(cb.outputs[0], it.inputs[0])
    col = T.vmath("MULTIPLY", it.outputs["Color"], tuple(tint[:3]))
    T.link(col, b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if emit > 0:
        T.link(col, b.inputs["Emission Color"])
        b.inputs["Emission Strength"].default_value = emit
    m["texture"] = tex
    return m


def relativize(blend_path):
    """Image paths -> relative to the saved .blend (call after saving, then save again)."""
    imgs = [i for i in bpy.data.images if i.filepath and not i.filepath.startswith("//") and i.source == "FILE"]
    if not imgs:
        return False
    d = os.path.dirname(os.path.abspath(blend_path))
    for i in imgs:
        i.filepath = "//" + os.path.relpath(bpy.path.abspath(i.filepath), d)
    return True


# ------------------------------------------------------------------------------------------------ the kit's set
# key -> (material name, texture, tint (linear multiplier), roughness, grain, metallic, normal strength)
WOOD_TONES = {"": ("oak", (1.0, 0.92, 0.85)), "_light": ("pine_raw", (1.0, 1.0, 1.0)), "_dark": ("oak", (0.5, 0.42, 0.36)),
              "_grey": ("pine_worn", (1.0, 1.0, 1.0))}
KIT = {
    "wood": ("TexWood", "oak", (1.0, 0.92, 0.85), 0.62, "x", 0.0, 1.0),
    "wood_dark": ("TexWoodDark", "oak", (0.5, 0.42, 0.36), 0.66, "x", 0.0, 1.0),
    "wood_light": ("TexWoodLight", "pine_raw", (1.0, 1.0, 1.0), 0.7, "x", 0.0, 1.0),
    "planks": ("TexWallPlanks", "pine_raw", (0.78, 0.68, 0.58), 0.85, "h", 0.0, 1.4),
    "planks_v": ("TexPlanksVertical", "pine_raw", (0.78, 0.68, 0.58), 0.85, "y", 0.0, 1.4),
    "siding": ("TexFarmSiding", "pine_worn", (1.0, 0.9, 0.78), 0.85, "y", 0.0, 1.4),
    "floor": ("TexFloorBoards", "floorboards", (1.0, 1.0, 1.0), 0.7, "x", 0.0, 1.2),
    "brick": ("TexBrick", "brick_red", (1.0, 1.0, 1.0), 0.9, "h", 0.0, 1.6),
    "brick_dark": ("TexBrickSoot", "brick_dark", (1.0, 1.0, 1.0), 0.9, "h", 0.0, 1.6),
    "brick_fired": ("TexBrickFired", "brick_red", (1.15, 1.0, 0.85), 0.9, "h", 0.0, 1.6),
    "conc": ("TexConcrete", "concrete", (1.0, 1.0, 1.0), 0.92, "h", 0.0, 1.0),
    "conc_dark": ("TexConcreteDark", "concrete", (0.72, 0.72, 0.72), 0.92, "h", 0.0, 1.0),
    "conc_light": ("TexConcreteLight", "concrete", (1.15, 1.13, 1.1), 0.9, "h", 0.0, 1.0),
    "concrete": ("TexConcreteCast", "concrete", (0.95, 0.95, 0.95), 0.92, "h", 0.0, 1.0),
    # corrugated sheets get their ribs from the geometry (hdkit.corrugated): flat photos, no ribbed ones
    "tin": ("TexTinSheet", "tin_flat_rust", (0.95, 0.97, 1.0), 0.55, "h", 0.45, 0.8),
    "tin_v": ("TexTinSheetV", "tin_flat_rust", (0.95, 0.97, 1.0), 0.55, "h", 0.45, 0.8),
    "tin_rust": ("TexTinRust", "tin_flat_rust", (1.0, 0.75, 0.6), 0.75, "h", 0.25, 1.0),
    "tin_rust_v": ("TexTinRustV", "tin_flat_rust", (1.0, 0.75, 0.6), 0.75, "h", 0.25, 1.0),
    "roof_red": ("TexRoofRed", "tin_flat_red", (1.0, 1.0, 1.0), 0.6, "h", 0.2, 1.0),
    "tin_ribbed": ("TexTinRibbed", "tin_rusty", (1.0, 1.0, 1.0), 0.75, "y", 0.2, 1.2),          # flat panels only
    "tin_ribbed_red": ("TexTinRibbedRed", "tin_painted", (1.0, 1.0, 1.0), 0.6, "y", 0.2, 1.2),
    "plaster": ("TexPlaster", "plaster_cracked", (1.0, 1.0, 1.0), 0.9, "h", 0.0, 1.0),
    "enamel": ("TexEnamel", "enamel", (1.0, 1.0, 1.0), 0.25, "h", 0.0, 0.0),        # glossy: no grain normal
    "iron": ("TexCastIron", "cast_iron", (1.0, 1.0, 1.0), 0.7, "h", 0.6, 1.2),
    "blanket": ("TexWoolBlanket", "wool_blanket", (1.0, 1.0, 1.0), 0.95, "x", 0.0, 1.2),
    "burlap": ("TexBurlap", "burlap", (1.0, 1.0, 1.0), 0.98, "h", 0.0, 1.4),
    "sandbag": ("TexSandbagCloth", "burlap", (1.05, 1.0, 0.92), 0.98, "h", 0.0, 1.4),
    "hay": ("TexHay", "hay", (1.0, 1.0, 1.0), 0.95, "x", 0.0, 1.4),
    "soil": ("TexSoil", "dirt_floor", (0.6, 0.55, 0.5), 0.98, "h", 0.0, 1.4),
    "soil_dry": ("TexSoilDry", "dirt_floor", (1.0, 1.0, 1.0), 0.98, "h", 0.0, 1.4),
    # new keys (cozy pass)
    "oak": ("TexOak", "oak", (1.0, 1.0, 1.0), 0.45, "x", 0.0, 0.8),
    "pine": ("TexPine", "pine_raw", (1.0, 1.0, 1.0), 0.75, "x", 0.0, 1.2),
    "pine_painted": ("TexPinePainted", "pine_painted", (1.0, 1.0, 1.0), 0.75, "h", 0.0, 1.2),
    "pine_painted_v": ("TexPinePaintedV", "pine_painted", (1.0, 1.0, 1.0), 0.75, "y", 0.0, 1.2),
    "pine_worn": ("TexPineWorn", "pine_worn", (1.0, 1.0, 1.0), 0.85, "h", 0.0, 1.4),
    "plywood": ("TexPlywood", "plywood", (1.0, 1.0, 1.0), 0.7, "x", 0.0, 0.8),
    "osb": ("TexOSB", "osb", (1.0, 1.0, 1.0), 0.8, "h", 0.0, 1.2),
    "wallpaper": ("TexWallpaperFloral", "wallpaper_floral", (1.0, 1.0, 1.0), 0.85, "h", 0.0, 0.5),
    "wallpaper_b": ("TexWallpaperStripe", "wallpaper_stripe", (1.0, 1.0, 1.0), 0.85, "h", 0.0, 0.5),
    "linoleum": ("TexLinoleum", "linoleum", (1.0, 1.0, 1.0), 0.45, "x", 0.0, 0.6),
    "rug_tex": ("TexRugWoven", "rug_woven", (1.0, 1.0, 1.0), 0.97, "x", 0.0, 1.0),
    "rug_rag": ("TexRugRag", "rug_rag", (1.0, 1.0, 1.0), 0.97, "x", 0.0, 1.2),
    "quilt": ("TexQuilt", "quilt", (1.0, 1.0, 1.0), 0.95, "x", 0.0, 1.0),
    "cushion": ("TexCushion", "cushion", (1.0, 1.0, 1.0), 0.95, "x", 0.0, 1.0),
    "ticking": ("TexTicking", "ticking", (1.0, 1.0, 1.0), 0.95, "x", 0.0, 0.8),
    "curtain": ("TexCurtain", "curtain", (1.0, 1.0, 1.0), 0.95, "h", 0.0, 0.6),
    "tiles": ("TexTiles", "tiles_ceramic", (1.0, 1.0, 1.0), 0.2, "h", 0.0, 1.2),
    "chicken_wire": ("TexChickenWire", "chicken_wire", (1.0, 1.0, 1.0), 0.5, "h", 0.6, 1.0),
    "steel_olive": ("TexSteelOlive", "steel_painted", (1.0, 1.0, 1.0), 0.55, "h", 0.3, 0.8),
    "brick_white": ("TexBrickWhite", "brick_white", (1.0, 1.0, 1.0), 0.9, "h", 0.0, 1.6),
    "doormat": ("TexDoormat", "doormat", (1.0, 1.0, 1.0), 0.98, "x", 0.0, 1.6),
    "dirt": ("TexDirtFloor", "dirt_floor", (1.0, 1.0, 1.0), 0.98, "h", 0.0, 1.4),
}
DUSTY = {"planks", "planks_v", "siding", "tin", "tin_v", "tin_rust", "tin_rust_v", "roof_red", "conc", "conc_dark",
         "concrete", "brick", "brick_dark", "brick_fired", "brick_white", "sandbag", "burlap", "pine_worn"}


def upgrade(M):
    """Replace the kit's procedural materials with image-textured ones where a photo exists (keys stay the same, so
    every builder picks them up) and add the new keys. Missing photos keep the procedural material."""
    if not os.path.exists(os.path.join(TEX, "index.json")):
        return M
    for key, (name, tex, tint, rough, grain, metal, ns) in KIT.items():
        if not available(tex):
            continue
        M[key] = image_material(name, tex, tint, rough, None, ns > 0, grain, metal, 0.12, 0.25 if key in DUSTY else 0.0, ns)
    for tone, (tex, tint) in WOOD_TONES.items():
        if not available(tex):
            continue
        for ax in "xyz":
            key = "wood%s_%s" % (tone, ax)
            if key in M:
                M[key] = image_material("TexWood%s_%s" % (tone.title().strip("_"), ax.upper()), tex, tint,
                                        0.62 if tex == "oak" else 0.75, None, True, ax, 0.0, 0.12, 0.0, 1.0)
    return M
