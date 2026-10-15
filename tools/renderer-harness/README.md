# Renderer harness

Runs the real `wwwroot/js/phaser.interop.js`, `keyboard.js` and `audio.js` without Blazor.
A fake `tickRef` (see `harness.js`) answers `Tick(deltaMs, inputBits)` with FrameSnapshot
objects in the wire format of `docs/ARCHITECTURE.md` §2, driven by a tiny JS platformer
simulation (the real game simulates in C#).

```sh
node tests/e2e/serve.mjs            # serves the repo root on http://127.0.0.1:8321
open http://127.0.0.1:8321/tools/renderer-harness/
```

* Arrow keys / A D move, Space / W / Up jump. Stomp spinners, collect the keycard, avoid spikes.
* Buttons exercise `setGamePaused`, flash, shake, static-layer rebuilds for the three themes,
  `setMusic` (through the snapshot's `music` field), `setSoundEnabled`, `setReducedMotion`,
  every `SoundCue`, `destroyPhaser` + `startPhaser`, and `setTouchButton`.
* The stats panel prints `window.vl4000Stats()` and `window.vl4000Audio.state()`.
* Atlas: the real `games/disk001/assets/sprites.{png,json}` when present, otherwise a generated
  placeholder atlas. `?atlas=placeholder` forces the placeholder, `?atlas=missing` simulates a 404.
  The sprite `debug/missing/0` is always absent so the renderer's fallback texture is visible.

Automated check (headless Chromium, screenshots in `tests/e2e/output/`):

```sh
cd tests/e2e && npm install && node harness-check.mjs [--atlas placeholder|missing] [--headed]
```
