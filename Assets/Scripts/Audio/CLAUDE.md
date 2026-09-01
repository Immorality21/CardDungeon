# Audio (`Assets.Scripts.Audio`)

Everything the game plays, and the dials that scale it. One folder, one namespace — the SFX side
moved here from `Combat/Audio` when music arrived, because the music bed and the volume settings serve
the hub as much as they serve a fight.

## The three pieces

- **`CombatAudio`** + **`CombatSound`** + **`SoundBankSO`** — fire-and-forget one-shots. Auto-creates
  on first use (no scene wiring, like `CombatFeedback`). `CombatAudio.Play(CombatSound.X)` picks a
  random clip for the event and plays it 2D via `PlayOneShot`, so overlapping hits layer. Clips are
  mapped by a `SoundBankSO` at `Resources/CombatSoundBank` — a `CombatSound → {clips[], volume}` table
  authored against the `Assets/Fantasy Interface Sounds/` pack. Events (append-only; the ints are
  serialized into the bank): `MeleeSwing`, `Impact`, `MagicCast`, `Draw`, `Heal`, `ItemUse`,
  `BossSignature`, `EnemyDeath`, `Victory`, `Defeat`, `CursorMove`, `Confirm`. Called from
  `CombatManager` (attacks / cast / draw / item / heal / boss wind-up / death / victory / defeat),
  `RoomActionUI` (command-menu cursor + confirm) and `AudioOptionsUI` (so a dial you move is a dial
  you hear).
- **`MusicPlayer`** + **`MusicTrack`** + **`MusicBankSO`** — the looping bed. Also auto-creating, and
  **`DontDestroyOnLoad`**: the hub and the dungeon are separate scenes, and a bed that restarted on
  every load would stutter at exactly the moments the game wants to feel continuous. Two
  `AudioSource`s with a weight each, so every change is a crossfade and nothing cuts. Tracks: `Hub`,
  `Exploration`, `Combat`, `BossCombat` — members exist only where something asks for them, which is
  why there is no `Victory`/`Defeat` track (those are one-shot stingers, and the bed fades out under
  them).
- **`AudioOptions`** + **`AudioChannel`** + **`AudioOptionsSaveData`** — Master / Music / SFX plus a
  mute, persisted to `savedata/Audio.json` the moment the player moves one. Static, not a
  `SingletonBehaviour`: it is read from places with no scene of their own, and holds nothing a scene
  load should reset.

## Rules that will bite

- **Master is the listener; the other two are applied at the source.** `AudioOptions.Apply()` sets
  `AudioListener.volume`, so Master scales *everything*, including sounds no channel knows about.
  Music and SFX are multiplied in by their own players (`MusicPlayer.Update`,
  `CombatAudio.PlayInternal`) — a listener volume cannot tell one kind of sound from another. Never
  fold Master into `MusicVolume`/`SfxVolume`: it would be applied twice, i.e. squared.
- **Mute is a gate, not a stored zero** (`AudioOptions.Gated`). Un-muting has to give the player back
  the dials they set.
- **`MusicPlayer.Play` for the track already playing is a no-op**, and deliberately checks the *track*
  before picking a clip — a multi-clip track picks at random, so comparing the freshly picked clip
  against what is playing would restart the theme on every call. `GameManager.Initialize` and
  `LevelMusic.PlayExploration` are called far more often than the music needs to change.
- **A track with no clips fades to silence** rather than leaving the previous bed running. That keeps
  a half-authored bank obvious instead of playing the floor's theme under a boss fight.
- **`Resources` must be qualified as `UnityEngine.Resources`** in this project — the game has its own
  `Assets.Scripts.Resources` namespace.
- **The dials snap to `AudioOptions.Step` (10%)** and `Snap` maps NaN to 0. A hand-edited or truncated
  `Audio.json` can hold NaN, and NaN reaching `AudioSource.volume` silences the game with no error.

## Where music is decided

`MusicPlayer` knows nothing about dungeons. **`Assets/Scripts/Dungeon/LevelMusic.cs`** is the seam:
it reads `DungeonManager.CurrentLevel` and hands the player a per-level override clip, the same split
`CombatStage` uses for the per-level battle backdrop.

- `LevelDefinitionSO.ExplorationMusic` / `.CombatMusic` — optional per-level (per-biome) clips that win
  over the bank's track. A boss ignores `CombatMusic` and takes the bank's `BossCombat`, because the
  point of a boss theme is that it is *not* the floor's.
- Call sites: `GameManager.Initialize` (both the new-level and resumed-level paths come through it →
  exploration), `CombatManager.RunCombat` (→ combat/boss), the victory and defeat branches (→ stop),
  `CombatManager.FinishVictory` (→ back to exploration), `MainMenuManager.Start` (→ hub).

## State of the content

**`Resources/MusicBank.asset` is checked in with all four tracks authored and no clips in any of
them** — the project owns no music files (only the interface-foley SFX pack). Everything above works
and is silent until clips are dropped in; that is the same discipline the hub plan uses for art
(author the fields, fill them later). Nothing needs to change in code to add music: drop loops on the
bank's four entries, or on a `LevelDefinitionSO` for a floor of its own.

## Player-facing controls

The hub's **Options** screen (`Assets/Scripts/MainMenu/AudioOptionsUI.cs`, `options-view` in
`MainMenu.uxml`) is the only place volume can be changed today — there is no in-dungeon pause menu, so
a player mid-run cannot reach it. Its dials are **stepped buttons, not sliders**, because the hub's
keyboard cursor navigates buttons and a slider would be unreachable without a mouse. See the MainMenu
guide.
