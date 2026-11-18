# SelectMOD multiplayer beta (0.6.0)

## Delivery boundary

Implemented for Standard **custom** multiplayer lobbies with 2–4 players, matching SelectMOD/game/content-mod installations. Solo remains supported. There is no new socket, custom numeric message registration, BaseLib dependency or global mod-state override. Cumulative ascension is preserved; independent ascension remains disabled.

Live rejoin and host migration are outside this beta. A selected active run rejects GetRejoinMessage; finish/quit together and reopen the saved multiplayer run with the original party. Resume requires all saved participants present. Selected replay remains blocked. Ordinary multiplayer lobbies, daily/Blitz/Dread, larger parties and arbitrary content-mod combinations are not certified.

Actual game references and Steam/ENet instances are unavailable in Cloud. This is an implementation and independent validation result, **not** real multiplayer acceptance. The owner's 0.5.0 success report is solo evidence only.

## Reviewed paths

Installed-source evidence is the owner report v2 recorded in [INSTALLED_GAME_REVIEW.md](INSTALLED_GAME_REVIEW.md). Relevant source contracts:

- `StartRunLobby.SetModifiers(IReadOnlyCollection<ModifierModel>)` replaces the list, sends `LobbyModifiersChangedMessage`, and notifies the listener.
- The initial `ClientLobbyJoinResponseMessage` contains the modifier list, including SavedProperty values. `InitializeFromMessage` restores it.
- `BeginRunForAllPlayers(string, List<ModifierModel>)` serializes the complete final list into `LobbyBeginRunMessage`; seed, full player records and Act1 override accompany it. The client restores that full list before `BeginRunLocally`. It does not rely on its cached preview to choose run modifiers.
- `NGame.StartNewMultiplayerRun(List<Player>, StartRunLobby, bool, IReadOnlyList<ActModel>, IReadOnlyList<ModifierModel>, string, int, DateTimeOffset?)` creates mutable Acts, calls RunState.CreateForNewRun then RunManager.SetUpNewMultiplayer.
- Multiplayer setup calls the same InitializeNewRun / GenerateRooms boundary as solo, including standard RNG consumption and the final A10 second boss.
- `LoadRunLobby.Run` and the load-join response carry SerializableRun, including modifiers and all resolved room IDs. SetUpSavedMultiplayer does not generate rooms again.
- Source-confirmed screen names and AfterInitialized hooks cover cached/new custom and character screens. The custom screen exposes Lobby; the character screen's reviewed `_lobby` field is used when no public property exists.
- Native NetClientGameService exposes HostNetId. Receive guards obtain that transport-owned identity; a payload field is not treated as authentication.

Full screen/player/run-lobby targets were added to the static exporter for later investigation. No new broad owner export is required unless integration fails.

## State and transport

| Component | Responsibility |
| --- | --- |
| EncounterDraft | Process-local solo preferences, never overwritten by guest traffic |
| MultiplayerSelection session | Weakly keyed to one lobby, host draft/revision, received immutable snapshot, failure/start/closed state |
| SelectLobbySettings | Transient standard ModifierModel with one string SavedProperty carrying a bounded canonical envelope |
| SelectEncounterSettings | Actual bound run rules, resolved results, saved network origin and participant count |
| EncounterRuntime | Standard generation followed by deterministic fixed overrides; unchanged game RNG; persisted final IDs |

The envelope contains protocol, random lobby session ID, monotonic revision, candidate/algorithm/version digest, cumulative ascension, indexed full boss/ancient IDs, and SHA-256 of the canonical body. Hashes detect differences; they do not authenticate a sender. The standard host transport identity and native mod/version checks remain authoritative.

All choices, including all-random, have a complete lobby envelope. Each selected Act is a position until native seed/Act selection finishes. At NGame's entry, every peer binds the packet's choices to the **actual** Act IDs and converts the transient model to a run model only if an override exists. Guests never consult local next-run values or local dialog enablement. All-random removes the transient model without adding gameplay rules. Neow's accepted storage-only filter still sees the final run model, not the transient transport model.

Candidate digests sort full IDs with ordinal comparison and include available Act identities/pools and the SelectMOD algorithm/version. Schema/sentinel/canonical-form/length/count/candidate checks run before native run creation. Changed/unknown targets are errors, never local fallbacks. The saved origin must match bound rules, player count and cumulative ascension before restoration; resolved saved room IDs must match before the game can apply deprecated-model fallbacks or increment reload counters.

## Authority, sequencing and failure

Only the host can edit. Native SetModifiers calls preserve the hidden SelectMOD envelope across ordinary modifier checkbox changes; updates allocate a new frozen transport model. Every edit clears the host's ready flag. Ascension changes refresh the snapshot too. A native join-time ascension clamp discovered at start causes a snapshot update and requires the host to ready again.

Clients validate host-only previews, ignore older revisions, reject same-revision conflicts and wrong sessions, and display received values without changing solo defaults. Unauthorized preview/start senders are ignored. Receive validation deserializes only SelectMOD-owned models; unrelated modifier constructors are left to the native handler. Client errors explicitly clear readiness instead of relying on a thrown exception escaping NetMessageBus's catch.

The authoritative final start packet may be newer than the last preview. It is complete, validated, frozen once and applied before run creation. Duplicate start requests are ignored; edits after freezing and delayed packets after lobby cleanup cannot resurrect a session. The native final player list and seed supply roster/order/Act selection. Final cumulative ascension comes from the frozen envelope even if a client's display was stale.

There is **no custom ACK barrier**: the reviewed native start packet carries the full snapshot, and each receiver validates/binds it before its first gameplay effect. Guest readiness is not represented as a per-revision SelectMOD ACK. A client that rejects a packet does not start; stopping other peers after a broadcast follows native disconnect/error/timeout behavior, not a new atomic cross-process rollback. Real packet sequencing, timeout and third-party hook combinations still require acceptance. Standard model/version mismatch and State Divergence checks have not been disabled.

Generation/load failures release only their own pending association. Async load tracking uses ownership tokens so an old failure cannot erase a newer run. Closed lobbies have weak tombstones to prevent late UI/network callbacks from editing them. Selected live rejoin is explicitly rejected rather than restoring incomplete active state.

## Diagnostics

- MP_LOBBY / MP_START_FROZEN: role, revision and canonical snapshot digest.
- MP_CONTRACT_CAPTURED: snapshot digest, Host/Client and start/resume phase.
- MP_START_CONTEXT: new-start seed/ascension/ordered roster/actual Act digest; for resume, start timestamp/Act index/party size/ascension context. These are limited metadata, not a full save hash.
- ROOM_PLAN_DIGEST: actual resolved boss/second boss/ancient plan.
- MP_START_COMPLETED / MP_RESUME_COMPLETED: native task completion and matching snapshot digest.
- MP_START_ATTEMPT / MP_RESUME_ATTEMPT: a later failed/incomplete attempt must not accidentally reuse older successful records.

Export one selectmod-probe.txt per peer. Compare-MultiplayerReports.ps1 checks latest fixed-selection records, phase, roles, snapshot/context/plan and completion markers. It preserves a previous comparison and selects the output in Explorer. MATCH is recorded-value agreement, **not** proof of packet reliability, full state equality, future combat determinism or gameplay completion. All-random has no saved override plan and is intentionally insufficient for this comparison. Synthetic log tests cover mismatch, absent markers, incomplete newer attempts, phase confusion and backups.

## Independent checks and remaining acceptance

182 assertions link production patches to independently written game/Godot contracts with test-only Harmony. They include 2/3/4-peer sessions, host-only editing, read-only guest UI, changed local defaults, lost previews, stale/spoofed/duplicate messages, frozen payloads, invalid schemas/digests/catalogs, native ascension clamps, cumulative A10/RNG behavior, actual fixed results, complete-party resume, saved tampering, no reroll, cleanup and delayed-failure isolation. Two additional .NET peer processes rebuild an independent fixture catalog, deserialize a JSON envelope without previews and agree with the fixture host on snapshot/plan/context/RNG count. These child processes do not use game packets, real SavedProperties, network sockets or game executables.

Public GodotSharp 4.5.1 compilation checks UI API calls with independent game types. PowerShell installer/export/comparison checks run on synthetic files/tools. Actual bundled-game compilation, Steam/ENet and graphical acceptance remain separate.

| Real-device case | What must be checked |
| --- | --- |
| Two peers, ordinary fixed Act1 target | Host edit, read-only guest display, both map/encounter IDs, native task completion |
| Different local defaults / guest panel disabled | Host snapshot remains effective; guest's subsequent solo preferences unchanged |
| Change standard modifiers / ascension / local character | SelectMOD snapshot retained; host must ready after edits; same effective A level |
| Join/leave before start | Latest whole snapshot/roster; no stale start or session reuse |
| Three and four players | Every peer's snapshot/context/plan agrees and gameplay progresses |
| A10 and cross-Act targets | Distinct second boss, actual encounter/reward progression; no divergence |
| Neow | Normal rewards remain available alongside own storage settings; foreign reward modifiers preserved |
| Save/quit/restart with all original participants | Saved choices/results win over new preferences; no reroll or repeated initial effects |
| Swap host role on resume/new run | Native ownership and saved identity handling; identical settings/results |
| Missing/mismatched game/MOD/candidates | Native mismatch or explicit rejection; no divergent continuation |
| Disconnect during active run | Live rejoin refusal is clear; full-party saved resume works |
| Consecutive runs, including same seed | New lobby identity, no old revision/cleanup/async failure affecting the next run |

Keep game/SelectMOD/content-mod versions and candidate-affecting configuration unchanged for a saved beta campaign. The origin/catalog validation is deliberately strict; incompatible future revisions need a defined migration. Solo 0.5.0 model identities/schema remain readable. No real saves are converted or copied by this work.
