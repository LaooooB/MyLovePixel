from pathlib import Path
for name, header in [('PixelCanvasView.Interaction.cs', 'using Avalonia.Media.Imaging;'), ('MainWindow.Shell.cs', 'using MyLovePixel.Core.Document;')]:
    path = Path('src/MyLovePixel.Desktop') / name
    text = path.read_text(encoding='utf-8-sig')
    if header not in text: path.write_text(header + '\n' + text, encoding='utf-8')
