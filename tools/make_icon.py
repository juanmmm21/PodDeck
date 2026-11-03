# Genera src/PodDeck.App/Assets/PodDeck.ico (y una vista previa PNG) con Pillow:
#   python tools/make_icon.py src/PodDeck.App/Assets/PodDeck.ico preview.png
import sys
from PIL import Image, ImageDraw

S = 1024  # se dibuja grande y se reduce para que los bordes queden suaves

def pod(d, cx, flip):
    # cabeza del auricular y patilla
    hx = cx + (60 if flip else -60)
    d.ellipse([hx - 150, 250, hx + 150, 520], fill=(245, 247, 250, 255))
    sx = cx + (110 if flip else -110)
    d.rounded_rectangle([sx - 55, 400, sx + 55, 800], radius=55, fill=(245, 247, 250, 255))
    # rejilla del altavoz
    gx = hx + (-70 if flip else 70)
    d.ellipse([gx - 42, 330, gx + 42, 414], fill=(40, 46, 60, 255))

img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)
d.rounded_rectangle([24, 24, S - 24, S - 24], radius=220, fill=(30, 36, 51, 255))
pod(d, 340, flip=False)
pod(d, 684, flip=True)
# barra de batería
d.rounded_rectangle([212, 860, 812, 930], radius=35, fill=(70, 78, 96, 255))
d.rounded_rectangle([212, 860, 652, 930], radius=35, fill=(52, 199, 89, 255))

out = sys.argv[1]
sizes = [(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (128, 128), (256, 256)]
img.resize((256, 256), Image.LANCZOS).save(out, sizes=sizes)
img.resize((512, 512), Image.LANCZOS).save(sys.argv[2])
