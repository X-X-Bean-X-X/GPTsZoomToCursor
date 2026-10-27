# GPT's Zoom to Cursor by X_Bean_X

Source code for version **0.3.0** of the Cities: Skylines II camera mod.

[Subscribe on Paradox Mods](https://mods.paradoxplaza.com/mods/165172/Windows)

## Settings

Open **Options > GPT’s Zoom to Cursor**.

- **Enable mod** — on by default. Wheel zoom-in moves toward terrain/water beneath the cursor.
- **Zoom out from cursor** — off by default. Enable to anchor zoom-out to the cursor too.
- **Enable automatic camera tilt** — off by default. Smoothly tilts toward straight down while zooming in, reaching 90 degrees at the closest zoom. Zoom-out retraces the transition. Manual angle changes establish a new starting angle.

While enabled, the mod removes the native zoom-dependent minimum-angle restriction. Physical camera limits and terrain collision remain. Without zoom input, the mod does not drive the angle.

## Build

Windows, a licensed Cities: Skylines II installation and its official modding toolchain are required. Game/Unity assemblies are not redistributed here. Tested against game 1.6.2f1.

The official toolchain supplies `CSII_MANAGEDPATH`, `CSII_MODPOSTPROCESSORPATH`, and `CSII_UNITYMODPROJECTPATH`. Make these available in your PowerShell environment. Use a .NET Framework-compatible Roslyn `csc.exe` supporting C# 9 or later; pass its path as `-Compiler` or set `CSC_PATH`.

```powershell
.\Build.ps1 -Compiler 'C:\path\to\roslyn\csc.exe'
.\Test.ps1 -Compiler 'C:\path\to\roslyn\csc.exe'
```

Build output is under `build/package`. Build.ps1 runs the official ModPostProcessor; `-CompileOnly` skips post-processing and is not a ready-to-install build. No script installs, publishes or modifies a city.

The seven runtime C# files and the test source are in the repository root. Build.ps1 excludes ZoomMathTests.cs from the mod DLL.

## Verification and limits

The published build passed official post-processing, 1,605 numerical checks and 11 recorded live checks. Live checks included intermediate tilt, the 90-degree endpoint, reverse tilt, retaining a low chosen angle, rebasing after an angle change, and cursor zoom. Manual-angle changes were tested using a camera-only fixture; a native mouse-drag test was not recorded. Exact same-render-frame endpoint timing was not independently captured. Compatibility with every other camera mod, collision configuration or map edge is not guaranteed.

Normal gameplay camera and mouse-wheel use are the intended scope. Cursor targeting uses terrain/water, not individual rooftops. Menus, alternate cameras, legacy camera mode and Editor behavior have separate handling. Near-horizontal views, collisions and map boundaries can prevent exact cursor anchoring.

No Harmony or companion mod is required. The mod does not make network requests or write city data. Settings are saved through the game's settings API. Reflection is used to preserve native input smoothing and temporarily adjust camera behavior; see NativeInputPeek.cs and AutomaticTilt.cs.

## Published binaries

Version 0.3.0, Paradox mod 165172:

- GPTZoomToCursor.dll: `84c21b01549244d7f2800c752f0a1d2954ff0938ea1ca8807648ebde6ff6af16`
- GPTZoomToCursor_win_x86_64.dll: `fbc9403610d9668eb3f6c20fb02fce10a82e30336f8369c9d3c1907af018447f`

Runtime source files are byte-identical to the retained source for the published build. Build/test scripts have been adapted to remove machine-specific paths. Rebuilding with different toolchains or paths is not claimed to produce byte-identical DLLs.

Created by **X_Bean_X**. Rhobium's CS1 Zoom To Cursor was a behavior reference; no CS1 source was copied.
