/* VIDEOLAND 4000 — keyboard / touch input (docs/ARCHITECTURE.md §3).
 *
 * Held gameplay buttons are tracked as a bit mask that the renderer samples every
 * frame via getInputBits(). One-shot UI keys are forwarded to Blazor through the
 * DotNetObjectReference registered with captureKeyDown(ref):
 *     ref.invokeMethodAsync('OnKeyDownAsync', event.code)
 *
 *   Bit  Name      Keys
 *     1  Left      ArrowLeft, KeyA
 *     2  Right     ArrowRight, KeyD
 *     4  Jump      Space, ArrowUp, KeyW
 *     8  Interact  KeyE
 *    16  Down      ArrowDown, KeyS
 *
 * Globals: captureKeyDown(dotnetRef), releaseKeyDown(), getInputBits(), setTouchButton(bit, down)
 * Extras for tooling: window.vl4000Input = { heldCodes(), touchBits(), reset() }
 */
(function () {
  'use strict';

  if (window.__vl4000KeyboardLoaded) { return; }
  window.__vl4000KeyboardLoaded = true;

  var BITS = {
    ArrowLeft: 1, KeyA: 1,
    ArrowRight: 2, KeyD: 2,
    Space: 4, ArrowUp: 4, KeyW: 4,
    KeyE: 8,
    ArrowDown: 16, KeyS: 16
  };

  var ONE_SHOT = { Enter: true, NumpadEnter: true, Space: true, KeyP: true, Escape: true, KeyR: true, KeyM: true, KeyN: true };

  var held = {};          // code -> true while physically held
  var heldMask = 0;
  var touchMask = 0;
  var dotnetRef = null;

  function isTypingTarget(target) {
    if (!target || target === window || target === document) { return false; }
    var tag = target.tagName;
    if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') { return true; }
    return !!target.isContentEditable;
  }

  // A focused button/link must keep its native Enter/Space activation (keyboard accessibility):
  // the browser's click then reaches Blazor, so the key is neither forwarded nor cancelled here.
  function isActivatableTarget(target, code) {
    if (code !== 'Enter' && code !== 'Space' && code !== 'NumpadEnter') { return false; }
    if (!target || target === window || target === document) { return false; }
    // Only real buttons and links activate natively; a focused div with role="button" does not.
    var tag = target.tagName;
    return tag === 'BUTTON' || (tag === 'A' && target.hasAttribute('href'));
  }

  function recomputeMask() {
    var mask = 0;
    for (var code in held) {
      if (held[code]) { mask |= BITS[code]; }
    }
    heldMask = mask;
  }

  function onKeyDown(event) {
    if (event.metaKey || event.ctrlKey || event.altKey) { return; } // keep browser shortcuts (Cmd+R, ...)
    if (isTypingTarget(event.target)) { return; }

    var code = event.code;
    if (isActivatableTarget(event.target, code)) { return; }
    var bit = BITS[code];
    var oneShot = ONE_SHOT[code];
    if (!bit && !oneShot) { return; }

    if (bit) {
      if (!held[code]) {
        held[code] = true;
        recomputeMask();
      }
    }

    if (oneShot && !event.repeat && dotnetRef) {
      try {
        var result = dotnetRef.invokeMethodAsync('OnKeyDownAsync', code);
        if (result && typeof result.catch === 'function') { result.catch(function () { /* page navigating */ }); }
      } catch (e) { /* reference disposed */ }
    }

    event.preventDefault();
  }

  function onKeyUp(event) {
    var code = event.code;
    if (held[code]) {
      delete held[code];
      recomputeMask();
    }
    if ((BITS[code] || ONE_SHOT[code]) && !isTypingTarget(event.target) && !isActivatableTarget(event.target, code) &&
        !event.metaKey && !event.ctrlKey && !event.altKey) {
      event.preventDefault();
    }
  }

  function releaseAll() {
    held = {};
    heldMask = 0;
  }

  window.addEventListener('keydown', onKeyDown);
  window.addEventListener('keyup', onKeyUp);
  window.addEventListener('blur', releaseAll);
  window.addEventListener('pagehide', releaseAll);
  document.addEventListener('visibilitychange', function () {
    if (document.hidden) { releaseAll(); }
  });

  window.captureKeyDown = function (ref) {
    dotnetRef = ref || null;
  };

  window.releaseKeyDown = function () {
    dotnetRef = null;
  };

  window.getInputBits = function () {
    return heldMask | touchMask;
  };

  window.setTouchButton = function (bit, down) {
    bit = bit | 0;
    if (down) {
      touchMask |= bit;
    } else {
      touchMask &= ~bit;
    }
  };

  window.vl4000Input = {
    heldCodes: function () { return Object.keys(held); },
    touchBits: function () { return touchMask; },
    reset: function () { releaseAll(); touchMask = 0; }
  };
})();
