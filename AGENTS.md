# AGENTS.md — FPSosu

FPSosu is a ruleset for **osu! lazer** that turns standard osu! into a **3D first-person aim trainer**. The flat 2D playfield is embedded into a 3D world, then projected back onto the screen through a camera that the mouse rotates.

## Core Concept

- Hit objects keep their **real osu! playfield positions**. Only their drawable position and scale are rewritten each frame, by projecting them through the camera.
- **Mouse movement rotates the camera** (yaw and pitch) instead of moving the cursor.
- The **crosshair stays fixed at the centre of the playfield**. Objects sweep across the screen as the camera turns.
- Aiming at an object means rotating until it sits under the crosshair. That is where hits are registered, so hit detection, slider tracking, spinners, judgements, scoring and star rating are all inherited from osu! unchanged.

## Architecture

`FPSosuRuleset` extends `OsuRuleset` and `DrawableFPSosuRuleset` extends `DrawableOsuRuleset`. Extending the *drawable* osu! ruleset is required, not cosmetic: several stock osu! mods (Classic, Relax, Autopilot) cast `drawableRuleset` to `DrawableOsuRuleset`, so the entire standard mod set keeps working.

| File | Purpose |
|------|---------|
| `Projection/FPSosuProjector.cs` | Pure projection math: playfield ↔ world ↔ screen, plus the camera-angle inverse. No framework dependencies, fully unit tested |
| `Projection/FPSosuProjectionMode.cs` | `Dome` (playfield wrapped on a sphere) or `Plane` (flat rectangle in space) |
| `Projection/FPSosuCamera.cs` | Camera yaw/pitch, clamped so the beatmap can never end up behind the player |
| `UI/FPSosuPlayfield.cs` | Projects every alive hit object each frame and drives the playfield boundary |
| `UI/FPSosuCrosshairContainer.cs` | The centred crosshair which replaces the osu! cursor |
| `UI/FPSosuPlayfieldBoundary.cs` | Draws the edge of the playfield as it sits in the world, so the beatmap has a visible frame of reference |
| `UI/DrawableFPSosuRuleset.cs` | Wires playfield, input manager, camera, replays and settings together |
| `UI/FPSosuSettingsSubsection.cs` | Exposes the projection and camera settings |
| `Configuration/` | `FPSosuRulesetConfigManager` (derives from `OsuRulesetConfigManager`) owning a nested `FPSosuConfigManager` for FPS-specific settings |
| `Replays/` | Replay playback (aims the camera) and recording (stores the aimed playfield point) |

### Projection Math

Two settings shape the projection, and they are deliberately **independent**:

- **Field of view** is zoom. It sets the focal length, controlling how large everything appears. Object scale is
  proportional to the focal length, so zooming in enlarges notes and zooming out shrinks them; at the default field
  of view a centred resting note keeps its natural osu! size.
- **Playfield span** is the angular width of the beatmap in the world, controlling how far the player must physically turn to cross it. This is the aim-training workload.

`PlayfieldToWorld` places a playfield position in the world; `WorldToPlayfield` projects it back through the camera; `PlayfieldToCameraAngles` and `CameraAnglesToPlayfield` are exact inverses of each other (verified to ~1e-14) and are what let autoplay and replays aim by rotation.

In `Dome` mode the playfield offset is treated as arc length on a sphere, so equal playfield distances always cost equal angular movement. The dome radius cancels out of both position and scale. In `Plane` mode the beatmap stays a flat rectangle, so its edges require progressively more turning, like a flat monitor target.

Because a smaller playfield span packs objects closer together, object size also scales with the span, keeping circles proportional to the gaps between them. Sliders are an extended shape, so they are projected by their head and tail rather than a single point; this keeps the whole body following the perspective instead of the far end swimming as the camera turns.

### Input Handling

`FPSosuInputManager` turns the frame-to-frame movement of the parent input manager's cursor into camera rotation. Measuring consecutive-position deltas (rather than recentering the cursor and reading its offset) applies every movement exactly once regardless of frame timing. The manager's own cursor is pinned to the crosshair, which is where hits are registered. The parent cursor is pulled back to the crosshair only once it drifts beyond a margin, so it never runs into the window edge and stops reporting movement.

Camera control is gated on `UseParentInput`, which the drawable ruleset clears while paused and a replay handler clears during replays. While paused this leaves the cursor free to navigate the pause menu, and forgetting the last cursor position means the movement made over the menu is not applied as a jump on resume. Only the input manager hosting the playfield drives the camera; the ruleset creates a second input manager for the resume overlay, and that one has camera control disabled so overlay cursors stay free.

Smooth camera control relies on **relative (raw) mouse input** being enabled in osu!'s input settings.

Two deliberate deviations from osu!: the playfield's storyboard alignment shift is disabled (the crosshair must be at the true view centre), and the resume overlay is `DelayedResumeOverlay`, because the standard one requires clicking a specific position which is impossible with a locked cursor.

## Building & Testing

```bash
dotnet build   # must be 0 errors, 0 warnings
dotnet test    # 39 tests
```

Tests cover the projection invariants (round-trips, span/FOV independence, clamping), the camera, and real gameplay through `PlayerTestScene`. The key end-to-end check is `TestSceneFPSosuAutoplay`: autoplay must build combo with **zero misses** while the camera rotates, which proves the projection preserved playability.

## Key Bindings

Inherited from osu!: Z / left mouse = left button, X / right mouse = right button, C = smoke.

## Design Principles

1. **Inherit, don't reimplement** — hit objects, mods, scoring and difficulty all come from `OsuRuleset`. Only projection, camera and cursor behaviour are customised.
2. **Keep the math pure** — `FPSosuProjector` has no framework dependencies so the geometry can be tested directly.
3. **Never strand the player** — camera rotation is clamped to the beatmap's angular extent, so every object stays reachable.
4. **No online ID collision** — `LegacyID` is `-1` to avoid conflicting with standard osu! (ID 0).
