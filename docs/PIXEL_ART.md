# Pixel Art — generating sprites with PixelLab

How sprites for Card Dungeon are made, sized, animated, imported and verified. Written after the
first PixelLab pass (2026-09-27: Abyssal Warden, Dark Jailor, Paladin). Read it before generating
or replacing any sprite, and add to it when a pass teaches something new.

**Short version:** PixelLab MCP for anything that matters, **match the size of the sprite you are
replacing**, animate with `animate_image`, pick three frames, overwrite the PNG in place, then
**watch it animate in a real fight** through the Unity MCP. The old hand-drawn `pixel-art` skill
path is a fallback for when PixelLab is out of generations.

---

## 1. Setup

PixelLab is an HTTP MCP server, registered in the **local** (per-project, not checked in) config:

```
claude mcp add pixellab https://api.pixellab.ai/mcp -t http -H "Authorization: Bearer <token>"
```

The token lives in `~/.claude.json`, never in the repo. Restart Claude Code (or `/mcp`) after adding
it. Tools appear as `mcp__pixellab__*`.

**Check the budget first — every time:** `mcp__pixellab__get_balance` (free). The account started
on the **free trial: 40 generations**, after which it is ~5 slow generations/day unless subscribed
(Tier 1 ≈ $12/month). A generation is spent the moment a job is queued, so a rejected prompt still
costs one.

## 2. Sizes — the rule that bit

| unit | canvas | PPU | combat size | notes |
|---|---|---|---|---|
| **Heroes** | **32×32** | 32 | 1.5 units (the party scales heroes ×1.5) | all heroes, 3-frame 96×32 strips |
| **Normal enemies** | **32×32** | 32 | 1 unit | the rule (user, 2026-09-27): regular enemies are 32×32 |
| **Bosses / complex enemies** | **64×64** allowed | **38** | 1.68 units (×1.8 `CombatScale` ≈ 3 on the stage) | Abyssal Warden, Cinder Tyrant. PixelLab leaves padding round the figure; at PPU 64 it rendered at half hero height. Bosses are *meant* to take more screen space than that — see §2b |

The **Dark Jailor** (a normal, non-boss enemy) was made at 64×64 before this rule was stated, so
it is currently the exception.

**Before generating a replacement, read the size of the sprite it replaces and of its siblings,
and generate at that size.** A 64×64 Paladin was nearly generated on the assumption heroes matched
the new enemies; they do not.

### 2b. How combat sizes and places units (`Combat/CombatStage.cs`)

- `PlaceUnit` rescales every unit for the fight: heroes ×1.5 and enemies ×2 of their scale outside
  combat (the shared enemy prefab is 0.5, so enemies land at 1.0).
- Enemies are **mirrored** (`flipX = true`) so they face the heroes. **Author enemies facing right
  (or dead front).** A "front view" prompt often comes back turned slightly left; after the flip
  that enemy looks away from the party (the Bog Shaman did — fixed by mirroring each frame of its
  PNGs in place, which keeps the meta and slicing). `Capture2DScene` **does** render `flipX` (tested),
  so the combat capture is the truth — but a 32 px quadruped is easy to misread at capture scale:
  crop and upscale the unit before judging which end is the head (the old Slag Hound was wrongly
  reported as facing away). To predict it from the PNG alone: combat shows it **mirrored**.
- Heroes stand in one column. Enemies use `EnemyFormation`: one column up to three, FF ranks of
  2 front / 3 back for four or five, and a **boss alone at the back** with its escort in front.
  Column slots are `min(halfH × 0.5, halfH × 1.3 / count)` apart (2.17 units for three).
- **Boss size is `EnemySO.CombatScale`**, not PPU: the Warden is 1.8 × its 1.68 units ≈ 3 units,
  twice a hero. Keep normal enemies at 1 — they share a column and a bigger one overlaps.
- The health bar anchors on `SpriteRenderer.bounds`, so it follows a bigger sprite automatically.

Combat size = `canvas px / PPU × transform scale`. If a new enemy looks small next to the heroes,
lower its PPU (all of its sprite files, static and strip) rather than touching the shared
`Resources/Enemy.prefab` scale.

## 3. Which PixelLab tool

| need | tool | cost | notes |
|---|---|---|---|
| small sprite (≤32 px), clean and readable | `create_image_pixen` | 1 | **best for heroes.** Canvas side must be a multiple of 4; below 32 it must be square |
| 64 px enemy, or a forced palette / img2img | `create_image_pixflux` | 1 | `init_image_strength` is *how much is kept* (500 ≈ unchanged, 150 = a real edit, ~50 = composition only) |
| best quality, style reference | `create_image_pro` | **20–40** | returns many candidates; too expensive on the trial |
| idle loop from a loose sprite | `animate_image` | 1 (64×64×8 frames) | returns `frame_count + 1` images — index 0 is your input |
| rotatable character / tileset / map | `create_character`, `create_topdown_tileset`, … | varies | unused so far |
| hand edits, lint, palette work | `pixelart_workbench` | free | `describe cli` for commands |

Useful parameters for sprites: `no_background: true`, `outline: "single color black outline"`,
`direction` (enemies face the heroes — `south` read well; heroes face `east`), `view: "side"`,
a fixed `seed` so a re-run is reproducible.

Jobs are async: queue them in parallel, then `wait_for_jobs` (free) instead of polling, then
`get_image`. Download with the returned `download_url` (`?index=N` for animation frames) via `curl`
— never re-type base64 by hand.

### Prompting that worked

- **Enemies (pixflux, 64×64):** subject + materials + silhouette props + "full body front view,
  boss monster for a dark fantasy dungeon RPG". Two seeds per enemy gave a real choice.
- **Heroes (pixen, 32×32):** *"cute chibi …, big head, …, thick dark outline, simple readable
  shapes, full body, facing right, retro JRPG party sprite"*. Without "chibi / big head" pixflux
  produced realistic, thin, outline-less knights that clashed with the party.

## 4. Style consistency — know what you are mixing

PixelLab art is not the same style as the original party even at the same resolution:

| 32×32 frame | colours | lone pixels* |
|---|---|---|
| new Paladin (PixelLab) | 42 | 127 |
| Warrior / Rogue / Cleric (original) | 9–10 | 9–48 |

\* a pixel whose colour differs from all four neighbours.

It reads as "more detailed" although it is the same 32×32. **Naive colour quantisation does not
fix it** — a median-cut to 12–16 colours bled the plume's orange into the face and lost the cape.
The user chose to redo the heroes one by one instead, so the party converges on one style. If you
ever need to match the old flat look, regenerate with `detail: "low detail"`, flat shading and a
forced palette (`color_image_base64` of an existing hero) rather than post-processing.

## 5. Animation

The game plays `EnemySO.AnimationFrames` / `HeroSO.AnimationFrames` through `SpriteAnimator` — a
plain loop, `(frame + 1) % count`, at `AnimationFps` (4 for every animated unit). There is **no
procedural idle any more** (`CombatIdleMotion`, a scale pulse, was removed 2026-09-27), so a unit
without frames stands still.

1. `animate_image(first_frame_url=<download_url>, action="subtle idle breathing loop, … stays in
   place, returns to starting pose", frame_count=4)`.
2. Download all 5 frames and **look at them.** Measure the pixel difference between every pair and
   choose three frames whose cyclic differences (a→b, b→c, c→a) are all small — that loops without
   a pop. Enemies: Warden used frames `0,1,4`, Jailor `0,1,2`.
3. **Reject frames that change identity.** On the 32 px Paladin, frames 1–3 turned the face to
   profile — they would flicker. Only frame 4 (≈ frame 0 plus cape/plume sway) was usable.
4. When the generated frames are unusable, build the loop the way the original heroes do:
   **rest → upper body dropped 1 px (feet planted) → rest variant.** All original hero strips follow
   that pattern (frame 1's top edge is at y = 1).

```python
from PIL import Image
f0 = Image.open("frame0.png").convert("RGBA")
top = f0.getbbox()[3] - 4                       # bottom 4 opaque rows are the feet
dip = Image.new("RGBA", f0.size, (0, 0, 0, 0))
dip.paste(f0.crop((0, 0, f0.width, top)), (0, 1))
dip.paste(f0.crop((0, top, f0.width, f0.height)), (0, top))
strip = Image.new("RGBA", (f0.width * 3, f0.height), (0, 0, 0, 0))
for k, f in enumerate([f0, dip, other_frame]):
    strip.paste(f, (k * f0.width, 0))
```

## 6. Importing into Unity

- **Replacing a sprite: overwrite the PNG in place.** Same size, same slicing → the `.meta` (GUID,
  sprite `internalID`s) is untouched and every `EnemySO`/`HeroSO` reference keeps working. The
  Paladin swap was a single file copy.
- **Strips** live in `Assets/Sprites/Animation/<name>-idle.png`: horizontal, 3 frames, full-frame
  slices (`x = k × width`, `y = 0`), `spriteMode: 2`, `filterMode: 0` (point),
  `textureCompression: 0` on every platform, pivot centre. Copy an existing strip's meta as the
  template; give each slice a unique `internalID` and list it in both `internalIDToNameTable` and
  `nameFileIdTable`.
- **Static sprite** (`EnemySO.Sprite`, used by UI such as the bestiary) is frame 0 as its own PNG,
  `spriteMode: 1`, referenced as `fileID: 21300000`.
- **New enemy** = `EnemySO` asset + its own `EnemyBehaviorSO` (copy a preset or a sibling) + an
  entry in `Assets/Resources/EnemyCatalog.asset` (`BestiaryTests` fails otherwise). It spawns
  nowhere until a spawn table or a `RunLevelEntry.BossEnemy` names it.
- After writing files, `AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport)` via the
  Unity MCP and load the SO back to confirm `Sprite` and every `AnimationFrames` entry resolve, with
  the expected `rect` and `pixelsPerUnit`.

## 7. Verifying — in a fight, not in the importer

Import checks prove the references resolve; they do not prove it looks right. **Use the sandbox**
(`docs/SANDBOX.md`): put the unit in a config, `SandboxLauncher.Launch`, press Fight — it is
repeatable and writes to a throwaway save, so it does not reveal the enemy in the player's bestiary
(the side effect noted below applies to the hand-spawned route only). Otherwise do the combat run in
`docs/GAMEPLAY_VALIDATION.md` (open `MainGameScene`, play, `runInBackground`, BFS to an enemy room,
walk the doors, `StartCombat`):

- To see a specific enemy, clear the target room and `EnemyManager.SpawnSingle(so, room)` before
  walking in.
- **A test fight writes the bestiary.** `CombatManager.RecordEnemySeen` marks every enemy in the
  fight as seen and saves `savedata/Meta.json` immediately, so spawning an enemy for a visual check
  reveals it in the player's bestiary. Tell the user which enemies a test fight exposed.
- To see a hero who is not in the default party (Warrior + Paladin), call
  `GameManager.Instance.Party.AddHero(heroSO)` before walking in. It is in memory only — the party
  is written to disk only by `CommitProgress` on a level clear — so the test does not touch the save.
- **Size:** `Capture2DScene` over the whole encounter and compare against the heroes, plus read
  `SpriteRenderer.bounds.size`. This is what caught the half-height Warden.
- **Animation — don't sample from separate commands.** Commands are roughly a whole number of
  animation periods apart, so sampling kept landing on the same frame and looked stalled. Instead
  hook `EditorApplication.update` from one command and `Debug.Log` each sprite change for a few
  seconds, then read the console:

```csharp
internal class Recorder
{
    public static SpriteRenderer Target; public static string Last; public static float Until;
    public static void Tick()
    {
        if (Target == null || Time.time > Until) { EditorApplication.update -= Tick; return; }
        if (Target.sprite.name != Last) { Last = Target.sprite.name; Debug.Log("[Rec] t=" + Time.time.ToString("0.000") + " " + Last); }
    }
}
// in Execute: Recorder.Target = <renderer>; Recorder.Until = Time.time + 3f; EditorApplication.update += Recorder.Tick;
```

  A healthy 3-frame, 4 fps loop logs `_0 → _1 → _2 → _0` every 0.25 s.

## 8. Fallback — no PixelLab generations

When `get_balance` shows 0 generations remaining (or the MCP is not connected), the `pixel-art`
skill falls back to **hand-authoring the sprite as a Python pixel grid**. It works, but the result
is noticeably weaker — fine for icons and placeholders, not for heroes, enemies or anything
animated. Say so to the user before using it, and prefer waiting for the daily slow generations
for character art. `pixelart_workbench` stays free and is the better tool for touch-ups either way.

## 9. Unused designs

Every generation is kept in the PixelLab gallery. The armoured-guardian Warden candidate became the
Dark Jailor; the realistic Paladin candidates (`0e5d9e6b…`, `98990fed…`) and the img2img one
(`f9e6df74…`) are still there if a future hero or enemy wants them.
