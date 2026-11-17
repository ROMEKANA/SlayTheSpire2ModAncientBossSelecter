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
