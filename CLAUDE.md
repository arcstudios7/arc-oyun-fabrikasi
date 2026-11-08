# ARC Oyun Fabrikası — rules for Claude

This repo turns a written game idea into a playable Unity game (WebGL + Android) **without anyone opening the Unity Editor**.
You write C# only. GitHub Actions (GameCI) compiles and builds it. **You cannot run Unity or the compiler**, so the code must compile on the first try.

The owner (Emre, ARC Studios) is not a programmer. He writes ideas in Turkish. All in-game text and GAME.md must be **Turkish**. Your messages to him (summary, comments) are Turkish too.

## How the project works

- There is no hand-made scene, prefab, material, sprite, sound or model. Everything is created from code at runtime.
- `Assets/Scripts/Arc/` is the engine layer (helpers). **Do not modify it** unless a change request explicitly needs a new helper; if so, only add, never break existing signatures.
- `Assets/Editor/ArcBuild.cs` creates the empty scene + base materials and builds. **Do not modify it.**
- `Arc.Boot` runs automatically after the scene loads, creates camera + light, then adds `Game.GameMain` to a new GameObject.
- **Your game lives in `Assets/Scripts/Game/`.** Required contract:
  - `GameInfo.cs`: `namespace Game { public static class GameInfo { const string Title, Slug, Version; const bool Portrait } }` — keep all four members.
    - `Title`: Turkish game name. `Slug`: lowercase `a-z0-9-`. Bump `Version` (1.0 → 1.1) on change requests.
  - `GameMain.cs`: `namespace Game { public class GameMain : Arc.ArcGame { ... } }` — the class name and namespace are fixed.
  - Extra files are fine (e.g. `Enemy.cs`, `Level.cs`), all in `namespace Game`.
- Package list (`Packages/manifest.json`) and `ProjectSettings/` must not be changed. No asset store, no third-party packages, no TextMeshPro, no new Input System, no URP.

## Modes

- **NEW_GAME**: replace the game. Delete old game files in `Assets/Scripts/Game/` that no longer belong, write the new `GameInfo.cs`, `GameMain.cs` (+ extra files), rewrite `GAME.md`.
- **CHANGE_REQUEST**: read the current game first, apply only the requested change, keep the rest working. Bump `GameInfo.Version`. Update `GAME.md`.
- If `_ci/build-log.txt` exists: the last build failed. Read it, fix every `error CS....` first (file + line are in the log), then apply any other request.
- Ambiguous idea? Do not ask questions (nobody can answer). Pick the most fun, simplest interpretation and mention your choices in `_ci/summary.md`.

## ArcGame base class (`Arc.ArcGame`)

It already provides: title menu with the game name + how-to-play, OYNA button, pause button + Esc/Android back, game over screen, score + best score (saved), restart, menu. You implement:

```csharp
protected override string HowToPlay => "Turkish, 1-2 short lines: controls + goal";
protected override void Setup()      { /* once: camera, static world, player */ }
protected override void ResetRound() { /* destroy spawned objects, reset positions/vars — called before every round AND on menu */ }
protected override void Tick(float dt) { /* gameplay, only while playing */ }
// optional:
protected override void IdleTick(float dt) { /* animate while in menu / game over */ }
protected override void DrawHud() { base.DrawHud(); /* extra HUD via Arc.Hud */ }
protected override bool ShowScore => true;
protected override void OnGameStarted() { }
protected override void OnGameEnded(bool won) { }
```
Members you use: `Score` (int field), `PlayTime` (seconds, read-only), `Current` (State), `GameOver()` (lose), `GameOver(true)` (win), `Pause()`, `Resume()`.
Never call `StartGame()` from Tick. Never set `Time.timeScale` yourself except restoring it to 1.

## Arc helper API (only these — they are tested)

**Palette** (colors): `Palette.Bg, Ground, Primary, Accent, Danger, Good, Purple, Text`, `Palette.Hex("#RRGGBB")`, `Palette.Lighter(c, 0.25f)`, `Palette.Darker(c, 0.25f)`. Fields are assignable (e.g. `Palette.Bg = Palette.Hex("#0B132B");` in Setup) to give each game its own look.

**Shapes** (3D): `Shapes.Cube(pos, scale, color, lit=true, collider=false, parent=null)`, `Shapes.Sphere(pos, diameter, color, ...)`, `Shapes.Cylinder(pos, scale, color, ...)`, `Shapes.Capsule(pos, scale, color, ...)`, `Shapes.Make(PrimitiveType, pos, scale, color, lit, collider, parent, name)`, `Shapes.Group(name, pos, parent=null)` → Transform, `Shapes.Tint(go, color, lit=true)`. All return GameObject (Group returns Transform). `pos` is local position when a parent is given.

**Shapes** (2D sprites): `Shapes.Sprite(Shape2D.Square|Circle|Triangle|Diamond|Star|Ring, Vector2 pos, Vector2 size, color, sortingOrder=0, parent=null, name=null)` → SpriteRenderer. `Shapes.GetSprite(Shape2D)` → Sprite.

**Mats**: `Mats.Lit(color)`, `Mats.Unlit(color)`, `Mats.Sprite(color)` (cached; never `new Material(Shader.Find(...))` yourself — shaders may be stripped).

**Inputs** (keyboard + mouse + touch in one): `Inputs.Horizontal`, `Inputs.Vertical` (-1..1; screen halves on touch), `Inputs.Move` (Vector2), `Inputs.Pressed` (tap/click/Space/Enter/Up this frame), `Inputs.Held`, `Inputs.Released`, `Inputs.Tap`, `Inputs.Swipe` (Vector2Int, arrows count), `Inputs.PointerScreen`, `Inputs.PointerWorld(planeZ=0)`, `Inputs.Back`.
Every game must be fully playable with **touch only** AND with **keyboard only**.

**Sfx** (synthesized, no files): `Sfx.Coin() Jump() Hit() Explosion() Click() PowerUp() Lose() Win() Shoot()`, `Sfx.Tone(key, f0Hz, f1Hz, seconds, Wave.Sine|Square|Triangle|Saw|Noise, volume)`, `Sfx.Music("C4 E4 G4 - A4 ...", bpm, volume)` (call once in Setup; notes C..B with # sharps, octave digit, "-" rest), `Sfx.StopMusic()`. `ArcGame` already plays Lose/Win on GameOver and Click on buttons.

**Juice**: `Juice.Shake(strength=0.3f, duration=0.25f)`, `Juice.Burst(pos, color, count=24, speed=6, size=0.25f, lifetime=0.6f, gravity=1)`, `Juice.Pop(transform, amount=0.3f)`, `Juice.HitStop(0.06f)`, `Juice.FloatText("+10", worldPos, color)`, `Juice.Flash(color, 0.15f)`.

**Hud** (only inside `DrawHud()` / `OnGUI`; units are reference pixels on a 720 px screen, auto-scaled): `Hud.Text(text, x, y, size=1, color=null)`, `Hud.TextRight(text, xFromRight, y, ...)`, `Hud.Center(text, yOffsetFromCenter, size, color)`, `Hud.Button(text, yOffset, width=380, height=96)` → bool, `Hud.CornerButton(text)` → bool (top-right is used by pause!), `Hud.Overlay(color)`, `Hud.Bar(x, y, w, h, fill01, color)`. Default `DrawHud` uses the top-left area (y 18–110) for score; put extra HUD below y=120 or on the right side below the pause button (y ≥ 100).

**Save**: `Save.Best()`, `Save.SubmitBest(score)`, `Save.GetInt/SetInt/GetFloat/SetFloat/GetString/SetString(key, ...)` (for unlocked levels, coins...).

**ArcRuntime.Run(IEnumerator)**: start a coroutine from anywhere (or just use `StartCoroutine` inside GameMain).

## Game design quality floor (every game must have all of these)

1. A clear goal and a clear lose (or win) condition; one round lasts 30 s – 3 min.
2. Difficulty that ramps with `PlayTime` or level number.
3. Feedback on every important event: sound (`Sfx`) + visual (`Juice.Burst/Pop/Shake/FloatText/Flash`).
4. Its own color theme (set `Palette.*` / `cam.backgroundColor` in Setup) and a short background melody with `Sfx.Music`.
5. Readable silhouettes: player, good things and bad things in clearly different colors/shapes. Build characters from several primitives grouped with `Shapes.Group` (body + eyes etc.), not one bare cube.
6. Something moving on the menu screen (`IdleTick`).
7. Works in both orientations' aspect ratios: recompute `halfW = cam.orthographicSize * cam.aspect` every frame; never hard-code screen width.
8. 60 fps on a cheap phone: < 300 live objects, destroy what leaves the screen, no `Find`/`GetComponent` in loops, no per-frame allocations of big arrays.

Camera recipes: **2D** → `cam.orthographic = true; cam.orthographicSize = 9f; cam.transform.SetPositionAndRotation(new Vector3(0,0,-10), Quaternion.identity);` use `Shapes.Sprite` or flat 3D shapes. **3D** → perspective, e.g. `cam.orthographic = false; cam.fieldOfView = 60f; cam.transform.position = new Vector3(0, 8, -9); cam.transform.LookAt(Vector3.zero);` and lit primitives; add a `Shapes.Cube` ground.

Collision: prefer simple distance / AABB checks in Tick (`Vector2.Distance`, `Mathf.Abs(dx) < (w1+w2)/2`). Only use Rigidbody / physics when the idea truly needs it (stacking, bouncing piles); then use `Rigidbody.linearVelocity` (NOT `.velocity`) and `Rigidbody2D.linearVelocity`.

## C# rules (the build has NO second chance)

- Unity **6000.0**, C# **9**. NOT allowed: file-scoped namespaces (`namespace X;`), `global using`, `record struct`, raw string literals (`"""`), `required` members, collection expressions (`[1,2]`), primary constructors on classes. Use block namespaces `namespace Game { ... }`.
- Every file starts with the `using` lines it needs (`using UnityEngine;`, `using Arc;`, `using System.Collections;` for IEnumerator, `using System.Collections.Generic;` for List/Dictionary).
- `Random` means `UnityEngine.Random` — do NOT add `using System;` in game files (makes `Random`/`Object` ambiguous). Use `System.Math` fully qualified if ever needed.
- Obsolete/removed APIs to avoid: `FindObjectOfType` (use `FindFirstObjectByType`), `Rigidbody.velocity` (use `linearVelocity`), `GUIText`, `GUITexture`, `WWW`, `Application.LoadLevel`, `OnMouseDown` (doesn't work with touch reliably — use `Inputs`), `UnityEngine.InputSystem` (not installed), `TMPro` (not installed).
- `using UnityEditor` is forbidden in `Assets/Scripts/`.
- `const` only for primitive/string; colors must be `static readonly Color` or created in methods.
- Every `List` you iterate while removing: loop backwards. Null-check destroyed objects (`if (t == null)`).
- No `async/await`, no threads, no file IO, no network (WebGL).
- Keep each file < ~600 lines; split into extra classes when bigger.

## Self-review checklist before you finish (do it, it's mandatory)

1. Re-read every file you wrote top to bottom. Check: braces balanced, every method/property exists in the API list above or in UnityEngine, argument order/types match exactly, every variable declared, no missing `using`.
2. `GameInfo` has Title, Slug, Version, Portrait. `GameMain : ArcGame` overrides Setup, ResetRound, Tick (all `protected override`).
3. `ResetRound()` cleans up everything `Tick()` spawns (otherwise objects pile up after restart).
4. Grep `Assets/Scripts/Game` for: `velocity`, `FindObjectOfType`, `using System;`, `TMPro`, `InputSystem`, `namespace Game;` — fix any hit.
5. Touch-only playable? Keyboard-only playable?

## GAME.md (Turkish, short)

```
# <Oyun adı>  (v<Version>)
<1-2 cümle: oyunun konusu>

## Nasıl oynanır
- Telefonda: ...
- Klavyede: ...

## Amaç
...

## Bu sürümde
- ...
```

## Local use (Claude Code chat, not CI)

When the owner asks for a game directly in a Claude Code session: follow the same rules, edit files, then commit and push to `main`. The `🛠️ Unity Build` workflow builds WebGL automatically on push (run it manually with "Run workflow" for an APK). Explain what you did to him in Turkish.
