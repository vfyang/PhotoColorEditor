param(
    [Parameter(Mandatory = $true)] [string] $Exe,
    [Parameter(Mandatory = $true)] [string] $FirstPhoto,
    [Parameter(Mandatory = $true)] [string] $SecondPhoto,
    [switch] $KeepOpen
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class EditorTestWindows
{
    private delegate bool Visit(IntPtr window, IntPtr context);
    [DllImport("user32.dll")] private static extern bool EnumWindows(Visit callback, IntPtr context);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, Visit callback, IntPtr context);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] private static extern int GetDlgCtrlID(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetDlgItem(IntPtr parent, int id);
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr window);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wParam, string value, uint flags, uint timeout, out IntPtr result);

    private static string ClassName(IntPtr window)
    {
        var name = new StringBuilder(128);
        GetClassName(window, name, name.Capacity);
        return name.ToString();
    }

    public static IntPtr[] Dialogs(int processId)
    {
        var found = new List<IntPtr>();
        EnumWindows((window, _) =>
        {
            uint owner;
            GetWindowThreadProcessId(window, out owner);
            if (owner != processId) return true;
            if (ClassName(window) == "#32770") found.Add(window);
            return true;
        }, IntPtr.Zero);
        return found.ToArray();
    }

    public static bool SetNativeFileName(IntPtr dialog, string path)
    {
        IntPtr field = IntPtr.Zero;
        EnumChildWindows(dialog, (window, _) =>
        {
            var id = GetDlgCtrlID(window);
            if (ClassName(window) != "Edit") return true;
            var parent = GetParent(window);
            if (id == 1148 || GetDlgCtrlID(parent) == 1148 || GetDlgCtrlID(GetParent(parent)) == 1148)
            {
                field = window;
                return false;
            }
            if (field == IntPtr.Zero && id == 1001) field = window;
            return true;
        }, IntPtr.Zero);
        if (field == IntPtr.Zero) return false;
        IntPtr result;
        return SendMessageTimeout(field, 0x000c, IntPtr.Zero, path, 2, 1500, out result) != IntPtr.Zero && result != IntPtr.Zero;
    }

    public static bool Confirm(IntPtr dialog)
    {
        var ok = GetDlgItem(dialog, 1);
        return ok != IntPtr.Zero && PostMessage(dialog, 0x0111, (IntPtr)1, ok);
    }
}
'@
$uia = [System.Windows.Automation.AutomationElement]
$descendants = [System.Windows.Automation.TreeScope]::Descendants

function Wait-For([scriptblock] $Read, [string] $Description) {
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        $found = & $Read
        if ($found) { return $found }
        if ($app.HasExited) { throw "The editor exited while waiting for $Description." }
        Start-Sleep -Milliseconds 140
    } while ([DateTime]::UtcNow -lt $deadline)
    $app.Refresh()
    $titles = foreach ($handle in [EditorTestWindows]::Dialogs($app.Id)) { $uia::FromHandle($handle).Current.Name }
    throw "Timed out waiting for $Description. Window: '$($app.MainWindowTitle)'. Dialogs: '$($titles -join ', ')'."
}

function Find-One($Root, $Property, $Value, $Scope = $descendants) {
    $condition = [System.Windows.Automation.PropertyCondition]::new($Property, $Value)
    $Root.FindFirst($Scope, $condition)
}

function Button([string[]] $Names) {
    foreach ($name in $Names) {
        $condition = [System.Windows.Automation.AndCondition]::new(
            [System.Windows.Automation.PropertyCondition]::new($uia::NameProperty, $name),
            [System.Windows.Automation.PropertyCondition]::new($uia::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
        )
        $buttons = $main.FindAll($descendants, $condition)
        foreach ($candidate in $buttons) {
            if ($candidate.Current.IsEnabled -and !$candidate.Current.IsOffscreen) { return $candidate }
        }
    }
}

function Click($Element) { $Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }

function Pick-File([string] $Path) {
    $chooser = Wait-For {
        foreach ($handle in [EditorTestWindows]::Dialogs($app.Id)) {
            $candidate = $uia::FromHandle($handle)
            if ($candidate -and (Find-One $candidate $uia::AutomationIdProperty '1')) { return @{ Handle = $handle; Element = $candidate } }
        }
    } 'the file chooser'
    $dialog = $chooser.Element
    if ([EditorTestWindows]::SetNativeFileName($chooser.Handle, $Path)) {
        if (![EditorTestWindows]::Confirm($chooser.Handle)) { Click (Find-One $dialog $uia::AutomationIdProperty '1') }
        return
    }
    $entry = Wait-For {
        $condition = [System.Windows.Automation.PropertyCondition]::new($uia::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)
        $edits = $dialog.FindAll($descendants, $condition)
        $fallback = @()
        foreach ($edit in $edits) {
            $valuePattern = $null
            if (!$edit.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref] $valuePattern) -or $valuePattern.Current.IsReadOnly) { continue }
            $label = $edit.Current.Name
            if ($edit.Current.AutomationId -in @('1148', '1001') -or $label -match 'file.?name|文件名') { return $valuePattern }
            if (!$edit.Current.IsOffscreen -and $label -notmatch 'search|搜索|address|地址') { $fallback += $valuePattern }
        }
        if ($fallback.Count -eq 1) { return $fallback[0] }
    } 'the file-name entry'
    $entry.SetValue($Path)
    $confirm = Wait-For { Find-One $dialog $uia::AutomationIdProperty '1' } 'the file chooser confirmation'
    Click $confirm
}

$exePath = (Resolve-Path -LiteralPath $Exe).Path
$fixtures = @((Resolve-Path -LiteralPath $FirstPhoto).Path, (Resolve-Path -LiteralPath $SecondPhoto).Path)
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$temporaryPath = Join-Path $temporaryRoot ('photo-editor-ui-' + [Guid]::NewGuid().ToString('N'))
$folder = New-Item -Path $temporaryPath -ItemType Directory
$style = if ($KeepOpen) { 'Normal' } else { 'Hidden' }
$app = Start-Process -FilePath $exePath -ArgumentList '--no-open-with-registration' -WindowStyle $style -PassThru
$passed = $false
try {
    $main = Wait-For {
        $app.Refresh()
        if ($app.MainWindowHandle -ne 0) { $uia::FromHandle($app.MainWindowHandle) }
    } 'the editor window'
    foreach ($source in $fixtures) {
        $originalHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
        Click (Wait-For { Button @('打开照片', '開啟照片', 'Open photo') } 'Open photo')
        Pick-File $source
        $auto = Wait-For { Button @('自动优化', '自動最佳化', 'Auto enhance') } 'Auto enhance'
        Click $auto
        $undo = Wait-For { Button @('撤销', '復原', 'Undo') } 'Undo'
        Click $undo
        Click (Wait-For { Button @('重做', 'Redo') } 'Redo')
        $save = Wait-For { Button @('另存为', '另存新檔', 'Save as') } 'Save as'
        $destination = Join-Path $folder.FullName ([IO.Path]::GetFileNameWithoutExtension($source) + '-edited.jpg')
        Click $save
        Pick-File $destination
        $null = Wait-For { Test-Path -LiteralPath $destination } 'the exported JPG'
        $original = [System.Drawing.Image]::FromFile($source)
        $edited = [System.Drawing.Image]::FromFile($destination)
        try {
            if ($original.Width -ne $edited.Width -or $original.Height -ne $edited.Height) { throw 'Export changed the dimensions.' }
            if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -eq $originalHash) { throw 'Export was identical to the original.' }
        } finally { $original.Dispose(); $edited.Dispose() }
        if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $originalHash) { throw 'The original file changed.' }
        "PASS: packaged EXE opened, auto-enhanced, undid, redid, and saved $([IO.Path]::GetFileName($source))."
    }
    if ($KeepOpen) {
        Click (Wait-For { Button @('打开照片', '開啟照片', 'Open photo') } 'Open photo')
        Pick-File $fixtures[0]
        $null = Wait-For { $main.Current.Name -like ('*' + [IO.Path]::GetFileName($fixtures[0]) + '*') } 'the original sample preview'
    }
    $passed = $true
} finally {
    if (!$app.HasExited -and (!$KeepOpen -or !$passed)) {
        if ($app.CloseMainWindow()) { $null = $app.WaitForExit(3000) }
        if (!$app.HasExited) { Stop-Process -Id $app.Id; $null = $app.WaitForExit(5000) }
    }
    if ($folder.FullName -eq $temporaryPath -and $folder.FullName.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase)) {
        $folder.Delete($true)
    }
}
