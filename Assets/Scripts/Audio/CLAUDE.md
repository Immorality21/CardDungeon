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
  serialized into the bank): `MeleeSwing`, `Impact`, `MagicCast`, *(3 retired — was `Draw`)*, `Heal`, `ItemUse`,
  `BossSignature`, `EnemyDeath`, `Victory`, `Defeat`, `CursorMove`, `Confirm`. Called from
  `CombatManager` (attacks / cast / item / heal / boss wind-up / death / victory / defeat),
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

**Six loops are in** (2026-09-01), from the *Ultimate Game Music Collection* (John Leonard French),
living in `Assets/Audio/Music/`:

| Track | Clips | File loudness | Bank volume | Effective |
|---|---|---|---|---|
| `Hub` | `Tavern LOOP SLOW` | −16.3 LUFS *(after +14 dB, see below)* | 0.45 | −23.2 |
| `Exploration` | `Ambient Dungeon LOOP` (−13.3), `Murky Dungeon LOOP` (−15.3) | −14.3 avg | 0.30 | −24.8 |
| `Combat` | `Combat LOOP` (−15.6), `Tense Combat LOOP` (−16.1) | −15.9 avg | 0.50 | −21.9 |
| `BossCombat` | `Boss LOOP` | −10.7 LUFS | 0.35 | −19.8 |

Volumes are well below the SFX bank's 0.7–0.9 on purpose: these are mastered orchestral loops sitting
under interface foley, and they multiply with the player's Music dial on top.

### Bank volume is not a loudness control — read this before touching it

**The pack does not master its tracks at consistent levels, and the bank cannot compensate past 1.0.**
Measured with `ffmpeg -filter_complex ebur128=peak=true`, the six loops span **−10.7 to −30.3 LUFS**:
`Boss LOOP` is 5 dB hotter than the combat tracks, and `Tavern LOOP SLOW` shipped **15 dB quieter than
everything else**, with 14.5 dB of true-peak headroom simply unused.

That produced the one trap worth writing down. The hub bed was reported as too quiet in play; the
instinct is to raise `Entry.Volume`, but matching the other tracks needed ~6× gain and `Volume` is
`[Range(0f, 1f)]` — **even 1.0 was nowhere near enough**. The fix has to be **gain on the source
file**: the Tavern loop was re-encoded from the original WAV with `volume=14dB` (peak −14.5 → −0.7
dBFS, so nothing clips), which moved it −30.3 → −16.3 LUFS. Combined with the bank change it is
**+16 dB** on where it started.

Two consequences for anyone adding a track:

1. **Measure the file before you pick a number.** `ffmpeg -v info -i clip.ogg -filter_complex
   ebur128=peak=true -f null -` prints integrated loudness and true peak. A bank volume chosen without
   it is a guess against an unknown master.
2. **A number can be lower while the track is louder.** `BossCombat` sits at 0.35 against `Combat`'s
   0.50 and is still the loudest thing in the game, because its master is 5 dB hotter. Reading the
   bank as a presence hierarchy is wrong; the *effective* column above is the hierarchy.

`AudioClip.GetData` will not read these clips (Streaming load type is what makes that fail, so the
error is a confirmation, not a bug), and `AudioSource.GetOutputData` returns silence from inside an MCP
`RunCommand` — so verify levels on the **files** with ffmpeg and confirm only the multiplier chain in
play.

**The sources are OGG, not WAV, and that is deliberate.** Unity keeps the source file in the project
and compresses only for the build, so a repo pays full WAV: the six tracks are 72 MB as shipped and
**8.4 MB** transcoded to OGG q6 (~192 kbps) first, against a whole `.git` of 42 MB. The build re-encodes
to Vorbis anyway, so this is a second lossy pass — inaudible on a bed under combat foley at these
rates, and worth the 88% saving. Import settings on every clip: **Streaming** load type (music must
never sit decompressed in RAM), Vorbis at 0.7, no preload, `loadInBackground`.

**Only ~2% of that pack is imported.** The full 4.7 GB `.unitypackage` stays in the machine-wide
Asset Store cache (`%APPDATA%/Unity/Asset Store-5.x/John Leonard French/AudioMusicOrchestral/`) — a
download is *not* an import, and the archive is never moved into the project. To add more tracks, open
it again and import selectively; do not import the whole thing (it is ~50× this project's size, and
thousands of clips for Unity to re-encode). Named alternatives worth knowing: `Combat/Boss Battle 1–5
Loop`, `Locations/Medieval Market LOOP`, and the matched **`Dark Dungeon AMBIENT LOOP` /
`Dark Dungeon ACTION LOOP`** and `Barren Dungeon/Combat/Boss` families — the same material in ambient
and action arrangements, which is what `LevelDefinitionSO`'s per-level music fields are *for*.

**Never put clips in `Assets/Resources/`.** Everything under a `Resources/` folder ships
unconditionally, referenced or not. `MusicBank.asset` lives there (it must — it is Resources-loaded),
but it references the clips by GUID, so they belong anywhere else.

## Player-facing controls

The hub's **Options** screen (`Assets/Scripts/MainMenu/AudioOptionsUI.cs`, `options-view` in
`MainMenu.uxml`) is the only place volume can be changed today — there is no in-dungeon pause menu, so
a player mid-run cannot reach it. Its dials are **stepped buttons, not sliders**, because the hub's
keyboard cursor navigates buttons and a slider would be unreachable without a mouse. See the MainMenu
guide.
