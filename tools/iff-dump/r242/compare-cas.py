"""Compare retained CAS artboards; no image data is written or redistributed."""
from pathlib import Path
from PIL import Image, ImageChops
import json
import math

repo = Path(__file__).resolve().parents[3]
rows = []
for tag, scale in [('800x600', 1), ('1280x800', 1), ('800x600@2x', 1.7033333)]:
    before = Image.open(repo / 'build/ui-audit/r241' / tag / 'uisurvey-casorig-cac.png').convert('RGB')
    after = Image.open(repo / 'build/ui-audit/r242' / tag / 'uisurvey-casorig-cac.png').convert('RGB')
    assert before.size == after.size
    artboard = (0, 0, math.floor(800 * scale), min(before.height, math.floor(600 * scale)))
    difference = ImageChops.difference(before.crop(artboard), after.crop(artboard))
    allowed = (math.floor(618 * scale) - 1, math.floor(145 * scale) - 1,
               math.ceil(718 * scale) + 1, math.ceil(365 * scale) + 1)
    outside = sum(difference.getpixel((x, y)) != (0, 0, 0)
                  and not (allowed[0] <= x < allowed[2] and allowed[1] <= y < allowed[3])
                  for y in range(difference.height) for x in range(difference.width))
    assert outside == 0, (tag, outside)
    rows.append({'run': tag, 'scale': scale, 'changed_bounds': difference.getbbox(),
                 'preview_with_one_pixel_sampling_border': allowed,
                 'changed_outside_sampling_border': outside})
(Path(__file__).resolve().parent / 'cas-size-comparison.json').write_text(json.dumps(rows, indent=2) + '\n')
print(json.dumps(rows, indent=2))
