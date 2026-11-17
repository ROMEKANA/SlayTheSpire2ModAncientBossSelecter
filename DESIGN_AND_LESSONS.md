# SelectMOD: initial design and lessons

Updated: 2026-11-18 (Asia/Tokyo). Status: 0.1.0 solo settings probe implemented; independent checks pass; owner report confirms actual-reference build, installation and value42 create/restart/load hooks. Broader gameplay and multiplayer acceptance remain pending.

Read [AGENTS.md](AGENTS.md) and [STS2_MODDING.md](STS2_MODDING.md) before implementation. The owner's current status and eventual commands belong in the Japanese [README.md](README.md). Detailed external-source evidence lives in [TECHNICAL_RESEARCH.md](TECHNICAL_RESEARCH.md); its research results are inherited records, not checks rerun during this documentation task.

## 1. Goal and initial scope

Build an independent Slay the Spire 2 gameplay MOD, working title SelectMOD:

- Select, ban or restrict eligible ancient and boss encounters per Act.
- Enable ascension effects independently, including A1 OFF with A2 ON.
- Keep multiplayer as a delivery goal and architectural constraint, with host-owned configuration and matching effective rules on every participant. Validate gameplay in solo first; do not enable unverified multiplayer behavior.
- Restore the original run rules and already resolved encounters after save/load.
- Use the standard loader and game-bundled Harmony. No runtime dependency on BaseLib, RitsuLib, ModConfig, UCR or another MOD.

Proposed first entry point: standard custom runs, solo and multiplayer. Ordinary runs, daily runs, cross-Act placement, unlocking unavailable encounters, ancient reward selection, additional characters and host-only installation are outside the initial proposal. The requested ancient feature selects the appearing ancient, not its reward. Product boundaries can be revised deliberately after API investigation.

Require matching compatible SelectMOD/gameplay MOD installations on all peers. Plan for affects_gameplay=true; do not inherit the previous utility's false flag. Manifest identity, public name, payload format and supported game version remain undecided. Add a PCK only if actual assets require it.

## 2. Evidence and prerequisites

The completed reference project is UnifiedVanillaSave. Its 0.3.0 owner success report and v0.113.1 source review describe that project only. They establish neither SelectMOD acceptance nor the installed game's current version.

The existing technical research records version-pinned external source investigation. It favors standard ModifierModel/SavedProperty persistence and synchronization, identifies ascension and generation hook candidates, and documents where reference MODs use BaseLib. None of those signatures has been verified against a current game DLL in this task.

Before choosing patches, obtain a read-only source/metadata report identifying the real game build, DLL hash (and MVID when available), tool version, exact signatures, callers and initialization order. Cover new/load lobbies, Modifier serialization/clone, first ascension use, Act generation, map construction and room restoration. Use scripts/Export-GameSources.ps1 to collect static evidence; do not require manual hash approval flags or copy the old save utility's exporter targets.

## 3. State ownership

| Logical state (proposed names) | Lifetime and contract |
| --- | --- |
| LocalPreferences | Per-PC next-run defaults; never consulted by active gameplay or overwritten with host preferences |
| LobbyDraft | Host-edited rules with session identity and revision; edits invalidate readiness/acknowledgments |
| RunConfigSnapshot | Validated, immutable rules frozen before first effect/selection; persisted with the run |
| ResolvedActPlan | Actual Act identity, ancient, primary/secondary boss IDs and resolved flags; persisted once resolved |

These are logical responsibilities, not a requirement for four architectural layers. Prefer a small pure rules component plus a game adapter and thin UI. Do not use process-wide static configuration as the active run's authority. Distinguish identical seeds in different runs and clear references/subscriptions/waits on cleanup.

## 4. Rule semantics

For each Act position and actual Act identity, keep separate ancient/boss rules:

| Mode | Proposed behavior |
| --- | --- |
| Vanilla | Preserve the original eligible selection and RNG behavior |
| Fixed | Require one eligible full model ID |
| Restricted | Select from eligible candidates intersected with optional Allow, then subtract Ban |

Use full stable IDs, deduplicate, and sort with ordinal comparison. Distinguish unspecified Allow from an explicitly empty set. Unknown IDs, unavailable Fixed targets, Fixed/Ban conflicts and empty effective sets are validation errors; never silently relax a ban or choose different local fallbacks. Validate again at generation if eligibility is only knowable then. Initial support is for the standard game's eligible pool; compatibility with other content MODs needs separate evidence.

Proposed A10 policy: restrictions apply to both bosses. Fixed selects the primary boss; the secondary must be another eligible allowed boss. Reject insufficient candidates when a second boss is required. Verify the real A10 implementation before adopting this as final behavior. Do not delete a second boss created by an unrelated mechanism merely because A10 is OFF.

## 5. Synchronization and start boundary

Prefer one self-owned ModifierModel with registered SavedProperty values, carried by the standard custom-run lobby, start and save paths. Verify supported property types, type registration, cloning, field completeness and load timing before choosing the wire representation. Do not assume arbitrary lists or strings serialize correctly.

Host-only editing must be enforced in state updates, not just by disabled UI. Apply received snapshots atomically after validation; do not trigger a send loop when reflecting host changes into guest UI.

Required behavior: every participant has applied the same frozen revision before the first gameplay decision. Inspect whether the standard mechanism guarantees this across edits, joins and loads. If sufficient, reuse it. Otherwise add only the missing digest/ACK/start barrier; transfer the complete snapshot separately only if the standard path cannot carry it. An unconditional custom networking stack is not an initial requirement.

Logical synchronization contract: protocol/schema/algorithm versions, compatible build/MOD identity, session/run identity, revision, ascension mode/mask, complete Act rules, candidate digest and resolved-plan digest where relevant. Canonicalize all fields using specified UTF-8 encoding, ordinal ID ordering and bounded lengths/counts. Hashes detect differences; they do not authenticate the sender. Check host identity from the transport, not a self-declared payload field.

If a custom barrier is necessary, use session-scoped states such as Editing → Validating → WaitingForAcks → Frozen → Starting → Active. ACK must identify the exact session/revision/digest after application. Edits, membership changes, cancellation and timeout invalidate the wait. Start at most once after rechecking membership; use bounded retry and return a clear failure to the lobby. Never let the original start method run while an asynchronous validation is pending. Keep game operations on the required game thread.

Keep standard MOD/model mismatch and State Divergence/checksum checks enabled. BaseLib's CustomMessageWrapper/ICustomMessage are not standalone game APIs. If additional messages are needed, verify the actual game registration, serialization, sender identity and handler lifecycle first.

## 6. Encounter generation and RNG

Trace the complete generation chain. Rules must be frozen before use; final encounters must be resolved before the map, room or event consumers copy/display them. A GenerateRooms postfix is only a candidate until caller ordering is established. Map icon and actual encounter must reference the same resolved choice.

First investigate deterministic filtering of standard candidates on every peer. If replacing a completed standard selection is safer, use a versioned deterministic selector independent of game RNG, after identical standard generation and before any consumers. Choose one approach from real call-chain evidence; do not mix them opportunistically.

Never advance game RNG only on the host and send the result to guests. Match standard RNG progression on every peer, including fixed choices and candidate-count edge cases. Preserve vanilla behavior in Vanilla mode. Do not use string.GetHashCode(), dictionary iteration, time or locale as selection inputs. If an independent selector is used, specify its algorithm/version and stable inputs, including run seed, Act identity/position, purpose and rule digest.

Persist final IDs when resolved. Reuse them on load; do not reroll a saved result. Before first map interaction or encounter use, compare applicable rules, candidates and results across peers through a verified transport path. A local log displaying a hash does not constitute comparison or synchronization.

## 7. Independent ascension effects

Store an independent-mode flag separately from a known-effect bit mask so all OFF is distinct from standard cumulative ascension. Initial target is A1–A10, subject to current-version enumeration and effect audit.

For managers associated with a SelectMOD run, use the mask as the authoritative answer for known effects. An OR-only postfix cannot turn an already enabled cumulative effect OFF. The first proposal sets the custom-run cumulative level to zero and presents the independent mode explicitly. Unknown enum members and unrelated managers retain standard behavior.

Audit all HasLevel callers and direct numeric ascension comparisons. Ensure the snapshot is bound before initial deck, potion slots, health, rewards or generation first consult it. Reuse game effect logic rather than recreating balance formulas. Loading must restore existing state without adding initial curses/effects again. Record actual standard custom-run achievement/unlock/history behavior before making user-facing claims.

## 8. Persistence, failure and cleanup

Save snapshot and resolved plan through a verified run-associated path. Prefer standard persistence; do not make a host-local sidecar or an in-memory weak table the sole source of truth. On resume, saved run values override current preferences and lobby defaults.

Unknown schema, missing required state, invalid IDs or incompatible builds prevent resume until a defined common migration exists. Never rewrite real saves automatically to test a migration. Reconnection must restore and compare the saved state before actions. If the game cannot support this safely, explicitly refuse reconnect/late join for the first version rather than guessing. Host migration is unverified; stop on loss of authority until supported.

Required hook failure, conflicts affecting effective rules, snapshot mismatch or divergent generation must prevent the affected run from progressing with partial behavior. Surface the Act/revision and reason. Remove only this MOD's patches and handlers during rollback; do not disable other MODs or the game's diagnostics. Use bounded logs without raw saves or account identifiers.

## 9. Implementation order and acceptance

1. Audit the installed game contracts and lifecycle; record exact evidence and the supported version.
2. Build a minimal independent loader/Modifier proof: first solo start/save/fresh-process load, then host edit/guest apply/start/load with one minimal setting. Check networking feasibility before expanding feature implementation.
3. Add pure rule validation/canonicalization and the verified freeze/start mechanism. Test stale revisions, duplicate messages, cancellation, timeout and membership changes where applicable.
4. Add encounter rules and independent ascension with focused solo boundary and resume tests. Verify initialization timing, no repeated initial effects, A10 and RNG behavior. Keep the same immutable run contract; explicitly block multiplayer while synchronization is unverified.
5. Provide one Windows Setup entry point and a separate read-only diagnostics exporter. Build against actual game references; stage only declared MOD payloads and preserve the previous installation on failure.
6. Run separate-process multiplayer acceptance, including different local defaults, both host roles, more than one guest, Act transitions, save/load, reconnect policy, mismatched versions and consecutive same-seed runs.

Independent tests should cover empty/unknown candidate errors, canonical ordering/digests, independent all-OFF mode, OFF overriding cumulative effects, serialization and chosen synchronization state transitions. Use full 1024-mask coverage only if useful and cheap. Fixtures demonstrate the declared contract, not the actual game's implementation.

Real acceptance must compare actual ancient/boss IDs, map display, effective ascension, persisted state and available RNG/checksum diagnostics. Report compilation, loader initialization, solo acceptance and multiplayer acceptance separately. No such checks have run for SelectMOD yet.

## 10. Lessons carried from the previous MOD

| Previous observation | SelectMOD consequence |
| --- | --- |
| Patches loaded while the real save consumers bypassed them | Follow actual selection/effect consumers and overloads before choosing hooks |
| Correct-looking path logs did not prove actual shared progress | Matching UI/log output is not proof of guest application or matching encounters |
| Early profile selection happened before later observed path output | Bind the snapshot before the first effect, not merely before a convenient logged stage |
| Bundled Harmony and installed SDK differed from assumed APIs/tools | Verify exact bundled methods and runtime; pin compatible tools |
| Manual hashes/config edits and repeated commands burdened the owner | Prepare one setup command and one text-report export; preserve owner-selected paths |
| Owner-reported success had a bounded scope | Keep acceptance evidence precise and do not inherit old version approvals |

These are transferable lessons, not instructions to add save routing or the old utility's backup machinery to SelectMOD. Add future findings here as: assumption → observed evidence → decision → regression check. There are no SelectMOD-specific runtime failures or successful fixes to record yet.

## 11. Script extraction and staged implementation (2026-11-18)

The owner authorized removing the temporary reference project after extracting useful scripts. Root AGENTS/STS2 notes retain the applicable lessons. Source reference: https://github.com/ROMEKANA/SlayTheSpire2ModSaveManeged ; bundled snapshot remains in repository history at 3181983. Do not depend on the deleted directory.

- scripts/Common.ps1 retains the three-line paths contract and read-only game reference resolution.
- scripts/Build.ps1 retains reference checks, explicit project targeting, SDK selection and native exit-code handling. It deliberately rejects the missing SelectMOD project and performs no staging or installation. Old save audits, tests, fingerprints and payload identities were not carried over.
- scripts/Export-GameSources.ps1 adapts the pinned ILSpy workflow, before/after dependency hashes and previous-report preservation. It temporarily decompiles the assembly as a project without building/executing it, collects major target types plus bounded search contexts, and emits local/selectmod-sources.txt. It never reads saves or writes game/config directories. Missing required types, tool failure or changed dependencies preserve the last successful report and retain partial output. Text search, missing optional target names and caller caps must be inspected; this report does not prove exhaustive coverage.
- tests/Scripts.Tests.ps1 exercises script orchestration using temporary fake dependencies and tools. It is not game/API/decompiler acceptance.

Decision: solo-first feature validation with an early minimal multiplayer persistence/synchronization spike. This isolates gameplay defects without deferring the architectural uncertainty until after a full solo implementation. Multiplayer remains blocked in intermediate prototypes until verified. Actual game source review, feature builds and real-game acceptance are pending.

Validation in this task: PowerShell 7.4.13 parsed and ran fixture tests on Linux. Installed actual ILSpyCmd 9.1.0.7988 under .NET SDK 9.0.321 and ran Export-GameSources.ps1 against a newly compiled independent C# assembly containing four test target types; target extraction and caller report generation succeeded. These checks do not establish Windows execution or current-game compatibility.

## 12. Windows generated-source path failure (2026-11-18)

Owner evidence: ILSpy installation and project export reached source scanning, then Windows PowerShell File.ReadAllLines raised DirectoryNotFoundException for a generated regex source under a deep checkout/local/source-export-<GUID>/source path. The reported path exceeds the legacy Windows 260-character limit. This is evidence of an export-script portability defect, not a game/MOD failure; exact Windows/.NET versions and local filesystem inspection are not available here.

Correction: use an isolated short sm-<id> directory under the OS temp directory and explicit extended Windows paths for both ReadAllText and ReadAllLines on generated source. Preserve normal relative paths in reports, support UNC paths, and avoid changing machine policy or requiring the owner to relocate the checkout. Stage the final report beside its destination under local/ so atomic replacement still works when TEMP is on another volume. Preserve partial output on failure and point to it in the error report; a cleanup failure after successful publication warns without invalidating the finished report.

Regression: fixtures create a generated source with an absolute path longer than 260 characters and verify its content is included. Cover drive, UNC, already-extended and non-Windows path conversion, successful cleanup, existing-report preservation and existing failure checks. Linux/PowerShell verification is not a Windows acceptance result; request the same export command after updating.

## 13. Installed-source evidence received (2026-11-18)

The owner supplied a successful Windows PowerShell 5.1 static report. Review and exact evidence identity are in [docs/INSTALLED_GAME_REVIEW.md](docs/INSTALLED_GAME_REVIEW.md). This supersedes earlier statements of having no installed-version source evidence, while leaving real compilation/runtime acceptance pending. Target the observed net9.0 runtime; do not infer a player-facing game version from FileVersion 0.1.0.0.

Confirmed start messages serialize modifier Props, all Acts generate rooms up front, the outer generation loop adds A10's final second boss, public room access/setters exist, and standard saved room state retains all three selected encounter IDs. Reuse those mechanisms before adding custom configuration packets or duplicate encounter persistence. OnRunLoaded occurs after deprecated-room validation, so it is not an early validation gate. OnRunCreated precedes the observed initial ascension application but does not establish timing for every possible caller.

Collector v1 missed supporting SavedProperties/packet/loader bodies and truncated search contexts. Collector v2 adds the inventory-confirmed types and raises the default cap with an explicit warning. Follow up on those contracts before implementing custom serialization or asserting multiplayer safety. The raw report remains outside Git; this evidence does not approve a game build or prove SelectMOD features.

## 14. Supporting contracts confirmed in report v2 (2026-11-18)

The second report includes all 1586 text matches and every requested target. See the v2 follow-up in [docs/INSTALLED_GAME_REVIEW.md](docs/INSTALLED_GAME_REVIEW.md) for exact evidence identity and source sections. Broad collection is sufficient for the initial prototype; do not ask the owner for another broad export at this stage.

Use registered int/bool/string properties rather than arbitrary rule collections. Keep a required schema sentinel distinct from valid all-OFF independent mode, because SavedProperties can omit nulls, ignore unknown properties and leave missing values at defaults. Serialize Act rules as a bounded canonical string when those rules are added, with complete validation after restoration. Strings and integer arrays have observed packet readers/writers; their actual values and full custom-model round trips remain to be tested.

The loader appends a MOD's assembly and marks it loaded after calling its initializer. ReflectionHelper caches loaded MOD types, so avoid eager model/type/serialization-cache discovery there. Add hooks in the initializer and confirm own model/property membership at the normal later initialization stage. Standard property IDs and gameplay hash checks stay enabled.

SerializableRun and the load-join response carry both modifiers and saved rooms. Reuse that transport first. NetMessageBus handler registration does not establish a custom numeric wire ID, and its exception catch means throwing inside a handler is not a sufficient start/stop gate. Resolve custom verification transport only if the minimal standard-path experiment shows it is necessary.

Next concrete implementation scope: one settings Modifier, supported scalar fields, solo clone/start/save/reload checks, and a minimal host/guest setting comparison. Encounter manipulation and ten-effect gameplay acceptance follow that proof. No live runtime result is claimed yet.

## 15. Solo settings probe while multiplayer acceptance is deferred

The owner cannot run host/guest acceptance until the weekend and asked to progress other work. Implemented 0.1.0 as a deliberately small persistence probe: a standard custom-run choice stores schema/value scalar SavedProperties, logs creation/load, and changes no ascension or encounter behavior. Per-run properties are the authority; current local preferences are read only when constructing a new choice. Invalid sentinels, duplicate models, unsupported mode and invalid values are rejected.

The standard choice list is extended after normal model discovery. Literal Japanese labels apply only to LocString instances owned by this MOD, through a weak mapping and narrowly scoped formatted-text prefix; no localization tables or asset packs are modified. This display adapter needs real-game acceptance alongside model discovery. Initialization installs hooks without eagerly querying ModelDb. Exact reviewed dependency hashes gate installation and runtime initialization; hashes identify the reviewed source, not a passed runtime matrix.

All multiplayer starts/loads are blocked while this probe is installed: lobby guards display a message and clear ready, and setup guards reject received start/load paths before changing RunManager state. Invalid saved settings are checked before the observed RunState restoration route. Do not enable multiplayer by treating fixture success as network acceptance. The eventual synchronization experiment is still pending, and this release sends no custom messages.

Setup.ps1 validates reviewed references, runs independent tests, builds the actual production project, stages only the declared three files, preserves custom config paths/preferences and installs with rollback. Existing releases/config backups remain outside the loader tree. A generated installed SelectMOD.paths.txt locates custom runtime config without committing personal paths. Export-ProbeReport.ps1 collects bounded setup/build/runtime evidence; Disable.ps1 moves the installation outside mods without modifying saves. No save-routing or save migration is added. A probe run contains a custom modifier and requires this MOD to resume.

Verification: production source linked against minimal independently written contract types and test-only Harmony, plus temporary PowerShell installer scenarios. These are explicitly not actual-game compilation, game SavedProperties round trips, Windows runtime acceptance or multiplayer tests. The owner next runs the prepared setup and solo create/save/restart/load experiment. Initial value42, change next-run preference to73 while closed, confirm old run42/new choice73. Gameplay effects and encounter selectors follow that proof rather than shipping an untested larger patch set.

## 16. Owner-side build and restart/load evidence (2026-11-18)

The owner supplied a 0.1.0 probe report with a successful real-reference build/install, two INITIALIZED sessions, CHOICE_READY and RUN_CREATED value42, then RUN_LOADED value42 after restart. See [docs/VALIDATION.md](docs/VALIDATION.md) for source identity and exact limits. This establishes a narrower success than a full gameplay or network matrix. Preserve the working setup/model path instead of redesigning it.

The report's Japanese dotnet output was corrupted while ASCII stage markers and runtime fields were intact. Scope UTF-8 console/pipeline encodings around the native SDK commands, restore prior encodings in finally, and keep nonzero exit handling in the same action. Regression uses a real native UTF-8 emitter plus success/failure restoration checks. No gameplay code or save schema changes are needed for this correction. Next solo acceptance is old saved value42 versus next-run preference73; real multiplayer remains deferred to the weekend.
