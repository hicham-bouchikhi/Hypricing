#!/bin/bash
set -e

AUR_DIR="$HOME/Publish/aur-hypricing-git"
REPO_DIR="$(cd "$(dirname "$0")/.." && pwd)"

makepkg --printsrcinfo -p "$REPO_DIR/PKGBUILD" > "$REPO_DIR/.SRCINFO"

cp "$REPO_DIR/PKGBUILD" "$AUR_DIR/PKGBUILD"
cp "$REPO_DIR/.SRCINFO" "$AUR_DIR/.SRCINFO"

cd "$AUR_DIR"
git add PKGBUILD .SRCINFO
git commit -m "${1:-update}"
git push
