#!/usr/bin/env bash
# Out-of-editor compile check for Assembly-CSharp, using the Roslyn compiler bundled with
# the project's Unity editor version and the references from the Unity-generated
# Assembly-CSharp.csproj. Read-only: writes only a throwaway DLL under $TMPDIR.
#
# Sources: every Assets/**/*.cs except the asmdef-owned Assets/Ink/** and */Editor/* folders
# (globbed, so new/deleted scripts are picked up without regenerating the csproj).
# Output: one line per distinct compiler error, paths relative to the repo root with
# forward slashes, e.g.  Assets/UI/Hotwheel.cs(156,20): error CS1061: ...
# Exit status: the compiler's (0 = clean). No output + exit 0 = compiles.
set -u
ROOT="${PERIHELION_ROOT:-$(git rev-parse --show-toplevel 2>/dev/null)}"
cd "$ROOT" || { echo "compile-check: cannot cd to repo root" >&2; exit 2; }
VER=$(grep -m1 'm_EditorVersion:' ProjectSettings/ProjectVersion.txt | awk '{print $2}')
EDITOR_DIR="${UNITY_EDITOR_DIR:-C:/Program Files/Unity/Hub/Editor/$VER/Editor}"
CSC="$EDITOR_DIR/Data/DotNetSdkRoslyn/csc.dll"
[ -f "$CSC" ] || { echo "compile-check: Roslyn not found at $CSC" >&2; exit 2; }
[ -f Assembly-CSharp.csproj ] || { echo "compile-check: Assembly-CSharp.csproj missing (open the project in Unity once)" >&2; exit 2; }

OUT_DIR="${TMPDIR:-/tmp}/perihelion-compile-check"
mkdir -p "$OUT_DIR"
# dotnet is a native Windows process: hand it a Windows-style path, not a Git Bash one.
command -v cygpath >/dev/null 2>&1 && OUT_DIR="$(cygpath -m "$OUT_DIR")"
RSP="$OUT_DIR/args.rsp"

DEFINES=$(grep -o '<DefineConstants>[^<]*' Assembly-CSharp.csproj | head -1 | sed 's/<DefineConstants>//')
{
  echo "-nologo"
  echo "-noconfig"
  echo "-nostdlib+"
  echo "-target:library"
  echo "-langversion:9.0"
  echo "-define:$DEFINES"
  echo "-out:\"$OUT_DIR/Assembly-CSharp.check.dll\""
  grep -o '<HintPath>[^<]*' Assembly-CSharp.csproj | sed 's/<HintPath>//' | while IFS= read -r r; do
    p="${r//\\//}"
    case "$p" in /*|?:/*) ;; *) p="$ROOT/$p" ;; esac
    [ -f "$p" ] && echo "-r:\"$p\""
  done
  [ -f "Library/ScriptAssemblies/Ink-Libraries.dll" ] && echo "-r:\"$ROOT/Library/ScriptAssemblies/Ink-Libraries.dll\""
  find Assets -name '*.cs' -not -path 'Assets/Ink/*' -not -path '*/Editor/*' | while IFS= read -r f; do
    echo "\"$ROOT/$f\""
  done
} > "$RSP"

dotnet "$CSC" "@$RSP" 2>&1 | grep -E 'error CS' | sed 's|[\\]|/|g' | sed "s|^$ROOT/||" | sort -u
exit "${PIPESTATUS[0]}"
