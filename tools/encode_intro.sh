#!/usr/bin/env bash
# Encode the recorded title frames (IntroRecorder, 30 fps PNGs) and the intro soundtrack into a boot film.
#   tools/encode_intro.sh <frames dir> <out.webm>
# e.g. HD:    tools/encode_intro.sh ../audio/intro_frames_hd Assets/StreamingAssets/Intro/intro_hd.webm
#      pixel: tools/encode_intro.sh ../audio/intro_frames_px Assets/StreamingAssets/Intro/intro.webm
set -euo pipefail
frames="$1"; out="$2"
root="$(cd "$(dirname "$0")/.." && pwd)"
audio="$root/../audio/intro_audio.wav"
ffmpeg="${FFMPEG:-$(command -v ffmpeg || ls "$root"/../audio/.venv-f5/lib/python3.*/site-packages/imageio_ffmpeg/binaries/ffmpeg-linux-* | head -1)}"
n=$(ls "$frames"/frame_*.png | wc -l)
[ "$n" -ge 780 ] || { echo "only $n of 780 frames in $frames: record again"; exit 1; }
"$ffmpeg" -y -framerate 30 -i "$frames/frame_%04d.png" -i "$audio" -map 0:v -map 1:a \
  -c:v libvpx -b:v 8M -qmin 4 -qmax 40 -pix_fmt yuv420p -auto-alt-ref 0 \
  -c:a libvorbis -q:a 5 -shortest "$out"
echo "wrote $out"
