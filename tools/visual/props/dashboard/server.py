#!/usr/bin/env python3
"""Prop generation dashboard (tools/visual/WORKFLOW.md, step 2-4): browse the prop sets, edit prompts / sizes, add
props, generate Krea-2 pictures (variants by seed, pick one or upload your own), turn them into 3D with TRELLIS.2,
preview the GLBs, and publish a set to the game (Blender fix-up + HD export).

    python3 tools/visual/props/dashboard/server.py [--port 8797]     # then open http://127.0.0.1:8797

One job runs at a time. Pictures need ComfyUI (started on demand, memory capped); before TRELLIS runs, ComfyUI is
stopped so the GPU and RAM are free. TRELLIS runs in ~/ai/TRELLIS.2/.venv under a 20 GB memory cap."""
import base64
import glob
import json
import os
import re
import subprocess
import sys
import threading
import time
import urllib.request
from http.server import ThreadingHTTPServer, BaseHTTPRequestHandler

HERE = os.path.dirname(os.path.abspath(__file__))
PROPS = os.path.dirname(HERE)
REPO = os.path.dirname(os.path.dirname(os.path.dirname(PROPS)))
sys.path.insert(0, os.path.join(REPO, "tools", "blender", "hd", "character"))
import fabric_textures as FT  # noqa: E402  Krea-2 client

TRELLIS = os.path.expanduser("~/ai/TRELLIS.2")
COMFY_DIR = os.path.expanduser("~/ai/ComfyUI")
COMFY = "http://127.0.0.1:8188"
OVERSEER = "http://127.0.0.1:8195"     # GPU Overseer (~/apps/gpu-overseer): hands out VRAM, unloads idle GPU apps
TRELLIS_NEED_MB = 20000
lock = threading.RLock()
queue = []            # [{kind, set, id, seed?}]
status = {}           # "set/id" -> text
current = {"job": None, "since": 0}
priority = []        # sets whose jobs run before everything else
log = []


def say(msg):
    line = time.strftime("%H:%M:%S ") + msg
    print(line, flush=True)
    with lock:
        log.append(line)
        del log[:-200]


# ------------------------------------------------------------------ sets on disk
def set_files():
    return sorted(glob.glob(os.path.join(PROPS, "*_set.json")))


def load_set(name):
    return json.load(open(os.path.join(PROPS, name + "_set.json")))


def save_set(spec):
    p = os.path.join(PROPS, spec["set"] + "_set.json")
    tmp = p + ".tmp"
    json.dump(spec, open(tmp, "w"), indent=1)
    os.replace(tmp, p)


def find(spec, pid):
    return next((p for p in spec["props"] if p["id"] == pid), None)


def mtime(p):
    return int(os.path.getmtime(p)) if os.path.exists(p) else 0


def seed_of(variant):
    s = variant.rsplit("@", 1)[1][:-4]
    return int(s) if s.isdigit() else 0


def state():
    sets = []
    for f in set_files():
        spec = json.load(open(f))
        name = spec["set"]
        base = os.path.join(PROPS, name)
        props = []
        for p in spec["props"]:
            img = os.path.join(base, "images", p["id"] + ".png")
            glb = os.path.join(base, "glb", p["id"] + ".glb")
            variants = sorted((os.path.basename(v) for v in glob.glob(os.path.join(base, "images", p["id"] + "@*.png"))), key=seed_of)
            props.append(dict(p, image=mtime(img), glb=mtime(glb), variants=variants,
                              status=status.get(name + "/" + p["id"], "")))
        published = os.path.exists(os.path.join(REPO, "tools", "blender", "hd", "world", "props_gen_%s.blend" % name))
        sets.append({"set": name, "props": props, "style": spec["style"], "published": published, "hold": bool(spec.get("hold"))})
    with lock:
        return {"sets": sets, "priority": list(priority), "queue": list(queue), "current": current["job"], "since": current["since"],
                "log": log[-40:], "comfy": comfy_up()}


# ------------------------------------------------------------------ ComfyUI
def comfy_up():
    try:
        urllib.request.urlopen(COMFY + "/system_stats", timeout=1)
        return True
    except Exception:
        return False


def comfy_unit():
    """True when ComfyUI runs as the comfyui.service user unit (it is shared with Krea Studio)."""
    r = subprocess.run(["systemctl", "--user", "cat", "comfyui.service"], capture_output=True)
    return r.returncode == 0


def overseer(path, body):
    try:
        req = urllib.request.Request(OVERSEER + path, data=json.dumps(body).encode(), headers={"content-type": "application/json"})
        return json.loads(urllib.request.urlopen(req, timeout=200).read())
    except Exception:
        return None  # no overseer: fall back to stopping ComfyUI ourselves


def comfy_start():
    if comfy_up():
        return
    if comfy_unit():
        say("starting ComfyUI (comfyui.service)")
        subprocess.run(["systemctl", "--user", "start", "comfyui.service"])
        for _ in range(180):
            if comfy_up():
                return
            time.sleep(1)
        raise RuntimeError("ComfyUI did not come up")
    say("starting ComfyUI (16 GB cap)")
    subprocess.Popen(["systemd-run", "--user", "--scope", "-p", "MemoryMax=16G", "-p", "MemorySwapMax=0",
                      os.path.join(COMFY_DIR, "comfyenv", "bin", "python"), "main.py", "--listen", "127.0.0.1", "--port", "8188"],
                     cwd=COMFY_DIR, stdout=open(os.path.join(HERE, "comfy.log"), "a"), stderr=subprocess.STDOUT,
                     start_new_session=True)
    for _ in range(180):
        if comfy_up():
            return
        time.sleep(1)
    raise RuntimeError("ComfyUI did not come up")


def gpu_for_trellis():
    """Make room for TRELLIS. With the GPU Overseer: ask it for the VRAM (it unloads ComfyUI's models and other idle
    GPU apps, and ComfyUI keeps serving Krea Studio). Without it: stop ComfyUI as before."""
    g = overseer("/api/acquire", {"client": "props-trellis", "need_mb": TRELLIS_NEED_MB, "timeout": 180})
    if g is None:
        return comfy_stop()
    if not g.get("ok"):
        raise RuntimeError("GPU busy: %s MB free, TRELLIS needs %d" % (g.get("free_mb"), TRELLIS_NEED_MB))
    say("GPU Overseer granted %d MB for TRELLIS" % TRELLIS_NEED_MB)


def comfy_stop():
    out = subprocess.run(["ps", "-eo", "pid=,args="], capture_output=True, text=True).stdout
    for line in out.splitlines():
        parts = line.split(None, 2)
        if len(parts) == 3 and parts[1].endswith("comfyenv/bin/python") and parts[2].startswith("main.py"):
            os.kill(int(parts[0]), 15)
            say("stopped ComfyUI (frees the GPU for TRELLIS)")
    for _ in range(30):
        if not comfy_up():
            return
        time.sleep(1)


# ------------------------------------------------------------------ jobs
def run_image(job):
    spec = load_set(job["set"])
    p = find(spec, job["id"])
    if not p:
        return
    comfy_start()
    seed = job.get("seed") or p.get("seed") or 9100
    FT.STYLE = spec["style"]
    im = FT.generate(p["prompt"], seed=seed)
    d = os.path.join(PROPS, job["set"], "images")
    os.makedirs(d, exist_ok=True)
    im.save(os.path.join(d, "%s@%d.png" % (p["id"], seed)))
    im.save(os.path.join(d, p["id"] + ".png"))
    with lock:
        spec = load_set(job["set"])
        q = find(spec, job["id"])
        if q:
            q["seed"] = seed
            save_set(spec)


def run_models(set_name, ids):
    """TRELLIS in fresh processes until every prop is done or failed (a failure stops its process)."""
    gpu_for_trellis()
    try:
        left = list(ids)
        while left:
            run_models_once(set_name, left)
            nxt = [i for i in left if not re.match(r"3D (done|failed)", status.get(set_name + "/" + i, ""))]
            if len(nxt) == len(left):
                break
            left = nxt
    finally:
        overseer("/api/release", {"client": "props-trellis"})


def run_models_once(set_name, ids):
    env = dict(os.environ, DINOV3=os.path.join(TRELLIS, "weights", "dinov3-vitl16"))
    cmd = ["systemd-run", "--user", "--scope", "-p", "MemoryMax=20G", "-p", "MemorySwapMax=0",
           os.path.join(TRELLIS, ".venv", "bin", "python"), os.path.join(PROPS, "run_trellis.py"),
           set_name + "_set.json", "--force"] + ids
    say("TRELLIS: %s (%d)" % (set_name, len(ids)))
    pr = subprocess.Popen(cmd, cwd=REPO, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    tail = []
    for line in pr.stdout:
        tail = (tail + [line.rstrip()])[-15:]
        f = re.match(r"\[trellis-fail\] (\S+) (\S+)", line)
        if f:
            status[set_name + "/" + f.group(1)] = "3D failed (%s)" % f.group(2)
            say("3D %s/%s failed: %s" % (set_name, f.group(1), f.group(2)))
        m = re.match(r"\[trellis\] (\S+) ([\d.]+)s", line)
        if m:
            status[set_name + "/" + m.group(1)] = "3D done (%ss)" % m.group(2)
            say("3D %s/%s %ss" % (set_name, m.group(1), m.group(2)))
    pr.wait()
    if pr.returncode and pr.returncode != 3:
        say("TRELLIS failed (%d): %s" % (pr.returncode, " | ".join(t for t in tail if t)[-400:]))
        for i in ids:
            if not status.get(set_name + "/" + i, "").startswith("3D done"):
                status[set_name + "/" + i] = "3D failed"


def publish(set_name):
    say("publish %s: Blender fix-up" % set_name)
    subprocess.run(["blender", "-b", "-P", os.path.join(PROPS, "fixup_blend.py"), "--", set_name + "_set.json"],
                   cwd=REPO, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=True)
    say("publish %s: HD export (atlases + LODs)" % set_name)
    subprocess.run(["systemd-run", "--user", "--scope", "-p", "MemoryMax=16G", "-p", "MemorySwapMax=0", "python3",
                    os.path.join(REPO, "tools", "blender", "hd", "export", "run_export.py"), "--jobs", "1",
                    "world/props_gen_" + set_name], cwd=REPO, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=True)
    subprocess.run(["python3", os.path.join(PROPS, "swaps.py")], cwd=REPO, stdout=subprocess.DEVNULL, check=True)   # Gen_<id> stands in for its source
    say("published %s -> Assets/MadMax/Models/HD/world/Gen_* + Resources/HDGenSwaps.txt (refresh Unity)" % set_name)


def worker():
    while True:
        with lock:
            # pictures first (ComfyUI stays up), then 3D batches (ComfyUI stopped), publishing last
            order = [i for k in ("image", "model") for i, j in enumerate(queue) if j["set"] in priority and j["kind"] == k]   # priority sets first
            order += [i for k in ("image", "model", "publish") for i, j in enumerate(queue) if j["kind"] == k and j["set"] not in priority]
            job = queue.pop(order[0]) if order else None
            if job and job["kind"] == "model":                       # batch every queued model job of the same set
                ids = [job["id"]] + [j["id"] for j in queue if j["kind"] == "model" and j["set"] == job["set"]]
                queue[:] = [j for j in queue if not (j["kind"] == "model" and j["set"] == job["set"])]
                job = {"kind": "model", "set": job["set"], "ids": ids}
            current["job"] = job
            current["since"] = time.time()
        if not job:
            time.sleep(0.5)
            continue
        try:
            if job["kind"] == "image":
                status[job["set"] + "/" + job["id"]] = "drawing..."
                t0 = time.time()
                run_image(job)
                status[job["set"] + "/" + job["id"]] = "picture %.0fs" % (time.time() - t0)
            elif job["kind"] == "model":
                for i in job["ids"]:
                    status[job["set"] + "/" + i] = "3D running"
                run_models(job["set"], job["ids"])
            elif job["kind"] == "publish":
                publish(job["set"])
        except Exception as e:
            say("job failed: %s %s" % (job, e))
            if job.get("id"):
                status[job["set"] + "/" + job["id"]] = "failed: %s" % e
        with lock:
            current["job"] = None


# ------------------------------------------------------------------ HTTP
class H(BaseHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def send(self, code, body, ctype="application/json"):
        if isinstance(body, (dict, list)):
            body = json.dumps(body).encode()
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Cache-Control", "no-store")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        path = self.path.split("?")[0]
        if path == "/":
            return self.send(200, open(os.path.join(HERE, "index.html"), "rb").read(), "text/html; charset=utf-8")
        if path == "/api/state":
            return self.send(200, state())
        m = re.match(r"^/files/([a-z0-9_]+)/(images|glb)/([a-z0-9_@]+\.(?:png|glb))$", path)
        if m:
            f = os.path.join(PROPS, m.group(1), m.group(2), m.group(3))
            if os.path.isfile(f):
                ct = "model/gltf-binary" if f.endswith(".glb") else "image/png"
                return self.send(200, open(f, "rb").read(), ct)
        self.send(404, {"error": "not found"})

    def do_POST(self):
        n = int(self.headers.get("Content-Length", 0))
        try:
            data = json.loads(self.rfile.read(n) or b"{}")
            return self.send(200, self.api(self.path, data))
        except Exception as e:
            return self.send(400, {"error": str(e)})

    def api(self, path, d):
        name = d.get("set", "")
        if name and not re.match(r"^[a-z0-9_]+$", name):
            raise ValueError("set names are lower_case_with_underscores")
        pid = d.get("id", "")
        if pid and not re.match(r"^[a-z0-9_]+$", pid):
            raise ValueError("ids are lower_case_with_underscores")
        with lock:
            if path == "/api/newset":
                if os.path.exists(os.path.join(PROPS, name + "_set.json")):
                    raise ValueError("set exists")
                save_set({"set": name, "style": load_set("street")["style"], "props": []})
            elif path == "/api/prop":                                # add or edit
                spec = load_set(name)
                p = find(spec, pid)
                if not p:
                    p = {"id": pid, "prompt": "", "size_m": 1.0, "mount": "floor", "faces": 4000}
                    spec["props"].append(p)
                for k in ("prompt", "mount"):
                    if k in d:
                        p[k] = str(d[k])
                if "size_m" in d:
                    p["size_m"] = float(d["size_m"])
                if "faces" in d:
                    p["faces"] = int(d["faces"])
                save_set(spec)
            elif path == "/api/style":
                spec = load_set(name)
                spec["style"] = str(d["style"])
                save_set(spec)
            elif path == "/api/delete":
                spec = load_set(name)
                spec["props"] = [p for p in spec["props"] if p["id"] != pid]
                save_set(spec)
            elif path == "/api/pick":                                # a variant becomes the starting image
                v = d["variant"]
                if not re.match(r"^[a-z0-9_]+@\d+\.png$", v):
                    raise ValueError("bad variant")
                src = os.path.join(PROPS, name, "images", v)
                dst = os.path.join(PROPS, name, "images", pid + ".png")
                open(dst, "wb").write(open(src, "rb").read())
                spec = load_set(name)
                find(spec, pid)["seed"] = seed_of(v)
                save_set(spec)
            elif path == "/api/upload":                              # your own starting image (data URL)
                from io import BytesIO
                from PIL import Image
                raw = base64.b64decode(d["data"].split(",", 1)[1])
                im = Image.open(BytesIO(raw)).convert("RGB")
                im.thumbnail((1024, 1024))
                d_img = os.path.join(PROPS, name, "images")
                os.makedirs(d_img, exist_ok=True)
                seed = 1 + max([0] + [seed_of(v) for v in glob.glob(os.path.join(d_img, pid + "@*.png"))])
                im.save(os.path.join(d_img, "%s@%d.png" % (pid, seed)))
                im.save(os.path.join(d_img, pid + ".png"))
                status[name + "/" + pid] = "uploaded"
            elif path == "/api/approve":                             # on-hold sets (vehicles) generate only after this
                spec = load_set(name)
                spec["hold"] = False
                save_set(spec)
                say("set %s approved for generation" % name)
            elif path == "/api/job":
                if load_set(name).get("hold"):
                    raise ValueError("set '%s' is on hold: approve it first" % name)
                kind = d["kind"]
                ids = [pid] if pid else [p["id"] for p in load_set(name)["props"]]
                for i in ids:
                    if kind in ("image", "both"):
                        has = os.path.exists(os.path.join(PROPS, name, "images", i + ".png"))
                        if not (d.get("missing") and has):
                            seed = int(d["seed"]) if d.get("seed") else None
                            if d.get("reroll"):
                                p = find(load_set(name), i)
                                vs = glob.glob(os.path.join(PROPS, name, "images", i + "@*.png"))
                                seed = max([p.get("seed") or 9100] + [seed_of(v) for v in vs]) + 7919
                            queue.append({"kind": "image", "set": name, "id": i, "seed": seed})
                            status[name + "/" + i] = "queued (picture)"
                    if kind in ("model", "both"):
                        has = os.path.exists(os.path.join(PROPS, name, "glb", i + ".glb"))
                        if not (d.get("missing") and has):
                            queue.append({"kind": "model", "set": name, "id": i})
                            if kind == "model":
                                status[name + "/" + i] = "queued (3D)"
            elif path == "/api/all":                                 # every set: missing pictures, then missing 3D
                for f in set_files():
                    spec = json.load(open(f))
                    if spec.get("hold"):
                        if not d.get("approve_held"):
                            continue
                        spec["hold"] = False
                        save_set(spec)
                    n = spec["set"]
                    for p in spec["props"]:
                        if not os.path.exists(os.path.join(PROPS, n, "images", p["id"] + ".png")):
                            queue.append({"kind": "image", "set": n, "id": p["id"], "seed": None})
                            status[n + "/" + p["id"]] = "queued (picture)"
                    for p in spec["props"]:
                        if not os.path.exists(os.path.join(PROPS, n, "glb", p["id"] + ".glb")):
                            queue.append({"kind": "model", "set": n, "id": p["id"]})
                say("queued everything missing: %d jobs" % len(queue))
            elif path == "/api/publish":
                if load_set(name).get("hold"):
                    raise ValueError("set '%s' is on hold" % name)
                queue.append({"kind": "publish", "set": name})
            elif path == "/api/priority":
                if name in priority:
                    priority.remove(name)
                else:
                    priority.append(name)
                say("priority sets: %s" % (", ".join(priority) or "none"))
            elif path == "/api/clear":
                queue.clear()
            else:
                raise ValueError("unknown " + path)
        return {"ok": True}


def main():
    port = int(sys.argv[sys.argv.index("--port") + 1]) if "--port" in sys.argv else 8797
    threading.Thread(target=worker, daemon=True).start()
    say("dashboard on http://127.0.0.1:%d" % port)
    ThreadingHTTPServer(("127.0.0.1", port), H).serve_forever()


if __name__ == "__main__":
    main()
