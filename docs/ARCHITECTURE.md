# Videoland 4000 — Architecture & Contracts

This document is the single source of truth for the interfaces shared by the
C# simulation, the JavaScript renderer, the pixel-art atlas tool and the Blazor
UI. Keep it in sync when changing any of them.

## 1. Layering (inherited from GameATron4000)

```
src/Core            pure C# game model: state machine, physics, entities, level parsing,
                    scoring, snapshot building. No Blazor/JS dependencies. Unit-tested.
src/Infrastructure  Phaser bridge (JS interop), mediator, HTTP/YAML repositories,
                    localStorage persistence.
src/BlazorUI        Blazor WebAssembly host: pages, HUD/menu components, CSS, JS, game disk content.
src/Core.Tests      xUnit tests for Core.
tools/atlas         Python (stdlib only) pixel-art -> sprite atlas generator.
tests/e2e           Playwright browser smoke test.
```

Runtime flow per frame (60 Hz, driven by Phaser's `update`):

```
Phaser update(delta) ──► JS samples held input bits
                    ──► tickRef.invokeMethod('Tick', deltaMs, inputBits)   (JS → .NET, synchronous)
                    ◄── FrameSnapshot JSON (camera, sprites, labels, sounds, optional static level)
JS reconciles Phaser game objects by sprite key, plays sound cues, applies camera/shake.
.NET publishes HudChanged / PhaseChanged / MessageShown through the mediator ONLY when values change.
```

Logical resolution is **320×180** pixels, tiles are **16×16**. Phaser scales the
canvas with `Scale.FIT`, `pixelArt: true`, `roundPixels: true`. All world
coordinates in snapshots are logical pixels. Gameplay uses a fixed timestep
(1/120 s) with an accumulator; rendering is per-frame.

## 2. FrameSnapshot wire format (C# → JS)

Property names are short on purpose.

```jsonc
{
  "cam":    { "x": 123.0, "y": 0.0, "shake": 0.0 },   // camera scroll (top-left) + shake intensity 0..1
  "sprites": [
    { "k": "player", "f": "player/run/1", "x": 100, "y": 164,
      "fx": false,          // flipX
      "a": 1, "s": 1,       // alpha, scale (integer scales for pixel art: 1,2,3,4)
      "d": 10,              // depth (see RenderDepth below)
      "t": null,            // tint 0xRRGGBB or null
      "ox": 0.5, "oy": 1,   // origin (default bottom-centre)
      "r": 0 }              // rotation radians
  ],
  "labels": [
    { "k": "lbl1", "x": 40, "y": 130, "text": "IDENTITY", "c": "#f6f2ff", "a": 1, "d": 20, "sf": 1 }
  ],
  "fx":    ["jump", "token"],   // sound cue names to play this frame (see SoundCue.cs)
  "flash": 0.0,                 // full-screen white/red flash intensity 0..1
  "level": null,                // or a LevelSnapshot when the static layer must be (re)built
  "levelId": 3,                 // id of the static layer that should currently be on screen
  "running": true,              // false while paused or in menus
  "music": "level"              // "title" | "level" | "boss" | "victory" | null (silence); renderer calls setMusic on change
}
```

`LevelSnapshot`:

```jsonc
{ "w": 1152, "h": 180, "theme": "dungeon" | "arena" | "title",
  "tiles": [ { "f": "tiles/floor_top", "x": 0, "y": 160, "d": 0 } ] }   // tile origin is top-left
```

Renderer rules:
* Keep a `Map<key, Sprite>`; create missing sprites, update existing, destroy sprites not present this frame.
* Same for labels (Phaser text, font `8px "Press Start 2P"`, `resolution` 1, `setScrollFactor(sf)`,
  origin (0.5, 0.5): `x`/`y` are the centre of the text). Label `Text` objects and the parallax `TileSprite`s
  are pooled/reused instead of destroyed: Phaser 3.55 leaks a `CONTEXT_RESTORED` listener per destroyed instance.
* When `level` is non-null: destroy the previous static layer and build the new one
  (tiles + parallax background for the theme). Camera bounds = (0,0,w,h).
* Camera: `scrollX = cam.x`, `scrollY = cam.y`; shake is a random offset of up to 3 px × `shake`
  (disabled when reduced motion is on).
* `flash` draws a full-screen rectangle with alpha = flash (fixed to camera).
* Never call back into .NET from the renderer except the per-frame `Tick`.

`RenderDepth` constants: Background −10, Decor −1, Tiles 0, Interactables 3, Collectibles 5,
Enemies 6, Boss 7, Projectiles 8, Player 10, Effects 12, Labels 20.

Background themes (parallax tileSprites, scroll factors 0.2 / 0.5):
* `dungeon`: `bg/sky` (far, sf 0.2) + `bg/racks` (near, sf 0.5), canvas colour `#0b0613`.
* `arena`: `bg/sky` + `bg/city` (sf 0.4), slight magenta ambient.
* `title`: `bg/sky` + `bg/city` slowly auto-scrolling.

## 3. Input (JS → C#)

Gameplay buttons are a bit mask sampled every frame from held keys + touch buttons:

| Bit | Name     | Keys                          |
|----:|----------|-------------------------------|
|   1 | Left     | ArrowLeft, KeyA               |
|   2 | Right    | ArrowRight, KeyD              |
|   4 | Jump     | Space, ArrowUp, KeyW          |
|   8 | Interact | KeyE                          |
|  16 | Down     | ArrowDown, KeyS (reserved)    |

One-shot UI keys are forwarded by `keyboard.js` to Blazor (`GlobalHotKeys.OnKeyDownAsync(code)`):
`Enter`/`NumpadEnter`/`Space` (start / confirm / skip), `KeyP`, `Escape` (pause), `KeyR` (retry), `KeyM` (menu),
`KeyN` (sound toggle). A focused `<button>` or link keeps its native Enter/Space activation (the key is then
neither forwarded nor cancelled). The session latches press edges until a fixed step consumes them, so presses
are never lost on displays faster than 120 Hz, and the key that confirms a menu never also acts in the game. `preventDefault()` is applied to game keys unless the event target is an
input/textarea/select/contenteditable. Key auto-repeat never triggers one-shot actions: the
simulation does edge detection on the held mask, and the Blazor hotkey handler ignores
`event.repeat`.

JS global API (implemented in `wwwroot/js/phaser.interop.js`, `keyboard.js`, `audio.js`):

```js
startPhaser(containerId, width, height, textureUrl, atlasUrl, tickRef) // creates the Phaser game
destroyPhaser()
setGamePaused(bool)           // pauses/resumes the Phaser scene loop
setSoundEnabled(bool)         // audio.js mute
setReducedMotion(bool)        // disables shake/flash
playSound(name)               // used by the UI for menu sounds
setMusic(trackOrNull)         // start/stop the generated chiptune loop ("title" | "level" | "boss" | "victory" | null)
captureKeyDown(dotnetRef) / releaseKeyDown()
getInputBits()                // held gameplay buttons (keyboard + touch)
setTouchButton(bit, down)     // called by the touch-controls overlay
prefersReducedMotion()        // feature detection used by the host at start-up
vl4000RegisterDebug(ref)      // defined in index.html; wired by the host only when the page URL contains ?dev
window.vl4000Stats()          // { ready, fps, tickMs, tickErrors, levelId, sprites, missingFrames, ... } for tests
window.vl4000Debug(cmd)       // ?dev only: forwards a text command to GameSession.ApplyDebugCommand
```

## 4. Mediator messages (UI ↔ session host)

Commands (one handler, in `PlayGame.razor`): `BootCompleted`, `StartGame`, `SkipIntro`,
`TogglePause`, `RetryGame`, `ReturnToMainMenu`, `ToggleSound`, `ToggleCrt`.

Events: `PhaseChanged(Phase, Previous)`, `HudChanged(HudState)`, `MessageShown(GameMessage)`,
`VictoryAchieved(VictoryStats)`, `GameEnded(FinalScore, HighScore, CheckpointAvailable)`,
`SettingsChanged(GameSettings)`, `ContentLoaded(GameManifest)`, `ContentFailed(error)`.

`GamePhase`: Booting, MainMenu, LevelIntro, Playing, Paused, BossEncounter, Victory, GameOver.

## 5. DOM structure & CSS

```html
<div class="page [crt] [reduced-motion]">
  <div class="stage" id="stage">          <!-- 16:9 box, container-type: size -->
    <div id="phaser"></div>               <!-- Phaser canvas (fills stage) -->
    <div class="overlay overlay-hud">…</div>
    <div class="overlay overlay-toasts">…</div>
    <div class="overlay overlay-screen">…</div>   <!-- boot / title / intro / pause / game over / victory -->
    <div class="overlay overlay-touch">…</div>
    <div class="crt-overlay" aria-hidden="true"></div>
  </div>
</div>
```

Font: "Press Start 2P" (SIL OFL, bundled in `wwwroot/assets/fonts`). Size everything in `cqw`
(container query width) so the UI scales with the stage: 1 logical pixel = `0.3125cqw`.

CSS custom properties (palette):

```css
--vl-bg:#0b0613; --vl-night:#1a0f2e; --vl-purple:#3b1f6b; --vl-violet:#6a2fd1;
--vl-magenta:#e6007e; --vl-pink:#ff4fa3; --vl-white:#f6f2ff; --vl-light:#c9c2d6;
--vl-grey:#6e6684; --vl-dark:#2e2940; --vl-cyan:#35d6ff; --vl-green:#3dff8a;
--vl-yellow:#ffd43b; --vl-orange:#ff8a3b; --vl-red:#ff3b5c;
```

## 6. Level YAML & ASCII legend

`wwwroot/games/disk001/levels/*.yaml` → `LevelSpec`. Map rows are 16 px tiles; the bottom row
of the map is aligned to the bottom of the 180 px tall world (`y = 180 − rows×16 + row×16`).

| Char | Meaning                                    | Solid |
|------|--------------------------------------------|-------|
| `.`  | empty                                      |       |
| `#`  | floor block (`tiles/floor_top` if empty above, else `tiles/floor`) | yes |
| `W`  | wall panel (`tiles/wall`)                  | yes |
| `r`  | server rack block (`tiles/rack_top` / `tiles/rack`) | yes |
| `=`  | one-way platform (`tiles/platform`), pass-through from below | top only |
| `^`  | glitch spike hazard (`tiles/spike`), damages on touch | no |
| `\|` | cable (decor) · `I` pillar · `g` glass · `m` monitor · `l` lamp · `p` poster | no |
| `s`  | server tower decor (16×32, anchored to tile bottom) · `c` coffee machine (16×32) | no |
| `P`  | player spawn                               |       |
| `T`  | token · `C` coffee · `B` cache boost · `K` circuit breaker · `R` rollback | |
| `S`  | spinner enemy (patrols ±48 px) · `X` expired token (box 3×2 tiles) · `G` bug | |
| `A`  | authentication terminal (16×32)            |       |
| `F`  | checkpoint flag (16×32; a level may have several, the latest one touched is the respawn point) · `E` exit door (32×32, anchored bottom-left) | |
| `1` `2` `3` | repair terminals: identity / content / streaming (boss arena) | |
| `O`  | boss spawn (arena)                         |       |

## 7. Sprite atlas catalogue

Single atlas, key `default`, generated by `python3 tools/atlas/build_atlas.py` into
`src/BlazorUI/wwwroot/games/disk001/assets/sprites.png|json` (Phaser JSON-hash format).
Frames are authored as text pixel-maps in `tools/atlas/frames_*.py` using the palette in
`tools/atlas/palette.py`. Every frame below MUST exist with exactly this cell size.

| Frame names | Cell | Description |
|-------------|------|-------------|
| `player/idle/0..1` | 16×20 | Friendly engineer, oversized head, headset, laptop under left arm, faces RIGHT. Idle = 1 px breathing bob. |
| `player/run/0..3` | 16×20 | Run cycle (legs alternate, laptop bobs). |
| `player/jump/0`, `player/fall/0` | 16×20 | Jump (knees up) / fall (arms up). |
| `player/hurt/0` | 16×20 | Flinch, "x" eyes, laptop tilted. |
| `player/dead/0` | 16×20 | Lying down / ghosted. |
| `player/cheer/0..1` | 16×20 | Laptop raised overhead. |
| `player/interact/0` | 16×20 | Typing pose (arms forward). |
| `spinner/spin/0..7` | 16×16 | Buffering spinner: 3/4 ring (white→magenta gradient) rotating 45° per frame, angry eyes in the centre. |
| `expired/float/0..1` | 16×16 | Cracked grey keycard, wobbling. |
| `expired/warn/0` | 16×16 | Dashed semi-transparent outline (reappearance warning). |
| `bug/walk/0..1` | 16×16 | Green beetle with antennae and mischievous eyes, legs alternate. |
| `bug/squash/0` | 16×16 | Flattened bug. |
| `token/spin/0..3` | 16×16 | Yellow/cyan keycard flipping: full, 3/4, edge-on, 3/4 (mirrored). |
| `coffee/idle/0..1` | 16×16 | White cup, brown coffee, wiggling steam. |
| `cache/idle/0..1` | 16×16 | Glowing cyan chip, glow pulses. |
| `breaker/idle/0..1` | 16×16 | Grey switch box with a red/green lever and a spark. |
| `rollback/idle/0..1` | 16×16 | Curved undo arrow (orange), pulsing. |
| `pop/0..2` | 16×16 | Enemy-defeat puff expanding. |
| `sparkle/0..2` | 16×16 | Collect burst (yellow/white). |
| `wave/0..1` | 16×16 | Ground shockwave arc (pink/white). |
| `warn/0..1` | 16×16 | Blinking exclamation / hazard stripes. |
| `bullet/0..3` | 8×8 | Mini buffering dot ring, rotating. |
| `terminal/locked/0..1` | 16×32 | Tall auth terminal, red screen with blinking "X". |
| `terminal/active/0..1` | 16×32 | Green screen with check mark, scanning line. |
| `terminal/denied/0` | 16×32 | Bright red screen. |
| `repair/idle/0..1` | 16×32 | Repair terminal, blue screen, blinking cursor. |
| `repair/ok/0`, `repair/bad/0`, `repair/cooldown/0` | 16×32 | Green / red / dim grey screen. |
| `icon/identity`, `icon/content`, `icon/streaming` | 8×8 | Key / film strip / play triangle (white on transparent). |
| `checkpoint/off/0` | 16×32 | Pole with grey flag, dark lamp. |
| `checkpoint/on/0..1` | 16×32 | Pole with magenta flag waving, lamp lit. |
| `exit/locked/0..1` | 32×32 | Metal door, bars, red lamp blinking. |
| `exit/open/0..1` | 32×32 | Door open, cyan/green glow pulsing. |
| `boss/spin/0..7` | 32×32 | Giant spinner ring with an evil face, rotating 45°/frame. |
| `boss/stun/0..1` | 32×32 | Ring stopped, dizzy spiral eyes, stars. |
| `boss/hit/0` | 32×32 | White flash with cracks. |
| `boss/charge/0..1` | 32×32 | Ring glowing bright (attack telegraph). |
| `boss/dead/0` | 32×32 | Broken ring pieces. |
| `tiles/floor_top`, `tiles/floor` | 16×16 | Server-room floor: top edge with highlight line / fill. |
| `tiles/platform` | 16×16 | One-way metal grate: top 5 px drawn, rest transparent. |
| `tiles/wall` | 16×16 | Wall panel with rivets. |
| `tiles/spike` | 16×16 | Glitch hazard: red/magenta jagged zigzag, bottom 8 px. |
| `tiles/rack_top`, `tiles/rack` | 16×16 | Server rack with LEDs. |
| `tiles/cable`, `tiles/pillar`, `tiles/glass` | 16×16 | Decor. |
| `deco/monitor`, `deco/lamp`, `deco/poster` | 16×16 | Decor (poster shows an original stylised "4000"). |
| `deco/server`, `deco/coffee_machine` | 16×32 | Decor. |
| `bg/sky` | 64×64 | Dark purple gradient with sparse star pixels, seamless on all edges. |
| `bg/racks` | 64×64 | Silhouetted server racks (night tones), seamless horizontally. |
| `bg/city` | 64×64 | Far skyline with antennas, seamless horizontally. |

Art rules: palette only; characters consistent in proportion (16 px wide heads ≈ 8–9 px);
no copyrighted characters or logos; readable silhouettes; pixel-perfect (no anti-aliasing).

## 8. Sound cues

See `src/Core/Rendering/SoundCue.cs`. All sounds are synthesised with WebAudio in
`wwwroot/js/audio.js` (no audio files). The AudioContext is created lazily on the first user
gesture; `setSoundEnabled(false)` mutes everything.

## 9. File ownership during parallel development

| Area | Files |
|------|-------|
| Simulation | `src/Core/Model/**`, `src/Core/Rendering/*.cs` (contracts fixed) |
| Renderer / input / audio | `src/BlazorUI/wwwroot/js/phaser.interop.js`, `keyboard.js`, `audio.js` |
| Pixel art | `tools/atlas/frames_*.py`, generated `wwwroot/games/disk001/assets/*` |
| UI components | `src/BlazorUI/Components/**`, `src/BlazorUI/wwwroot/css/app.css` |
| Host / wiring | `src/BlazorUI/Pages/PlayGame.razor`, `Program.cs`, `src/Infrastructure/**` |
| Tests | `src/Core.Tests/**`, `tests/e2e/**` |
