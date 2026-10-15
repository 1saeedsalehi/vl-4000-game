#!/usr/bin/env node
// Browser smoke test for VIDEOLAND 4000.
//
// Boots the real Blazor WebAssembly app in headless Chromium and plays through the whole game:
// boot screen → title → level intro → gameplay (movement, token pickup, jump) → pause/resume →
// terminal → checkpoint → exit → boss fight (repair-terminal mechanic, all phases) → victory →
// main menu → game over → retry. Uses the ?dev debug hook (window.vl4000Debug) only to move the
// player around; every interaction still goes through the real game mechanics.
//
// Usage:  node smoke.mjs            (starts `dotnet run` on http://localhost:5237 unless GAME_URL is set)
//         GAME_URL=http://localhost:5237/ node smoke.mjs
// Screenshots: tests/e2e/output/smoke/*.png. Exit code 1 when any check fails.

import { spawn } from 'node:child_process';
import { mkdirSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..');
const out = path.join(here, 'output', 'smoke');
mkdirSync(out, { recursive: true });

const port = process.env.PORT || '5237';
let baseUrl = process.env.GAME_URL || `http://localhost:${port}/`;
if (!baseUrl.endsWith('/')) baseUrl += '/';

const failures = [];
const check = (name, condition, detail = '') => {
  console.log(`${condition ? 'PASS' : 'FAIL'}  ${name}${detail ? `  (${detail})` : ''}`);
  if (!condition) failures.push(name);
};

async function isUp(url) {
  try {
    const response = await fetch(url);
    return response.ok;
  } catch {
    return false;
  }
}

let server = null;
if (!(await isUp(baseUrl))) {
  console.log(`Starting dev server on ${baseUrl} ...`);
  server = spawn('dotnet', ['run', '--project', path.join(repo, 'src', 'BlazorUI', 'BlazorUI.csproj'), '--', '--urls', baseUrl], {
    cwd: repo,
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  server.stderr.on('data', (d) => process.stderr.write(d));
  const deadline = Date.now() + 240_000;
  while (Date.now() < deadline && !(await isUp(baseUrl))) {
    await new Promise((r) => setTimeout(r, 1000));
  }
  if (!(await isUp(baseUrl))) {
    console.error('Dev server did not start in time');
    server.kill();
    process.exit(1);
  }
}

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
const consoleErrors = [];
page.on('console', (m) => { if (m.type() === 'error') consoleErrors.push(m.text()); });
page.on('pageerror', (e) => consoleErrors.push(`[pageerror] ${e.message}`));

const wait = (ms) => page.waitForTimeout(ms);
const bodyText = async () => (await page.locator('body').innerText()).replace(/\s+/g, ' ');
const shot = (name) => page.screenshot({ path: path.join(out, `${name}.png`) });
const debug = (command) => page.evaluate((c) => window.vl4000Debug(c), command);
const state = async () => {
  const s = await debug('state');
  const m = Object.fromEntries([...s.matchAll(/(\w+)=([^\s]+)/g)].map((x) => [x[1], x[2]]));
  m.phase = s.split(' ')[0];
  const pos = /player=\((-?\d+),(-?\d+)\)/.exec(s);
  m.x = pos ? Number(pos[1]) : NaN;
  m.y = pos ? Number(pos[2]) : NaN;
  return m;
};
const tap = async (code, holdMs = 60) => { await page.keyboard.down(code); await wait(holdMs); await page.keyboard.up(code); };
const waitFor = async (predicate, timeoutMs, label) => {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (await predicate()) return true;
    await wait(100);
  }
  console.log(`  timeout waiting for ${label}`);
  return false;
};

try {
  // ---- boot ---------------------------------------------------------------------------------
  await page.goto(`${baseUrl}?dev=1`, { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('#phaser canvas', { timeout: 120_000 });
  await wait(500);
  check('boot screen shows the fictional self-test', (await bodyText()).includes('VIDEOLAND SYSTEMS'));
  await shot('01-boot');

  await tap('Enter');
  check('Enter skips the boot into the title screen (only one screen)', await waitFor(async () => (await bodyText()).includes('[ PRESS START ]'), 3000, 'title'));
  const title = await bodyText();
  check('title shows the high score and controls', title.includes('HIGH SCORE') && title.includes('INTERACT'));
  await shot('02-title');

  // ---- start --------------------------------------------------------------------------------
  await tap('Enter');
  check('Start opens the level intro', await waitFor(async () => (await bodyText()).includes('THE LOGIN DUNGEON'), 3000, 'intro'));
  await shot('03-intro');
  await tap('Enter');
  check('intro can be skipped into gameplay', await waitFor(async () => (await bodyText()).includes('TOKENS: 00 / 05'), 3000, 'hud'));
  const start = await state();
  check('session is Playing', start.phase === 'Playing', start.phase);

  // ---- movement, pickup, jump -----------------------------------------------------------------
  await page.keyboard.down('ArrowRight');
  const picked = await waitFor(async () => (await state()).score !== '0', 2500, 'first token');
  await page.keyboard.up('ArrowRight');
  check('walking right collects the first token (score > 0)', picked);
  const afterWalk = await state();
  check('player moved right', afterWalk.x > 60, afterWalk.player);
  await shot('04-playing');

  const groundY = afterWalk.y;
  await page.keyboard.down('Space');
  await wait(120);
  const midJump = await state();
  await page.keyboard.up('Space');
  check('jump lifts the player', midJump.y < groundY - 8, `${groundY} -> ${midJump.y}`);
  await wait(900);

  // ---- pause / resume --------------------------------------------------------------------------
  await tap('KeyP');
  check('P pauses the game', await waitFor(async () => (await bodyText()).includes('PAUSED'), 2000, 'pause'));
  const pausedState = await state();
  await page.keyboard.down('ArrowRight'); await wait(500); await page.keyboard.up('ArrowRight');
  const stillPaused = await state();
  check('world does not advance while paused', pausedState.player === stillPaused.player && stillPaused.phase === 'Paused');
  await shot('05-paused');
  await tap('KeyP');
  check('P resumes', await waitFor(async () => (await state()).phase === 'Playing', 2000, 'resume'));

  // ---- objectives ------------------------------------------------------------------------------
  await debug('tokens');
  await wait(200);
  check('HUD objective switches to the terminal', (await bodyText()).includes('RESTORE AUTHENTICATION'));
  await debug('terminal');
  await wait(150);
  await tap('KeyE');
  check('terminal activates with enough tokens', await waitFor(async () => (await state()).terminal === 'True', 2000, 'terminal'));
  const afterTerminal = await state();
  check('objective score awarded once (5 tokens + terminal = 1000)', afterTerminal.score === '1000', afterTerminal.score);
  check('exit unlocked by the terminal', afterTerminal.exit === 'True');
  await shot('06-terminal');

  await debug('checkpoint');
  check('checkpoint activates on contact', await waitFor(async () => (await state()).checkpoint === 'True', 2000, 'checkpoint'));
  await wait(200);
  check('checkpoint awards 250', (await state()).score === '1250', (await state()).score);

  await debug('exit');
  await page.keyboard.down('ArrowRight'); await wait(300); await page.keyboard.up('ArrowRight');
  check('entering the exit starts the boss encounter', await waitFor(async () => (await state()).phase === 'BossEncounter', 3000, 'boss'));
  check('prime time warning is shown', (await bodyText()).includes('PRIME TIME TRAFFIC DETECTED'));
  await shot('07-boss-intro');

  // ---- boss fight ------------------------------------------------------------------------------
  const phasesSeen = new Set();
  let bossOk = true;
  for (let hit = 0; hit < 4 && bossOk; hit++) {
    bossOk = await waitFor(async () => (await state()).boss?.startsWith('Attacking'), 8000, 'boss attacking');
    if (!bossOk) break;
    const bossInfo = (await state()).boss.split('/');
    phasesSeen.add(bossInfo[1]);
    await debug('heal');
    await debug('repair');
    await wait(150);
    await tap('KeyE');
    bossOk = await waitFor(async () => (await state()).boss?.startsWith('Stunned'), 2000, 'stun');
    if (!bossOk) break;
    await wait(600);
    await debug('boss-touch');
    bossOk = await waitFor(async () => (await state()).boss?.startsWith('HitReact'), 2000, 'hit');
    if (hit === 0) await shot('08-boss-hit');
  }
  check('boss can be hurt only through the repair-terminal mechanic (4 cycles)', bossOk);
  check('boss health progressed through all three phases', phasesSeen.size >= 3 || (await state()).phase === 'Victory', [...phasesSeen].join(','));
  check('defeating the boss leads to victory', await waitFor(async () => (await bodyText()).includes('ALL STREAMS OPERATIONAL'), 6000, 'victory'));
  const victory = await bodyText();
  check('victory shows final score and deployment message', victory.includes('FINAL SCORE: 003250') && victory.includes('DEPLOYMENT SUCCESSFUL'), victory.slice(0, 120));
  await shot('09-victory');
  const stored = await page.evaluate(() => localStorage.getItem('vl4000.highscore'));
  check('high score persisted to localStorage', stored === '3250', String(stored));

  // ---- menu, game over, retry -----------------------------------------------------------------
  await tap('KeyM');
  check('M returns to the main menu with the new high score', await waitFor(async () => (await bodyText()).includes('HIGH SCORE: 003250'), 3000, 'menu'));
  await tap('Enter'); await wait(400); await tap('Enter');
  check('a new game starts clean', await waitFor(async () => (await state()).phase === 'Playing' && (await state()).score === '0', 3000, 'new game'));
  await debug('hurt'); await wait(80); await debug('hurt'); await wait(80); await debug('hurt');
  check('losing all health shows the game-over screen', await waitFor(async () => (await bodyText()).includes('PRODUCTION INCIDENT'), 5000, 'game over'));
  await shot('10-gameover');
  await tap('KeyR');
  check('R retries into a fresh run', await waitFor(async () => (await state()).phase === 'Playing', 3000, 'retry'));

  // ---- health -----------------------------------------------------------------------------------
  const stats = await page.evaluate(() => window.vl4000Stats());
  console.log('renderer stats:', JSON.stringify(stats));
  check('renderer runs at a healthy frame rate', stats.fps > 30, `${stats.fps} fps`);
  check('no simulation tick errors', stats.tickErrors === 0);
  check('no missing atlas frames', stats.missingFrames.length === 0, stats.missingFrames.join(','));
  check('no console or page errors', consoleErrors.length === 0, consoleErrors.slice(0, 3).join(' | '));
} catch (error) {
  console.error(error);
  failures.push(`exception: ${error.message}`);
  await shot('99-exception').catch(() => {});
} finally {
  await browser.close();
  if (server) server.kill('SIGTERM');
}

console.log(failures.length === 0 ? '\nSMOKE TEST PASSED' : `\nSMOKE TEST FAILED (${failures.length}): ${failures.join('; ')}`);
process.exit(failures.length === 0 ? 0 : 1);
