"""World frames: pickups, terminals, checkpoint, exit door, tiles, decor and
parallax backgrounds. All artwork is original, authored as text pixel-maps
using the shared palette (see palette.py / docs/ARCHITECTURE.md section 7).

Style notes (shared with frames_characters):
* 1 px black ('K') outline around props/items; tiles and backgrounds have none.
* Props are bottom-aligned (origin bottom-centre at runtime).
* Shading uses 2-3 tones per material; magenta/pink are accents, cyan/green/
  yellow are "good", red is "error/locked".
"""

import random

from pixelmap import blank, blit, flip_h, frame, line, rect, replace, set_px, shift

FRAMES = {}

# ---------------------------------------------------------------------------
# token/spin/0..3 (16x16): glowing keycard flipping around its vertical axis.
# ---------------------------------------------------------------------------

_TOKEN_FULL = frame("""
................
................
......Y.........
..KKKKKKKKKKKK..
..KWYYYYYYYYYK..
..KYccccccccYK..
..KYCCCCCCCCYK..
..KYYYYYYYYYyK..
..KYWWYYYYYYyK..
..KYWlYYYYYYyK..
..KYYYYYYYYYyK..
..KyyyyyyyyyyK..
..KKKKKKKKKKKK..
..............Y.
................
................
""")

_TOKEN_3Q = frame("""
................
................
................
....KKKKKKKK....
....KYYYYYYK....
....KYccccYK....
....KYCCCCYK....
....KYYYYYyK....
....KYWYYYyK....
....KYlYYYyK....
....KYYYYYyK....
....KyyyyyyK....
....KKKKKKKK....
................
.............Y..
................
""")

_TOKEN_EDGE = frame("""
................
................
.....Y..........
......KYYK......
......KYYK......
......KccK......
......KCCK......
......KYyK......
......KYyK......
......KYyK......
......KYyK......
......KyyK......
......KKKK......
................
................
.........Y......
""")

FRAMES["token/spin/0"] = _TOKEN_FULL
FRAMES["token/spin/1"] = _TOKEN_3Q
FRAMES["token/spin/2"] = _TOKEN_EDGE
_token3 = flip_h(_TOKEN_3Q)
set_px(_token3, 2, 14, ".")
set_px(_token3, 1, 2, "Y")
FRAMES["token/spin/3"] = _token3

# ---------------------------------------------------------------------------
# coffee/idle/0..1 (16x16): white cup, brown coffee, wiggling steam.
# ---------------------------------------------------------------------------

_CUP = frame("""
................
................
................
................
................
...KKKKKKKK.....
...KbBBBBBK.....
...KWWWWWlKKKK..
...KWWWWWlK.KlK.
...KWWWWWlK.KlK.
...KWWWWWlKKKK..
...KWWWWWlK.....
...KlWWWllK.....
...KKKKKKKK.....
..KllllllllK....
...KKKKKKKK.....
""")

_STEAM_0 = frame("""
................
.....l...l......
.....l...l......
......l.l.......
......l.l.......
""")

_STEAM_1 = frame("""
................
......l.l.......
......l.l.......
.....l...l......
.....l...l......
""")

FRAMES["coffee/idle/0"] = blit([r for r in _CUP], _STEAM_0, 0, 0)
FRAMES["coffee/idle/1"] = blit([r for r in _CUP], _STEAM_1, 0, 0)

# ---------------------------------------------------------------------------
# cache/idle/0..1 (16x16): glowing cyan chip, glow pulses ('q' = half cyan).
# ---------------------------------------------------------------------------

_CHIP = frame("""
................
................
................
................
................
...KKKKKKKKKK...
...KCCCCCCCCK...
.llKCWcccccCKll.
.KKKCccccccCKKK.
...KCcCCCCcCK...
.llKCcCccCcCKll.
.KKKCcCCCCcCKKK.
...KCccccccCK...
...KCCCCCCCCK...
...KKKKKKKKKK...
................
""")

_GLOW_0 = frame("""
................
................
................
................
......q..q......
..q..........q..
................
................
................
................
................
................
................
................
..q..........q..
.......qq.......
""")

_GLOW_1 = frame("""
................
................
................
....q......q....
...q.q....q.q...
.q............q.
q..............q
................
................
................
q..............q
.q............q.
................
...q........q...
.q............q.
.....qqqqqq.....
""")

FRAMES["cache/idle/0"] = blit([r for r in _CHIP], _GLOW_0, 0, 0)
_chip_bright = replace(_CHIP, {"C": "c"})
for _x in (7, 8):
    set_px(_chip_bright, _x, 10, "W")
_chip_bright = blit(_chip_bright, _GLOW_1, 0, 0)
FRAMES["cache/idle/1"] = _chip_bright

# ---------------------------------------------------------------------------
# breaker/idle/0..1 (16x16): grey switch box, green/red lever, yellow spark.
# ---------------------------------------------------------------------------

_BREAKER = frame("""
................
................
................
...KKKKKKKKKK...
...KlllllllgK...
...KldgggggdK...
...KlgKKKKggK...
...KlgKGGKggK...
...KlgKWWKggK...
...KlgKllKggK...
...KlgKrrKggK...
...KlgKrrKggK...
...KlgKKKKggK...
...KldgggggdK...
...KdddddddgK...
...KKKKKKKKKK...
""")

_SPARK_0 = frame("""
............Y...
...........YW...
..........YY....
""")

_SPARK_1 = frame("""
................
..........Y.....
...........YY...
""")

FRAMES["breaker/idle/0"] = blit([r for r in _BREAKER], _SPARK_0, 0, 0)
FRAMES["breaker/idle/1"] = blit([r for r in _BREAKER], _SPARK_1, 0, 0)

# ---------------------------------------------------------------------------
# rollback/idle/0..1 (16x16): curved orange "undo" arrow, pulsing.
# ---------------------------------------------------------------------------

_ROLLBACK = frame("""
................
................
.....KKKKKK.....
....KYYYYYYK....
...KYOOOOOOYK...
..KYOKKKKKKOOK..
..KOOK....KOOK..
..KOOK....KOOK..
KKKOOKKK..KOOK..
KOOOOOOOK.KOOK..
.KOOOOOK..KOOK..
..KOOOK...KOOK..
...KOK...KKOOK..
....K....KOOyK..
.........KKKKK..
................
""")

FRAMES["rollback/idle/0"] = _ROLLBACK
_rb1 = replace(_ROLLBACK, {"Y": "W"})
for _x, _y in ((7, 0), (14, 4), (1, 6)):
    set_px(_rb1, _x, _y, "Y")
FRAMES["rollback/idle/1"] = _rb1

# ---------------------------------------------------------------------------
# Terminals (16x32): auth terminal (dark cabinet) and repair terminal (purple
# cabinet). The screen interior is 8x10 at x=4..11, y=4..13.
# ---------------------------------------------------------------------------

_AUTH_CABINET = frame("""
.KKKKKKKKKKKKKK.
.KuuuuuuuuuuuuK.
.KuddddddddddnK.
.KuKKKKKKKKKKnK.
.KuK........KnK.
.KuK........KnK.
.KuK........KnK.
.KuK........KnK.
.KuK........KnK.
.KuK........KnK.
.KuK........KnK.
.KuK........KnK.
.KuK........KnK.
.KuK........KnK.
.KuKKKKKKKKKKnK.
.KuddddddddddnK.
.KulglglglgldnK.
.KuKKKKKKKKKKnK.
.KuddddddddddnK.
.KuddKKKKKKcdnK.
.KuddddddddddnK.
.KuddddddddddnK.
.KunnnnnnnnnnnK.
.KuddddddddddnK.
.KunnnnnnnnnnnK.
.KuddddddddddnK.
.KunnnnnnnnnnnK.
.KuddddddddddnK.
.KuddddddddddnK.
.KuddddddddddnK.
.KnnnnnnnnnnnnK.
.KKKKKKKKKKKKKK.
""")

# repair terminals share the cabinet shape but are purple/violet
_REPAIR_CABINET = replace(_AUTH_CABINET, {"d": "p", "u": "v"})


def _screen(bg, art):
    """8x10 screen: `art` rows use '.' for background pixels."""
    rows = frame(art)
    out = []
    for y in range(10):
        row = rows[y] if y < len(rows) else "." * 8
        out.append("".join(bg if ch == "." else ch for ch in row.ljust(8, ".")))
    return out


def _with_screen(cabinet, screen_rows):
    return blit([r for r in cabinet], screen_rows, 4, 4)


_X_MARK = """
........
........
.R....R.
..R..R..
...RR...
...RR...
..R..R..
.R....R.
........
........
"""

_CHECK_MARK = """
........
........
.......G
......G.
.G...G..
..G.G...
...G....
........
........
........
"""

# locked: dark red screen, X blinks bright / dim
FRAMES["terminal/locked/0"] = _with_screen(_AUTH_CABINET, _screen("r", _X_MARK))
FRAMES["terminal/locked/1"] = _with_screen(
    _AUTH_CABINET, _screen("n", _X_MARK.replace("R", "r")))

# active: dark green screen, check mark, a bright scanline moving down
_active0 = _screen("e", _CHECK_MARK)
_active1 = _screen("e", _CHECK_MARK)
_active0[1] = "".join("W" if ch == "G" else "G" for ch in _active0[1])
_active1[7] = "".join("W" if ch == "G" else "G" for ch in _active1[7])
FRAMES["terminal/active/0"] = _with_screen(_AUTH_CABINET, _active0)
FRAMES["terminal/active/1"] = _with_screen(_AUTH_CABINET, _active1)

# denied: bright red screen with a white X
FRAMES["terminal/denied/0"] = _with_screen(
    _AUTH_CABINET, _screen("R", _X_MARK.replace("R", "W")))

# repair idle: blue screen with code lines and a blinking cursor
_CODE = """
........
.ccc.c..
.c.cc...
........
.cccc.c.
.c......
........
.cc.ccc.
........
........
"""
_repair0 = _screen("C", _CODE)
_repair1 = _screen("C", _CODE)
_repair0[8] = "C" + "W" + "C" * 6
FRAMES["repair/idle/0"] = _with_screen(_REPAIR_CABINET, _repair0)
FRAMES["repair/idle/1"] = _with_screen(_REPAIR_CABINET, _repair1)
FRAMES["repair/ok/0"] = _with_screen(_REPAIR_CABINET, _screen("e", _CHECK_MARK))
FRAMES["repair/bad/0"] = _with_screen(_REPAIR_CABINET, _screen("r", _X_MARK))
_COOL = """
........
.ggg.g..
.g.gg...
........
.gggg.g.
.g......
........
.gg.ggg.
........
........
"""
FRAMES["repair/cooldown/0"] = _with_screen(
    replace(_REPAIR_CABINET, {"c": "d"}), _screen("d", _COOL))

# ---------------------------------------------------------------------------
# icon/* (8x8): white glyphs on transparent (HUD / repair terminal labels).
# ---------------------------------------------------------------------------

FRAMES["icon/identity"] = frame("""
........
.WWW....
W...W...
W...WWWW
W...W.WW
.WWW..W.
........
........
""")

FRAMES["icon/content"] = frame("""
........
WWWWWWWW
W.W.W.W.
WWWWWWWW
WWWWWWWW
.W.W.W.W
WWWWWWWW
........
""")

FRAMES["icon/streaming"] = frame("""
........
.W......
.WWW....
.WWWWW..
.WWWWWW.
.WWWWW..
.WWW....
.W......
""")

# ---------------------------------------------------------------------------
# checkpoint/* (16x32): pole with a lamp on top and a flag to the right.
# ---------------------------------------------------------------------------

_POLE_BASE = frame("""
.....KKKKKK.....
.....K....K.....
.....K....K.....
.....K....K.....
.....K....K.....
.....KKKKKK.....
......KlgK......
......KlgK......
......KlgK......
......KlgK......
......KlgK......
......KlgK......
......KlgK......
......KlgK......
......KlgK......
......KlgK......
......KlgK......
......KlgK......
......KlgK......
......KlgK......
......KlgK......
......KlgK......
......KlgK......
......KlgK......
......KlgK......
......KlgK......
......KlgK......
......KlgK......
......KlgK......
....KKKKKKKK....
....KuuggggK....
....KKKKKKKK....
""")

_LAMP_OFF = frame("""
dgdd
dddd
dddd
nnnn
""")

_LAMP_ON = frame("""
YWYY
YYYY
YYYY
yyyy
""")

_FLAG_OFF = frame("""
KKKKKK.
KgggggK
KggggdK
KgggdgK
KggggdK
KgggggK
KKKKKKK
""")

_FLAG_ON_0 = frame("""
KKKKK..
KmMMMKK
KMMmMMK
KMMMMMK
KMMMMMK
KKKMMMK
..KKKKK
""")

_FLAG_ON_1 = frame("""
..KKKKK
KKKMMMK
KmMMMMK
KMMmMMK
KMMMMMK
KMMMKKK
KKKKK..
""")


def _checkpoint(lamp, flag, glow=False):
    rows = blit([r for r in _POLE_BASE], lamp, 6, 1)
    rows = blit(rows, flag, 9, 6)
    if glow:
        for x, y in ((4, 2), (11, 3)):
            set_px(rows, x, y, "Y")
    return rows


FRAMES["checkpoint/off/0"] = _checkpoint(_LAMP_OFF, _FLAG_OFF)
FRAMES["checkpoint/on/0"] = _checkpoint(_LAMP_ON, _FLAG_ON_0, glow=True)
FRAMES["checkpoint/on/1"] = _checkpoint(_LAMP_ON, _FLAG_ON_1, glow=True)

# ---------------------------------------------------------------------------
# exit/* (32x32): server-room blast door with hazard stripes and a lamp.
# ---------------------------------------------------------------------------


def _door_frame(lamp_on):
    rows = blank(32, 32)
    rect(rows, 0, 0, 32, 32, "K", fill=False)          # outer outline
    rect(rows, 1, 1, 30, 30, "d")                      # frame body
    rect(rows, 1, 1, 30, 1, "g")                       # top highlight
    rect(rows, 1, 1, 1, 30, "g")                       # left highlight
    # hazard stripes on the header (rows 2..4), diagonal yellow/black
    for y in range(2, 5):
        for x in range(2, 30):
            if 12 <= x <= 19:
                continue
            rows_ch = "Y" if ((x + y) // 2) % 2 == 0 else "K"
            set_px(rows, x, y, rows_ch)
    rect(rows, 1, 5, 30, 1, "K")                        # header bottom edge
    # lamp housing in the header centre
    rect(rows, 12, 1, 8, 5, "K")
    if lamp_on:
        rect(rows, 13, 2, 6, 3, "R")
        set_px(rows, 14, 2, "W")
        set_px(rows, 15, 2, "W")
        set_px(rows, 13, 4, "r")
        set_px(rows, 18, 4, "r")
    else:
        rect(rows, 13, 2, 6, 3, "r")
        set_px(rows, 14, 2, "R")
    # door opening: x=4..27, y=6..30 (frame posts at x=1..3 and 28..30)
    rect(rows, 3, 6, 1, 25, "K")
    rect(rows, 28, 6, 1, 25, "K")
    rect(rows, 2, 6, 1, 25, "u")
    rect(rows, 29, 6, 1, 25, "n")
    # threshold
    rect(rows, 1, 30, 30, 1, "n")
    return rows


def _door_leaves(rows):
    """Closed double door with a barred window, rivets and a centre seam."""
    rect(rows, 4, 6, 24, 24, "g")
    rect(rows, 4, 6, 24, 1, "l")                        # top highlight
    rect(rows, 4, 6, 1, 24, "l")                        # left highlight
    rect(rows, 27, 6, 1, 24, "d")                       # right shadow
    rect(rows, 4, 29, 24, 1, "d")                       # bottom shadow
    rect(rows, 15, 6, 2, 24, "K")                       # centre seam
    # barred window
    rect(rows, 7, 9, 18, 9, "K", fill=False)
    rect(rows, 8, 10, 16, 7, "n")
    for x in (11, 15, 16, 20):
        rect(rows, x, 10, 1, 7, "u")
    # rivets
    for x, y in ((6, 8), (25, 8), (6, 27), (25, 27), (10, 27), (21, 27), (10, 8), (21, 8)):
        set_px(rows, x, y, "d")
        set_px(rows, x + 1, y, "l")
    # bolt plates near the seam
    rect(rows, 12, 21, 3, 3, "l")
    rect(rows, 17, 21, 3, 3, "l")
    rect(rows, 13, 22, 1, 1, "d")
    rect(rows, 18, 22, 1, 1, "d")
    return rows


def _door_open(rows, bright):
    """Leaves retracted into the posts; glowing interior."""
    rect(rows, 4, 6, 24, 24, "n")
    # retracted leaf edges
    rect(rows, 4, 6, 2, 24, "g")
    rect(rows, 26, 6, 2, 24, "g")
    rect(rows, 5, 6, 1, 24, "d")
    rect(rows, 26, 6, 1, 24, "l")
    # glow bands: outer half-cyan, then dark cyan, cyan, white core
    core = ["q", "C", "c", "W"] if bright else ["C", "c", "W"]
    inset = 0
    for ch in core:
        x0 = 6 + inset
        w = 20 - inset * 2
        rect(rows, x0, 6 + inset, w, 24 - inset, ch)
        inset += 3 if ch != "q" else 2
    # floor reflection
    rect(rows, 6, 29, 20, 1, "e")
    rect(rows, 9, 29, 14, 1, "G")
    if bright:
        for x, y in ((8, 8), (23, 8), (7, 20), (24, 20), (8, 26), (23, 26)):
            set_px(rows, x, y, "W")
    return rows


FRAMES["exit/locked/0"] = _door_leaves(_door_frame(lamp_on=True))
FRAMES["exit/locked/1"] = _door_leaves(_door_frame(lamp_on=False))
FRAMES["exit/open/0"] = _door_open(_door_frame(lamp_on=False), bright=False)
FRAMES["exit/open/1"] = _door_open(_door_frame(lamp_on=False), bright=True)

# ---------------------------------------------------------------------------
# tiles/* (16x16): no outlines; floor/platform/wall/rack tile horizontally.
# ---------------------------------------------------------------------------


def _floor_panel(rows, x, y, w, h):
    """Bevelled raised-floor panel: grey highlight top/left, night shadow right/bottom."""
    rect(rows, x, y, w, h, "d")
    rect(rows, x, y, w, 1, "g")
    rect(rows, x, y, 1, h, "g")
    rect(rows, x + w - 1, y, 1, h, "n")
    rect(rows, x, y + h - 1, w, 1, "n")
    return rows


_floor = blank(16, 16)
for _px, _py in ((0, 0), (8, 0), (0, 8), (8, 8)):
    _floor_panel(_floor, _px, _py, 8, 8)
FRAMES["tiles/floor"] = _floor

_floor_top = blank(16, 16)
_floor_panel(_floor_top, 0, 2, 8, 6)
_floor_panel(_floor_top, 8, 2, 8, 6)
_floor_panel(_floor_top, 0, 8, 8, 8)
_floor_panel(_floor_top, 8, 8, 8, 8)
rect(_floor_top, 0, 0, 16, 1, "l")
rect(_floor_top, 0, 1, 16, 1, "u")
FRAMES["tiles/floor_top"] = _floor_top

FRAMES["tiles/platform"] = frame("""
llllllllllllllll
ggnnggnnggnnggnn
ggnnggnnggnnggnn
gggggggggggggggg
dddddddddddddddd
................
................
................
................
................
................
................
................
................
................
................
""")

_wall = blank(16, 16)
rect(_wall, 0, 0, 16, 16, "p")
rect(_wall, 0, 0, 16, 1, "v")
rect(_wall, 0, 0, 1, 16, "v")
rect(_wall, 15, 0, 1, 16, "n")
rect(_wall, 0, 15, 16, 1, "n")
for _rx, _ry in ((2, 2), (12, 2), (2, 12), (12, 12)):
    set_px(_wall, _rx, _ry, "u")
    set_px(_wall, _rx + 1, _ry + 1, "n")
# vent slits
for _vy in (6, 8, 10):
    rect(_wall, 5, _vy, 6, 1, "n")
FRAMES["tiles/wall"] = _wall

# glitch spike hazard: jagged red/magenta zigzag in the bottom 8 px
_SPIKE_HEIGHTS = [2, 4, 6, 8, 8, 6, 4, 2, 2, 4, 6, 8, 8, 6, 4, 2]   # zigzag, wraps
_spike = blank(16, 16)


def _spike_filled(x, y):
    x %= 16                      # wrap horizontally so the tile stays seamless
    if y > 15:
        return True
    return y >= 16 - _SPIKE_HEIGHTS[x]


for _x in range(16):
    for _y in range(8, 16):
        if not _spike_filled(_x, _y):
            continue
        # distance to the nearest empty pixel decides the tone (bevelled edge)
        _edge = not all(_spike_filled(_x + dx, _y + dy) for dx, dy in ((1, 0), (-1, 0), (0, -1)))
        _edge2 = not all(_spike_filled(_x + dx, _y + dy)
                         for dx, dy in ((2, 0), (-2, 0), (0, -2), (1, -1), (-1, -1)))
        if _edge:
            _ch = "m"
        elif _edge2:
            _ch = "M"
        elif _y >= 14:
            _ch = "r"
        else:
            _ch = "R"
        set_px(_spike, _x, _y, _ch)
# glitch flecks on the tips
for _x, _y in ((3, 9), (11, 9)):
    set_px(_spike, _x, _y, "W")
FRAMES["tiles/spike"] = _spike


def _rack_unit(rows, y, leds):
    """3-row server unit spanning x=1..14 inside the rails."""
    rect(rows, 1, y, 14, 1, "g")
    rect(rows, 1, y + 1, 14, 2, "d")
    set_px(rows, 2, y + 1, leds[0])
    set_px(rows, 4, y + 1, leds[1])
    rect(rows, 11, y + 1, 2, 1, "l")
    rect(rows, 7, y + 2, 6, 1, "n")
    return rows


_rack = blank(16, 16)
rect(_rack, 0, 0, 16, 16, "n")
rect(_rack, 0, 0, 1, 16, "u")
rect(_rack, 15, 0, 1, 16, "u")
for _uy, _leds in ((0, "Gc"), (4, "GG"), (8, "Rc"), (12, "Gc")):
    _rack_unit(_rack, _uy, _leds)
FRAMES["tiles/rack"] = _rack

_rack_top = blank(16, 16)
rect(_rack_top, 0, 0, 16, 16, "n")
rect(_rack_top, 0, 0, 1, 16, "u")
rect(_rack_top, 15, 0, 1, 16, "u")
rect(_rack_top, 0, 0, 16, 1, "l")
rect(_rack_top, 0, 1, 16, 1, "g")
rect(_rack_top, 2, 2, 12, 1, "d")
for _uy, _leds in ((4, "cG"), (8, "GG"), (12, "Gc")):
    _rack_unit(_rack_top, _uy, _leds)
FRAMES["tiles/rack_top"] = _rack_top

FRAMES["tiles/cable"] = frame("""
.......gd..p....
.......gd..p....
.......gd..p....
.......gd..p....
......llll.p....
.......gd..p....
.......gd..p....
.......gd..p....
.......gd..p....
.......gd..p....
.......gd..p....
.......gd..p....
......llll.p....
.......gd..p....
.......gd..p....
.......gd..p....
""")

_pillar = blank(16, 16)
rect(_pillar, 3, 0, 10, 16, "d")
rect(_pillar, 3, 0, 1, 16, "l")
rect(_pillar, 4, 0, 3, 16, "g")
rect(_pillar, 11, 0, 2, 16, "n")
rect(_pillar, 3, 7, 10, 1, "n")
rect(_pillar, 3, 8, 10, 1, "g")
FRAMES["tiles/pillar"] = _pillar

_glass = blank(16, 16)
rect(_glass, 0, 0, 16, 16, "q")
rect(_glass, 0, 0, 16, 16, "u", fill=False)
line(_glass, 13, 1, 1, 13, "h")
line(_glass, 14, 1, 2, 13, "h")
line(_glass, 14, 6, 9, 11, "h")
FRAMES["tiles/glass"] = _glass

# ---------------------------------------------------------------------------
# deco/* : 16x16 monitor / lamp / poster, 16x32 server tower / coffee machine.
# ---------------------------------------------------------------------------

FRAMES["deco/monitor"] = frame("""
................
................
................
.KKKKKKKKKKKKK..
.KdddddddddddK..
.KdCCCCCCCCCdK..
.KdCcCCCcCCCdK..
.KdCCcCcCcCCdK..
.KdCCCcCCCcCdK..
.KdCCCCCCCCGdK..
.KdddddddddddK..
.KKKKKKKKKKKKK..
......KggK......
......KggK......
....KKKKKKKK....
....KddddddK....
""")

FRAMES["deco/lamp"] = frame("""
.......Kg.......
.......Kg.......
.......Kg.......
.......Kg.......
.....KKKKKK.....
....KuuuuuuK....
...KuggggggdK...
..KKKKKKKKKKKK..
....KYYWYYYK....
.....KYYYYK.....
......KyyK......
......KKKK......
................
................
................
................
""")

FRAMES["deco/poster"] = frame("""
................
.KKKKKKKKKKKKK..
.KMMMMMMMMMMMK..
.KMnnnnnnnnnMK..
.KMnnWnnnnnnMK..
.KMnnWWWnnnnMK..
.KMnnWWWWWnnMK..
.KMnnWWWWWWnMK..
.KMnnWWWWWnnMK..
.KMnnWWWnnnnMK..
.KMnnWnnnnnnMK..
.KMnnnnnnnnnMK..
.KMnmmmnmnmnMK..
.KMnnnnnnnnnMK..
.KMMMMMMMMMMMK..
.KKKKKKKKKKKKK..
""")

_server = blank(16, 32)
rect(_server, 2, 1, 12, 31, "K")
rect(_server, 3, 2, 10, 29, "d")
rect(_server, 3, 2, 1, 29, "u")
rect(_server, 12, 2, 1, 29, "n")
rect(_server, 3, 2, 10, 1, "u")
for _i, _uy in enumerate(range(4, 28, 4)):
    rect(_server, 4, _uy, 8, 1, "g")
    rect(_server, 4, _uy + 1, 8, 2, "d")
    set_px(_server, 5, _uy + 1, "G" if _i % 3 != 2 else "R")
    set_px(_server, 7, _uy + 1, "c" if _i % 2 == 0 else "e")
    rect(_server, 9, _uy + 2, 3, 1, "n")
rect(_server, 3, 29, 10, 1, "n")
rect(_server, 2, 0, 12, 1, "K")
rect(_server, 3, 1, 10, 1, "l")
FRAMES["deco/server"] = _server

FRAMES["deco/coffee_machine"] = frame("""
................
..KKKKKKKKKKKK..
..KllllllllllK..
..KlggggggggdK..
..KlgKKKKKKgdK..
..KlgKnnnnKgdK..
..KlgKnGnnKgdK..
..KlgKnnnnKgdK..
..KlgKKKKKKgdK..
..KlggggggggdK..
..KlgRgggcggdK..
..KlggggggggdK..
..KKKKKKKKKKKK..
..KlddddddddlK..
..KldKKKKKKdlK..
..KldKnnnnKdlK..
..KldKnnnnKdlK..
..KldKnWWnKdlK..
..KldKnWWnKdlK..
..KldKnBBnKdlK..
..KldKKKKKKdlK..
..KlddddddddlK..
..KKKKKKKKKKKK..
..KuuuuuuuuuuK..
..KgggggggggdK..
..KgggggggggdK..
..KgggggggggdK..
..KgggggggggdK..
..KgggggggggdK..
..KgggggggggdK..
..KdddddddddnK..
..KKKKKKKKKKKK..
""")

# ---------------------------------------------------------------------------
# bg/* (64x64): seamless parallax backgrounds, generated with a fixed seed.
# ---------------------------------------------------------------------------


def _bg_sky():
    rng = random.Random(4000)
    rows = blank(64, 64, "k")
    # soft nebula band around the middle (both tile edges stay 'k' -> seamless)
    for y in range(64):
        weight = max(0.0, 1.0 - abs(y - 34) / 16.0)
        for x in range(64):
            if rng.random() < 0.22 * weight:
                set_px(rows, x, y, "n")
    # stars: mostly dim, a few bright, a couple of 'plus' sparkles
    for _ in range(26):
        x, y = rng.randrange(64), rng.randrange(64)
        set_px(rows, x, y, rng.choice("ggggllld"))
    for _ in range(5):
        x, y = rng.randrange(64), rng.randrange(64)
        set_px(rows, x, y, "W")
    for _ in range(2):
        x, y = rng.randrange(1, 63), rng.randrange(1, 63)
        set_px(rows, x, y, "l")
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            set_px(rows, x + dx, y + dy, "g")
    return rows


def _bg_racks():
    rng = random.Random(1337)
    rows = blank(64, 64, "k")
    x = 0
    cabinet_widths = [16, 16, 16, 16]
    for w in cabinet_widths:
        rect(rows, x, 0, w, 64, "n")
        rect(rows, x, 0, 1, 64, "p")
        rect(rows, x + w - 1, 0, 1, 64, "p")
        for y in range(0, 64, 4):
            rect(rows, x + 1, y, w - 2, 1, "k")          # unit separator
            if rng.random() < 0.55:
                led = rng.choice("CCeerv")
                set_px(rows, x + 2 + rng.randrange(3), y + 2, led)
            if rng.random() < 0.3:
                rect(rows, x + w - 6, y + 2, 3, 1, "d")   # handle
        x += w
    return rows


def _bg_city():
    rng = random.Random(2024)
    rows = blank(64, 64, "k")
    for _ in range(14):
        set_px(rows, rng.randrange(64), rng.randrange(28), rng.choice("gggd"))
    # far buildings (night purple), then near buildings (purple) in front
    for colour, top_min, top_max, widths in (
        ("n", 22, 36, [9, 6, 11, 7, 10, 8, 13]),
        ("p", 34, 48, [7, 12, 5, 9, 14, 6, 11]),
    ):
        x = 0
        while x < 64:
            w = widths[len(widths) and (x * 7 + len(widths)) % len(widths)]
            w = min(w, 64 - x) if 64 - x < 4 else w
            top = rng.randrange(top_min, top_max)
            rect(rows, x, top, w, 64 - top, colour)
            if colour == "p":
                # sparse lit windows
                for wy in range(top + 2, 62, 3):
                    for wx in range(x + 1, x + w - 1, 2):
                        if rng.random() < 0.18:
                            set_px(rows, wx, wy, rng.choice("ddvv"))
            # antenna on some roofs
            if w >= 7 and rng.random() < 0.6:
                ax = x + w // 2
                h = rng.randrange(3, 8)
                line(rows, ax, top - 1, ax, top - h, colour if colour == "p" else "n")
                if colour == "p" and rng.random() < 0.7:
                    set_px(rows, ax, top - h - 1, "r")
            x += w
    # ground haze
    rect(rows, 0, 62, 64, 2, "p")
    return rows


FRAMES["bg/sky"] = _bg_sky()
FRAMES["bg/racks"] = _bg_racks()
FRAMES["bg/city"] = _bg_city()
