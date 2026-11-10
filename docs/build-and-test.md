# Build and test

Use the .NET 10 SDK and PowerShell 7 on Windows. Run the commands from the repository root.

```powershell
dotnet publish src/PhotoColorEditor -c Release -r win-x64 --self-contained true -p:PublishProfile=Windows-x64 -o dist
dotnet run --project tests/PhotoColorEditor.Tests -- --ui
& tests/Test-SingleFile.ps1 -Exe dist/PhotoColorEditor.exe
```

The publish command creates a self-contained `dist/PhotoColorEditor.exe` for Windows x64. The test runner generates its own sample photos. The `--ui` option also runs the desktop editing tests. The PowerShell smoke test starts the standalone EXE in a temporary folder and verifies that it uses its bundled runtime.
