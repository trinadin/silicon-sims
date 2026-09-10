"""Compare retained pre/post live bottom-strip pixels at each display size."""
from pathlib import Path
import json
from PIL import Image, ImageChops

repo = Path(__file__).resolve().parents[3]
rows = []
for name in ['800x600', '1280x800', '800x600@2x']:
    old = Image.open(repo / 'build/ui-audit/r242' / name / 'uisurvey-live.png').convert('RGBA')
    new = Image.open(repo / 'build/ui-audit/r243' / name / 'uisurvey-live.png').convert('RGBA')
    assert old.size == new.size
    width, height = new.size
    # Logical height is 600 in both 800x600 requests and 800 in the wide run.
    logical_height = 800 if name == '1280x800' else 600
    band = round(100 * height / logical_height)
    box = (0, height-band, width, height)
    diff = ImageChops.difference(old.crop(box), new.crop(box))
    changed = sum(any(pixel) for pixel in diff.get_flattened_data())
    scale = height / logical_height
    needs_box = (round(520*scale), round(height-92*scale), round(700*scale), round(height-2*scale))
    needs_diff = ImageChops.difference(old.crop(needs_box), new.crop(needs_box))
    needs_changed = sum(any(pixel) for pixel in needs_diff.get_flattened_data())
    rows.append({'display': name, 'physical_size': new.size, 'bottom_strip': box,
                 'changed_rgba_pixels': changed, 'equal': changed == 0,
                 'difference_bounds_within_strip': diff.convert('RGB').getbbox(),
                 'needs_interior': needs_box, 'needs_changed_rgba_pixels': needs_changed})
    assert needs_changed == 0, rows[-1]
Path(__file__).with_name('live-hud-comparison.json').write_text(json.dumps(rows, indent=2)+'\n')
print(json.dumps(rows, indent=2))
