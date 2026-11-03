# Fuji Barcode

Simple Avalonia desktop app for barcode-driven RPA execution.

## What This App Does

The app accepts barcode scans from a keyboard-wedge scanner, authenticates the operator on a touchscreen keypad, and records product traceability locally.

It supports two scan modes:

- `Log ID`
  - The scanned value is treated as a log ID.
  - The app looks up that log ID in the local SQLite `barcode_recipe_mappings` table.
  - The lookup returns a `recipe_name`.

- `Recipe`
  - The scanned value is treated as the recipe name directly.
  - The recipe name must exactly match a recipe saved in the Admin mappings.

The app validates each scan against the saved mappings before opening the Operator Login dialog. Log ID scans use the existing exact/unique suffix lookup to resolve a nonempty recipe; Recipe scans check for an exact saved recipe name. Unknown values, ambiguous Log IDs, or database errors stop the scan before login, without a history row or script launch. A global script override does not bypass this check.

Each valid scan opens one fresh Operator ID/PIN dialog; credentials are never reused. After successful authentication, the app:

1. inserts an immutable history row with the authenticated operator, authentication time, scanned value, Log ID (for Log ID mode), and recipe
2. uses the saved global RPA script override when one is configured; otherwise it uses the recipe name as the script name
3. calls `POST /run/{scriptName}` — the engine starts the script asynchronously and immediately returns HTTP 202 Accepted
4. appends the launch outcome and shows it in the operator status text

Wrong credentials keep Operator Login open, clear the PIN, and show a retry message; the pending scan continues only after successful authentication. Cancel closes the dialog and discards the scan. Neither rejection nor cancellation writes history or launches a script. A failed initial history write also prevents launch. `run_ok` means the launch request was accepted, not that the script finished.

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
- each scan requires touchscreen entry of a numeric Operator ID and PIN; the PIN is masked
- enter four Operator ID digits to automatically select PIN; entering the fourth PIN digit automatically submits the login
- tap either display to edit it using the number pad, Clear, and Delete; Confirm remains available for manual submission
- keyboard/scanner input cannot enter credentials or activate Confirm in the keypad
- while processing or viewing Admin, scanning is disabled

Main flow is in [ViewModels/MainWindowViewModel.cs](C:\Users\Lee%20Guang%20You\Documents\BioE%20Repo\fuji-barcode\ViewModels\MainWindowViewModel.cs).

## Admin Screen

The admin screen maintains barcode-to-recipe mappings and provides Operators and History windows.

- `Operators`: create an Operator ID and PIN, reset a PIN, or delete an account using touch controls. Both Operator ID and PIN must contain exactly 4 digits. Leading zeroes are preserved. Resets replace the salted PBKDF2 hash; existing PINs cannot be displayed. Account deletion leaves history intact. Accounts created with longer or shorter IDs must be recreated with a 4-digit ID; PINs outside the 4-digit rule must be reset.
- Account creation and PIN resets still require tapping Confirm to save.
- `History`: browse immutable scan records with Refresh and Previous/Next controls. Tap Select month to choose a month/year, then tap Apply to filter by local calendar month; All months clears the filter. Rows show the operator, full matched database Log ID, raw scanned value, recipe, and timestamp. Type and outcome are retained in storage and hidden in this view. Direct Recipe scans leave Log ID empty; scans are not linked through remembered state.
- New Log ID records snapshot the matched database ID separately from the raw scan. Stored Log IDs are always displayed unchanged, including exact matches. Only legacy records without a stored Log ID infer one from the current mappings; missing or ambiguous matches display the raw scan. Stored history is not rewritten.
- The existing Unysis script password dialog remains separate from operator authentication. Operator management uses the existing Admin access model.

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

Existing mapping table, `barcode_recipe_mappings`:

- `log_id`
  - scanned barcode value
  - primary key
- `recipe_name`
  - recipe used for script lookup
- `updated_at`
  - last update timestamp (ISO 8601 text)

Additional tables:

- `operators`: `operator_id` and `pin_hash`; no plaintext PIN is stored.
- `scan_history`: one immutable row per accepted scan, including a unique ID, UTC authentication timestamp, operator ID, scan type/value, nullable `log_id`, recipe ID, and initial outcome `pending`. Log ID scans store the full matched database Log ID and resolved recipe; `scanned_value` preserves the raw scan. Direct Recipe scans store the recipe with an empty Log ID.
- `scan_outcomes`: an appended final launch outcome for a recipe scan (`run_ok`, `run_failed`, or `connection_error`). History combines this with the original scan row; neither record is overwritten.

An interrupted launch or failed outcome save may leave a row `pending`; the app cannot infer whether an asynchronous script completed. A connection error may also leave actual launch status uncertain.

The schema is extended automatically by [Services/BarcodeLookupService.cs](Services/BarcodeLookupService.cs) on startup. Existing mappings and settings are retained. Credentials and history belong to the local OS user's database; other users or PCs do not automatically share them.

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

Admin creates the operator account through Operators before first use. Each scan is an independent transaction.

### Log ID mode

1. operator leaves mode on `Log ID`
2. scanner scans barcode
3. app looks up `log_id -> recipe_name` in local SQLite; missing or ambiguous mappings stop before login
4. operator enters fresh Operator ID and PIN on the touchscreen keypad
5. app records the authenticated operator, full matched Log ID, raw scan, and resolved recipe
6. app uses the global override or recipe name as the script name and triggers the run API
7. app appends the launch outcome and shows it in status text

### Recipe mode

1. operator switches mode to `Recipe`
2. scanner scans recipe name directly
3. app checks that the recipe exists in saved mappings; unknown recipes stop before login
4. operator enters fresh Operator ID and PIN on the touchscreen keypad
5. app records the authenticated operator and scanned recipe; Log ID is empty
6. app uses the global override or scanned recipe name as the script name and triggers the run API
7. app appends the launch outcome and shows it in status text

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

First create an operator through Admin → Operators, and save an Admin mapping from `TEST-BC-001` to `group_script1`. Recipe mode accepts only recipe names registered in these mappings.

1. switch the app to `Recipe`
2. scan or type `group_script1`
3. submit and enter fresh Operator ID/PIN on the touch keypad

Expected status flow:

- `Recipe: group_script1`
- `Using recipe script: group_script1`
- launch result (accepted or failed); History contains the operator and recipe

If the script name is wrong, `rpa-engine` returns the run error.

### 6. Test Log ID mode

Use the mapping prepared above in the app's `Admin` screen:

- `Log ID`: `TEST-BC-001`
- `Recipe name`: `group_script1`

Then:

1. switch back to `Log ID`
2. scan or type `TEST-BC-001`
3. submit and enter fresh Operator ID/PIN on the touch keypad

Expected status flow:

- `Resolved recipe: group_script1`
- `Using recipe script: group_script1`
- launch result (accepted or failed); History also preserves the scanned Log ID

### 7. Quick failure checks

Use these to confirm the integration errors are understandable:

- wrong `BaseUrl`
  - expected: `Connection error: ...`
- missing barcode mapping in `Log ID` mode
  - expected: `Log ID '...' not found`; no login dialog, history row, or launch
- unknown recipe in `Recipe` mode
  - expected: `Recipe '...' not found in saved mappings`; no login dialog, history row, or launch
- wrong script name in `rpa-engine`
  - expected: the engine's run error
- remote engine with wrong API key
  - expected: HTTP 401 launch rejection recorded as `run_failed`
- wrong Operator ID/PIN
  - expected: login remains open with a retry message and an empty PIN; no history row or launch until successful authentication
- Cancel
  - expected: dialog closes with no history row or launch
- initial history write failure
  - expected: no launch

## Notes

- The test project covers the HTTP client and storage service.
- The app assumes the barcode scanner acts like a keyboard and sends Enter.
- The app uses an embedded SQLite database — no PostgreSQL installation required.
- The database file is stored in the per-user local app-data folder, same as `user-preferences.json`.
- `Tmds.DBus.Protocol` is pinned to the patched `0.21.3` release to override Avalonia's transitive `0.21.2` dependency.
