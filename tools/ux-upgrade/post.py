"""Compiler-confirmed integration corrections. Removed after source materialization."""
from pathlib import Path
root = Path(__file__).resolve().parents[2]
p = root / 'src/MyLovePixel.Application/ColorLibraryStore.cs'
s = p.read_text()
for field in ('Colors', 'Folders', 'TemporaryColors'):
    old = f'if (data.{field}.Count >= '
    new = f'if (data.{field}!.Count >= '
    s = s.replace(old, new)
p.write_text(s, encoding='utf-8', newline='\n')
