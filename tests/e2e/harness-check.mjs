// Smoke test for the JavaScript renderer / input / audio modules using the standalone
// harness in tools/renderer-harness (no Blazor needed).
//
//   cd tests/e2e && npm install && node harness-check.mjs [--headed] [--atlas placeholder|missing]
//
// Starts serve.mjs on a random port, opens the harness in headless Chromium, drives the
// keyboard and the harness buttons, collects console errors / page errors / failed
// requests, reads window.vl4000Stats() and writes screenshots + summary.json to output/.
// Exit code 1 when anything failed.

import path from 'node:path';
import fs from 'node:fs';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright';
import { startServer } from './serve.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const headed = args.includes('--headed');
const atlasArg = args.indexOf('--atlas');
const atlasMode = atlasArg >= 0 ? args[atlasArg + 1] : null;
// Default mode writes to output/, the alternative atlas modes to output/<mode>/.
const outDir = path.join(here, 'output', atlasMode || '');
fs.mkdirSync(outDir, { recursive: true });

const failures = [];
const consoleErrors = [];
const consoleWarnings = [];
const pageErrors = [];
const failedRequests = [];
const notes = [];

function check(condition, message) {
  if (!condition) { failures.push(message); console.log('  FAIL ' + message); }
  else { console.log('  ok   ' + message); }
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

const srv = await startServer({ port: 0 });
const harnessUrl = `${srv.url}/tools/renderer-harness/index.html${atlasMode ? `?atlas=${atlasMode}` : ''}`;
console.log('server:', srv.url);
console.log('harness:', harnessUrl);

const browser = await chromium.launch({
  headless: !headed,
  args: ['--autoplay-policy=no-user-gesture-required', '--enable-unsafe-swiftshader']
});

let exitCode = 0;
try {
  const page = await browser.newPage({ viewport: { width: 1100, height: 820 } });
  page.on('console', (msg) => {
    const text = msg.text();
    const url = (msg.location() && msg.location().url) || '';
    if (msg.type() === 'error') {
      if (atlasMode === 'missing' && url.includes('does-not-exist')) { return; } // the simulated 404 itself
      consoleErrors.push(text + (url ? ` (${url})` : ''));
      console.log('  [console.error]', text, url);
    } else if (msg.type() === 'warning') { consoleWarnings.push(text); }
  });
  page.on('pageerror', (err) => { pageErrors.push(String(err)); console.log('  [pageerror]', String(err)); });
  page.on('requestfailed', (req) => failedRequests.push(`${req.method()} ${req.url()} ${req.failure()?.errorText}`));
  page.on('response', (res) => {
    if (res.status() >= 400 && !res.url().includes('does-not-exist')) { failedRequests.push(`${res.status()} ${res.url()}`); }
  });

  console.log('\n[1] load harness');
  await page.goto(harnessUrl, { waitUntil: 'load' });
  await page.waitForSelector('#phaser canvas', { timeout: 20000 });
  await page.waitForFunction(() => window.vl4000Stats && window.vl4000Stats().ready, null, { timeout: 20000 });
  await page.waitForFunction(() => window.vl4000Stats().frames > 30, null, { timeout: 20000 });
  const atlas = await page.evaluate(() => window.harness.atlas());
  notes.push(`atlas source: ${atlas.source} (${atlas.frameCount} frames)`);
  console.log('  atlas:', atlas.source, atlas.frameCount, 'frames');
  let stats = await page.evaluate(() => window.vl4000Stats());
  console.log('  stats:', JSON.stringify(stats));
  check(stats.ready === true, 'renderer reported Ready');
  check(stats.renderer === 'webgl' || stats.renderer === 'canvas', `renderer type is ${stats.renderer}`);
  check(stats.statics > 100, `static layer built (${stats.statics} objects)`);
  check(stats.sprites >= 5, `sprites reconciled (${stats.sprites})`);
  check(stats.labels >= 4, `labels reconciled (${stats.labels})`);
  check(stats.levelId === 1, `levelId is ${stats.levelId}`);
  check(stats.music === 'level', `music track is ${stats.music}`);
  check(stats.tickErrors === 0, 'no tick errors');
  await page.click('#stage', { position: { x: 10, y: 10 } }); // user gesture: unlocks audio
  await sleep(600);
  await page.screenshot({ path: path.join(outDir, '01-initial.png') });

  console.log('\n[2] keyboard input');
  await page.keyboard.down('ArrowRight');
  await sleep(120);
  let bits = await page.evaluate(() => window.getInputBits());
  check(bits === 2, `ArrowRight held -> bits ${bits}`);
  await page.keyboard.down('KeyW');
  await sleep(60);
  bits = await page.evaluate(() => window.getInputBits());
  check(bits === 6, `ArrowRight + KeyW held -> bits ${bits}`);
  await page.keyboard.up('KeyW');
  await sleep(1500);
  await page.keyboard.down('Space');     // a real press lasts several frames; press() would be sub-frame
  await sleep(150);
  await page.keyboard.up('Space');
  await sleep(120);
  await page.screenshot({ path: path.join(outDir, '02-running-jump.png') });
  await sleep(900);
  await page.keyboard.up('ArrowRight');
  await sleep(100);
  bits = await page.evaluate(() => window.getInputBits());
  check(bits === 0, `all released -> bits ${bits}`);
  const playerX = await page.evaluate(() => window.harness.sim.player.x);
  check(playerX > 160, `player moved right (x=${Math.round(playerX)})`);
  const camX = await page.evaluate(() => window.harness.sim.cam.x);
  check(camX > 2, `camera followed the player (cam.x=${Math.round(camX)})`);
  await page.evaluate(() => window.setTouchButton(1, true));
  await sleep(50);
  bits = await page.evaluate(() => window.getInputBits());
  check(bits === 1, `touch left -> bits ${bits}`);
  await page.evaluate(() => window.setTouchButton(1, false));

  console.log('\n[3] one-shot key forwarding');
  await page.evaluate(() => {
    window.__received = [];
    window.captureKeyDown({ invokeMethodAsync: (method, code) => { window.__received.push(method + ':' + code); return Promise.resolve(); } });
  });
  await page.keyboard.press('KeyP');
  await page.keyboard.press('Enter');
  await page.keyboard.press('KeyM');
  await page.keyboard.press('KeyR');
  await page.keyboard.press('KeyN');
  await page.keyboard.press('Escape');
  await page.keyboard.press('KeyZ'); // not a game key
  await sleep(150);
  const received = await page.evaluate(() => window.__received);
  check(JSON.stringify(received) === JSON.stringify([
    'OnKeyDownAsync:KeyP', 'OnKeyDownAsync:Enter', 'OnKeyDownAsync:KeyM', 'OnKeyDownAsync:KeyR', 'OnKeyDownAsync:KeyN', 'OnKeyDownAsync:Escape'
  ]), `one-shot keys forwarded: ${received.join(' ')}`);
  await page.evaluate(() => window.releaseKeyDown());

  console.log('\n[4] flash + shake + camera');
  await page.click('#btn-flash');
  await page.click('#btn-shake');
  await sleep(90);
  await page.screenshot({ path: path.join(outDir, '03-flash-shake.png') });
  const shakeInfo = await page.evaluate(() => ({ flash: window.harness.sim.flash, shake: window.harness.sim.shake }));
  check(shakeInfo.flash > 0 && shakeInfo.shake > 0, `flash ${shakeInfo.flash.toFixed(2)} shake ${shakeInfo.shake.toFixed(2)} active`);
  await sleep(1200);

  console.log('\n[5] reduced motion');
  await page.click('#btn-motion');
  await page.click('#btn-shake');
  await page.click('#btn-flash');
  await sleep(120);
  stats = await page.evaluate(() => window.vl4000Stats());
  check(stats.reducedMotion === true, 'reduced motion flag set');
  await page.screenshot({ path: path.join(outDir, '04-reduced-motion.png') });
  await page.click('#btn-motion');
  await sleep(1000);

  console.log('\n[6] static layer rebuild (themes)');
  await page.click('#btn-theme');
  await sleep(400);
  stats = await page.evaluate(() => window.vl4000Stats());
  check(stats.levelId === 2, `arena layer built (levelId ${stats.levelId})`);
  await page.screenshot({ path: path.join(outDir, '05-theme-arena.png') });
  await page.click('#btn-theme');
  await sleep(400);
  stats = await page.evaluate(() => window.vl4000Stats());
  check(stats.levelId === 3, `title layer built (levelId ${stats.levelId})`);
  await page.screenshot({ path: path.join(outDir, '06-theme-title.png') });
  await page.click('#btn-theme');
  await sleep(300);

  console.log('\n[7] pause / resume');
  await page.click('#btn-pause');
  await sleep(150);
  const framesA = await page.evaluate(() => window.vl4000Stats().frames);
  await sleep(500);
  const framesB = await page.evaluate(() => window.vl4000Stats().frames);
  check(framesA === framesB, `paused: no ticks (${framesA} -> ${framesB})`);
  await page.click('#btn-pause');
  await sleep(500);
  const framesC = await page.evaluate(() => window.vl4000Stats().frames);
  check(framesC > framesB + 10, `resumed: ticks advance (${framesB} -> ${framesC})`);

  console.log('\n[8] audio');
  const audio0 = await page.evaluate(() => window.vl4000Audio.state());
  console.log('  audio state:', JSON.stringify(audio0));
  check(audio0.available === true, 'WebAudio available');
  check(audio0.track === 'level', `music track requested: ${audio0.track}`);
  if (audio0.context === 'running') {
    check(audio0.playing === 'level', `music scheduler running (${audio0.playing}, step ${audio0.step})`);
  } else {
    notes.push(`audio context state in headless: ${audio0.context} (music starts on first gesture)`);
  }
  await page.selectOption('#sel-music', 'boss');
  await sleep(300);
  const audio1 = await page.evaluate(() => window.vl4000Audio.state());
  check(audio1.track === 'boss', `setMusic via snapshot change -> ${audio1.track}`);
  const cues = await page.evaluate(() => window.vl4000Audio.cues());
  const expectedCues = ['boot', 'menu_move', 'menu_select', 'start', 'jump', 'land', 'token', 'coffee', 'powerup', 'powerdown', 'hurt',
    'death', 'stomp', 'terminal_ok', 'terminal_denied', 'checkpoint', 'exit_open', 'exit_enter', 'boss_appear', 'boss_shoot', 'boss_charge',
    'boss_hit', 'boss_stun', 'boss_defeat', 'wave', 'arena_shift', 'rollback', 'warning', 'victory', 'gameover', 'pause'];
  const missingCues = expectedCues.filter((c) => !cues.includes(c));
  check(missingCues.length === 0, `all ${expectedCues.length} SoundCue names implemented${missingCues.length ? ' (missing: ' + missingCues.join(',') + ')' : ''}`);
  await page.evaluate(async (names) => {
    for (const n of names) { window.playSound(n); await new Promise((r) => setTimeout(r, 40)); }
    window.playSound('definitely_not_a_cue');
    window.setMusic('nope');
    window.setMusic('victory');
    window.setMusic(null);
  }, cues);
  await sleep(200);
  // Output metering: the synth must actually produce signal at a modest level, and mute must be silent.
  const sampleMeter = async (ms) => page.evaluate(async (total) => {
    let peak = 0, rms = 0, n = 0;
    const end = performance.now() + total;
    while (performance.now() < end) {
      const m = window.vl4000Audio.meter();
      peak = Math.max(peak, m.peak); rms += m.rms; n++;
      await new Promise((r) => setTimeout(r, 40));
    }
    return { peak, rms: n ? rms / n : 0 };
  }, ms);
  await page.selectOption('#sel-music', 'boss');
  await sleep(400);
  const musicLevel = await sampleMeter(700);
  console.log('  music level:', JSON.stringify(musicLevel));
  if (audio0.context === 'running') {
    check(musicLevel.peak > 0.005, `music is audible (peak ${musicLevel.peak.toFixed(3)})`);
    check(musicLevel.peak < 0.9, `music does not clip (peak ${musicLevel.peak.toFixed(3)})`);
  }
  await page.selectOption('#sel-music', '');
  await sleep(600);
  const silence = await sampleMeter(300);
  if (audio0.context === 'running') { check(silence.peak < 0.01, `setMusic(null) goes quiet (peak ${silence.peak.toFixed(4)})`); }
  await page.evaluate(() => window.playSound('victory'));
  const cueLevel = await sampleMeter(500);
  if (audio0.context === 'running') {
    check(cueLevel.peak > 0.005 && cueLevel.peak < 0.9, `cue is audible without clipping (peak ${cueLevel.peak.toFixed(3)})`);
  }
  await page.evaluate(() => window.setSoundEnabled(false));
  await page.selectOption('#sel-music', 'level');
  await page.evaluate(() => window.playSound('boss_defeat'));
  await sleep(400);
  const muted = await sampleMeter(300);
  if (audio0.context === 'running') { check(muted.peak < 0.002, `setSoundEnabled(false) is silent (peak ${muted.peak.toFixed(4)})`); }
  await page.evaluate(() => window.setSoundEnabled(true));
  await sleep(500);
  const unmuted = await page.evaluate(() => window.vl4000Audio.state());
  check(unmuted.playing === 'level' || audio0.context !== 'running', `music resumes after unmute (${unmuted.playing})`);

  console.log('\n[9] fallback texture for missing frames');
  stats = await page.evaluate(() => window.vl4000Stats());
  console.log('  missing frames:', stats.missingFrames.join(', ') || '(none)');
  check(stats.missingFrames.includes('debug/missing/0'), 'unknown frame fell back to the generated texture (warned once)');
  if (atlas.source === 'placeholder') {
    check(stats.missingFrames.some((f) => f.startsWith('coffee/idle/')), 'coffee frame fell back to the generated texture');
  } else if (atlas.source === 'missing') {
    check(stats.atlas === false && stats.missingFrames.length > 10, 'every frame fell back without errors');
  } else {
    const realMissing = stats.missingFrames.filter((f) => f !== 'debug/missing/0');
    notes.push(`real atlas missing frames: ${realMissing.join(', ') || 'none'}`);
  }

  console.log('\n[10] destroy + restart (no duplicate loops)');
  await page.click('#btn-restart');
  await page.waitForFunction(() => window.harness.tickRef.readyCount === 2 && window.vl4000Stats().frames > 5, null, { timeout: 20000 });
  const canvases = await page.evaluate(() => document.querySelectorAll('#phaser canvas').length);
  check(canvases === 1, `exactly one canvas after restart (${canvases})`);
  const readyCount = await page.evaluate(() => window.harness.tickRef.readyCount);
  check(readyCount === 2, `Ready invoked once per start (${readyCount})`);
  const f0 = await page.evaluate(() => window.vl4000Stats().frames);
  await sleep(1000);
  const f1 = await page.evaluate(() => window.vl4000Stats().frames);
  const tps = f1 - f0;
  check(tps > 20 && tps < 130, `ticks per second after restart: ${tps} (one loop)`);
  stats = await page.evaluate(() => window.vl4000Stats());
  const simLevelId = await page.evaluate(() => window.harness.sim.levelId);
  check(stats.levelId === simLevelId && stats.statics > 100, `level re-sent after restart (levelId ${stats.levelId}, ${stats.statics} statics)`);
  check(stats.music === 'level', `music re-applied after restart (${stats.music})`);
  await page.keyboard.down('ArrowRight');
  await sleep(1500);
  await page.keyboard.down('Space');
  await sleep(150);
  await page.keyboard.up('Space');
  await sleep(200);
  await page.screenshot({ path: path.join(outDir, '07-after-restart.png') });
  await page.keyboard.up('ArrowRight');

  console.log('\n[11] performance');
  await sleep(1500);
  stats = await page.evaluate(() => window.vl4000Stats());
  notes.push(`fps ${stats.fps}, tickMs ${stats.tickMs} (max ${stats.tickMsMax}), renderer ${stats.renderer}`);
  console.log('  fps', stats.fps, 'tickMs', stats.tickMs, 'max', stats.tickMsMax);
  check(stats.fps > 20, `fps ${stats.fps}`);
  check(stats.tickMs < 8, `tick ${stats.tickMs} ms`);
  check(stats.tickErrors === 0, 'still no tick errors');

  console.log('\n[12] destroy');
  await page.click('#btn-destroy');
  await sleep(500);
  const afterDestroy = await page.evaluate(() => ({ canvases: document.querySelectorAll('#phaser canvas').length, stats: window.vl4000Stats(), audio: window.vl4000Audio.state() }));
  check(afterDestroy.canvases === 0, `canvas removed on destroy (${afterDestroy.canvases})`);
  check(afterDestroy.stats.ready === false && afterDestroy.stats.frames === 0, 'stats reset after destroy');
  check(afterDestroy.audio.track === null, 'music stopped on destroy');
  await page.screenshot({ path: path.join(outDir, '08-destroyed.png') });

  // Larger view of the canvas only, for pixel inspection.
  await page.click('#btn-restart');
  await page.waitForFunction(() => window.vl4000Stats().ready && window.vl4000Stats().frames > 30, null, { timeout: 20000 });
  await sleep(400);
  const canvas = await page.$('#phaser canvas');
  await canvas.screenshot({ path: path.join(outDir, '09-canvas-only.png') });

  console.log('\n[13] console');
  const expectedWarning = (w) =>
    w.includes('[vl4000 renderer] atlas frame') ||            // deliberate missing frame
    w.includes('[vl4000 renderer] failed to load') ||          // --atlas missing mode
    w.includes('[vl4000 renderer] atlas "default" unavailable') ||
    w.includes('definitely_not_a_cue') || w.includes('unknown music track "nope"') ||   // deliberate bad names
    w.includes('willReadFrequently');                          // Chrome perf hint about Phaser's text measuring
  const unexpectedWarnings = consoleWarnings.filter((w) => !expectedWarning(w));
  check(consoleErrors.length === 0, `console errors: ${consoleErrors.length}`);
  check(pageErrors.length === 0, `page errors: ${pageErrors.length}`);
  check(failedRequests.length === 0, `failed requests: ${failedRequests.length}${failedRequests.length ? ' ' + failedRequests.join(' | ') : ''}`);
  if (unexpectedWarnings.length) {
    console.log('  warnings:', unexpectedWarnings.join(' | '));
    notes.push(`warnings: ${unexpectedWarnings.join(' | ')}`);
  }
} catch (err) {
  failures.push('exception: ' + (err && err.stack || err));
  console.error(err);
} finally {
  await browser.close();
  await srv.close();
}

const summary = { ok: failures.length === 0, failures, notes, consoleErrors, pageErrors, failedRequests, consoleWarnings };
fs.writeFileSync(path.join(outDir, 'summary.json'), JSON.stringify(summary, null, 2));
console.log('\n' + (summary.ok ? 'PASS' : 'FAIL') + ' — ' + failures.length + ' failure(s); notes: ' + notes.join('; '));
console.log('screenshots in', outDir);
if (!summary.ok) { exitCode = 1; }
process.exit(exitCode);
