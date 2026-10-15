/* VIDEOLAND 4000 — chiptune audio.
 *
 * Everything is synthesised with WebAudio (square / pulse / triangle / noise with
 * simple envelopes); there are no audio files. The AudioContext is created lazily on
 * the first user gesture (pointerdown / keydown / touchstart on window) and resumed
 * whenever the browser suspends it. Every entry point silently no-ops when WebAudio
 * is unavailable, the context is not running yet, or sound is disabled.
 *
 * Globals (docs/ARCHITECTURE.md §3 / §8):
 *   setSoundEnabled(bool)       mute / unmute everything (music restarts on unmute)
 *   playSound(name)             one of the SoundCue names (src/Core/Rendering/SoundCue.cs)
 *   setMusic(trackOrNull)       "title" | "level" | "boss" | "victory" | null
 * Extras for tooling: window.vl4000Audio = { cues(), tracks(), state(), unlock() }
 */
(function () {
  'use strict';

  if (window.__vl4000AudioLoaded) { return; }
  window.__vl4000AudioLoaded = true;

  var MASTER_GAIN = 0.15;   // modest overall level
  var MUSIC_GAIN = 0.5;     // relative to master
  var SFX_GAIN = 1.0;
  var LOOKAHEAD_S = 0.14;   // music scheduler lookahead
  var TICK_MS = 30;         // music scheduler interval
  var MIN_REPEAT_S = 0.03;  // identical cue re-trigger guard

  var ctx = null;
  var master = null;
  var sfxBus = null;
  var musicBus = null;
  var noiseBuffer = null;
  var waves = {};           // custom PeriodicWaves (pulse widths)
  var enabled = true;
  var currentTrack = null;  // requested track, survives mute / suspend
  var music = null;         // running scheduler state
  var lastPlayed = {};      // cue -> last trigger time
  var warned = {};
  var hiddenMuted = false;

  // --------------------------------------------------------------------------
  // Context lifecycle
  // --------------------------------------------------------------------------

  function createContext() {
    if (ctx) { return ctx; }
    var AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) { return null; }
    try {
      ctx = new AC();
      master = ctx.createGain();
      master.gain.value = enabled ? MASTER_GAIN : 0;
      master.connect(ctx.destination);
      sfxBus = ctx.createGain();
      sfxBus.gain.value = SFX_GAIN;
      sfxBus.connect(master);
      musicBus = ctx.createGain();
      musicBus.gain.value = MUSIC_GAIN;
      musicBus.connect(master);
      noiseBuffer = makeNoiseBuffer();
      waves.pulse25 = makePulseWave(0.25);
      waves.pulse12 = makePulseWave(0.125);
    } catch (e) {
      ctx = null;
      return null;
    }
    return ctx;
  }

  function isRunning() {
    return !!ctx && ctx.state === 'running';
  }

  function unlock() {
    var c = createContext();
    if (!c) { return; }
    if (c.state === 'running') {
      onRunning();
      return;
    }
    try {
      var p = c.resume();
      if (p && typeof p.then === 'function') { p.then(onRunning, noop); }
    } catch (e) { /* ignore */ }
  }

  function onRunning() {
    if (enabled && currentTrack && !music && !hiddenMuted) { startMusic(currentTrack); }
  }

  function noop() {}

  window.addEventListener('pointerdown', unlock, { passive: true });
  window.addEventListener('touchstart', unlock, { passive: true });
  window.addEventListener('keydown', unlock);

  // Background tabs: stop the scheduler (timers are throttled there) and mute.
  document.addEventListener('visibilitychange', function () {
    if (!ctx) { return; }
    if (document.hidden) {
      hiddenMuted = true;
      stopMusic();
      rampMaster(0);
    } else {
      hiddenMuted = false;
      rampMaster(enabled ? MASTER_GAIN : 0);
      if (enabled && currentTrack && isRunning()) { startMusic(currentTrack); }
    }
  });

  function rampMaster(value) {
    if (!master) { return; }
    var now = ctx.currentTime;
    master.gain.cancelScheduledValues(now);
    master.gain.setTargetAtTime(value, now, 0.02);
  }

  function makeNoiseBuffer() {
    var seconds = 1;
    var buffer = ctx.createBuffer(1, ctx.sampleRate * seconds, ctx.sampleRate);
    var data = buffer.getChannelData(0);
    // Deterministic LCG so the texture is identical on every load.
    var seed = 0x4000;
    for (var i = 0; i < data.length; i++) {
      seed = (seed * 1664525 + 1013904223) >>> 0;
      data[i] = (seed / 0xffffffff) * 2 - 1;
    }
    return buffer;
  }

  function makePulseWave(duty) {
    var n = 32;
    var real = new Float32Array(n);
    var imag = new Float32Array(n);
    for (var k = 1; k < n; k++) {
      imag[k] = (2 / (k * Math.PI)) * Math.sin(k * Math.PI * duty);
    }
    return ctx.createPeriodicWave(real, imag, { disableNormalization: false });
  }

  // --------------------------------------------------------------------------
  // Synth primitives
  // --------------------------------------------------------------------------

  var NOTE_INDEX = { C: 0, D: 2, E: 4, F: 5, G: 7, A: 9, B: 11 };
  var hzCache = {};

  /** "C4", "F#3", "Bb2" -> frequency in Hz (A4 = 440). */
  function hz(note) {
    if (typeof note === 'number') { return note; }
    var cached = hzCache[note];
    if (cached) { return cached; }
    var m = /^([A-G])(#|b)?(-?\d)$/.exec(note);
    if (!m) { throw new Error('bad note ' + note); }
    var semis = NOTE_INDEX[m[1]] + (m[2] === '#' ? 1 : m[2] === 'b' ? -1 : 0) + (parseInt(m[3], 10) + 1) * 12;
    var f = 440 * Math.pow(2, (semis - 69) / 12);
    hzCache[note] = f;
    return f;
  }

  function setWave(osc, w) {
    if (w === 'pulse25' || w === 'pulse12') {
      osc.setPeriodicWave(waves[w]);
    } else {
      osc.type = w || 'square';
    }
  }

  /**
   * One oscillator voice.
   * o = { w: waveform, f: start Hz/note, f2: end Hz/note (glide), t: start, d: duration,
   *       v: volume, a: attack s, h: hold fraction (0 = pluck, 0.7 = gate), bus,
   *       vib: { rate, depth } optional vibrato in Hz }
   */
  function tone(o) {
    var t = o.t;
    var d = o.d;
    var v = o.v == null ? 0.4 : o.v;
    var a = o.a == null ? 0.004 : o.a;
    var hold = o.h == null ? 0 : o.h;
    var osc = ctx.createOscillator();
    setWave(osc, o.w);
    var f0 = hz(o.f);
    osc.frequency.setValueAtTime(f0, t);
    if (o.f2 != null) {
      var f1 = hz(o.f2);
      if (f1 !== f0) { osc.frequency.exponentialRampToValueAtTime(f1, t + d); }
    }
    var g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, t);
    g.gain.linearRampToValueAtTime(v, t + a);
    if (hold > 0) {
      var tHold = t + Math.max(a, d * hold);
      g.gain.setValueAtTime(v, tHold);
      g.gain.exponentialRampToValueAtTime(0.0001, t + d);
    } else {
      g.gain.exponentialRampToValueAtTime(0.0001, t + d);
    }
    osc.connect(g);
    g.connect(o.bus || sfxBus);
    if (o.vib) {
      var lfo = ctx.createOscillator();
      lfo.type = 'sine';
      lfo.frequency.setValueAtTime(o.vib.rate, t);
      var lfoGain = ctx.createGain();
      lfoGain.gain.setValueAtTime(o.vib.depth, t);
      lfo.connect(lfoGain);
      lfoGain.connect(osc.frequency);
      lfo.start(t);
      lfo.stop(t + d + 0.05);
    }
    osc.start(t);
    osc.stop(t + d + 0.05);
    return osc;
  }

  /**
   * Filtered noise burst. o = { t, d, v, type: 'lowpass'|'highpass'|'bandpass', f, f2, q, a, h, bus }
   */
  function noise(o) {
    var t = o.t;
    var d = o.d;
    var v = o.v == null ? 0.3 : o.v;
    var a = o.a == null ? 0.002 : o.a;
    var hold = o.h == null ? 0 : o.h;
    var src = ctx.createBufferSource();
    src.buffer = noiseBuffer;
    src.loop = true;
    src.playbackRate.value = o.rate || 1;
    var node = src;
    if (o.f) {
      var filter = ctx.createBiquadFilter();
      filter.type = o.type || 'lowpass';
      filter.frequency.setValueAtTime(o.f, t);
      if (o.f2) { filter.frequency.exponentialRampToValueAtTime(o.f2, t + d); }
      filter.Q.value = o.q == null ? 0.8 : o.q;
      src.connect(filter);
      node = filter;
    }
    var g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, t);
    g.gain.linearRampToValueAtTime(v, t + a);
    if (hold > 0) {
      g.gain.setValueAtTime(v, t + Math.max(a, d * hold));
    }
    g.gain.exponentialRampToValueAtTime(0.0001, t + d);
    node.connect(g);
    g.connect(o.bus || sfxBus);
    src.start(t);
    src.stop(t + d + 0.05);
  }

  /** Plays notes back to back. */
  function seq(t, notes, step, o) {
    for (var i = 0; i < notes.length; i++) {
      if (notes[i] === '-') { continue; }
      tone({ w: o.w, f: notes[i], t: t + i * step, d: o.d || step * 0.95, v: o.v, a: o.a, h: o.h, bus: o.bus });
    }
    return t + notes.length * step;
  }

  /** Plays notes simultaneously. */
  function chord(t, notes, d, o) {
    for (var i = 0; i < notes.length; i++) {
      tone({ w: o.w, f: notes[i], t: t, d: d, v: o.v, a: o.a, h: o.h, bus: o.bus });
    }
  }

  // --------------------------------------------------------------------------
  // Sound cues (one distinct jingle/blip per SoundCue name)
  // --------------------------------------------------------------------------

  var CUES = {
    boot: function (t) {
      // Power-on hum, disk tick, rising "system ready" arpeggio.
      noise({ t: t, d: 0.08, v: 0.12, type: 'highpass', f: 3000 });
      tone({ w: 'triangle', f: 55, f2: 110, t: t, d: 0.35, v: 0.25, h: 0.6 });
      seq(t + 0.25, ['C4', 'E4', 'G4', 'C5'], 0.075, { w: 'triangle', v: 0.3, h: 0.5 });
      tone({ w: 'pulse25', f: 'E5', t: t + 0.55, d: 0.25, v: 0.18, h: 0.4 });
    },
    menu_move: function (t) {
      tone({ w: 'square', f: 'A5', t: t, d: 0.045, v: 0.22 });
    },
    menu_select: function (t) {
      tone({ w: 'square', f: 'E5', t: t, d: 0.05, v: 0.26 });
      tone({ w: 'square', f: 'B5', t: t + 0.05, d: 0.1, v: 0.26 });
    },
    start: function (t) {
      seq(t, ['C5', 'E5', 'G5'], 0.07, { w: 'square', v: 0.26 });
      tone({ w: 'square', f: 'C6', t: t + 0.21, d: 0.3, v: 0.26, h: 0.5 });
      tone({ w: 'triangle', f: 'C4', t: t + 0.21, d: 0.3, v: 0.3, h: 0.5 });
    },
    jump: function (t) {
      tone({ w: 'square', f: 280, f2: 760, t: t, d: 0.13, v: 0.22, h: 0.3 });
    },
    land: function (t) {
      noise({ t: t, d: 0.06, v: 0.25, type: 'lowpass', f: 400 });
      tone({ w: 'triangle', f: 130, f2: 70, t: t, d: 0.07, v: 0.3 });
    },
    token: function (t) {
      tone({ w: 'square', f: 'E6', t: t, d: 0.06, v: 0.22 });
      tone({ w: 'square', f: 'A6', t: t + 0.06, d: 0.16, v: 0.22, h: 0.4 });
    },
    coffee: function (t) {
      // Percolating bubbles then a satisfied "ahh" slide.
      seq(t, ['A4', 'C#5', 'E5', 'A5'], 0.055, { w: 'triangle', v: 0.3 });
      noise({ t: t, d: 0.2, v: 0.08, type: 'bandpass', f: 900, f2: 2400, q: 2 });
      tone({ w: 'pulse25', f: 'E5', f2: 'A5', t: t + 0.24, d: 0.22, v: 0.2, h: 0.5 });
    },
    powerup: function (t) {
      seq(t, ['C5', 'E5', 'G5', 'C6', 'E6', 'G6'], 0.045, { w: 'square', v: 0.22 });
      tone({ w: 'pulse25', f: 'C7', t: t + 0.27, d: 0.18, v: 0.16, h: 0.3 });
    },
    powerdown: function (t) {
      seq(t, ['G5', 'E5', 'C5', 'G4', 'E4'], 0.07, { w: 'triangle', v: 0.3 });
      tone({ w: 'square', f: 'C4', f2: 'C3', t: t + 0.35, d: 0.2, v: 0.18 });
    },
    hurt: function (t) {
      tone({ w: 'square', f: 420, f2: 120, t: t, d: 0.2, v: 0.28, h: 0.2 });
      noise({ t: t, d: 0.12, v: 0.2, type: 'bandpass', f: 1200, q: 1.2 });
    },
    death: function (t) {
      // Classic three-step fall with a wobble and a final thud.
      tone({ w: 'square', f: 'A4', f2: 'E4', t: t, d: 0.18, v: 0.26, h: 0.5 });
      tone({ w: 'square', f: 'F4', f2: 'C4', t: t + 0.2, d: 0.18, v: 0.26, h: 0.5 });
      tone({ w: 'square', f: 'D4', f2: 'G3', t: t + 0.4, d: 0.22, v: 0.26, h: 0.5, vib: { rate: 12, depth: 10 } });
      tone({ w: 'triangle', f: 90, f2: 40, t: t + 0.66, d: 0.3, v: 0.35 });
      noise({ t: t + 0.66, d: 0.2, v: 0.2, type: 'lowpass', f: 300 });
    },
    stomp: function (t) {
      noise({ t: t, d: 0.09, v: 0.3, type: 'lowpass', f: 1800, f2: 200 });
      tone({ w: 'square', f: 240, f2: 70, t: t, d: 0.12, v: 0.28 });
      tone({ w: 'square', f: 'E5', f2: 'B5', t: t + 0.1, d: 0.1, v: 0.16 });
    },
    terminal_ok: function (t) {
      tone({ w: 'triangle', f: 'A5', t: t, d: 0.09, v: 0.32 });
      tone({ w: 'triangle', f: 'E6', t: t + 0.1, d: 0.22, v: 0.32, h: 0.4 });
      tone({ w: 'pulse25', f: 'A6', t: t + 0.1, d: 0.22, v: 0.08, h: 0.4 });
    },
    terminal_denied: function (t) {
      tone({ w: 'pulse25', f: 150, t: t, d: 0.11, v: 0.3, h: 0.8 });
      tone({ w: 'pulse25', f: 140, t: t + 0.14, d: 0.16, v: 0.3, h: 0.8 });
      tone({ w: 'square', f: 75, t: t + 0.14, d: 0.16, v: 0.15, h: 0.8 });
    },
    checkpoint: function (t) {
      seq(t, ['D5', 'F#5', 'A5', 'D6'], 0.065, { w: 'triangle', v: 0.3 });
      chord(t + 0.26, ['A5', 'D6'], 0.3, { w: 'pulse25', v: 0.14, h: 0.5 });
    },
    exit_open: function (t) {
      // Lock sliding open, then a bright two-note resolution.
      noise({ t: t, d: 0.12, v: 0.15, type: 'bandpass', f: 600, f2: 3000, q: 3 });
      tone({ w: 'square', f: 'G4', f2: 'G5', t: t, d: 0.18, v: 0.2, h: 0.3 });
      chord(t + 0.2, ['G5', 'B5', 'D6'], 0.35, { w: 'pulse25', v: 0.14, h: 0.5 });
    },
    exit_enter: function (t) {
      // Whoosh down the corridor with a descending triple.
      noise({ t: t, d: 0.35, v: 0.18, type: 'lowpass', f: 3500, f2: 150 });
      seq(t, ['C6', 'G5', 'E5', 'C5'], 0.08, { w: 'triangle', v: 0.28 });
    },
    boss_appear: function (t) {
      // Three low menacing pulses + a dissonant stab.
      for (var i = 0; i < 3; i++) {
        tone({ w: 'pulse25', f: 55, t: t + i * 0.22, d: 0.18, v: 0.4, h: 0.6 });
        noise({ t: t + i * 0.22, d: 0.1, v: 0.14, type: 'lowpass', f: 500 });
      }
      chord(t + 0.66, ['D4', 'Ab4', 'D5'], 0.5, { w: 'square', v: 0.14, h: 0.6 });
    },
    boss_shoot: function (t) {
      tone({ w: 'pulse12', f: 900, f2: 180, t: t, d: 0.11, v: 0.24 });
    },
    boss_charge: function (t) {
      tone({ w: 'square', f: 160, f2: 1400, t: t, d: 0.5, v: 0.2, h: 0.85, vib: { rate: 28, depth: 40 } });
      noise({ t: t, d: 0.5, v: 0.08, type: 'highpass', f: 2000, f2: 6000 });
    },
    boss_hit: function (t) {
      noise({ t: t, d: 0.12, v: 0.3, type: 'bandpass', f: 2600, q: 1.5 });
      tone({ w: 'square', f: 'A5', f2: 'A3', t: t, d: 0.18, v: 0.26 });
      tone({ w: 'triangle', f: 120, f2: 50, t: t, d: 0.14, v: 0.3 });
    },
    boss_stun: function (t) {
      // Dizzy wobble.
      tone({ w: 'triangle', f: 'E5', f2: 'C5', t: t, d: 0.6, v: 0.28, h: 0.8, vib: { rate: 9, depth: 45 } });
      seq(t + 0.05, ['B5', 'A5', 'G5', 'F#5', 'E5'], 0.11, { w: 'pulse25', v: 0.1 });
    },
    boss_defeat: function (t) {
      // Grinding ring, big noise explosion, then a triumphant resolution.
      tone({ w: 'square', f: 300, f2: 30, t: t, d: 0.7, v: 0.22, h: 0.7, vib: { rate: 18, depth: 60 } });
      noise({ t: t + 0.1, d: 0.9, v: 0.35, type: 'lowpass', f: 4000, f2: 120, h: 0.2 });
      noise({ t: t + 0.5, d: 0.6, v: 0.18, type: 'lowpass', f: 1200, f2: 80 });
      seq(t + 1.0, ['C5', 'E5', 'G5', 'C6'], 0.09, { w: 'square', v: 0.22 });
      chord(t + 1.36, ['C5', 'E5', 'G5', 'C6'], 0.6, { w: 'pulse25', v: 0.14, h: 0.6 });
    },
    wave: function (t) {
      // Ground shockwave rumble.
      noise({ t: t, d: 0.35, v: 0.3, type: 'lowpass', f: 260, f2: 60, h: 0.3 });
      tone({ w: 'square', f: 70, f2: 35, t: t, d: 0.3, v: 0.3, h: 0.4 });
    },
    arena_shift: function (t) {
      // Mechanical relays clunking as the floor re-arranges.
      for (var i = 0; i < 3; i++) {
        noise({ t: t + i * 0.12, d: 0.05, v: 0.22, type: 'bandpass', f: 700 + i * 300, q: 4 });
        tone({ w: 'pulse25', f: 110 + i * 55, t: t + i * 0.12, d: 0.1, v: 0.22, h: 0.7 });
      }
      tone({ w: 'triangle', f: 'A3', f2: 'A4', t: t + 0.36, d: 0.25, v: 0.22, h: 0.4 });
    },
    rollback: function (t) {
      // Tape rewind: rising warble then a blip "restored".
      tone({ w: 'triangle', f: 220, f2: 1100, t: t, d: 0.3, v: 0.26, h: 0.8, vib: { rate: 22, depth: 70 } });
      noise({ t: t, d: 0.3, v: 0.06, type: 'highpass', f: 2500 });
      tone({ w: 'square', f: 'C6', t: t + 0.32, d: 0.06, v: 0.2 });
      tone({ w: 'square', f: 'G6', t: t + 0.38, d: 0.12, v: 0.2 });
    },
    warning: function (t) {
      // Two-tone alarm, three cycles.
      for (var i = 0; i < 3; i++) {
        tone({ w: 'square', f: 'A5', t: t + i * 0.16, d: 0.07, v: 0.2, h: 0.7 });
        tone({ w: 'square', f: 'E5', t: t + i * 0.16 + 0.08, d: 0.07, v: 0.2, h: 0.7 });
      }
    },
    victory: function (t) {
      // Short fanfare with a harmony line.
      var melody = ['G5', 'G5', 'G5', 'C6', '-', 'E6', 'D6', 'C6'];
      var harmony = ['E5', 'E5', 'E5', 'G5', '-', 'C6', 'B5', 'G5'];
      seq(t, melody, 0.09, { w: 'square', v: 0.22, h: 0.6 });
      seq(t, harmony, 0.09, { w: 'pulse25', v: 0.14, h: 0.6 });
      seq(t, ['C3', '-', 'G3', '-', 'C3', '-', 'G3', '-'], 0.09, { w: 'triangle', v: 0.3, h: 0.5 });
      chord(t + 0.74, ['C5', 'E5', 'G5', 'C6'], 0.7, { w: 'square', v: 0.14, h: 0.6 });
      chord(t + 0.74, ['C3'], 0.7, { w: 'triangle', v: 0.3, h: 0.6 });
    },
    gameover: function (t) {
      // Slow minor descent, the stream has buffered forever.
      seq(t, ['E5', 'D#5', 'D5', 'C#5'], 0.2, { w: 'square', v: 0.22, h: 0.7 });
      tone({ w: 'square', f: 'C5', f2: 'B4', t: t + 0.8, d: 0.5, v: 0.22, h: 0.8, vib: { rate: 6, depth: 6 } });
      seq(t, ['A2', '-', 'G#2', '-', 'G2', '-', 'F#2', '-'], 0.2, { w: 'triangle', v: 0.3, h: 0.7 });
      tone({ w: 'triangle', f: 'E2', t: t + 0.8, d: 0.6, v: 0.3, h: 0.8 });
    },
    pause: function (t) {
      tone({ w: 'square', f: 'E5', t: t, d: 0.05, v: 0.2 });
      tone({ w: 'square', f: 'A4', t: t + 0.07, d: 0.09, v: 0.2 });
    }
  };

  // --------------------------------------------------------------------------
  // Music: four short looping patterns (16th-note steps, "." = tie, "-" = rest)
  // --------------------------------------------------------------------------

  function bars() {
    return Array.prototype.join.call(arguments, ' ');
  }

  var TRACKS = {
    // "Midnight on-call": slow, moody arpeggios in A minor.
    title: {
      bpm: 100,
      lanes: [
        {
          w: 'pulse25', v: 0.2, h: 0.55,
          p: bars(
            'A4 . C5 . E5 . C5 . A4 . C5 . E5 . A5 .',
            'F4 . A4 . C5 . A4 . F4 . A4 . C5 . F5 .',
            'E4 . G4 . C5 . G4 . E4 . G4 . C5 . E5 .',
            'D4 . G4 . B4 . G4 . D4 . G4 . B4 . D5 .')
        },
        {
          w: 'square', v: 0.11, h: 0.9,
          p: bars(
            '- - - - E5 . . . . . . . D5 . . .',
            'C5 . . . . . . . - - - - A4 . . .',
            'G4 . . . . . . . C5 . . . . . . .',
            'B4 . . . . . . . D5 . . . . . . .')
        },
        {
          w: 'triangle', v: 0.34, h: 0.85,
          p: bars(
            'A2 . . . . . . . A2 . . . E2 . . .',
            'F2 . . . . . . . F2 . . . C3 . . .',
            'C3 . . . . . . . C3 . . . G2 . . .',
            'G2 . . . . . . . G2 . . . B2 . . .')
        },
        {
          drums: true,
          p: bars(
            'k - - - - - h - - - - - - - h -',
            'k - - - - - h - - - - - - - h -',
            'k - - - - - h - - - - - - - h -',
            'k - - - - - h - - - - - h - o -')
        }
      ]
    },

    // "Prime time push": upbeat driving loop, E minor / G major.
    level: {
      bpm: 150,
      lanes: [
        {
          w: 'square', v: 0.19, h: 0.55,
          p: bars(
            'E5 . . . G5 . E5 . B4 . . . D5 . E5 .',
            'C5 . . . E5 . C5 . G4 . . . C5 . D5 .',
            'B4 . . . D5 . G5 . D5 . . . B4 . G4 .',
            'A4 . . . . . . . F#4 . A4 . D5 . . .',
            'E5 . . . G5 . E5 . B5 . . . A5 . G5 .',
            'E5 . . . . . C5 . G5 . . . E5 . C5 .',
            'D5 . . . B4 . D5 . G5 . . . . . D5 .',
            'F#5 . . . E5 . D5 . A4 . . . . . . .')
        },
        {
          w: 'pulse25', v: 0.07, h: 0.4,
          p: bars(
            'E4 G4 B4 G4 E4 G4 B4 G4 E4 G4 B4 G4 E4 G4 B4 G4',
            'C4 E4 G4 E4 C4 E4 G4 E4 C4 E4 G4 E4 C4 E4 G4 E4',
            'G3 B3 D4 B3 G3 B3 D4 B3 G3 B3 D4 B3 G3 B3 D4 B3',
            'D4 F#4 A4 F#4 D4 F#4 A4 F#4 D4 F#4 A4 F#4 D4 F#4 A4 F#4')
        },
        {
          w: 'triangle', v: 0.4, h: 0.7,
          p: bars(
            'E2 . E3 . E2 . E3 . E2 . E3 . E2 . D3 .',
            'C2 . C3 . C2 . C3 . C2 . C3 . C2 . B2 .',
            'G2 . G3 . G2 . G3 . G2 . G3 . G2 . F#2 .',
            'D2 . D3 . D2 . D3 . D2 . D3 . D2 . D3 .')
        },
        {
          drums: true,
          p: bars(
            'k - h - s - h - k - h k s - h -',
            'k - h - s - h - k - h k s - h h',
            'k - h - s - h - k - h k s - h -',
            'k - h - s - h k k - h - s s h o')
        }
      ]
    },

    // "Buffering wheel of doom": tense chromatic riff in D minor.
    boss: {
      bpm: 170,
      lanes: [
        {
          w: 'pulse25', v: 0.3, h: 0.5,
          p: bars(
            'D2 D2 D3 D2 D2 F2 D2 Ab2 D2 D2 D3 D2 G2 D2 F2 D2',
            'D2 D2 D3 D2 D2 F2 D2 Ab2 D2 D2 D3 D2 G2 D2 F2 D2',
            'Bb1 Bb1 Bb2 Bb1 Bb1 Db2 Bb1 E2 Bb1 Bb1 Bb2 Bb1 Eb2 Bb1 Db2 Bb1',
            'A1 A1 A2 A1 A1 C2 A1 Eb2 A1 A1 A2 A1 C#2 E2 G2 Bb2')
        },
        {
          w: 'square', v: 0.16, h: 0.6,
          p: bars(
            '- - - - D5 . - - - - - - F5 . E5 .',
            '- - - - D5 . - - A5 . . . G#5 . G5 .',
            '- - - - Bb4 . - - - - - - D5 . Db5 .',
            'A4 . . . . . . . Bb4 . A4 . G4 . E4 .')
        },
        {
          w: 'triangle', v: 0.3, h: 0.9,
          p: bars(
            'D1 . . . . . . . . . . . . . . .',
            'D1 . . . . . . . . . . . . . . .',
            'Bb0 . . . . . . . . . . . . . . .',
            'A0 . . . . . . . . . . . . . . .')
        },
        {
          drums: true,
          p: bars(
            'k - h k s - h - k - k - s - h h',
            'k - h k s - h - k - k - s - s s',
            'k - h k s - h - k - k - s - h h',
            'k - h k s - h - k k k k s s s o')
        }
      ]
    },

    // "Stream restored": bright C major fanfare loop.
    victory: {
      bpm: 128,
      lanes: [
        {
          w: 'square', v: 0.2, h: 0.6,
          p: bars(
            'C5 . E5 . G5 . . . E5 . G5 . C6 . . .',
            'A5 . . . G5 . F5 . A5 . . . . . G5 .',
            'G5 . . . F5 . E5 . D5 . . . B4 . D5 .',
            'C5 . . . . . . . - - - - G4 . B4 .')
        },
        {
          w: 'pulse25', v: 0.12, h: 0.6,
          p: bars(
            'E4 . G4 . C5 . . . G4 . C5 . E5 . . .',
            'F5 . . . E5 . C5 . F5 . . . . . E5 .',
            'E5 . . . D5 . C5 . B4 . . . G4 . B4 .',
            'E4 . . . . . . . - - - - E4 . G4 .')
        },
        {
          w: 'triangle', v: 0.38, h: 0.8,
          p: bars(
            'C3 . . . G2 . . . C3 . . . G2 . . .',
            'F2 . . . C3 . . . F2 . . . C3 . . .',
            'G2 . . . D3 . . . G2 . . . B2 . . .',
            'C3 . . . . . . . G2 . . . G2 . . .')
        },
        {
          drums: true,
          p: bars(
            'k - h - s - h - k - h - s - h -',
            'k - h - s - h - k - h - s - h o',
            'k - h - s - h - k - h - s - h -',
            'k - h - s - h - k - h k s s h o')
        }
      ]
    }
  };

  /** Compiles a pattern string into { length, events[step] = { note, len } }. */
  function compileLane(lane) {
    var tokens = lane.p.trim().split(/\s+/);
    var events = new Array(tokens.length);
    for (var i = 0; i < tokens.length; i++) {
      var tok = tokens[i];
      if (tok === '-' || tok === '.') { continue; }
      var len = 1;
      while (i + len < tokens.length && tokens[i + len] === '.') { len++; }
      events[i] = { note: tok, len: len };
    }
    return { def: lane, length: tokens.length, events: events };
  }

  function playDrum(kind, t, bus) {
    switch (kind) {
      case 'k':
        tone({ w: 'triangle', f: 160, f2: 42, t: t, d: 0.13, v: 0.55, bus: bus });
        noise({ t: t, d: 0.03, v: 0.2, type: 'lowpass', f: 900, bus: bus });
        break;
      case 's':
        noise({ t: t, d: 0.12, v: 0.3, type: 'bandpass', f: 1900, q: 0.9, bus: bus });
        tone({ w: 'triangle', f: 220, f2: 120, t: t, d: 0.07, v: 0.22, bus: bus });
        break;
      case 'h':
        noise({ t: t, d: 0.03, v: 0.14, type: 'highpass', f: 6500, bus: bus });
        break;
      case 'o':
        noise({ t: t, d: 0.11, v: 0.12, type: 'highpass', f: 5500, bus: bus });
        break;
      default:
        break;
    }
  }

  function startMusic(name) {
    var def = TRACKS[name];
    if (!def || !ctx) { return; }
    stopMusic();
    var bus = ctx.createGain();
    bus.gain.value = 1;
    bus.connect(musicBus);
    var lanes = [];
    for (var i = 0; i < def.lanes.length; i++) { lanes.push(compileLane(def.lanes[i])); }
    music = {
      name: name,
      bus: bus,
      lanes: lanes,
      stepDur: 60 / def.bpm / 4,
      step: 0,
      nextTime: ctx.currentTime + 0.06,
      timer: 0
    };
    music.timer = window.setInterval(scheduleMusic, TICK_MS);
    scheduleMusic();
  }

  function scheduleMusic() {
    var m = music;
    if (!m) { return; }
    var horizon = ctx.currentTime + LOOKAHEAD_S;
    while (m.nextTime < horizon) {
      var t = m.nextTime;
      for (var i = 0; i < m.lanes.length; i++) {
        var lane = m.lanes[i];
        var ev = lane.events[m.step % lane.length];
        if (!ev) { continue; }
        if (lane.def.drums) {
          playDrum(ev.note, t, m.bus);
        } else {
          tone({ w: lane.def.w, f: ev.note, t: t, d: ev.len * m.stepDur * 0.98, v: lane.def.v, h: lane.def.h, a: 0.003, bus: m.bus });
        }
      }
      m.nextTime += m.stepDur;
      m.step++;
    }
  }

  function stopMusic() {
    var m = music;
    if (!m) { return; }
    music = null;
    window.clearInterval(m.timer);
    var now = ctx.currentTime;
    m.bus.gain.cancelScheduledValues(now);
    m.bus.gain.setTargetAtTime(0, now, 0.03);
    window.setTimeout(function () {
      try { m.bus.disconnect(); } catch (e) { /* ignore */ }
    }, 400);
  }

  // --------------------------------------------------------------------------
  // Public API
  // --------------------------------------------------------------------------

  function warnOnce(key, message) {
    if (warned[key]) { return; }
    warned[key] = true;
    console.warn(message);
  }

  window.setSoundEnabled = function (on) {
    enabled = !!on;
    if (!ctx) { return; }
    rampMaster(enabled && !hiddenMuted ? MASTER_GAIN : 0);
    if (enabled) {
      if (currentTrack && !music && isRunning() && !hiddenMuted) { startMusic(currentTrack); }
    } else {
      stopMusic();
    }
  };

  window.playSound = function (name) {
    if (!enabled || !isRunning()) { return; }
    var cue = CUES[name];
    if (!cue) {
      warnOnce('cue:' + name, '[vl4000 audio] unknown sound cue "' + name + '"');
      return;
    }
    var now = ctx.currentTime;
    if (lastPlayed[name] != null && now - lastPlayed[name] < MIN_REPEAT_S) { return; }
    lastPlayed[name] = now;
    try {
      cue(now + 0.005);
    } catch (e) {
      warnOnce('cueerr:' + name, '[vl4000 audio] cue "' + name + '" failed: ' + e);
    }
  };

  window.setMusic = function (track) {
    var next = track == null || track === '' ? null : String(track);
    if (next !== null && !TRACKS[next]) {
      warnOnce('track:' + next, '[vl4000 audio] unknown music track "' + next + '"');
      next = null;
    }
    if (next === currentTrack) { return; }   // mute / hidden / unlock paths restart it themselves
    currentTrack = next;
    if (!ctx) { return; }                     // starts on the first gesture (unlock)
    if (next === null) { stopMusic(); return; }
    if (enabled && isRunning() && !hiddenMuted) { startMusic(next); }
  };

  var analyser = null;
  var meterBuffer = null;

  /** Output level after the master gain (tooling / smoke test): { peak, rms, context }. */
  function meter() {
    if (!ctx || !master) { return { peak: 0, rms: 0, context: ctx ? ctx.state : 'none' }; }
    if (!analyser) {
      analyser = ctx.createAnalyser();
      analyser.fftSize = 2048;
      meterBuffer = new Float32Array(analyser.fftSize);
      master.connect(analyser);
    }
    analyser.getFloatTimeDomainData(meterBuffer);
    var peak = 0, sum = 0;
    for (var i = 0; i < meterBuffer.length; i++) {
      var v = meterBuffer[i];
      if (v < 0) { v = -v; }
      if (v > peak) { peak = v; }
      sum += meterBuffer[i] * meterBuffer[i];
    }
    return { peak: peak, rms: Math.sqrt(sum / meterBuffer.length), context: ctx.state };
  }

  window.vl4000Audio = {
    cues: function () { return Object.keys(CUES); },
    tracks: function () { return Object.keys(TRACKS); },
    unlock: unlock,
    meter: meter,
    state: function () {
      return {
        available: !!(window.AudioContext || window.webkitAudioContext),
        context: ctx ? ctx.state : 'none',
        enabled: enabled,
        track: currentTrack,
        playing: music ? music.name : null,
        step: music ? music.step : 0
      };
    }
  };
})();
