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

Players don't need the SDK or MGCB. Publish a build that already contains the
compiled content:

```
cd 7th-Dawn/DuskAndDawn
dotnet publish -c Release -r win-x64 --self-contained
```

Zip the `bin/Release/net8.0/win-x64/publish` folder and share that. Use
`linux-x64` or `osx-arm64` for other platforms.
