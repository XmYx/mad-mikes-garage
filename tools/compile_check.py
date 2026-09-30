#!/usr/bin/env python3
"""Compile the game's C# outside the Unity editor, exactly as Unity does (same references, defines, analyzers), for a
checkout that the editor doesn't have open (git worktrees used by parallel agents). Errors only; exit code 1 on errors.

    python3 tools/compile_check.py [checkout root]      # default: the current directory

Needs the main project's Library (the editor must have compiled it once): the response files and package DLLs come
from there."""
import os, re, subprocess, sys, tempfile

MAIN = "/home/magix/PycharmProjects/MadMaxUnity/share"
UNITY = "/home/magix/Unity/Hub/Editor/6000.6.3f1/Editor/Data"
DOTNET = UNITY + "/DotNetSdk/dotnet"
CSC = UNITY + "/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll"
DAG = MAIN + "/Library/Bee/artifacts/2400b0aE.dag"


def absolutize(line):
    m = re.match(r'^(-r:|-analyzer:|/additionalfile:)"(.*)"$', line)
    if m and not m.group(2).startswith("/"):
        return '%s"%s/%s"' % (m.group(1), MAIN, m.group(2))
    return line


def sources(root, editor):
    out = []
    for base, dirs, files in os.walk(os.path.join(root, "Assets")):
        is_editor = "/Editor" in base.replace(root, "")
        if is_editor != editor:
            continue
        out += [os.path.join(base, f) for f in files if f.endswith(".cs")]
    return out


def build(rsp_name, root, editor, extra_refs, out_dll):
    lines = open(os.path.join(DAG, rsp_name)).read().splitlines()
    keep = []
    listed = set()
    for l in lines:
        s = l.strip()
        if not s:
            continue
        if s.startswith('"Assets/') or s.startswith("Assets/"):
            listed.add(s.strip('"'))
            continue
        if s.startswith("-out:") or s.startswith("-refout:"):
            continue
        if editor and "Assembly-CSharp" in s and s.startswith("-r:") and "Editor" not in s:
            continue  # the runtime assembly: use the one we just built
        keep.append(absolutize(s))
    srcs = sources(root, editor)
    # anything the editor compiles outside Assets/MadMax (plugins, other folders) that isn't in the checkout
    for rel in listed:
        p = os.path.join(root, rel)
        if not os.path.exists(p) and os.path.exists(os.path.join(MAIN, rel)) and not rel.startswith("Assets/MadMax/"):
            srcs.append(os.path.join(MAIN, rel))
    keep += ['-r:"%s"' % r for r in extra_refs]
    keep.append('-out:"%s"' % out_dll)
    keep += ['"%s"' % s for s in srcs]
    rsp = out_dll + ".rsp"
    open(rsp, "w").write("\n".join(keep))
    res = subprocess.run([DOTNET, "exec", CSC, "@" + rsp], capture_output=True, text=True, cwd=root)
    errors = [l for l in res.stdout.splitlines() if ": error " in l]
    return errors


def main():
    root = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else os.getcwd())
    tmp = tempfile.mkdtemp(prefix="madmax_csc_")
    runtime = os.path.join(tmp, "Assembly-CSharp.dll")
    errs = build("Assembly-CSharp.rsp", root, False, [], runtime)
    if not errs:
        errs = build("Assembly-CSharp-Editor.rsp", root, True, [runtime], os.path.join(tmp, "Assembly-CSharp-Editor.dll"))
    for e in errs:
        print(e.replace(root + "/", ""))
    print("OK" if not errs else "%d error(s)" % len(errs))
    sys.exit(1 if errs else 0)


if __name__ == "__main__":
    main()
