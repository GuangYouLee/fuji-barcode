# Fuji Barcode

Simple Avalonia desktop app for barcode-driven RPA execution.

## What This App Does

The app accepts barcode scans from a keyboard-wedge scanner and turns them into an RPA script run.

It supports two scan modes:

- `Log ID`
  - The scanned value is treated as a log ID.
  - The app looks up that log ID in the local SQLite `barcode_recipe_mappings` table.
  - The lookup returns a `recipe_name`.

- `Recipe`
  - The scanned value is treated as the recipe name directly.
  - No database lookup is needed.

After the recipe name is known, the app:

1. calls `rpa-engine` `GET /api/scripts` — returns all top-level **folders** and standalone **`.json` files** from the engine's `scripts` directory
2. matches the recipe name to a script/package name
3. calls `POST /run/{scriptName}` — the engine starts the script asynchronously and immediately returns HTTP 202 Accepted
4. shows the launch result in the operator status text

## How Matching Works

Script matching is implemented in [Helpers/RecipeScriptResolver.cs](C:\Users\Lee%20Guang%20You\Documents\BioE%20Repo\fuji-barcode\Helpers\RecipeScriptResolver.cs).

Rules:

- exact match wins
- otherwise names starting with `recipeName_` are considered
- if multiple versioned matches exist, the highest `_Vx.y` version wins
- if matches are still ambiguous, the app stops and shows an error instead of guessing

Example:

- recipe name: `group_script1`
- matching engine script: `group_script1_202605261129_V1.0`

## Main Screen

The main window is the operator screen.

It has:

- one scan input box
- mode toggle: `Log ID` or `Recipe`
- `Submit` button
- `Admin` button
- status text

Behavior:

- the input is focused on load
- scanner input can be submitted with Enter
- the app clears the scan input after each attempt
- while processing, the screen is guarded by `IsBusy`

Main flow is in [ViewModels/MainWindowViewModel.cs](C:\Users\Lee%20Guang%20You\Documents\BioE%20Repo\fuji-barcode\ViewModels\MainWindowViewModel.cs).

## Admin Screen

The admin screen is for maintaining barcode-to-recipe mappings inside the app.

It supports CRUD:

- `Create`
  - enter `Log ID` and `Recipe name`
  - click `Save`
- `Read`
  - click `Refresh`
  - view mappings in the list
- `Update`
  - select a row
  - edit the values
  - click `Save`
- `Delete`
  - select a row or enter a log ID
  - click `Delete`

Mutating actions have confirmation dialogs:

- save confirmation for create/update
- delete confirmation for delete

Admin logic is in:

- [ViewModels/AdminWindowViewModel.cs](C:\Users\Lee%20Guang%20You\Documents\BioE%20Repo\fuji-barcode\ViewModels\AdminWindowViewModel.cs)
- [Views/AdminWindow.axaml](C:\Users\Lee%20Guang%20You\Documents\BioE%20Repo\fuji-barcode\Views\AdminWindow.axaml)

## Database

The app uses an embedded SQLite database (no external database server required).

The database file is automatically created in the per-user local app-data folder at:

- **Windows:** `C:\Users\<username>\AppData\Local\fuji-barcode\barcode.db`
- **Linux:** `~/.local/share/fuji-barcode/barcode.db` (or equivalent `XDG_DATA_HOME`)

The file is placed in the same folder as `user-preferences.json`.

Table structure:

- `log_id`
  - scanned barcode value
  - primary key
- `recipe_name`
  - recipe used for script lookup
- `updated_at`
  - last update timestamp (ISO 8601 text)

The table schema is created automatically by [Services/BarcodeLookupService.cs](Services/BarcodeLookupService.cs) on startup.

## RPA Engine

The app talks to `rpa-engine` using HTTP.

Config:

```json
{
  "RpaEngine": {
    "BaseUrl": "http://localhost:5000",
    "ApiKey": "",
    "TargetName": ""
  }
}
```

Fields:

- `BaseUrl`
  - root URL of the engine
- `ApiKey`
  - optional API key header `X-API-Key`
- `TargetName`
  - optional target appended as `?target=...`

Client implementation:

- [Services/RpaEngineClient.cs](C:\Users\Lee%20Guang%20You\Documents\BioE%20Repo\fuji-barcode\Services\RpaEngineClient.cs)

## Typical Operator Flow

### Log ID mode

1. operator leaves mode on `Log ID`
2. scanner scans barcode
3. app looks up `log_id -> recipe_name` in local SQLite
4. app resolves the matching engine script
5. app triggers `rpa-engine` run API
6. app shows result in status text

### Recipe mode

1. operator switches mode to `Recipe`
2. scanner scans recipe name directly
3. app skips database lookup
4. app resolves the matching engine script
5. app triggers `rpa-engine` run API
6. app shows result in status text

## Build And Run

Build:

```powershell
dotnet build .\fuji-barcode.slnx
```

Run:

```powershell
dotnet run --project .\fuji-barcode.csproj
```

Tests:

```powershell
dotnet test .\fuji-barcode.Tests\fuji-barcode.Tests.csproj
```

## Notes

- The test project currently covers script resolution logic and storage service.
- The app assumes the barcode scanner acts like a keyboard and sends Enter.
- The app uses an embedded SQLite database — no PostgreSQL installation required.
- The database file is stored in the per-user local app-data folder, same as `user-preferences.json`.
- Current builds may still show the known transitive `Tmds.DBus.Protocol` warning from the Avalonia dependency chain.
