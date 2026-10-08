"""One-time transport of the reviewed source patch; removed after integration."""
from pathlib import Path
import base64
import gzip
import hashlib
import subprocess

root = Path(__file__).resolve().parent
packed = base64.b64decode(''.join((root / f'part{i}.txt').read_text().strip() for i in range(4)), validate=True)
assert hashlib.sha256(packed).hexdigest() == '93f09ec9dcf638904bb228da8e997896f54e92d4fdb1baee9a556299c06f9bb2', 'Source transport checksum mismatch'
patch = gzip.decompress(packed)
subprocess.run(['git', 'apply', '--check', '-'], input=patch, check=True)
subprocess.run(['git', 'apply', '-'], input=patch, check=True)
subprocess.run(['git', 'diff', '--check'], check=True)
print('Applied checksum-verified navigation and rendering source.')
