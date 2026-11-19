# SelectMOD handoff

Snapshot as of 2026-11-19 JST. Repository: [ROMEKANA/SlayTheSpire2ModAncientBossSelecter](https://github.com/ROMEKANA/SlayTheSpire2ModAncientBossSelecter). The last functional change on main at handoff was `11830cd`, the Workshop uploader fix. Read [AGENTS.md](../AGENTS.md), [DESIGN_AND_LESSONS.md](../DESIGN_AND_LESSONS.md), and [STS2_MODDING.md](../STS2_MODDING.md) before changing behavior. Give the owner short instructions in Japanese. Use the existing script entry points.

## Current handoff — 0.8.0

Owner observed the solo A4-off / A5-on combination: potion capacity retained and Ascender’s Bane present. No claim for enemy attacks, odds, double boss or multiplayer hardware yet. The new fixed ten-slot UI lets the host or solo player edit lower levels without changing the scalar. Unavailable slots reserve space. The optional ModifierModel.Icon getter patch supplies the ascension model's own cached blue flame. The native top-left cumulative tooltip is still unfiltered. The Japanese README covers updating the existing unlisted standalone Workshop item and a two-peer smoke test.

## Prior handoff — 0.7.4

0.7.3 checks the saved effective mask against the native scalar and the host encounter envelope before restoration. 0.7.4 ties asynchronous load cleanup to the initiating attempt and offers a manual BaseLib comparison build. Use Setup with no switch for standalone, or with -BaseLib for the alternative. Both keep the same saved IDs and run rules. The BaseLib path uses the real 3.4.8 AddUnique API to attach the ascension controls. The optional package provides compile references only. Owner must have BaseLib and its dependencies enabled to load that variant. See README for the two commands. Steam upload status and weekend multiplayer acceptance are unchanged.

## Previous handoff — 0.7.2

Owner confirmed the 0.6.0 Workshop upload succeeded. Do not repeat publication. Multiplayer hardware testing is scheduled for the weekend. The development checkout now implements normal/custom ascension selection. See [the implementation history and acceptance cases](ASCENSION_AUDIT.md#normal-and-custom-runs-in-072). Use the existing Setup entry point on the reviewed installation for the first game-referenced build and solo smoke test. No new Workshop upload was requested.

## Prior handoff — 0.6.0

Distribute the current SelectMOD 0.6.0 gameplay build through Steam Workshop as an unlisted item and share the subscription link with friends. Unlisted means anyone with the link can access it. Current priority is verifying publication and subscription, then testing multiplayer with friends. The owner deferred ordinary-run classification and individual ascension toggles.

## Evidence and open state

| Stage | Evidence |
| --- | --- |
| Solo 0.5.0 | Owner reported it worked after the Neow fix and cross-Act expansion. Coverage of individual encounters and save/resume was unspecified. |
| 0.6.0 build | Owner's Windows transcript shows .NET SDK 9.0.321, 185 contract checks, a successful build against the installed game DLLs with zero warnings/errors, and successful Workshop staging. |
| Workshop attempt | Uploader reached `Initializing Steam`. Windows PowerShell raised `NativeCommandError` on Steam SDK stderr `Setting breakpad minidump AppID = 2868840`. The transcript ends there. Item creation and upload status remain unknown. |
| Uploader fix | Published to main in `11830cd`. The wrapper now captures both streams directly and checks the native exit code. The Linux PowerShell child-process regression and script fixtures passed. Owner-side retry, subscription and real multiplayer remain unconfirmed. |

The 185 independent assertions and two separate .NET receiver processes cover fixture behavior. The Godot compile check uses public Godot APIs and game fixtures. See [VALIDATION.md](VALIDATION.md) for the verification boundaries.

## Resume Workshop publication

1. Check for a newer owner report or Workshop URL first. If the item already works, continue with subscription and game checks. The agent environment has no confirmed access to the owner's Windows installation or signed-in Steam session.
2. On the owner's Windows PC, update from the repository root with `git pull`. If the old ModUploader process is still active, wait for it to finish before inspecting the owner's Workshop items. The old PowerShell failure could have left the child process running.
3. Keep `local/workshop/`. The upload marker at `local/workshop/upload-pending.txt` and item IDs in `local/workshop/SelectMOD/mod_id.txt` and `local/workshop/SelectMOD-item-id.txt` support recovery. If an existing item ID is already stored, run `.\scripts\Publish-Workshop.ps1`. If an item exists but its ID is missing locally, run `.\scripts\Publish-Workshop.ps1 -WorkshopItemId <numeric-ID>`. If the owner's Workshop has no item from the interrupted attempt, run `.\scripts\Publish-Workshop.ps1 -RetryNewItem`. A pending marker without a known ID is intentional.
4. If preparation fails, collect `local/workshop-prepare-report.txt`. If uploading fails, collect `local/workshop-upload-report.txt`, which records stdout, stderr and the exit code after the uploader exits. The wrapper prints a waiting message during the upload. The official uploader can warn about a visibility setter failure and still exit successfully.
5. Before sharing the returned URL, verify the item actually shows unlisted visibility and handle any Steam Workshop agreement prompt. For subscription testing, wait for download and restart STS2. If the owner's manual copy is installed, close STS2 and run `.\scripts\Disable.ps1` before testing the subscription because an equal-version manual copy takes priority. Verify the mod loads and the character screen has the settings panel.
6. With two subscribers on the same game and active gameplay mod versions, start a fresh Standard custom run. Confirm the guest sees the host's selection, both start, and both get the same fixed encounter. Then expand coverage using [MULTIPLAYER.md](MULTIPLAYER.md). Capture each player's logs with `.\scripts\Export-ProbeReport.ps1` and compare with `.\scripts\Compare-MultiplayerReports.ps1` using the README instructions. Matching logs supplement real gameplay observations.

See the Japanese commands and owner instructions in [README.md](../README.md#workshopで知人に配る060).

## Implementation map

- [Publish-Workshop.ps1](../scripts/Publish-Workshop.ps1) calls [Prepare-Workshop.ps1](../scripts/Prepare-Workshop.ps1), then uses the official MegaCrit ModUploader v0.2.0 with an archive SHA256 pin. The process wrapper is in [Common.ps1](../scripts/Common.ps1). `-PrepareOnly` builds and stages locally. The upload payload is exactly `SelectMOD.dll` and `SelectMOD.json`. Workshop metadata and preview sit outside the uploaded content. Never package the manual install folder with its machine-specific `SelectMOD.paths.txt`.
- [ModEntry.cs](../src/SelectMOD/ModEntry.cs) checks reviewed game references and enables the settings panel by default when a subscriber has no config. Standard Windows logs are at `%LOCALAPPDATA%\SelectMOD\selectmod.log`. [Setup.ps1](../scripts/Setup.ps1) remains the manual install route. Keep the three-line `local/paths.txt`, saves, configuration and private reports out of Git and Workshop.
- [EncounterSelectionPanel.cs](../src/SelectMOD/EncounterSelectionPanel.cs), [EncounterCatalog.cs](../src/SelectMOD/EncounterCatalog.cs), and [EncounterRuntime.cs](../src/SelectMOD/EncounterRuntime.cs) cover the collapsible selector, stable candidates and fixed selection. Random uses game rules. Cross-Act choices are experimental. Fixed solo selections use custom classification. Run-owned settings store the resolved IDs. [NeowRewardPatch.cs](../src/SelectMOD/NeowRewardPatch.cs) filters the mod's storage-only modifiers from Neow's reward decision.
- [MultiplayerPatches.cs](../src/SelectMOD/MultiplayerPatches.cs), [MultiplayerSelection.cs](../src/SelectMOD/MultiplayerSelection.cs), and [MultiplayerContract.cs](../src/SelectMOD/MultiplayerContract.cs) implement the 2–4 player Standard custom beta. The host owns the draft and the game's final lobby start message carries the complete snapshot. Guests display it read-only. Cumulative ascension uses the host value. Saved resume needs the original full party. Live rejoin and host migration are outside current support.
- Supported reference evidence comes from Steam Build 26660232. Use [reviewed-builds.json](../compatibility/reviewed-builds.json) and [INSTALLED_GAME_REVIEW.md](INSTALLED_GAME_REVIEW.md). For changed game DLLs, collect installed-source evidence with [Export-GameSources.ps1](../scripts/Export-GameSources.ps1) and review the relevant call chain before updating the fingerprints.

## Checks for subsequent changes

Run the smallest relevant set. From the repository root, the uploader regression commands are `pwsh -NoProfile -File tests/NativeProcess.Tests.ps1`, `pwsh -NoProfile -File tests/Workshop.Tests.ps1`, and `pwsh -NoProfile -File tests/Scripts.Tests.ps1`. For gameplay changes, run `dotnet run --project tests/SelectMOD.ContractTests -c Release` and `dotnet build tests/SelectMOD.GodotCompileChecks -c Release --nologo` as relevant. Follow the repo's direct-main Git workflow after verification.
