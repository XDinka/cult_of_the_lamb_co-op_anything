"""Draws the installer icon (a red crown in a bone ring) into installer/icon.ico, no libraries needed.

Usage: python -I tools/make-icon.py installer/icon.ico
"""
import math
import struct
import sys
import zlib

NIGHT = (18, 10, 12)
BONE = (242, 230, 207)
RED_TOP = (226, 58, 68)
RED_BOTTOM = (110, 16, 24)


def inside(poly, x, y):
    hit = False
    j = len(poly) - 1
    for i in range(len(poly)):
        xi, yi = poly[i]
        xj, yj = poly[j]
        if (yi > y) != (yj > y) and x < (xj - xi) * (y - yi) / (yj - yi) + xi:
            hit = not hit
        j = i
    return hit


def near_segment(px, py, a, b, width):
    ax, ay = a
    bx, by = b
    dx, dy = bx - ax, by - ay
    t = max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy)))
    return math.hypot(px - ax - t * dx, py - ay - t * dy) <= width


def sample(x, y):
    """Colour and alpha of one point of the unit icon (0..1 both ways)."""
    cx, cy = 0.5, 0.5
    r = math.hypot(x - cx, y - cy)
    w, h = 0.56, 0.4
    left, top = cx - w / 2, cy - h * 0.62
    bottom = top + h
    crown = [(left, bottom), (left - w * 0.04, top + h * 0.18), (left + w * 0.2, top + h * 0.5), (left + w * 0.3, top - h * 0.02),
             (left + w * 0.5, top + h * 0.42), (left + w * 0.7, top - h * 0.02), (left + w * 0.8, top + h * 0.5),
             (left + w * 1.04, top + h * 0.18), (left + w, bottom)]
    edge = 0.022
    for i in range(len(crown)):
        if near_segment(x, y, crown[i], crown[(i + 1) % len(crown)], edge):
            return BONE, 255
    if inside(crown, x, y):
        eye_y = bottom - h * 0.2
        if ((x - cx) / 0.1) ** 2 + ((y - eye_y) / 0.05) ** 2 <= 1:
            return (NIGHT if abs(x - cx) < 0.014 else BONE), 255
        t = (y - top) / h
        return tuple(int(RED_TOP[k] + (RED_BOTTOM[k] - RED_TOP[k]) * t) for k in range(3)), 255
    for i in (1, 3, 5, 7):
        if math.hypot(x - crown[i][0], y - crown[i][1]) <= 0.036:
            return BONE, 255
    if 0.455 <= r <= 0.49:
        return BONE, 255
    if r < 0.455:
        return NIGHT, 255
    return NIGHT, 0


def render(size, ss=4):
    rows = []
    for py in range(size):
        row = bytearray()
        for px in range(size):
            acc = [0, 0, 0, 0]
            for sy in range(ss):
                for sx in range(ss):
                    (cr, cg, cb), a = sample((px + (sx + 0.5) / ss) / size, (py + (sy + 0.5) / ss) / size)
                    acc[0] += cr * a
                    acc[1] += cg * a
                    acc[2] += cb * a
                    acc[3] += a
            n = ss * ss
            alpha = acc[3] / n
            if acc[3]:
                row += bytes((int(acc[0] / acc[3]), int(acc[1] / acc[3]), int(acc[2] / acc[3]), int(alpha)))
            else:
                row += bytes((0, 0, 0, 0))
        rows.append(bytes(row))
    return rows


def png(size, rows):
    raw = b"".join(b"\x00" + row for row in rows)

    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)) + \
        chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")


def main(out):
    sizes = [16, 24, 32, 48, 64, 128, 256]
    images = [png(s, render(s, 4 if s <= 64 else 2)) for s in sizes]
    header = struct.pack("<HHH", 0, 1, len(images))
    offset = 6 + 16 * len(images)
    entries = b""
    for s, data in zip(sizes, images):
        entries += struct.pack("<BBBBHHII", s % 256, s % 256, 0, 0, 1, 32, len(data), offset)
        offset += len(data)
    with open(out, "wb") as f:
        f.write(header + entries + b"".join(images))


if __name__ == "__main__":
    main(sys.argv[1])
