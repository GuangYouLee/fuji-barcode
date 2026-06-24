# Fuji Barcode

Simple Avalonia desktop app for barcode-driven RPA execution.

## What This App Does

The app accepts barcode scans from a keyboard-wedge scanner and turns them into an RPA script run.

It supports two scan modes:

- `Object ID`
  - The scanned value is treated as an object ID.
  - The app looks up that object ID in PostgreSQL table `barcode_recipe_mappings`.
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
- mode toggle: `Object ID` or `Recipe`
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
  - enter `Object ID` and `Recipe name`
  - click `Save`
- `Read`
  - click `Refresh`
  - view mappings in the list
- `Update`
  - select a row
  - edit the values
  - click `Save`
- `Delete`
  - select a row or enter an object ID
  - click `Delete`

Mutating actions have confirmation dialogs:

- save confirmation for create/update
- delete confirmation for delete

Admin logic is in:

- [ViewModels/AdminWindowViewModel.cs](C:\Users\Lee%20Guang%20You\Documents\BioE%20Repo\fuji-barcode\ViewModels\AdminWindowViewModel.cs)
- [Views/AdminWindow.axaml](C:\Users\Lee%20Guang%20You\Documents\BioE%20Repo\fuji-barcode\Views\AdminWindow.axaml)

## Database

The app uses PostgreSQL through `Npgsql`.

Current connection string is in [appsettings.json](C:\Users\Lee%20Guang%20You\Documents\BioE%20Repo\fuji-barcode\appsettings.json):

```json
{
  "ConnectionStrings": {
    "BarcodeDb": "Host=localhost;Database=barcode;Username=postgres;Password=1234"
  }
}
```

Schema file:

- [sql/barcode_recipe_mappings.sql](C:\Users\Lee%20Guang%20You\Documents\BioE%20Repo\fuji-barcode\sql\barcode_recipe_mappings.sql)

Table structure:

- `object_id`
  - scanned barcode value
  - primary key
- `recipe_name`
  - recipe used for script lookup
- `updated_at`
  - last update timestamp

Database access lives in [Services/BarcodeLookupService.cs](C:\Users\Lee%20Guang%20You\Documents\BioE%20Repo\fuji-barcode\Services\BarcodeLookupService.cs).

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

### Object ID mode

1. operator leaves mode on `Object ID`
2. scanner scans barcode
3. app looks up `object_id -> recipe_name` in PostgreSQL
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

- The test project currently covers script resolution logic, not UI or live database flows.
- The app assumes the barcode scanner acts like a keyboard and sends Enter.
- The app currently uses local PostgreSQL and local/default `rpa-engine` settings from `appsettings.json`.
- Current builds may still show the known transitive `Tmds.DBus.Protocol` warning from the Avalonia dependency chain.
