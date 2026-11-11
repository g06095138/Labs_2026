#!/usr/bin/env python3
"""Create a portable source archive, including hidden VS Code launch settings."""
from pathlib import Path
import zipfile

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "LR3_Telecom.zip"
EXCLUDED = {"bin", "obj", ".vs", ".git", "__pycache__", "TestResults"}

with zipfile.ZipFile(OUTPUT, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
    for path in sorted(ROOT.rglob("*")):
        relative = path.relative_to(ROOT)
        if (not path.is_file() or path == OUTPUT or any(part in EXCLUDED for part in relative.parts)
                or path.name == ".DS_Store"):
            continue
        archive.write(path, Path("LR3_Telecom") / relative)

with zipfile.ZipFile(OUTPUT) as archive:
    assert archive.testzip() is None
    files = set(archive.namelist())
    for required in ["TelecomFinance.sln", ".vscode/launch.json", "README.md",
                     "src/TelecomFinance.Console/Program.cs", "docs/requirements.md",
                     "docs/design.md", "docs/diagrams/domain.svg",
                     "reports/ЛР3_Оператор_связи.docx"]:
        assert "LR3_Telecom/" + required in files, required
    print(f"{OUTPUT.name}: {len(files)} files; {OUTPUT.stat().st_size} bytes")
