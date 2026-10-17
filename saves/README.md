# Saves

Saved games carried over from the development VM, so a fresh clone can carry on with the same character. The game
doesn't read this folder by itself. Either copy the `.json` files into the game's save folder:

- Windows: `%LOCALAPPDATA%\AshenReliquary\saves`
- Linux: `~/.local/share/AshenReliquary/saves`
- macOS: `~/Library/Application Support/AshenReliquary/saves`

or point the game at this folder with the `ASHEN_SAVE_DIR` environment variable (it then saves here too), e.g.
`ASHEN_SAVE_DIR=saves dotnet run --project src/AshenReliquary.App` from the repo root.

- `the-ashbound-e9f063.json`: The Ashbound, a level 1 Human Fighter on the Gallows Road.
