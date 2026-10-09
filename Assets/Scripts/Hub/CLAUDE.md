# The Hub (`Assets.Scripts.Hub`)

The town between runs, and every service that hangs off it. `HubManager` drives `HubScene`;
`MerchantUI`, `PartySelectUI`, `CampaignMapUI` and `CampaignPresenter` live here too. The Forge
(`Cards/UI/MagicForgeUI`), the Bestiary (`Enemies/UI/BestiaryUI`), the Inventory
(`Items/UI/InventoryHubUI`) and the Sphere Grid (`Heroes/UI/SphereGridUI`) live with their subsystems
but are constructed and driven from here.

## Three scenes, and the loop between them

```
MenuScene  ──Continue (= open the save file)──▶  HubScene
 title only                                         │  ▲
 no save read                              road ▶ run │  │ level cleared / run complete / party wipe
                                                    ▼  │
                                              MainGameScene
```

*(Split on 2026-09-05. It was one scene with a view swap until then — `docs/plans/HUB.md` §7 open
question 5 recommended keeping it that way, and that recommendation was deliberately overturned.)*

- **MenuScene is the landing scene** (build index 0) and is **dependency-free on purpose**: no
  managers, no catalogs, no save file. That is what makes room for the **save-slot picker** it is
  meant to grow — a picker cannot share a document with screens that read the save it has not chosen
  yet. With one slot today, **Continue is that choice**.
- **HubScene is where the game lives.** Both ways out of a dungeon — `DungeonManager.cs` on level
  clear *and* run complete, and `RoomActionUI`'s death screen — load **HubScene**. The game never
  returns to MenuScene on its own; the only route back is the town's explicit **Main Menu** button.
- **Scene names are string literals in exactly three places**: `HubManager.OnEnterDungeon`
  (`MainGameScene`), `HubManager.OnLeaveToMainMenu` (`MenuScene`), and the two dungeon exits
  (`HubScene`). There is no quit-to-menu or pause path from a dungeon.
- **`HubManager.MarkRunCompleted(runKey)` is a static** written by `DungeonManager` on the way out of a
  finished run — the victory screen is owed across a scene load (and names the run, with a "Now open"
  row per run the clear made startable, `CampaignOps.OpenedBy`), and statics are the only thing that
  crosses it besides `DungeonManager`'s own. It moved here with `complete-view`; leaving it on
  `MainMenuManager` would have meant the victory screen never showed.
- **Nothing is `DontDestroyOnLoad` except `MusicPlayer`.** Every manager is re-created and
  re-`Load()`ed per scene, so MenuScene → HubScene is cheap and safe by construction — the cost is
  re-reading save files. `MusicPlayer.Play(MusicTrack.Hub)` runs in *both* scenes, and requesting the
  track already playing is a no-op, so walking into town does not restart the bed.
- **HubScene must carry its own `MagicCatalog` and `MagicComboCatalog` prefab instances.** They are
  scene-wired `SingletonBehaviour`s with `[SerializeField]` lists, and `SingletonBehaviour`
  auto-creates a bare GameObject when it finds none — so a scene missing them does not throw, it
  produces an **empty** catalog and the Forge renders nothing. `HubUISetup` instantiates them for you.
  `MetaProgressManager`, `InventoryManager` and `PartyResourceManager` load from disk in `Awake` and
  would survive auto-creation; the meta-progress prefab is placed anyway so the scene states its
  dependencies.

## The town

**A painted town, not a column of buttons.** Home used to be ten stacked `cd-menu-button`s in a
`cd-window--tall` (88%) frame with ~85 units of headroom left — *one more button* — which is the
measurement `docs/plans/HUB.md` §7 was written against. Services are lots now and the hub can grow
without a layout budget.

- **`HubSO`** (`Assets/Resources/Hub.asset`, Resources-loaded exactly like `CampaignSO` and
  `ItemCatalogSO`) holds every `BuildingSO`, the backdrop, and **`ReferenceSize`** — the pixel rect
  every authored `Position` is expressed in. Changing that rect invalidates every position.
- **`BuildingSO`** is pure content: `Key` (save id), `Service`, the two rectangles
  (`Position` + `HitSize`, `DrawOffset` + `DrawSize`), `DrawOrder`, per-state sprites,
  `PlacedByDefault`, and the progression fields — `PlacementCost`, `RequiredRunKeys`,
  `MaxLevel`, `GoldPerUpgrade` (the *first* rung's price — see `BuildingOps.UpgradeCost`) and
  `LevelGrants` (what each level buys, in the player's words).
- **Progress lives in the save** — `MetaProgressSaveData.Buildings`, the same split `CampaignSO`
  makes with `CompletedRunKeys`, so one authored town reads differently per save.
- **All rules are in `BuildingOps`** (pure, static, scene-free): `StateOf`, `LevelOf`, `InDrawOrder`,
  `LotRect`, `SpriteFor`, `UpgradeCost`, `GrantFor`, plus authoring validators. `HubPresenter` turns those into classes and
  text; `HubView` only draws. `BuildingOpsTests` and `HubContentTests` drive them with no scene.

### Two rectangles per lot, and why

`Position` + `HitSize` is **the box you can click**. `DrawOffset` + `DrawSize` is **where the sprite
paints**. They are separate fields because a painted town needs silhouettes that overlap — a tower
behind a roof, a banner past a wall — while UI Toolkit's hit-testing stays stubbornly rectangular.

`HubView` renders them as three layers: backdrop, then a **sprite layer** on draw rects, then the
**buttons** on hit rects. A button with art behind it goes transparent and drops its glyph — the
sprite is doing the identifying. `HubContentTests.NoTwoLots_HitBoxesOverlap` polices the hit boxes
and says nothing about the art, which is free to overlap as much as it likes.

**A lot's name and badge hang below it, never on it** (`.hub-lot__caption`, 2026-09-28). Inside the
hitbox they sat on the building's own art (the Storehouse's name over its door) and wrapped to the
lot's width mid-phrase. The caption is a child of the button: it moves with the lot, never picks and
does not wrap. A lot within `CaptionRoom` of the town's bottom edge (the campfire) takes its caption
**above** instead, or it runs off the frame.

**The town prints no costs or requirements** (the owner's call, 2026-09-28). A town where every
building printed its costs read like a spreadsheet. Beside each name there is one badge instead:
- a **hammer** when there is something to build or raise (`LotBadge.Build`);
- a **padlock** when the lot is not on offer yet (`LotBadge.Locked`);
- a **gold pip** on a *built* lot with something waiting inside (`LotBadge.New`, 2026-09-30): a grid
  node some owned hero can buy now, a combo upgrade at the Forge the purse covers, or a Bestiary with more enemies
  met than when it was last opened (`BestiaryViewedCount`). It wins over the hammer. `HubManager.BadgeWithNews`
  decides it, since it needs the roster and the save; the presenter stays pure;
- nothing when the lot is finished.

`HubPresenter.BadgeFor` picks the badge and `HubView.SetLotBadge` draws it (icons in
`Assets/UI/Icons/hub_hammer.png` / `hub_lock.png`). The price, what the lot grants and what unlocks it
(`DescribeState`) live on the lot panel, one click away. Do not bring a text note back to the town.

### Three more constraints the renderer exists to satisfy

- **The town scales as one unit.** Everything is absolutely positioned inside one fixed-size canvas
  that is uniformly scaled and centred (`HubView.Relayout`). Scaling lots individually desyncs the
  art from the hitboxes — UITK applies a transform to hit-testing as well as to paint, which is the
  same trap `cd-window--fixed` exists to avoid.
- **UI Toolkit has no z-index.** Siblings paint in the order they are added, so `BuildingOps.InDrawOrder`
  (DrawOrder, then `Position.y` as a painter's algorithm, then list order) is the *only* thing
  deciding which building is in front. Nothing may re-sort the lots after `SetTown`.
- **A USS transition only runs on a change.** `hub-art--phasing` is therefore the *starting* state —
  `HubView.SetLotSprite` adds it, lets a frame lay out, then removes it, and the sprite animates from
  there back to its resting opacity and scale. A class that set the *end* state would animate nothing,
  because the end state is already the default.

### Building and upgrading

The gates are **on**. A lot is `Absent` until every key in its `RequiredRunKeys` is cleared, then
`Available` (a foundation, and a price), then `Built`.

- **Materials gate *whether*, gold gates *when*.** Placement spends `PlacementCost` out of the
  inventory — materials only come out of runs, so a lot depends on **where the player has been**.
  Upgrades spend `GoldPerUpgrade`, which keeps gold's tuition role and gives the hub a sink that
  scales forever.
- **Money is not in `BuildingOps`.** Affordability needs the inventory and the purse, which are
  singletons; `HubManager.CanPayFor` asks them. Every rule in `BuildingOps` stays a pure function of
  a `HubProgress`, which is what lets the tests and the balance model reason about hub states no save
  ever held. Same split `SphereGridOps.CanActivate` makes about material costs.
- **The level is recorded only after the payment succeeds**, so a failed spend can never leave a
  building standing for free.
- **A click stops at the lot panel only when there is a decision to make** — unbuilt, or upgradable.
  A finished lot opens its service directly, because a panel on every merchant visit is a tax on the
  common case (`HubPresenter.NeedsPanel`).
- **A locked lot names the run in its way** rather than saying "Locked" — a gate you cannot see the
  far side of is just a dead button (`HubManager.DescribeLotStatus`).
- **The lot panel (2026-09-30)** is one fixed size (`.hub-lotpanel`): the building's sprite (an unbuilt
  lot previews what it will become), a have / need cost table built by `HubManager.BuildLotCosts`
  (which also returns the shortfall sentence for `lot-reason`), the labelled grant, and the buttons
  **Enter, Build / Upgrade, Back** in document order — the shared cursor's first arrow lands on the
  first, and Enter is the common action on a built lot. `HubPresenter.ActionLabel` says only what the
  button does ("Build", "Upgrade to Lv 2"); the price is the table's.

#### What a level grants — the rule, and the first lot to use it

**A level grants access, capacity or information — never a raw stat.** This is not taste. `HUB.md`
§7 makes buildings a *hard* axis on the investment frontier — you cannot substitute XP for a Forge
you have not built — so a building is a **precondition the model tests**, not a currency it prices.
The moment a level grants power, gold reaches power by a second route and `InvestmentPointsPerGold`
(repriced in `BALANCING.md` §5q) is measuring the wrong world. Author capacity, ceilings, choice and
knowledge; leave stats to gear and the grid.

**The Ability Forge is the first lot with a ladder** (2026-09-17). `MaxLevel 3`, `GoldPerUpgrade`
250, and what each level buys is the **upgrade ceiling** for combos: 1 → 3 → 5.
`MetaProgressManager.UpgradeCeilingForForgeLevel` is the pure mapping, `ComboUpgradeCeiling` the
save-aware read, and every combo Can/Cost/Try path consults it instead of the old flat constant.
*(It covered abilities too until 2026-10-06, when the Forge stopped upgrading abilities: Essence
became the revisit currency and an ability grows on its hero's sphere grid through Essence-priced
nodes, `docs/plans/HUB.md` §3c. The Abilities tab is a read-only collection now.)*

Three properties make it safe, and they are the template for the next lot:

- **It gates buying, not what has been bought.** A level already paid for keeps its power forever —
  `GetComboPowerBonus` deliberately does not consult the ceiling. No hub change can reach back into
  a combo the player has already raised.
- **A full Forge lands exactly on `MaxComboUpgradeLevel`**, where the flat constant used to sit, so
  the *endgame* power ceiling is unchanged and only the ramp is gated.
  `HubContentTests.TheForge_CanBeRaisedToTheGamesUpgradeCeiling` fails if the authored ladder and the
  code ceiling ever drift apart — authored short and the deep combo levels become unsellable;
  authored tall and a level of the lot buys nothing.
- **It says so on screen before the player pays.** The Forge header carries `Forge ceiling: Lv N`,
  a capped combo reads *"raise the Forge to go further"* rather than showing a dead button, and
  the lot panel carries the promise beside the price.

**Two pieces of shared machinery landed with it**, and every later lot inherits them:

- **`BuildingOps.UpgradeCost`** — the authored `GoldPerUpgrade` buys the *first* rung, and each one
  after costs a multiple (`UpgradeCostForLevel`: 250 / 500 / 750). Flat pricing makes a ladder stop
  mattering the moment the player can afford one rung; the curve is the one `PartySlots.CostForNext`
  and `ComboUpgradeCostForNextLevel` already use, so the three ladders escalate alike. Read the cost
  through this, never off `GoldPerUpgrade` directly.
- **`BuildingSO.LevelGrants` + `HubPresenter.DescribeNextGrant`** — one authored line per level,
  shown in the lot panel as *"Level 2: …"*. Authored on the building because what a level means is
  different for every service and the town renderer must not know any of it.
  `BuildingOps.GetLevelsWithNoGrant` + `HubContentTests.EveryLevelOnSale_SaysWhatItBuys` fail on a
  priced rung that never says what it buys — the same rule `NoLot_OffersAFreeUpgrade` enforces from
  the other side.

**The Campfire is the second lot with a ladder** (2026-09-17), and it is the one that shows the rule is not just about the Forge. `MaxLevel 3`, `GoldPerUpgrade` 300, and a level grants **ways to divide the run's XP** — Even, then Mentor, then Catch Up (`CampfireOps`). Every mode hands out the same total, so the fire grants *control over a fixed pool* and can never make a run pay more. It landed alongside the removal of the party-slot purchase: the party is four wide from the start, because buying the right to field a hero was gold buying a hero, which §5b had already ruled out. See the Heroes guide for the split itself.

The other four lots are still `MaxLevel 1`. `HubState.LevelOf(service)` is the seam each one reads
when its turn comes — the merchant would size its stock off it, the Sphere Hall gate grid depth
(that one is `HUB.md` phase **5**, and it changes `GreedySpend`). The **Storehouse deliberately has
no ladder**: `InventoryManager` has no capacity cap and inventing one to have something to sell
would be a nerf dressed as content. It gets its rungs when crafting lands (phase 7).

#### The opening sequence (provisional)

Priced against the measured yields in `docs/plans/HUB.md` §7 — Scrap Iron is an order of magnitude
the most plentiful (≈31.7 a campaign), which makes it the right currency for a cheap frequent cost
and the wrong one for a gate.

| lot | offered | costs |
|---|---|---|
| Campfire | placed by default | — |
| Storehouse | placed by default | — (your own bag, never gated) |
| Hall of Progression (`sphere-hall`) | from the start | 1 Rotted Timber |
| Bestiary | from the start | 4 Scrap Iron |
| Merchant | from the start | 10 Scrap Iron · 1 Rotted Timber (was 8 + 2 until playtest 2 finding 14: timber only drops from caches) |
| Magic Forge | after `TutorialRun` | 3 Ember Iron · 2 Slag Coal |

The Sphere Hall is deliberately the cheapest thing in town and is **not** gated on a run: the grid
is where banked XP goes, and making a player finish a run before they can spend any of what they
earned reads as a lock rather than as pacing.

**Its one timber is a guarantee, not a roll.** Clearing *Dungeon Entrance* — the campaign's first
floor — always yields exactly it (`LevelDefinitionSO.GuaranteedMaterials`; see the Dungeon guide).
That is deliberate and it is the reason the field exists: the first loop a tutorial can point at
(**take the road → clear the floor → come home → build the Sphere Hall → spend your banked XP**) has
to be a promise the game keeps, and every other tap is a roll on a roll. Rotted Timber is otherwise
**cache-only** (§7 phase 1's table: ~3.1 a campaign, none from kills), and that first floor has
`TreasureRooms: 0` — so without the guarantee it produced no materials at all.
`MaterialContentTests.EveryOpeningHubCost_IsObtainableOnTheOpeningRun` is what keeps this true if
the prices or the floors move. The tutorial itself is `docs/plans/POLISH_CONTENT.md` §20.

**These numbers are a first pass, not a balance pass.** They exist so the flow is exercisable; the
yields they are priced against were measured before anything spent materials.
`HubContentTests` guards the shape rather than the values: every gate names a real run, every lot is
reachable by clearing the campaign, no line asks for more than a campaign yields, and a fresh save
always has something standing *and* something offered.

### The road is not a building

`HubService` has no `Story` member and `road-btn` is authored in `Hub.uxml`, not in `Hub.asset`. The
story is the way *out* of town, not a service the town provides, and **a building must never be able
to lock the player out of running** — `docs/plans/HUB.md` §7 open question 4, guarded by
`HubContentTests.TheStory_IsNotABuilding` alongside
`CampaignAssetTests.Campaign_NeverStrandsASaveWithNothingToPlay`.

There is also **no separate "Continue Run" affordance**: `CampaignMapUI` already renders the active
run as continuable, so the road is one door with two meanings, decided by the save.

### The campfire, and the storehouse

The two `PlacedByDefault` lots, so a fresh profile owns a working hub without the save writing
anything (`BuildingOps.LevelOf` reads a default-placed lot as level 1 on an empty save). The
campfire opens `PartySelectUI` (the party slot it used to sell is gone — see the Campfire ladder above). The storehouse is free for a different reason: it is the
player's **own bag**, not a service someone provides, and gating it would mean the loot from run one
cannot be equipped (§7 open question 6).

### Art

**Real art since 2026-09-30** (PixelLab, `docs/PIXEL_ART.md` §8c) — the September placeholders
from `tools/hub-art/` are gone from every field; that folder's scripts are dead weight now. What is
in `Assets/Sprites/Hub/`:

- `hub_backdrop.png` — 320×180, **exactly ¼ of `ReferenceSize`**, so a lot position maps to a whole
  backdrop pixel. Its horizon sits in the top fifth, the ground is open (the buildings stand on it)
  and the road runs up the right edge toward the signpost — keep that composition if it is redrawn,
  or the authored lot layout stops fitting it.
- `bld_*.png` (64×64; the campfire 96×64) and `lot_foundation.png` — 3/4-view buildings on
  transparent backgrounds, drawn at **exactly 3×** (`DrawSize` 192×192, the campfire 288×192). The
  PNGs were overwritten in place, so every GUID and `BuildingSO` reference is the original.
- `hub_signpost.png` — the road (`road-btn`) is a signpost now (`.hub-road--art`), not a panel.
- All of them import uncompressed (`textureCompression: 0`) — compression smears pixel art.

`AbsentSprite` is left **null** on purpose: a locked lot falls back to the flat slab and its glyph,
which reads as "something is coming here". `HubView` degrades to that slab wherever a sprite is
missing, so the town stays playable with no art at all. **The art layer shares the lot's state
classes** (`hub-lot--available` …); `.hub-art.hub-lot--*` clears their slab background, or a dark
box paints behind the sprite. The `hub-*` classes live at the end of
`Assets/UI/Theme/CardDungeon.uss` — but **before** `.cd-nav--selected`, which must stay last.

### Ambience — the town is alive *(2026-10-08)*

Presentation only; nothing else reads it.

- **`BuildingSO.IdleFrames` + `IdleFps`** play in place of a *built* lot's sprite (`BuildingOps.FramesFor`
  - only when built, and only author them for a lot whose level sprites are all the same art). The
  campfire and forge have 7-frame PixelLab loops (`bld_campfire_1..6.png`, `bld_forge_1..6.png`, frame 0
  the original).
- **`HubAmbientLight`** (`BuildingSO.Lights`, `HubSO.BackdropLights`): a point in design pixels (from the
  lot's DrawRect corner, or the town's), a glow (`Radius`, `Color`, `Intensity`, `Flicker`) and optional
  `EmbersPerSecond` / `SmokePerSecond`. `HubSO.TwinklingStars` + `StarField` scatter stars over the sky.
- **`UI/AmbienceLayer`** draws all of it: the glow is `fx_glow.png` (a stepped, pixel-art radial, tinted per
  light), sparks and smoke are one-backdrop-pixel squares snapped to the 4x grid, and one scheduled tick
  (50 ms) drives everything. The maths is pure in **`HubAmbience`** (`HubAmbienceTests`): flicker is two
  octaves of Perlin noise per light, never a random value per frame (a strobe). `HubView` has two layers -
  the backdrop's under the buildings, the lots' over them - and `SetLotAmbience` re-arms a lot after a
  build. Capped at 90 particles.
- **Menu backdrops** (`.cd-bg--title / --level-entry / --story-map`): `UI/BackdropAmbienceView` lays an
  `AmbienceLayer` on a canvas the image's size x4 and cover-scales it exactly as
  `scale-and-crop` scales the image, so a glow stays on its torch at any window size. Data:
  `Resources/BackdropAmbience.asset` (`BackdropAmbienceSO`), keyed by the backdrop class.
  `HubManager.RefreshBackdrop` and `MainMenuManager.Start` attach and switch it.

## UI Toolkit (this is how all game UI works)

All UI is **UI Toolkit** (UXML + USS), not uGUI. The pattern, used identically by every screen:

- A **UXML** file defines the view tree (`Assets/UI/Hub/Hub.uxml` here; also
  `Assets/UI/MainMenu/MainMenu.uxml`, `Assets/UI/Combat/MagicSelection.uxml`,
  `Assets/UI/Rooms/RoomAction.uxml`).
- A shared **theme stylesheet** `Assets/UI/Theme/CardDungeon.uss` styles everything. Each UXML links
  it via `<Style src=…>`. The `hub-*` classes live near the end — but **before** `.cd-nav--selected`,
  which must stay last (see Keyboard navigation).
- A **controller MonoBehaviour** on a `UIDocument` queries elements by name, registers `clicked`
  callbacks, and toggles views via `style.display`. Dynamic lists are built as `VisualElement`s in
  code — **no prefabs, and never at runtime what could be authored**.
- An **editor bootstrap** wires the serialized refs: **Tools → Hub → Setup Hub UI** for HubScene,
  **Tools → MainMenu → Setup Main Menu UI** for MenuScene. Both operate on *the open scene*. Re-run
  after editing structure, then save the scene.

**Every window wears one pixel-art frame** (2026-10-08): `Assets/UI/Frames/window_frame.png`, 24px with
6px nine-slice borders drawn at 3x (`-unity-slice-*`, `-unity-slice-scale: 3px`) on `.cd-window` and
the combat panels (`.cd-bar`, `.cd-command-window`, `.cd-party-status`, `.cd-turn-order`,
`.cd-victory`). The border is an 8px **transparent** border so content never paints over the rim, and
the fill is part of the image. A new window class should reuse that block rather than a flat border.

`HubUISetup` wires `_document`, `_runDefinition` and `_partyRoster`, and additionally guarantees the
EventSystem, a camera and the three prefab instances. `MainMenuUISetup` now wires **only**
`_document` — the run definition and the roster left with the screens that needed them, which is the
split working.

**The hit-test trap** is the most load-bearing UI rule here: a `cd-dock-center` window that *changes
size* leaves UITK's input hit-testing on the old transform, offsetting every click. That is why
`bestiary-view` and `campaign-view` are `cd-window--fixed`, `inventory-view`, `party-view`,
`merchant-view` and `grid-view` are px-sized `cd-inv-window`s (1180x720) whose every region exists in every state, and why the town is one
fixed canvas.

## Keyboard navigation

`HubManager` owns **one** `ImmoralityGaming.Menu.KeyboardNavigator` on the document root, and it
navigates *whatever buttons are currently visible* rather than a list wired per screen — which works
because only one view is displayed at a time. Arrows move (spatially, falling back to document order
for up/down so a plain column wraps), Tab steps, Enter/Space presses, Escape backs out.

- **The town is included, not excluded.** Its lots are ordinary `Button`s and the navigator moves
  between them spatially on **`worldBound` centres**, which carry the letterbox transform — so the
  arrows follow the town as drawn, and the road and the Main Menu button join the same cursor for
  free. `HubView` deliberately has no cursor of its own.
- **`NavigatesCurrentView()` is the gate**, and it excludes the campaign map, the sphere grid, the
  bestiary and the inventory. Those four build their own cursors because they pan or scroll content
  the shared navigator cannot see; they are children of this same root, so without the gate a key
  they chose not to handle would bubble up here and be acted on twice.
- **There is no cursor until the first arrow key.** A highlight painted the moment a screen opens
  would sit on a mouse player's screen forever.
- **Escape presses the screen's own Back button** (`CancelButtonForCurrentView`) rather than calling
  the panel's `Hide` — the panels raise `OnClosed` from there and this class depends on that to get
  back to the town. The forge stacks an inspect page over its grid, so Escape backs out one layer at
  a time. **In the town Escape does nothing**: leaving for the title screen is a deliberate click,
  not something to do by accident mid-run.
- **`PanelKeyboard.Claim()` runs every frame from `Update`.** A UITK panel receives the OS keyboard
  only while its `PanelEventHandler` is the EventSystem's *selected* GameObject; clicking a UITK
  element selects it as a side effect, and clicking the background clears it again. See gotcha 15 in
  `docs/GAMEPLAY_VALIDATION.md`.
- **A service covers the town, it does not hide it** *(2026-09-30)*. The lot panel and every service
  call `CoverTown()`: `hub-view` stays shown but **disabled** under `hub-scrim`, so the painted town
  (and the Build sprite swap) stays on screen while the shared cursor — which only walks *enabled*
  visible buttons — cannot reach a lot behind the window, and the scrim takes the mouse. The screens
  with a backdrop of their own (story map, level entry, run complete) call `HideTown()`. Anything that
  asks "is the town the screen?" must use **`IsTownActive()`**, not `IsShown(_hubView)` —
  `NavigatesCurrentView` and the tutorial's `CurrentTutorialScreen` both do.
- **Every panel switch calls `ResetKeyboardNavigation()`**: the cursor pointed at a button on the
  screen that just went away, and focus may have been taken by a panel that focuses its own subtree.
- The highlight class is `cd-nav--selected`, defined **last** in `CardDungeon.uss` on purpose: every
  button class there is a single-class selector of equal specificity, so source order is what makes
  the cursor win over the button's own background — including `.hub-lot`.

## Panels

Each is a **plain view-controller** (not a MonoBehaviour): it takes the `VisualElement` subtree for
its view, queries its controls, and exposes `Show()` / `Hide()` + an `OnClosed` event. `Hide` **is**
the close path — it raises `OnClosed`, and `HubManager` never calls it directly.

- **CampaignMapUI** — the story map, and the only way to start a run. Draws `CampaignSO` as a node
  graph: cleared runs behind you, open ones ahead, locked ones greyed with a "Requires …" line,
  secret branches absent until they unlock. All progression decisions come from `CampaignOps`, all
  styling from `CampaignPresenter`. It does **not** write `Run.json` — it raises `OnRunChosen` and
  the manager does, so there is exactly one writer. Reuses `SphereGridView` as its renderer with its
  own `cm-node--*` state classes. On the inventory frame since 2026-09-30 with a legend and a detail
  column (floors of the run, what a clear opens); `Show(runKey, activeLevelIndex)` needs the level
  for the "Level 2 of 4" line. **It always opens on `DefaultSelection()`** (the run in progress, else
  the first open one) — it used to remember the last node, so Enter could reopen on a locked run. **Every visible run is named under its node** (2026-09-28,
  playtest finding 11) through `SphereGridView.NodeInfo.Caption`, which is **opt-in**: only
  `CampaignPresenter` sets it, so sphere grid nodes stay caption-free. `NodeCaptionTests` pins both
  halves — change one and the other will tell you.
- **PartySelectUI** (the campfire) — which owned heroes actually march out. Writes
  `PartySaveData.SelectedHeroKeys` through `HeroRoster.SetSelectedKeys`; `DungeonManager.FieldedHeroes()`
  reads it. **On the inventory frame since 2026-09-30** (`cd-inv-window` + `cd-camp-*`): four seat
  tiles (seat 1 leads), the roster, a detail column and the XP split (`CampfireOps`, the lot's ladder).
  A roster row or seat only **selects**; March out / Stay behind / **Lead the party** live in the
  detail column with a `party-reason` line saying why a dimmed one is dimmed — so a stray click never
  benches anyone. Every control is a `Button`, so the shared navigator drives it (it is *not* one of
  the screens with its own cursor). Minimum one hero; the cap is `MetaProgressManager.GetPartyCap()`
  (four; the slot purchase is gone). The screen states the XP share as the standing cost of width.
  Reachable from the campfire and from the run-progress screen next to *Enter Dungeon*;
  `_partyOpenedFromProgress` sends Back where the player came from.
  **The lineup locks for the rest of a run** *(2026-10-02)* once its first floor is entered
  (`CampaignOps.LocksParty`: `RunKey` set and a floor generated or cleared) until it is cleared or lost.
  The screen still opens, since the leader and the XP split can still change, but March out / Stay behind dim
  with the reason in `party-reason`, and the run screen hides *Change Party*. A run only picked on
  the map is still open. Swapping mid-run was a cheese: charges are run-scoped, so a fresh hero
  replaced a spent one, and after a mid-floor quit a newcomer resumed at full HP in place of a downed hero.
  Rescues still join mid-run (`TryFieldIfRoom`). They were earned inside the run.
- **The level-entry screen** (`progress-view`, `HubManager.ShowRunProgressPanel`) — one fixed size
  (`.hub-entry`): run name + "Level N of M", a pip per floor (`BuildLevelPips`), the fielded party as
  portrait tiles with HP (`BuildPartyTiles`), and Enter Dungeon / Change Party / Back in that order (Change Party hidden once the run is
  underway).
- **MerchantUI** — the Gold sink (gear, and the healing-potion carry cap). See the Progression guide.
  **On the inventory frame since 2026-09-30**: Buy / Sell / Potion Belt tabs, a hero strip choosing
  who gear is compared against, and a detail column reusing the inventory's grants block
  (`InventoryHubUI.AddItemLines`, `StatCell`, `SetIcon`, `ApplyRarity` are `internal static` for this).
  Same row-selects / detail-acts split as the campfire, driven by the shared navigator.
- **MagicForgeUI** (`Cards/UI`) — Essence sink for **combos** plus the ability collection
  (Abilities tab read-only since 2026-10-06), undiscovered entries folded into one count row. **Requires a `MagicCatalog` in the scene** or it logs a warning and shows empty.
- **InventoryHubUI** (`Items/UI`) — the between-runs bag: Equipment / Abilities / Consumables /
  Materials. Equipment is managed *only* here. **Redesigned 2026-09-28** into one fixed frame: a header
  band with the tabs, a portrait hero strip (muted, never hidden, on the party-wide tabs), three
  px-wide columns and a per-state hint line. Equipment is **FF-style**: the cursor walks the hero's
  slots, the middle column lists what fits the slot under it — the bag *and* what other heroes wear
  (equipping takes it off them) — and Enter moves into that list, where the detail column shows every
  stat now → after. Esc backs out one layer. Stats are **base + bought grid nodes + gear**
  (`HeroStatCalculator`); the old screen and the campfire used `HeroSO.BaseStats` and understated
  every hero who had spent XP. Abilities are described through `HeroSnapshotUnit`, so the numbers
  are the hero's own. Item wording (slot names, bonus lines, rarity classes, the swap maths) is
  `Items/ItemPresenter` (`ItemPresenterTests`).
- **BestiaryUI** (`Enemies/UI`) — the enemy knowledge collection. On the inventory frame since
  2026-09-30: met enemies as rows, the unmet as compact unselectable rows at the bottom, a portrait
  page in two columns; opens on the first enemy met. Keeps its own Up/Down cursor.
- **SphereGridUI** (`Heroes/UI`) — the one place XP is ever spent. On the inventory frame since
  2026-09-30 (portrait strip with banked XP, frontier framing via `SphereGridView.FrameNodes`, a
  detail column with a `grid-reason` line). Keeps its own cursor (it pans content the shared
  navigator cannot see).

## The tutorial

`HubManager`'s "THE TUTORIAL" region applies `Assets/Scripts/Tutorial/TutorialOps` to the town: a
**New Game** (`HubManager.RequestNewGame`, set by the title screen) skips the town and goes straight
into floor 1; home again, every lot but the Hall of Progression, the road and Main Menu are
**disabled**, the hall's panel is locked to Build then Enter, and the grid to one node
(`SphereGridUI.SetGuide`). Only `TutorialStarted`/`TutorialFinished` are saved; the step is derived.
Read **`docs/TUTORIAL.md`** before changing any of it — including why `CanPayFor` must use
`InventoryManager.Instance`, not `HasInstance`.

## The tavern is gone

Retired 2026-09-05 with this refactor (`NEXT_STEPS.md` §5b: *"the tavern is gone; heroes are unlocked
through progression only. Gold never buys a hero again."*). `TavernUI`, `tavern-view`,
`MetaProgressSaveData.TavernStock`, `ShopPricing.RecruitPrice`, `HeroSO.RecruitCost` and
`HeroRoster.RemoveOwned` are all deleted. `HeroRoster.GetRecruitable` became **`GetUnownedHeroes`** —
it is the "not yet unlocked" set an unlock system wants.

**Heroes are obtained by rescue only** (`RunLevelEntry.RescueHero`), on top of a starting lineup of
exactly one. §5b's unlock half **shipped 2026-09-06**: the record was already there
(`PartySaveData.OwnedHeroKeys`) and the missing piece was that `StartingHeroes` was handing out three
heroes, which made the authored rescues no-ops. `CampaignNodeEntry.RequiresHeroes` now lets a run gate
on owning a hero — `CampaignMapUI` reads the roster and renders the shortfall as a *"Needs in your
roster: …"* line. *"The roster screen needs a home that is not a shop"* is answered: it is the campfire.

What is still open is **where unlocks come from**: rescue is the only source, so four of the seven
heroes are unreachable content. Clearing a run, a room event, a secret node and a boss are all
unbuilt.

One consequence recorded rather than fixed: `BalanceAnalyzer` still models the widest party from the
whole catalog, which the tavern used to justify (a hero was bought with gold exactly as a party slot
is). A hero is a progression *unlock* now — a hard precondition on the frontier rather than a currency
inside it — so that assumption is marked in the code and left for the balance pass, which is paused
until the specialization refactor lands.
