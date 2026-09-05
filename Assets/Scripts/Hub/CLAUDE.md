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
- **`HubManager.MarkRunCompleted()` is a static** written by `DungeonManager` on the way out of a
  finished run — the victory screen is owed across a scene load, and statics are the only thing that
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
  `MaxLevel`, `GoldPerUpgrade`.
- **Progress lives in the save** — `MetaProgressSaveData.Buildings`, the same split `CampaignSO`
  makes with `CompletedRunKeys`, so one authored town reads differently per save.
- **All rules are in `BuildingOps`** (pure, static, scene-free): `StateOf`, `LevelOf`, `InDrawOrder`,
  `LotRect`, `SpriteFor`, plus authoring validators. `HubPresenter` turns those into classes and
  text; `HubView` only draws. `BuildingOpsTests` and `HubContentTests` drive them with no scene.

### Two rectangles per lot, and why

`Position` + `HitSize` is **the box you can click**. `DrawOffset` + `DrawSize` is **where the sprite
paints**. They are separate fields because a painted town needs silhouettes that overlap — a tower
behind a roof, a banner past a wall — while UI Toolkit's hit-testing stays stubbornly rectangular.

`HubView` renders them as three layers: backdrop, then a **sprite layer** on draw rects, then the
**buttons** on hit rects. A button with art behind it goes transparent and drops its glyph — the
sprite is doing the identifying. `HubContentTests.NoTwoLots_HitBoxesOverlap` polices the hit boxes
and says nothing about the art, which is free to overlap as much as it likes.

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

#### What a level *does* is undecided — and deliberately not guessed

The upgrade machinery is complete and tested, but **every lot ships `MaxLevel 1`**, so nothing is on
sale that buys nothing. `HubState.LevelOf(service)` is the seam a screen reads when that changes —
the merchant would size its stock off it, the forge its discount. Turning one on is two authored
fields (`MaxLevel`, `GoldPerUpgrade`) plus whatever the screen does with the number.
`HubContentTests.NoLot_OffersAFreeUpgrade` fails on a level with no price.

#### The opening sequence (provisional)

Priced against the measured yields in `docs/plans/HUB.md` §7 — Scrap Iron is an order of magnitude
the most plentiful (≈31.7 a campaign), which makes it the right currency for a cheap frequent cost
and the wrong one for a gate.

| lot | offered | costs |
|---|---|---|
| Campfire | placed by default | — |
| Storehouse | placed by default | — (your own bag, never gated) |
| Sphere Hall | from the start | 1 Rotted Timber |
| Bestiary | from the start | 4 Scrap Iron |
| Merchant | from the start | 8 Scrap Iron · 2 Rotted Timber |
| Magic Forge | after `TutorialRun` | 3 Ember Iron · 2 Slag Coal |

The Sphere Hall is deliberately the cheapest thing in town and is **not** gated on a run: the grid
is where banked XP goes, and making a player finish a run before they can spend any of what they
earned reads as a lock rather than as pacing. Note Rotted Timber is **cache-only** (§7 phase 1's
table: ~3.1 a campaign, none from kills), so the one timber is an errand — find a treasure room —
rather than something a few fights hand you.

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
campfire opens `PartySelectUI`, which is also where the next party slot is bought —
`docs/plans/HUB.md` §7 open question 3. The storehouse is free for a different reason: it is the
player's **own bag**, not a service someone provides, and gating it would mean the loot from run one
cannot be equipped (§7 open question 6).

### Art

`Assets/Sprites/Hub/` holds a 320×180 backdrop — exactly ¼ of `ReferenceSize`, so a lot position
maps to a whole backdrop pixel — plus one sprite per lot and a shared `lot_foundation` for the
Available state. All of it is **placeholder**, regenerated by `tools/hub-art/`; read that folder's
README before running anything there, especially the note about `.meta` GUIDs, which orphan every
sprite reference if they are rewritten (that happened once during authoring, and the symptom is a
town silently rendering flat slabs again).

`AbsentSprite` is left **null** on purpose: a locked lot falls back to the flat slab and its glyph,
which reads as "something is coming here". `HubView` degrades to that slab wherever a sprite is
missing, so the town stays playable with no art at all. The `hub-*` classes live at the end of
`Assets/UI/Theme/CardDungeon.uss` — but **before** `.cd-nav--selected`, which must stay last.

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

`HubUISetup` wires `_document`, `_runDefinition` and `_partyRoster`, and additionally guarantees the
EventSystem, a camera and the three prefab instances. `MainMenuUISetup` now wires **only**
`_document` — the run definition and the roster left with the screens that needed them, which is the
split working.

**The hit-test trap** is the most load-bearing UI rule here: a `cd-dock-center` window that *changes
size* leaves UITK's input hit-testing on the old transform, offsetting every click. That is why
`bestiary-view`, `inventory-view`, `grid-view` and `campaign-view` are `cd-window--fixed`, and why
the town is one fixed canvas.

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
  own `cm-node--*` state classes.
- **PartySelectUI** (the campfire) — which owned heroes actually march out. Writes
  `PartySaveData.SelectedHeroKeys` through `HeroRoster.SetSelectedKeys`; `DungeonManager.FieldedHeroes()`
  reads it. Two lists with a Field/Bench button per row, a minimum of one hero, and the cap from
  `MetaProgressManager.GetPartyCap()`. It also **sells the next party slot for Gold**
  (`TryBuyPartySlot`, `PartySlots`) — the price of going wider sits next to the reason not to, since
  the screen states the XP share as the standing cost of width. Reachable from the campfire and from
  the run-progress screen next to *Enter Dungeon*; `_partyOpenedFromProgress` sends Back where the
  player came from.
- **MerchantUI** — the Gold sink (gear, and the healing-potion carry cap). See the Progression guide.
- **MagicForgeUI** (`Cards/UI`) — Essence sink + collection grid, All Magic / Combos tabs, `?` for
  undiscovered. **Requires a `MagicCatalog` in the scene** or it logs a warning and shows empty.
- **InventoryHubUI** (`Items/UI`) — the between-runs bag: Equipment / Spells / Consumables /
  Materials. Equipment is managed *only* here.
- **BestiaryUI** (`Enemies/UI`) — the enemy knowledge collection.
- **SphereGridUI** (`Heroes/UI`) — the one place XP is ever spent.

## The tavern is gone

Retired 2026-09-05 with this refactor (`NEXT_STEPS.md` §5b: *"the tavern is gone; heroes are unlocked
through progression only. Gold never buys a hero again."*). `TavernUI`, `tavern-view`,
`MetaProgressSaveData.TavernStock`, `ShopPricing.RecruitPrice`, `HeroSO.RecruitCost` and
`HeroRoster.RemoveOwned` are all deleted. `HeroRoster.GetRecruitable` became **`GetUnownedHeroes`** —
it is the "not yet unlocked" set an unlock system wants.

**Heroes are obtained by rescue only right now** (`RunLevelEntry.RescueHero`) plus
`PartyRosterSO.StartingHeroes`. The §5b unlock record — clearing a run grants a hero — is still open,
and *"the roster screen needs a home that is not a shop"* is answered: it is the campfire.

One consequence recorded rather than fixed: `BalanceAnalyzer` still models the widest party from the
whole catalog, which the tavern used to justify (a hero was bought with gold exactly as a party slot
is). A hero is a progression *unlock* now — a hard precondition on the frontier rather than a currency
inside it — so that assumption is marked in the code and left for the balance pass, which is paused
until the specialization refactor lands.
