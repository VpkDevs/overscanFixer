# overscanFixer (PRO Edition)

[![Release](https://img.shields.io/github/v/release/VpkDevs/overscanFixer?style=flat-square)](https://github.com/VpkDevs/overscanFixer/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6?style=flat-square&logo=windows)](https://microsoft.com)

**overscanFixer** is a maximalist, high-performance Windows display boundary management suite. It completely eliminates clipped taskbars, cropped window chrome, and cut-off game HUDs on TVs, docked handhelds (Steam Deck, ROG Ally, Legion Go), projectors, and multi-monitor sim setups — **without touching or breaking your GPU driver control panels**.

---

## ⚡ Why overscanFixer Dwarfs the Alternatives

| Feature / Capability | AMD Adrenalin / NVIDIA CP | PowerToys / FancyZones | **overscanFixer PRO** |
|---|---|---|---|
| **Survives GPU driver crashes & updates** | ❌ Wiped on every driver update | ❌ N/A | **✅ 100% immune (Win32 work-area layer)** |
| **Hardware EDID Display Recognition** | ⚠️ Generic monitor indexes | ⚠️ Generic handles | **✅ Direct WMI EDID decoding (Exact TV/Monitor Model)** |
| **Auto-trigger per Game / App** | ⚠️ Game profiles often fail | ❌ No | **✅ Foreground process watchdog with auto-revert** |
| **OLED Anti-Burn-In Protection** | ❌ None | ❌ None | **✅ 8-point orbital margin drift (Sub-pixel safe)** |
| **Windows Logon Sequence Resilience** | ❌ Resets frequently | ❌ N/A | **✅ Multi-stage retry enforcement loop (+1.5s, +2.5s, +4s)** |
| **Click-Through Mask Customization** | ❌ Solid black or nothing | ❌ No masking | **✅ Custom Hex color + adjustable alpha opacity** |
| **Instant Hotkey Toggle** | ❌ None | ❌ Complex combos | **✅ Instant `Ctrl + Alt + O` toggle** |

---

## 🌟 Maximalist Featureset

### 1. 🔍 True Hardware EDID Monitor Intelligence
- Queries ACPI/WMI `WmiMonitorID` to decode the manufacturer ASCII model string (e.g., `LG OLED48C2`, `Samsung QN90B`, `Insignia NS-50D510NA19`).
- Automatically associates individual margin profiles to physical display hardware.
- Automatically handles hotplug events (`WM_DISPLAYCHANGE`) when docking or turning AV receivers on/off.

### 2. 🎯 Foreground Process Auto-Triggers
- Dedicated rule engine that watches the active foreground window.
- When you launch a simulator, retro emulator (`RetroArch`), media player (`vlc`), or Steam game, your specialized overscan or letterboxing profile engages automatically.
- Reverts cleanly back to your default desktop profile when the application is minimized or closed.

### 3. 🛡️ OLED Anti-Burn-In Pixel Orbit
- Designed for users running OLED TVs as desktop monitors (e.g. LG C-series, Samsung QD-OLEDs).
- Subtly shifts safe-margin boundaries over time in a smooth 8-point orbital loop (e.g., 2–4 pixels every 5–10 minutes).
- Keeps static HUDs, taskbars, and browser borders continuously moving across adjacent sub-pixels without disrupting the user.

### 4. 🎨 Matte Border Masking with Hex & Alpha Customization
- Layered transparent click-through windows (`WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`).
- Supports custom matte colors (Pure Black, Deep Slate, Midnight Blue, or custom Hex) and variable opacity (10% to 100%).
- Built-in presets for **Retro 4:3 Pillarboxing** and **Cinema 2.39:1 Letterboxing**.

### 5. 🛡️ Unbreakable Windows Boot Resilience
- Employs logon task scheduling with stabilized startup delays (`schtasks /delay 0000:10`) plus registry `Run` fallbacks.
- Integrated background watchdog enforces the target work area across 3 retry intervals on boot to fight Windows DWM and Explorer shell drift.

### 6. 🚀 Frictionless Tray & Hotkey Workflow
- Native Windows system tray context menu with one-click **Switch Profile** submenu.
- Interactive tooltip displaying active profile and exact 4-side pixel offsets in real time (`OverscanFix: [Standard TV] L48 T27 R48 B27`).
- Global hotkey toggle `Ctrl + Alt + O`.

---

## 🛠️ Building & Running

### Requirements
- Windows 10 / 11 (x64)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```powershell
# Clone the repository
git clone https://github.com/VpkDevs/overscanFixer.git
cd overscanFixer

# Build debug
dotnet build

# Run application
dotnet run

# Compile Release distribution
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

---

## 📄 License
MIT License. Free for personal and commercial usage.
