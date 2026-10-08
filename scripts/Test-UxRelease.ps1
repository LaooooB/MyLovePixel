param([Parameter(Mandatory=$true)][string]$Executable, [Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
# Windows PowerShell exposes the native UI Automation framework assemblies.
if ($PSVersionTable.PSEdition -eq 'Core') {
    & powershell.exe -NoProfile -Sta -ExecutionPolicy Bypass -File $PSCommandPath -Executable $Executable -OutputDirectory $OutputDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Native Windows smoke tests failed.' }
    return
}
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class NativeUi {
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
 [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
 [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
}
'@
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$results = New-Object System.Collections.Generic.List[string]
$paletteFile = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'MyLovePixel\user-palette.json'
$paletteBackup = Join-Path $OutputDirectory 'original-palette.json'
$hadPalette = Test-Path $paletteFile
if ($hadPalette) { Copy-Item $paletteFile $paletteBackup -Force }
# Exercise an upgrade of real legacy-format preferences in the isolated runner.
$legacyColors = @(0..63 | ForEach-Object { @{ hex = ('#{0:X6}' -f (0x110000 + $_)); name = ('Existing color ' + $_) } })
New-Item -ItemType Directory -Force (Split-Path $paletteFile) | Out-Null
$legacyText = @{ schemaVersion = 2; colors = $legacyColors } | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText($paletteFile, $legacyText, (New-Object Text.UTF8Encoding($false)))
$legacyHash = (Get-FileHash $paletteFile -Algorithm SHA256).Hash
$p = Start-Process $Executable -PassThru
function Alive {
    $p.Refresh()
    if ($p.HasExited) { throw "EXE exited unexpectedly with code $($p.ExitCode)." }
}
function Find-Control([string]$Id) {
    $condition = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)),
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $Id)))
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        Alive
        $element = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($null -ne $element) { return $element }
        Start-Sleep -Milliseconds 150
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Control not accessible: $Id"
}
function Invoke-Control([string]$Id) {
    $element = Find-Control $Id
    Write-Output ("Native action: " + $Id)
    $pattern = $null
    if ($element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
        $pattern.Invoke()
    } else {
        # Menu items expose selection/expand patterns on some native backends;
        # exercise the actual visible click instead of requiring InvokePattern.
        if ($element.Current.IsOffscreen -or !$element.Current.IsEnabled) { throw "Cannot click hidden/disabled control: $Id" }
        $point = $element.GetClickablePoint()
        Click-Point $point.X $point.Y
    }
    Start-Sleep -Milliseconds 200
}
function Click-Point([double]$X, [double]$Y) {
    [NativeUi]::SetCursorPos([int]$X, [int]$Y) | Out-Null
    [NativeUi]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    [NativeUi]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 200
}
try {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $eye = Find-Control 'tool.core.eyedropper'
    $p.Refresh()
    [NativeUi]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
    if ($eye.Current.Name -ne 'Eyedropper') { throw 'Eyedropper has no accessible name.' }
    $results.Add("PASS packaged startup and named eyedropper ($($watch.ElapsedMilliseconds) ms)")
    Invoke-Control 'tool.core.eyedropper'
    $canvas = Find-Control 'workspace.canvas'
    $rect = $canvas.Current.BoundingRectangle
    Click-Point ($rect.X + $rect.Width / 2) ($rect.Y + $rect.Height / 2)
    Alive
    $results.Add('PASS packaged eyedropper click')
    Invoke-Control 'tool.core.pencil'
    # Use an opaque foreground after sampling the initially transparent document.
    $hex = Find-Control 'color.hex'
    $hex.SetFocus()
    [System.Windows.Forms.SendKeys]::SendWait('^a')
    [System.Windows.Forms.SendKeys]::SendWait('#336699')
    [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
    Click-Point ($rect.X + $rect.Width / 2) ($rect.Y + $rect.Height / 2)
    Invoke-Control 'project.close'
    $dialog = Find-Control 'dialog.unsaved'
    Invoke-Control 'dialog.cancel'
    Alive
    Find-Control 'workspace.canvas' | Out-Null
    $results.Add('PASS native paint and canceled unsaved-document close')
    # Draw an opaque horizontal path, erase it with one right drag, and sample
    # interior pixels through the actual packaged UI before/after one Undo.
    Invoke-Control 'tool.core.pencil'
    $hex = Find-Control 'color.hex'
    $hex.SetFocus()
    [System.Windows.Forms.SendKeys]::SendWait('^a')
    [System.Windows.Forms.SendKeys]::SendWait('#336699FF')
    [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
    $canvas = Find-Control 'workspace.canvas'
    $rect = $canvas.Current.BoundingRectangle
    $y = $rect.Y + $rect.Height * 0.45
    $x1 = $rect.X + $rect.Width * 0.35
    $x2 = $rect.X + $rect.Width * 0.65
    [NativeUi]::SetCursorPos([int]$x1, [int]$y) | Out-Null
    [NativeUi]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    for ($i = 1; $i -le 12; $i++) {
        [NativeUi]::SetCursorPos([int]($x1 + ($x2-$x1)*$i/12), [int]$y) | Out-Null
        Start-Sleep -Milliseconds 16
    }
    [NativeUi]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 150
    [NativeUi]::SetCursorPos([int]$x1, [int]$y) | Out-Null
    [NativeUi]::mouse_event(8, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 30
    [NativeUi]::SetCursorPos([int]$x2, [int]$y) | Out-Null
    Start-Sleep -Milliseconds 40
    [NativeUi]::mouse_event(16, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 150
    Invoke-Control 'tool.core.eyedropper'
    Invoke-Control 'color.channels'
    foreach ($fraction in @(0.2, 0.5, 0.8)) {
        Click-Point ($x1+($x2-$x1)*$fraction) $y
        $alpha = Find-Control 'color.alpha'
        $range = $alpha.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)
        if ($range.Current.Value -ne 0) { throw 'Native right drag left an opaque interior pixel.' }
    }
    Invoke-Control 'edit.undo'
    foreach ($fraction in @(0.2, 0.5, 0.8)) {
        Click-Point ($x1+($x2-$x1)*$fraction) $y
        $alpha = Find-Control 'color.alpha'
        $range = $alpha.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)
        if ($range.Current.Value -ne 255) { throw 'One native Undo failed to restore the erase path.' }
    }
    $results.Add('PASS packaged right-button drag, gap-free erase, and whole-stroke Undo')
    Invoke-Control 'color.channels'
    Invoke-Control 'color.keep'
    $firstQuick = (Find-Control 'color.hex').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
    Invoke-Control 'palette.folder.manage'
    Invoke-Control 'folder.action.new'
    $folderName = 'My scene colors'
    (Find-Control 'folder.name').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($folderName)
    Invoke-Control 'folder.save'
    $data = Get-Content -Raw -Encoding UTF8 $paletteFile | ConvertFrom-Json
    if (@($data.folders).Count -ne 1 -or $data.folders[0].name -ne $folderName) { throw 'Native folder creation was not persisted.' }
    if ($data.schemaVersion -ne 3 -or @($data.colors).Count -ne 64) { throw 'Legacy colors were lost during folder migration.' }
    foreach ($i in 0..63) {
        if ($data.colors[$i].name -ne ('Existing color ' + $i) -or $data.colors[$i].hex -ne ('#{0:X6}' -f (0x110000 + $i))) { throw 'An existing saved color changed on upgrade.' }
    }
    if ((Get-FileHash ($paletteFile + '.v2.bak') -Algorithm SHA256).Hash -ne $legacyHash) { throw 'The original legacy palette backup is not byte-exact.' }
    $results.Add('PASS native legacy palette upgrade retains all 64 existing names, HEX values, order and original backup')
    Invoke-Control 'palette.open'
    $savedHex = Find-Control 'palette.hex'
    $savedHex.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('#654321')
    $savedName = Find-Control 'palette.name'
    $name1 = -join @([char]0x6728, [char]0x5934, [char]0x9634, [char]0x5F71)
    $savedName.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($name1)
    Invoke-Control 'palette.save'
    $swatch = Find-Control 'palette.swatch.654321'
    if (!$swatch.Current.Name.Contains($name1)) { throw 'The saved name is not exposed on the swatch.' }
    Invoke-Control 'palette.rename'
    $name2 = $name1 + ' 2'
    (Find-Control 'palette.name').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($name2)
    Invoke-Control 'palette.save'
    $data = Get-Content -Raw -Encoding UTF8 $paletteFile | ConvertFrom-Json
    $named = @($data.colors | Where-Object { $_.hex -eq '#654321' })
    if ($named.Count -ne 1 -or $named[0].name -ne $name2) { throw 'Name and HEX were not durably saved without duplicates.' }
    $results.Add('PASS native HEX entry, visible Unicode name, rename and persistent named palette')
    Stop-Process -Id $p.Id -Force
    $p = Start-Process $Executable -PassThru
    (Find-Control 'palette.search').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('#654321')
    Start-Sleep -Milliseconds 200
    $swatch = Find-Control 'palette.swatch.654321'
    if (!$swatch.Current.Name.Contains($name2)) { throw 'Named swatch disappeared after restart.' }
    Invoke-Control 'palette.swatch.654321'
    $value = (Find-Control 'color.hex').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
    if ($value -ne '#654321') { throw 'Reloaded named color does not apply to drawing.' }
    $results.Add('PASS native restart retains the custom name and color and can use it again')
    $data = Get-Content -Raw -Encoding UTF8 $paletteFile | ConvertFrom-Json
    if (@($data.colors).Count -ne 65 -or @($data.folders).Count -ne 1 -or @($data.quickColors).Count -ne 1) { throw 'Restart lost stored colors, folders or temporary slots.' }
    (Find-Control 'palette.search').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($folderName)
    Start-Sleep -Milliseconds 200
    Find-Control 'palette.swatch.654321' | Out-Null
    $results.Add('PASS native saved-color search includes folder names after restart')
    Invoke-Control 'color.keep'
    Invoke-Control ('quick.swatch.' + $firstQuick.TrimStart('#'))
    $actual = (Find-Control 'color.hex').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
    if ($actual -ne $firstQuick) { throw 'Quick color cannot be restored after restart.' }
    Invoke-Control 'color.picker'
    $spectrum = Find-Control 'color.picker.spectrum'
    $srect = $spectrum.Current.BoundingRectangle
    [NativeUi]::SetCursorPos([int]($srect.X + $srect.Width*.25), [int]($srect.Y+$srect.Height*.25)) | Out-Null
    [NativeUi]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    [NativeUi]::SetCursorPos([int]($srect.X + $srect.Width*.8), [int]($srect.Y+$srect.Height*.4)) | Out-Null
    Start-Sleep -Milliseconds 100
    [NativeUi]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 150
    $picked = (Find-Control 'color.picker.hex').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
    if ($picked -eq $firstQuick) { throw 'Dragging the native spectrum did not update the color.' }
    Invoke-Control 'color.picker.cancel'
    if ((Find-Control 'color.hex').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value -ne $firstQuick) { throw 'Cancel picker changed drawing color.' }
    Invoke-Control 'color.picker'
    (Find-Control 'color.picker.hex').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('#1289AB40')
    Invoke-Control 'color.picker.apply'
    if ((Find-Control 'color.hex').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value -ne '#1289AB40') { throw 'Picker lost confirmed HEX or alpha.' }
    $results.Add('PASS native quick-slot reuse, draggable spectrum, cancellation and exact RGBA confirmation')
    Invoke-Control 'preview.enlarge'
    $previewWindow = Find-Control 'preview.window'
    $previewView = Find-Control 'preview.large.viewport'
    $previewRect = $previewView.Current.BoundingRectangle
    if ($previewRect.Width -lt 300 -or $previewRect.Height -lt 180) { throw 'Enlarged preview has no useful viewing area.' }
    $beforeZoom = (Find-Control 'preview.large.zoom.actual').Current.Name
    Invoke-Control 'preview.large.zoom.in'
    $afterZoom = (Find-Control 'preview.large.zoom.actual').Current.Name
    if ($beforeZoom -eq $afterZoom) { throw 'Native preview zoom-in did not change the displayed zoom.' }
    Invoke-Control 'preview.large.zoom.out'
    Invoke-Control 'preview.large.zoom.actual'
    if (!(Find-Control 'preview.large.zoom.actual').Current.Name.Contains('100%')) { throw 'Native preview actual-size reset failed.' }
    Invoke-Control 'preview.large.zoom.fit'
    [NativeUi]::SetCursorPos(0, 0) | Out-Null
    Start-Sleep -Milliseconds 300
    $previewRect = (Find-Control 'preview.large.viewport').Current.BoundingRectangle
    $previewBitmap = New-Object System.Drawing.Bitmap([int]$previewRect.Width, [int]$previewRect.Height)
    $previewGraphics = [System.Drawing.Graphics]::FromImage($previewBitmap)
    try {
        $previewGraphics.CopyFromScreen([int]$previewRect.X, [int]$previewRect.Y, 0, 0, $previewBitmap.Size)
        foreach ($fraction in @(0.1, 0.3, 0.5, 0.7, 0.9)) {
            $pixel = $previewBitmap.GetPixel([int]($previewBitmap.Width * $fraction), [int]($previewBitmap.Height * $fraction))
            if ($pixel.R -ne 255 -or $pixel.G -ne 255 -or $pixel.B -ne 255) { throw 'The packaged preview background is not pure white.' }
        }
        $previewBitmap.Save((Join-Path $OutputDirectory 'preview-white.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $previewGraphics.Dispose(); $previewBitmap.Dispose() }
    $previewWindow.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    Start-Sleep -Milliseconds 250
    Invoke-Control 'preview.enlarge'
    (Find-Control 'preview.window').GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    Find-Control 'workspace.canvas' | Out-Null
    $results.Add('PASS native resizable Preview window, zoom-in/out, actual-size, fit, pure white background and reopen')
    [NativeUi]::SetWindowPos($p.MainWindowHandle, [IntPtr]::Zero, 0, 0, 960, 640, 6) | Out-Null
    Start-Sleep -Milliseconds 400
    $eye = Find-Control 'tool.core.eyedropper'
    if ($eye.Current.IsOffscreen) { throw 'Named eyedropper is clipped in the compact window.' }
    $results.Add('PASS compact native window')
    $screen = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $bitmap = New-Object System.Drawing.Bitmap($screen.Width, $screen.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($screen.Left, $screen.Top, 0, 0, $bitmap.Size)
        $bitmap.Save((Join-Path $OutputDirectory 'windows-desktop.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose(); $bitmap.Dispose() }
    $results.Add("OS: $([Environment]::OSVersion.VersionString); process: Windows x64; display $($screen.Width)x$($screen.Height)")
    $results | Set-Content (Join-Path $OutputDirectory 'native-smoke.txt')
    $results | Write-Output
} catch {
    $results.Add("FAIL " + $_.Exception.Message)
    $results | Set-Content (Join-Path $OutputDirectory 'native-smoke.txt')
    $results | Write-Output
    throw
} finally {
    if (!$p.HasExited) { Stop-Process -Id $p.Id -Force }
    if ($hadPalette) { Copy-Item $paletteBackup $paletteFile -Force }
    elseif (Test-Path $paletteFile) { Remove-Item $paletteFile -Force }
}
