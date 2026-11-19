# Validation evidence

## 0.10.0 encounter presentation and normal multiplayer

226 contract checks/groups passed, including exact localized fixed-choice tooltip lines and normal three-player start, guest agreement, save and resume. Both public Godot API builds (standalone and BaseLib) completed without warnings/errors, all five PowerShell fixture suites passed and headless Godot decoded the encounter icon at 64×64. Live Steam transport and the rendered game menu remain unverified.

## 0.9.0 encounter selection

225 independent checks/groups passed. New coverage includes identical Act III boss IDs surviving ordinary solo save/load, delivery to all simulated peers, ignoring a dormant second-boss choice with A10 off, group labels and ordering, panel drag and restored anchors, and the flame strip below the custom-screen character container. Both Godot API flavors compiled with zero warnings/errors; all five PowerShell fixture suites passed. The installed game, actual two-fight encounter completion, UI scaling and Steam multiplayer remain real-device checks.

## 0.8.0 display and distribution

The owner verified solo A4 disabled and A5 enabled under the previous UI. This is gameplay evidence for that one combination. For 0.8.0, 213 independent contract checks/groups passed, including ten reserved slots, editing below the current level, rejecting above-level clicks, host-to-guest flame updates and guest input rejection. Both public Godot API builds completed without warnings/errors, and the five PowerShell fixture suites passed. Headless Godot 4.6.3 rasterized the embedded blue flame to a nonempty 64×64 image. The top bar getter, screen fit and real multiplayer still need game acceptance. The native portrait tooltip displays cumulative levels until a verified tooltip builder is available.

## 0.7.4 follow-up

202 contract checks/groups passed, including corrupted saved masks, a delayed load failure after the next run starts, and two receiver processes. All 1,023 filtered A10 masks passed both effect query routes and A7 cache cleanup across consecutive fixture runs. The standalone and BaseLib compile checks use public GodotSharp 4.5.1 and independent game contracts; both compiled with zero warnings/errors. The optional build references the real BaseLib 3.4.8 package. Installation fixtures exercise a standalone → BaseLib → standalone round trip. Runtime testing on the reviewed Steam installation remains pending.

## 0.7.2 development checks (2026-11-19)

.NET SDK 9.0.203: 196 independent contract assertions/groups passed, including all 11,264 preference/level combinations, the ordinary screen signal, saved mask isolation, A10 suppression, daily bypass, two simulated ordinary network endpoints and existing custom encounter coverage. The existing two independent receiver processes also passed. GodotSharp 4.5.1 compile check completed with zero warnings/errors. All five PowerShell fixture suites passed under Linux PowerShell 7.4.13. Production game DLLs, real UI, Steam transport, unlocks and achievements were not exercised. The owner confirmed Workshop upload of 0.6.0; no 0.7.2 release was uploaded.


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


## 0.3.0 owner report and 0.4.0 selector revision

Owner-uploaded selectmod-probe.txt SHA-256: `0E992B9C0D8EA9647309CFBEC4732243AB716790A71483DF9A7B89D3F1F05025`. Actual-reference build/install succeeded with zero compiler warnings/errors. Between 2026-11-17 21:04–21:09 UTC (2026-11-18 06:04–06:09 JST), seven starts reached START_COMPLETED. All seven captured changed=False; none has encounter preparation/resolution markers. The owner reports an Act1 boss did not match the choice and asks for a larger character-screen UI with stable full per-Act choices. This confirms runtime starts without confirming any override or save/load of selected results. Raw engine logs/private paths/account IDs are not committed. Engine warnings and other mod names do not establish causation.

0.4.0 agent verification: .NET SDK 9.0.321 linked production code with independent game/Godot contracts and test-only Harmony: 42 checks passed. Public GodotSharp 4.5.1 compile check: zero warnings/errors. PowerShell 7.4.13 Setup.Tests.ps1 and Scripts.Tests.ps1 passed on Linux, retaining the accepted installation and Explorer-export behavior. The new test drives user ItemSelected signals through the attached panel to run setup/generation with an alternate-map boss and locked ancient; checks stable complete pools/order, cached-screen refresh and next-run/run-state isolation. Previous relevant save/ascension/RNG/unsupported-mode checks remain. Old modal cancellation tests were replaced because the modal implementation was removed.

Pending: actual 0.4.0 game-reference compilation, readable/layout-correct upper-right panel, option clicks, captured changed=True and matching actual boss/ancient/map IDs, unlocked/locked/alternate-map safety, real save/restart/load and compatibility with the owner's other mods. Cloud does not contain the game. No graphical or gameplay acceptance is claimed. Owner should choose Act1 boss on the character screen, observe the specified-setting status, start normally, and export the prepared report after checking encounters/reload.


## 0.4.0 compile failure and 0.4.1 correction

Owner console evidence: 42 fixture checks passed, then the actual game-referenced compiler raised one CS1061 at EncounterSelectionPanel.cs:89 (T has no Title); zero warnings. Setup reported no installation performed. No 0.4.0 UI or gameplay success is established.

Agent reproduced the same CS1061 by removing the incorrectly declared Title from the independent AbstractModel fixture. The correction passes a concrete-type label selector to the generic UI builder, preserving ID-based choices. Agent verification for 0.4.1: 42 independent production-linked contract/Harmony checks pass, public GodotSharp 4.5.1 compile check passes with zero warnings/errors, and PowerShell Scripts.Tests.ps1 / Setup.Tests.ps1 pass on Linux. Actual game references remain unavailable here; the owner must rerun the established Setup entry point to establish production compilation, then test the panel and chosen encounters.


## 0.4.1 owner acceptance and 0.4.2 folding

Owner-uploaded selectmod-probe.txt SHA-256: `28E106884E2A84CFADDE7DD495E8A97591A006B740D6575C365D8D8D1367F558`. Actual-reference build/install succeeded with zero warnings/errors. On 2026-11-17 21:43 UTC (2026-11-18 06:43 JST), SELECTION_CAPTURED changed=True bound Act1 UNDERDOCKS to THE_KIN_BOSS and NEOW, ENCOUNTER_RESOLVED confirmed those IDs, and START_COMPLETED followed. The owner says it worked and identifies the panel/start-button overlap. This supersedes the pending 0.4.1 build and basic UI/selection status above. No save/restart/load, full Act/candidate/A10 acceptance or multiplayer evidence is claimed.

0.4.2 agent verification: 47 independent contract/Harmony checks pass, including initial collapse, expanded/collapsed root-height changes, draft preservation, reopening and selected-run start while collapsed. The public GodotSharp 4.5.1 compile check succeeds with zero warnings/errors. Setup.Tests.ps1 also passes on Linux. These fixtures do not simulate Godot layout/hit testing; the owner still verifies actual start-button access and repeated open/close. Gameplay classification and save schema remain unchanged.


## 0.4.2 Neow failure and 0.5.0 experimental fix

Owner report SHA-256: `8CB05C4910833704D33A346397B0758C3EB106731B3081A4A80B0D821B7A185F`. It records actual-reference build/install and three selected starts on 2026-11-18 08:05–08:07 UTC (17:05–17:07 JST), resolving Act1 WATERFALL_GIANT_BOSS or THE_KIN_BOSS, Act2 THE_INSATIABLE_BOSS, Act3 QUEEN_BOSS and NEOW/DARV ancient IDs. The owner confirms boss selection but reports missing Neow behavior. One start leaves Act1 ancient random and still resolves NEOW, consistent with a modifier-branch issue rather than fixed-Neow-only replacement. No complete encounter/save/load matrix is inferred. Unrelated engine task/exit warnings do not identify the cause.

0.5.0 independent verification: 58 contract/Harmony checks pass, including a failing-before-patch Neow count-branch witness with an interface-typed run receiver, standard reward route with own storage, foreign reward preservation, no mutation of persisted modifiers, and Act3-only boss/ancient applied in Act1 without extra RNG. Public GodotSharp 4.5.1 compile check passes with zero warnings/errors. Scripts.Tests.ps1 and Setup.Tests.ps1 pass on Linux. These are not actual game compilation or reward/encounter acceptance. The Neow witness uses independent option markers; real EventOptions/actions and other mod patch interactions remain unverified. Request a new solo start checking Neow rewards first, then one cross-Act target at a time and real save/restart/load.


## 0.5.0 usage report and 0.6.0 multiplayer beta

The owner states 0.5.0 worked (2026-11-18 JST), then requests autonomous multiplayer implementation. No new raw log accompanies that statement. Record solo usage acceptance; do not infer every Neow reward, cross-Act combination, Act transition, save scenario or multiplayer success.

Agent verification for 0.6.0: .NET SDK 9.0.321 runner passes 182 independent production-linked contract/Harmony assertions. It exercises 2/3/4-peer host/client fixtures, read-only guest UI, local-default isolation, lost previews, unauthorized/stale/conflicting/duplicate packets, published payload immutability, invalid schemas/catalogs/digests, ascension refresh/clamp/reconfirmation, whole-party saved resume, corrupted saved rules/results/ascension, closed-session cleanup and late-async-failure ownership. Two distinct .NET receiver processes reconstruct a fixture catalog, consume the host envelope without previews and agree with the fixture host on resolved IDs, ordered start context and RNG draw count. This is process isolation with independent JSON/game contracts, not real SavedProperties/network packets or a game executable.

Public GodotSharp 4.5.1 compile check: zero warnings/errors with independent game contracts. PowerShell 7.4.13 Scripts.Tests.ps1, Setup.Tests.ps1 and MultiplayerReports.Tests.ps1 pass on Linux. The report comparer rejects mismatch, missing roles/phase/completion and newer incomplete attempts, preserving the previous comparison. Test-only assemblies and game references remain outside the three-file installation payload. No real save data is read/copied/converted by these checks.

Pending: actual 0.6.0 bundled-reference build, native model/property discovery and packet round trips, host/client UI composition with other mods, real cumulative ascension/Neow/map/encounter effects, 2–4-player progression, host-role swaps, save/quit/restart/resume and native rejection/timeout behavior. Setup and the read-only exporter are ready; every peer must install the same build and use Standard custom mode. Live rejoin/host migration remain unsupported. docs/MULTIPLAYER.md contains the concrete acceptance matrix and distinguishes data validation from an ACK barrier or full gameplay guarantee.


## Workshop preparation of 0.6.0 (2026-11-19 JST)

Agent-run: 185 production-linked independent contract/Harmony assertions (including three subscriber-default checks) and two separate .NET receiver processes; public Godot compile check; PowerShell parser/installer/export/report suites; tests/Workshop.Tests.ps1 temporary fixture orchestration. The latter checks portable allowlisting, no config/save writes, Japanese metadata, build failure preservation, partial native uploader failure, item-ID duplication guard/recovery, ID backup restoration and unknown-reference rejection. Fake payload and uploader files are temporary test fixtures, not production game references or Steam sessions.

Pending: actual game-reference production build, official Windows uploader execution, Workshop create/update/visibility/legal-agreement acceptance, subscriber download and real loader initialization, existing manual-copy removal, subscriber fresh-config UI, actual multiplayer acceptance. A prepared script or upstream zero exit code does not certify these stages.


## Workshop native stderr correction

Owner console output confirms actual-reference 0.6.0 build succeeded with zero warnings/errors and workspace preparation completed. No installation, upload success or gameplay acceptance follows from that. The native uploader then emitted the breakpad AppID initialization line and Windows PowerShell stopped with NativeCommandError.

Agent-run regression: tests/NativeProcess.Tests.ps1 executes real child processes with the same stderr line and exit0, then a nonzero exit17; captures UTF-8, quoted/empty/trailing-slash arguments and both pipes exceeding normal buffer capacity. tests/Workshop.Tests.ps1 remains a separate orchestration double for publication identity/pending recovery and failure propagation. tests/Scripts.Tests.ps1 validates syntax and existing script behavior. Pending: corrected Windows PowerShell 5.1 run, Steam submission/visibility and subscriber/gameplay acceptance.
