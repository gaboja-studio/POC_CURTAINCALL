"""User-authorized cleanup of GPT-generated artwork; does not touch Unity assets."""
from pathlib import Path
import json
from PIL import Image, ImageOps, ImageDraw

ROOT = Path(__file__).resolve().parent
source = Image.open(ROOT / 'eye-source-gpt.png').convert('RGB')
frames = []
rects = []
for i in range(8):
    col, row = i % 4, i // 4
    region = (round(col * source.width / 4), round(row * source.height / 2),
              round((col + 1) * source.width / 4), round((row + 1) * source.height / 2))
    tile = source.crop(region)
    # The original background is black. Luminance becomes coverage, while RGB
    # becomes pure white so Unity tint never inherits the background color.
    alpha = ImageOps.grayscale(tile).point(lambda v: round(max(0, min(1, (v - 24) / 216)) * 255))
    bbox = alpha.point(lambda v: 255 if v >= 64 else 0).getbbox()
    assert bbox is not None
    alpha = alpha.crop(bbox)
    # Align eyelid tips, not the visible bounding box: a closed curved lid has
    # its visual mass below the pivot while an open eye straddles it.
    edge_width = max(4, round(alpha.width * .035))
    edge_ys = [y for y in range(alpha.height) for x in list(range(edge_width)) + list(range(alpha.width-edge_width, alpha.width)) if alpha.getpixel((x,y)) > 128]
    pivot_y = sorted(edge_ys)[len(edge_ys)//2]
    scale = 192 / alpha.width
    alpha = alpha.resize((192, round(alpha.height * scale)), Image.Resampling.LANCZOS)
    # Lanczos can produce almost-transparent isolated ringing; discard <= 2.
    alpha = alpha.point(lambda v: 0 if v <= 2 else v)
    frame = Image.new('RGBA', (256,256), (255,255,255,0))
    coverage = Image.new('L',(256,256),0)
    offset = (32, round(128-pivot_y*scale))
    coverage.paste(alpha, offset)
    frame.putalpha(coverage)
    frame.save(ROOT / f'eye-{i:02d}.png')
    frames.append(frame)
    rects.append({'name':f'eye-{i:02d}', 'x':col*256, 'y':(1-row)*256,
                  'width':256, 'height':256, 'pivot':[0.5,0.5]})

atlas = Image.new('RGBA',(1024,512),(255,255,255,0))
for i,frame in enumerate(frames):
    atlas.paste(frame, ((i%4)*256,(i//4)*256))
atlas.save(ROOT/'eye-white-atlas.png')
frames[0].save(ROOT/'eye-closed.png')
frames[-1].save(ROOT/'eye-open.png')

contact = Image.new('RGB',(1024,512),'#20272B')
contact.paste(atlas,(0,0),atlas)
contact.save(ROOT/'eye-contact-preview.png')

def preview(frame):
    canvas=Image.new('RGB',(480,320),'#20272B')
    canvas.paste(frame,(112,0),frame)
    small=frame.resize((64,64),Image.Resampling.LANCZOS)
    canvas.paste(small,(208,222),small)
    return canvas

sequence = [frames[0]] + frames + [frames[-1]] + list(reversed(frames))
durations = [800] + [60,60,60,70,60,60,60,70] + [800] + [60,60,60,70,60,60,60,70]
gif_frames=[preview(f) for f in sequence]
gif_frames[0].save(ROOT/'eye-animation-preview.gif',save_all=True,append_images=gif_frames[1:],duration=durations,loop=0,disposal=2)

manifest={'status':'awaiting_user_design_confirmation','atlas':'eye-white-atlas.png',
          'size':[1024,512],'grid':[4,2],'cellSize':[256,256], 'frameCount':8,
          'frameOrder':'left to right, top to bottom', 'unityRectOrigin':'bottom left',
          'frames':rects, 'clip':{'fps':16,'durationSeconds':0.5,'open':list(range(8)), 'close':list(range(7,-1,-1))},
          'color':'RGB always 255,255,255; coverage in alpha',
          'integration':'Not imported into Unity. Color, emission and local-player state are deferred until confirmation.'}
(ROOT/'sprite-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')

assert atlas.mode=='RGBA' and atlas.size==(1024,512)
assert atlas.getextrema()[:3] == ((255,255),(255,255),(255,255))
assert atlas.getextrema()[3]==(0,255)
for frame in frames:
    assert frame.getchannel('A').getbbox() is not None
    assert frame.getpixel((0,0))[3]==0
print('8 frames; atlas 1024x512 RGBA; RGB all white; alpha 0..255; 0.5s open/close preview generated.')
