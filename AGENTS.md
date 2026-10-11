# AGENTS.md — FPSosu

FPSosu is a ruleset for **osu! lazer** that turns standard osu! into a **3D first-person aim trainer**. The flat 2D playfield is embedded into a 3D world, then projected back onto the screen through a camera that the mouse rotates.

## Core Concept

- Hit objects keep their **real osu! playfield positions**. Only their drawable position and scale are rewritten each frame, by projecting them through the camera.
- **Mouse movement rotates the camera** (yaw and pitch) instead of moving the cursor.
- The **crosshair stays fixed at the centre of the playfield**. Objects sweep across the screen as the camera turns.
- Aiming at an object means rotating until it sits under the crosshair. That is where hits are registered, so hit detection, slider tracking, spinners, judgements, scoring and star rating are all inherited from osu! unchanged.

## Architecture

`FPSosuRuleset` is what the game registers, and `DrawableFPSosuRuleset` extends `DrawableOsuRuleset`. Extending the *drawable* osu! ruleset is required, not cosmetic: several stock osu! mods (Classic, Relax, Autopilot) cast `drawableRuleset` to `DrawableOsuRuleset`, so the entire standard mod set keeps working.

`FPSosuRuleset` itself does **not** derive from `OsuRuleset` - see [Ruleset registration](#ruleset-registration) for the reason. All of osu!'s behaviour is inherited by `FPSosuOsuRuleset`, an internal ruleset which is never registered with the game, and `FPSosuRuleset` forwards to it.

| File | Purpose |
| ---- | ------- |
| `FPSosuRuleset.cs` | The registered ruleset. Reports FPSosu's identity and forwards everything else to `FPSosuOsuRuleset`. Deliberately not an `ILegacyRuleset` |
| `FPSosuOsuRuleset.cs` | Internal `OsuRuleset` which gameplay actually runs on. Inherits hit objects, scoring, mods, difficulty, skinning and replays from osu!, and hosts what FPSosu overrides (mod list, icon, config, settings) |
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
dotnet test    # 54 tests
```

Tests cover the projection invariants (round-trips, span/FOV independence, clamping), the camera, and real gameplay through `PlayerTestScene`. The key end-to-end check is `TestSceneFPSosuAutoplay`: autoplay must build combo with **zero misses** while the camera rotates, which proves the projection preserved playability.

`TestFPSosuRulesetRegistration` reproduces the registration bug described below against a real ruleset store, and `TestFPSosuRulesetSurface` fails if the registered ruleset stops forwarding an osu! override (which is what makes osu! updates safe).

## Key Bindings

Inherited from osu!: Z / left mouse = left button, X / right mouse = right button, C = smoke.

## Design Principles

1. **Inherit, don't reimplement** — hit objects, mods, scoring and difficulty all come from `OsuRuleset`. Only projection, camera and cursor behaviour are customised.
2. **Keep the math pure** — `FPSosuProjector` has no framework dependencies so the geometry can be tested directly.
3. **Never strand the player** — camera rotation is clamped to the beatmap's angular extent, so every object stays reachable.
4. **Never claim another ruleset's online ID** — the registered ruleset must not implement `ILegacyRuleset` (see below).

## Ruleset registration

How a ruleset is registered decides whether the game will show it at all, so this is the sharpest edge in the project.

`RealmRulesetStore.prepareDetachedRulesets` registers `ILegacyRuleset` implementations **first**, and keys their database rows on `RulesetInfo.OnlineID` rather than on their instantiation info - a row is only written when no row with that online ID exists yet. `RulesetInfo.ShortName` is the primary key, so re-registering under a new online ID does not update the existing row, it throws `RealmDuplicatePrimaryKeyValueException` out of the store constructor.

Every ruleset which is *not* itself a legacy ruleset reports an online ID of `-1`, because that is the fallback in `Ruleset.RulesetInfo`:

```csharp
OnlineID = (this as ILegacyRuleset)?.LegacyID ?? -1,
```

So a ruleset which is an `ILegacyRuleset` and also reports `-1` (the obvious choice for a ruleset derived from osu!, which must keep away from osu!'s ID of 0) shares that ID with every ordinary custom ruleset. Whichever custom ruleset registered first takes the `-1` slot, and the derived one never gets a row at all: it is then silently absent from the game, with **no error logged anywhere**, because the code which reports failures is only reached for rulesets which do have a row.

This is why `FPSosuRuleset` derives from `Ruleset` rather than `OsuRuleset`, and forwards to `FPSosuOsuRuleset` (an internal `OsuRuleset`) instead. Not being an `ILegacyRuleset` is what makes the store key its row on the registered type, which works alongside any number of other custom rulesets, and makes a re-registration update the existing row rather than colliding with it.

Consequences to keep in mind when editing:

- `FPSosuOsuRuleset` must stay non-public: the store registers the first **public** `Ruleset` subclass in the assembly.
- Every member `OsuRuleset` overrides must be declared on `FPSosuRuleset` too, otherwise the game's UI keeps asking the registered ruleset and gets `Ruleset`'s much emptier default (no star ratings, wrong score breakdown, no editor). `TestFPSosuRulesetSurface` enforces this, so a new osu! override shows up as a failing test rather than as a missing feature.
- `FPSosuOsuRuleset` reports the same `ShortName`, `Description` and online ID as `FPSosuRuleset`, so gameplay is still attributed to FPSosu rather than to osu!.

This affects any custom ruleset derived from osu!, osu!taiko, osu!catch or osu!mania, not just this one - the underlying store behaviour is an upstream bug, documented in [`docs/upstream-legacy-ruleset-registration.md`](docs/upstream-legacy-ruleset-registration.md) together with the reproduction and a proposed fix.
