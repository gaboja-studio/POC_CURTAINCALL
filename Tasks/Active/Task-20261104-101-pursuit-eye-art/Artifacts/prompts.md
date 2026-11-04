# GPT image prompts

Built-in image_gen; no API/CLI fallback. Earlier colored/outlined and noisy alpha results were rejected. Final artwork source:

```text
Draw a perfectly clean minimalist game icon animation sheet on a perfectly flat solid black (#000000) background. 8 pure white eye icons in a precise 4-column 2-row grid. No texture, no shading, no gray, no distress, no flecks or speckles. White flat filled shapes, NOT white-filled eyeballs: open eyes have two tapered white curved lids and one round solid white pupil, with the large space between pupil and lids remaining pure black. No extra outline/keyline around the white shapes, no eyelashes, no shadows, no glow, no text. Top row left to right: closed shallow curved lid, slit open, quarter open, half open. Bottom row: 60 percent open, 75 percent open, 90 percent open, fully open. Same icon width in each cell, tips and pupil center aligned within cells, ample blank margin. Serious restrained stealth horror game aesthetic. Pixel clean, vector-like flat white glyphs on black. Wide 2:1 composition.
```

The following image_gen transparency edit was attempted, but rejected for visible residue. User explicitly approved deterministic pixel cleanup instead:

```text
Background extraction ONLY. Preserve the attached eight white eye glyphs exactly, their clean smooth shape, scale, layout and positions. Remove ALL black background pixels, including the black holes INSIDE the eyes, and replace them with genuine alpha transparency. Preserve the opaque WHITE eyelids and pupils; WHITE IS THE FOREGROUND TO KEEP. Black is the background to remove. White foreground must stay solid opaque white, with only smooth antialiasing on its edges. Do not add or redraw anything, do not turn the black eye interiors white. No outlines, no shadows, no glow, no noise, no texture, no checkerboard. Deliver true transparent RGBA sprite atlas of the same eight white glyphs.
```

