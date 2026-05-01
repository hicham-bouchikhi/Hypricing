#!/bin/bash
set -e

if [[ -z "$1" ]]; then
    echo "Usage: $0 <version>  (e.g. v0.7)"
    exit 1
fi

VERSION="$1"
PKGVER="${VERSION#v}"
AUR_DIR="$HOME/Publish/aur-hypricing-bin"
TARBALL="hypricing-v${PKGVER}-linux-x64.tar.gz"
URL="https://github.com/Esperadoce/Hypricing/releases/download/${VERSION}/${TARBALL}"

echo "Fetching sha256 for ${URL} ..."
SHA256=$(curl -sL "$URL" | sha256sum | cut -d' ' -f1)
echo "sha256: ${SHA256}"

sed -i "s/^pkgver=.*/pkgver=${PKGVER}/" "$AUR_DIR/PKGBUILD"
sed -i "s/^sha256sums=.*/sha256sums=('${SHA256}')/" "$AUR_DIR/PKGBUILD"

cd "$AUR_DIR"
makepkg --printsrcinfo > .SRCINFO
git add PKGBUILD .SRCINFO
git commit -m "Update to ${VERSION}"
git push
