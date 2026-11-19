# Publishing

1. Build `UiAutoLib` in Release for Win32 and x64.
2. Restore and build `WinUiAuto.sln` in Release.
3. Pack the Windows API project:

```powershell
dotnet pack .\WinUiAuto.Windows\UiAutomation.Windows.csproj -c Release -o .\artifacts\packages
```

4. Inspect the generated `.nupkg` and `.snupkg`, then publish using a protected NuGet API key:

```powershell
dotnet nuget push .\artifacts\packages\*.nupkg --source https://api.nuget.org/v3/index.json --api-key $env:NUGET_API_KEY
```

Never commit API keys or publish credentials. Package metadata points at the canonical repository URL and declares the MIT license.
