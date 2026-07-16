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

1. uses the saved global RPA script override when one is configured; otherwise it uses the recipe name as the script name
2. calls `POST /run/{scriptName}` — the engine starts the script asynchronously and immediately returns HTTP 202 Accepted
3. shows the launch result in the operator status text

The Admin screen can call `GET /api/scripts` to populate the optional global-script autocomplete.

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

Config is shipped as `appsettings.default.json`. On first launch the app copies it to `%LOCALAPPDATA%\fuji-barcode\appsettings.json`. Edit the LocalAppData copy. Default values:

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
4. app uses the global override or recipe name as the script name
5. app triggers `rpa-engine` run API
6. app shows result in status text

### Recipe mode

1. operator switches mode to `Recipe`
2. scanner scans recipe name directly
3. app skips database lookup
4. app uses the global override or scanned recipe name as the script name
5. app triggers `rpa-engine` run API
6. app shows result in status text

## Install

### Windows

For end users, build the MSI installer:

```powershell
powershell -ExecutionPolicy Bypass -File .\packaging\windows\build-msi.ps1
```

Installer output:

- `artifacts/installers/fuji-barcode-win-x64.msi`

Install steps:

1. double-click `fuji-barcode-win-x64.msi`
2. follow the Windows Installer prompts
3. launch `Fuji Barcode` from the desktop or Start Menu shortcut
4. edit `C:\Users\<username>\AppData\Local\fuji-barcode\appsettings.json` if `rpa-engine` settings need to change

Notes:

- the MSI is self-contained for Windows x64
- on first launch the app creates `barcode.db`, `user-preferences.json`, and `appsettings.json` under `AppData\Local\fuji-barcode`

### Linux

There is no Linux installer package in this repo now. Install by publishing and copying the app folder.

Build a self-contained Linux publish:

```bash
dotnet publish ./fuji-barcode.csproj -c Release -r linux-x64 --self-contained true -o ./artifacts/publish/linux-x64
```

Install/run steps:

1. copy `./artifacts/publish/linux-x64/` to the target Linux machine
2. make the binary executable if needed: `chmod +x ./fuji-barcode`
3. run the app from that folder: `./fuji-barcode`
4. edit `~/.local/share/fuji-barcode/appsettings.json` if `rpa-engine` settings need to change

Notes:

- the app stores `barcode.db`, `user-preferences.json`, and `appsettings.json` under the Linux local app-data folder
- the exact Linux data path follows .NET `LocalApplicationData` / `XDG_DATA_HOME`

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

## Test With rpa-engine

Use this when you want to verify the real app-to-engine path instead of only unit tests.

### 1. Start rpa-engine

From `C:\Users\Lee Guang You\Documents\BioE Repo\rpa-engine\rpa-engine`:

```powershell
dotnet run --project .\RpaLinux.csproj
```

Expected:

- engine listens on `http://localhost:5000`
- `GET /api/scripts` returns the top-level script folders / `.json` files
- `POST /run/{scriptName}` accepts runs

If `Server:ApiKey` is empty in `rpa-engine\appsettings.json`, the engine may generate a key file at:

- `C:\rpa-data\generated-api-key.txt`

For local `localhost` testing, `fuji-barcode` can leave `RpaEngine:ApiKey` empty because loopback requests are allowed.

### 2. Point fuji-barcode at the engine

Edit the live app config:

- `C:\Users\<username>\AppData\Local\fuji-barcode\appsettings.json`

Use:

```json
{
  "RpaEngine": {
    "BaseUrl": "http://localhost:5000",
    "ApiKey": "",
    "TargetName": ""
  }
}
```

If you are calling a remote engine instead of `localhost`, set:

- `BaseUrl` to the remote engine URL
- `ApiKey` to the same value used by `rpa-engine` `Server:ApiKey`, or the contents of `C:\rpa-data\generated-api-key.txt`

### 3. Prepare one known script in rpa-engine

Make sure `rpa-engine` has a top-level script or script folder whose name exactly matches the recipe you plan to scan, or save that script name as the global override in the Admin screen.

Example:

- script/package name: `group_script1`
- recipe to scan: `group_script1`

### 4. Run fuji-barcode

From this repo:

```powershell
dotnet run --project .\fuji-barcode.csproj
```

### 5. Test Recipe mode

1. switch the app to `Recipe`
2. scan or type `group_script1`
3. submit

Expected status flow:

- `Recipe: group_script1`
- `Using recipe script: group_script1`
- `Run OK: ...`

If the script name is wrong, `rpa-engine` returns the run error.

### 6. Test Log ID mode

First add a mapping in the app's `Admin` screen:

- `Log ID`: `TEST-BC-001`
- `Recipe name`: `group_script1`

Then:

1. switch back to `Log ID`
2. scan or type `TEST-BC-001`
3. submit

Expected status flow:

- `Resolved recipe: group_script1`
- `Using recipe script: group_script1`
- `Run OK: ...`

### 7. Quick failure checks

Use these to confirm the integration errors are understandable:

- wrong `BaseUrl`
  - expected: `Connection error: ...`
- missing barcode mapping in `Log ID` mode
  - expected: `Log ID '...' not found`
- wrong script name in `rpa-engine`
  - expected: the engine's run error
- remote engine with wrong API key
  - expected: HTTP 401 surfaced as a connection/request failure

## Notes

- The test project covers the HTTP client and storage service.
- The app assumes the barcode scanner acts like a keyboard and sends Enter.
- The app uses an embedded SQLite database — no PostgreSQL installation required.
- The database file is stored in the per-user local app-data folder, same as `user-preferences.json`.
- Current builds may still show the known transitive `Tmds.DBus.Protocol` warning from the Avalonia dependency chain.
