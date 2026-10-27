"""Per window parameters for the text tests, and the tag the gatherer puts
on a value measured with them. Mirrors WindowParams.cs, which the server
uses. Everything here is server side: the client never sees the secret, it
receives the derived params and sends tagged values back.

  py -I derive.py <window> [secret]      print the params and tags for a window
  py -I derive.py vectors                print 20 test vectors for the C# check

A window is a 5 second slot: window = floor(unixSeconds / 5). The server
issues params for the current window and accepts the current and previous
one. Each window's params are HMAC-SHA256(secret, window) spread over:
  glyphs   24 characters from ALPHABET
  size     an integer 12 to 23 px (f_metrics scales sans-serif by 27/32)
  shapes   8 glyphs from SHAPE_POOL, no repeats
  walk     two uint32 for m_tanhwalk
"""
import hashlib
import hmac
import json
import struct
import sys

# Letters, digits and the Latin-1 letters every Windows core font carries.
# Nothing that falls back to another font: a fallback font varies per
# install (language packs, Office) and would give genuine users different
# answers. Stays in the BMP so FNV over UTF-16 units equals code points.
ALPHABET = ("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789"
            " .,;:!?@&#%()-+=/")
SHAPE_POOL = list("gQ@&R3Bae$kmswyz%?#") + ["ß", "ą", "ж", "é", "ø", "ç", "ł"]
GLYPHS_LEN = 24


def derive(secret, window):
    """The params for a window, as the gatherer takes them."""
    mac = hmac.new(secret, struct.pack(">Q", window), hashlib.sha256).digest()
    # Stretch to as many bytes as needed with counter blocks.
    stream = b"".join(hmac.new(secret, mac + bytes([i]), hashlib.sha256).digest() for i in range(4))
    glyphs = "".join(ALPHABET[b % len(ALPHABET)] for b in stream[:GLYPHS_LEN])
    size = 12 + stream[GLYPHS_LEN] % 12
    shapes = []
    pool = list(SHAPE_POOL)
    for b in stream[GLYPHS_LEN + 1:GLYPHS_LEN + 9]:
        shapes.append(pool.pop(b % len(pool)))
    w1, w2 = struct.unpack(">II", stream[40:48])
    return {"glyphs": glyphs, "size": size, "shapes": shapes, "walk": [w1, w2]}


def fnv1a32(text):
    h = 0x811C9DC5
    for ch in text:
        h ^= ord(ch)
        h = (h * 0x01000193) & 0xFFFFFFFF
    return f"{h:08x}"


def js_json(v):
    """JSON.stringify's output for the input arrays the gatherer tags."""
    return json.dumps(v, separators=(",", ":"), ensure_ascii=False)


DEFAULTS = {"glyphs": "The quick brown fox jumps over 1234567890 lazy dogs", "size": 16,
            "shapes": ["g", "@", "&", "ß", "ą", "ж", "R", "3"],
            "walk": [2654435769, 2135587861]}


def tag(inputs, defaults):
    used = js_json(inputs)
    return "" if used == js_json(defaults) else "p" + fnv1a32(used) + ":"


def tags(p):
    """The tag each parameterised test puts on its value under params p."""
    return {
        "f_metrics.1": tag([p["glyphs"], p["size"]], [DEFAULTS["glyphs"], DEFAULTS["size"]]),
        "f_segoeshape.2": tag([p["shapes"]], [DEFAULTS["shapes"]]),
        "m_tanhwalk.1": tag([p["walk"]], [DEFAULTS["walk"]]),
    }


if __name__ == "__main__":
    if sys.argv[1] == "vectors":
        secret = b"lab-secret-not-for-production"
        for w in range(20):
            p = derive(secret, 1_000_000 + w * 7919)
            print(json.dumps({"window": 1_000_000 + w * 7919, "params": p, "tags": tags(p)}, ensure_ascii=False))
    else:
        secret = (sys.argv[2] if len(sys.argv) > 2 else "lab-secret-not-for-production").encode()
        p = derive(secret, int(sys.argv[1]))
        print(json.dumps({"params": p, "tags": tags(p)}, ensure_ascii=False, indent=1))
