#!/usr/bin/env python3
"""Compare UI RGB pixels (not RGBA alpha bounding boxes) and record dimensions."""
from pathlib import Path
import json
import math
import re
from PIL import Image, ImageChops

repo = Path(__file__).resolve().parents[3]
root = repo / 'build/ui-audit/r238'
results = {}
for tag in ['800x600', '1280x800', '800x600@2x']:
    directory = root / tag
    log = (root / f'size-{tag}.log').read_text()
    match = re.search(r'uiaudit fit=budget:True logical=(\d+)x(\d+) dpi=([\d.]+)', log)
    assert match, f'Missing successful viewport fit in {tag}'
    width, height, scale = int(match[1]), int(match[2]), float(match[3])
    comparisons = {}
    for tab in ['graphics', 'sound', 'play']:
        images = [Image.open(directory / f'uisurvey-audit-options-{tab}{suffix}.png').convert('RGB')
                  for suffix in ['', '-before-refresh', '-click']]
        # MainPanel starts at logical x220 and uses the bottom native100px.
        # Disregard the world, whose first capture may warm up render caches.
        box = (math.ceil(220 * scale), math.ceil((height - 100) * scale),
               min(images[0].width, math.floor(min(1024, width) * scale)),
               min(images[0].height, math.floor(height * scale)))
        comparisons[tab] = {
            'panel_rgb_identical': all(ImageChops.difference(images[0], other).crop(box).getbbox() is None
                                       for other in images[1:]),
            'whole_frame_refresh_diff': ImageChops.difference(images[0], images[1]).getbbox(),
            'whole_frame_click_diff': ImageChops.difference(images[0], images[2]).getbbox(),
            'panel_crop': box
        }
        assert comparisons[tab]['panel_rgb_identical'], (tag, tab)
    results[tag] = {
        'captures': len(list(directory.glob('*.png'))),
        'physical_dimensions': sorted({Image.open(p).size for p in directory.glob('*.png')}),
        'logical_dimensions': [width, height], 'effective_scale': scale,
        'options': comparisons
    }
output = repo / 'tools/iff-dump/r238/size-image-checks.json'
output.write_text(json.dumps(results, indent=2) + '\n')
print(json.dumps(results, indent=2))
