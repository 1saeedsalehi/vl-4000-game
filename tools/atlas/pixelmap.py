"""Helpers for authoring frames as text pixel-maps."""

from palette import PALETTE


def frame(text):
    """Convert a multi-line string into a list of rows (blank first/last lines ignored)."""
    rows = [line for line in text.split("\n")]
    while rows and rows[0].strip() == "":
        rows.pop(0)
    while rows and rows[-1].strip() == "":
        rows.pop()
    rows = [row.rstrip("\r") for row in rows]
    width = max(len(row) for row in rows)
    return [row.ljust(width, ".") for row in rows]


def blank(width, height, ch="."):
    return [ch * width for _ in range(height)]


def flip_h(rows):
    return [row[::-1] for row in rows]


def flip_v(rows):
    return list(reversed(rows))


def set_px(rows, x, y, ch):
    if 0 <= y < len(rows) and 0 <= x < len(rows[y]):
        row = rows[y]
        rows[y] = row[:x] + ch + row[x + 1:]


def get_px(rows, x, y):
    if 0 <= y < len(rows) and 0 <= x < len(rows[y]):
        return rows[y][x]
    return "."


def rect(rows, x, y, w, h, ch, fill=True):
    for yy in range(y, y + h):
        for xx in range(x, x + w):
            if fill or yy in (y, y + h - 1) or xx in (x, x + w - 1):
                set_px(rows, xx, yy, ch)
    return rows


def line(rows, x0, y0, x1, y1, ch):
    dx, dy = abs(x1 - x0), -abs(y1 - y0)
    sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
    err = dx + dy
    while True:
        set_px(rows, x0, y0, ch)
        if x0 == x1 and y0 == y1:
            break
        e2 = 2 * err
        if e2 >= dy:
            err += dy
            x0 += sx
        if e2 <= dx:
            err += dx
            y0 += sy
    return rows


def circle(rows, cx, cy, radius, ch, fill=False, thickness=1.0):
    """Pixel circle (ring when fill=False). cx/cy may be fractional (e.g. 7.5 for 16px cells)."""
    h = len(rows)
    w = len(rows[0]) if rows else 0
    for yy in range(h):
        for xx in range(w):
            d = ((xx + 0.5 - cx - 0.5) ** 2 + (yy + 0.5 - cy - 0.5) ** 2) ** 0.5
            if fill:
                if d <= radius + 0.01:
                    set_px(rows, xx, yy, ch)
            elif radius - thickness <= d <= radius + 0.01:
                set_px(rows, xx, yy, ch)
    return rows


def blit(base, overlay, dx=0, dy=0):
    """Draw overlay onto base at offset; '.' is transparent."""
    for yy, row in enumerate(overlay):
        for xx, ch in enumerate(row):
            if ch != ".":
                set_px(base, xx + dx, yy + dy, ch)
    return base


def replace(rows, mapping):
    """Recolour: mapping is {from_char: to_char}."""
    return ["".join(mapping.get(ch, ch) for ch in row) for row in rows]


def shift(rows, dx, dy):
    w, h = len(rows[0]), len(rows)
    out = blank(w, h)
    return blit(out, rows, dx, dy)


def validate(name, rows, width, height):
    if len(rows) != height or any(len(r) != width for r in rows):
        raise ValueError(
            f"Frame '{name}' must be {width}x{height}, got {len(rows[0]) if rows else 0}x{len(rows)}")
    for row in rows:
        for ch in row:
            if ch not in PALETTE:
                raise ValueError(f"Frame '{name}' uses unknown palette char '{ch}'")
    return rows
