"""Shared palette for all pixel-art frames.

Each frame is a list of equal-length strings; every character maps to a colour
below. '.' is transparent. Colours may be #RRGGBB or #RRGGBBAA.
"""

PALETTE = {
    ".": None,          # transparent
    "K": "#000000",     # black (outlines)
    "k": "#0b0613",     # darkest purple (background)
    "n": "#1a0f2e",     # night purple
    "p": "#3b1f6b",     # purple
    "v": "#6a2fd1",     # violet
    "M": "#e6007e",     # magenta accent
    "m": "#ff4fa3",     # hot pink
    "W": "#f6f2ff",     # white
    "l": "#c9c2d6",     # light grey
    "g": "#6e6684",     # grey
    "d": "#2e2940",     # dark grey
    "u": "#8a7bb0",     # lavender metal
    "c": "#35d6ff",     # cyan
    "C": "#1b7fa8",     # dark cyan
    "G": "#3dff8a",     # green
    "e": "#1f8f4a",     # dark green
    "Y": "#ffd43b",     # yellow
    "y": "#b88a12",     # dark yellow
    "O": "#ff8a3b",     # orange
    "R": "#ff3b5c",     # red
    "r": "#8f1c2e",     # dark red
    "S": "#f6c9a8",     # skin
    "B": "#6b3d2a",     # brown
    "b": "#3d2217",     # dark brown
    "h": "#ffffff80",   # half-transparent white (ghost outlines)
    "s": "#00000060",   # soft shadow
    "t": "#e6007e80",   # half-transparent magenta (glow)
    "q": "#35d6ff80",   # half-transparent cyan (glow)
}


def parse_color(value):
    """'#RRGGBB' or '#RRGGBBAA' -> (r, g, b, a)."""
    if value is None:
        return (0, 0, 0, 0)
    value = value.lstrip("#")
    r, g, b = int(value[0:2], 16), int(value[2:4], 16), int(value[4:6], 16)
    a = int(value[6:8], 16) if len(value) == 8 else 255
    return (r, g, b, a)
