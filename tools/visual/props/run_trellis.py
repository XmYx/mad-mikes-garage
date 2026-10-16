#!/usr/bin/env python3
"""Step 3 of the prop workflow (tools/visual/WORKFLOW.md): image -> 3D with TRELLIS.2-4B (MIT, local), one GLB with
PBR textures per prop picture. Run with the TRELLIS venv (~/ai/TRELLIS.2/.venv, torch cu130 + xformers):

    ~/ai/TRELLIS.2/.venv/bin/python tools/visual/props/run_trellis.py street_set.json [ids...] [--res=512|1024] [--force]

Weights: ~/ai/TRELLIS.2/weights/{TRELLIS.2-4B, TRELLIS-image-large, BiRefNet}; the image encoder DINOv3
(facebook/dinov3-vitl16-pretrain-lvd1689m, gated: accept its licence and `hf auth login`, or DINOV3=<local dir>).
Background removal is BiRefNet (MIT) instead of RMBG-2.0 (non-commercial). Writes <set>/glb/<id>.glb."""
import json
import os
import sys
import time

TRELLIS = os.path.expanduser("~/ai/TRELLIS.2")
W = os.path.join(TRELLIS, "weights")
sys.path.insert(0, TRELLIS)
os.environ.setdefault("ATTN_BACKEND", "xformers")
os.environ.setdefault("SPARSE_ATTN_BACKEND", "xformers")
os.environ["PYTORCH_CUDA_ALLOC_CONF"] = "expandable_segments:True"
os.environ["OPENCV_IO_ENABLE_OPENEXR"] = "1"

HERE = os.path.dirname(os.path.abspath(__file__))


def local_pipeline():
    """pipeline.json with the models pointing at the local copies (the decoder borrowed from TRELLIS-image-large,
    BiRefNet for the background), written next to the weights so relative ckpts resolve."""
    src = os.path.join(W, "TRELLIS.2-4B")
    cfg = json.load(open(os.path.join(src, "pipeline.json")))
    m = cfg["args"]["models"]
    for k, v in list(m.items()):
        if v.startswith("microsoft/TRELLIS-image-large/"):
            m[k] = os.path.join(W, "TRELLIS-image-large", v.split("microsoft/TRELLIS-image-large/")[1])
    cfg["args"]["rembg_model"] = {"name": "NoRembg", "args": {}}                     # inputs come cut out (cutout()): no remote model code
    if os.environ.get("TRELLIS_RES", "512") == "512":                                # the 1024 stages stay on disk (2.6 GB each in RAM)
        for k in [k for k in m if k.endswith("_1024")]:
            del m[k]
    dino = os.environ.get("DINOV3")
    if dino:
        cfg["args"]["image_cond_model"]["args"]["model_name"] = dino
    json.dump(cfg, open(os.path.join(src, "pipeline_local.json"), "w"), indent=1)
    return src


def cutout(im):
    """RGBA from a Krea picture on white: flood fill the background from every border pixel (tolerance for the soft
    studio falloff), keep the object, feather the edge one pixel."""
    from PIL import Image, ImageFilter
    import numpy as np
    rgb = np.asarray(im.convert("RGB"), np.int16)
    h, w, _ = rgb.shape
    bg = np.zeros((h, w), bool)
    light = (rgb.min(axis=2) > 222) & (rgb.max(axis=2) - rgb.min(axis=2) < 22)          # near-white, unsaturated
    from collections import deque
    q = deque()
    for x in range(w):
        for y in (0, h - 1):
            if light[y, x] and not bg[y, x]: bg[y, x] = True; q.append((y, x))
    for y in range(h):
        for x in (0, w - 1):
            if light[y, x] and not bg[y, x]: bg[y, x] = True; q.append((y, x))
    while q:
        y, x = q.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and light[ny, nx] and not bg[ny, nx]:
                bg[ny, nx] = True; q.append((ny, nx))
    a = Image.fromarray(np.where(bg, 0, 255).astype(np.uint8)).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.8))
    out = im.convert("RGB"); out.putalpha(a)
    return out


def main(argv):
    import torch
    from PIL import Image
    from trellis2.pipelines import Trellis2ImageTo3DPipeline
    import trellis2.pipelines.rembg as rembg

    class NoRembg:                                                                         # the pipeline only calls it on RGB inputs
        def __init__(self, **k): pass
        def to(self, *a): return self
        def cuda(self): return self
        def cpu(self): return self
        def __call__(self, image): raise RuntimeError("expected an RGBA cut-out")
    rembg.NoRembg = NoRembg
    import o_voxel

    res = next((a.split("=")[1] for a in argv if a.startswith("--res=")), "512")
    os.environ["TRELLIS_RES"] = res
    force = "--force" in argv
    args = [a for a in argv if not a.startswith("--")]
    spec = json.load(open(os.path.join(HERE, args[0])))
    only = set(args[1:])
    base = os.path.join(HERE, spec["set"])
    out = os.path.join(base, "glb")
    os.makedirs(out, exist_ok=True)

    pipe = Trellis2ImageTo3DPipeline.from_pretrained(local_pipeline(), config_file="pipeline_local.json")
    pipe.low_vram = False                                                                   # models stay on the GPU (24 GB): RAM stays free
    pipe.cuda()
    pipe.image_cond_model.model.to(torch.bfloat16)                                          # DINOv3 loads fp32 (fp16 overflows: empty structures); flows bf16, decoders fp16 already
    import gc

    def clean():
        gc.collect()
        torch.cuda.empty_cache()
        torch.cuda.ipc_collect()
    for p in spec["props"]:
        pid = p["id"]
        if only and pid not in only:
            continue
        dst = os.path.join(out, pid + ".glb")
        if os.path.exists(dst) and not force:
            continue
        t0 = time.time()
        img = cutout(Image.open(os.path.join(base, "images", pid + ".png")))
        img.save(os.path.join(out, pid + "_cut.png"))
        ptype = res if res != "1024" else "1024_cascade"
        try:
            try:
                mesh = pipe.run(img, seed=7, pipeline_type=ptype)[0]
            except torch.OutOfMemoryError:                                                 # the editor holds VRAM too: a smaller token budget
                clean()
                mesh = pipe.run(img, seed=7, pipeline_type=ptype, max_num_tokens=20000)[0]
        except Exception as e:                                                             # the CUDA context is spoilt: report, stop (the caller restarts)
            print("[trellis-fail] %s %s" % (pid, type(e).__name__), flush=True)
            sys.exit(3)
        try:                                                                                # CuMesh can die on odd shapes (CUDA error in simplify)
            mesh.simplify(300000)                                                           # keeps the CPU-side remesh / bake small
            glb = o_voxel.postprocess.to_glb(
                vertices=mesh.vertices, faces=mesh.faces, attr_volume=mesh.attrs, coords=mesh.coords,
                attr_layout=mesh.layout, voxel_size=mesh.voxel_size, aabb=[[-0.5, -0.5, -0.5], [0.5, 0.5, 0.5]],
                decimation_target=max(20000, p.get("faces", 4000) * 6), texture_size=1024,
                remesh=True, remesh_band=1, remesh_project=0, verbose=False)
            glb.export(dst)
        except Exception as e:                                                             # same as above: report this prop, the caller restarts for the rest
            print("[trellis-fail] %s %s" % (pid, type(e).__name__), flush=True)
            sys.exit(3)
        print("[trellis] %s %.1fs" % (pid, time.time() - t0), flush=True)
        del mesh, glb
        clean()


if __name__ == "__main__":
    main(sys.argv[1:])
