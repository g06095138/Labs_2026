#!/usr/bin/env python3
"""Rebuild diagrams with PlantUML and export tight vector PDFs.

Requires Java, a local PlantUML jar, Python Playwright and Chromium.
Example: python scripts/render_diagrams.py --plantuml /path/to/plantuml.jar
"""
from __future__ import annotations

import argparse
from pathlib import Path
import re
import shutil
import subprocess
import xml.etree.ElementTree as ET


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plantuml', required=True, type=Path)
    parser.add_argument('--chromium', help='Optional system Chromium executable')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    directory = root / 'docs' / 'diagrams'
    paths = [directory / f'{name}.puml' for name in ('domain', 'components')]
    for fmt in ('svg', 'png'):
        subprocess.run(['java', '-Djava.awt.headless=true', '-jar', str(args.plantuml.resolve()),
                        '-t' + fmt, *map(str, paths)], check=True)

    from playwright.sync_api import sync_playwright
    executable = args.chromium or shutil.which('chromium') or shutil.which('google-chrome')
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(headless=True, executable_path=executable,
                                             args=['--no-sandbox'])
        try:
            for path in paths:
                svg = path.with_suffix('.svg').read_text(encoding='utf-8')
                element = ET.fromstring(svg)
                width = float(re.sub(r'[^\d.]', '', element.attrib['width']))
                height = float(re.sub(r'[^\d.]', '', element.attrib['height']))
                page = browser.new_page(viewport={'width': int(width) + 1, 'height': int(height) + 1})
                page.set_content('<style>html,body{margin:0;padding:0}svg{display:block}@page{margin:0}</style>' + svg)
                page.evaluate('document.fonts.ready')
                page.pdf(path=str(path.with_suffix('.pdf')), width=f'{width}px', height=f'{height}px',
                         print_background=True, margin={'top':'0','bottom':'0','left':'0','right':'0'})
                page.close()
                print(f'{path.stem}: SVG, PNG, PDF generated ({width:g} × {height:g} px)')
        finally:
            browser.close()


if __name__ == '__main__':
    main()
