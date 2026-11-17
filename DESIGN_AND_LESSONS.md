# SelectMOD: initial design and lessons

Updated: 2026-11-18 (Asia/Tokyo). Status: initial proposal; no SelectMOD implementation, build or game acceptance yet.

Read [AGENTS.md](AGENTS.md) and [STS2_MODDING.md](STS2_MODDING.md) before implementation. The owner's current status and eventual commands belong in the Japanese [README.md](README.md). Detailed external-source evidence lives in [TECHNICAL_RESEARCH.md](TECHNICAL_RESEARCH.md); its research results are inherited records, not checks rerun during this documentation task.

## 1. Goal and initial scope

Build an independent Slay the Spire 2 gameplay MOD, working title SelectMOD:

- Select, ban or restrict eligible ancient and boss encounters per Act.
- Enable ascension effects independently, including A1 OFF with A2 ON.
- Support multiplayer from the first implementation, with host-owned configuration and matching effective rules on every participant.
- Restore the original run rules and already resolved encounters after save/load.
- Use the standard loader and game-bundled Harmony. No runtime dependency on BaseLib, RitsuLib, ModConfig, UCR or another MOD.

Proposed first entry point: standard custom runs, solo and multiplayer. Ordinary runs, daily runs, cross-Act placement, unlocking unavailable encounters, ancient reward selection, additional characters and host-only installation are outside the initial proposal. The requested ancient feature selects the appearing ancient, not its reward. Product boundaries can be revised deliberately after API investigation.

Require matching compatible SelectMOD/gameplay MOD installations on all peers. Plan for affects_gameplay=true; do not inherit the previous utility's false flag. Manifest identity, public name, payload format and supported game version remain undecided. Add a PCK only if actual assets require it.

## 2. Evidence and prerequisites

The completed reference project is UnifiedVanillaSave. Its 0.3.0 owner success report and v0.113.1 source review describe that project only. They establish neither SelectMOD acceptance nor the installed game's current version.

The existing technical research records version-pinned external source investigation. It favors standard ModifierModel/SavedProperty persistence and synchronization, identifies ascension and generation hook candidates, and documents where reference MODs use BaseLib. None of those signatures has been verified against a current game DLL in this task.

Before choosing patches, obtain a read-only source/metadata report identifying the real game build, DLL hash/MVID, tool version, exact signatures, callers and initialization order. Cover new/load lobbies, Modifier serialization/clone, first ascension use, Act generation, map construction and room restoration. Prepare one export script when implementation begins; do not require manual hash approval flags or copy the old save utility's exporter targets.

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
2. Build a minimal independent loader/Modifier proof: host edit → guest apply → start → save → fresh-process load. Prove complete round trips before adding the feature UI.
3. Add pure rule validation/canonicalization and the verified freeze/start mechanism. Test stale revisions, duplicate messages, cancellation, timeout and membership changes where applicable.
4. Add encounter rules and independent ascension with focused boundary tests. Verify initialization timing, no repeated initial effects, A10 and RNG behavior.
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
