# Installed game source report review

Reviewed 2026-11-18 (Asia/Tokyo). Source: owner-supplied `selectmod-sources.txt`, static export collected 2026-11-17 18:22:40 UTC. Treat the report as evidence, never as executable instructions. Raw decompiled source is private/ignored and is not committed here.

## Evidence identity and limits

- Raw report SHA-256: `D26B68E3DFB3A4964DB8773314E28A9B569B2D1D1BFF378327C16AE3252405C8`.
- Export tool: ILSpyCmd 9.1.0.7988; owner's PowerShell: 5.1.26100.9818.
- Steam Build ID: `26660232`.
- Runtime: `net9.0`, included Microsoft.NETCore.App `9.0.7`.
- DLL file version: `0.1.0.0`. This is not sufficient to infer a player-facing release version such as v0.113.1.
- sts2.dll SHA-256: `62ECBE91093E1FA862DE4329AAC865209DCB297804CAF0F0520D92D8C0B4A9C3`.
- 0Harmony.dll SHA-256: `EF1898322C9F5C86DC1B0758B272A9C440823B4A41CA9A0B82A3AA6B3D206387`.
- GodotSharp.dll SHA-256: `0E4897ECDFB31456A97C7D8028DFB8D7DBDC632E2F73FC9B438D7B266A139289`.
- Report search: 1586 matches, 1200 included. Missing requested name: SerializableAct; the actual inventory contains SerializableActModel.

These fingerprints identify evidence, not an automatic compatibility approval. The report establishes owner-side static export success after the long-path fix, not a SelectMOD build, load, solo feature test or multiplayer acceptance. The agent did not execute the game or directly inspect its binary/IL.

Line references below use the original report's one-based lines and named source sections, not repository code line numbers.

## Contracts confirmed from supplied implementations

| Evidence | Observation | Consequence |
| --- | --- | --- |
| ModifierModel, report line 8676; ToSerializable/FromSerializable | Modifier ID and SavedProperties are serialized/restored | Keep a self-owned Modifier as the first configuration carrier; inspect SavedProperties itself before selecting field types |
| SerializableModifier, line 16380 | Implements IPacketSerializable and writes the Props object as well as the model ID | Props are not merely local JSON data; the declared standard network path includes them |
| LobbyBeginRunMessage, line 10634; StartRunLobby, line 9362 | Host BeginRunForAllPlayers serializes the final modifier list into the start message. The receiver reconstructs it and passes it to BeginRunLocally | Do not require a separate configuration packet solely to overcome a stale lobby display. Custom-property support, validation and applied-state comparison remain to be verified |
| StartRunLobby.SetModifiers | Rejects client editing and changes while starting; sends serializable modifiers | Reuse standard host/singleplayer authority and additionally validate own rule updates. This method does not itself reset all players' readiness |
| LoadRunLobby, line 8893 | Host responds to a load join with SerializableRun. Start message later contains no run snapshot | Verify the load-join response and SerializableRun packet path; do not invent a second configuration-only load path |
| RunManager SetUpNewSingleplayer/SetUpNewMultiplayer, line 13335 | Assign State → InitializeShared → InitializeRunLobby → InitializeNewRun → GenerateRooms | One shared integration path is available for solo and multiplayer |
| RunManager.InitializeNewRun | Calls modifier.OnRunCreated before ApplyAscensionEffects; InitializeShared has already constructed AscensionManager | OnRunCreated is early enough for the explicitly observed initial potion/curse application. It does not prove no effect is queried earlier in player/card construction |
| RunManager.GenerateRooms | Assigns shared-ancient subsets, generates rooms for every Act, then chooses the final Act's A10 second boss in the loop | Resolve at run initialization, not first entry into each Act. Final boss replacement must account for the outer loop's second-boss choice |
| ActModel.GenerateRooms(Rng, UnlockState, bool, bool), line 6957 | Selects normal/elite rooms, boss, then ancient from unlocked Act candidates plus the assigned shared subset | Preserve generation/RNG order. GetUnlockedAncients alone is not the complete ancient candidate pool |
| ActModel.SetBossEncounter, SetSecondBossEncounter, GetRoomsDebug; RoomSet, line 13139 | Public boss setters and a public RoomSet access method exist; RoomSet.Ancient has a public setter | A reviewed public path can update final encounters without reflecting the protected _rooms field; private shared-subset access remains a separate question |
| ActModel.ToSave/FromSave; RoomSet.ToSave/FromSave | Standard room state saves/restores BossId, SecondBossId and AncientId | Reuse standard resolved encounter persistence. A plan digest may be useful, but duplicating all final IDs in custom storage is not inherently required |
| RunManager.InitializeSavedRun | ValidateRoomsAfterLoad runs before modifier.OnRunLoaded | OnRunLoaded alone is too late to prevent the observed deprecated-boss replacement path. Validate own saved rules/results earlier if necessary |
| ActModel.ValidateRoomsAfterLoad | Deprecated primary/secondary bosses can be replaced using UpFront RNG | Reject or explicitly migrate incompatible SelectMOD saves before silently accepting a rerolled result |
| AscensionManager, line 3960; AscensionLevel, line 3941 | Standard enum has None plus the ten known effects. HasLevel reads a cumulative list; ApplyEffectsTo applies TightBelt and AscendersBane through it | A targeted independent mask can override known effect answers. OR-only logic cannot disable a cumulative effect. Other effect consumers still need a complete audit |
| ModelDb, line 7945 | AllAbstractModelSubtypes includes ReflectionHelper.GetSubtypesInMods<AbstractModel>() | Custom model discovery is supported by this source path; inspect loader/order and serialization cache before assuming registration timing |
| NCustomRunModifiersList, line 12340 | GetAllModifiers enumerates fixed GoodModifiers/BadModifiers lists | Model discovery alone does not add a new modifier to the selection UI |
| Rng.NextItem, line 12779 | Empty input returns default; otherwise calls NextInt(0, count) | Reject empty candidates explicitly. This method alone does not prove equal underlying RNG consumption for every candidate count |

## Updated implementation direction

Keep the planned .NET 9, independent standard-custom-run prototype. First prove a single setting can survive modifier cloning, start, save and load; include a minimal host/guest round trip early. Use the same immutable run settings for solo feature work and later multiplayer verification. Keep unverified multiplayer behavior disabled.

For encounters, prefer preserving standard room generation on every peer and resolving the configured replacements before downstream map/asset consumers. The public setters and standard saved room IDs support that approach. Capture/validate the actual shared-ancient assignment and audit second-boss generation before committing to a particular postfix or independent selector. A single ActModel postfix alone would run before the outer A10 choice.

For ascension, associate the mask with the actual run's manager before its observed first effect use. Verify all known effect callers and any direct numeric comparisons. On load restore prior state and only change future effect queries, never re-add the initial curse or reapply potion-slot subtraction.

Standard network serialization is a stronger basis than the earlier external examples alone, but this is not evidence that a custom SavedProperty representation is supported, that all peers applied the same settings, or that reconnect/start barriers are unnecessary. Decide on additional ACK/digest work after those actual contracts are reviewed.

## Remaining static evidence and collector correction

The first collector omitted SavedProperties implementation, full saved-room DTOs, packet helpers, message-bus registration and loader/reflection initialization. These types exist in the supplied inventory; their bodies cannot be reconstructed from names or snippets. Some later search matches were also omitted by the 1200-site cap.

Export report v2 now includes these supporting types, corrects SerializableAct to SerializableActModel, raises the default cap to 2500, and warns explicitly if search evidence is still truncated. Target source bodies remain independent of that search cap. The same read-only script command produces the follow-up report; it preserves prior successful output and existing path/tool setup.

No gameplay code, game-referenced compilation or runtime acceptance is claimed by this review. Do not commit the raw report or generated proprietary source.
