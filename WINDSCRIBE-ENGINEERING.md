# Windscribe Desktop Application — Complete Engineering Breakdown
*(Source-verified from the official public mirror: github.com/Windscribe/Desktop-App — GPL-2.0, 74-repo org)*

## TL;DR

**Language: C++17 (pure native). Framework: Qt Widgets (NOT Electron, NOT QML). Build: CMake + vcpkg + GitLab CI + Docker. Architecture: multi-process (GUI frontend ⇄ engine backend ⇄ privileged helper/service) with per-protocol connectors (WireGuard / OpenVPN / IKEv2) behind an abstract factory.**

---

## 1. The Definitive Stack

| Layer | Technology (verified) |
|---|---|
| Language | **C++** (whole desktop client — GitHub org lists repo language as C++) |
| GUI framework | **Qt Widgets** (confirmed: `commonwidgets/`, `widgetutils/`, `dpiscaleawarewidget`, `.qrc` resources, zero `.qml` files) |
| Build system | **CMake** (root + per-module `CMakeLists.txt`), **Ninja** |
| Dependency mgmt | **vcpkg** with custom registry repo `ws-vcpkg-registry` (overlay ports/triplets) |
| CI/CD | **GitLab CI** (`.gitlab-ci.yml`) + **Docker** build images |
| Build orchestration | **Python 3** scripts (`tools/build_all`, `requirements.txt`: pyyaml) with flags `--dev-mode --arm64 --sign-app --sign-installer` |
| Code signing | Windows `code_signing.pfx` (installer/windows/signing), macOS **notarization** (`tools/notarize.yml`) |
| License | **GPL-2.0** (public mirror of their proprietary client) |
| Scripting/CLI deps | Go (wstunnel), Rust available (boringtun mirror) |

## 2. Process Architecture (the key design)

Windscribe 2.0 is **not one process** — it's a 3-tier process split (classic privilege-separation):

```
┌────────────────────────────┐
│  GUI frontend (Qt Widgets) │  user process, no admin rights
│  connectwindow, locations, │
│  preferences, tray, dialogs│
└─────────────┬──────────────┘
              │ IPC
┌─────────────▼──────────────┐
│  Engine (backend process)  │  connectionmanager, firewall,
│  18 modules — see §4       │  DNS, ping, autoupdater, API
└─────────────┬──────────────┘
              │ IPC (privileged channel)
┌─────────────▼──────────────┐
│  Helper (macOS) /          │  runs as root / SYSTEM
│  Service (Windows)         │  adapters, routes, firewall rules,
│  + Installer helper        │  split tunneling drivers
└────────────────────────────┘
```

- **Why**: OS tunnel/route/firewall manipulation needs admin; the GUI must never run elevated. Each tier is separately crashable/restartable.
- Windows side logs `<install>/windscribeservice.log`; macOS helper at `/Library/Logs/com.windscribe.helper.macos/`; Linux `/var/log/windscribe/helper_log.txt`.

## 3. Repository Map (`src/`)

```
src/
├── client/               # the app users see
│   ├── client-common/    # shared client types
│   ├── engine/           # BACKEND: engine/engine (18 modules), engine/mac, engine/utils
│   └── frontend/
│       ├── gui/          # Qt Widgets UI (30+ folders, see §5)
│       ├── cli/          # CLI frontend (same engine, text UI)
│       └── frontend-common/
├── helper/               # privileged helpers (helper/macos with helper-info.plist)
├── installer/            # installers/uninstallers per OS (+ signing)
├── splittunneling/       # split-tunnel drivers/components
├── utils/                # shared utilities
└── windscribe-cli/       # pure headless CLI (Linux daemon mode)
```

## 4. Engine Internals (`src/client/engine/engine/` — 18 modules)

`apiresources` · `autoupdater` · `bridgeapi` · `connectionmanager` · `connectivitydiagnostic` · `connectstatecontroller` · `customconfigs` · `dns` · `firewall` (their kill-switch) · `helper` (IPC to privileged helper) · `locationsmodel` · `macaddresscontroller` (MAC spoofing!) · `networkdetectionmanager` · `ping` (latency probes) · `proxy` · `splittunnelextension` · `vpnshare` (hotspot/proxy share) · `wireguardconfig`

Plus top-level: `engine.cpp` (orchestrator), `crossplatformobjectfactory.cpp` (platform abstraction), `packetsizecontroller`, `measurementcpuusage`, `testvpntunnel`, `getdeviceid`, `adapterutils_win` etc.

### Protocol connectors (connectionmanager/connectors/)
```
connectors/
├── wireguard/     # WireGuard engine wiring
├── openvpn/       # OpenVPN engine wiring (incl. DCO data-offload)
├── ikev2/         # IKEv2/IPsec (strongSwan-based on Linux)
├── connectionfactory.cpp/.h      # abstract factory: IConnection
└── iconnection.h / iconnectionfactory.h / connectionplatformpolicy.*
```
**Design**: every protocol implements `IConnection`; `ConnectionFactory` picks per-OS/protocol; `attemptstrategy/` handles connect/retry/upgrade-on-fail flows; `classifyconnecterror` maps OS errors to user-facing states; `sleepevents_*` react to system sleep/wake.

## 5. GUI internals (Qt Widgets, 30+ folders)

`application` (main.cpp bootstrap) · `mainwindow(+controller,state)` · `connectwindow` · `overlaysconnectwindow` · `locations(model)+locationswindow` · `preferenceswindow` · `protocolwindow` · `loginwindow` · `twofactorauth` · `newsfeedwindow` · `emergencyconnectwindow` · `externalconfig` · `systemtray`+`trayicon` · `dialogs` · `tooltips` · `generalmessage(+controller)` · `bottominfowidget` · `commongraphics` · `commonwidgets` · `permissions` · `log` · `graphicresources` · `themecontroller` (theming!) · `dpiscalemanager` + `dpiscaleawarewidget` (HiDPI) · `windowsizemanager` · `showingdialogstate` · `widgetutils` · `utils` · `tests` · assets: `png/ svg/ gif/ sounds/ translations/ resources/` · resources: `images.qrc`, `windscribe.qrc`, `windscribe_mac.qrc` · `freetrafficnotificationcontroller`, `loginattemptscontroller`, `generalmessagecontroller`

**Custom-drawn UI**: the distinctive curved/connecting GUI is custom-painted Widgets (commongraphics), not QML/Declarative.

## 6. VPN Protocol & Dependency Stack

Built by `tools/deps` scripts: **OpenVPN (with DCO data-channel offload)** · **Wintun** (Windows TUN driver) · **WireGuard** · **AmneziaWG** (obfuscated WireGuard fork — DPI resistance) · **WSTunnel** (Go; tunnels OpenVPN over websocket/TCP/QUIC to bypass blocks)

Org-wide protocol mirrors: `wireguard-go`, `boringtun` (Rust), `wireguard-apple`, `amneziawg-go/apple`, `strongswan` (IKEv2), `OpenVPNAdapter`, `iOSOpenVPNAdapter`.

**`wsnet`** (separate repo): their cross-platform **C++ networking library** used by clients (API layer, DNS, connectivity) — the engine's networking backbone.

## 7. Platform Notes

- **Windows**: Windscribe Service (SYSTEM), Wintun driver, installer + separate installer-helper, split-tunnel driver.
- **macOS**: privileged helper (`helper-info.plist`, CFBundleVersion bump required per change), notarization pipeline, `wireguard-apple`-style network extension concepts via splittunnelextension.
- **Linux**: Qt GUI + full headless CLI (`windscribe-cli` skill folder), logs to `/var/log/windscribe/`, strongSwan for IKEv2.
- **ARM64**: explicit `--arm64` build support.

## 8. Windscribe Ecosystem (74 repos)

- **iOS-App**: Swift (+ C++ adapters) · **Android-App**: C (+Java/Kotlin layers) · **browser-extension / -mv3**: JavaScript
- **Infra tooling is Go-heavy**: ctrld (DNS forwarder), doggo (DNS CLI), quicsni (QUIC SNI reader), go-tproxy, zerolog/zap, goproxy — plus wstunnel, amneziawg-go.
- Their philosophy: **C++ for clients, Go for network tooling, Swift/Kotlin per platform, JS for extensions.**

## 9. Build & Release Engineering (replicable recipe)

1. `tools/deps/*` builds third-party engines from source (reproducible, pinned via ws-vcpkg-registry).
2. CMake per module → static/shared libs → link frontends.
3. `tools/build_all [--dev-mode] [--arm64] [--sign-*]` (Python) orchestrates everything.
4. GitLab CI + Docker images for reproducible cross-compilation.
5. Signing: Windows Authenticode (pfx) → installer; macOS codesign → notarize.yml submission.
6. Installers per-OS in `src/installer` with uninstallers and elevated installer-helper.

## 10. Lessons directly applicable to a project like S-T-E-A-L-T-H

1. **Privilege separation** (GUI ⇄ engine ⇄ helper) — exactly what S-T-E-A-L-T-H lacks; it currently elevates the whole app.
2. **IConnection + factory** for multi-protocol — same pattern fits multi-engine privacy actions.
3. **vcpkg + CMake + CI + Docker** — reproducible native builds (vs. raw csc today).
4. **Controller/state classes** (mainwindowcontroller, themecontroller) — separation S-T-E-A-L-T-H can adopt as it grows.
5. **themecontroller pattern** — Windscribe themes the same way (central controller) as S-T-E-A-L-T-H v8's ThemeManager — validation of that design.

---
*All findings read from the public GPL-2.0 mirror: github.com/Windscribe/Desktop-App (+ org page). No proprietary code was accessed; no reverse-engineering of distributed binaries was performed.*
