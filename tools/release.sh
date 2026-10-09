#!/usr/bin/env bash
# Local release: build Linux, Windows and macOS players, package them and publish a GitHub release.
#
#   tools/release.sh                 # next minor ("subversion"): v0.1 -> v0.2
#   tools/release.sh --major         # next major: v0.4 -> v1.0
#   tools/release.sh --dry-run       # build and package, no tag / release
#   tools/release.sh --notes FILE    # release notes from a file (default: commits since the last tag)
#   tools/release.sh --no-hd         # skip rendering the HD pack (ships whatever Models/HD holds, or the voxel look)
#
# The HD asset pack (Assets/MadMax/Models/HD, ~1.6 GB) is not in git: it is rendered here from the committed .blend
# sources in tools/blender/hd (Blender 5, GPU bake; only jobs whose .blend content or exporter changed are re-exported,
# a full render from a fresh clone takes ~40 min with 6 jobs).
# The version is the highest published vMAJOR.MINOR[.PATCH] GitHub release plus one. Builds go through CiBuild:
# in batch mode when no editor has the project open, otherwise inside the open editor via Unity MCP
# (tools/unity_mcp.py). Requires: Unity 6000.6.3f1 with Linux/Windows/Mac build support, gh (authenticated).
# GitHub Actions never runs for this: the Release workflow is manual-only.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
REPO="XmYx/mad-mikes-garage"
UNITY="${UNITY:-$HOME/Unity/Hub/Editor/6000.6.3f1/Editor/Unity}"
MCP="$(dirname "$ROOT")/tools/unity_mcp.py"
OUT="$ROOT/Builds/Release"
GAME="MadMikesGarage"

major=0; dry=0; notes=""; hd=1
while [ $# -gt 0 ]; do
  case "$1" in
    --major) major=1 ;;
    --dry-run) dry=1 ;;
    --notes) notes="$2"; shift ;;
    --no-hd) hd=0 ;;
    -h|--help) sed -n 2,18p "$0"; exit 0 ;;
    *) echo "unknown option $1" >&2; exit 2 ;;
  esac
  shift
done

cd "$ROOT"
say() { echo "[release] $*"; }
die() { echo "[release] ERROR: $*" >&2; exit 1; }

# ---- preconditions: on main, clean, pushed
[ "$(git rev-parse --abbrev-ref HEAD)" = "main" ] || die "not on main"
[ -z "$(git status --porcelain --untracked-files=no)" ] || die "uncommitted changes (commit or stash first)"
git fetch -q origin --tags
[ "$(git rev-parse HEAD)" = "$(git rev-parse origin/main)" ] || die "main differs from origin/main (push or pull first)"
command -v gh >/dev/null || die "gh not installed"
[ -x "$UNITY" ] || die "Unity not found at $UNITY (set UNITY=...)"
for m in LinuxStandaloneSupport WindowsStandaloneSupport MacStandaloneSupport; do
  [ -d "$(dirname "$UNITY")/Data/PlaybackEngines/$m" ] || die "missing build module $m (unityhub --headless install-modules ...)"
done

# ---- next version from the remote tags
# published releases count (stray tags without a release, like the old CI v0.2.0, do not)
last="$(gh release list -R "$REPO" -L 200 --json tagName --jq '.[].tagName' | sed 's#^v##' | grep -E '^[0-9]+\.[0-9]+(\.[0-9]+)?$' | sort -t. -k1,1n -k2,2n -k3,3n | tail -1 || true)"
[ -n "$last" ] || last="0.0"
M="${last%%.*}"; rest="${last#*.}"; m="${rest%%.*}"
if [ "$major" = 1 ]; then M=$((M + 1)); m=0; else m=$((m + 1)); fi
ver="$M.$m"; tag="v$ver"; full="$ver.0"
sha="$(git rev-parse HEAD)"
say "last release v$last -> $tag (player version $full) at ${sha:0:7}"
git ls-remote --tags origin "$tag" | grep -q . && die "tag $tag already exists"

# ---- HD asset pack, rendered from the committed Blender sources
if [ "$hd" = 1 ]; then
  command -v "${BLENDER:-blender}" >/dev/null || die "Blender not found (set BLENDER=..., or --no-hd)"
  say "HD pack: rendering stale assets from tools/blender/hd (log: Logs/hd_export.log)"
  python3 tools/blender/hd/export/run_export.py --jobs "${HD_JOBS:-6}" | tail -3 || die "HD export failed (see Logs/hd_export.log)"
  python3 tools/blender/hd/export/run_export.py --check || die "HD pack still stale after the export"
  python3 tools/blender/hd/terrain/make_textures.py | tail -1 || die "HD terrain textures failed"
fi

# ---- build
rm -rf "$OUT"; mkdir -p "$OUT"
targets=("StandaloneLinux64:Linux/$GAME.x86_64" "StandaloneWindows64:Windows/$GAME.exe" "StandaloneOSX:macOS/$GAME.app")
editor_open() { pgrep -f -- "-projectpath $ROOT|-projectPath $ROOT" >/dev/null 2>&1 && [ -f "$ROOT/Temp/UnityLockfile" ]; }

if editor_open; then
  say "an editor has the project open: building inside it through Unity MCP"
  [ -f "$MCP" ] || die "no $MCP to drive the open editor (close the editor to build in batch mode)"
  rm -f "$OUT/editor_builds.txt"
  code="var r = \\\"\\\"; bool regen = true;"
  for t in "${targets[@]}"; do
    T="${t%%:*}"; P="$OUT/${t#*:}"
    code+=" { bool ok = MadMax.EditorTools.CiBuild.Run(UnityEditor.BuildTarget.$T, \\\"$P\\\", \\\"$full\\\", regen); regen = false; r += \\\"$T \\\" + ok + \\\"\\\\n\\\"; }"
  done
  code+=" UnityEditor.EditorUserBuildSettings.SwitchActiveBuildTarget(UnityEditor.BuildTargetGroup.Standalone, UnityEditor.BuildTarget.StandaloneLinux64); System.IO.File.WriteAllText(\\\"$OUT/editor_builds.txt\\\", r); return \\\"built\\\";"
  # run on the editor's main thread directly (a delayCall can sit unfired in an unfocused editor); the MCP request
  # times out long before the builds end, so it runs in the background and the result file is what counts
  (timeout 7200 python3 "$MCP" execute_code "{\"action\":\"execute\",\"code\":\"$code\"}" > "$OUT/editor_mcp.txt" 2>&1 &)
  until [ -f "$OUT/editor_builds.txt" ]; do sleep 15; done
  cat "$OUT/editor_builds.txt"
  grep -q False "$OUT/editor_builds.txt" && die "a build failed (see the editor console)"
else
  regen=true
  for t in "${targets[@]}"; do
    T="${t%%:*}"; P="$OUT/${t#*:}"
    say "building $T (batch mode)"
    "$UNITY" -batchmode -nographics -quit -projectPath "$ROOT" -executeMethod MadMax.EditorTools.CiBuild.Build \
      -customBuildTarget "$T" -customBuildPath "$P" -buildVersion "$full" -regenerate "$regen" -logFile "$OUT/build_$T.log" \
      || die "$T build failed (see $OUT/build_$T.log)"
    grep -q "CI build $T .*Succeeded" "$OUT/build_$T.log" || die "$T build did not succeed (see $OUT/build_$T.log)"
    regen=false
  done
fi

# regeneration may touch tracked generated content: the release must match a commit
if [ -n "$(git status --porcelain --untracked-files=no)" ]; then
  if [ "$dry" = 1 ]; then say "note: regeneration changed tracked files (dry run: not committed)"; else
    git add -A -- . ':!.codex'
    git commit -qm "Regenerated content for $tag"
    git push -q origin main
    sha="$(git rev-parse HEAD)"
    say "committed regenerated content: ${sha:0:7}"
  fi
fi

# ---- smoke test the Linux player (data checks, ~30 s)
python3 tools/acceptance/run.py --suite catalogue --player "$OUT/Linux/$GAME.x86_64" --timeout 300 | tail -2 || die "Linux player smoke test failed"

# ---- package
say "packaging"
lin="$OUT/$GAME-$tag-linux-x86_64.tar.gz"; win="$OUT/$GAME-$tag-windows-x64.zip"; mac="$OUT/$GAME-$tag-macos-universal.zip"
tar -czf "$lin" -C "$OUT" Linux &
(cd "$OUT/Windows" && zip -qr -9 "$win" . -x "${GAME}_BackUpThisFolder_ButDontShipItWithYourGame/*") &
(cd "$OUT/macOS" && zip -qry -9 "$mac" "$GAME.app") &
wait
ls -la "$lin" "$win" "$mac"
# GitHub refuses release assets over 2 GiB: bigger packages go up in 1.9 GB parts (joined with cat)
assets=(); split_note=""
for f in "$lin" "$win" "$mac"; do
  if [ "$(stat -c %s "$f")" -gt 2000000000 ]; then
    split -b 1900000000 -d -a 2 "$f" "$f.part"
    rm -f "$f"
    for q in "$f".part*; do assets+=("$q"); done
    b="$(basename "$f")"; split_note+="- \`$b\` is split: \`cat $b.part* > $b\` (Windows: \`copy /b $b.part00+$b.part01 $b\`), then extract."$'\n'
    say "split $(basename "$f") into $(ls "$f".part* | wc -l) parts"
  else assets+=("$f"); fi
done

if [ "$dry" = 1 ]; then say "dry run: packages in $OUT, nothing published"; exit 0; fi

# ---- notes + release
nf="$OUT/notes.md"
if [ -n "$notes" ]; then
  cp "$notes" "$nf"
  [ -z "$split_note" ] || printf "\n## Large downloads\n%s" "$split_note" >> "$nf"
else
  {
    echo "Mad Mike's Garage $ver (player version $full, built locally from \`${sha:0:7}\`)."
    echo
    echo "## Downloads"
    echo "- **Linux** (x86_64): \`$(basename "$lin")\` — extract, run \`Linux/$GAME.x86_64\`."
    echo "- **Windows** (x64): \`$(basename "$win")\` — extract, run \`$GAME.exe\`."
    echo "- **macOS** (universal): \`$(basename "$mac")\` — unsigned: right-click → Open, or \`xattr -dr com.apple.quarantine $GAME.app\`."
    [ -n "$split_note" ] && printf "%s" "$split_note"
    echo
    echo "## Changes since v$last"
    git log --no-merges --format='- %s' "v$last..$sha" 2>/dev/null | grep -v "Regenerated content" | head -80 || true
  } > "$nf"
fi
gh release create "$tag" -R "$REPO" --target "$sha" --title "Mad Mike's Garage $ver" --notes-file "$nf" "${assets[@]}"
say "published $(gh release view "$tag" -R "$REPO" --json url --jq .url)"
