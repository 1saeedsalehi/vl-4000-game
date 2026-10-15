# Sprite atlas tool

All artwork is original and authored as text pixel-maps (one character per
pixel) using the shared palette in `palette.py`. Run:

```sh
python3 tools/atlas/build_atlas.py --preview
```

This writes `src/BlazorUI/wwwroot/games/disk001/assets/sprites.png` + `sprites.json`
(Phaser JSON-hash atlas) and QA previews in `tools/atlas/preview/`.
Frame names and cell sizes are defined in `docs/ARCHITECTURE.md` §7 and enforced by `CATALOGUE`.

Add frames in any `frames_*.py` module:

```python
from pixelmap import frame
FRAMES = {}
FRAMES["coffee/idle/0"] = frame("""
................
......h.h.......
""")
```
