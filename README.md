# FPSosu
osu! lazer as an aim trainer. much like the FPoSu mod for [McOsu](https://store.steampowered.com/app/607260/McOsu/), but in real osu!.

**!!!WARNING!!! this project is pure vibeslop. sorry.**

The standard osu! playfield is embedded in 3D and projected back onto the 2D playfield.
The mouse rotates the camera while the crosshair stays pinned to the centre of the screen; hit detection, scoring, mods and difficulty are inherited untouched from standard osu!.

# In-game config

- **Projections**: *Dome* wraps the playfield onto a sphere centred on the camera (equal angular cost everywhere); *Plane* floats it flat in front of you (edges cost more, like a monitor target).
- **Controls**: mouse = look, `Z` / `X` = osu! buttons (rebindable). Spinners are spun by swinging the camera back and forth.

# Building
Requires the .NET 8 SDK.

```bash
dotnet build   # ruleset + tests
dotnet test    # headless gameplay tests
dotnet run --project osu.Game.Rulesets.FPSosu.Tests   # visual test browser
```

# Installation
Copy `osu.Game.Rulesets.FPSosu/bin/Release/net8.0/osu.Game.Rulesets.FPSosu.dll` into the `rulesets` folder next to your osu! (lazer) executable, or grab the DLL from the [releases](../../releases) page. The ruleset then shows up as "FPSosu" in the song select.

# Settings
Under *Options → Gameplay → FPSosu*:

- **Projection / field of view / playfield span** – how the beatmap sits in the world and how far you must turn to cross it.
- **Sensitivity** – camera turn rate, plus a **converter** from CS2/CS:GO, Valorant, Rainbow Six Siege, Apex, Overwatch 2 and CoD: enter your sensitivity there, apply, and the turn rate matches (DPI is only used to report the cm/360°). osu!'s own cursor sensitivity is normalised out.
- **Crosshair overshoot** – how far past the beatmap edge you can look.
- **Crosshair** – gap, line length, line thickness, opacity, centre dot, outline, colour.

# Mods
Standard mods mostly work (HD, HR, DT, FL, Relax, ...). Mods that fight the projection are hidden from the mod list: autoplay, cinema, bubbles, bloom, barrel roll, deflate, grow, spin-in, transform, wiggle, depth, repel, magnetised and no scope.

# Development

## Contributing
Since this codebase is AI slop, contribute tokens of your own to improve it.

## Releases
Pushing a `v*` tag runs the GitHub Actions workflow: it builds the Release configuration, runs the tests and attaches the ruleset DLL to a GitHub release for that tag.
