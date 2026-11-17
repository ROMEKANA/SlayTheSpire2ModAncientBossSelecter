# Validation evidence

## 0.1.0 owner probe report, reviewed 2026-11-18 (Asia/Tokyo)

Source: owner-uploaded selectmod-probe.txt. Raw SHA-256: `7E7A11AC0440B1D04205D10868CF8881FBB12C407B21E67810795BEDF740C43D`. The private raw report contains local paths and is not committed. The agent did not execute the game.

| Stage | Supplied evidence | Bounded conclusion |
| --- | --- | --- |
| Reference gate | REFERENCES_MATCH_REVIEWED_SOURCE | The setup's reviewed-reference check passed |
| Independent tests | CONTRACT_TESTS_PASSED and 26 contract/Harmony checks | Owner-side independent tests passed; these remain fixtures |
| Production build | ACTUAL_REFERENCE_BUILD_PASSED and compiler output showing zero warnings/errors | Production source compiled against the owner's game references |
| Installation | INSTALLED version=0.1.0 | Setup completed its install path; broader installer/platform combinations are separate |
| First game session | INITIALIZED, CHOICE_READY schema=1 value=42, RUN_CREATED schema=1 value=42 | Game initialization, choice construction and the selected run's creation hook executed |
| Restart/load session | A second INITIALIZED followed by RUN_LOADED schema=1 value=42 | The run's schema/value were restored in the new session. The missing-field sentinel did not replace the saved value |

Runtime timestamps span 2026-11-17 19:11–19:12 UTC (2026-11-18 04:11–04:12 JST). They show two initialization sequences and consistent value42. There is no explicit owner statement or full-game trace proving play after that hook, successful Japanese labels, completion of a run, or every save scenario. Do not turn these observations into a complete solo acceptance matrix.

Still pending: change next-run preference to73 while closed and verify old saved run42/new choice73; full gameplay effects and encounter selection; actual packet/host-guest comparison; real multiplayer acceptance. Multiplayer remains disabled in this probe, as requested while weekend testing is unavailable.

The owner report also revealed mojibake in Japanese dotnet build/test output. The C# compiler's production result and our ASCII stage markers are still readable, and this was not a production build failure. Build and contract-test commands now run with scoped UTF-8 console/pipeline encodings, restoring the prior settings on success/failure. Keep the accepted setup workflow. Do not try to repair the already-corrupted old text or require reinstall solely for this log fix.

Regression verification for the script fix: a native child process emits Japanese UTF-8 text while the parent initially uses ASCII; assert correct decoded text and captured file, then assert encoding restoration after success and an injected exception. Existing setup tests also check native-command failure propagation and installation preservation. These are agent-run PowerShell checks, not owner-side verification of the corrected Japanese logs.

## 0.1.0 next-run preference separation, reviewed 2026-11-18 (Asia/Tokyo)

Source: second owner-uploaded selectmod-probe.txt. Raw SHA-256: `5ECDC2C385CCC0BE23EBA7C4FCD280515F30458CAA670BD01369F34752308B70`. This report repeats successful reviewed-reference checks, 26 independent tests, the production build (zero warnings/errors) and installation. New runtime evidence is:

| UTC timestamp | Event | Conclusion |
| --- | --- | --- |
| 2026-11-17 19:21:17 | RUN_LOADED schema=1 value=42 | The saved run retains its original value after the preference update |
| 2026-11-17 19:21:33 | CHOICE_READY schema=1 value=73 | The new choice reads the changed next-run preference |
| 2026-11-17 19:21:38 | RUN_CREATED schema=1 value=73 | A newly created run captures the changed value |

These events are 2026-11-18 04:21 JST. They complete the planned 42/73 separation check at the recorded hooks. No further repetition of that check is required to start solo feature implementation. This does not establish a reload of the new73 run, a full-game completion, visual UI acceptance or network synchronization.

Japanese compiler output is readable in this second report, including the successful production build and independent-fixture warnings. This provides owner-side evidence for the scoped native UTF-8 capture correction. The warnings belong to the independent contract fixture; the production build reports zero warnings and zero errors.

The earlier pending preference test and unverified corrected-log statements describe the first report and are superseded by this follow-up. Real multiplayer acceptance remains deferred until the weekend; the prototype's multiplayer gates stay enabled. Ascension and encounter gameplay effects remain unimplemented and untested. Next work is the independent ascension feature in solo, preserving the now-observed run-owned settings and the accepted setup workflow. Raw reports remain outside Git.

## 0.2.0 automated scope (2026-11-18)

Added independent ascension code and fixtures for all 1024 masks through manager and run-helper query paths, including a cumulative backing value that would otherwise turn OFF bits back ON. Checks also cover early helper use, A4/A5 initial decisions, unknown enum preservation, unrelated managers, A7 cache restoration, A10 generation, saved-mask priority, no repeated initial effects, frozen properties, missing fields, duplicates, foreign patch owners and retaining an active binding after a rejected second setup, asynchronous-load cleanup and late-failure isolation. PowerShell fixtures validate new settings migration, explicit empty ON lists, duplicate ON numbers and preference retention alongside previous failure/rollback coverage.

Production source is linked into independent contract tests with test-only Harmony. New effect/game contract fixtures are not game source and do not certify actual serialization, enemy stats, probability distribution or runtime behavior. The new version's actual-reference build, owner-side A1–A10 effect matrix, old probe-save regression and multiplayer acceptance are pending. Existing 0.1.0 owner results above remain scoped to that version/path.

Agent-run results for 0.2.0: .NET SDK 9.0.321 contract/Harmony runner passed 28723 assertions, including all 1024 masks; PowerShell 7.4.13 Scripts.Tests.ps1 and Setup.Tests.ps1 passed on Linux. Real game references are absent from this Cloud environment, so a production game-referenced build was not claimed or substituted with fixtures.


## 0.2.0 owner startup freeze and 0.3.0 replacement

Owner-uploaded selectmod-probe.txt SHA-256: `7548E28B265D96591823FCCC782906CB6811722A213A31C22348245DF5C22C1C`. It records reviewed references, 28723 passing independent fixture assertions, production compilation with zero warnings/errors and installation of 0.2.0. Runtime reaches INITIALIZED, CHOICE_READY and ASCENSION_CHOICE mask=10 at 2026-11-17 20:21 UTC (2026-11-18 05:21 JST). No ASCENSION_BOUND/CREATED, exception or engine trace is present. The owner reports a freeze after starting. Root cause is not established; passing fixtures did not establish usable runtime behavior.

0.3.0 prioritizes solo fixed/default encounter selection, removes the active independent-ascension implementation and adds a normal-run pre-start dialog. Retired ascension saves are rejected rather than converted; the earlier probe model remains readable. Technical decisions and verified source signatures are in DESIGN_AND_LESSONS.md section 19.

Agent-run verification on Linux: .NET SDK 9.0.321 production-linked independent contract/Harmony runner (36 checks), public GodotSharp 4.5.1 compile-only project (zero warnings/errors), and PowerShell 7.4.13 Scripts.Tests.ps1 / Setup.Tests.ps1. Scenarios cover async selection, duplicate start, external scene teardown/cancellation and retry, fixed encounters, unchanged/disabled UI, frozen rules/results, saved-ID comparison before restore, no reload reroll, standard RNG and cumulative ascension preservation, A10 collision, retired saves, multiplayer blocking, config retention and installation rollback. Diagnostic fixtures verify the bounded engine-log tail and exclusion of save contents. Windows Explorer selection code is included, but Explorer itself was not executed on this Linux host.

The fixture serializer is independent JSON, not the game's SavedProperties serializer. The public Godot assembly check still uses independent game contracts and is not an actual-game build. Production still references only installed game assemblies; test packages are not installed into mods. No real 0.3.0 DLL build, graphical popup, gameplay progression, actual save/restart/load, map/encounter comparison or multiplayer run was performed here. Next owner acceptance is NEW normal solo start with fixed boss/ancient, actual encounter, save/restart/load, then unchanged start. A10 and Act transitions need explicit runtime acceptance too.
