#!/usr/bin/env python3
"""Regenerate every Bus Driver asset.

    python3 tools/artgen/main.py
"""
from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import art
import fonts
import icons
import sfx


def main():
    print("fonts")
    print("  ", ", ".join(fonts.build_all()))
    print("art")
    written = art.generate()
    print("  ", len(written), "textures")
    print("icons and splash")
    icons.generate()
    print("sound")
    print("  ", ", ".join(sfx.generate()))


if __name__ == "__main__":
    main()
