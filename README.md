# 7th Dawn

A MonoGame (DesktopGL) game. The project lives in `7th-Dawn/DuskAndDawn`.

## Building from source

1. Install the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).
2. Restore the MonoGame content builder (MGCB). It is a .NET local tool, so a
   normal NuGet restore does not install it:

   ```
   cd 7th-Dawn/DuskAndDawn
   dotnet tool restore
   ```

   The build tries to do this on its own, but if that fails (for example with
   no internet on the first build) it only prints a warning, the game's
   content is not built, and the game crashes on startup when loading
   `DefaultFont`.
3. Build and run:

   ```
   dotnet run
   ```

On Linux, building the fonts needs FreeType (`libfreetype6`).

If a build still fails after `dotnet tool restore`, delete the `bin` and `obj`
folders in `7th-Dawn/DuskAndDawn` and build again.

## Sharing the game with players

Players don't need the SDK or MGCB. Publish a build that already contains the
compiled content:

```
cd 7th-Dawn/DuskAndDawn
dotnet publish -c Release -r win-x64 --self-contained
```

Zip the `bin/Release/net9.0/win-x64/publish` folder and share that. Use
`linux-x64` or `osx-arm64` for other platforms.
