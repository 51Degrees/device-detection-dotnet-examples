"""Writes jobs.json (the oracle's windows, real gatherer params) and ab.json
(the A/B candidate variants) for the lab server.

  py -I jobs.py [windows]
"""
import hashlib
import hmac
import json
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import derive  # noqa: E402

SECRET = b"lab-secret-not-for-production"
TESTS = ["f_metrics.1", "f_segoeshape.2", "m_tanhwalk.1", "f_segoeshape.1", "f_grid.2"]

ALPHABETS = {
    # letters, digits, ASCII punctuation
    "a": "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,;:!?@&#%()-+=/",
    # plus Latin-1 letters every core Windows font carries
    "b": "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,;:!?@&#%()-+=/"
         "éèüöäßñçøåÉÖ",
    # plus Greek, Cyrillic and symbols that may fall back to another font
    "c": "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,;:!?@&#%()-+=/"
         "éßñαβωЖдя∑∞←☺♥",
}

# id: (fonts, alphabet, length, fractional size)
VARIANTS = {
    "V0_generic4_a24_int": ("generic4", "a", 24, False),
    "V1_generic4_b24_int": ("generic4", "b", 24, False),
    "V2_generic4_c24_int": ("generic4", "c", 24, False),
    "V3_named6_a24_int": ("named6", "a", 24, False),
    "V4_named6_b24_int": ("named6", "b", 24, False),
    "V5_named6_c24_int": ("named6", "c", 24, False),
    "V6_named6_b48_int": ("named6", "b", 48, False),
    "V7_named6_b24_frac": ("named6", "b", 24, True),
    "V8_generic4_b24_frac": ("generic4", "b", 24, True),
    "V9_named3_b24_int": ("named3", "b", 24, False),
    "V10_sysui_b24_int": ("sysui", "b", 24, False),
}


def stream(window, salt):
    mac = hmac.new(SECRET, struct.pack(">Q", window) + salt, hashlib.sha256).digest()
    return b"".join(hmac.new(SECRET, mac + bytes([i]), hashlib.sha256).digest() for i in range(2))


def main():
    n = int(sys.argv[1]) if len(sys.argv) > 1 else 8
    windows = [358_000_000 + i * 131 for i in range(n)]
    jobs = []
    for w in windows:
        p = derive.derive(SECRET, w)
        jobs.append({"window": w, "params": p, "tests": TESTS, "tags": derive.tags(p)})
    json.dump(jobs, open(os.path.join(HERE, "jobs.json"), "w", encoding="utf-8"), ensure_ascii=False)
    ab = []
    for vid, (fonts, alpha, length, frac) in VARIANTS.items():
        rows = []
        for w in windows:
            s = stream(w, vid.encode())
            text = "".join(ALPHABETS[alpha][b % len(ALPHABETS[alpha])] for b in s[:length])
            size = 12 + s[length] % 12
            if frac:
                size += (s[length + 1] % 4) * 0.25
            rows.append({"window": w, "text": text, "size": size})
        ab.append({"id": vid, "fonts": fonts, "windows": rows})
    json.dump(ab, open(os.path.join(HERE, "ab.json"), "w", encoding="utf-8"), ensure_ascii=False)
    print(f"{n} windows, {len(VARIANTS)} variants")


if __name__ == "__main__":
    main()
