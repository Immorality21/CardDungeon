# Pixel Art — generating sprites with PixelLab

How sprites for Card Dungeon are made, sized, animated, imported and verified. Written after the
first PixelLab pass (2026-09-27: Abyssal Warden, Dark Jailor, Paladin). Read it before generating
or replacing any sprite, and add to it when a pass teaches something new.

**Short version:** PixelLab MCP for anything that matters, **match the size of the sprite you are
replacing**, animate with `animate_image`, pick three frames, overwrite the PNG in place, then
**watch it animate in a real fight** through the Unity MCP. **A new or replaced hero or enemy also
needs its hit frames (the wince, §5b)**: ask for them in the same pass, not as a follow-up. A summon
needs them only if it is a **party replacement** (`SummonKind.ReplaceParty`). The old hand-drawn `pixel-art` skill
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
costs one. **Subscribed 2026-09-28** (Tier 1, 2,000 generations a month).

**At most 8 jobs run at once.** A 9th request fails with `rate limit exceeded (8/8 jobs)` and is not
queued (nor charged), so feed a large batch in waves: queue 8, `wait_for_jobs`, queue as many as
finished. Keep the name → job id list in a scratchpad file between waves.

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
- Heroes stand in one column up to two, two ranks from three (`HeroFormation`). Enemies use `EnemyFormation`: one column up to three, FF ranks of
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
- **Heroes (pixen, 32×32): heroes have BIG heads** (owner's rule, 2026-09-29). The reference is
  the **Rogue**: its hood/head fills rows 0–19 of 32 — about **60% of the sprite's height**, ~26 px
  wide — over a small body, and that head is where the detail (the face) lives. "Cute chibi, big
  head" is not enough on its own: the first Warrior round (2026-09-29) came back with heads a third
  of the height. The prompt that hit it:
  *"super deformed chibi <hero>, oversized head taking up two thirds of the sprite, tiny body,
  detailed face with big eyes, <outfit and props>, muted desaturated dark colours, thick dark
  outline, full body, facing right, retro JRPG party sprite"*, `detail: "highly detailed"`,
  `single color black outline`, `direction: "east"`. Keep the muted-colour words — the Rogue sits at
  ~0.2 average saturation and a bright hero stands out next to it. A forced palette
  (`color_image_base64` of the Rogue, pixflux) made worse sprites at 32 px, not closer ones. Without
  "chibi" at all, pixflux produces realistic, thin, outline-less knights.
  Measure before choosing: count the head's rows, and compare colour count / saturation with the
  Rogue (53 colours, 0.19 lightness, 0.21 saturation).
- **Item icons (pixen, 32×32, all 30 redone 2026-09-28):** *"<name>, <what it looks like>, dark
  fantasy RPG inventory item icon, single object centered, clean readable silhouette"* (weapons
  add "diagonal", shields/armour "front view", materials say "crafting material inventory icon"),
  `no_background`, `single color black outline`, `medium detail`, **seed 9001 for the whole set** -
  one seed across a set keeps the palette and shading consistent. Wearables need **"empty item, no
  person"** or the model dresses a figure in them (the first cloak had a face in its hood; the
  first greaves and gauntlets came back as a whole armoured body). Review a batch as a contact
  sheet at 4x on the inventory's tile colour, not one by one: the misses and the near-duplicates
  (a "simple sword" that was the iron sword again) only show side by side.
  Import: equipment icons overwrite their own PNG in `Assets/Sprites/Items/` (GUID kept); a new
  icon copies `iron_sword.png.meta` with a fresh GUID and the item's `Icon` is re-pointed.
  `ItemPresenterTests` fails if an item has no icon or two items share one.

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
4. **At 32 px with a big-head hero, expect the generated frames to change the face** — on the
   2026-09-29 pass every frame after the first turned the Cultist's, Cleric's and Ranger's face, a
   blink or a new expression, and the Tinkerer kept only one. The loop that always works is
   **rest → rest with the upper body dropped 1 px → one generated frame that only moves a prop**
   (a flame, a glow, a blinking gadget), or the rest frame again when there is none.
   When the generated frames are unusable, build the loop the way the original heroes do:
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

## 5b. Hit frames — the wince

Struck units **flinch** (`CombatFeedback.Flinch`): a knock-back away from the other side, plus,
when the definition has them, a one-shot of **drawn hit frames** over the idle loop
(`SpriteAnimator.PlayOnce`, all frames together over 0.4 s - the first frame is the impact, the second the held wince). The code recoil alone was lost under the
camera shake (2026-10-07), which is why the drawn frames exist. Read through `IFlinches.HitFrames`.

**Who needs them — check this whenever you generate or replace a unit:**

| unit | hit frames? | field |
|---|---|---|
| **Hero** | **always** — every hero flinches | `HeroSO.HitFrames` |
| **Enemy that flinches** | **always** | `EnemySO.HitFrames` |
| Enemy with `Flinches` off (bosses, golems, statues — anything meant to read as heavy) | no | — |
| **Party-replacement summon** (`SummonKind.ReplaceParty`) | **yes** — it stands alone on the field, takes the hits, and flinches like a hero (unless `Flinches` is off, e.g. the Cairn Golem) | `SummonSO.HitFrames` |
| Any other summon (special attack, beside the party) | no (owner's rule, 2026-10-07) | — |

An Ultra form shows no hit frames (`Hero.InForm`): the hero wears the form's art, so their own wince
would flash the wrong figure. A form sprite therefore needs none.

**How they were made (2026-10-07, 17 units):**

1. `animate_image(first_frame_url=<the idle strip's frame 0>, action="gets hit and recoils, flinches
   backwards in pain, wince, then recovers", frame_count=4)` — one generation per unit, run in waves
   of 8.
2. Download all 5 frames and lay them out as a contact sheet (4x, on the combat purple). **Frames 2
   and 3 were the recoil for every one of the 17**, and none of them changed identity — at 32 px the
   wince reads as a lean back and a squint, which is what you want.
3. Same rule as §5: reject a frame that turns the face or drops a prop the unit is known by.

**Files:** a 2-frame horizontal strip next to the idle, `<name>-hit.png` (64×32 for a 32 px unit),
sliced `<name>_hit_0` / `_1`, same import settings as the idle strip (§6). It must match the idle's
**size and facing** — enemies are drawn facing right and flipped in combat, and the hit frames get
the same flip. A unit with no hit frames still flinches; it just only recoils.

## 6. Importing into Unity

- **Replacing a sprite: overwrite the PNG in place.** Same size, same slicing → the `.meta` (GUID,
  sprite `internalID`s) is untouched and every `EnemySO`/`HeroSO` reference keeps working. The
  Paladin swap was a single file copy.
- **Where things live** (reorganised 2026-10-07; there is no `Animation/` folder any more):
  `Assets/Sprites/Heroes/` (`<hero>-idle.png`, `<hero>-hit.png`, an Ultra form as
  `<hero>-<form>-idle.png`), `Assets/Sprites/Enemies/` (`<enemy>-idle.png`, `<enemy>-hit.png`, the
  static `<Enemy>.png`), `Assets/Sprites/Summons/`, `Items/`, `Abilities/`, `Environment/`, `Hub/`,
  `UI/`, `Backgrounds/`. `_Unused/` holds art nothing references — some legacy, some not wired up yet.
  Move sprites with `AssetDatabase.MoveAsset`, never on disk, so the GUIDs and references survive.
- **Strips** are `<name>-idle.png` in their unit's folder: horizontal, 3 frames, full-frame
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

## 8b. The 2026-09-29 enemy pass — what it learned

Six enemies redone in one sitting (Gilded Hoarder, Mirefather, Gilded Mote, Hex Weaver, Stone
Sentinel, Dark Jailor): **2 candidates each + 1 `animate_image` each = 18 generations.**

- **Redraw, don't animate, the old flat art.** The five "still" enemies were original hand-drawn
  sprites (~10 colours). Animating them would have kept two styles on one stage; a PixelLab redraw
  of the same subject costs one generation more and matches the Warden and Bog Shaman.
- **Candidates side by side, old art in the first column.** One contact sheet (`old | A | B`, 4x, on
  the combat purple) made every pick in one look — including that both Jailor candidates had drifted
  from violet to molten orange.
- **A frame picker in code, identity checks by eye.** Choose three frames with `f0` fixed and the
  smallest worst cyclic difference, but only among frames that keep the subject's identity: the
  Hoarder's gem went dark on frames 3-4, the Sentinel's head shifted on 3, the Mote smeared into a
  double coin on 3, the Jailor's outline thickened on 1. Losing the lightning on some of the Hex
  Weaver's frames was kept — it reads as flicker.
- **Facing still bites on asymmetric subjects.** A "front view" chest came back with its mouth to the
  left, so after the combat flip it faced away from the party. Mirroring every frame of the static
  PNG and the strip in place fixed it (GUIDs and slices untouched). Check a combat screenshot, not
  the PNG, for anything with a mouth, a weapon or a face.
- **The import is scriptable end to end.** Static PNG overwritten in place (GUID kept, PPU set), the
  strip's meta generated from `bog-shaman-idle.png.meta` with a fresh GUID, fresh slice
  `internalID`s and rects, then `AnimationFrames` / `AnimationFps: 4` / `CombatScale: 1.8` written
  into the `EnemySO` YAML. Reusing an existing strip's GUID and slice IDs (the Jailor) means its
  `EnemySO` needs no edit at all, even when the frame size changes.

## 8c. The 2026-09-30 hub town pass — what it learned

The town (six buildings, the unbuilt plot, a signpost for the road, the backdrop) redone in one
sitting: **2 candidates each with `create_image_pixflux` (64×64, the campfire 96×64,
`no_background`, `view: "low top-down"`, `single color black outline`, `highly detailed`, seeds 5101 /
5202) + 4 failed backdrops + 1 `create_image_pro` backdrop ≈ 45 generations.**

- **Buildings: one prompt shape for the whole set** — *"<building> for a dark fantasy RPG town,
  <materials and two identifying props>, muted purples and greys with warm <fire/lantern/violet>
  light, night"*. "low top-down" came back as a consistent 3/4 isometric set with its own little
  ground tile. Pick for the identifying prop: the Bestiary's candidate A buried its skull.
- **A backdrop that buildings stand on is not a landscape.** Text-only pixflux backdrops came back
  as side-on scenes with the horizon at 70% and cottages of their own — unusable under isometric
  buildings. **img2img from a hand-made flat-colour composition guide** (8 colours, 931 bytes, small
  enough to pass as `init_image_base64`) kept the layout but came back flat and bland at strength 60
  and 90. What worked: **`create_image_pro` (25 generations, one candidate at 320×180) with the chosen
  forge's download URL as `style_image_url`** (`style_copy`: palette, outline, shading) and a prompt
  that states the composition in words and says *"no buildings - leave the ground open"*.
- **Pro backdrops can come back with a baked white border** (here 5 px left, 2 top, 3 bottom, fully
  opaque). Fill it by repeating the nearest real edge pixel rather than cropping and rescaling, which
  would break the pixel grid. Check the edges of any pro scene before importing.
- **Draw at a whole multiple.** The placeholders drew 64 px art at 200×190 (3.1×, uneven pixels);
  the new rects are exactly 3×. The backdrop is 4×, which is close enough to read as one world.

## 8d. Hub idle loops — what the 2026-10-08 pass learned

The campfire and forge animated with `animate_image` from the shipped PNG (passed inline as a
`data:` URL - both are under 6 KB), `frame_count=6`, seeds 7301 / 7302, 1 generation each.

- **Describe what must stay still**: *"... logs and stones stay completely still"* /
  *"building stays completely still"*. The forge came back with only the door and windows moving.
- **Even so, check outside the moving part.** The campfire's generated frames lightened the stone ring
  slightly (a handful of pixels outside the flame), which would flicker the ring. The fix that keeps
  the art exact: paste only the flame's box from each generated frame onto the original
  (`box=(34,0,63,37)` on the 96x64 sprite) - measure where frames differ from frame 0 first.
- **Smoke, sparks and glow are code, not frames** (`AmbienceLayer`): the model did not draw chimney
  smoke when asked, and code effects can spill outside the sprite's box.

## 8e. Combat backgrounds and the death screen — the 2026-10-08 pass

Four combat backgrounds (`Sprites/Backgrounds/combat_halls|caverns|archive|chapel.png`) and the death
backdrop (`UI/Backdrops/death.png`), all `create_image_pro` at 320x180 (the death screen 384x216, like
the other menu backdrops), `no_background: false`, **15 generations each** (25 for 384x216), one
candidate each, every one used first time.

- **Style reference: an existing background, halved and quantized to 32 colours** (`combat_blood`,
  then `level_entry` for the death screen) passed as a `data:` URL - about 6-9 KB of base64. The full
  PNGs are 30-45 KB and too big to pass inline; style only needs palette, outline and shading.
- **Prompt shape**: *"side-view battle backdrop for a dark fantasy dungeon RPG: <place, 4-6 concrete
  props>, a broad flat floor across the bottom third left open for combatants, no characters, no
  creatures, <palette> with <light source>"*. The floor-band clause is what makes it work with
  `CombatStage.BackgroundFloorLift`.
- **Composition for a screen with a dialog**: say where the focus goes ("on the right half ... the left
  half is deep shadow and plain wall") and dock the dialog on the empty side.
- No baked borders this time - still check the corners.

## 9. Unused designs

Every generation is kept in the PixelLab gallery. The armoured-guardian Warden candidate became the
Dark Jailor; the realistic Paladin candidates (`0e5d9e6b…`, `98990fed…`) and the img2img one
(`f9e6df74…`) are still there if a future hero or enemy wants them.
