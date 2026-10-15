/* VIDEOLAND 4000 — Phaser renderer bridge (docs/ARCHITECTURE.md §2 / §3).
 *
 * The C# simulation owns all game logic and physics. Every frame the scene samples
 * the held input bits, calls tickRef.invokeMethod('Tick', deltaMs, inputBits)
 * synchronously and reconciles Phaser game objects with the returned FrameSnapshot:
 * sprites and labels by key, the static tile layer when snapshot.level is present,
 * camera scroll / shake / flash, sound cues and the music track.
 *
 * Globals:
 *   startPhaser(containerId, width, height, textureUrl, atlasUrl, tickRef) -> Promise
 *   destroyPhaser() -> Promise          fully stops the loop and removes listeners
 *   setGamePaused(bool)                 pauses / resumes the scene update loop
 *   setReducedMotion(bool)              disables camera shake and the flash overlay
 *   prefersReducedMotion() -> bool      OS media query (used by the host on boot)
 *   window.vl4000Stats() -> { fps, tickMs, ... }
 * (setSoundEnabled / playSound / setMusic live in audio.js, input in keyboard.js.)
 */
(function () {
  'use strict';

  if (window.__vl4000InteropLoaded) { return; }
  window.__vl4000InteropLoaded = true;

  var ATLAS_KEY = 'default';
  var LABEL_POOL_MAX = 64;
  var FALLBACK_KEY = 'vl-missing';
  var FALLBACK_BG_KEY = 'vl-missing-bg';
  var SCENE_KEY = 'vl4000';
  var FONT_FAMILY = '"Press Start 2P"';
  var FONT_SPEC = '8px ' + FONT_FAMILY;
  var SHAKE_PIXELS = 3;
  var FLASH_COLOR = 0xffe8f0;
  var EMPTY = [];

  var state = {
    game: null,
    scene: null,
    tickRef: null,
    ready: false,
    paused: false,
    reducedMotion: false,
    lastMusic: undefined,
    resizeObserver: null,
    destroying: null,
    missing: {},
    missingCount: 0,
    stats: { frames: 0, tickMs: 0, tickMsMax: 0, tickErrors: 0 }
  };

  function hasPhaser() {
    return typeof window.Phaser !== 'undefined';
  }

  function noteMissingFrame(name) {
    var key = String(name);
    if (state.missing[key]) { return; }
    state.missing[key] = true;
    state.missingCount++;
    console.warn('[vl4000 renderer] atlas frame "' + key + '" is missing, using the fallback texture');
  }

  // --------------------------------------------------------------------------
  // Fallback textures (generated, no files)
  // --------------------------------------------------------------------------

  function ensureFallbackTextures(scene) {
    var textures = scene.textures;
    if (!textures.exists(FALLBACK_KEY)) {
      var tex = textures.createCanvas(FALLBACK_KEY, 8, 8);
      var c = tex.getContext();
      c.fillStyle = '#ff00ff';
      c.fillRect(0, 0, 8, 8);
      c.fillStyle = '#8a0057';
      c.fillRect(0, 0, 8, 1);
      c.fillRect(0, 7, 8, 1);
      c.fillRect(0, 0, 1, 8);
      c.fillRect(7, 0, 1, 8);
      tex.refresh();
    }
    if (!textures.exists(FALLBACK_BG_KEY)) {
      var bg = textures.createCanvas(FALLBACK_BG_KEY, 64, 64);
      var b = bg.getContext();
      b.fillStyle = '#1a0f2e';
      b.fillRect(0, 0, 64, 64);
      b.fillStyle = '#3b1f6b';
      var stars = [[5, 7], [20, 3], [41, 12], [58, 30], [13, 40], [33, 52], [50, 47], [27, 25], [61, 58], [3, 60], [45, 36], [17, 19]];
      for (var i = 0; i < stars.length; i++) { b.fillRect(stars[i][0], stars[i][1], 1, 1); }
      bg.refresh();
    }
  }

  // --------------------------------------------------------------------------
  // Scene
  // --------------------------------------------------------------------------

  class VlScene extends Phaser.Scene {
    constructor(opts) {
      super({ key: SCENE_KEY });
      this.opts = opts;
      this.atlas = null;
      this.atlasFailed = false;
      this.sprites = new Map();
      this.labels = new Map();
      this.labelPool = [];
      this.parallaxSprites = [];
      this.staticGroup = null;
      this.parallax = [];
      this.flashRect = null;
      this.levelId = undefined;
      this.stamp = 0;
      this.sweepStamp = 0;
      this.textStyle = { fontFamily: FONT_FAMILY, fontSize: '8px', color: '#f6f2ff', resolution: 1 };
      this.readySent = false;
    }

    preload() {
      var self = this;
      this.load.on(Phaser.Loader.Events.FILE_LOAD_ERROR, function (file) {
        self.atlasFailed = true;
        console.warn('[vl4000 renderer] failed to load "' + (file && file.src) + '"; frames will use the fallback texture');
      });
      this.load.atlas(ATLAS_KEY, this.opts.textureUrl, this.opts.atlasUrl);
    }

    create() {
      try {
        ensureFallbackTextures(this);
        this.atlas = (!this.atlasFailed && this.textures.exists(ATLAS_KEY)) ? this.textures.get(ATLAS_KEY) : null;
        if (!this.atlas) {
          console.warn('[vl4000 renderer] atlas "' + ATLAS_KEY + '" unavailable, every frame uses the fallback texture');
        }
        var w = this.opts.width;
        var h = this.opts.height;
        var cam = this.cameras.main;
        cam.setRoundPixels(true);
        cam.setBounds(0, 0, w, h);
        this.staticGroup = this.add.group();
        this.flashRect = this.add.rectangle(0, 0, w, h, FLASH_COLOR, 1)
          .setOrigin(0, 0)
          .setScrollFactor(0)
          .setDepth(100)
          .setVisible(false);
        state.scene = this;
        state.ready = true;
        if (state.paused) { this.scene.pause(); }
      } catch (e) {
        console.error('[vl4000 renderer] scene setup failed', e);
      }
      this.sendReady();
    }

    sendReady() {
      if (this.readySent) { return; }
      this.readySent = true;
      var ref = this.opts.tickRef;
      if (!ref || typeof ref.invokeMethod !== 'function') { return; }
      try {
        ref.invokeMethod('Ready');
      } catch (e) {
        console.error('[vl4000 renderer] Ready callback failed', e);
      }
    }

    update(time, delta) {
      var ref = this.opts.tickRef;
      if (!ref) { return; }
      var bits = typeof window.getInputBits === 'function' ? window.getInputBits() : 0;
      var t0 = performance.now();
      var snap;
      try {
        snap = ref.invokeMethod('Tick', delta, bits);
      } catch (e) {
        this.onTickError(e);
        return;
      }
      var ms = performance.now() - t0;
      var st = state.stats;
      st.frames++;
      st.tickMs = st.tickMs === 0 ? ms : st.tickMs + (ms - st.tickMs) * 0.1;
      if (ms > st.tickMsMax) { st.tickMsMax = ms; }
      if (!snap) { return; }
      try {
        this.applySnapshot(snap, delta);
      } catch (e) {
        this.onTickError(e);
      }
    }

    onTickError(e) {
      var st = state.stats;
      st.tickErrors++;
      if (st.tickErrors <= 3 || st.tickErrors % 300 === 0) {
        console.error('[vl4000 renderer] frame tick failed (' + st.tickErrors + ')', e);
      }
    }

    applySnapshot(snap, delta) {
      this.stamp++;
      if (snap.level) {
        this.buildStatic(snap.level, snap.levelId);
      } else if (snap.levelId !== this.levelId) {
        // The simulation expects a layer we do not have (or none): never show a stale one.
        this.clearStatic();
        this.levelId = snap.levelId;
      }
      this.syncSprites(snap.sprites || EMPTY);
      this.syncLabels(snap.labels || EMPTY);
      this.applyCamera(snap.cam, snap.flash, delta);
      var fx = snap.fx;
      if (fx && fx.length && typeof window.playSound === 'function') {
        for (var i = 0; i < fx.length; i++) { window.playSound(fx[i]); }
      }
      var music = snap.music == null ? null : snap.music;
      if (music !== state.lastMusic) {
        state.lastMusic = music;
        if (typeof window.setMusic === 'function') { window.setMusic(music); }
      }
    }

    // ---- frames -----------------------------------------------------------

    applyFrame(obj, name) {
      if (obj.vlFrame === name) { return; }
      obj.vlFrame = name;
      var atlas = this.atlas;
      if (atlas && atlas.has(name)) {
        if (obj.texture !== atlas) {
          obj.setTexture(ATLAS_KEY, name);
        } else {
          obj.setFrame(name);
        }
      } else {
        noteMissingFrame(name);
        if (obj.texture.key !== FALLBACK_KEY) { obj.setTexture(FALLBACK_KEY); }
      }
    }

    // ---- sprites ----------------------------------------------------------

    syncSprites(list) {
      var stamp = this.stamp;
      var map = this.sprites;
      for (var i = 0; i < list.length; i++) {
        var s = list[i];
        var obj = map.get(s.k);
        if (!obj) {
          obj = this.add.sprite(s.x, s.y, FALLBACK_KEY);
          obj.vlFrame = null;
          obj.vlTint = -1;
          map.set(s.k, obj);
        }
        obj.vlStamp = stamp;
        this.applyFrame(obj, s.f);
        obj.x = s.x;
        obj.y = s.y;
        var flip = !!s.fx;
        if (obj.flipX !== flip) { obj.flipX = flip; }
        var alpha = s.a == null ? 1 : s.a;
        if (obj.alpha !== alpha) { obj.alpha = alpha; }
        var scale = s.s == null ? 1 : s.s;
        if (obj.scaleX !== scale || obj.scaleY !== scale) { obj.setScale(scale); }
        var depth = s.d | 0;
        if (obj.depth !== depth) { obj.setDepth(depth); }
        var tint = s.t == null ? -1 : s.t;
        if (obj.vlTint !== tint) {
          obj.vlTint = tint;
          if (tint < 0) { obj.clearTint(); } else { obj.setTint(tint); }
        }
        var ox = s.ox == null ? 0.5 : s.ox;
        var oy = s.oy == null ? 1 : s.oy;
        if (obj.originX !== ox || obj.originY !== oy) { obj.setOrigin(ox, oy); }
        var rot = s.r || 0;
        if (obj.rotation !== rot) { obj.rotation = rot; }
      }
      this.sweepStamp = stamp;
      map.forEach(this.sweepEntry, this);
    }

    sweepEntry(obj, key, map) {
      if (obj.vlStamp !== this.sweepStamp) {
        map.delete(key);
        obj.destroy();
      }
    }

    // Labels are pooled rather than destroyed: in Phaser 3.55 Text.destroy() leaves a
    // CONTEXT_RESTORED listener on the game event emitter, so churning floating texts
    // (+100 popups) would leak until destroyPhaser. Hidden labels wait in the pool.
    sweepLabel(obj, key, map) {
      if (obj.vlStamp !== this.sweepStamp) {
        map.delete(key);
        if (this.labelPool.length < LABEL_POOL_MAX) {
          obj.setVisible(false);
          obj.vlText = null;
          obj.vlColor = null;
          this.labelPool.push(obj);
        } else {
          obj.destroy();
        }
      }
    }

    // ---- labels -----------------------------------------------------------

    syncLabels(list) {
      var stamp = this.stamp;
      var map = this.labels;
      for (var i = 0; i < list.length; i++) {
        var l = list[i];
        var text = l.text == null ? '' : String(l.text);
        var color = l.c || '#f6f2ff';
        var obj = map.get(l.k);
        if (!obj) {
          obj = this.labelPool.pop();
          if (obj) {
            obj.setVisible(true);
            obj.setText(text);
            obj.setColor(color);
          } else {
            this.textStyle.color = color;
            obj = this.add.text(l.x, l.y, text, this.textStyle);
            obj.setOrigin(0.5, 0.5);
            if (!this.textStyle.metrics) {
              // Measure the pixel font once; every later label reuses the metrics.
              this.textStyle.metrics = obj.getTextMetrics();
            }
          }
          obj.vlText = text;
          obj.vlColor = color;
          map.set(l.k, obj);
        } else {
          if (obj.vlText !== text) { obj.vlText = text; obj.setText(text); }
          if (obj.vlColor !== color) { obj.vlColor = color; obj.setColor(color); }
        }
        obj.vlStamp = stamp;
        obj.x = l.x;
        obj.y = l.y;
        var alpha = l.a == null ? 1 : l.a;
        if (obj.alpha !== alpha) { obj.alpha = alpha; }
        var depth = l.d == null ? 20 : l.d | 0;
        if (obj.depth !== depth) { obj.setDepth(depth); }
        var sf = l.sf == null ? 1 : l.sf;
        if (obj.scrollFactorX !== sf || obj.scrollFactorY !== sf) { obj.setScrollFactor(sf); }
      }
      this.sweepStamp = stamp;
      map.forEach(this.sweepLabel, this);
    }

    // ---- static layer -----------------------------------------------------

    clearStatic() {
      if (this.staticGroup) { this.staticGroup.clear(true, true); }
      for (var i = 0; i < this.parallaxSprites.length; i++) {
        this.parallaxSprites[i].setVisible(false);
      }
      this.parallax.length = 0;
    }

    buildStatic(level, levelId) {
      this.clearStatic();
      this.levelId = levelId;
      var w = level.w > 0 ? level.w : this.opts.width;
      var h = level.h > 0 ? level.h : this.opts.height;
      this.cameras.main.setBounds(0, 0, w, h);
      this.buildBackground(level.theme || 'dungeon');
      var tiles = level.tiles || EMPTY;
      var atlas = this.atlas;
      for (var i = 0; i < tiles.length; i++) {
        var t = tiles[i];
        var img;
        if (atlas && atlas.has(t.f)) {
          img = this.add.image(t.x, t.y, ATLAS_KEY, t.f);
        } else {
          noteMissingFrame(t.f);
          img = this.add.image(t.x, t.y, FALLBACK_KEY).setScale(2);
        }
        img.setOrigin(0, 0);
        img.setDepth(t.d == null ? 0 : t.d);
        this.staticGroup.add(img);
      }
    }

    buildBackground(theme) {
      var w = this.opts.width;
      var h = this.opts.height;
      var bandH = 64;
      var bandY = h - 16 - bandH;      // sits on the usual floor row
      switch (theme) {
        case 'arena':
          this.addParallaxLayer('bg/sky', 0, 0, w, h, 0.2, -10, 0);
          this.addParallaxLayer('bg/city', 0, bandY, w, bandH, 0.4, -9, 0);
          var ambient = this.add.rectangle(0, 0, w, h, 0xe6007e, 0.07)
            .setOrigin(0, 0).setScrollFactor(0).setDepth(-8);
          this.staticGroup.add(ambient);
          break;
        case 'title':
          this.addParallaxLayer('bg/sky', 0, 0, w, h, 0.2, -10, 2);
          this.addParallaxLayer('bg/city', 0, bandY, w, bandH, 0.4, -9, 8);
          break;
        default: // dungeon
          this.addParallaxLayer('bg/sky', 0, 0, w, h, 0.2, -10, 0);
          this.addParallaxLayer('bg/racks', 0, bandY, w, bandH, 0.5, -9, 0);
          break;
      }
    }

    addParallaxLayer(frame, x, y, w, h, sf, depth, autoPxPerSecond) {
      var hasFrame = !!(this.atlas && this.atlas.has(frame));
      if (!hasFrame) { noteMissingFrame(frame); }
      // Reuse one TileSprite per layer slot for the whole scene lifetime (never destroyed per rebuild).
      var slot = this.parallax.length;
      var ts = this.parallaxSprites[slot];
      if (!ts) {
        ts = hasFrame
          ? this.add.tileSprite(x, y, w, h, ATLAS_KEY, frame)
          : this.add.tileSprite(x, y, w, h, FALLBACK_BG_KEY);
        ts.setOrigin(0, 0).setScrollFactor(0);
        this.parallaxSprites[slot] = ts;
      } else {
        if (hasFrame) { ts.setTexture(ATLAS_KEY, frame); } else { ts.setTexture(FALLBACK_BG_KEY); }
        ts.setPosition(x, y);
        ts.setSize(w, h);
        ts.setVisible(true);
      }
      ts.setDepth(depth);
      ts.tilePositionX = 0;
      ts.tilePositionY = 0;
      this.parallax.push({ ts: ts, sf: sf, auto: autoPxPerSecond, offset: 0 });
    }

    // ---- camera / flash ---------------------------------------------------

    applyCamera(cam, flash, delta) {
      var c = this.cameras.main;
      var x = cam ? cam.x : 0;
      var y = cam ? cam.y : 0;
      c.scrollX = x;
      c.scrollY = y;

      var reduced = state.reducedMotion;
      var shake = cam && cam.shake > 0 && !reduced ? Math.min(1, cam.shake) : 0;
      if (shake > 0) {
        var amp = SHAKE_PIXELS * shake;
        var ox = Math.round((Math.random() * 2 - 1) * amp);
        var oy = Math.round((Math.random() * 2 - 1) * amp);
        if (c.x !== ox || c.y !== oy) { c.setPosition(ox, oy); }
      } else if (c.x !== 0 || c.y !== 0) {
        c.setPosition(0, 0);
      }

      var layers = this.parallax;
      for (var i = 0; i < layers.length; i++) {
        var p = layers[i];
        if (p.auto && !reduced) { p.offset += p.auto * delta / 1000; }
        p.ts.tilePositionX = x * p.sf + p.offset;
        p.ts.tilePositionY = y * p.sf;
      }

      var f = flash > 0 && !reduced ? Math.min(1, flash) : 0;
      var rect = this.flashRect;
      if (f > 0) {
        if (!rect.visible) { rect.setVisible(true); }
        rect.setAlpha(f);
      } else if (rect.visible) {
        rect.setVisible(false);
      }
    }
  }

  // --------------------------------------------------------------------------
  // Lifecycle
  // --------------------------------------------------------------------------

  function loadFont() {
    try {
      if (!document.fonts || typeof document.fonts.load !== 'function') { return Promise.resolve(); }
      var timeout = new Promise(function (resolve) { setTimeout(resolve, 3000); });
      var load = document.fonts.load(FONT_SPEC).then(function (faces) {
        if (!faces || faces.length === 0) {
          console.warn('[vl4000 renderer] font ' + FONT_SPEC + ' is not available; labels will use a fallback font');
        }
      }, function () { /* ignore */ });
      return Promise.race([load, timeout]);
    } catch (e) {
      return Promise.resolve();
    }
  }

  function resetState() {
    if (state.resizeObserver) {
      try { state.resizeObserver.disconnect(); } catch (e) { /* ignore */ }
      state.resizeObserver = null;
    }
    state.game = null;
    state.scene = null;
    state.tickRef = null;
    state.ready = false;
    state.paused = false;
    state.destroying = null;
    state.lastMusic = undefined;
    state.stats.frames = 0;
    state.stats.tickMs = 0;
    state.stats.tickMsMax = 0;
    state.stats.tickErrors = 0;
    if (typeof window.setMusic === 'function') { window.setMusic(null); }
  }

  function createGame(containerId, width, height, textureUrl, atlasUrl, tickRef) {
    if (!hasPhaser()) { throw new Error('Phaser is not loaded'); }
    var parent = document.getElementById(containerId);
    if (!parent) { throw new Error('startPhaser: container "' + containerId + '" not found'); }
    var w = width > 0 ? width | 0 : 320;
    var h = height > 0 ? height | 0 : 180;
    state.tickRef = tickRef;
    state.lastMusic = undefined;
    state.stats.frames = 0;
    state.stats.tickMs = 0;
    state.stats.tickMsMax = 0;
    state.stats.tickErrors = 0;

    var scene = new VlScene({ tickRef: tickRef, textureUrl: textureUrl, atlasUrl: atlasUrl, width: w, height: h });
    var config = {
      type: Phaser.AUTO,
      parent: containerId,
      width: w,
      height: h,
      backgroundColor: '#0b0613',
      pixelArt: true,
      roundPixels: true,
      antialias: false,
      banner: false,
      autoFocus: false,
      disableContextMenu: true,
      audio: { noAudio: true },
      input: { keyboard: false, mouse: false, touch: false, gamepad: false },
      scale: {
        mode: Phaser.Scale.FIT,
        autoCenter: Phaser.Scale.CENTER_BOTH,
        width: w,
        height: h,
        expandParent: false
      },
      render: { pixelArt: true, roundPixels: true, antialias: false },
      scene: [scene]
    };
    state.game = new Phaser.Game(config);

    if (typeof ResizeObserver === 'function') {
      state.resizeObserver = new ResizeObserver(function () {
        var game = state.game;
        if (game && game.isBooted && game.scale) {
          try { game.scale.refresh(); } catch (e) { /* ignore */ }
        }
      });
      state.resizeObserver.observe(parent);
    }
    return state.game;
  }

  window.startPhaser = function (containerId, width, height, textureUrl, atlasUrl, tickRef) {
    var pending = state.destroying || Promise.resolve();
    return pending
      .then(function () {
        if (state.game) { return window.destroyPhaser(); }
        return undefined;
      })
      .then(loadFont)
      .then(function () {
        createGame(containerId, width, height, textureUrl, atlasUrl, tickRef);
      });
  };

  window.destroyPhaser = function () {
    if (state.destroying) { return state.destroying; }
    var game = state.game;
    if (!game) {
      resetState();
      return Promise.resolve();
    }
    state.destroying = new Promise(function (resolve) {
      var done = false;
      function finish() {
        if (done) { return; }
        done = true;
        resetState();
        resolve();
      }
      try { game.events.once(Phaser.Core.Events.DESTROY, finish); } catch (e) { /* ignore */ }
      try {
        game.destroy(true);
      } catch (e) {
        finish();
        return;
      }
      // Phaser destroys at the start of the next step; if the loop is not running
      // (hidden tab, not booted) force it so no second loop can ever overlap.
      setTimeout(function () {
        if (done) { return; }
        try {
          if (game.pendingDestroy && game.isBooted) { game.runDestroy(); }
        } catch (e) { /* ignore */ }
        finish();
      }, 120);
    });
    return state.destroying;
  };

  window.setGamePaused = function (paused) {
    state.paused = !!paused;
    var scene = state.scene;
    if (!scene || !state.game || !scene.scene) { return; }
    try {
      var plugin = scene.scene;
      if (state.paused) {
        if (!plugin.isPaused(SCENE_KEY)) { plugin.pause(); }
      } else if (plugin.isPaused(SCENE_KEY)) {
        plugin.resume();
      }
    } catch (e) {
      console.warn('[vl4000 renderer] setGamePaused failed', e);
    }
  };

  window.setReducedMotion = function (reduced) {
    state.reducedMotion = !!reduced;
  };

  window.prefersReducedMotion = function () {
    try {
      return !!(window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);
    } catch (e) {
      return false;
    }
  };

  window.vl4000Stats = function () {
    var game = state.game;
    var scene = state.scene;
    var st = state.stats;
    var fps = game && game.loop ? game.loop.actualFps : 0;
    var result = {
      ready: state.ready,
      fps: Math.round(fps * 10) / 10,
      tickMs: Math.round(st.tickMs * 1000) / 1000,
      tickMsMax: Math.round(st.tickMsMax * 1000) / 1000,
      frames: st.frames,
      tickErrors: st.tickErrors,
      paused: state.paused,
      reducedMotion: state.reducedMotion,
      music: state.lastMusic == null ? null : state.lastMusic,
      levelId: scene ? scene.levelId : undefined,
      sprites: scene ? scene.sprites.size : 0,
      labels: scene ? scene.labels.size : 0,
      statics: scene && scene.staticGroup ? scene.staticGroup.getLength() : 0,
      atlas: scene ? !!scene.atlas : false,
      missingFrames: Object.keys(state.missing),
      renderer: game && game.renderer ? (game.renderer.type === Phaser.WEBGL ? 'webgl' : 'canvas') : null,
      inputBits: typeof window.getInputBits === 'function' ? window.getInputBits() : 0
    };
    st.tickMsMax = 0;
    return result;
  };
})();
