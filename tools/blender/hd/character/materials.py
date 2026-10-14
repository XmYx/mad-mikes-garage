"""Hand-painted-look procedural materials (Eevee). Texture space = the mesh's rest position (`rest_position`
attribute), so patterns stick to the skin when the armature poses it. Colours are game hex values (sRGB).

Fabric kinds sample a tileable photo texture from tools/blender/hd/textures/fabric (fabric_textures.py): box projection
over the rest position, tinted so its mean colour is the game colour, its normal map applied tri-planar (object space,
Normal Map node -> Bump with the albedo's luminance), then the procedural mottling, grime mask, fading and cavity
darkening on top."""
import json
import math
import os

import bpy

from hdlib import srgb_lin

DUST = "8a7a64"
FABRIC = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "textures", "fabric")
_STATS = None

# kind -> candidate textures (the nearest mean colour wins unless the material names one with tex=)
KIND_TEX = {
    "cloth": ["jersey"], "knit": ["knit_sweater"], "denim": ["denim_blue", "denim_faded", "denim_olive"],
    "canvas": ["canvas_sand", "canvas_olive", "canvas_grey"], "leather": ["leather_brown", "leather_black", "leather_worn"],
    "rubber": ["rubber"], "check": ["check_shemagh"], "stripe": ["stripe_poncho"],
}
# texture -> tile size in metres (garment `scale` makes it finer), normal strength, bump strength
TEX_SIZE = {
    "denim_blue": 0.16, "denim_faded": 0.16, "denim_olive": 0.16, "canvas_sand": 0.18, "canvas_olive": 0.18, "canvas_grey": 0.18,
    "canvas_patched": 0.32, "knit_sweater": 0.16, "knit_beanie": 0.1, "jersey": 0.14, "fleece": 0.12, "leather_brown": 0.22,
    "leather_black": 0.2, "leather_worn": 0.24, "leather_waxed": 0.3, "suede": 0.16, "rubber": 0.14, "kevlar": 0.09,
    "cordura": 0.1, "webbing": 0.05, "check_shemagh": 0.22, "stripe_poncho": 0.42, "hazmat_vinyl": 0.32, "felt": 0.14,
    "straw": 0.1, "fur": 0.1, "quilted": 0.34, "wool_coarse": 0.16, "seam_strip": 0.12,
}


GRAIN = {"denim": 1.8, "canvas": 1.8, "cloth": 1.7, "knit": 1.8, "check": 1.0, "stripe": 1.2, "leather": 1.3, "rubber": 1.2}


def _stats():
    global _STATS
    if _STATS is None:
        p = os.path.join(FABRIC, "stats.json")
        _STATS = json.load(open(p)) if os.path.exists(p) else {}
    return _STATS


def pick_tex(kind, hexcol, tex=None):
    """The fabric texture for a material: explicit `tex`, else the kind's candidate nearest in colour (None = none)."""
    st = _stats()
    if tex:
        return tex if tex in st else None
    cands = [t for t in KIND_TEX.get(kind, []) if t in st]
    if not cands:
        return None
    b = srgb_lin(hexcol)[:3]
    def dist(t):
        m = st[t]
        lb, lm = sum(b) / 3 + 1e-4, sum(m) / 3 + 1e-4
        chroma = sum(abs(b[i] / lb - m[i] / lm) for i in range(3))
        return chroma + 0.6 * abs(math.log(lb / lm))
    return min(cands, key=dist)


BAKE_PX = int(os.environ.get("HD_FABRIC_PX", "256"))   # packed copy: about the texel density of a garment atlas


def _image(name, colour=True):
    """The fabric image, pre-filtered to BAKE_PX (a one-sample bake would alias a finer image) and packed into the
    .blend, so the committed wardrobe blends export without the texture folder next to them."""
    key = f"fab_{name}_{BAKE_PX}"
    img = bpy.data.images.get(key)
    if img:
        return img
    img = bpy.data.images.load(os.path.join(FABRIC, name + ".png"), check_existing=False)
    img.name = key
    img.colorspace_settings.name = "sRGB" if colour else "Non-Color"
    if img.size[0] > BAKE_PX:
        img.scale(BAKE_PX, BAKE_PX)
    img.pack()
    return img


def _vmath(nt, op, a, b=None, x=0, y=0, scale=None):
    v = nt.nodes.new("ShaderNodeVectorMath"); v.operation = op; v.location = (x, y)
    L = nt.links.new
    for i, s in enumerate((a, b)):
        if s is None:
            continue
        if isinstance(s, (tuple, list)):
            v.inputs[i].default_value = s
        else:
            L(s, v.inputs[i])
    if scale is not None:
        v.inputs["Scale"].default_value = scale
    return v.outputs[0] if op not in ("DOT_PRODUCT", "LENGTH", "DISTANCE") else v.outputs[1]


def _tex_box(nt, img, vec, x, y, blend=0.25):
    t = nt.nodes.new("ShaderNodeTexImage"); t.location = (x, y)
    t.image = img; t.projection = "BOX"; t.projection_blend = blend; t.interpolation = "Linear"
    nt.links.new(vec, t.inputs["Vector"])
    return t


def _triplanar_normal(nt, img, vec, x, y):
    """Object-space normal from a tangent-space normal map, tri-planar (UDN blend, weights |n|^4). Returns a colour
    (n * 0.5 + 0.5) for a Normal Map node in OBJECT space."""
    L = nt.links.new
    tc = nt.nodes.new("ShaderNodeTexCoord"); tc.location = (x - 600, y)
    N = tc.outputs["Normal"]
    sep_p = nt.nodes.new("ShaderNodeSeparateXYZ"); sep_p.location = (x - 600, y - 200); L(vec, sep_p.inputs[0])
    sep_n = nt.nodes.new("ShaderNodeSeparateXYZ"); sep_n.location = (x - 400, y + 200); L(N, sep_n.inputs[0])
    px, py, pz = sep_p.outputs
    # weights
    absn = _vmath(nt, "ABSOLUTE", N, x=x - 400, y=y + 400)
    sq = _vmath(nt, "MULTIPLY", absn, absn, x=x - 250, y=y + 400)
    w4 = _vmath(nt, "MULTIPLY", sq, sq, x=x - 100, y=y + 400)
    tot = _vmath(nt, "DOT_PRODUCT", w4, (1.0, 1.0, 1.0), x=x + 50, y=y + 450)
    inv = nt.nodes.new("ShaderNodeMath"); inv.operation = "DIVIDE"; inv.location = (x + 200, y + 450)
    inv.inputs[0].default_value = 1.0; L(tot, inv.inputs[1])
    wn = _vmath(nt, "SCALE", w4, x=x + 350, y=y + 400); L(inv.outputs[0], wn.node.inputs["Scale"])
    sep_w = nt.nodes.new("ShaderNodeSeparateXYZ"); sep_w.location = (x + 500, y + 400); L(wn, sep_w.inputs[0])
    acc = N
    planes = (("X", (py, pz), lambda r, g: (None, r, g)), ("Y", (px, pz), lambda r, g: (r, None, g)), ("Z", (px, py), lambda r, g: (r, g, None)))
    for i, (ax, (u, v), _) in enumerate(planes):
        cu = nt.nodes.new("ShaderNodeCombineXYZ"); cu.location = (x - 200, y - 200 - i * 260)
        L(u, cu.inputs[0]); L(v, cu.inputs[1])
        t = nt.nodes.new("ShaderNodeTexImage"); t.location = (x, y - 200 - i * 260)
        t.image = img; t.interpolation = "Linear"
        L(cu.outputs[0], t.inputs["Vector"])
        tn = _vmath(nt, "MULTIPLY_ADD", t.outputs["Color"], (2.0, 2.0, 2.0), x=x + 250, y=y - 200 - i * 260)
        tn.node.inputs[2].default_value = (-1.0, -1.0, -1.0)
        st = nt.nodes.new("ShaderNodeSeparateXYZ"); st.location = (x + 400, y - 200 - i * 260); L(tn, st.inputs[0])
        d = nt.nodes.new("ShaderNodeCombineXYZ"); d.location = (x + 550, y - 200 - i * 260)
        r, g = st.outputs[0], st.outputs[1]
        slots = {"X": (1, 2), "Y": (0, 2), "Z": (0, 1)}[ax]
        L(r, d.inputs[slots[0]]); L(g, d.inputs[slots[1]])
        sc = _vmath(nt, "SCALE", d.outputs[0], x=x + 700, y=y - 200 - i * 260)
        L(sep_w.outputs[i], sc.node.inputs["Scale"])
        acc = _vmath(nt, "ADD", acc, sc, x=x + 850, y=y - 200 - i * 260)
    nn = _vmath(nt, "NORMALIZE", acc, x=x + 1000, y=y - 300)
    col = _vmath(nt, "MULTIPLY_ADD", nn, (0.5, 0.5, 0.5), x=x + 1150, y=y - 300)
    col.node.inputs[2].default_value = (0.5, 0.5, 0.5)
    return col


def _n(nt, kind, x, y, **inputs):
    n = nt.nodes.new(kind)
    n.location = (x, y)
    for k, v in inputs.items():
        n.inputs[k].default_value = v
    return n


def make(name, hexcol, kind="cloth", rough=None, metal=0.0, grime=0.35, scale=1.0, hex2=None, tex=None, nstrength=None):
    """kind: skin cloth knit denim leather canvas metal rubber hair eye lens plastic check stripe.
    tex: a fabric texture name (fabric_textures.TEXTURES) overriding the kind's default; nstrength: normal strength."""
    key = f"{name}"
    m = bpy.data.materials.get(key)
    if m:
        return m
    m = bpy.data.materials.new(key)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = _n(nt, "ShaderNodeOutputMaterial", 1400, 0)
    bs = _n(nt, "ShaderNodeBsdfPrincipled", 1100, 0)
    L = nt.links.new
    L(bs.outputs[0], out.inputs[0])
    base = srgb_lin(hexcol)
    attr = _n(nt, "ShaderNodeAttribute", -1200, 0)
    attr.attribute_type = "GEOMETRY"
    attr.attribute_name = "rest_position"
    col_attr = _n(nt, "ShaderNodeVertexColor", -1200, 300)
    col_attr.layer_name = "Col"
    vec = attr.outputs["Vector"]

    rgb = _n(nt, "ShaderNodeRGB", -900, 400)
    rgb.outputs[0].default_value = base
    colour = rgb.outputs[0]
    if kind == "skin":                       # vertex colour carries lips / cheeks / stubble on the skin tone
        mul = _n(nt, "ShaderNodeMix", -700, 400); mul.data_type = "RGBA"; mul.blend_type = "MULTIPLY"
        mul.inputs["Factor"].default_value = 1.0
        L(colour, mul.inputs[6]); L(col_attr.outputs["Color"], mul.inputs[7])
        colour = mul.outputs[2]

    # large-scale hand-painted mottling
    nz = _n(nt, "ShaderNodeTexNoise", -900, 100)
    nz.inputs["Scale"].default_value = 6.0 * scale
    nz.inputs["Detail"].default_value = 4.0
    nz.inputs["Roughness"].default_value = 0.55
    L(vec, nz.inputs["Vector"])
    ramp = _n(nt, "ShaderNodeMapRange", -700, 100)
    ramp.inputs["From Min"].default_value = 0.3; ramp.inputs["From Max"].default_value = 0.7
    ramp.inputs["To Min"].default_value = 0.82; ramp.inputs["To Max"].default_value = 1.12
    L(nz.outputs["Fac"], ramp.inputs["Value"])
    mott = _n(nt, "ShaderNodeMix", -500, 300); mott.data_type = "RGBA"; mott.blend_type = "MULTIPLY"
    mott.inputs["Factor"].default_value = 1.0
    L(colour, mott.inputs[6]); L(ramp.outputs[0], mott.inputs[7])
    colour = mott.outputs[2]

    bump_h = None
    nmap = None
    tname = pick_tex(kind, hexcol, tex)
    if tname:
        # photo fabric: box projection over the rest pose, mean colour = the game colour (mottled), normal map tri-planar
        tvec = _vmath(nt, "SCALE", vec, x=-1050, y=-1200, scale=scale / TEX_SIZE.get(tname, 0.18))
        timg = _tex_box(nt, _image(tname), tvec, -850, -1200)
        if kind == "check":
            # two-colour weave: the photo's luminance picks between the dark check colour and the cloth colour
            bw = _n(nt, "ShaderNodeRGBToBW", -650, -1200); L(timg.outputs["Color"], bw.inputs[0])
            lr = _n(nt, "ShaderNodeMapRange", -500, -1200)
            lr.inputs["From Min"].default_value = 0.06; lr.inputs["From Max"].default_value = 0.4
            L(bw.outputs[0], lr.inputs["Value"])
            tm = _n(nt, "ShaderNodeMix", -350, -1100); tm.data_type = "RGBA"; tm.clamp_factor = True
            L(lr.outputs[0], tm.inputs["Factor"])
            tm.inputs[6].default_value = srgb_lin(hex2 or "232127"); L(colour, tm.inputs[7])
            tcol = tm.outputs[2]
        else:
            mean = _stats().get(tname, [0.18, 0.18, 0.18])
            # detail = photo / its mean (1 on average), contrast raised (GRAIN) so the weave reads after the bake,
            # times the mottled game colour
            t1 = _n(nt, "ShaderNodeMix", -650, -1100); t1.data_type = "RGBA"; t1.blend_type = "MULTIPLY"
            t1.inputs["Factor"].default_value = 1.0
            L(timg.outputs["Color"], t1.inputs[6])
            t1.inputs[7].default_value = tuple(min(8.0, 1.0 / max(1e-3, c)) for c in mean) + (1.0,)
            gm_ = _n(nt, "ShaderNodeGamma", -550, -1100)
            gm_.inputs["Gamma"].default_value = GRAIN.get(kind, 1.3)
            L(t1.outputs[2], gm_.inputs["Color"])
            t2 = _n(nt, "ShaderNodeMix", -450, -1100); t2.data_type = "RGBA"; t2.blend_type = "MULTIPLY"
            t2.inputs["Factor"].default_value = 1.0
            L(gm_.outputs[0], t2.inputs[6]); L(colour, t2.inputs[7])
            tcol = t2.outputs[2]
        tb = _n(nt, "ShaderNodeMix", -250, -1000); tb.data_type = "RGBA"
        tb.inputs["Factor"].default_value = 0.92
        L(colour, tb.inputs[6]); L(tcol, tb.inputs[7])
        colour = tb.outputs[2]
        hb = _n(nt, "ShaderNodeRGBToBW", -650, -1350); L(timg.outputs["Color"], hb.inputs[0])
        bump_h = hb.outputs[0]
        if os.path.exists(os.path.join(FABRIC, tname + "_n.png")):
            ncol = _triplanar_normal(nt, _image(tname + "_n", False), tvec, -500, -1700)
            nm = _n(nt, "ShaderNodeNormalMap", 900, -500)
            nm.space = "OBJECT"
            nm.inputs["Strength"].default_value = nstrength if nstrength is not None else (1.2 if kind == "leather" else 1.7)
            L(ncol, nm.inputs["Color"])
            nmap = nm.outputs["Normal"]
    if kind in ("check", "stripe") and not tname:
        # shemagh check / scarf stripes in the second colour
        pat = _n(nt, "ShaderNodeTexWave", -900, -700)
        pat.wave_type = "BANDS"; pat.bands_direction = "Z"; pat.wave_profile = "SAW" if kind == "stripe" else "SIN"
        pat.inputs["Scale"].default_value = 18 if kind == "stripe" else 55
        L(vec, pat.inputs["Vector"])
        th = _n(nt, "ShaderNodeMath", -700, -700); th.operation = "GREATER_THAN"
        th.inputs[1].default_value = 0.62 if kind == "stripe" else 0.8
        L(pat.outputs["Fac"], th.inputs[0])
        fac = th.outputs[0]
        if kind == "check":
            pat2 = _n(nt, "ShaderNodeTexWave", -900, -900)
            pat2.wave_type = "BANDS"; pat2.bands_direction = "X"
            pat2.inputs["Scale"].default_value = 55
            L(vec, pat2.inputs["Vector"])
            th2 = _n(nt, "ShaderNodeMath", -700, -900); th2.operation = "GREATER_THAN"
            th2.inputs[1].default_value = 0.8
            L(pat2.outputs["Fac"], th2.inputs[0])
            mx2 = _n(nt, "ShaderNodeMath", -550, -800); mx2.operation = "MAXIMUM"
            L(th.outputs[0], mx2.inputs[0]); L(th2.outputs[0], mx2.inputs[1])
            fac = mx2.outputs[0]
        pm = _n(nt, "ShaderNodeMix", -400, -700); pm.data_type = "RGBA"
        L(fac, pm.inputs["Factor"]); L(colour, pm.inputs[6])
        pm.inputs[7].default_value = srgb_lin(hex2 or "232127")
        colour = pm.outputs[2]
        kind_w = "cloth"
    else:
        kind_w = kind
    if kind_w in ("cloth", "knit", "denim", "canvas") and not tname:
        # woven texture: two crossed fine wave bands
        w1 = _n(nt, "ShaderNodeTexWave", -900, -200)
        w1.wave_type = "BANDS"; w1.bands_direction = "DIAGONAL" if kind == "denim" else "X"
        w1.inputs["Scale"].default_value = (260 if kind != "knit" else 120) * scale
        w1.inputs["Distortion"].default_value = 0.6 if kind == "knit" else 0.2
        L(vec, w1.inputs["Vector"])
        w2 = _n(nt, "ShaderNodeTexWave", -900, -400)
        w2.wave_type = "BANDS"; w2.bands_direction = "Z" if kind != "denim" else "DIAGONAL"
        w2.inputs["Scale"].default_value = (260 if kind != "knit" else 60) * scale
        L(vec, w2.inputs["Vector"])
        mx = _n(nt, "ShaderNodeMath", -700, -300); mx.operation = "MULTIPLY"
        L(w1.outputs["Fac"], mx.inputs[0]); L(w2.outputs["Fac"], mx.inputs[1])
        bump_h = mx.outputs[0]
        wv = _n(nt, "ShaderNodeMapRange", -500, -300)
        wv.inputs["To Min"].default_value = 0.9 if kind != "denim" else 0.78; wv.inputs["To Max"].default_value = 1.05
        L(mx.outputs[0], wv.inputs["Value"])
        m2 = _n(nt, "ShaderNodeMix", -300, 300); m2.data_type = "RGBA"; m2.blend_type = "MULTIPLY"
        m2.inputs["Factor"].default_value = 1.0
        L(colour, m2.inputs[6]); L(wv.outputs[0], m2.inputs[7])
        colour = m2.outputs[2]
        if kind == "denim" and hex2:
            pass
    if kind in ("leather", "rubber", "plastic") and not tname:
        cr = _n(nt, "ShaderNodeTexVoronoi", -900, -200)
        cr.inputs["Scale"].default_value = 140 * scale if kind == "leather" else 60 * scale
        L(vec, cr.inputs["Vector"])
        bump_h = cr.outputs["Distance"]
    if kind == "metal":
        sc = _n(nt, "ShaderNodeTexNoise", -900, -200)
        sc.inputs["Scale"].default_value = 30 * scale; sc.inputs["Detail"].default_value = 8
        L(vec, sc.inputs["Vector"])
        rust = _n(nt, "ShaderNodeMapRange", -700, -200)
        rust.inputs["From Min"].default_value = 0.55; rust.inputs["From Max"].default_value = 0.68
        L(sc.outputs["Fac"], rust.inputs["Value"])
        rm = _n(nt, "ShaderNodeMix", -300, 200); rm.data_type = "RGBA"
        L(rust.outputs[0], rm.inputs["Factor"]); L(colour, rm.inputs[6])
        rm.inputs[7].default_value = srgb_lin(hex2 or "80401d")
        colour = rm.outputs[2]
        bump_h = sc.outputs["Fac"]
    if kind == "hair":
        st = _n(nt, "ShaderNodeTexWave", -900, -200)
        st.wave_type = "BANDS"; st.bands_direction = "X"
        st.inputs["Scale"].default_value = 160; st.inputs["Distortion"].default_value = 4.0
        st.inputs["Detail"].default_value = 3
        L(vec, st.inputs["Vector"])
        sr = _n(nt, "ShaderNodeMapRange", -700, -200)
        sr.inputs["To Min"].default_value = 0.65; sr.inputs["To Max"].default_value = 1.3
        L(st.outputs["Fac"], sr.inputs["Value"])
        hm = _n(nt, "ShaderNodeMix", -300, 200); hm.data_type = "RGBA"; hm.blend_type = "MULTIPLY"
        hm.inputs["Factor"].default_value = 1.0
        L(colour, hm.inputs[6]); L(sr.outputs[0], hm.inputs[7])
        colour = hm.outputs[2]
        bump_h = st.outputs["Fac"]

    # dust / grime rising from the ground, broken up by noise
    if grime > 0 and kind not in ("eye", "lens"):
        sep = _n(nt, "ShaderNodeSeparateXYZ", -900, 700)
        L(vec, sep.inputs[0])
        gz = _n(nt, "ShaderNodeMapRange", -700, 700)
        gz.inputs["From Min"].default_value = 0.0; gz.inputs["From Max"].default_value = 0.9
        gz.inputs["To Min"].default_value = 1.0; gz.inputs["To Max"].default_value = 0.15
        L(sep.outputs["Z"], gz.inputs["Value"])
        gn = _n(nt, "ShaderNodeTexNoise", -900, 900)
        gn.inputs["Scale"].default_value = 14; gn.inputs["Detail"].default_value = 6
        L(vec, gn.inputs["Vector"])
        gm = _n(nt, "ShaderNodeMath", -500, 800); gm.operation = "MULTIPLY"
        L(gz.outputs[0], gm.inputs[0]); L(gn.outputs["Fac"], gm.inputs[1])
        if "grime_mask" in _stats():
            # photographed dirt / sweat / oil stains: dark = dirty, mostly where the height ramp says so
            gvec = _vmath(nt, "SCALE", vec, x=-1050, y=1100, scale=1.0 / 0.45)
            gt = _tex_box(nt, _image("grime_mask"), gvec, -900, 1100, blend=0.3)
            gw = _n(nt, "ShaderNodeMapRange", -700, 1100)
            gw.inputs["From Min"].default_value = 0.75; gw.inputs["From Max"].default_value = 0.2
            gbw = _n(nt, "ShaderNodeRGBToBW", -800, 1200); L(gt.outputs["Color"], gbw.inputs[0])
            L(gbw.outputs[0], gw.inputs["Value"])
            gw2 = _n(nt, "ShaderNodeMath", -600, 1000); gw2.operation = "MULTIPLY_ADD"
            L(gw.outputs[0], gw2.inputs[0]); gw2.inputs[1].default_value = 0.55
            L(gm.outputs[0], gw2.inputs[2])
            gm = gw2
        gf = _n(nt, "ShaderNodeMath", -350, 800); gf.operation = "MULTIPLY"
        gf.inputs[1].default_value = grime * 1.6
        L(gm.outputs[0], gf.inputs[0])
        gc = _n(nt, "ShaderNodeMix", -100, 500); gc.data_type = "RGBA"
        gc.clamp_factor = True
        L(gf.outputs[0], gc.inputs["Factor"]); L(colour, gc.inputs[6])
        gc.inputs[7].default_value = srgb_lin(DUST)
        colour = gc.outputs[2]

    # sun fading on cloth: paler, greyer patches on the up-facing surfaces (shoulders, thighs, hat crowns)
    if tname and kind in ("cloth", "knit", "denim", "canvas", "check", "stripe"):
        fn = _n(nt, "ShaderNodeTexNoise", -900, 1500)
        fn.inputs["Scale"].default_value = 3.0; fn.inputs["Detail"].default_value = 3
        L(vec, fn.inputs["Vector"])
        fr = _n(nt, "ShaderNodeMapRange", -700, 1500)
        fr.inputs["From Min"].default_value = 0.45; fr.inputs["From Max"].default_value = 0.7
        fr.inputs["To Max"].default_value = 0.45
        L(fn.outputs["Fac"], fr.inputs["Value"])
        tcn = _n(nt, "ShaderNodeTexCoord", -900, 1700)
        sn = _n(nt, "ShaderNodeSeparateXYZ", -750, 1700); L(tcn.outputs["Normal"], sn.inputs[0])
        up = _n(nt, "ShaderNodeMapRange", -600, 1700)
        up.inputs["From Min"].default_value = -0.2; up.inputs["From Max"].default_value = 1.0
        up.inputs["To Min"].default_value = 0.35
        L(sn.outputs["Z"], up.inputs["Value"])
        ff = _n(nt, "ShaderNodeMath", -450, 1600); ff.operation = "MULTIPLY"
        L(fr.outputs[0], ff.inputs[0]); L(up.outputs[0], ff.inputs[1])
        hsv = _n(nt, "ShaderNodeHueSaturation", -300, 1400)
        hsv.inputs["Saturation"].default_value = 0.65; hsv.inputs["Value"].default_value = 1.3
        L(colour, hsv.inputs["Color"])
        fm = _n(nt, "ShaderNodeMix", -150, 1400); fm.data_type = "RGBA"; fm.clamp_factor = True
        L(ff.outputs[0], fm.inputs["Factor"]); L(colour, fm.inputs[6]); L(hsv.outputs["Color"], fm.inputs[7])
        colour = fm.outputs[2]

    # cavity darkening (painted ambient occlusion)
    if kind not in ("eye", "lens"):
        ao = _n(nt, "ShaderNodeAmbientOcclusion", 300, 600)
        ao.inputs["Distance"].default_value = 0.05
        ao.only_local = True
        L(colour, ao.inputs["Color"])
        ar = _n(nt, "ShaderNodeMapRange", 500, 700)
        ar.inputs["To Min"].default_value = 0.55; ar.inputs["To Max"].default_value = 1.0
        L(ao.outputs["AO"], ar.inputs["Value"])
        am = _n(nt, "ShaderNodeMix", 700, 500); am.data_type = "RGBA"; am.blend_type = "MULTIPLY"
        am.inputs["Factor"].default_value = 1.0
        L(colour, am.inputs[6]); L(ar.outputs[0], am.inputs[7])
        colour = am.outputs[2]

    if kind == "eye":
        tc = _n(nt, "ShaderNodeTexCoord", -900, -300)
        sep = _n(nt, "ShaderNodeSeparateXYZ", -700, -300)
        L(tc.outputs["Generated"], sep.inputs[0])
        # objects are authored facing Blender -Y: iris where y is most negative
        iris = _n(nt, "ShaderNodeMapRange", -500, -300)
        iris.inputs["From Min"].default_value = 0.085; iris.inputs["From Max"].default_value = 0.1
        iris.inputs["To Min"].default_value = 1.0; iris.inputs["To Max"].default_value = 0.0
        L(sep.outputs["Y"], iris.inputs["Value"])
        pup = _n(nt, "ShaderNodeMapRange", -500, -500)
        pup.inputs["From Min"].default_value = 0.025; pup.inputs["From Max"].default_value = 0.034
        pup.inputs["To Min"].default_value = 1.0; pup.inputs["To Max"].default_value = 0.0
        L(sep.outputs["Y"], pup.inputs["Value"])
        c1 = _n(nt, "ShaderNodeMix", -300, -300); c1.data_type = "RGBA"
        L(iris.outputs[0], c1.inputs["Factor"])
        c1.inputs[6].default_value = srgb_lin("e8e0d4"); c1.inputs[7].default_value = srgb_lin(hex2 or "4a3420")
        c2 = _n(nt, "ShaderNodeMix", -100, -300); c2.data_type = "RGBA"
        L(pup.outputs[0], c2.inputs["Factor"]); L(c1.outputs[2], c2.inputs[6])
        c2.inputs[7].default_value = srgb_lin("0a0806")
        colour = c2.outputs[2]
        rough = 0.15 if rough is None else rough

    L(colour, bs.inputs["Base Color"])
    bs.inputs["Roughness"].default_value = rough if rough is not None else {
        "skin": 0.55, "leather": 0.42, "metal": 0.4, "rubber": 0.75, "hair": 0.5, "lens": 0.08, "plastic": 0.35}.get(kind, 0.85)
    bs.inputs["Metallic"].default_value = metal if metal else (0.85 if kind == "metal" else 0.0)
    if kind == "skin":
        bs.inputs["Subsurface Weight"].default_value = 0.12
        bs.inputs["Subsurface Radius"].default_value = (0.012, 0.004, 0.002)
        bs.inputs["Subsurface Scale"].default_value = 0.6
    if kind == "lens":
        bs.inputs["Specular IOR Level"].default_value = 1.0
    if kind == "leather":
        bs.inputs["Coat Weight"].default_value = 0.25
        bs.inputs["Coat Roughness"].default_value = 0.35
    if kind == "hair":
        bs.inputs["Anisotropic"].default_value = 0.6 if "Anisotropic" in bs.inputs else 0
    if bump_h is not None:
        bp = _n(nt, "ShaderNodeBump", 800, -300)
        if tname:
            bp.inputs["Strength"].default_value = 0.45
            bp.inputs["Distance"].default_value = 0.0006
        else:
            bp.inputs["Strength"].default_value = {"leather": 0.25, "metal": 0.15, "hair": 0.35, "rubber": 0.2}.get(kind, 0.18)
            bp.inputs["Distance"].default_value = 0.002
        L(bump_h, bp.inputs["Height"])
        if nmap is not None:
            L(nmap, bp.inputs["Normal"])
        L(bp.outputs["Normal"], bs.inputs["Normal"])
    elif nmap is not None:
        L(nmap, bs.inputs["Normal"])
    m["fabric"] = tname or ""
    return m
