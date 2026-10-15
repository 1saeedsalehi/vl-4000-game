/* VIDEOLAND 4000 — standalone renderer harness.
 *
 * Drives wwwroot/js/phaser.interop.js + keyboard.js + audio.js without Blazor: a fake
 * tickRef runs a tiny JS platformer simulation and returns FrameSnapshot objects in the
 * exact wire format of docs/ARCHITECTURE.md §2. When the real atlas is missing (artists
 * still working) a generated placeholder atlas is used; frames that are absent from the
 * atlas must show the renderer's magenta fallback texture (the coffee pickup does this
 * on purpose).
 */
(function () {
  'use strict';

  var W = 320, H = 180, TILE = 16;
  var ASSETS = '../../src/BlazorUI/wwwroot/games/disk001/assets/';
  var params = new URLSearchParams(location.search);
  var logEl = document.getElementById('log');

  function log(msg) {
    console.log('[harness] ' + msg);
    if (logEl) { logEl.textContent = (logEl.textContent + '\n' + msg).split('\n').slice(-6).join('\n'); }
  }

  // ==========================================================================
  // Placeholder atlas (canvas drawn, Phaser JSON-hash format)
  // ==========================================================================

  var PAL = {
    k: '#0b0613', n: '#1a0f2e', p: '#3b1f6b', v: '#6a2fd1', M: '#e6007e', m: '#ff4fa3',
    W: '#f6f2ff', l: '#c9c2d6', g: '#6e6684', d: '#2e2940', u: '#8a7bb0', c: '#35d6ff',
    C: '#1b7fa8', G: '#3dff8a', e: '#1f8f4a', Y: '#ffd43b', y: '#b88a12', O: '#ff8a3b',
    R: '#ff3b5c', r: '#8f1c2e', S: '#f6c9a8', B: '#6b3d2a', b: '#3d2217', K: '#000000'
  };

  function buildPlaceholderAtlas() {
    var frames = {};   // name -> { w, h, draw(ctx, ox, oy) }

    function px(ctx, ox, oy, x, y, w, h, col) {
      ctx.fillStyle = PAL[col] || col;
      ctx.fillRect(ox + x, oy + y, w, h);
    }

    function player(pose, step) {
      return { w: 16, h: 20, draw: function (c, ox, oy) {
        var bob = pose === 'idle' && step === 1 ? 1 : 0;
        // head (8x8) with hair + headset
        px(c, ox, oy, 4, 1 + bob, 8, 8, 'S');
        px(c, ox, oy, 4, 1 + bob, 8, 2, 'b');
        px(c, ox, oy, 3, 3 + bob, 1, 4, 'd');
        px(c, ox, oy, 12, 3 + bob, 1, 4, 'd');
        px(c, ox, oy, 3, 2 + bob, 10, 1, 'd');
        px(c, ox, oy, 9, 5 + bob, 1, 1, 'K');       // eye (faces right)
        px(c, ox, oy, 12, 6 + bob, 2, 2, 'c');      // mic
        // body
        px(c, ox, oy, 4, 9 + bob, 8, 7, pose === 'hurt' ? 'R' : 'v');
        // laptop under the left arm
        px(c, ox, oy, 0, 11 + bob, 5, 4, 'l');
        px(c, ox, oy, 1, 12 + bob, 3, 2, 'C');
        // arms
        if (pose === 'fall' || pose === 'cheer') {
          px(c, ox, oy, 12, 5, 2, 5, 'S');
        } else {
          px(c, ox, oy, 12, 10 + bob, 2, 4, 'S');
        }
        // legs
        if (pose === 'jump') {
          px(c, ox, oy, 5, 15, 3, 3, 'd'); px(c, ox, oy, 9, 15, 3, 3, 'd');
        } else if (pose === 'run') {
          var o = [0, 2, 0, -2][step];
          px(c, ox, oy, 5 - o, 16, 3, 4, 'd'); px(c, ox, oy, 9 + o, 16, 3, 4, 'd');
        } else {
          px(c, ox, oy, 5, 16, 3, 4, 'd'); px(c, ox, oy, 9, 16, 3, 4, 'd');
        }
      } };
    }
    frames['player/idle/0'] = player('idle', 0);
    frames['player/idle/1'] = player('idle', 1);
    for (var i = 0; i < 4; i++) { frames['player/run/' + i] = player('run', i); }
    frames['player/jump/0'] = player('jump', 0);
    frames['player/fall/0'] = player('fall', 0);
    frames['player/hurt/0'] = player('hurt', 0);
    frames['player/dead/0'] = player('hurt', 0);

    function spinner(step) {
      return { w: 16, h: 16, draw: function (c, ox, oy) {
        var gap = step * Math.PI / 4;
        for (var y = 0; y < 16; y++) {
          for (var x = 0; x < 16; x++) {
            var dx = x + 0.5 - 8, dy = y + 0.5 - 8;
            var d = Math.sqrt(dx * dx + dy * dy);
            if (d < 4.6 || d > 7.2) { continue; }
            var a = Math.atan2(dy, dx) - gap;
            while (a < 0) { a += Math.PI * 2; }
            if (a > Math.PI * 1.5) { continue; }           // 3/4 ring
            var t = a / (Math.PI * 1.5);
            px(c, ox, oy, x, y, 1, 1, t < 0.33 ? 'W' : t < 0.66 ? 'm' : 'M');
          }
        }
        px(c, ox, oy, 6, 7, 1, 1, 'R'); px(c, ox, oy, 9, 7, 1, 1, 'R');
        px(c, ox, oy, 6, 9, 4, 1, 'R');
      } };
    }
    for (i = 0; i < 8; i++) { frames['spinner/spin/' + i] = spinner(i); }

    function token(step) {
      var widths = [12, 8, 2, 8];
      return { w: 16, h: 16, draw: function (c, ox, oy) {
        var w = widths[step], x0 = 8 - w / 2;
        px(c, ox, oy, x0, 2, w, 12, 'Y');
        if (w > 2) {
          px(c, ox, oy, x0, 5, w, 2, 'c');
          px(c, ox, oy, step === 3 ? x0 + w - 3 : x0 + 1, 9, 2, 2, 'y');
        }
      } };
    }
    for (i = 0; i < 4; i++) { frames['token/spin/' + i] = token(i); }

    function sparkle(step) {
      return { w: 16, h: 16, draw: function (c, ox, oy) {
        var r = 2 + step * 2;
        px(c, ox, oy, 8 - r, 7, r * 2, 2, step === 2 ? 'l' : 'Y');
        px(c, ox, oy, 7, 8 - r, 2, r * 2, step === 2 ? 'l' : 'W');
      } };
    }
    for (i = 0; i < 3; i++) { frames['sparkle/' + i] = sparkle(i); }

    function pop(step) {
      return { w: 16, h: 16, draw: function (c, ox, oy) {
        var r = 3 + step * 2;
        for (var y = 0; y < 16; y++) {
          for (var x = 0; x < 16; x++) {
            var d = Math.sqrt((x + 0.5 - 8) * (x + 0.5 - 8) + (y + 0.5 - 8) * (y + 0.5 - 8));
            if (Math.abs(d - r) < 1) { px(c, ox, oy, x, y, 1, 1, step === 0 ? 'W' : step === 1 ? 'l' : 'g'); }
          }
        }
      } };
    }
    for (i = 0; i < 3; i++) { frames['pop/' + i] = pop(i); }

    frames['tiles/floor_top'] = { w: 16, h: 16, draw: function (c, ox, oy) {
      px(c, ox, oy, 0, 0, 16, 16, 'd'); px(c, ox, oy, 0, 0, 16, 1, 'u'); px(c, ox, oy, 0, 1, 16, 1, 'g');
      px(c, ox, oy, 3, 6, 2, 1, 'n'); px(c, ox, oy, 10, 11, 3, 1, 'n');
    } };
    frames['tiles/floor'] = { w: 16, h: 16, draw: function (c, ox, oy) {
      px(c, ox, oy, 0, 0, 16, 16, 'd'); px(c, ox, oy, 2, 3, 3, 1, 'n'); px(c, ox, oy, 9, 9, 4, 1, 'n');
    } };
    frames['tiles/platform'] = { w: 16, h: 16, draw: function (c, ox, oy) {
      px(c, ox, oy, 0, 0, 16, 1, 'l');
      for (var x = 0; x < 16; x += 2) { px(c, ox, oy, x, 1, 1, 4, 'u'); }
      px(c, ox, oy, 0, 4, 16, 1, 'g');
    } };
    frames['tiles/wall'] = { w: 16, h: 16, draw: function (c, ox, oy) {
      px(c, ox, oy, 0, 0, 16, 16, 'n'); px(c, ox, oy, 0, 0, 16, 1, 'p'); px(c, ox, oy, 0, 0, 1, 16, 'p');
      px(c, ox, oy, 2, 2, 1, 1, 'g'); px(c, ox, oy, 13, 2, 1, 1, 'g'); px(c, ox, oy, 2, 13, 1, 1, 'g'); px(c, ox, oy, 13, 13, 1, 1, 'g');
    } };
    frames['tiles/rack_top'] = { w: 16, h: 16, draw: function (c, ox, oy) {
      px(c, ox, oy, 0, 0, 16, 16, 'd'); px(c, ox, oy, 0, 0, 16, 1, 'u'); px(c, ox, oy, 1, 3, 14, 1, 'n');
      px(c, ox, oy, 2, 5, 1, 1, 'G'); px(c, ox, oy, 4, 5, 1, 1, 'G'); px(c, ox, oy, 6, 5, 1, 1, 'R'); px(c, ox, oy, 1, 8, 14, 1, 'n');
      px(c, ox, oy, 2, 10, 1, 1, 'G'); px(c, ox, oy, 4, 10, 1, 1, 'c'); px(c, ox, oy, 1, 13, 14, 1, 'n');
    } };
    frames['tiles/rack'] = { w: 16, h: 16, draw: function (c, ox, oy) {
      px(c, ox, oy, 0, 0, 16, 16, 'd'); px(c, ox, oy, 1, 2, 14, 1, 'n'); px(c, ox, oy, 1, 7, 14, 1, 'n'); px(c, ox, oy, 1, 12, 14, 1, 'n');
      px(c, ox, oy, 2, 4, 1, 1, 'G'); px(c, ox, oy, 4, 4, 1, 1, 'R'); px(c, ox, oy, 2, 9, 1, 1, 'c'); px(c, ox, oy, 4, 14, 1, 1, 'G');
    } };
    frames['tiles/spike'] = { w: 16, h: 16, draw: function (c, ox, oy) {
      for (var x = 0; x < 16; x++) {
        var hgt = (x % 4 === 1 || x % 4 === 2) ? 7 : 3;
        px(c, ox, oy, x, 16 - hgt, 1, hgt, x % 2 ? 'M' : 'R');
      }
    } };
    frames['tiles/pillar'] = { w: 16, h: 16, draw: function (c, ox, oy) {
      px(c, ox, oy, 5, 0, 6, 16, 'p'); px(c, ox, oy, 5, 0, 1, 16, 'v'); px(c, ox, oy, 10, 0, 1, 16, 'n');
    } };
    frames['tiles/cable'] = { w: 16, h: 16, draw: function (c, ox, oy) {
      px(c, ox, oy, 7, 0, 2, 16, 'g'); px(c, ox, oy, 7, 6, 2, 2, 'M');
    } };
    frames['deco/monitor'] = { w: 16, h: 16, draw: function (c, ox, oy) {
      px(c, ox, oy, 1, 2, 14, 11, 'g'); px(c, ox, oy, 2, 3, 12, 8, 'C'); px(c, ox, oy, 3, 4, 6, 1, 'c'); px(c, ox, oy, 3, 6, 9, 1, 'c');
      px(c, ox, oy, 6, 13, 4, 2, 'd');
    } };
    frames['deco/lamp'] = { w: 16, h: 16, draw: function (c, ox, oy) {
      px(c, ox, oy, 7, 0, 2, 5, 'g'); px(c, ox, oy, 4, 5, 8, 3, 'd'); px(c, ox, oy, 5, 8, 6, 2, 'Y');
    } };
    frames['deco/poster'] = { w: 16, h: 16, draw: function (c, ox, oy) {
      px(c, ox, oy, 2, 1, 12, 14, 'n'); px(c, ox, oy, 3, 2, 10, 12, 'p'); px(c, ox, oy, 4, 4, 8, 3, 'm'); px(c, ox, oy, 4, 9, 8, 3, 'c');
    } };

    frames['bg/sky'] = { w: 64, h: 64, draw: function (c, ox, oy) {
      px(c, ox, oy, 0, 0, 64, 64, '#0e0819');
      var patches = [[4, 10, 12, 6], [30, 40, 20, 8], [50, 4, 10, 5]];
      for (var i = 0; i < patches.length; i++) { px(c, ox, oy, patches[i][0], patches[i][1], patches[i][2], patches[i][3], '#120a20'); }
      var stars = [[3, 5, 'l'], [17, 22, 'g'], [29, 9, 'W'], [44, 31, 'g'], [57, 14, 'l'], [11, 47, 'g'], [38, 55, 'l'], [60, 44, 'g'], [24, 36, 'g'], [50, 59, 'l']];
      for (i = 0; i < stars.length; i++) { px(c, ox, oy, stars[i][0], stars[i][1], 1, 1, stars[i][2]); }
    } };
    frames['bg/racks'] = { w: 64, h: 64, draw: function (c, ox, oy) {
      for (var r = 0; r < 4; r++) {
        var x0 = r * 16 + 1;
        var top = 14 + (r % 2) * 6;
        px(c, ox, oy, x0, top, 14, 64 - top, 'n');
        px(c, ox, oy, x0, top, 14, 1, 'p');
        for (var y = top + 4; y < 60; y += 6) {
          px(c, ox, oy, x0 + 2, y, 1, 1, (y + r) % 3 ? 'p' : 'v');
          px(c, ox, oy, x0 + 5, y, 1, 1, 'p');
        }
      }
    } };
    frames['bg/city'] = { w: 64, h: 64, draw: function (c, ox, oy) {
      var buildings = [[0, 30, 9], [10, 18, 7], [18, 38, 12], [31, 24, 10], [42, 44, 8], [51, 14, 12]];
      for (var i = 0; i < buildings.length; i++) {
        var b = buildings[i];
        px(c, ox, oy, b[0], b[1], b[2], 64 - b[1], 'n');
        for (var y = b[1] + 3; y < 60; y += 5) {
          for (var x = b[0] + 1; x < b[0] + b[2] - 1; x += 3) {
            if ((x * 7 + y * 3) % 5 === 0) { px(c, ox, oy, x, y, 1, 1, 'p'); }
          }
        }
      }
      px(c, ox, oy, 22, 30, 1, 8, 'g'); px(c, ox, oy, 22, 29, 1, 1, 'M');  // antenna with a beacon
      px(c, ox, oy, 46, 36, 1, 8, 'g'); px(c, ox, oy, 46, 35, 1, 1, 'R');
    } };

    // Shelf packing, same rules as tools/atlas/build_atlas.py.
    var names = Object.keys(frames).sort(function (a, b) {
      return (frames[b].h - frames[a].h) || (frames[b].w - frames[a].w) || (a < b ? -1 : 1);
    });
    var maxW = 256, padding = 1, x = 0, y = 0, shelf = 0, placements = {};
    names.forEach(function (name) {
      var f = frames[name];
      if (x + f.w + padding > maxW) { x = 0; y += shelf + padding; shelf = 0; }
      placements[name] = { x: x, y: y, w: f.w, h: f.h };
      x += f.w + padding;
      shelf = Math.max(shelf, f.h);
    });
    var height = 1;
    while (height < y + shelf) { height *= 2; }

    var canvas = document.createElement('canvas');
    canvas.width = maxW;
    canvas.height = height;
    var ctx = canvas.getContext('2d');
    names.forEach(function (name) {
      var p = placements[name];
      frames[name].draw(ctx, p.x, p.y);
    });

    var json = { frames: {}, meta: { app: 'renderer-harness placeholder', image: 'placeholder.png', format: 'RGBA8888', size: { w: maxW, h: height }, scale: '1' } };
    names.forEach(function (name) {
      var p = placements[name];
      json.frames[name] = {
        frame: { x: p.x, y: p.y, w: p.w, h: p.h },
        rotated: false, trimmed: false,
        spriteSourceSize: { x: 0, y: 0, w: p.w, h: p.h },
        sourceSize: { w: p.w, h: p.h }
      };
    });
    // Phaser's loader rejects data: URIs, so both files are served as blob: URLs.
    return new Promise(function (resolve) {
      canvas.toBlob(function (blob) {
        resolve({
          source: 'placeholder',
          textureUrl: URL.createObjectURL(blob),
          atlasUrl: URL.createObjectURL(new Blob([JSON.stringify(json)], { type: 'application/json' })),
          frameCount: names.length
        });
      }, 'image/png');
    });
  }

  function resolveAtlas() {
    var mode = params.get('atlas');
    if (mode === 'missing') {
      return Promise.resolve({ source: 'missing', textureUrl: ASSETS + 'does-not-exist.png', atlasUrl: ASSETS + 'does-not-exist.json', frameCount: 0 });
    }
    if (mode === 'placeholder') { return buildPlaceholderAtlas(); }
    return fetch(ASSETS + 'sprites.json', { cache: 'no-store' })
      .then(function (res) {
        if (!res.ok) { throw new Error('HTTP ' + res.status); }
        return res.json();
      })
      .then(function (data) {
        return { source: 'real', textureUrl: ASSETS + 'sprites.png', atlasUrl: ASSETS + 'sprites.json', frameCount: Object.keys(data.frames || {}).length };
      })
      .catch(function (err) {
        log('real atlas unavailable (' + err.message + '), using the generated placeholder atlas');
        return buildPlaceholderAtlas();
      });
  }

  // ==========================================================================
  // Level (ASCII legend of docs/ARCHITECTURE.md §6)
  // ==========================================================================

  var COLS = 72, ROWS = 11;
  var LEVEL_W = COLS * TILE;
  var ROW_Y0 = H - ROWS * TILE;   // 4
  var grid = [];
  for (var r = 0; r < ROWS; r++) { grid.push(new Array(COLS).fill('.')); }
  function put(row, c0, c1, ch) { for (var c = c0; c <= c1; c++) { grid[row][c] = ch; } }

  put(10, 0, COLS - 1, '#');
  put(10, 30, 32, '.');                       // the pit
  for (r = 0; r <= 9; r++) { grid[r][0] = 'W'; grid[r][COLS - 1] = 'W'; }
  put(7, 10, 13, '='); put(5, 18, 21, '='); put(7, 26, 29, '='); put(8, 36, 40, '=');
  put(6, 44, 47, '='); put(8, 52, 55, '='); put(6, 62, 65, '=');
  grid[9][8] = 'r';                                            // one tile high: hoppable
  put(7, 60, 61, 'r'); put(8, 60, 61, 'r'); put(9, 60, 61, 'r');
  put(9, 40, 41, '^'); grid[9][23] = '^';
  grid[9][3] = 'm'; grid[9][5] = 'm'; grid[9][34] = 'm'; grid[9][57] = 'm';
  grid[3][16] = 'l'; grid[4][45] = 'l'; grid[2][66] = 'l';
  for (r = 6; r <= 9; r++) { grid[r][24] = 'I'; grid[r][50] = 'I'; }
  put(0, 2, 3, '|'); put(1, 2, 3, '|'); put(0, 58, 58, '|'); put(1, 58, 58, '|');
  grid[8][2] = 'p'; grid[8][68] = 'p';

  function isSolid(ch) { return ch === '#' || ch === 'W' || ch === 'r'; }
  function cellAt(px, py) {
    var c = Math.floor(px / TILE), rr = Math.floor((py - ROW_Y0) / TILE);
    if (c < 0 || c >= COLS || rr < 0 || rr >= ROWS) { return c < 0 || c >= COLS ? 'W' : '.'; }
    return grid[rr][c];
  }

  function buildLevelSnapshot(theme) {
    var tiles = [];
    for (var row = 0; row < ROWS; row++) {
      for (var col = 0; col < COLS; col++) {
        var ch = grid[row][col];
        var above = row > 0 ? grid[row - 1][col] : '.';
        var f = null, d = 0;
        switch (ch) {
          case '#': f = above === '#' || above === 'r' ? 'tiles/floor' : 'tiles/floor_top'; break;
          case 'W': f = 'tiles/wall'; break;
          case 'r': f = above === 'r' ? 'tiles/rack' : 'tiles/rack_top'; break;
          case '=': f = 'tiles/platform'; break;
          case '^': f = 'tiles/spike'; break;
          case '|': f = 'tiles/cable'; d = -1; break;
          case 'I': f = 'tiles/pillar'; d = -1; break;
          case 'm': f = 'deco/monitor'; d = -1; break;
          case 'l': f = 'deco/lamp'; d = -1; break;
          case 'p': f = 'deco/poster'; d = -1; break;
          default: break;
        }
        if (f) { tiles.push({ f: f, x: col * TILE, y: ROW_Y0 + row * TILE, d: d }); }
      }
    }
    return { w: LEVEL_W, h: H, theme: theme, tiles: tiles };
  }

  // ==========================================================================
  // Simulation (harness only; the real game simulates in C#)
  // ==========================================================================

  var GRAVITY = 640, JUMP = 215, MAX_VX = 95, ACCEL = 900, FRICTION = 1100, FIXED_DT = 1 / 120;
  var THEMES = ['dungeon', 'arena', 'title'];

  var sim = {
    time: 0,
    accumulator: 0,
    levelId: 0,
    theme: 'dungeon',
    pendingLevel: null,
    music: 'level',
    flash: 0,
    shake: 0,
    score: 0,
    hp: 3,
    fx: [],
    effects: [],
    popups: [],
    toast: null,
    nextToastAt: 12,
    uid: 0,
    player: null,
    spinners: [],
    token: null,
    coffee: null,
    cam: { x: 0, y: 0, shake: 0 },
    ticks: 0
  };

  function makePlayer() {
    return { x: 40, y: ROW_Y0 + 10 * TILE, vx: 0, vy: 0, onGround: false, facing: 1, jumpHeld: false,
      coyote: 0, runClock: 0, hurt: 0, invuln: 0 };
  }

  function resetWorld() {
    sim.player = makePlayer();
    sim.spinners = [
      { k: 'spinner1', x0: 14 * TILE + 8, y: ROW_Y0 + 10 * TILE, x: 14 * TILE + 8, dir: 1, dead: 0 },
      { k: 'spinner2', x0: 46 * TILE, y: ROW_Y0 + 6 * TILE, x: 46 * TILE, dir: -1, dead: 0 },
      { k: 'spinner3', x0: 58 * TILE, y: ROW_Y0 + 10 * TILE, x: 58 * TILE, dir: 1, dead: 0 }
    ];
    sim.tokenSpots = [[20 * TILE, ROW_Y0 + 5 * TILE - 2], [38 * TILE, ROW_Y0 + 8 * TILE - 2], [64 * TILE, ROW_Y0 + 6 * TILE - 2]];
    sim.token = { spot: 0, respawn: 0 };
    sim.coffee = { x: 49 * TILE + 8, y: ROW_Y0 + 10 * TILE, respawn: 0 };
    sim.pendingLevel = buildLevelSnapshot(sim.theme);
    sim.levelId++;
  }

  function rectsOverlap(ax0, ay0, ax1, ay1, bx0, by0, bx1, by1) {
    return ax0 < bx1 && ax1 > bx0 && ay0 < by1 && ay1 > by0;
  }

  function solidInBox(x0, x1, y0, y1) {
    var c0 = Math.floor(x0 / TILE), c1 = Math.floor(x1 / TILE);
    var r0 = Math.floor((y0 - ROW_Y0) / TILE), r1 = Math.floor((y1 - ROW_Y0) / TILE);
    for (var rr = r0; rr <= r1; rr++) {
      for (var cc = c0; cc <= c1; cc++) {
        if (cc < 0 || cc >= COLS) { return true; }          // outside the level counts as wall
        if (rr < 0 || rr >= ROWS) { continue; }
        if (isSolid(grid[rr][cc])) { return true; }
      }
    }
    return false;
  }

  function findLanding(x0, x1, oldY, newY) {
    var best = null;
    var rowStart = Math.ceil((oldY - ROW_Y0) / TILE), rowEnd = Math.floor((newY - ROW_Y0) / TILE);
    for (var row = rowStart; row <= rowEnd; row++) {
      if (row < 0 || row >= ROWS) { continue; }
      var top = ROW_Y0 + row * TILE;
      if (top < oldY - 0.001 || top > newY + 0.001) { continue; }
      for (var col = Math.floor(x0 / TILE); col <= Math.floor(x1 / TILE); col++) {
        if (col < 0 || col >= COLS) { continue; }
        var ch = grid[row][col];
        if (isSolid(ch) || ch === '=') {
          if (best === null || top < best) { best = top; }
        }
      }
    }
    return best;
  }

  function groundBelow(x0, x1, y) {
    for (var col = Math.floor(x0 / TILE); col <= Math.floor(x1 / TILE); col++) {
      var ch = cellAt(col * TILE + 1, y + 1);
      if (isSolid(ch) || (ch === '=' && Math.abs(((y + 1 - ROW_Y0) % TILE)) < 1.5)) { return true; }
    }
    return false;
  }

  function popup(text, x, y, color) {
    sim.popups.push({ k: 'pop' + (++sim.uid), text: text, x: x, y: y, t: 0, dur: 0.9, c: color || '#ffd43b' });
  }

  function effect(prefix, count, x, y, dur) {
    sim.effects.push({ k: 'fx' + (++sim.uid), prefix: prefix, count: count, x: x, y: y, t: 0, dur: dur });
  }

  function hurtPlayer(p, knockDir) {
    if (p.invuln > 0) { return; }
    sim.hp--;
    p.hurt = 0.35;
    p.invuln = 1.4;
    p.vy = -140;
    p.vx = 110 * knockDir;
    sim.flash = Math.max(sim.flash, 0.7);
    sim.shake = Math.max(sim.shake, 0.6);
    if (sim.hp <= 0) {
      sim.fx.push('death');
      sim.hp = 3;
      sim.player = makePlayer();
      sim.player.invuln = 2;
      sim.toast = { text: 'SESSION EXPIRED. RE-AUTHENTICATING...', until: sim.time + 2.5, c: '#ff3b5c' };
    } else {
      sim.fx.push('hurt');
    }
  }

  function stepPlayer(dt, bits) {
    var p = sim.player;
    var left = bits & 1, right = bits & 2, jump = bits & 4;

    if (p.hurt <= 0) {
      if (left && !right) { p.vx -= ACCEL * dt; p.facing = -1; }
      else if (right && !left) { p.vx += ACCEL * dt; p.facing = 1; }
      else if (p.vx !== 0) {
        var f = FRICTION * dt;
        p.vx = Math.abs(p.vx) <= f ? 0 : p.vx - Math.sign(p.vx) * f;
      }
      p.vx = Math.max(-MAX_VX, Math.min(MAX_VX, p.vx));
    } else {
      p.vx *= Math.pow(0.02, dt);
    }

    var jumpPressed = jump && !p.jumpHeld;
    p.jumpHeld = !!jump;
    if (jumpPressed && (p.onGround || p.coyote > 0) && p.hurt <= 0) {
      p.vy = -JUMP;
      p.onGround = false;
      p.coyote = 0;
      sim.fx.push('jump');
    }
    if (!jump && p.vy < -70) { p.vy = -70; }
    p.vy = Math.min(p.vy + GRAVITY * dt, 300);

    var nx = p.x + p.vx * dt;
    if (solidInBox(nx - 5, nx + 5, p.y - 17, p.y - 1)) { nx = p.x; p.vx = 0; }
    p.x = Math.max(6, Math.min(LEVEL_W - 6, nx));

    var ny = p.y + p.vy * dt;
    if (p.vy > 0) {
      var landY = findLanding(p.x - 4, p.x + 4, p.y, ny);
      if (landY !== null) {
        ny = landY;
        if (!p.onGround) { sim.fx.push('land'); }
        p.onGround = true;
        p.vy = 0;
        p.coyote = 0.08;
      } else {
        if (p.onGround) { p.coyote = 0.08; }
        p.onGround = false;
      }
    } else if (p.vy < 0) {
      if (solidInBox(p.x - 4, p.x + 4, ny - 18, ny - 18)) { ny = p.y; p.vy = 0; }
      p.onGround = false;
    } else if (!groundBelow(p.x - 4, p.x + 4, p.y)) {
      p.onGround = false;
    }
    p.y = ny;
    p.coyote = Math.max(0, p.coyote - dt);
    p.hurt = Math.max(0, p.hurt - dt);
    p.invuln = Math.max(0, p.invuln - dt);
    if (Math.abs(p.vx) > 10 && p.onGround) { p.runClock += Math.abs(p.vx) * dt / 9; }

    if (p.y > H + 24) {
      sim.fx.push('death');
      sim.hp = 3;
      sim.player = makePlayer();
      sim.player.invuln = 2;
      sim.flash = 1;
      sim.toast = { text: 'FELL INTO THE BACKLOG', until: sim.time + 2.5, c: '#ff3b5c' };
      return;
    }

    // Glitch spikes: bottom 8 px of a '^' cell.
    var feetCell = cellAt(p.x, p.y - 1);
    if (feetCell === '^' && ((p.y - 1 - ROW_Y0) % TILE) >= 8) { hurtPlayer(p, -p.facing); }
  }

  function stepSpinners(dt) {
    var p = sim.player;
    for (var i = 0; i < sim.spinners.length; i++) {
      var s = sim.spinners[i];
      if (s.dead > 0) {
        s.dead -= dt;
        if (s.dead <= 0) { s.x = s.x0; s.dir = 1; }
        continue;
      }
      s.x += s.dir * 40 * dt;
      if (s.x > s.x0 + 48) { s.x = s.x0 + 48; s.dir = -1; }
      if (s.x < s.x0 - 48) { s.x = s.x0 - 48; s.dir = 1; }
      if (p.invuln <= 0 && rectsOverlap(p.x - 5, p.y - 18, p.x + 5, p.y, s.x - 7, s.y - 14, s.x + 7, s.y)) {
        if (p.vy > 0 && p.y < s.y - 7) {
          s.dead = 3;
          p.vy = -150;
          sim.score += 150;
          sim.fx.push('stomp');
          effect('pop', 3, s.x, s.y, 0.3);
          popup('+150', s.x, s.y - 18, '#ff4fa3');
        } else {
          hurtPlayer(p, p.x < s.x ? -1 : 1);
        }
      }
    }
  }

  function stepPickups(dt) {
    var p = sim.player;
    var tk = sim.token;
    if (tk.respawn > 0) {
      tk.respawn -= dt;
    } else {
      var spot = sim.tokenSpots[tk.spot];
      var ty = spot[1] + Math.sin(sim.time * 4) * 2;
      if (rectsOverlap(p.x - 5, p.y - 18, p.x + 5, p.y, spot[0] - 6, ty - 14, spot[0] + 6, ty)) {
        sim.score += 100;
        sim.fx.push('token');
        effect('sparkle', 3, spot[0], ty, 0.3);
        popup('+100', spot[0], ty - 16, '#ffd43b');
        tk.respawn = 4;
        tk.spot = (tk.spot + 1) % sim.tokenSpots.length;
      }
    }
    var cf = sim.coffee;
    if (cf.respawn > 0) {
      cf.respawn -= dt;
    } else if (rectsOverlap(p.x - 5, p.y - 18, p.x + 5, p.y, cf.x - 6, cf.y - 14, cf.x + 6, cf.y)) {
      sim.score += 50;
      sim.fx.push('coffee');
      popup('+50 CAFFEINE', cf.x, cf.y - 18, '#f6f2ff');
      cf.respawn = 6;
    }
  }

  function stepWorld(dt, bits) {
    sim.time += dt;
    stepPlayer(dt, bits);
    stepSpinners(dt);
    stepPickups(dt);
    sim.flash = Math.max(0, sim.flash - 2.5 * dt);
    sim.shake = Math.max(0, sim.shake - 1.4 * dt);
    for (var i = sim.effects.length - 1; i >= 0; i--) {
      sim.effects[i].t += dt;
      if (sim.effects[i].t >= sim.effects[i].dur) { sim.effects.splice(i, 1); }
    }
    for (i = sim.popups.length - 1; i >= 0; i--) {
      sim.popups[i].t += dt;
      if (sim.popups[i].t >= sim.popups[i].dur) { sim.popups.splice(i, 1); }
    }
    if (sim.time >= sim.nextToastAt) {
      sim.nextToastAt = sim.time + 14;
      sim.fx.push('warning');
      sim.toast = { text: 'DEPLOY FREEZE: IT IS FRIDAY', until: sim.time + 2.5, c: '#ffd43b' };
    }
    var target = Math.max(0, Math.min(LEVEL_W - W, sim.player.x - W / 2));
    sim.cam.x += (target - sim.cam.x) * Math.min(1, 8 * dt);
  }

  function playerFrame(p) {
    if (p.hurt > 0) { return 'player/hurt/0'; }
    if (!p.onGround) { return p.vy < 0 ? 'player/jump/0' : 'player/fall/0'; }
    if (Math.abs(p.vx) > 10) { return 'player/run/' + (Math.floor(p.runClock) % 4); }
    return 'player/idle/' + (Math.floor(sim.time * 2.5) % 2);
  }

  function pad(n, width) {
    var s = String(n);
    while (s.length < width) { s = '0' + s; }
    return s;
  }

  function buildSnapshot() {
    var p = sim.player;
    var sprites = [];
    var labels = [];
    var i;

    sprites.push({ k: 'player', f: playerFrame(p), x: Math.round(p.x), y: Math.round(p.y), fx: p.facing < 0,
      a: p.invuln > 0 && Math.floor(sim.time * 12) % 2 === 0 ? 0.35 : 1, s: 1, d: 10, t: null, ox: 0.5, oy: 1, r: 0 });

    for (i = 0; i < sim.spinners.length; i++) {
      var s = sim.spinners[i];
      if (s.dead > 0) { continue; }
      sprites.push({ k: s.k, f: 'spinner/spin/' + (Math.floor(sim.time / 0.07) % 8), x: Math.round(s.x), y: s.y, fx: false,
        a: 1, s: 1, d: 6, t: null, ox: 0.5, oy: 1, r: 0 });
    }

    if (sim.token.respawn <= 0) {
      var spot = sim.tokenSpots[sim.token.spot];
      sprites.push({ k: 'token', f: 'token/spin/' + (Math.floor(sim.time / 0.12) % 4), x: spot[0], y: Math.round(spot[1] + Math.sin(sim.time * 4) * 2),
        fx: false, a: 1, s: 1, d: 5, t: null, ox: 0.5, oy: 1, r: 0 });
    }
    if (sim.coffee.respawn <= 0) {
      // 'coffee/idle/*' is deliberately absent from the placeholder atlas -> renderer fallback texture.
      sprites.push({ k: 'coffee', f: 'coffee/idle/' + (Math.floor(sim.time / 0.3) % 2), x: sim.coffee.x, y: sim.coffee.y, fx: false,
        a: 1, s: 1, d: 5, t: null, ox: 0.5, oy: 1, r: 0 });
    }
    for (i = 0; i < sim.effects.length; i++) {
      var e = sim.effects[i];
      var idx = Math.min(e.count - 1, Math.floor(e.t / e.dur * e.count));
      sprites.push({ k: e.k, f: e.prefix + '/' + idx, x: e.x, y: e.y, fx: false, a: 1, s: 1, d: 12, t: null, ox: 0.5, oy: 1, r: 0 });
    }
    // A tinted, scaled, rotated sprite exercises the remaining attributes.
    sprites.push({ k: 'badge', f: 'token/spin/0', x: 70 * TILE, y: ROW_Y0 + 10 * TILE - 24, fx: false, a: 0.9, s: 2, d: 3,
      t: 0x35d6ff, ox: 0.5, oy: 0.5, r: sim.time * 1.5 });
    // A frame that never exists in any atlas: must render as the magenta fallback box, never throw.
    sprites.push({ k: 'mystery', f: 'debug/missing/0', x: 8 * TILE + 8, y: ROW_Y0 + 9 * TILE, fx: false, a: 1, s: 1, d: 5,
      t: null, ox: 0.5, oy: 1, r: 0 });

    labels.push({ k: 'hud', x: W / 2, y: 8, text: 'SCORE ' + pad(sim.score, 5) + '  HP ' + sim.hp, c: '#ffd43b', a: 1, d: 20, sf: 0 });
    labels.push({ k: 'title', x: 128, y: 64, text: 'LOGIN DUNGEON', c: '#c9c2d6', a: 1, d: 20, sf: 1 });
    labels.push({ k: 'hint', x: 72, y: 118, text: 'ARROWS + SPACE', c: '#ff4fa3', a: 0.7 + 0.3 * Math.sin(sim.time * 3), d: 20, sf: 1 });
    labels.push({ k: 'far', x: 66 * TILE, y: 60, text: 'END OF SHIFT', c: '#3dff8a', a: 1, d: 20, sf: 1 });
    for (i = 0; i < sim.popups.length; i++) {
      var pu = sim.popups[i];
      var k = pu.t / pu.dur;
      labels.push({ k: pu.k, x: pu.x, y: Math.round(pu.y - 20 * k), text: pu.text, c: pu.c, a: k > 0.6 ? 1 - (k - 0.6) / 0.4 : 1, d: 20, sf: 1 });
    }
    if (sim.toast && sim.toast.until > sim.time) {
      labels.push({ k: 'toast', x: W / 2, y: 168, text: sim.toast.text, c: sim.toast.c, a: 1, d: 20, sf: 0 });
    }

    var level = sim.pendingLevel;
    sim.pendingLevel = null;
    var fx = sim.fx;
    sim.fx = [];
    return {
      cam: { x: sim.cam.x, y: 0, shake: sim.shake },
      sprites: sprites,
      labels: labels,
      fx: fx,
      flash: sim.flash,
      level: level,
      levelId: sim.levelId,
      running: true,
      music: sim.music
    };
  }

  function tick(deltaMs, bits) {
    sim.ticks++;
    sim.accumulator += Math.min(deltaMs, 50) / 1000;
    while (sim.accumulator >= FIXED_DT) {
      stepWorld(FIXED_DT, bits);
      sim.accumulator -= FIXED_DT;
    }
    return buildSnapshot();
  }

  var tickRef = {
    readyCount: 0,
    invokeMethod: function (name, a, b) {
      if (name === 'Ready') {
        this.readyCount++;
        log('Ready received (' + this.readyCount + ')');
        return undefined;
      }
      if (name === 'Tick') { return tick(a, b); }
      throw new Error('unknown method ' + name);
    },
    invokeMethodAsync: function (name, a, b) {
      return Promise.resolve(this.invokeMethod(name, a, b));
    }
  };

  // ==========================================================================
  // Page wiring
  // ==========================================================================

  var atlasInfo = null;
  var soundOn = true, motionReduced = false;

  function $(id) { return document.getElementById(id); }
  function blur(btn) { if (btn && btn.blur) { btn.blur(); } }

  function start() {
    return resolveAtlas().then(function (info) {
      atlasInfo = info;
      log('atlas source: ' + info.source + ' (' + info.frameCount + ' frames)');
      resetWorld();
      return window.startPhaser('phaser', W, H, info.textureUrl, info.atlasUrl, tickRef);
    }).then(function () {
      log('startPhaser resolved');
    }).catch(function (err) {
      console.error(err);
      log('startPhaser failed: ' + err.message);
    });
  }

  function restart() {
    log('destroying…');
    return window.destroyPhaser().then(function () {
      log('destroyed, restarting');
      return start();
    });
  }

  $('btn-pause').addEventListener('click', function () {
    var paused = !window.vl4000Stats().paused;
    window.setGamePaused(paused);
    this.textContent = paused ? 'Resume' : 'Pause';
    this.classList.toggle('on', paused);
    blur(this);
  });
  $('btn-flash').addEventListener('click', function () { sim.flash = 1; blur(this); });
  $('btn-shake').addEventListener('click', function () { sim.shake = 1; blur(this); });
  $('btn-theme').addEventListener('click', function () {
    sim.theme = THEMES[(THEMES.indexOf(sim.theme) + 1) % THEMES.length];
    sim.pendingLevel = buildLevelSnapshot(sim.theme);
    sim.levelId++;
    sim.fx.push('arena_shift');
    this.textContent = 'Theme: ' + sim.theme;
    blur(this);
  });
  $('sel-music').addEventListener('change', function () {
    sim.music = this.value === '' ? null : this.value;   // flows through the snapshot -> setMusic on change
    blur(this);
  });
  $('btn-sound').addEventListener('click', function () {
    soundOn = !soundOn;
    window.setSoundEnabled(soundOn);
    this.textContent = 'Sound: ' + (soundOn ? 'on' : 'off');
    this.classList.toggle('on', soundOn);
    blur(this);
  });
  $('btn-motion').addEventListener('click', function () {
    motionReduced = !motionReduced;
    window.setReducedMotion(motionReduced);
    this.textContent = 'Reduced motion: ' + (motionReduced ? 'on' : 'off');
    this.classList.toggle('on', motionReduced);
    blur(this);
  });

  var cueSelect = $('sel-cue');
  window.vl4000Audio.cues().forEach(function (name) {
    var opt = document.createElement('option');
    opt.value = name;
    opt.textContent = name;
    cueSelect.appendChild(opt);
  });
  $('btn-cue').addEventListener('click', function () { window.playSound(cueSelect.value); blur(this); });
  $('btn-all-cues').addEventListener('click', function () {
    var names = window.vl4000Audio.cues();
    names.forEach(function (name, i) { setTimeout(function () { window.playSound(name); }, i * 450); });
    log('playing all ' + names.length + ' cues');
    blur(this);
  });
  $('btn-restart').addEventListener('click', function () { blur(this); restart(); });
  $('btn-destroy').addEventListener('click', function () {
    blur(this);
    window.destroyPhaser().then(function () { log('destroyed'); });
  });

  Array.prototype.forEach.call(document.querySelectorAll('.touch button'), function (btn) {
    var bit = parseInt(btn.getAttribute('data-bit'), 10);
    var down = function (e) { e.preventDefault(); window.setTouchButton(bit, true); };
    var up = function () { window.setTouchButton(bit, false); };
    btn.addEventListener('pointerdown', down);
    btn.addEventListener('pointerup', up);
    btn.addEventListener('pointercancel', up);
    btn.addEventListener('pointerleave', up);
  });

  var statsEl = $('stats');
  setInterval(function () {
    var s = window.vl4000Stats();
    var a = window.vl4000Audio.state();
    statsEl.textContent =
      'ready ' + s.ready + '  renderer ' + s.renderer + '  fps ' + s.fps + '  tick ' + s.tickMs + ' ms (max ' + s.tickMsMax + ')  frames ' + s.frames +
      '\nsprites ' + s.sprites + '  labels ' + s.labels + '  statics ' + s.statics + '  levelId ' + s.levelId + '  atlas ' + (atlasInfo ? atlasInfo.source : '?') +
      '  missing [' + s.missingFrames.join(', ') + ']' +
      '\ninput bits ' + s.inputBits + '  paused ' + s.paused + '  reducedMotion ' + s.reducedMotion + '  tickErrors ' + s.tickErrors +
      '\naudio ' + a.context + '  enabled ' + a.enabled + '  track ' + a.track + '  playing ' + a.playing + '  step ' + a.step +
      '\nplayer x ' + Math.round(sim.player ? sim.player.x : 0) + ' y ' + Math.round(sim.player ? sim.player.y : 0) + '  cam ' + Math.round(sim.cam.x) + '  score ' + sim.score + '  hp ' + sim.hp;
  }, 250);

  window.harness = { sim: sim, tickRef: tickRef, start: start, restart: restart, atlas: function () { return atlasInfo; }, grid: grid };

  start();
})();
