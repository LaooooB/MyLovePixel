using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MyLovePixel.Application;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Effects;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Core.Tiles;
using MyLovePixel.Export;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private void RefreshEffects()
    {
        _effectsPanel.Children.Clear();
        var session = Current();
        if (session is null) return;

        AddPanelLabel(_effectsPanel, "Effect stack");
        var add = new ComboBox { ItemsSource = _plugins.GetEffectTypes(), SelectedIndex = 0 };
        _effectsPanel.Children.Add(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 6,
            Children =
            {
                add,
                Place(TextIconButton("＋", "Add", "Add effect", () =>
                {
                    if (add.SelectedItem is string type) Safe(() => _selectedEffect = _plugins.AddEffect(session, type));
                }), 1),
            },
        });

        var effects = _plugins.GetEffects(session);
        if (_selectedEffect is { } selected && effects.All(v => v.Id != selected)) _selectedEffect = null;
        foreach (var effect in effects)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("30,*,30,30,30"), ColumnSpacing = 4 };
            row.Children.Add(SmallIcon(effect.Enabled ? "●" : "○", effect.Enabled ? "Disable effect" : "Enable effect", () => session.SetEffectEnabled(effect.Id, !effect.Enabled)));
            var choose = new Button { Content = effect.DisplayName, HorizontalContentAlignment = HorizontalAlignment.Left };
            if (_selectedEffect == effect.Id) choose.Classes.Add("selected");
            choose.Click += (_, _) => { _selectedEffect = effect.Id; RefreshEffects(); };
            Grid.SetColumn(choose, 1);
            row.Children.Add(choose);
            row.Children.Add(Place(SmallIcon("↑", "Move effect up", () => session.MoveEffect(effect.Id, -1)), 2));
            row.Children.Add(Place(SmallIcon("↓", "Move effect down", () => session.MoveEffect(effect.Id, 1)), 3));
            row.Children.Add(Place(SmallIcon("×", "Remove effect", () => session.RemoveEffect(effect.Id)), 4));
            _effectsPanel.Children.Add(row);
        }

        if (_selectedEffect is { } id)
        {
            AddPanelLabel(_effectsPanel, "Selected effect parameters");
            foreach (var parameter in _plugins.GetEffectParameters(session, id))
                _effectsPanel.Children.Add(BuildEffectParameter(session, id, parameter));
            _effectsPanel.Children.Add(TextIconButton("", "Bake Effects", "Bake effects into the current image", () => Safe(() => _plugins.BakeEffects(session))));
        }
    }

    private Control BuildEffectParameter(DocumentSession session, EffectInstanceId id, EffectParameterPresentation parameter)
    {
        Control editor;
        switch (parameter.Kind)
        {
            case EffectParameterKind.Integer:
            {
                var n = new NumericUpDown { Value = parameter.Value.IntegerValue, Minimum = parameter.Minimum is { } min ? (decimal)min : decimal.MinValue, Maximum = parameter.Maximum is { } max ? (decimal)max : decimal.MaxValue, Increment = 1, FormatString = "0" };
                n.ValueChanged += (_, _) => { if (n.Value is { } v) Safe(() => _plugins.SetEffectParameter(session, id, parameter.Key, EffectValue.Integer((long)v))); };
                editor = n;
                break;
            }
            case EffectParameterKind.Number:
            {
                var n = new NumericUpDown { Value = (decimal)parameter.Value.NumberValue, Minimum = parameter.Minimum is { } min ? (decimal)min : -1000000m, Maximum = parameter.Maximum is { } max ? (decimal)max : 1000000m, Increment = 0.1m, FormatString = "0.###" };
                n.ValueChanged += (_, _) => { if (n.Value is { } v) Safe(() => _plugins.SetEffectParameter(session, id, parameter.Key, EffectValue.Number((double)v))); };
                editor = n;
                break;
            }
            case EffectParameterKind.Boolean:
            {
                var c = new CheckBox { IsChecked = parameter.Value.BooleanValue };
                c.Click += (_, _) => _plugins.SetEffectParameter(session, id, parameter.Key, EffectValue.Boolean(c.IsChecked == true));
                editor = c;
                break;
            }
            case EffectParameterKind.Point:
            {
                var x = Number(parameter.Value.PointValue.X, -4096, 4096);
                var y = Number(parameter.Value.PointValue.Y, -4096, 4096);
                void Set() => _plugins.SetEffectParameter(session, id, parameter.Key, EffectValue.Point(new IntPoint((int)(x.Value ?? 0), (int)(y.Value ?? 0))));
                x.ValueChanged += (_, _) => Safe(Set);
                y.ValueChanged += (_, _) => Safe(Set);
                editor = Icons(x, y);
                break;
            }
            case EffectParameterKind.Color:
            {
                var swatch = new Border { Width = 28, Height = 28, Background = Brush(parameter.Value.ColorValue), CornerRadius = new CornerRadius(4) };
                var b = new Button { Content = swatch, Padding = new Thickness(2) };
                b.Click += async (_, _) =>
                {
                    var c = await new ColorDialog(parameter.Value.ColorValue).ShowDialog<Rgba32?>(this);
                    if (c is { } v) _plugins.SetEffectParameter(session, id, parameter.Key, EffectValue.Color(v));
                };
                editor = b;
                break;
            }
            case EffectParameterKind.PaletteReference:
            {
                var palettes = session.GetPaletteEditors();
                var combo = new ComboBox { ItemsSource = palettes.Select(v => v.Id).ToArray(), SelectedItem = parameter.Value.PaletteIdValue };
                combo.SelectionChanged += (_, _) =>
                {
                    if (combo.SelectedItem is PaletteId p) _plugins.SetEffectParameter(session, id, parameter.Key, EffectValue.PaletteReference(p));
                };
                editor = combo;
                break;
            }
            case EffectParameterKind.Text:
            {
                var text = new TextBox { Text = parameter.Value.TextValue ?? string.Empty };
                text.LostFocus += (_, _) => _plugins.SetEffectParameter(session, id, parameter.Key, EffectValue.Text(text.Text ?? string.Empty));
                editor = text;
                break;
            }
            default:
                editor = new TextBlock { Text = "—" };
                break;
        }

        if (!parameter.Animatable) return Labeled(parameter.DisplayName, editor);
        var key = SmallIcon(parameter.HasKeyframe ? "◆" : "◇", parameter.HasKeyframe ? "Clear keyframe" : "Set keyframe", () =>
        {
            Safe(() =>
            {
                if (parameter.HasKeyframe) _plugins.ClearEffectParameterKeyframe(session, id, parameter.Key);
                else _plugins.SetEffectParameterKeyframe(session, id, parameter.Key, parameter.Value);
            });
            RefreshEffects();
        });
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,30"), ColumnSpacing = 4 };
        row.Children.Add(editor);
        row.Children.Add(Place(key, 1));
        return Labeled(parameter.DisplayName, row);
    }

    private void RefreshTiles()
    {
        _tilesPanel.Children.Clear();
        var session = Current();
        if (session is null) return;
        var sets = session.GetTilesets();
        if (_selectedTileset is null && sets.Count > 0) _selectedTileset = sets[0].Id;
        if (_selectedTileset is { } sid && sets.All(v => v.Id != sid)) _selectedTileset = sets.Count == 0 ? null : sets[0].Id;

        AddPanelLabel(_tilesPanel, "Tileset");
        var setItems = sets.Select(v => $"{v.Name} {v.TileSize.Width}×{v.TileSize.Height}").ToArray();
        var setIndex = sets.ToList().FindIndex(v => v.Id == _selectedTileset);
        var setCombo = new ComboBox { ItemsSource = setItems, SelectedIndex = setIndex };
        setCombo.SelectionChanged += (_, _) =>
        {
            if ((uint)setCombo.SelectedIndex < (uint)sets.Count)
            {
                _selectedTileset = sets[setCombo.SelectedIndex].Id;
                _selectedTile = null;
                RefreshTiles();
            }
        };
        var tw = Number(16, 1, 256);
        var th = Number(16, 1, 256);
        _tilesPanel.Children.Add(setCombo);
        _tilesPanel.Children.Add(Labeled("Tile size", Icons(tw, th)));
        _tilesPanel.Children.Add(TextIconButton("＋", "Add Tileset", "Add tileset", () =>
        {
            _selectedTileset = session.AddTileset("Tileset", new IntSize((int)(tw.Value ?? 16), (int)(th.Value ?? 16)));
            RefreshTiles();
        }));

        if (_selectedTileset is not { } tilesetId) return;
        AddPanelLabel(_tilesPanel, "Tiles");
        var tiles = session.GetTiles(tilesetId, _selectedTile);
        if (_selectedTile is null && tiles.Count > 0) _selectedTile = tiles[0].Id;
        var tileWrap = new WrapPanel { ItemWidth = 38, ItemHeight = 34 };
        foreach (var tile in tiles.Select((value, index) => (value, index)))
        {
            var b = new Button { Content = tile.index.ToString(), MinWidth = 34, Height = 30, Padding = new Thickness(5, 2) };
            ToolTip.SetTip(b, $"Tile {tile.index}: {tile.value.Name}");
            b.Click += (_, _) => { _selectedTile = tile.value.Id; _tileErase = false; RefreshTiles(); };
            if (_selectedTile == tile.value.Id && !_tileErase) b.Classes.Add("selected");
            tileWrap.Children.Add(b);
        }
        tileWrap.Children.Add(IconButton("＋", "Add tile", () => { _selectedTile = session.AddTile(tilesetId); RefreshTiles(); }));
        _tilesPanel.Children.Add(tileWrap);

        AddPanelLabel(_tilesPanel, "Tilemap");
        var maps = session.GetTilemaps().Where(v => v.TilesetId == tilesetId).ToArray();
        if (_selectedTilemap is null && maps.Length > 0) _selectedTilemap = maps[0].Id;
        if (_selectedTilemap is { } mid && maps.All(v => v.Id != mid)) _selectedTilemap = maps.Length == 0 ? null : maps[0].Id;
        var mapIndex = Array.FindIndex(maps, v => v.Id == _selectedTilemap);
        var mapCombo = new ComboBox { ItemsSource = maps.Select(v => v.Name).ToArray(), SelectedIndex = mapIndex };
        mapCombo.SelectionChanged += (_, _) =>
        {
            if ((uint)mapCombo.SelectedIndex < (uint)maps.Length)
            {
                _selectedTilemap = maps[mapCombo.SelectedIndex].Id;
                RefreshTiles();
            }
        };
        _tilesPanel.Children.Add(mapCombo);
        _tilesPanel.Children.Add(TextIconButton("＋", "Add Tilemap", "Add tilemap", () =>
        {
            _selectedTilemap = session.AddTilemap("Tilemap", tilesetId);
            RefreshTiles();
        }));
        if (_selectedTilemap is not { } tilemapId) return;

        AddPanelLabel(_tilesPanel, "Paint controls");
        _tilesPanel.Children.Add(Icons(
            ToggleIcon("⌫", "Erase tile", () => _tileErase, v => { _tileErase = v; RefreshTiles(); }),
            ToggleFlag("↔", TileCellFlags.FlipX),
            ToggleFlag("↕", TileCellFlags.FlipY),
            ToggleFlag("↻", TileCellFlags.Rotate90)));
        _tilesPanel.Children.Add(TextIconButton("✎", "Edit Tile Pixels", "Edit selected tile pixels", EditSelectedTileAsync));
        _tilesPanel.Children.Add(TextIconButton("", "AutoTile…", "AutoTile", async () => await AutoTileAsync(tilesetId, tilemapId)));
        _tilesPanel.Children.Add(TextIconButton("", "Collect Unused Tiles", "Collect unused tiles", () => { Safe(() => session.CollectUnusedTiles(tilesetId)); RefreshTiles(); }));
        if (_selectedTileCell is { } selectedCell)
        {
            _tilesPanel.Children.Add(TextIconButton("", "Make Selected Cell Unique", "Make selected cell unique", () =>
            {
                Safe(() => session.MakeUniqueTile(tilemapId, selectedCell.X, selectedCell.Y));
                RefreshTiles();
            }));
        }

        AddPanelLabel(_tilesPanel, "Viewport");
        var vx = Number(_tileViewportX, -8192, 8192);
        var vy = Number(_tileViewportY, -8192, 8192);
        vx.ValueChanged += (_, _) => { _tileViewportX = (int)(vx.Value ?? 0); RefreshTiles(); };
        vy.ValueChanged += (_, _) => { _tileViewportY = (int)(vy.Value ?? 0); RefreshTiles(); };
        _tilesPanel.Children.Add(Labeled("Origin X / Y", Icons(vx, vy)));
        _tilesPanel.Children.Add(BuildTileGrid(session, tilemapId));
    }

    private Control BuildTileGrid(DocumentSession session, TilemapId tilemapId)
    {
        var snapshot = session.CaptureSnapshot().GetTilemap(tilemapId);
        var grid = new Grid();
        for (var i = 0; i < 8; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
        {
            var cx = _tileViewportX + x;
            var cy = _tileViewportY + y;
            var cell = snapshot.GetCell(new IntPoint(cx, cy));
            var b = new Button { Content = cell is null ? "·" : "■", MinWidth = 28, Height = 28, Padding = new Thickness(0) };
            ToolTip.SetTip(b, cell is null ? $"Cell {cx}, {cy} · empty" : $"Cell {cx}, {cy} · painted");
            if (_selectedTileCell == (cx, cy)) b.Classes.Add("selected");
            b.Click += (_, _) =>
            {
                _selectedTileCell = (cx, cy);
                Safe(() => session.SetTileCell(tilemapId, cx, cy, _tileErase ? null : _selectedTile, _tileFlags));
                RefreshTiles();
            };
            Grid.SetColumn(b, x);
            Grid.SetRow(b, y);
            grid.Children.Add(b);
        }
        return grid;
    }

    private void RefreshAnimation()
    {
        _animationPanel.Children.Clear();
        var session = Current();
        if (session is null) return;
        var s = session.CaptureSnapshot();
        var frameIndex = s.FrameOrder.ToList().IndexOf(session.CurrentFrameId);

        var onion = new StackPanel { Spacing = 6 };
        var prev = Number(_onionPrevious, 0, 12);
        var next = Number(_onionNext, 0, 12);
        var opacity = Number(_onionOpacity, 0, 255);
        var falloff = new NumericUpDown { Value = (decimal)_onionFalloff, Minimum = 0, Maximum = 1, Increment = 0.05m, FormatString = "0.00" };
        prev.ValueChanged += (_, _) => { _onionPrevious = (int)(prev.Value ?? 1); if (_onionSkin) RefreshCanvas(); };
        next.ValueChanged += (_, _) => { _onionNext = (int)(next.Value ?? 1); if (_onionSkin) RefreshCanvas(); };
        opacity.ValueChanged += (_, _) => { _onionOpacity = (byte)(opacity.Value ?? 96); if (_onionSkin) RefreshCanvas(); };
        falloff.ValueChanged += (_, _) => { _onionFalloff = (double)(falloff.Value ?? 0.65m); if (_onionSkin) RefreshCanvas(); };
        onion.Children.Add(ToggleTextButton("◌", "Onion Skin", "Onion skin", () => _onionSkin, v => { _onionSkin = v; RefreshCanvas(); RefreshAnimation(); }));
        onion.Children.Add(Labeled("Previous", prev));
        onion.Children.Add(Labeled("Next", next));
        onion.Children.Add(Labeled("Opacity", opacity));
        onion.Children.Add(Labeled("Falloff", falloff));
        _animationPanel.Children.Add(Expander("Onion Skin", onion));

        var clips = new StackPanel { Spacing = 5 };
        clips.Children.Add(TextIconButton("＋", "Add Clip", "Add animation clip", () => session.AddAnimationClip($"Clip {session.GetAnimationClips().Count + 1}", 0, s.FrameOrder.Count - 1, AnimationLoopMode.Loop)));
        foreach (var clip in session.GetAnimationClips())
            clips.Children.Add(ListRow($"{clip.Name}  {clip.Start + 1}–{clip.End + 1}", Icons(IconButton("✎", "Edit clip", async () => await EditClipAsync(clip)), IconButton("×", "Remove clip", () => session.RemoveAnimationClip(clip.Id)))));
        _animationPanel.Children.Add(Expander("Clips", clips));

        var tags = new StackPanel { Spacing = 5 };
        tags.Children.Add(TextIconButton("＋", "Add Tag", "Add tag at current frame", () => session.AddAnimationTag($"Tag {session.GetAnimationTags().Count + 1}", frameIndex, frameIndex)));
        foreach (var tag in session.GetAnimationTags())
            tags.Children.Add(ListRow($"{tag.Name}  {tag.Start + 1}–{tag.End + 1}", Icons(IconButton("✎", "Edit tag", async () => await EditTagAsync(tag)), IconButton("×", "Remove tag", () => session.RemoveAnimationTag(tag.Id)))));
        _animationPanel.Children.Add(Expander("Tags", tags));

        var pivot = new StackPanel { Spacing = 6 };
        var px = Number(_hover?.X ?? 0, -8192, 8192);
        var py = Number(_hover?.Y ?? 0, -8192, 8192);
        pivot.Children.Add(Labeled("X / Y", Icons(px, py)));
        pivot.Children.Add(Icons(
            TextIconButton("", "Set Pivot", "Set pivot", () => session.SetPivot((int)(px.Value ?? 0), (int)(py.Value ?? 0))),
            TextIconButton("×", "Clear", "Clear pivot", session.ClearPivot)));
        _animationPanel.Children.Add(Expander("Pivot", pivot));

        var tracks = session.GetCurrentAnimationTracks();
        var boxes = new StackPanel { Spacing = 5 };
        boxes.Children.Add(ListRow($"Hitboxes · {tracks.HitboxCount}", Icons(IconButton("✎", "Edit hitboxes", EditHitboxesAsync), IconButton("×", "Clear hitboxes", session.ClearHitboxes))));
        boxes.Children.Add(ListRow($"Hurtboxes · {tracks.HurtboxCount}", Icons(IconButton("✎", "Edit hurtboxes", EditHurtboxesAsync), IconButton("×", "Clear hurtboxes", session.ClearHurtboxes))));
        _animationPanel.Children.Add(Expander("Collision Boxes", boxes));

        _animationPanel.Children.Add(Expander("Sockets", ListRow($"Sockets · {tracks.SocketCount}", Icons(IconButton("✎", "Edit sockets", EditSocketsAsync), IconButton("×", "Clear sockets", session.ClearSockets)))));
        _animationPanel.Children.Add(Expander("Events", ListRow($"Events · {tracks.EventCount}", Icons(IconButton("✎", "Edit events", EditEventsAsync), IconButton("×", "Clear events", session.ClearAnimationEvents)))));

        var slices = new StackPanel { Spacing = 5 };
        slices.Children.Add(TextIconButton("＋", "Add Canvas Slice", "Add canvas slice", () => session.AddSpriteSlice($"Slice {session.GetSpriteSlices().Count + 1}", 0, 0, s.Canvas.Size.Width, s.Canvas.Size.Height, s.Canvas.Size.Width / 2, s.Canvas.Size.Height / 2)));
        foreach (var slice in session.GetSpriteSlices())
            slices.Children.Add(ListRow(slice.Name, Icons(IconButton("✎", "Edit slice", async () => await EditSliceAsync(slice)), IconButton("×", "Remove slice", () => session.RemoveSpriteSlice(slice.Id)))));
        _animationPanel.Children.Add(Expander("Slices", slices));

        if (session.GetPaletteEditors().Count > 0)
        {
            _animationPanel.Children.Add(Expander("Color Cycles", ListRow($"Cycles · {tracks.ColorCycleCount}", Icons(IconButton("✎", "Edit color cycles", EditColorCyclesAsync), IconButton("×", "Clear color cycles", session.ClearColorCycles)))));
        }
    }
}
