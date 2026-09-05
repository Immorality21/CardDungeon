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
- **`BuildingSO`** is pure content: `Key` (save id), `Service`, `Position` + `HitSize`, `DrawOrder`,
  per-state sprites, `PlacedByDefault`, and the phase-4 fields (`PlacementCost`, `RequiredRunKeys`,
  `GoldPerUpgrade`) that are **authored now and read by nothing yet**.
- **Progress lives in the save** — `MetaProgressSaveData.Buildings`, the same split `CampaignSO`
  makes with `CompletedRunKeys`, so one authored town reads differently per save.
- **All rules are in `BuildingOps`** (pure, static, scene-free): `StateOf`, `LevelOf`, `InDrawOrder`,
  `LotRect`, `SpriteFor`, plus authoring validators. `HubPresenter` turns those into classes and
  text; `HubView` only draws. `BuildingOpsTests` and `HubContentTests` drive them with no scene.

### Three constraints the renderer exists to satisfy

- **The town scales as one unit.** Everything is absolutely positioned inside one fixed-size canvas
  that is uniformly scaled and centred (`HubView.Relayout`). Scaling lots individually desyncs the
  art from the hitboxes — UITK applies a transform to hit-testing as well as to paint, which is the
  same trap `cd-window--fixed` exists to avoid.
- **UI Toolkit has no z-index.** Siblings paint in the order they are added, so `BuildingOps.InDrawOrder`
  (DrawOrder, then `Position.y` as a painter's algorithm, then list order) is the *only* thing
  deciding which building is in front. Nothing may re-sort the lots after `SetTown`.
- **Hit-testing is rectangular.** A lot's button is its authored `HitSize`, whatever the sprite looks
  like. Overlapping rects steal each other's clicks and the symptom is a building that looks fine and
  does nothing — `HubContentTests.NoTwoLots_Overlap` is what catches it.

### The phase switch

**`BuildingOps.EverythingIsPlaced` is `true`**, so every lot reads as built at level 1 whatever the
save says, and `PlacementCost` / `RequiredRunKeys` are inert. That is `docs/plans/HUB.md` §7 phases
2–3: the data model and the town renderer land while the game plays exactly as it did, and phase 4
turns the gates on against a hub that already works — migration risk kept apart from design risk.

**Flipping that constant to false is most of phase 4.** Both `StateOf` and `LevelOf` take an explicit
overload with the switch passed in, and `BuildingOpsTests` covers the gated behaviour *now*, so it
does not arrive untested on the day the constant flips.

### The road is not a building

`HubService` has no `Story` member and `road-btn` is authored in `Hub.uxml`, not in `Hub.asset`. The
story is the way *out* of town, not a service the town provides, and **a building must never be able
to lock the player out of running** — `docs/plans/HUB.md` §7 open question 4, guarded by
`HubContentTests.TheStory_IsNotABuilding` alongside
`CampaignAssetTests.Campaign_NeverStrandsASaveWithNothingToPlay`.

There is also **no separate "Continue Run" affordance**: `CampaignMapUI` already renders the active
run as continuable, so the road is one door with two meanings, decided by the save.

### The campfire

The one lot with `PlacedByDefault`, so a fresh profile owns a working hub without the save writing
anything (`BuildingOps.LevelOf` reads a default-placed lot as level 1 with an empty save). It opens
`PartySelectUI`, which is also where the next party slot is bought — `docs/plans/HUB.md` §7 open
question 3.

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
