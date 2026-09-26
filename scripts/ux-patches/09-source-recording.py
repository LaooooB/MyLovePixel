"""Record application source using contents-only CI permissions."""
from pathlib import Path
import subprocess
import zipfile

# Patch 08 changed build packaging, not application behavior. Keep the checked-in
# workflow unchanged: the build token deliberately cannot edit workflow files.
subprocess.run(['git', 'restore', '--source=HEAD', '--', '.github/workflows/ux-release.yml'], check=True)
# The existing recording step stages tracked changes plus src/tests/docs only.
subprocess.run(['git', 'add', '--', 'scripts/ux-release-ready'], check=True)

with zipfile.ZipFile('release/review-source.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
    for folder in ['src', 'tests', 'docs', '.github', 'scripts']:
        for path in Path(folder).rglob('*'):
            if path.is_file() and not any(p in path.parts for p in ['bin', 'obj', '__pycache__', 'ux-patches']):
                archive.write(path, path.as_posix())
    for name in ['Directory.Build.props', 'Directory.Packages.props', 'MyLovePixel.slnx', 'global.json', 'HANDOFF.md', 'README.md', 'THIRD_PARTY_NOTICES.md']:
        archive.write(name)
