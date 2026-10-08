# Apply only the exact local edits tested on the delivered 5572f97 source.
from pathlib import Path
import base64
import hashlib
import lzma
import subprocess

parts = [Path('scripts/color-studio-data.1'), Path('scripts/color-studio-data.2')]
encoded = ''.join(path.read_text(encoding='utf-8').strip() for path in parts)
patch = lzma.decompress(base64.b64decode(encoded, validate=True))
if hashlib.sha256(patch).hexdigest() != '6619746873cf32c9e148b48860106e6c73414c9dca0c2310175209d5560c1727':
    raise RuntimeError('Local source delta checksum mismatch')
subprocess.run(['git', 'apply', '--index', '--whitespace=nowarn', '-'], input=patch, check=True)
for path in parts:
    path.unlink()
print('Applied exact tested Color Studio changes. No user preferences are bundled or overwritten.')
