"""
Draws the main menu's "x CO-OP ANYTHING" line (COTLMP/Assets/coop-anything.png, built into the mod) from the
cover (Media/cover.png): its cream letters and red cross are taken out of the picture and drawn again on
nothing, with a black outline as the cover has.

    python tools/make-menu-title.py

Needs Pillow.
"""
import os

from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
COVER = os.path.join(ROOT, "Media", "cover.png")
OUT = os.path.join(ROOT, "COTLMP", "Assets", "coop-anything.png")

# Where the line is on the 1280 x 720 cover
BOX = (418, 344, 852, 399)
CREAM = (243, 236, 222)
RED = (214, 32, 38)
INK = (16, 10, 12)


def keep_large(Mask, Least):
    """The mask without its pieces smaller than Least pixels"""
    W, H = Mask.size
    Px = Mask.load()
    Seen = bytearray(W * H)
    for Y0 in range(H):
        for X0 in range(W):
            if Px[X0, Y0] == 0 or Seen[Y0 * W + X0]:
                continue
            Piece, Todo = [], [(X0, Y0)]
            Seen[Y0 * W + X0] = 1
            while Todo:
                X, Y = Todo.pop()
                Piece.append((X, Y))
                for Nx, Ny in ((X + 1, Y), (X - 1, Y), (X, Y + 1), (X, Y - 1)):
                    if 0 <= Nx < W and 0 <= Ny < H and Px[Nx, Ny] and not Seen[Ny * W + Nx]:
                        Seen[Ny * W + Nx] = 1
                        Todo.append((Nx, Ny))
            if len(Piece) < Least:
                for X, Y in Piece:
                    Px[X, Y] = 0
    return Mask


def main():
    Cover = Image.open(COVER).convert("RGB")
    Part = Cover.crop(BOX)
    Scale = 4
    Part = Part.resize((Part.width * Scale, Part.height * Scale), Image.LANCZOS)
    W, H = Part.size
    Pixels = Part.load()

    Letters = Image.new("L", (W, H), 0)
    Cross = Image.new("L", (W, H), 0)
    L, C = Letters.load(), Cross.load()
    for Y in range(H):
        for X in range(W):
            R, G, B = Pixels[X, Y]
            Bright = (R + G + B) / 3
            if Bright > 175 and max(R, G, B) - min(R, G, B) < 60:
                L[X, Y] = 255
            elif R > 150 and G < 90 and B < 90:
                C[X, Y] = 255

    # Smooth edges, then the outline around both, as on the cover
    Letters = Letters.filter(ImageFilter.GaussianBlur(1.2)).point(lambda A: 255 if A > 110 else 0)
    Cross = Cross.filter(ImageFilter.GaussianBlur(1.2)).point(lambda A: 255 if A > 110 else 0)
    # Specks of the picture behind (a bit of the lamb, a candle) aren't letters
    Letters = keep_large(Letters, 400)
    Cross = keep_large(Cross, 400)
    Shape = Image.new("L", (W, H), 0)
    Shape.paste(255, mask=Letters)
    Shape.paste(255, mask=Cross)
    Outline = Shape.filter(ImageFilter.MaxFilter(13)).filter(ImageFilter.GaussianBlur(1.5))

    Out = Image.new("RGBA", (W, H), INK + (0,))
    Out.putalpha(Outline)
    Out.paste(Image.new("RGBA", (W, H), CREAM + (255,)), mask=Letters.filter(ImageFilter.GaussianBlur(0.8)))
    Out.paste(Image.new("RGBA", (W, H), RED + (255,)), mask=Cross.filter(ImageFilter.GaussianBlur(0.8)))
    Out = Out.crop(Out.getbbox())
    Out = Out.resize((Out.width // 2, Out.height // 2), Image.LANCZOS)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    Out.save(OUT, optimize=True)
    print(OUT, Out.size)


if __name__ == "__main__":
    main()
