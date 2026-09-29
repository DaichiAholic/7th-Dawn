# 7th Dawn

A MonoGame (DesktopGL) game. The project lives in `7th-Dawn/DuskAndDawn`.

## Building from source

1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
   or newer (9 and 10 work too). With Visual Studio, use 2022 version 17.8 or
   later.
2. Build and run:

   ```
   cd 7th-Dawn/DuskAndDawn
   dotnet run
   ```

   Or open `7th-Dawn.sln` in Visual Studio / Rider and press Run.

**The first build needs the internet and takes a while.** Besides the NuGet
packages, it downloads the MonoGame content builder (MGCB, about 220 MB), which
turns the art and fonts in `Content/` into the files the game loads. It's a
.NET local tool (`.config/dotnet-tools.json`), so it's downloaded once per
computer and reused after that.

On Linux, building the fonts needs FreeType (`libfreetype6`).

## If the build fails

- **"This file came from another computer and might be blocked"**: Windows
  marks every file from a "Download ZIP" as downloaded from the internet. The
  build clears that mark itself on Windows, but if Visual Studio or Windows
  still complains, clear it by hand, either way:
  - Before unzipping: right-click the ZIP > Properties > tick **Unblock** >
    OK, then extract it again.
  - After unzipping: open PowerShell in the project folder and run
    `Get-ChildItem -Recurse | Unblock-File`

  Cloning with Git or GitHub Desktop instead of downloading a ZIP avoids the
  mark entirely.

- **"Couldn't download the MonoGame content builder"**, or an error that
  `dotnet mgcb` exited with a code: the MGCB download didn't finish (a slow
  or dropped connection, or a firewall). Run this, then build again:

  ```
  cd 7th-Dawn/DuskAndDawn
  dotnet tool restore
  ```

- **`NETSDK1045` ("The current .NET SDK does not support targeting ...")**:
  that computer's .NET SDK is too old. Install the .NET 8 SDK or newer, or
  update Visual Studio.
- **`NU1301` ("Unable to load the service index")** or another NuGet error:
  NuGet couldn't reach nuget.org. Check the connection and build again. The
  repo's `nuget.config` makes the game restore from nuget.org only, so other
  package sources set up on that computer don't get in the way.
- **"...Task.targets was not imported ... due to the file being invalid"**
  (or another error pointing into `.nuget\packages`): that computer's copy of
  a NuGet package got cut off mid-download. Close Visual Studio, delete that
  package's folder in `%USERPROFILE%\.nuget\packages` (or run
  `dotnet nuget locals all --clear`), delete `bin` and `obj`, and build again.
  If it keeps happening, antivirus is usually scanning the files as they're
  written, so exclude the `.nuget` folder.
- **Anything else**: delete the `bin` and `obj` folders in
  `7th-Dawn/DuskAndDawn` and build again.

On Windows, keep the project out of OneDrive-synced folders (Desktop and
Documents often are), since syncing can lock files mid-build. Also keep it out of
very deep folders: a "Download ZIP" unpacks into a folder inside a folder, and
long paths can break the build.

The MGCB Editor (for editing `Content.mgcb` by hand) is set up for Windows. On
macOS or Linux, add it from `7th-Dawn/DuskAndDawn` with
`dotnet tool install --local dotnet-mgcb-editor-mac --version 3.8.5.1` (or
`dotnet-mgcb-editor-linux`).

## Sharing the game with players

**Every push builds the game automatically.** GitHub Actions
(`.github/workflows/windows-build.yml`) builds it on a clean Windows machine.
For `main`, `master` and the `*-Updates` branches it also puts a ready-to-play
zip on the repo's **Releases** page, e.g. "Latest build: 1.5-Updates". Players
download `7th-Dawn-win64.zip`, unzip it and run `DuskAndDawn.exe`, with
nothing to install. If a build fails there (a red X on the commit), the
problem is in the code, not on anyone's computer.

To make the same build by hand:

The GitHub repo holds the source code only. The `bin` and `obj` folders, where a
build puts the finished game, are left out on purpose (`.gitignore`), so
downloading the repo always means building it. Players shouldn't have to:
publish a build that already contains everything, including the .NET
runtime and the built art:

```
cd 7th-Dawn/DuskAndDawn
dotnet publish -c Release -r win-x64 --self-contained -p:PublishTrimmed=true -p:TrimMode=partial -p:JsonSerializerIsReflectionEnabledByDefault=true -p:DebugType=none -o publish/7th-Dawn
```

Zip the `publish/7th-Dawn` folder (about 10 MB zipped) and share that, for
example as a GitHub Release. Players unzip it and run `DuskAndDawn.exe`, with
nothing to install. Keep `JsonSerializerIsReflectionEnabledByDefault=true`:
trimming switches that off by default, and without it saves and settings
silently stop working. Use `linux-x64` or `osx-arm64` for other platforms.
