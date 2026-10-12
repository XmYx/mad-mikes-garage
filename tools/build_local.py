#!/usr/bin/env python3
"""Local player build with the HD asset pack: render the HD pack from the committed Blender sources, the HD terrain
textures, then build the player through CiBuild. Nothing is committed, tagged or published (tools/release.sh does that).

    python3 tools/build_local.py                      # HD pack + terrain + macOS player -> Builds/Local/macOS
    python3 tools/build_local.py --platform linux --platform windows
    python3 tools/build_local.py --no-hd              # build with whatever Models/HD already holds
    python3 tools/build_local.py --hd-only            # just bring the HD pack up to date
    python3 tools/build_local.py --zip                # also package the player next to it

Blender: --blender, $BLENDER, `blender` on PATH, /Applications/Blender.app, Spotlight, or a blender-*.dmg in
~/Downloads (mounted read-only for the run and detached afterwards). On Apple Silicon the bake runs on Metal.
Unity: --unity, $UNITY, else the Hub editor matching ProjectSettings/ProjectVersion.txt (or the newest 6000.x
installed). With the project closed the build runs in batch mode; with an editor open it is queued inside that editor
through the Unity AI Assistant MCP relay. The HD export resumes: an interrupted run picks up where it stopped.
"""
import argparse
import atexit
import glob
import json
import os
import platform
import queue
import re
import shutil
import subprocess
import sys
import tempfile
import threading
import time
from concurrent.futures import FIRST_COMPLETED, ThreadPoolExecutor, wait

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
HD_EXPORT = os.path.join(ROOT, "tools", "blender", "hd", "export")
TERRAIN = os.path.join(ROOT, "tools", "blender", "hd", "terrain", "make_textures.py")
GAME = "MadMikesGarage"
TARGETS = {  # --platform -> (BuildTarget, player path under the output folder, PlaybackEngines module)
    "mac": ("StandaloneOSX", "macOS/%s.app" % GAME, "MacStandaloneSupport"),
    "linux": ("StandaloneLinux64", "Linux/%s.x86_64" % GAME, "LinuxStandaloneSupport"),
    "windows": ("StandaloneWindows64", "Windows/%s.exe" % GAME, "WindowsStandaloneSupport"),
}

# ================================================================================================= terminal output
TTY = sys.stdout.isatty() and os.environ.get("TERM") != "dumb"
NO_COLOR = not TTY or "NO_COLOR" in os.environ


def c(code, s):
    return s if NO_COLOR else "\033[%sm%s\033[0m" % (code, s)


def dim(s): return c("2", s)
def bold(s): return c("1", s)
def green(s): return c("32", s)
def red(s): return c("31", s)
def yellow(s): return c("33", s)
def cyan(s): return c("36", s)


def fmt_dur(s):
    s = int(max(0, s))
    if s >= 3600:
        return "%dh%02dm" % (s // 3600, s % 3600 // 60)
    return "%dm%02ds" % (s // 60, s % 60) if s >= 60 else "%ds" % s


def fmt_bytes(n):
    for u in ("B", "KB", "MB", "GB"):
        if n < 1024 or u == "GB":
            return "%.1f %s" % (n, u) if u != "B" else "%d B" % n
        n /= 1024.0


def bar(frac, width):
    """Smooth unicode bar (eighth blocks)."""
    frac = min(1.0, max(0.0, frac))
    if NO_COLOR:
        n = int(round(frac * width))
        return "[" + "#" * n + "-" * (width - n) + "]"
    cells = frac * width
    full = int(cells)
    part = " ▏▎▍▌▋▊▉"[int((cells - full) * 8)] if full < width else ""
    return c("36", "█" * full + part.strip()) + c("2", "░" * (width - full - len(part.strip())))


SPIN = "⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏"


class Panel:
    """A block of status lines redrawn in place (TTY), or occasional plain lines (logs, CI)."""

    def __init__(self):
        self.lines_drawn = 0
        self.lock = threading.Lock()
        self.last_plain = 0.0

    def draw(self, lines, plain=None, force_plain=False):
        with self.lock:
            if TTY:
                w = shutil.get_terminal_size((100, 20)).columns
                out = ("\033[%dA" % self.lines_drawn if self.lines_drawn else "")
                out += "".join("\r\033[2K" + clip(ln, w - 1) + "\n" for ln in lines) + "\033[J"
                sys.stdout.write(out)
                sys.stdout.flush()
                self.lines_drawn = len(lines)
            elif plain and (force_plain or time.time() - self.last_plain > 30):
                print(plain, flush=True)
                self.last_plain = time.time()

    def above(self, text):
        """Print a permanent line above the live block."""
        with self.lock:
            if TTY and self.lines_drawn:
                sys.stdout.write("\033[%dA\r\033[J" % self.lines_drawn)
                self.lines_drawn = 0
            print(text, flush=True)

    def close(self):
        """Leave the block on screen as it is."""
        with self.lock:
            self.lines_drawn = 0

    def clear(self):
        """Erase the block."""
        with self.lock:
            if TTY and self.lines_drawn:
                sys.stdout.write("\033[%dA\r\033[J" % self.lines_drawn)
                sys.stdout.flush()
            self.lines_drawn = 0


ANSI = re.compile(r"\033\[[0-9;]*m")


def clip(s, w):
    """Clip to w visible columns, keeping colour codes intact."""
    if len(ANSI.sub("", s)) <= w:
        return s
    out, vis = [], 0
    for tok in re.split(r"(\033\[[0-9;]*m)", s):
        if ANSI.fullmatch(tok or "x"):
            out.append(tok)
        else:
            take = tok[: max(0, w - 1 - vis)]
            out.append(take)
            vis += len(take)
            if vis >= w - 1:
                out.append("…")
                break
    return "".join(out) + ("" if NO_COLOR else "\033[0m")


def step(n, total, title):
    print("\n" + bold(cyan("[%d/%d] " % (n, total)) + title), flush=True)


def ok(msg): print("  " + green("✓ ") + msg, flush=True)
def warn(msg): print("  " + yellow("! ") + msg, flush=True)


def die(msg):
    print("\n  " + red("✗ " + msg), flush=True)
    sys.exit(1)


# ================================================================================================= tools discovery
MOUNTS = []


def detach_mounts():
    for m in MOUNTS:
        subprocess.run(["hdiutil", "detach", "-quiet", m], capture_output=True)
        shutil.rmtree(m, ignore_errors=True)


def find_blender(explicit):
    cands = [explicit, os.environ.get("BLENDER"), shutil.which("blender"),
             "/Applications/Blender.app/Contents/MacOS/Blender",
             os.path.expanduser("~/Applications/Blender.app/Contents/MacOS/Blender")]
    for b in cands:
        if b and os.path.isfile(b) and os.access(b, os.X_OK):
            return b, None
    if sys.platform == "darwin":
        r = subprocess.run(["mdfind", "kMDItemCFBundleIdentifier == 'org.blenderfoundation.blender'"],
                           capture_output=True, text=True)
        for app in r.stdout.split("\n"):
            b = os.path.join(app.strip(), "Contents/MacOS/Blender")
            if app.strip() and os.path.isfile(b):
                return b, None
        dmgs = sorted(glob.glob(os.path.expanduser("~/Downloads/[Bb]lender-*.dmg")), key=os.path.getmtime)
        # an image left attached by an earlier run (closed terminal, crash) cannot be attached twice: reuse it
        info = subprocess.run(["hdiutil", "info"], capture_output=True, text=True).stdout
        for block in info.split("================================================"):
            m = re.search(r"image-path\s*:\s*(.+)", block)
            if m and os.path.basename(m.group(1).strip()).lower().startswith("blender-"):
                for mp in re.findall(r"^/dev/disk\S+[ \t]+\S+[ \t]+(/.+)$", block, re.M):
                    b = os.path.join(mp.strip(), "Blender.app/Contents/MacOS/Blender")
                    if os.path.isfile(b):
                        return b, m.group(1).strip()
        if dmgs:
            mnt = tempfile.mkdtemp(prefix="blender-")
            r = subprocess.run(["hdiutil", "attach", "-nobrowse", "-readonly", "-noverify", "-mountpoint", mnt, dmgs[-1]],
                               capture_output=True, text=True)
            if r.returncode == 0:
                MOUNTS.append(mnt)
                b = os.path.join(mnt, "Blender.app/Contents/MacOS/Blender")
                if os.path.isfile(b):
                    return b, dmgs[-1]
    return None, None


def project_unity_version():
    m = re.search(r"m_EditorVersion: (\S+)", open(os.path.join(ROOT, "ProjectSettings/ProjectVersion.txt")).read())
    return m.group(1) if m else ""


def unity_exe(editor_dir):
    if sys.platform == "darwin":
        return os.path.join(editor_dir, "Unity.app/Contents/MacOS/Unity")
    if os.name == "nt":
        return os.path.join(editor_dir, "Editor", "Unity.exe")
    return os.path.join(editor_dir, "Editor", "Unity")


def find_unity(explicit):
    """-> (Unity executable, version, note)."""
    for u in (explicit, os.environ.get("UNITY")):
        if u and os.path.isfile(u):
            return u, "", ""
    want = project_unity_version()
    roots = ["/Applications/Unity/Hub/Editor", os.path.expanduser("~/Unity/Hub/Editor"),
             os.path.expanduser("~/Applications/Unity/Hub/Editor"), r"C:\Program Files\Unity\Hub\Editor"]
    found = {}
    for r in roots:
        for d in glob.glob(os.path.join(r, "*")):
            if os.path.isfile(unity_exe(d)):
                found.setdefault(os.path.basename(d), d)
    if want in found:
        return unity_exe(found[want]), want, ""
    same = sorted((v for v in found if v.split(".")[0] == want.split(".")[0]),
                  key=lambda v: [int(x) if x.isdigit() else x for x in re.split(r"[.a-z]", v)])
    if same:
        v = same[-1]
        return unity_exe(found[v]), v, "project is on %s, using installed %s" % (want, v)
    return None, want, ""


def playback_engines(unity):
    """PlaybackEngines folders for an editor (Hub layout on macOS keeps them next to Unity.app)."""
    d = os.path.dirname(unity)
    for _ in range(4):
        p = os.path.join(d, "PlaybackEngines")
        if os.path.isdir(p):
            return p
        d = os.path.dirname(d)
    return ""


def editor_open():
    """PID of a Unity editor holding this project, or None."""
    if not os.path.exists(os.path.join(ROOT, "Temp", "UnityLockfile")):
        return None
    r = subprocess.run(["pgrep", "-if", "-projectpath %s( |$)" % re.escape(ROOT)], capture_output=True, text=True)
    pids = [int(p) for p in r.stdout.split() if p.strip()]
    return pids[0] if pids else None


def next_version():
    tags = subprocess.run(["git", "-C", ROOT, "tag"], capture_output=True, text=True).stdout.split()
    vs = [tuple(int(x) for x in t[1:].split(".")[:2]) for t in tags if re.fullmatch(r"v\d+\.\d+(\.\d+)?", t)]
    M, m = max(vs) if vs else (0, 0)
    return "%d.%d.0" % (M, m + 1)


# ================================================================================================= HD pack
EXCLUSIVE = ("character/",)   # jobs so heavy that only one of them runs at a time (others still run beside it)


def free_gb():
    """Memory available for new processes (free + inactive + purgeable + speculative pages on macOS)."""
    try:
        if sys.platform == "darwin":
            txt = subprocess.run(["vm_stat"], capture_output=True, text=True).stdout
            page = int(re.search(r"page size of (\d+)", txt).group(1))
            pages = sum(int(m) for k, m in re.findall(r"Pages (free|inactive|speculative|purgeable):\s+(\d+)", txt))
            return pages * page / 2 ** 30
        for ln in open("/proc/meminfo"):
            if ln.startswith("MemAvailable:"):
                return int(ln.split()[1]) / 2 ** 20
    except (OSError, AttributeError, ValueError):
        pass
    return float("inf")


def hd_export(blender, jobs, force, filters, panel, cpu=False, min_free=4.0):
    os.environ["BLENDER"] = blender
    sys.path.insert(0, HD_EXPORT)
    import run_export as rx  # noqa: E402  (reads $BLENDER at import)
    rx.BLENDER = blender

    messages = []

    def quiet_log(msg):  # keep the runner's log file, keep its chatter off the live panel
        os.makedirs(os.path.dirname(rx.LOG), exist_ok=True)
        with open(rx.LOG, "a") as f:
            f.write(time.strftime("%Y-%m-%d %H:%M:%S ") + msg + "\n")
        messages.append(msg)
    rx.log = quiet_log

    J = rx.jobs()
    if cpu:
        J = {n: (g, b, e + ["--cpu"]) for n, (g, b, e) in J.items()}
    names = [n for n in J if not filters or any(n == f or n.startswith(f) for f in filters)]
    names.sort(key=lambda n: -os.path.getsize(J[n][1]) if os.path.exists(J[n][1]) else 0)   # big files first
    out = os.path.join(ROOT, "Assets", "MadMax", "Models", "HD")
    os.makedirs(out, exist_ok=True)

    # which jobs need work (hashes every source .blend once)
    stale, t0 = [], time.time()
    for i, n in enumerate(names, 1):
        if force or not os.path.exists(J[n][1]) or not rx.up_to_date(n, J[n][1]):
            stale.append(n)
        if i % 8 == 0 or i == len(names):
            panel.draw(["  %s checking sources %s %d/%d" % (SPIN[i // 8 % len(SPIN)], bar(i / len(names), 30), i, len(names))],
                       "checking sources %d/%d" % (i, len(names)))
    panel.clear()
    fresh = len(names) - len(stale)
    ok("%d HD jobs, %d up to date, %s to render" % (len(names), fresh, bold(str(len(stale)))))
    if not stale:
        rx.index(out)
        return True

    rx.log("export start: %d jobs (%d stale), %d parallel, %s bake -> %s (tools/build_local.py)"
           % (len(names), len(stale), jobs, "CPU" if cpu else "GPU", out))
    lock = threading.Lock()
    running = {}           # name -> start time
    done = {"ok": 0, "fail": 0, "missing": 0, "skip": 0}
    failed, durations = [], []
    stop = threading.Event()
    status = {"wait": ""}  # why the scheduler is holding a job back

    def job(name):
        if stop.is_set():
            return name, "skip", 0.0, "cancelled"
        with lock:
            running[name] = time.time()
        try:
            r = rx.run(name, J[name], out, force)
            if r[1] == "fail" and not stop.is_set():   # Metal now and then crashes a bake outright: one retry
                rx.log("[retry] %s after: %s" % (name, (r[3].strip().splitlines() or [""])[-1][:160]))
                r2 = rx.run(name, J[name], out, force)
                r = (r2[0], r2[1], r[2] + r2[2], r2[3])
            return r
        finally:
            with lock:
                running.pop(name, None)

    t0 = time.time()

    def render(final=False):
        with lock:
            n_done = sum(done.values())
            el = time.time() - t0
            frac = n_done / len(stale)
            # throughput over finished jobs; before the first finishes, guess from the in-flight ages
            eta = (el / n_done) * (len(stale) - n_done) if n_done else 0
            spin = SPIN[int(time.time() * 8) % len(SPIN)]
            w = shutil.get_terminal_size((100, 20)).columns
            bw = max(20, min(50, w - 60))
            lines = ["  %s %s %s %s  %s elapsed  %s" % (
                green("●") if final else cyan(spin), bar(frac, bw), bold("%5.1f%%" % (frac * 100)),
                "%d/%d" % (n_done, len(stale)), fmt_dur(el),
                ("ETA " + bold(fmt_dur(eta))) if n_done and not final else "")]
            lines.append("    %s  %s  %s  %s" % (
                green("ok %d" % done["ok"]), (red if done["fail"] else dim)("failed %d" % (done["fail"] + done["missing"])),
                dim("running %d/%d" % (len(running), jobs)) + dim("  %.1f GB free" % free_gb() if not final else ""),
                dim("avg %.0fs/job" % (sum(durations) / len(durations))) if durations else ""))
            if not final:
                for n, st in sorted(running.items(), key=lambda kv: kv[1]):
                    lines.append("    %s %-46s %s" % (cyan("▸"), n, dim(fmt_dur(time.time() - st))))
                idle = min(jobs, len(stale) - n_done) - len(running)
                if idle > 0 and status["wait"]:
                    lines.append("    %s %s" % (yellow("⏸"), dim(status["wait"])))
                    idle -= 1
                lines += [""] * max(0, idle)
            plain = "HD %d/%d (%.0f%%) ok %d failed %d, %s elapsed, ETA %s" % (
                n_done, len(stale), frac * 100, done["ok"], done["fail"] + done["missing"], fmt_dur(el), fmt_dur(eta))
        panel.draw(lines, plain, force_plain=final)

    ticker_stop = threading.Event()

    def ticker():
        while not ticker_stop.wait(0.25 if TTY else 5):
            render()
    th = threading.Thread(target=ticker, daemon=True)
    th.start()
    ex = ThreadPoolExecutor(max_workers=jobs)
    pending, futs = list(stale), set()

    def pick():
        """Next job that may start now: free slot, enough memory, at most one EXCLUSIVE job running."""
        if len(futs) >= jobs or not pending:
            return None
        if futs and free_gb() < min_free:     # always let one job run, however tight memory is
            status["wait"] = "waiting for memory (%.1f GB free, want %.0f)" % (free_gb(), min_free)
            return None
        with lock:
            heavy = any(r.startswith(EXCLUSIVE) for r in running)
        for i, n in enumerate(pending):
            if not (heavy and n.startswith(EXCLUSIVE)):
                status["wait"] = ""
                return pending.pop(i)
        status["wait"] = "holding %d character job(s) until the running one finishes" % len(pending)
        return None

    def finished_one(f):
        name, st, dt, msg = f.result()
        with lock:
            done[st] += 1
            if st == "ok":
                durations.append(dt)
            k = sum(done.values())
        if st != "skip":
            rx.log("[%d/%d] %-4s %-48s %6.1fs %s" % (k, len(stale), st.upper(), name, dt, msg if st != "fail" else ""))
        if st in ("fail", "missing"):
            failed.append(name)
            rx.log("  " + msg.replace("\n", "\n  "))
            panel.above("    " + red("✗ ") + "%s  %s" % (name, dim((msg.strip().splitlines() or [""])[-1][:120])))

    try:
        while pending or futs:
            while True:
                n = pick()
                if n is None:
                    break
                with lock:
                    running[n] = time.time()   # shown at once, before the thread gets going
                futs.add(ex.submit(job, n))
                time.sleep(2)                  # stagger starts so memory readings catch up with the new process
            if not futs:
                time.sleep(1)
                continue
            fin, _ = wait(futs, timeout=2, return_when=FIRST_COMPLETED)
            for f in fin:
                futs.discard(f)
                finished_one(f)
    except KeyboardInterrupt:
        stop.set()
        ticker_stop.set()
        ex.shutdown(wait=False, cancel_futures=True)
        panel.close()
        print()
        die("interrupted: finished jobs are kept, run again to resume")
    ticker_stop.set()
    th.join()
    ex.shutdown()
    render(final=True)
    panel.close()

    rows = rx.index(out)
    rx.report(rows)
    rx.log("export done in %.1f min: %s; failed: %s" % ((time.time() - t0) / 60, done, failed or "none"))
    size = sum(r["bytes"] for r in rows)
    ok("HD pack: %d assets, %s in %s" % (len(rows), fmt_bytes(size), fmt_dur(time.time() - t0)))
    if failed:
        warn("%d jobs failed (per-job logs in Logs/hd_export/): %s" % (len(failed), ", ".join(failed[:12])))
        return False
    return True


def has_terrain_deps(py):
    return subprocess.run([py, "-c", "import numpy, PIL"], capture_output=True).returncode == 0


def terrain_python(panel):
    """A Python with numpy + Pillow for make_textures.py: this one, or a cached venv made on first use."""
    if has_terrain_deps(sys.executable):
        return sys.executable
    venv = os.path.expanduser("~/.cache/mad-mikes-garage/venv")
    py = os.path.join(venv, "Scripts" if os.name == "nt" else "bin", "python3" if os.name != "nt" else "python.exe")
    if os.path.isfile(py) and has_terrain_deps(py):
        return py
    base = next((b for b in (shutil.which("python3.13"), shutil.which("python3.12"), sys.executable) if b), sys.executable)
    rc, txt, _ = run_with_spinner([base, "-m", "venv", venv], "creating venv for numpy + Pillow", panel)
    if rc == 0:
        rc, txt, _ = run_with_spinner([py, "-m", "pip", "install", "-q", "--disable-pip-version-check", "numpy", "pillow"],
                                      "installing numpy + Pillow", panel)
    if rc != 0 or not has_terrain_deps(py):
        print(txt[-1500:])
        die("could not set up numpy + Pillow for the terrain textures (pip install numpy pillow)")
    ok("numpy + Pillow venv: " + dim(venv))
    return py


def run_with_spinner(cmd, label, panel, env=None, cwd=ROOT):
    t0 = time.time()
    p = subprocess.Popen(cmd, cwd=cwd, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    out, q = [], queue.Queue()
    threading.Thread(target=lambda: [q.put(ln) for ln in p.stdout] + [q.put(None)], daemon=True).start()
    last = ""
    while True:
        try:
            ln = q.get(timeout=0.2)
            if ln is None:
                break
            out.append(ln)
            last = ln.strip() or last
        except queue.Empty:
            pass
        panel.draw(["  %s %s  %s  %s" % (cyan(SPIN[int(time.time() * 8) % len(SPIN)]), label, dim(fmt_dur(time.time() - t0)),
                                         dim(last[:90]))], "%s %s" % (label, last[:90]))
    p.wait()
    panel.clear()
    return p.returncode, "".join(out), time.time() - t0


# ================================================================================================= Unity build
STAGES = [  # (label, log regex) in the order a build passes through them
    ("starting editor", r"COMMAND LINE ARGUMENTS|Successfully changed project path"),
    ("importing assets", r"Asset Pipeline Refresh|Start importing|Refreshing native plugins"),
    ("compiling scripts", r"\[ScriptCompilation\]|bee_backend|Tundra build|ScriptCompilationBuildProgram"),
    ("regenerating content", r"\[MadMax\]|BuildGameScene"),
    ("building player", r"BuildPlayer|Building Player|DisplayProgressbar: Build|Building scene|Preparing assets for build"),
    ("compiling shaders", r"Compiling shader "),
    ("writing player", r"Build Asset Bundles|Packaging assets|Creating .*\.app|Copying|Writing files|Build Finished"),
]


class LogFollower:
    """Follows a Unity log and turns it into a stage bar."""

    def __init__(self, path, start=0):
        self.path, self.pos, self.buf = path, start, ""
        self.stage, self.shaders, self.last, self.lines = 0, 0, "", []

    def poll(self):
        try:
            with open(self.path, "r", errors="replace") as f:
                if os.path.getsize(self.path) < self.pos:
                    self.pos = 0
                f.seek(self.pos)
                data = f.read()
                self.pos = f.tell()
        except OSError:
            return
        self.buf += data
        *done, self.buf = self.buf.split("\n")
        for ln in done:
            self.lines.append(ln)
            for i, (_, rx) in enumerate(STAGES):
                if i >= self.stage and re.search(rx, ln):
                    self.stage = i
            if ln.startswith("Compiling shader "):
                self.shaders += 1
            s = ln.strip()
            if s and not s.startswith(("UnityEngine.", "UnityEditor.", "(Filename", "System.", "at ", "[Licensing")) and len(s) > 8:
                self.last = s
        del self.lines[:-400]

    def draw(self, panel, t0, title):
        lab = STAGES[self.stage][0]
        frac = (self.stage + 0.5) / len(STAGES)
        spin = cyan(SPIN[int(time.time() * 8) % len(SPIN)])
        extra = dim("  %d shaders" % self.shaders) if self.shaders else ""
        panel.draw(["  %s %s %s  %s %s  %s%s" % (spin, title, bar(frac, 24), bold(lab), dim("(%d/%d)" % (self.stage + 1, len(STAGES))),
                                                 dim(fmt_dur(time.time() - t0)), extra),
                    "    " + dim(self.last[:140])],
                   "%s: %s, %s elapsed" % (title, lab, fmt_dur(time.time() - t0)))


def batch_build(unity, target, path, version, regen, log, panel):
    cmd = [unity, "-batchmode", "-nographics", "-quit", "-projectPath", ROOT,
           "-executeMethod", "MadMax.EditorTools.CiBuild.Build", "-customBuildTarget", target, "-customBuildPath", path,
           "-buildVersion", version, "-regenerate", "true" if regen else "false", "-logFile", log]
    if os.path.exists(log):
        os.remove(log)
    t0 = time.time()
    p = subprocess.Popen(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    fol = LogFollower(log)
    try:
        while p.poll() is None:
            fol.poll()
            fol.draw(panel, t0, target)
            time.sleep(0.25)
    except KeyboardInterrupt:
        p.terminate()
        panel.close()
        die("interrupted")
    fol.poll()
    panel.clear()
    text = open(log, errors="replace").read() if os.path.exists(log) else ""
    m = re.search(r"CI build %s .*" % target, text)
    good = p.returncode == 0 and m and "Succeeded" in m.group(0)
    return bool(good), (m.group(0) if m else ""), time.time() - t0, fol


def find_relay():
    for pat in ("~/.unity/relay/relay_*.app/Contents/MacOS/relay_*",
                ROOT + "/Library/PackageCache/com.unity.ai.assistant@*/RelayApp~/relay_*.app/Contents/MacOS/relay_*",
                ROOT + "/Library/PackageCache/com.unity.ai.assistant@*/RelayApp~/relay_*"):
        for f in sorted(glob.glob(os.path.expanduser(pat))):
            if os.path.isfile(f) and os.access(f, os.X_OK):
                return f
    return None


class Mcp:
    """Minimal stdio MCP client for the Unity AI Assistant relay (`relay --mcp`)."""

    def __init__(self, relay):
        self.p = subprocess.Popen([relay, "--mcp"], cwd=ROOT, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                  stderr=subprocess.DEVNULL, text=True, bufsize=1)
        self.q, self.n = queue.Queue(), 0
        threading.Thread(target=lambda: [self.q.put(ln) for ln in self.p.stdout], daemon=True).start()
        self.rpc("initialize", {"protocolVersion": "2025-06-18", "capabilities": {},
                                "clientInfo": {"name": "build_local", "version": "1"}})
        self.p.stdin.write(json.dumps({"jsonrpc": "2.0", "method": "notifications/initialized"}) + "\n")
        self.p.stdin.flush()

    def rpc(self, method, params, timeout=120):
        self.n += 1
        self.p.stdin.write(json.dumps({"jsonrpc": "2.0", "id": self.n, "method": method, "params": params}) + "\n")
        self.p.stdin.flush()
        end = time.time() + timeout
        while time.time() < end:
            try:
                m = json.loads(self.q.get(timeout=max(0.1, end - time.time())))
            except (queue.Empty, ValueError):
                continue
            if m.get("id") == self.n:
                return m
        raise TimeoutError(method)

    def tools(self):
        for _ in range(10):
            t = [x["name"] for x in self.rpc("tools/list", {}).get("result", {}).get("tools", [])]
            if t:
                return t
            time.sleep(1)
        return []

    def close(self):
        self.p.terminate()


def editor_build(builds, version, out, panel):
    """Queue every build in the open editor (one delayCall) and follow Logs/Editor.log."""
    relay = find_relay()
    if not relay:
        die("an editor has the project open and no Unity AI Assistant relay was found: close the editor to build in batch mode")
    mcp = Mcp(relay)
    if "Unity_RunCommand" not in mcp.tools():
        mcp.close()
        die("the editor's MCP relay offers no Unity_RunCommand (approve the connection in the editor, or close it)")
    res = os.path.join(out, "editor_builds.txt")
    if os.path.exists(res):
        os.remove(res)
    body = " ".join('{ bool ok = MadMax.EditorTools.CiBuild.Run(BuildTarget.%s, "%s", "%s", %s); r += "%s " + ok + "\\n"; }'
                    % (t, p, version, "true" if regen else "false", t) for t, p, regen in builds)
    code = """using UnityEngine;
using UnityEditor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        EditorApplication.delayCall += () => {
            var r = "";
            %s
            System.IO.File.WriteAllText("%s", r);
        };
        result.Log("queued");
    }
}""" % (body, res)
    log = os.path.join(ROOT, "Logs", "Editor.log")
    start = os.path.getsize(log) if os.path.exists(log) else 0
    r = mcp.rpc("tools/call", {"name": "Unity_RunCommand", "arguments": {"Code": code, "Title": "build_local: queue builds"}})
    mcp.close()
    if "queued" not in json.dumps(r):
        die("could not queue the build in the editor: %s" % json.dumps(r)[:400])
    ok("queued in the open editor (pid %s)" % editor_open())
    fol, t0 = LogFollower(log, start), time.time()
    try:
        while not os.path.exists(res):
            fol.poll()
            fol.draw(panel, t0, "editor")
            time.sleep(0.5)
    except KeyboardInterrupt:
        panel.close()
        die("stopped following (the build keeps running in the editor)")
    panel.clear()
    results = dict(ln.split() for ln in open(res).read().split("\n") if ln.strip())
    return results, time.time() - t0


def package(target, player):
    base = os.path.dirname(player)
    if target == "StandaloneOSX":
        z = player[:-4] + "-macos-universal.zip"
        subprocess.run(["ditto", "-c", "-k", "--sequesterRsrc", "--keepParent", player, z], check=True)
    elif target == "StandaloneLinux64":
        z = os.path.join(os.path.dirname(base), GAME + "-linux-x86_64.tar.gz")
        subprocess.run(["tar", "-czf", z, "-C", os.path.dirname(base), os.path.basename(base)], check=True)
    else:
        z = os.path.join(os.path.dirname(base), GAME + "-windows-x64.zip")
        subprocess.run(["zip", "-qr", "-9", z, ".", "-x", GAME + "_BackUpThisFolder_ButDontShipItWithYourGame/*"],
                       cwd=base, check=True)
    return z


def dir_size(p):
    if os.path.isfile(p):                  # Linux / Windows: the launcher sits beside its _Data folder
        p = os.path.dirname(p)
    return sum(os.path.getsize(os.path.join(d, f)) for d, _, fs in os.walk(p) for f in fs
               if not os.path.islink(os.path.join(d, f)))


# ================================================================================================= main
def main():
    host = {"darwin": "mac", "linux": "linux", "win32": "windows"}.get(sys.platform, "linux")
    ap = argparse.ArgumentParser(description="Local Mad Mike's Garage build with the HD asset pack (no commit, no release).",
                                 formatter_class=argparse.RawDescriptionHelpFormatter, epilog=__doc__.split("\n\n", 1)[1])
    ap.add_argument("--platform", action="append", choices=sorted(TARGETS), help="player(s) to build (default: %s)" % host)
    ap.add_argument("--out", default=os.path.join(ROOT, "Builds", "Local"), help="output folder (default Builds/Local)")
    ap.add_argument("--version", help="player version (default: next minor after the newest git tag)")
    ap.add_argument("--no-hd", action="store_true", help="skip rendering the HD pack and terrain textures")
    ap.add_argument("--hd-only", action="store_true", help="render the HD pack and terrain, no player build")
    ap.add_argument("--force-hd", action="store_true", help="re-render every HD job even when up to date")
    ap.add_argument("--hd-filter", action="append", default=[], metavar="PREFIX", help="only these HD jobs (e.g. cars/)")
    ap.add_argument("--jobs", type=int, help="parallel Blender processes (default 1 for a Metal bake, else 4)")
    ap.add_argument("--cpu", action="store_true", help="bake on the CPU (default: GPU, Metal on Apple Silicon)")
    ap.add_argument("--min-free-gb", type=float, default=4.0, metavar="GB",
                    help="start another Blender job only with this much memory free (default 4)")
    ap.add_argument("--no-regenerate", action="store_true", help="build without regenerating the game scene first")
    ap.add_argument("--zip", action="store_true", help="also package each player")
    ap.add_argument("--blender", help="Blender executable")
    ap.add_argument("--unity", help="Unity editor executable")
    a = ap.parse_args()

    platforms = a.platform or [host]
    apple = sys.platform == "darwin" and platform.machine() == "arm64"
    # Metal shares the machine's unified memory: parallel GPU bakes run it out (Cycles OOM, even a full system stall),
    # so a GPU bake on Apple Silicon renders one job at a time; the CPU bake can run several
    jobs = a.jobs or (1 if apple and not a.cpu else 4)
    version = a.version or next_version()
    out = os.path.abspath(a.out)
    do_hd, do_build = not a.no_hd, not a.hd_only
    steps = (2 if do_hd else 0) + (1 if do_build else 0) + (1 if do_build and a.zip else 0)
    n = 0
    panel = Panel()
    atexit.register(detach_mounts)
    T0 = time.time()
    timings = []

    sha = subprocess.run(["git", "-C", ROOT, "rev-parse", "--short", "HEAD"], capture_output=True, text=True).stdout.strip()
    dirty = subprocess.run(["git", "-C", ROOT, "status", "--porcelain", "--untracked-files=no"],
                           capture_output=True, text=True).stdout.strip()
    print(bold("Mad Mike's Garage · local build") + dim("  %s%s · v%s · %s%s" % (
        sha, " (modified)" if dirty else "", version, ", ".join(platforms) if do_build else "HD only",
        "" if do_hd else " · no HD")))

    # ---- preflight
    unity = None
    if do_build:
        unity, uver, note = find_unity(a.unity)
        if not editor_open():
            if not unity:
                die("Unity %s not found (install it with the Hub, or pass --unity)" % uver)
            pe = playback_engines(unity)
            for p in platforms:
                mod = TARGETS[p][2]
                if pe and not os.path.isdir(os.path.join(pe, mod)):
                    die("Unity at %s has no %s build support (add the module in the Hub)" % (unity, mod))
            if note:
                warn(note)
    blender = None
    if do_hd:
        blender, dmg = find_blender(a.blender)
        if not blender:
            die("Blender not found (pass --blender, set BLENDER, install it, or use --no-hd)")
        bv = subprocess.run([blender, "--version"], capture_output=True, text=True).stdout.split("\n")[0]
        ok("%s%s" % (bv or blender, dim("  (mounted from %s)" % os.path.basename(dmg)) if dmg else ""))
        exp = open(os.path.join(HD_EXPORT, "export_hd.py")).read()
        if apple and not a.cpu and '"METAL"' not in exp:
            warn("export_hd.py has no METAL bake device: baking on the CPU (much slower)")

    # ---- HD pack
    if do_hd:
        n += 1
        step(n, steps, "HD asset pack " + dim("(%d at a time, %s bake, log: Logs/hd_export.log)"
                                              % (jobs, "CPU" if a.cpu else "Metal" if apple else "GPU")))
        t = time.time()
        if not hd_export(blender, jobs, a.force_hd, a.hd_filter, panel, a.cpu, a.min_free_gb):
            die("HD export had failures: fix them or rerun (finished jobs are kept)")
        timings.append(("HD pack", time.time() - t))

        n += 1
        step(n, steps, "HD terrain textures")
        rc, txt, dt = run_with_spinner([terrain_python(panel), TERRAIN], "rendering terrain maps", panel)
        if rc != 0:
            print(txt[-2000:])
            die("terrain textures failed")
        maps = glob.glob(os.path.join(ROOT, "Assets", "MadMax", "Resources", "HDTerrain", "*.png"))
        ok("%d terrain maps → Assets/MadMax/Resources/HDTerrain  %s" % (len(maps), dim("(%s)" % fmt_dur(dt))))
        timings.append(("terrain", dt))

    # ---- player
    built = []
    if do_build:
        n += 1
        os.makedirs(out, exist_ok=True)
        builds = [(TARGETS[p][0], os.path.join(out, TARGETS[p][1]), not a.no_regenerate and i == 0)
                  for i, p in enumerate(platforms)]
        pid = editor_open()
        step(n, steps, "Player build " + dim("(%s)" % ("open editor, pid %d" % pid if pid else "batch mode: " + unity)))
        t = time.time()
        if pid:
            results, _ = editor_build(builds, version, out, panel)
            for tgt, path, _ in builds:
                if results.get(tgt) != "True":
                    die("%s build failed (see the editor console / Logs/Editor.log)" % tgt)
                ok("%s → %s  %s" % (tgt, os.path.relpath(path, ROOT), dim(fmt_bytes(dir_size(path)))))
                built.append((tgt, path))
        else:
            for tgt, path, regen in builds:
                log = os.path.join(out, "build_%s.log" % tgt)
                good, line, dt, fol = batch_build(unity, tgt, path, version, regen, log, panel)
                if not good:
                    errs = [ln for ln in fol.lines if re.search(r"error|Exception|failed", ln, re.I)][-12:]
                    for e in errs:
                        print("    " + dim(e[:200]))
                    die("%s build failed (full log: %s)" % (tgt, os.path.relpath(log, ROOT)))
                ok("%s → %s  %s" % (tgt, os.path.relpath(path, ROOT), dim("%s in %s" % (fmt_bytes(dir_size(path)), fmt_dur(dt)))))
                built.append((tgt, path))
        timings.append(("player", time.time() - t))

        if a.zip:
            n += 1
            step(n, steps, "Packaging")
            for tgt, path in built:
                z = package(tgt, path)
                ok("%s  %s" % (os.path.relpath(z, ROOT), dim(fmt_bytes(os.path.getsize(z)))))

    # ---- summary
    print("\n" + green(bold("Done")) + " in %s  " % fmt_dur(time.time() - T0) +
          dim("  ".join("%s %s" % (k, fmt_dur(v)) for k, v in timings)))
    for tgt, path in built:
        print("  " + bold(path))
    if do_build and sys.platform == "darwin" and any(t == "StandaloneOSX" for t, _ in built):
        print(dim("  run: open \"%s\"" % [p for t, p in built if t == "StandaloneOSX"][0]))
    changed = subprocess.run(["git", "-C", ROOT, "status", "--porcelain", "--untracked-files=no"],
                             capture_output=True, text=True).stdout.rstrip()
    if changed:
        print(dim("  working tree has uncommitted changes (nothing was committed):"))
        for ln in changed.splitlines()[:10]:  # noqa
            print(dim("    " + ln))


if __name__ == "__main__":
    main()
