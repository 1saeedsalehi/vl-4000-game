#!/usr/bin/env python3
"""Builds the Phaser sprite atlas (PNG + JSON hash) from text pixel-maps.

Usage:  python3 tools/atlas/build_atlas.py [--out DIR] [--preview]

Stdlib only (zlib/struct). Frames come from frames_*.py modules in this folder,
each exporting FRAMES: dict[name -> list[str]]. Expected cell sizes are checked
against CATALOGUE below, which mirrors docs/ARCHITECTURE.md §7.
"""

import argparse
import importlib
import json
import os
import re
import struct
import sys
import zlib

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from palette import PALETTE, parse_color  # noqa: E402
from pixelmap import validate  # noqa: E402

# name pattern -> (width, height); patterns are regexes matched against the full name.
CATALOGUE = [
    (r"player/.*", (16, 20)),
    (r"spinner/spin/[0-7]", (16, 16)),
    (r"expired/.*", (16, 16)),
    (r"bug/.*", (16, 16)),
    (r"token/spin/[0-3]", (16, 16)),
    (r"(coffee|cache|breaker|rollback)/idle/[01]", (16, 16)),
    (r"(pop|sparkle)/[0-2]", (16, 16)),
    (r"(wave|warn)/[01]", (16, 16)),
    (r"bullet/[0-3]", (8, 8)),
    (r"terminal/.*", (16, 32)),
    (r"repair/.*", (16, 32)),
    (r"icon/.*", (8, 8)),
    (r"checkpoint/.*", (16, 32)),
    (r"exit/.*", (32, 32)),
    (r"boss/.*", (32, 32)),
    (r"tiles/.*", (16, 16)),
    (r"deco/(monitor|lamp|poster)", (16, 16)),
    (r"deco/(server|coffee_machine)", (16, 32)),
    (r"bg/.*", (64, 64)),
]

REQUIRED = (
    [f"player/idle/{i}" for i in range(2)] + [f"player/run/{i}" for i in range(4)]
    + ["player/jump/0", "player/fall/0", "player/hurt/0", "player/dead/0", "player/interact/0"]
    + [f"player/cheer/{i}" for i in range(2)]
    + [f"spinner/spin/{i}" for i in range(8)]
    + ["expired/float/0", "expired/float/1", "expired/warn/0"]
    + ["bug/walk/0", "bug/walk/1", "bug/squash/0"]
    + [f"token/spin/{i}" for i in range(4)]
    + [f"{k}/idle/{i}" for k in ("coffee", "cache", "breaker", "rollback") for i in range(2)]
    + [f"{k}/{i}" for k in ("pop", "sparkle") for i in range(3)]
    + [f"{k}/{i}" for k in ("wave", "warn") for i in range(2)]
    + [f"bullet/{i}" for i in range(4)]
    + ["terminal/locked/0", "terminal/locked/1", "terminal/active/0", "terminal/active/1", "terminal/denied/0"]
    + ["repair/idle/0", "repair/idle/1", "repair/ok/0", "repair/bad/0", "repair/cooldown/0"]
    + ["icon/identity", "icon/content", "icon/streaming"]
    + ["checkpoint/off/0", "checkpoint/on/0", "checkpoint/on/1"]
    + ["exit/locked/0", "exit/locked/1", "exit/open/0", "exit/open/1"]
    + [f"boss/spin/{i}" for i in range(8)] + ["boss/stun/0", "boss/stun/1", "boss/hit/0", "boss/charge/0", "boss/charge/1", "boss/dead/0"]
    + ["tiles/floor_top", "tiles/floor", "tiles/platform", "tiles/wall", "tiles/spike", "tiles/rack_top", "tiles/rack", "tiles/cable", "tiles/pillar", "tiles/glass"]
    + ["deco/monitor", "deco/server", "deco/lamp", "deco/poster", "deco/coffee_machine"]
    + ["bg/sky", "bg/racks", "bg/city"]
)


def expected_size(name):
    for pattern, size in CATALOGUE:
        if re.fullmatch(pattern, name):
            return size
    return None


def load_frames():
    frames = {}
    folder = os.path.dirname(os.path.abspath(__file__))
    modules = sorted(f[:-3] for f in os.listdir(folder) if f.startswith("frames_") and f.endswith(".py"))
    for module_name in modules:
        module = importlib.import_module(module_name)
        for name, rows in module.FRAMES.items():
            if name in frames:
                raise ValueError(f"Duplicate frame '{name}' in {module_name}")
            size = expected_size(name)
            if size is None:
                raise ValueError(f"Frame '{name}' ({module_name}) is not in the catalogue")
            frames[name] = validate(name, rows, *size)
    return frames, modules


def pack(frames, max_width=512, padding=1):
    """Shelf packing sorted by height then width. Returns (placements, width, height)."""
    items = sorted(frames.items(), key=lambda kv: (-len(kv[1]), -len(kv[1][0]), kv[0]))
    placements = {}
    x = y = shelf_h = 0
    for name, rows in items:
        w, h = len(rows[0]), len(rows)
        if x + w + padding > max_width:
            x = 0
            y += shelf_h + padding
            shelf_h = 0
        placements[name] = (x, y, w, h)
        x += w + padding
        shelf_h = max(shelf_h, h)
    height = y + shelf_h
    width = max_width

    def pow2(v):
        p = 1
        while p < v:
            p *= 2
        return p

    return placements, pow2(width), pow2(height)


def rasterize(frames, placements, width, height, scale=1):
    pixels = bytearray(width * height * scale * scale * 4)
    stride = width * scale * 4
    for name, (px, py, w, h) in placements.items():
        rows = frames[name]
        for yy in range(h):
            for xx in range(w):
                color = parse_color(PALETTE[rows[yy][xx]])
                if color[3] == 0:
                    continue
                for sy in range(scale):
                    for sx in range(scale):
                        ox = ((px + xx) * scale + sx) * 4
                        oy = ((py + yy) * scale + sy) * stride
                        pixels[oy + ox:oy + ox + 4] = bytes(color)
    return pixels


def write_png(path, width, height, rgba):
    def chunk(tag, data):
        c = struct.pack(">I", len(data)) + tag + data
        return c + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    stride = width * 4
    raw = bytearray()
    for y in range(height):
        raw.append(0)  # filter: none
        raw.extend(rgba[y * stride:(y + 1) * stride])
    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(raw), 9))
    png += chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)


def write_json(path, placements, width, height, image_name):
    frames = {}
    for name, (x, y, w, h) in sorted(placements.items()):
        frames[name] = {
            "frame": {"x": x, "y": y, "w": w, "h": h},
            "rotated": False,
            "trimmed": False,
            "spriteSourceSize": {"x": 0, "y": 0, "w": w, "h": h},
            "sourceSize": {"w": w, "h": h},
        }
    data = {
        "frames": frames,
        "meta": {
            "app": "tools/atlas/build_atlas.py",
            "image": image_name,
            "format": "RGBA8888",
            "size": {"w": width, "h": height},
            "scale": "1",
        },
    }
    with open(path, "w") as f:
        json.dump(data, f, indent=1, sort_keys=True)


def write_contact_sheet(path, frames, scale=3, cell=40, columns=12):
    """Grid of all frames (for visual QA), each centred in a cell with a dark border."""
    names = sorted(frames)
    rows_count = (len(names) + columns - 1) // columns
    width, height = columns * cell, rows_count * cell
    pixels = bytearray(width * height * 4)
    stride = width * 4
    for i in range(width * height):
        pixels[i * 4:i * 4 + 4] = bytes((26, 15, 46, 255))
    for idx, name in enumerate(names):
        rows = frames[name]
        w, h = len(rows[0]), len(rows)
        s = scale if max(w, h) * scale <= cell - 2 else max(1, (cell - 2) // max(w, h))
        cx = (idx % columns) * cell + (cell - w * s) // 2
        cy = (idx // columns) * cell + (cell - h * s) // 2
        for yy in range(h):
            for xx in range(w):
                color = parse_color(PALETTE[rows[yy][xx]])
                if color[3] == 0:
                    continue
                a = color[3] / 255
                for sy in range(s):
                    for sx in range(s):
                        o = (cy + yy * s + sy) * stride + (cx + xx * s + sx) * 4
                        bg = pixels[o:o + 3]
                        blended = bytes(int(color[i] * a + bg[i] * (1 - a)) for i in range(3)) + b"\xff"
                        pixels[o:o + 4] = blended
        # cell border
        x0, y0 = (idx % columns) * cell, (idx // columns) * cell
        for xx in range(cell):
            for (bx, by) in ((x0 + xx, y0), (x0 + xx, y0 + cell - 1)):
                o = by * stride + bx * 4
                pixels[o:o + 4] = bytes((59, 31, 107, 255))
        for yy in range(cell):
            for (bx, by) in ((x0, y0 + yy), (x0 + cell - 1, y0 + yy)):
                o = by * stride + bx * 4
                pixels[o:o + 4] = bytes((59, 31, 107, 255))
    write_png(path, width, height, pixels)
    return names


def main():
    parser = argparse.ArgumentParser()
    default_out = os.path.join(os.path.dirname(__file__), "..", "..", "src", "BlazorUI", "wwwroot", "games", "disk001", "assets")
    parser.add_argument("--out", default=default_out)
    parser.add_argument("--preview", action="store_true", help="also write preview PNGs")
    parser.add_argument("--preview-dir", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "preview"))
    parser.add_argument("--check", action="store_true", help="only validate frames")
    args = parser.parse_args()

    frames, modules = load_frames()
    missing = [name for name in REQUIRED if name not in frames]
    print(f"Loaded {len(frames)} frames from {', '.join(modules) or 'no modules'}")
    if missing:
        print(f"Missing {len(missing)} required frames:")
        for name in missing:
            print(f"  - {name}")
    if args.check:
        sys.exit(1 if missing else 0)

    placements, width, height = pack(frames)
    os.makedirs(args.out, exist_ok=True)
    png_path = os.path.join(args.out, "sprites.png")
    json_path = os.path.join(args.out, "sprites.json")
    write_png(png_path, width, height, rasterize(frames, placements, width, height))
    write_json(json_path, placements, width, height, "sprites.png")
    print(f"Wrote {png_path} ({width}x{height}) and {json_path}")

    if args.preview:
        preview_dir = args.preview_dir
        os.makedirs(preview_dir, exist_ok=True)
        write_png(os.path.join(preview_dir, "atlas_x3.png"), width * 3, height * 3,
                  rasterize(frames, placements, width, height, scale=3))
        names = write_contact_sheet(os.path.join(preview_dir, "sheet.png"), frames)
        with open(os.path.join(preview_dir, "sheet_index.txt"), "w") as f:
            for idx, name in enumerate(names):
                f.write(f"{idx // 12},{idx % 12}\t{name}\n")
        print(f"Wrote previews to {preview_dir}")

    sys.exit(1 if missing else 0)


if __name__ == "__main__":
    main()
