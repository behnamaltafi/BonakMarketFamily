#!/usr/bin/env python3
"""Static safety checks for the Smartstore-to-BonakMarket source rename."""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
errors = []


def check(condition: bool, message: str) -> None:
    if not condition:
        errors.append(message)


for sln in ("BonakMarket.sln", "BonakMarket.Tools.sln"):
    p = ROOT / sln
    check(p.is_file(), f"Missing solution: {sln}")
    if not p.is_file():
        continue
    contents = p.read_text(encoding="utf-8-sig")
    projects = re.findall(r'^Project\("[^"]+"\) = "[^"]+", "([^"]+\.csproj)"', contents, re.MULTILINE)
    check(bool(projects), f"Could not extract projects from {sln}")
    for project in projects:
        normalized = project.replace("\\", "/")
        check((ROOT / normalized).is_file(), f"{sln}: missing {normalized}")

reverted_ids = {
    "Smartstore.Licensing",
    "Smartstore.LightningCss.Native",
    "Smartstore.EmbeddedSass.Net.Compiler",
    "Smartstore.wkhtmltopdf.Native.$(ProbeRID)",
}
bad_names = {name.replace("Smartstore", "BonakMarket") for name in reverted_ids}
found = set()
for project in ROOT.rglob("*.csproj"):
    if any(p in ("obj", "bin", ".temp") for p in project.parts):
        continue
    content = project.read_text(encoding="utf-8-sig")
    found.update(re.findall(r'<PackageReference\s+Include="([^"]+)"', content))
    for invalid in bad_names:
        check(f'Include="{invalid}"' not in content, f"Unpublished package ID in {project}: {invalid}")
for package in reverted_ids - {"Smartstore.wkhtmltopdf.Native.$(ProbeRID)"}:
    check(package in found, f"Missing published NuGet package reference: {package}")

repo = "https://github.com/behnamaltafi/BonakMarketFamily"
metadata = (ROOT / "src/BonakMarket.Build/BonakMarket.Common.props").read_text(encoding="utf-8")
check(repo in metadata, "Common.props does not use the real repository URL.")
module_props = (ROOT / "src/BonakMarket.Build/BonakMarket.Module.props").read_text(encoding="utf-8")
check("BonakMarket.ModuleBuilder.csproj" in module_props, "ModuleBuilder must be a source project dependency.")
helper = (ROOT / "src/BonakMarket.Build/BonakMarket.ModuleBuilder/src/BonakMarket.ModuleBuilder.csproj").read_text(encoding="utf-8")
check("<TargetFramework>net10.0</TargetFramework>" in helper, "ModuleBuilder must target net10.0.")

for path in [*(ROOT / ".github/workflows").glob("*.yml"), *ROOT.glob("docker-compose*.yml")]:
    content = path.read_text(encoding="utf-8")
    for image in re.findall(r'ghcr\.io/[a-zA-Z0-9_.-]+/[a-zA-Z0-9_.-]+', content):
        check(image == image.lower(), f"GHCR image must be lowercase: {path}: {image}")

for path in [
    ROOT / "src/BonakMarket/Pdf/WkHtml/WkHtmlToPdfConverter.cs",
    ROOT / "build/wkhtml-tests/Probe/Program.cs",
    ROOT / "build/wkhtml-tests/run.sh",
]:
    check(path.is_file(), f"Missing PDF file: {path}")
    if path.is_file():
        check("BonakMarket.wkhtmltopdf.Native" not in path.read_text(encoding="utf-8"),
              f"Native package ID was incorrectly renamed in {path}")

readme = (ROOT / "README.md").read_text(encoding="utf-8-sig")
for asset in re.findall(r'src="(assets/[^"]+)"', readme):
    check((ROOT / asset).is_file(), f"README image not found: {asset}")

if errors:
    for message in errors:
        print("ERROR:", message, file=sys.stderr)
    sys.exit(1)
print("Rename static validation passed.")
