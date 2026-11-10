param(
    [Parameter(Mandatory = $true)] [string] $Exe
)

$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath $Exe).Path
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$testPath = Join-Path $tempRoot ('photo-editor-exe-test-' + [Guid]::NewGuid().ToString('N'))
$testRoot = New-Item -ItemType Directory -Path $testPath
$priorExtract = $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR
$priorDotnet = $env:DOTNET_ROOT
$priorDotnetX64 = $env:DOTNET_ROOT_X64
$priorLookup = $env:DOTNET_MULTILEVEL_LOOKUP
$app = $null
try {
    $emptyFolder = $testRoot.CreateSubdirectory('照片 程序')
    $copy = Join-Path $emptyFolder.FullName 'PhotoColorEditor.exe'
    Copy-Item -LiteralPath $source -Destination $copy
    $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = Join-Path $testRoot.FullName 'extracted'
    $env:DOTNET_ROOT = Join-Path $testRoot.FullName 'unavailable-dotnet'
    $env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
    $env:DOTNET_MULTILEVEL_LOOKUP = '0'
    $app = Start-Process -FilePath $copy -ArgumentList '--no-open-with-registration' -WorkingDirectory $emptyFolder.FullName -WindowStyle Hidden -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(35)
    do {
        Start-Sleep -Milliseconds 200
        $app.Refresh()
        if ($app.HasExited) { throw "EXE exited with code $($app.ExitCode)." }
    } until ($app.MainWindowHandle -ne 0 -or [DateTime]::UtcNow -gt $deadline)
    if ($app.MainWindowHandle -eq 0) { throw 'No WPF main window was detected.' }

    $nativeCore = Get-ChildItem -LiteralPath $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR -Filter 'coreclr.dll' -Recurse | Select-Object -First 1
    $nativeWpf = Get-ChildItem -LiteralPath $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR -Filter 'wpfgfx_cor3.dll' -Recurse | Select-Object -First 1
    if (!$nativeCore -or !$nativeWpf) { throw 'Bundled runtime files were not extracted.' }
    if (@(Get-ChildItem -LiteralPath $emptyFolder.FullName -Force).Count -ne 1) { throw 'The EXE folder contains unexpected companion files.' }
    "PASS: standalone launch with '$($app.MainWindowTitle)', runtime $($nativeCore.VersionInfo.FileVersion)"
    "SHA-256: $((Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash)"
} finally {
    if ($app -and !$app.HasExited) {
        if ($app.CloseMainWindow()) { $null = $app.WaitForExit(5000) }
        if (!$app.HasExited) { Stop-Process -Id $app.Id; $null = $app.WaitForExit(5000) }
    }
    $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = $priorExtract
    $env:DOTNET_ROOT = $priorDotnet
    $env:DOTNET_ROOT_X64 = $priorDotnetX64
    $env:DOTNET_MULTILEVEL_LOOKUP = $priorLookup
    if ($testRoot.FullName -eq $testPath -and $testRoot.FullName.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
        $testRoot.Delete($true)
    }
}
