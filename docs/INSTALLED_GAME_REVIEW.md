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

## Report v2 follow-up (2026-11-18)

The owner supplied report v2, collected 2026-11-17 18:29:31 UTC. SHA-256: `84343E80438811D84B789124D5E938A1A95F79E3C08CF1F278D27CFBF6738EB3`. Game/dependency hashes and Steam Build ID match the first report. All 1586 search matches are included (cap 2500), and the requested-type-not-found section is empty. This completes the broad static collection needed for the initial settings prototype; do not request another broad export merely to begin it. Search coverage still is not a semantic proof of all runtime routes.

The earlier section records what was unresolved in v1; the following observations supersede those specific evidence gaps.

| v2 source section | Confirmed contract | Implementation consequence |
| --- | --- | --- |
| SavedProperties, report line 19676 | FromInternal accepts int, bool, string, int[], enum, enum[], ModelId, SerializableCard and List<SerializableCard>. Unsupported non-null values throw. Null values are warned about and omitted | Use registered scalar fields for schema/mode/mask. A bounded canonical JSON string is a viable representation for Act rules; arbitrary rule DTOs, string[] and List<ModelId> are not directly supported |
| SavedProperties.Serialize/Deserialize | Writes and reads each supported storage category, including strings and integer arrays. Category property counts use 8 bits | Keep the small configuration model well within per-category counts. Do not reinterpret the 8-bit property count as a string-length bound |
| SavedProperties.FillInternal | Sets known named properties by reflection; unknown properties can be ignored and absent values do not populate fields | Use explicit invalid/uninitialized defaults for required configuration and validate after restoration. Missing schema/mask must not silently become a valid all-OFF configuration |
| ModelIdSerializationCache, line 12354 | Discovers SavedProperty attributes from model types, sorts properties by attribute order then ordinal name, and maps names to numeric network IDs | Declare real SavedProperty properties. Never insert arbitrary names into Props and assume they have a network ID. Keep names stable and use a schema for interpretation changes |
| ModelIdSerializationCache.Init | Calls ContentSorter for model IDs and includes relevant gameplay model/property names in its hash | Preserve this standard mechanism. Hash equality does not replace validation of our settings/schema; do not infer that field type/value compatibility is comprehensively covered by the hash |
| PacketWriter.WriteString / PacketReader.ReadString, line 13039 and its reader section | UTF-8 byte length is written as an int, followed by the encoded bytes | Bound our encoded rules before sending and after receiving; the final small payload limit must be tested with the real transport rather than guessed from property counts |
| SerializableRun; ClientLoadJoinResponseMessage, line 11982 | Run packet serialization carries both Acts and Modifiers; the load-join response serializes the entire run | The standard saved-configuration and resolved-encounter data are carried to a joining load client. No separate configuration transfer is justified merely because the later begin-loaded-run message is empty |
| SerializableActModel, line 20305; SerializableRoomSet, line 20384 | Packet serialization includes room state and optional primary boss, second boss and ancient IDs with matching readers | Standard persistence of resolved encounters is also a network contract, not only a JSON/local-save contract |
| ModManager, line 4478; ReflectionHelper, line 4360 | Mod initializer runs before the assembly is appended to the mod's assembly list and marked Loaded. ReflectionHelper caches types from loaded mods | Do not eagerly populate ModelDb/serialization/type-discovery caches inside our initializer. Install hooks there and verify model/property discovery later, after loading |
| NetMessageBus, line 13855 | Handler registration is separate from message numeric-ID resolution. Serialization uses message.ToId and deserialization uses MessageTypes. Handler exceptions are logged and caught | RegisterMessageHandler alone is not proof that a new message can traverse the wire. A throwing validation handler alone is not proof that the run has been stopped |

### Prototype contract selected from this evidence

Start with one self-owned settings Modifier and registered scalar properties, with a distinct schema sentinel, independent-mode flag and 10-bit ascension mask. Use an explicit bounded string encoding for Act rules when adding encounter configuration, rather than unsupported SavedProperty collection types. Normalize and validate complete values atomically; default/unset schema is invalid, while an explicitly configured zero mask is valid all-OFF independent mode.

Keep the standard modifier selection/change/start/save/load paths as the first data transport. Verify clone, JSON and actual packet round trips against the game assembly before implementing an additional transfer protocol. Do not introduce a new INetMessage just because generic handler registration exists; numeric message registration has not been established by that method.

The first prototype should prove discovery and property-cache membership after loading, solo setting/save/reload, then host edit → guest applied snapshot → start/load with deliberately different local defaults. Add an explicit start gate for invalid state and block unverified multiplayer feature use. Neither a successful source export nor the game's normal model hash establishes this acceptance.

There is no additional user-side source collection action required now. Gameplay implementation, actual-reference build and live solo/multiplayer checks remain separate next steps. The raw reports and extracted source remain outside Git.
