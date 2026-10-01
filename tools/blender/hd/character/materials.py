"""Hand-painted-look procedural materials (Eevee). Texture space = the mesh's rest position (`rest_position`
attribute), so patterns stick to the skin when the armature poses it. Colours are game hex values (sRGB)."""
import bpy

from hdlib import srgb_lin

DUST = "8a7a64"


def _n(nt, kind, x, y, **inputs):
    n = nt.nodes.new(kind)
    n.location = (x, y)
    for k, v in inputs.items():
        n.inputs[k].default_value = v
    return n


def make(name, hexcol, kind="cloth", rough=None, metal=0.0, grime=0.35, scale=1.0, hex2=None):
    """kind: skin cloth knit denim leather canvas metal rubber hair eye lens plastic"""
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
    if kind in ("check", "stripe"):
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
    if kind_w in ("cloth", "knit", "denim", "canvas"):
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
    if kind in ("leather", "rubber", "plastic"):
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
        gf = _n(nt, "ShaderNodeMath", -350, 800); gf.operation = "MULTIPLY"
        gf.inputs[1].default_value = grime * 1.6
        L(gm.outputs[0], gf.inputs[0])
        gc = _n(nt, "ShaderNodeMix", -100, 500); gc.data_type = "RGBA"
        gc.clamp_factor = True
        L(gf.outputs[0], gc.inputs["Factor"]); L(colour, gc.inputs[6])
        gc.inputs[7].default_value = srgb_lin(DUST)
        colour = gc.outputs[2]

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
        bp.inputs["Strength"].default_value = {"leather": 0.25, "metal": 0.15, "hair": 0.35, "rubber": 0.2}.get(kind, 0.18)
        bp.inputs["Distance"].default_value = 0.002
        L(bump_h, bp.inputs["Height"])
        L(bp.outputs["Normal"], bs.inputs["Normal"])
    return m
