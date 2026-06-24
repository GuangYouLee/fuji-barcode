  # AI Implementation Plan

  ## Goal

  Make fuji-barcode treat the current rpa-engine /run/{name} success payload
  as a successful run, so the operator UI no longer shows a false failure
  after a script is accepted.

  ## Assumptions

  - Use the Client only scope: fix fuji-barcode and leave rpa-engine
    unchanged.

  - The authoritative engine success contract remains HTTP success (202
    Accepted today) plus a JSON body like { "status": "started",
    "scriptName": "...", "totalCommands": 5 }.

  - Existing non-2xx handling in the desktop app should stay as-is through
    EnsureSuccessStatusCode() and the existing HttpRequestException path in
    the view-model.

  ## Current Code Findings

  - Services/RpaEngineClient.cs
      - RunScriptAsync(string scriptName) posts to /run/{scriptName},
        appends ?target=... when configured, then deserializes directly into
        ScriptRunResult.

      - ScriptRunResult currently only maps success and message.

  - ViewModels/MainWindowViewModel.cs
      - ProcessScanAsync() treats result.Success == true as Run OK: ...;
        otherwise it shows Run failed: ....

  - rpa-engine/Api/ScriptApi.Helpers.cs
      - Local start returns Results.Accepted(new RunResponse { Status =
        "started", ScriptName = ..., TotalCommands = ... }).

  - rpa-engine/Models/JsonContext.cs
      - RunResponse contains status, scriptName, and totalCommands; it does
        not contain success or message.

  - ..\fuji-barcode.Tests
      - No existing tests cover RpaEngineClient; only RecipeScriptResolver is
        covered today.

  ## Files to Inspect First

  - Services/RpaEngineClient.cs
  - ViewModels/MainWindowViewModel.cs
  - ..\fuji-barcode.Tests\RecipeScriptResolverTests.cs
  - C:\Users\Lee Guang You\Documents\BioE Repo\rpa-engine\rpa-
    engine\Api\ScriptApi.Helpers.cs

  - C:\Users\Lee Guang You\Documents\BioE Repo\rpa-engine\rpa-
    engine\Models\JsonContext.cs

  ## Files to Change

  - Services/RpaEngineClient.cs
      - Target: RpaEngineClient, RunScriptAsync(string scriptName),
        ScriptRunResult

      - Purpose: normalize the engine’s accepted-run response into the
        desktop app’s success/failure model

  - ..\fuji-barcode.Tests\RpaEngineClientTests.cs
      - Target: new test class RpaEngineClientTests
      - Purpose: cover current engine payload shape, legacy success/message
        shape, and empty-success-body fallback

  ## Detailed Implementation Steps

  ### Step 1: Add a test seam for HTTP response simulation

  - File: Services/RpaEngineClient.cs
  - Target: RpaEngineClient constructors
  - Action: update
  - Change details:
      - Keep the existing RpaEngineClient(IConfiguration configuration)
        constructor for app DI.

      - Add a second constructor that accepts an HttpClient and optional
        targetName.

      - Make the existing configuration constructor build/configure the
        HttpClient exactly as today, then delegate to the new constructor.

      - Do not change DI registration in App.axaml.cs; the current app wiring
        should continue to use the configuration constructor unchanged.

  - Edge cases:
      - Preserve BaseAddress.
      - Preserve optional X-API-Key.
      - Preserve optional target query support.

  - Verification:
      - RpaEngineClientTests can instantiate the client with a fake
        HttpMessageHandler and no config file dependency.

  ### Step 2: Normalize run success from HTTP success plus current engine
  JSON

  - File: Services/RpaEngineClient.cs
  - Target: RunScriptAsync(string scriptName)
  - Action: replace
  - Change details:
      - Stop deserializing the /run/{name} response directly into the current
        ScriptRunResult.

      - After response.EnsureSuccessStatusCode(), read the response body as
        text and normalize it into ScriptRunResult.

      - Add a private response DTO inside this file for run payload parsing,
        with nullable fields for both shapes:
          - success
          - message
          - status
          - scriptName
          - totalCommands

      - Add a private helper in RpaEngineClient to map that raw DTO into the
        returned ScriptRunResult.

      - Mapping rules:
          - If raw success is explicitly true, return Success = true and keep
            message when present.

          - If raw success is explicitly false, return Success = false and
            keep message when present.

          - Otherwise, if the body has a non-empty status field from the
            engine payload, treat the run as successful.

          - For successful runs without a message, prefer a useful fallback
            message in this order:
              1. scriptName from the engine payload
              2. the method parameter scriptName

          - If the body is empty after a successful HTTP status, still return
            Success = true with fallback message scriptName.

          - If the body is malformed JSON after a successful HTTP status, do
            not surface a false failure; return Success = true with fallback
            message scriptName.

      - Keep EnsureSuccessStatusCode() before this mapping so 4xx/5xx still
        flow into the existing view-model exception handling.

  - Edge cases:
      - Do not turn explicit engine failure payloads (success: false) into
        success.

      - Do not break future legacy callers if the engine later returns
        success/message.

      - Do not remove the ?target= append logic.

  - Verification:
      - Unit tests for current engine payload, legacy payload, explicit
        failure payload, and empty success body.

  ### Step 3: Keep the UI behavior unchanged and let the client fix supply
  the right result

  - File: ViewModels/MainWindowViewModel.cs
  - Target: ProcessScanAsync()
  - Action: no change
  - Change details:
      - Do not rewrite the success/failure UI logic.
      - The fix should be entirely upstream in RpaEngineClient, so the
        existing Run OK: and Run failed: branches remain valid.

  - Edge cases:
      - Existing connection-error and invalid-operation messages must remain
        unchanged.

  - Verification:
      - No view-model edits required if RunScriptAsync() now returns the
        correct ScriptRunResult.

  ### Step 4: Add focused unit tests for the run response contract

  - File: ..\fuji-barcode.Tests\RpaEngineClientTests.cs
  - Target: new class RpaEngineClientTests
  - Action: add
  - Change details:
      - Add a small fake HttpMessageHandler in the test file to return
        controlled responses and capture the outgoing request URI.

      - Add these tests:
          1. RunScriptAsync_returns_success_for_started_status_payload
              - Response: 202 Accepted
              - Body: { "status": "started", "scriptName": "group_script1",
                "totalCommands": 5 }

              - Expect: Success == true
              - Expect: message uses group_script1

          2. RunScriptAsync_preserves_legacy_success_payload
              - Response: 200 OK
              - Body: { "success": true, "message": "Started" }
              - Expect: Success == true
              - Expect: message Started

          3. RunScriptAsync_preserves_explicit_failure_payload
              - Response: 200 OK
              - Body: { "success": false, "message": "Already running" }
              - Expect: Success == false
              - Expect: message Already running

          4. RunScriptAsync_treats_empty_success_body_as_success
              - Response: 202 Accepted
              - Body: empty string
              - Expect: Success == true
              - Expect: message falls back to the requested script name

          5. RunScriptAsync_appends_target_query_when_target_name_is_configur
             ed
              - Construct client with targetName
              - Call RunScriptAsync("abc")
              - Expect posted URI path /run/abc?target=...

  - Edge cases:
      - Use exact status codes that match current engine behavior.
      - Keep test bodies small and explicit; do not add network or server
        integration tests for this fix.

  - Verification:
      - dotnet test ..\fuji-barcode.Tests\fuji-barcode.Tests.csproj --filter
        RpaEngineClientTests

  ## Data / API / State Changes

  - No database changes.
  - No config schema changes.
  - No UI layout changes.
  - No rpa-engine API changes in this plan.
  - Internal desktop-client behavior change only:
      - successful HTTP /run/{name} responses with the current engine payload
        will now map to ScriptRunResult.Success = true.

  ## Tests / Verification

  - dotnet build .\fuji-barcode.slnx
  - dotnet test ..\fuji-barcode.Tests\fuji-barcode.Tests.csproj --filter
    RpaEngineClientTests

  - dotnet test ..\fuji-barcode.Tests\fuji-barcode.Tests.csproj
  - Manual check after implementation:
      - run the desktop app against the current rpa-engine
      - submit a known-good recipe/object ID
      - confirm the status text shows Run OK: instead of Run failed: Unknown
        error

  ## Risks / Edge Cases

  - If the engine ever returns 2xx with a semantically negative payload but
    without success: false, the client will still treat it as accepted. That
    is acceptable for this fix because the current engine contract uses
    4xx/5xx for start failures and 202 Accepted for accepted runs.

  - The fallback message should not become empty; always fall back to the
    requested scriptName.

  - Keep the fix local to the run-start response path; do not widen it into
    unrelated list/status endpoint parsing.

  ## Do Not Change

  - C:\Users\Lee Guang You\Documents\BioE Repo\rpa-engine\rpa-engine\...
  - ViewModels/MainWindowViewModel.cs success/failure branch text
  - Helpers/RecipeScriptResolver.cs
  - Services/BarcodeLookupService.cs
  - App.axaml.cs DI registration behavior, unless the new constructor
    addition makes a minimal no-op delegation cleanup necessary

  - appsettings.json keys or semantics

  ## Handoff Notes for KiloCode

  KiloCode must:

  - follow this plan step by step
  - edit only the files listed unless the plan is impossible
  - use the exact target functions/components/routes listed above
  - make the smallest correct changes
  - preserve existing style
  - avoid unrelated refactors
  - not redesign the solution
  - report any deviation from the plan in .agents/reports/ai-code.md
