# User data

The saved games and settings from the development VM, laid out like the game's own data folder, so a fresh clone can
carry on with the same character and volumes. The game doesn't read this folder by itself. Either copy its contents
into the game's data folder:

- Windows: `%LOCALAPPDATA%\AshenReliquary\`
- Linux: `~/.local/share/AshenReliquary/`
- macOS: `~/Library/Application Support/AshenReliquary/`

(`saves/*.json` into its `saves` folder, `settings.json` beside it), or point the game at this folder with the
`ASHEN_SAVE_DIR` environment variable. It then reads and writes the saves here, and `settings.json` one level up,
which is this folder. From the repo root, for example:

    ASHEN_SAVE_DIR=userdata/saves dotnet run --project src/AshenReliquary.App

(On Windows PowerShell: `$env:ASHEN_SAVE_DIR="userdata/saves"; dotnet run --project src/AshenReliquary.App`.)

- `saves/the-ashbound-e9f063.json`: The Ashbound, a level 1 Human Fighter on the Gallows Road.
- `settings.json`: the volumes (master 0.8, effects 0.9, music 0.76, ambience 0.51).
