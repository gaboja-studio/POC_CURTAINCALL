# GPT Image popup kit — v3

Built-in image_gen tool. Workflow: gameplay reference → concept → transparent fill → transparent frame → Unity prefab kit.

Numbers and focus names remain TextMeshPro, not baked pixels.


## Concept

Use case: precise-object-edit / ui-mockup.
Edit target: supplied REAL gameplay screenshot of our oppressive dark circus co-op game. Preserve the entire 1920x1080 game scene, camera, character, lighting, all existing HUD artwork and placement pixel-faithfully.
Change only the audience score feedback: remove the tiny +12 / -5 near bottom balance meter. Put one compact score reaction widget immediately to the RIGHT of the upper-left round circus engagement gauge, approximately x225 y108 width170 height100. The gameplay center must remain completely clear.
Widget style must match the screenshot's existing UI: flat dark desaturated teal charcoal enamel, thin worn ivory/beige outline, barely visible tarnished brass detail, clipped 3px corners, subtle restrained texture. The widget should read as a small extension of the gauge, not a ticket, toast notification, comic label or medieval ornament. No celebration icons, gore, spikes, ribbons or extra illustrations.
At top of widget small label exactly "내 호응", below it large bold warm ivory +12, below in muted coral red −5. Both numbers highly readable with subtle dark edge. Supporting panel only behind these numbers, minimal and narrow. Match existing typography and contrast. No extra concept annotations or legends. This is an image-generated design reference, not a report layout.

## Background fill

Use case: background-extraction / UI production sprite.
Reference image: the small audience score panel beside the upper-left round circus gauge. Generate ONLY its BACKGROUND FILL LAYER for a Unity UI prefab. No text, no numerals, no border, no rivets, no icons.
A flat front-facing compact rectangular piece of dark charcoal desaturated teal enamel (#1c292d), subtle fine aged patina, very restrained texture like the reference's inventory and objective panels. Four tiny chamfered corners. 4:3 aspect rectangle, centered, nearly fills image with a modest transparent margin. All exterior pixels truly alpha-transparent. Interior opaque dark teal. Flat orthographic UI raster sprite, no perspective, no 3D thickness, no cast shadow. Do not include the screenshot, other UI, a display board, checkerboard, labels, or watermark. Suitable for 9-slice scaling; uniform quiet center, no motifs.

## Frame

Use case: background-extraction / UI production sprite.
Input reference 1 is our dark circus game's UI. Input reference 2 is the background fill layer we have just generated.
Generate ONLY the separate FRAME LAYER matching this small score panel for a Unity prefab.
A very thin, flat worn ivory/beige metal rectangular outline with subtly tarnished brass rivets at its four corners, 4:3 aspect, tiny chamfered corners, matching the reference UI. The frame must look like existing inventory-slot/goal-panel borders, not fantasy scrollwork. Interior of rectangle must be COMPLETELY TRANSPARENT, not dark filled. Exterior also completely transparent. No background fill, no cast shadow, no glow, no text or numbers or labels or icons. Front-on orthographic, even line weight, crisp production sprite, full outline centered with modest transparent margin. Keep subdued and usable at 160x110 UI pixels.
