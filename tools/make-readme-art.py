"""
Draws the README pictures (Media/readme) from the game's own sprites and font.

    python tools/make-readme-art.py "<game folder>" [version]

Needs Pillow, UnityPy and fontTools. Reads sprites from the game's data (the main asset files and
the UI bundle) into obj/readme-art, then draws the download button, the section titles and the
dividers. Nothing of the game is kept in the repository but the drawn pictures.
"""
import os
import random
import re
import sys

import UnityPy
from fontTools.ttLib import TTFont
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GAME = sys.argv[1]
VERSION = sys.argv[2] if len(sys.argv) > 2 else "0.2.0"
DATA = os.path.join(GAME, "Cult Of The Lamb_Data")
CACHE = os.path.join(ROOT, "obj", "readme-art")
OUT = os.path.join(ROOT, "Media", "readme")

# The UI bundle with the co-op indicators (Lamb and Goat heads)
UI_BUNDLE = "18ea63788e02ba13c11efede084882b9.bundle"
SPRITES = {"Crown", "Crown_White_Eye", "Divider Grey", "Pentagram-Star_1", "Pentagram-Star",
           "Candle_Flame_Small", "CandleLight", "Coop_Indicator_Lamb", "Coop_Indicator_Goat"}

CREAM = (243, 233, 214)
INK = (17, 10, 12)
RED = (196, 30, 42)
DEEP = (110, 10, 20)


def extract():
    os.makedirs(CACHE, exist_ok=True)
    if all(os.path.exists(os.path.join(CACHE, Name + ".png")) for Name in SPRITES) and \
            os.path.exists(os.path.join(CACHE, "font.ttf")):
        return
    Files = [os.path.join(DATA, "resources.assets"), os.path.join(DATA, "sharedassets0.assets"),
             os.path.join(DATA, "StreamingAssets", "aa", "StandaloneWindows64", UI_BUNDLE)]
    for File in Files:
        Env = UnityPy.load(File)
        for Object in Env.objects:
            if Object.type.name == "Font":
                Font = Object.read()
                if Font.m_Name == "6096-Bold" and Font.m_FontData:
                    open(os.path.join(CACHE, "font.ttf"), "wb").write(bytes(Font.m_FontData))
            elif Object.type.name == "Sprite" and Object.peek_name() in SPRITES:
                try:
                    Object.read().image.save(os.path.join(CACHE, Object.peek_name() + ".png"))
                except Exception:
                    pass  # an atlas copy without its texture here; the bundle has the other one


def sprite(Name):
    return Image.open(os.path.join(CACHE, Name + ".png")).convert("RGBA")


class Writer:
    """The game's font, with a Windows serif for the letters it lacks (Cyrillic)"""

    def __init__(self, Size):
        self.Main = ImageFont.truetype(os.path.join(CACHE, "font.ttf"), Size)
        self.Map = TTFont(os.path.join(CACHE, "font.ttf")).getBestCmap()
        Windows = os.environ.get("WINDIR", r"C:\Windows")
        self.Other = ImageFont.truetype(os.path.join(Windows, "Fonts", "georgiab.ttf"), int(Size * 0.86))

    def font(self, Text):
        return self.Main if all(ord(C) in self.Map or C == " " for C in Text) else self.Other

    def width(self, Text, Spacing=0):
        Font = self.font(Text)
        return sum(Font.getlength(C) + Spacing for C in Text) - Spacing

    def draw(self, Draw, Center, Text, Fill, Shadow=None, Spacing=0):
        Font = self.font(Text)
        if Spacing:
            Widths = [Font.getlength(C) + Spacing for C in Text]
            X = Center[0] - (sum(Widths) - Spacing) / 2
            for C, W in zip(Text, Widths):
                if Shadow:
                    Draw.text((X + 4, Center[1] + 5), C, font=Font, fill=Shadow, anchor="lm")
                Draw.text((X, Center[1]), C, font=Font, fill=Fill, anchor="lm")
                X += W
            return
        if Shadow:
            Draw.text((Center[0] + 4, Center[1] + 5), Text, font=Font, fill=Shadow, anchor="mm")
        Draw.text(Center, Text, font=Font, fill=Fill, anchor="mm")


def ragged(W, H, Inset, Amp, Seed, Step=14):
    """A rectangle with torn paper edges, as the game's panels"""
    R = random.Random(Seed)
    Points = []
    def Edge(X0, Y0, X1, Y1, Nx, Ny):
        Length = max(abs(X1 - X0), abs(Y1 - Y0))
        for I in range(0, int(Length), Step):
            T = I / Length
            Off = R.uniform(-Amp, Amp) * (0.4 if R.random() < 0.7 else 1.0)
            Points.append((X0 + (X1 - X0) * T + Nx * Off, Y0 + (Y1 - Y0) * T + Ny * Off))
    Edge(Inset, Inset, W - Inset, Inset, 0, 1)
    Edge(W - Inset, Inset, W - Inset, H - Inset, 1, 0)
    Edge(W - Inset, H - Inset, Inset, H - Inset, 0, 1)
    Edge(Inset, H - Inset, Inset, Inset, 1, 0)
    return Points


def tint(Image_, Color):
    Solid = Image.new("RGBA", Image_.size, Color + (255,))
    Solid.putalpha(Image_.getchannel("A"))
    return Solid


def finish(Big, Name, Scale=2):
    Small = Big.resize((Big.width // Scale, Big.height // Scale), Image.LANCZOS)
    Small.save(os.path.join(OUT, Name), optimize=True)
    print(Name, Small.size)


def download_button():
    # Drawn at 2x of the file, the file is 2x of what the README shows
    W, H = 2800, 640
    Big = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    Draw = ImageDraw.Draw(Big)

    # Torn black panel, then the red one inside it with a red-to-deep shade
    Draw.polygon(ragged(W, H, 30, 18, 7), fill=INK + (255,))
    Inner = ragged(W, H, 74, 10, 11)
    Mask = Image.new("L", (W, H), 0)
    ImageDraw.Draw(Mask).polygon(Inner, fill=255)
    Shade = Image.new("RGBA", (W, H))
    ShadeDraw = ImageDraw.Draw(Shade)
    for Y in range(H):
        T = abs(Y - H * 0.42) / (H * 0.6)
        Color = tuple(int(RED[I] * (1 - T) + DEEP[I] * T) for I in range(3))
        ShadeDraw.line((0, Y, W, Y), fill=Color + (255,))
    Big.paste(Shade, (0, 0), Mask)

    # The ritual circle behind the words, faint
    Circle = tint(sprite("Pentagram-Star_1"), (0, 0, 0)).resize((900, 900), Image.LANCZOS)
    Circle.putalpha(Circle.getchannel("A").point(lambda A: A * 0.22))
    Layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    Layer.paste(Circle, (W // 2 - 450, H // 2 - 450), Circle)
    Layer.putalpha(ImageChops.multiply(Layer.getchannel("A"), Mask))
    Big.alpha_composite(Layer)

    # Cream hairline just inside the red
    Line = Image.new("L", (W, H), 0)
    ImageDraw.Draw(Line).line(ragged(W, H, 100, 5, 13) + [ragged(W, H, 100, 5, 13)[0]], fill=255, width=5)
    Big.paste(Image.new("RGBA", (W, H), CREAM + (170,)), (0, 0), Line)

    Draw = ImageDraw.Draw(Big)
    # Lamb and Goat on either side, the way the game marks the two co-op players
    for Name, X, Flip in (("Coop_Indicator_Lamb", 150, False), ("Coop_Indicator_Goat", W - 150 - 360, True)):
        Head = sprite(Name)
        Head = Head.resize((360, int(Head.height * 360 / Head.width)), Image.LANCZOS)
        if Flip:
            Head = Head.transpose(Image.FLIP_LEFT_RIGHT)
        Big.alpha_composite(Head, (X, H // 2 - Head.height // 2 + 10))

    Title = Writer(190)
    Title.draw(Draw, (W // 2, H // 2 - 50), "DOWNLOAD NOW", CREAM, Shadow=INK, Spacing=6)
    Sub = Writer(60)
    Sub.draw(Draw, (W // 2, H // 2 + 120), f"WINDOWS INSTALLER  +  v{VERSION}  +  FREE", CREAM, Spacing=6)
    finish(Big, "download.png")


def section(Name, Text, Seed):
    W, H = 3200, 400
    Big = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    Title = Writer(170)
    Mark = tint(sprite("Crown_White_Eye"), CREAM)
    Mark = Mark.resize((140, int(Mark.height * 140 / Mark.width)), Image.LANCZOS)
    # Torn black panel: crown, words, crown
    Half = int(Title.width(Text, 10)) // 2 + 60 + Mark.width + 50
    Left, Right = W // 2 - Half, W // 2 + Half
    Draw = ImageDraw.Draw(Big)
    Draw.polygon([(X + Left - 20, Y + 40) for X, Y in ragged(Right - Left + 40, H - 80, 20, 14, Seed)],
                 fill=INK + (255,))
    # The game's divider runs out from both sides of the panel, in red
    Divider = tint(sprite("Divider Grey"), RED)
    Divider = Divider.resize((int(Divider.width * 1.6), int(Divider.height * 1.6)), Image.LANCZOS)
    Arm = Divider.crop((0, 0, int(Divider.width * 0.36), Divider.height))
    Big.alpha_composite(Arm, (Left - 40 - Arm.width, H // 2 - Arm.height // 2))
    Arm = Arm.transpose(Image.FLIP_LEFT_RIGHT)
    Big.alpha_composite(Arm, (Right + 40, H // 2 - Arm.height // 2))
    # The crown with the game's red eye on either side of the words
    for X in (Left + 50, Right - 50 - Mark.width):
        Y = H // 2 - Mark.height // 2
        Big.alpha_composite(Mark, (X, Y))
        Cx, Cy = X + Mark.width // 2, Y + int(Mark.height * 0.6)
        Draw.ellipse((Cx - 28, Cy - 16, Cx + 28, Cy + 16), fill=RED + (255,))
        Draw.ellipse((Cx - 7, Cy - 14, Cx + 7, Cy + 14), fill=INK + (255,))
    Title.draw(Draw, (W // 2, H // 2 - 4), Text, CREAM, Spacing=10)
    finish(Big, Name)


def heads():
    for Name, File in (("Coop_Indicator_Lamb", "lamb.png"), ("Coop_Indicator_Goat", "goat.png")):
        Head = sprite(Name)
        Head.thumbnail((192, 192), Image.LANCZOS)
        Head.save(os.path.join(OUT, File), optimize=True)
        print(File, Head.size)


def divider():
    Divider = tint(sprite("Divider Grey"), RED)
    Divider = Divider.resize((Divider.width * 3 // 2, Divider.height * 3 // 2), Image.LANCZOS)
    Divider.save(os.path.join(OUT, "divider.png"), optimize=True)
    print("divider.png", Divider.size)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    extract()
    download_button()
    for Index, (File, Text) in enumerate([
            ("title-play.png", "BEGIN THE RITUAL"),
            ("title-features.png", "WHAT THE FLOCK SHARES"),
            ("title-needs.png", "WHAT YOU NEED"),
            ("title-russian.png", "ПО-РУССКИ"),
            ("title-devout.png", "FOR THE DEVOUT"),
            ("title-credits.png", "CREDITS")]):
        section(File, Text, 20 + Index)
    heads()
    divider()
