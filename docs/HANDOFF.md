# SelectMOD handoff

## Resume here — 2026-11-20 JST

The owner paused development at standalone **0.11.2**, implemented on main at `fe3aa2f`. Fetch main and compare the current HEAD and manifest with that baseline before editing. There is no queued feature or scheduled upload. Follow the new request. Read [project rules](../AGENTS.md) and the [Japanese README](../README.md) for owner-facing commands. Paths below are relative to the repository root.

### Evidence at the pause

- The owner played solo and verified A4 off with the potion slot preserved and A5 on with the curse present. A normal multiplayer host reported a one-time error after installation and could not reproduce it. The wording, originating mod and full multiplayer save/resume outcome are unknown.
- 0.11.2 restored the native tooltip title “SelectMOD ボス・エンシェント選択”. The latest fixture run passed 241 checks and a separate peer-process comparison. The standalone Godot API compile check and Workshop orchestration fixture passed. Windows game rendering and the latest release upload were not verified in that session.
- The earlier upload to item 3842017022 reached Steam commit and returned AccessDenied. That item is retired. The owner runs the uploader locally. The replacement flow stores the new ID and selected cover in `local/workshop-selectmod/`. Check the owner's latest successful upload report or URL for the actual replacement ID and version. Publishing uses unlisted visibility. BaseLib development ended at 0.11.1.

### If a bug is reported

Start with the installed versions, the first failing action and the symptom. For a recurrence on the host, collect `local/selectmod-probe.txt` immediately using `.\scripts\Export-ProbeReport.ps1`. Look for `MP_HOST_INIT_FAILED` (0.11.2 onward), its exception and the surrounding game log. For a host/guest disagreement, collect each peer's report with its role and compare using `scripts/Compare-MultiplayerReports.ps1 -Reports`. See [multiplayer flows](MULTIPLAYER.md). For an Intent Graph 2 interaction, distinguish graph preview from battle behavior. Some graphs read the scalar level directly and can disagree with a sparse mask. See [ascension audit](ASCENSION_AUDIT.md).

### If a feature is requested

Trace panel input through next-run preferences, lobby synchronization, run capture and save/load as applicable. UI lives in `EncounterSelectionPanel` and `AscensionPanel`; encounter resolution in `EncounterRuntime`; host authority in `MultiplayerSelection`. Keep Standard runs classified as Standard, preserve daily behavior and use frozen run settings on resume. Guests must keep their own next-run preferences. Extend the relevant fixture and run `dotnet run --project tests/SelectMOD.ContractTests -c Release`. For UI/API changes also build `tests/SelectMOD.GodotCompileChecks` in Release using the standalone default. See [validation history](VALIDATION.md).

### If the game updates

The reviewed baseline is Steam build 26660232. Compare the installed assemblies against [reviewed fingerprints](../compatibility/reviewed-builds.json). From the owner's Windows checkout, `.\scripts\Export-GameSources.ps1` produces `local/selectmod-sources.txt`. Review the affected patch targets, signatures, callers and initialization order against [the installed-game review](INSTALLED_GAME_REVIEW.md) before updating fingerprints or fixtures. Rebuild against the installed game with `.\scripts\Setup.ps1`, then exercise the affected new-run and resume paths. The owner handles gameplay acceptance and Steam publication.

## Historical notes through 0.11.1

The sections below record earlier releases, checks and publishing procedures. Use the current workflow above and the README when resuming.

## Previous handoff — 0.11.1

All-random solo starts skip encounter catalog inspection. A panel failure alone does not veto startup. Extra ascension levels are outside the ten-effect mask. Run restoration checks the frozen origin digest without comparing its historical catalog with current candidates. New multiplayer lobbies still compare catalogs. Missing vanilla Neow reward reads after another transpiler are logged.

## Previous handoff — 0.11.0

Owner reported being able to play and encountering trouble alongside Intent Graph. Exact installed versions/symptoms and a physical combined run remain unverified. The old owner-based rejection was reproduced. See ASCENSION_AUDIT.md for the upstream preview behavior and scalar graph limitation. Unselected room slots now retain their generated instances. Local encounter-choices.json stores stable Act-indexed next-run fixed choices on solo edits and successful host edits/reset. Incoming guest settings and save restoration do not modify preferences. Missing content falls back to random per slot.

## Previous handoff — 0.10.0

The encounter icon uses a gold crown and crossed swords. Its tooltip formats only non-null run-owned rules using localized model titles, in the same ancient / boss / second-boss order as the dropdowns. The local OptionButton theme follows the supplied language-settings reference. Normal Standard lobbies may omit a modifier list when joining or starting. The host attaches a marker from the authoritative session to the final start message. The receiver still validates the complete final snapshot. The three-player fixture exercises a normal host start with an empty native modifier list and a full-party save/load.

## Previous handoff — 0.9.0

Normal starts retain their native classification for fixed bosses and ancients in solo and host-controlled multiplayer. A nullable SecondBoss is omitted from old-format lobby JSON when unset so saved envelope hashes remain valid. Capture it only for effective A10 in Act III; override the existing native second slot and allow explicit same-encounter pairs. Never synthesize a double boss when A10 is skipped. Candidate groups derive from model IDs and native pools. Panel grip coordinates persist locally as normalized anchors. The custom-screen flame strip tracks the character button container or BaseLib scroll container. Verify click access at the owner's resolution and play through an identical-boss pair on hardware.

## Previous handoff — 0.8.0

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
