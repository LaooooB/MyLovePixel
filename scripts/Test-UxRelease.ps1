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
    $pattern = $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
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
    $swatch = Find-Control 'palette.swatch.654321'
    if (!$swatch.Current.Name.Contains($name2)) { throw 'Named swatch disappeared after restart.' }
    Invoke-Control 'palette.swatch.654321'
    $value = (Find-Control 'color.hex').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
    if ($value -ne '#654321') { throw 'Reloaded named color does not apply to drawing.' }
    $results.Add('PASS native restart retains the custom name and color and can use it again')
    [NativeUi]::SetWindowPos($p.MainWindowHandle, [IntPtr]::Zero, 0, 0, 960, 640, 6) | Out-Null
    Start-Sleep -Milliseconds 400
    $eye = Find-Control 'tool.core.eyedropper'
    if ($eye.Current.IsOffscreen) { throw 'Named eyedropper is clipped in the compact window.' }
    $results.Add('PASS compact native window')
    Invoke-Control 'palette.rename'
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
