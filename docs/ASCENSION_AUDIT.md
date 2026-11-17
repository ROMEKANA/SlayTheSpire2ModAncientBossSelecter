# Independent ascension integration audit (0.2.0)

> Historical 0.2.0 audit. Independent ascension is disabled in 0.3.0 after an owner-reported startup freeze. No runtime effect acceptance was obtained. Current work is solo encounter selection; do not treat the mechanism below as active.

Source evidence: the owner's v2 report identified in INSTALLED_GAME_REVIEW.md, Steam Build ID 26660232 and sts2.dll SHA-256 62ECBE91093E1FA862DE4329AAC865209DCB297804CAF0F0520D92D8C0B4A9C3. Source excerpts are static evidence, not agent-run game tests. Proprietary source is not committed.

## Reviewed effect routes

AscensionLevel defines None=0 and the ten effects in order. AscensionHelper.HasAscension and its int/float/decimal GetValueIfAscension helpers delegate to RunManager.HasAscension, which checks IsInProgress then calls its actual AscensionManager.HasLevel. The latter normally checks the cumulative level list. The supplied search contains all 1586 matches of the collector's pattern; this is not exhaustive semantic/IL analysis of arbitrary aliases or numeric thresholds.

| Effect | Observed consumers / source symbols | 0.2.0 handling |
| --- | --- | --- |
| A1 SwarmingElites | MapPointTypeCounts.NumOfElites | Scoped helper/manager query override |
| A2 WearyTraveler | AncientEventModel healing path | Scoped helper/manager query override |
| A3 Poverty | EncounterModel gold bounds, OneOffSynchronizer reward | Scoped helper/manager query override |
| A4 TightBelt | AscensionManager.ApplyEffectsTo | Binding exists before new-run initial effects |
| A5 AscendersBane | AscensionManager.ApplyEffectsTo | Same early binding; game load path does not reapply initial effects |
| A6 Inflation | MerchantCardRemovalEntry.BaseCost/PriceIncrease | Scoped helper/manager query override; preserve original game formulas |
| A7 Scarcity | CardFactory upgraded-card scaling, CardRarityOdds dynamic probabilities/growth and public static regularCommonOdds | Query override plus a run-scoped cached-field override, restored on cleanup |
| A8 ToughEnemies | Numerous monster HP properties via helper | Scoped helper/manager query override; verify actual HP in game |
| A9 DeadlyEnemies | Numerous monster move/strength properties via helper | Scoped helper/manager query override; verify actual actions in game |
| A10 DoubleBoss | RunManager.GenerateRooms final-Act second-boss branch | Same scoped actual manager; log final saved-room second-boss presence after generation |

Most recorded numeric AscensionLevel/Ascension comparisons concern display, history, progress/unlocks, scoring and mode selection. They are not rewritten to pretend the independent mask is a cumulative difficulty. No promise is made that a non-contiguous mask qualifies for standard achievements/progression. Standard custom-run policy remains authoritative.

## Lifecycle and isolation

The selected SelectAscensionSettings Modifier stores its own schema and 10-bit mask as real SavedProperty integer fields. Model absence means normal cumulative mode; explicit mask0 means independent all-OFF. Old SelectSettingsProbe models/fields remain unchanged, so 0.1.0 probe saves do not acquire effects from current preferences. Required new fields default to -1, rejecting missing values. Freeze disallows changing an active model to different values.

Before the standard custom lobby starts, its cumulative value is normalized to0 through the observed SyncAscensionChange method. Run/save guards require that cumulative0 alongside the independent model; modes and multiplayer outside the supported entry are rejected. The UI choice describes the effective A numbers; this release configures them through Setup's -Ascensions parameter, with the existing one-command installer retained.

Solo setup prefixes validate and associate an immutable mask with that RunManager before shared initialization. The helper query can consult this binding once IsInProgress is true, including before shared initialization has returned. InitializeShared postfix binds the newly constructed actual AscensionManager before InitializeNewRun and room generation; unknown enum values and unrelated managers use the original methods. It compares observed known-effect answers with the frozen mask before progression. New/load setup failures release only a binding identified by that attempt's token, not an existing run's binding. Faulted asynchronous load tasks are observed too; a late failure from an older attempt cannot remove a newer run's binding. Cleanup finalizer releases bindings even when cleanup throws.

A7's regularCommonOdds field is a static initialization cache in the supplied source. At manager attachment, preserve its current value and assign the game's observed A7-on/off values, 0.615f/0.6f. Restore the previous value on cleanup; if another component changed it in the meantime, log the conflict and preserve that external value rather than overwrite it. This is a narrowly scoped bridge for the observed cache, not a general replacement for game reward math. Cross-run behavior is covered in fixtures; actual game probability behavior still requires acceptance.

Competing Harmony owners on HasLevel/HasAscension are rejected before beginning an independent run. Original MOD/model hashes and checksum diagnostics remain enabled. Multiplayer is still globally blocked by this development release. A replay containing the new model is explicitly refused rather than silently running its effects at cumulative0. Old/unselected replays are not changed.

## Normal-run extension point

The supplied NGame.StartNewSingleplayerRun signature accepts both an IReadOnlyList<ModifierModel> and isCustomGame, calls RunState.CreateForNewRun, then RunManager.SetUpNewSingleplayer. RunState holds/restores Modifiers independently of its IsCustomGame flag. The effect implementation therefore depends on the selected run model and actual manager, not a screen or lobby type.

0.2.0 does not inject the model into ordinary runs or relax its custom-run validation. A future ordinary-run adapter must attach settings before run creation/early effect consumers, choose UI placement, audit normal-run start/cancel/reload routes and settle achievement/unlock/history behavior. Merely changing the custom boolean is not sufficient. No additional broad source export is requested now, and ordinary-run support is not claimed.

## Verification boundary

Independent contracts plus real test-only Harmony exercise all 1024 masks through both effect query paths, including OFF overriding a cumulative backing value, unknown enums, unrelated managers, early helper queries, initial A4/A5 decisions, A7 apply/restore, A10 generation decision, frozen values, save-vs-preference separation, missing fields, collisions and cleanup. These fixtures never replace production game references and are not evidence of actual enemy stats, reward odds or a network round trip.

The 0.1.0 owner evidence remains valid for its original scalar-probe path. The added 0.2.0 properties/hooks require a fresh actual-reference build and solo acceptance. First check A1 OFF/A2 ON/A4 ON, then A5 alone with save/load, all-OFF mode, consecutive different-mask runs and A10 before claiming gameplay success. Actual multiplayer testing remains deferred.
