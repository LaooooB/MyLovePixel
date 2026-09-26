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
}
