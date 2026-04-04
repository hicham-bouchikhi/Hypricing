# Maintainer: Esperadoce <https://github.com/Esperadoce>
pkgname=hypricing-git
pkgver=r0
pkgrel=1
pkgdesc="GUI settings manager for Hyprland"
arch=('x86_64')
url="https://github.com/Esperadoce/Hypricing"
license=('MIT')
makedepends=('dotnet-sdk-10.0' 'git' 'clang')
provides=('hypricing')
conflicts=('hypricing')
source=("git+${url}.git")
sha256sums=('SKIP')

pkgver() {
    cd Hypricing
    printf "r%s.%s" "$(git rev-list --count HEAD)" "$(git rev-parse --short HEAD)"
}

build() {
    cd Hypricing
    dotnet publish src/Hypricing.Desktop/Hypricing.Desktop.csproj \
        -c Release \
        -r linux-x64 \
        --self-contained true \
        -o publish
}

package() {
    cd Hypricing
    install -Dm755 publish/Hypricing.Desktop "$pkgdir/usr/bin/hypricing"
    install -Dm644 hypricing.desktop "$pkgdir/usr/share/applications/hypricing.desktop"
    install -Dm644 LICENSE "$pkgdir/usr/share/licenses/$pkgname/LICENSE"
}
