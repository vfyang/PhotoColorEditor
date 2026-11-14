# Build and test

Use the .NET 10.0.404 SDK (or a newer .NET 10 servicing SDK) and PowerShell 7 on Windows. Run the commands from the repository root.

```powershell
dotnet publish src/PhotoColorEditor -c Release -r win-x64 --self-contained true -p:PublishProfile=Windows-x64 -o dist
dotnet run --project tests/PhotoColorEditor.Tests -- --ui
& tests/Test-SingleFile.ps1 -Exe dist/PhotoColorEditor.exe
```

The publish command creates a self-contained `dist/PhotoColorEditor.exe` for Windows x64. The test runner generates its own sample photos. The `--ui` option also runs the desktop editing tests. The PowerShell smoke test starts the standalone EXE in a temporary folder and verifies that it uses its bundled runtime.

The Windows publish profile pins the bundled runtime to .NET 10.0.15. Update that pin when applying future security servicing updates, then rebuild and redistribute the self-contained EXE. A machine-wide runtime update does not update an existing release EXE.
