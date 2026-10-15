"""Character, enemy, boss and effect frames for the Videoland 4000 atlas.

All artwork is original. Frames are text pixel-maps (see palette.py for the
character -> colour mapping, '.' = transparent). Characters face RIGHT and are
bottom-aligned in their cell (runtime origin is bottom-centre).

Delivered here (see docs/ARCHITECTURE.md section 7):
  player/*            16x20   hero engineer (hand drawn)
  spinner/spin/0..7   16x16   buffering spinner enemy (generated ring + hand drawn face)
  expired/*           16x16   cracked grey keycard
  bug/*               16x16   green beetle
  boss/*              32x32   the Infinite Loading Spinner
  bullet/0..3          8x8    mini ring projectile
  pop/*, sparkle/*, wave/*, warn/*   16x16 effects
"""

import math

from pixelmap import blank, blit, frame, get_px, line, replace, set_px

FRAMES = {}


# ---------------------------------------------------------------------------
# generic helpers
# ---------------------------------------------------------------------------
def outline(rows, ch="K"):
    """Give every transparent pixel that touches (8-neighbourhood) a drawn pixel the colour ch."""
    h, w = len(rows), len(rows[0])
    out = [r for r in rows]
    for y in range(h):
        for x in range(w):
            if rows[y][x] != ".":
                continue
            touch = False
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    if (dx or dy) and get_px(rows, x + dx, y + dy) not in (".", ch):
                        touch = True
            if touch:
                set_px(out, x, y, ch)
    return out


def arc_ring(size, r_out, r_in, head_deg, sweep_deg, stops):
    """3/4-style ring: pixels with r_in <= d < r_out whose angle lies within sweep_deg
    behind the head (clockwise rotation). stops = [(fraction, char), ...] head -> tail."""
    rows = blank(size, size)
    c = (size - 1) / 2
    for y in range(size):
        for x in range(size):
            dx, dy = x - c, y - c
            d = math.hypot(dx, dy)
            if not (r_in <= d < r_out):
                continue
            theta = math.degrees(math.atan2(dy, dx))
            behind = (head_deg - theta) % 360
            if behind > sweep_deg:
                continue
            f = behind / sweep_deg
            for stop, ch in stops:
                if f <= stop:
                    set_px(rows, x, y, ch)
                    break
    return rows


def disc(size, radius, ch):
    rows = blank(size, size)
    c = (size - 1) / 2
    for y in range(size):
        for x in range(size):
            if math.hypot(x - c, y - c) < radius:
                set_px(rows, x, y, ch)
    return rows


def put(name, rows):
    FRAMES[name] = [r for r in rows]
    return FRAMES[name]


# ---------------------------------------------------------------------------
# PLAYER (16x20)
# ---------------------------------------------------------------------------
# Head outline x4..x14 rows 0..9 (9 px interior), headset band + ear cup on the
# left, torso x5..x13 rows 10..14 (magenta hoodie, violet hood), laptop tucked
# under the far arm sticking out behind (x0..x5 rows 11..14, cyan screen edge),
# jeans rows 15..17, sneakers rows 18..19.

PLAYER_IDLE_0 = frame("""
......KKKKKKK...
.....KBlllllBK..
....KglBbBBBBBK.
....KglBBBBBBbK.
....KllBSSSSSSK.
....KllSWKSWKSK.
....KglSSSSSSSK.
....KKKSSSSKSSK.
.....KSSSSSSSK..
......KKKKKKK...
.....KMMvvvMMK..
.KKKKKmMMMMpMK..
KlcccKmMMMMpMK..
KggggKmMMMMpMK..
.KKKKKmMMMMpSK..
......KCCKCCK...
......KCCKCCK...
......KCCKCCK...
.....KWWKKWWWK..
.....KKKKKKKKK..
""")

UPPER = PLAYER_IDLE_0[:15]          # head + torso + laptop (rows 0..14)
LEGS_IDLE = PLAYER_IDLE_0[15:]      # rows 15..19


def player(upper, legs, upper_dy=0):
    rows = blank(16, 20)
    blit(rows, upper, 0, upper_dy)
    blit(rows, legs, 0, 20 - len(legs))
    return rows


def arm_back(upper):
    """Near arm swung back: hand appears at the back hip, right side becomes plain body."""
    rows = [r for r in upper]
    for y in range(11, 15):
        set_px(rows, 11, y, "M")
        set_px(rows, 12, y, "p")
    set_px(rows, 6, 12, "p")
    set_px(rows, 6, 13, "M")
    set_px(rows, 6, 14, "S")
    return rows


def arm_fwd(upper):
    """Near arm swung forward: hand raised to waist height in front."""
    rows = [r for r in upper]
    set_px(rows, 12, 13, "S")
    set_px(rows, 12, 14, "p")
    set_px(rows, 11, 14, "M")
    return rows


put("player/idle/0", PLAYER_IDLE_0)
# breathing bob: head sinks 1 px into the shoulders
idle1 = blank(16, 20)
blit(idle1, PLAYER_IDLE_0[:10], 0, 1)
blit(idle1, PLAYER_IDLE_0[11:], 0, 11)
put("player/idle/1", idle1)

# run cycle: 0 contact (right leg forward, body 1px lower), 1 pass (trailing foot
# kicked back), 2 contact (mirrored stride, arm forward), 3 pass (knee up front)
LEGS_CONTACT_A = frame("""
....KCCK.KCCK...
...KCCK...KCCK..
..KWWWK...KWWWK.
..KKKKK...KKKKK.
""")
LEGS_PASS_A = frame("""
.....KCCKCCK....
....KCCKKCCK....
...KWWWKKCCK....
...KKKKKKWWWK...
........KKKKK...
""")
LEGS_CONTACT_B = frame("""
....KCCKKKCCK...
...KCCK...KCCK..
..KWWWK...KWWWK.
..KKKKK...KKKKK.
""")
LEGS_PASS_B = frame("""
......KCCKCCK...
......KCCKKCCK..
......KCCKKWWWK.
.....KWWWKKKKKK.
.....KKKKK......
""")
put("player/run/0", player(arm_back(UPPER), LEGS_CONTACT_A, 1))
put("player/run/1", player(UPPER, LEGS_PASS_A, 0))
put("player/run/2", player(arm_fwd(UPPER), LEGS_CONTACT_B, 1))
put("player/run/3", player(UPPER, LEGS_PASS_B, 0))

# jump: knees tucked, arm pumped up
LEGS_JUMP = frame("""
.....KCCKKCCK...
....KCCCKKCCCK..
....KWWWKKWWWK..
....KKKKKKKKKK..
................
""")
jump_upper = [r for r in UPPER]
set_px(jump_upper, 12, 11, "S")
set_px(jump_upper, 12, 14, "p")
set_px(jump_upper, 11, 14, "M")
put("player/jump/0", player(jump_upper, LEGS_JUMP))

# fall: near arm flung up sideways, legs straight and apart
LEGS_FALL = frame("""
......KCCKCCK...
.....KCCKKKCCK..
.....KCCK.KCCK..
....KWWWK.KWWWK.
....KKKKK.KKKKK.
""")
fall_upper = [r for r in UPPER]
for x in (13, 14, 15):
    set_px(fall_upper, x, 9, "K")
    set_px(fall_upper, x, 11, "K")
set_px(fall_upper, 13, 10, "M")
set_px(fall_upper, 14, 10, "S")
set_px(fall_upper, 15, 10, "K")
set_px(fall_upper, 12, 12, "p")
set_px(fall_upper, 12, 13, "p")
set_px(fall_upper, 12, 14, "p")
set_px(fall_upper, 11, 12, "M")
set_px(fall_upper, 11, 13, "M")
set_px(fall_upper, 11, 14, "M")
put("player/fall/0", player(fall_upper, LEGS_FALL))

# hurt: lean back, x eyes, open mouth, laptop knocked askew
put("player/hurt/0", frame("""
.....KKKKKKK....
....KBlllllBK...
...KglBbBBBBBK..
...KglBBBBBBbK..
...KllKSKSKSKK..
...KllSKSSSKSK..
...KglKSKSKSKK..
...KKKSSSKKSSK..
....KSSSKRRKSK..
.....KKKKKKKK...
.....KMMvvvMMK..
.....KmMMMMpMK..
.KKKKKmMMMMpMK..
KlccKKmMMMMpMK..
KgggKKmMMMMpSK..
.KKKK.KCCKKCCK..
......KCCK.KCCK.
.....KCCK..KCCK.
....KWWWK..KWWWK
....KKKKK..KKKKK
"""))

# dead: ghosted engineer (translucent), x eyes, arms raised, wavy ghost hem; the
# laptop comes along.
put("player/dead/0", frame("""
......KKKKKKK...
.....KhlllllhK..
....KhhhhhhhhhK.
....KhhhhhhhhhK.
....KhKhKhKhKhK.
....KhhKhhhKhhK.
....KhKhKhKhKhK.
...KKKhhhhhKhhK.
..KhKKhhhhKKKhKK
..KhhKKKKKKKhhhK
..KhhKhhhhhhKhhK
..KhKKhhhhhhKKhK
..KKKhhhhhhhhKK.
KhhhhKhhhhhhhhK.
KhhhhKhhhhhhhhK.
.KKKKKhhhhhhhhK.
.....KhhhhhhhhK.
.....KhhKhhKhhK.
.....KhKKKhKKhK.
.....KK.KK.KKKK.
"""))

# cheer: laptop raised overhead with both arms (figure 2 px lower, knees bent)
CHEER_0 = frame("""
...KKKKKKKKKKK..
.KSKlccccccclKSK
.KMKKKKKKKKKKKMK
.KMKKBlllllBKKMK
.KMKglBbBBBBBKMK
.KMKglBBBBBBbKMK
.KMKllBKSSSKSKMK
.KMKllKSKSKSKKMK
.KMKglSSSSSSSKMK
.KMKKKSSSSSSSKMK
.KMKKSSKKKKSKKMK
.KMK.KKKKKKK.KMK
.KMMMMMvvvMMMMMK
.KKKKmMMMMpMKKKK
....KmMMMMpMK...
....KmMMMMpMK...
....KmMMMMpMK...
.....KCCKCCK....
....KWWKKWWWK...
....KKKKKKKKK...
""")
put("player/cheer/0", CHEER_0)
# cheer 1: laptop pumped 1 px lower (arms bend), legs stretch 1 px
cheer1 = blank(16, 20)
blit(cheer1, CHEER_0[3:], 0, 3)
blit(cheer1, CHEER_0[0:3], 0, 1)
put("player/cheer/1", cheer1)

# interact: typing on the laptop held open in front
put("player/interact/0", frame("""
......KKKKKKK...
.....KBlllllBK..
....KglBbBBBBBK.
....KglBBBBBBbK.
....KllBSSSSSSK.
....KllSWKSWKSK.
....KglSSSSSSSK.
....KKKSSSSKSSK.
.....KSSSSSSSK..
......KKKKKKK...
.....KMMvvvMMK..
.....KmMMMMMMMKK
.....KmMMMMKKKcK
.....KmMMMKScccK
.....KmMMMKSKggK
......KCCKKKKKK.
......KCCKCCK...
......KCCKCCK...
.....KWWKKWWWK..
.....KKKKKKKKK..
"""))

# ---------------------------------------------------------------------------
# SPINNER enemy (16x16): 3/4 ring, white head fading to magenta tail, angry face
# ---------------------------------------------------------------------------
SPINNER_FACE = frame("""
................
................
................
................
.....dddddd.....
....dddddddd....
....dWddddWd....
....ddWddWdd....
....dWWddWWd....
....dddddddd....
.....ddmmdd.....
.....dddddd.....
................
................
................
................
""")
SPINNER_STOPS = [(0.18, "W"), (0.45, "m"), (0.8, "M"), (1.0, "t")]
for i in range(8):
    ring = arc_ring(16, 6.5, 4.5, i * 45 - 90, 270, SPINNER_STOPS)
    body = blit([r for r in SPINNER_FACE], ring)
    put(f"spinner/spin/{i}", outline(body))

# ---------------------------------------------------------------------------
# BULLET (8x8): mini ring dot
# ---------------------------------------------------------------------------
BULLET_STOPS = [(0.25, "W"), (0.6, "m"), (1.0, "M")]
for i in range(4):
    ring = arc_ring(8, 3.5, 1.5, i * 90 - 90, 270, BULLET_STOPS)
    put(f"bullet/{i}", outline(ring))

# ---------------------------------------------------------------------------
# BOSS (32x32): the Infinite Loading Spinner
# ---------------------------------------------------------------------------
BOSS_FACE = frame("""
................................
................................
................................
................................
................................
................................
................................
................................
............pppppppp............
..........pppppppppppp..........
.........pppppppppppppp.........
........pppppppppppppppp........
........pYYpppppppppppYYp.......
.......pYYYYpppppppppYYYYp......
.......ppYKYYpppppppYYKYpp......
.......pppYYYYpppppYYYYppp......
.......ppppppppppppppppppp......
.......pppWKWKWKWKWKWKWppp......
........ppKWKWKWKWKWKWKpp.......
........ppWKWKWKWKWKWKWpp.......
.........pKWKWKWKWKWKWKp........
..........ppWKWKWKWKWpp.........
............ppppppppp...........
..............ppppp.............
................................
................................
................................
................................
................................
................................
................................
................................
""")
BOSS_STOPS = [(0.12, "W"), (0.3, "m"), (0.62, "M"), (0.85, "v"), (1.0, "p")]


def boss_ring(head_deg, stops=BOSS_STOPS):
    return arc_ring(32, 15.5, 10.5, head_deg, 270, stops)


def boss_frame(ring, face=BOSS_FACE):
    body = blit([r for r in face], ring)
    return outline(body)


for i in range(8):
    put(f"boss/spin/{i}", boss_frame(boss_ring(i * 45 - 90)))

# stun: ring stopped, spiral eyes, stars circling
STUN_FACE = replace(BOSS_FACE, {"Y": "p"})
SPIRAL = frame("""
.WWW.
W...W
.WW.W
.W..W
..WW.
""")
for (ex, ey) in ((8, 11), (20, 11)):
    for dy, row in enumerate(SPIRAL):
        for dx, ch in enumerate(row):
            set_px(STUN_FACE, ex + dx, ey + dy, "W" if ch == "W" else "p")
# flat wobbly mouth
for y in (17, 18, 19, 20, 21):
    STUN_FACE[y] = STUN_FACE[y].replace("W", "p").replace("K", "p")
for x in range(10, 22):
    set_px(STUN_FACE, x, 19 if (x // 2) % 2 == 0 else 20, "W")
stun_ring = boss_ring(-90, [(0.3, "l"), (0.7, "g"), (1.0, "d")])
for i in range(2):
    f = boss_frame(stun_ring, STUN_FACE)
    stars = ((4, 3), (16, 1), (27, 4)) if i == 0 else ((7, 1), (22, 2), (29, 7))
    for (sx, sy) in stars:
        for (dx, dy) in ((0, 0), (-1, 0), (1, 0), (0, -1), (0, 1)):
            set_px(f, sx + dx, sy + dy, "Y")
    put(f"boss/stun/{i}", f)

# hit: white flash with cracks
hit = boss_frame(boss_ring(-90))
hit = replace(hit, {"W": "W", "m": "W", "M": "W", "v": "W", "p": "l", "Y": "W", "t": "W"})
for (x0, y0, x1, y1) in ((2, 10, 8, 14), (8, 14, 6, 19), (29, 9, 24, 13), (24, 13, 26, 18), (15, 1, 14, 6), (14, 6, 17, 9)):
    line(hit, x0, y0, x1, y1, "K")
put("boss/hit/0", hit)

# charge: ring glowing bright
put("boss/charge/0", boss_frame(boss_ring(-90, [(0.3, "W"), (0.6, "m"), (0.85, "M"), (1.0, "m")])))
charge1 = boss_frame(boss_ring(-90, [(0.5, "W"), (0.8, "m"), (1.0, "W")]))
put("boss/charge/1", charge1)

# dead: broken ring pieces, x eyes
dead_ring = blank(32, 32)
for (head, sweep, dx, dy) in ((-125, 55, -1, 1), (-55, 50, 1, 2), (15, 60, 1, 3), (90, 55, 0, 2), (150, 45, -1, 2)):
    piece = arc_ring(32, 15.5, 10.5, head, sweep, [(0.35, "m"), (0.75, "M"), (1.0, "v")])
    blit(dead_ring, piece, dx, dy)
DEAD_FACE = replace(BOSS_FACE, {"Y": "p", "W": "p", "K": "p"})
for (ex, ey) in ((9, 12), (20, 12)):
    for k in range(4):
        set_px(DEAD_FACE, ex + k, ey + k, "W")
        set_px(DEAD_FACE, ex + 3 - k, ey + k, "W")
for x in range(12, 20):
    set_px(DEAD_FACE, x, 20, "W")
set_px(DEAD_FACE, 11, 21, "W")
set_px(DEAD_FACE, 20, 21, "W")
put("boss/dead/0", boss_frame(dead_ring, DEAD_FACE))

# ---------------------------------------------------------------------------
# EXPIRED token (16x16): cracked grey keycard with a sad face, 1 px tilt (the
# lower half sits 1 px further left), magnetic stripe along the bottom.
# ---------------------------------------------------------------------------
EXPIRED_0 = frame("""
................
................
................
...KKKKKKKKKKK..
..KlllllllglgK..
..KlKKllKKlggK..
..KllllllllglK..
.KlllKKllglgK...
.KllKllKllggK...
.KlllllllglgK...
.KddddddddddK...
.KKKKKKKKKKKK...
................
................
................
................
""")
put("expired/float/0", EXPIRED_0)
# wobble: tilt the other way (top half left, bottom half right) and bob 1 px up
exp1 = blank(16, 16)
blit(exp1, EXPIRED_0[:7], -1, -1)
blit(exp1, EXPIRED_0[7:], 1, 6)
put("expired/float/1", exp1)
# reappearance warning: dashed half-transparent outline of the card only
warn_card = blank(16, 16)
for y, row in enumerate(EXPIRED_0):
    for x, ch in enumerate(row):
        if ch == "K" and (x + y) % 2 == 0:
            set_px(warn_card, x, y, "h")
put("expired/warn/0", warn_card)

# ---------------------------------------------------------------------------
# BUG (16x16): mischievous green beetle facing right - dark-green head with two
# big eyes (pupils looking ahead), V antennae, spotted shell, 3 legs per frame.
# ---------------------------------------------------------------------------
BUG_BODY = frame("""
................
................
................
................
...........G..G.
...........K..K.
............KK..
..........KKKKKK
..KKKKKKKKKeeeeK
.KGGGGGGGGKWWWWK
.KGeGGeGGGeWKWKK
.KGGGGGGGGeeeKeK
.KeeeeeeeeKKKKKK
..KKKKKKKK......
................
................
""")
bug0 = [r for r in BUG_BODY]
for x in (3, 6, 9):
    set_px(bug0, x, 14, "K")
    set_px(bug0, x - 1, 15, "K")
put("bug/walk/0", bug0)
bug1 = [r for r in BUG_BODY]
for x in (2, 5, 8):
    set_px(bug1, x, 14, "K")
    set_px(bug1, x + 1, 15, "K")
put("bug/walk/1", bug1)
put("bug/squash/0", frame("""
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
...........GK.GK
.KKKKKKKKKKKKKK.
KGGeGGeGGGeWKWKK
KeeeeeeeeeeeeeeK
KKKKKKKKKKKKKKKK
"""))

# ---------------------------------------------------------------------------
# EFFECTS (16x16)
# ---------------------------------------------------------------------------
def puff(rows, cx, cy, r, ch):
    for y in range(16):
        for x in range(16):
            if math.hypot(x - cx, y - cy) < r:
                set_px(rows, x, y, ch)


pop0 = blank(16, 16)
puff(pop0, 7.5, 7.5, 3.6, "l")
puff(pop0, 7.5, 7.5, 2.2, "W")
put("pop/0", pop0)
pop1 = blank(16, 16)
for (cx, cy) in ((4, 4), (11, 4), (4, 11), (11, 11)):
    puff(pop1, cx, cy, 2.6, "l")
    puff(pop1, cx, cy, 1.3, "W")
puff(pop1, 7.5, 7.5, 1.6, "g")
put("pop/1", pop1)
pop2 = blank(16, 16)
for (cx, cy) in ((2, 2), (13, 2), (2, 13), (13, 13), (7.5, 1), (7.5, 14), (1, 7.5), (14, 7.5)):
    puff(pop2, cx, cy, 1.6, "g")
    set_px(pop2, int(cx), int(cy), "l")
put("pop/2", pop2)

spark0 = blank(16, 16)
for (x, y) in ((7, 7), (8, 7), (7, 8), (8, 8)):
    set_px(spark0, x, y, "W")
for (x, y) in ((7, 5), (8, 5), (7, 10), (8, 10), (5, 7), (5, 8), (10, 7), (10, 8)):
    set_px(spark0, x, y, "Y")
put("sparkle/0", spark0)
spark1 = blank(16, 16)
for k in range(1, 6):
    for (x, y) in ((7, 7 - k), (8, 7 - k), (7, 8 + k), (8, 8 + k), (7 - k, 7), (7 - k, 8), (8 + k, 7), (8 + k, 8)):
        set_px(spark1, x, y, "Y" if k < 4 else "W")
for (x, y) in ((7, 7), (8, 7), (7, 8), (8, 8), (6, 7), (9, 8), (7, 6), (8, 9)):
    set_px(spark1, x, y, "W")
for (x, y) in ((4, 4), (11, 4), (4, 11), (11, 11)):
    set_px(spark1, x, y, "Y")
put("sparkle/1", spark1)
# burst: hollow centre, four long arms with white tips, diagonal embers
spark2 = blank(16, 16)
for k in range(3, 8):
    ch = "W" if k == 7 else "Y"
    for (x, y) in ((7, 7 - k), (8, 7 - k), (7, 8 + k), (8, 8 + k), (7 - k, 7), (7 - k, 8), (8 + k, 7), (8 + k, 8)):
        set_px(spark2, x, y, ch)
for (x, y) in ((2, 2), (13, 2), (2, 13), (13, 13)):
    set_px(spark2, x, y, "W")
for (x, y) in ((3, 3), (12, 3), (3, 12), (12, 12), (5, 5), (10, 5), (5, 10), (10, 10)):
    set_px(spark2, x, y, "Y")
put("sparkle/2", spark2)

# wave: ground shockwave arc, bottom aligned
def wave(cx, r, thick, crest, fill):
    rows = blank(16, 16)
    for y in range(16):
        for x in range(16):
            d = math.hypot(x - cx, y - 15.5)
            if r - thick <= d < r and y <= 15:
                set_px(rows, x, y, fill)
            if r - 1 <= d < r and y <= 15 and y < 15.5 - r * 0.5:
                set_px(rows, x, y, crest)
    return rows


w0 = wave(7.5, 7.5, 2.5, "W", "m")
for x in range(1, 5):
    set_px(w0, x, 15, "M")
put("wave/0", w0)
w1 = wave(8.5, 8.0, 2.5, "W", "m")
for x in range(0, 6, 2):
    set_px(w1, x, 15, "M")
put("wave/1", w1)

# warn: hazard diamond with blinking exclamation
def diamond(fill, bang):
    rows = blank(16, 16)
    for y in range(16):
        for x in range(16):
            m = abs(x - 7.5) + abs(y - 7.5)
            if m <= 7:
                set_px(rows, x, y, fill)
            if 6 < m <= 7:
                set_px(rows, x, y, "K")
            if 4.5 < m <= 5.5 and (x + y) % 2 == 0:
                set_px(rows, x, y, "K")
    if bang:
        for y in range(4, 9):
            set_px(rows, 7, y, "K")
            set_px(rows, 8, y, "K")
        set_px(rows, 7, 10, "K")
        set_px(rows, 8, 10, "K")
    return rows


put("warn/0", diamond("Y", True))
put("warn/1", diamond("y", False))
