# Menu Improvements — a whole-game UI review *(2026-09-28)*

An outside review of every menu I could reach, judged at the game view's **1280x720** (panel scale
0.8 from the 1600x900 reference, so the 16px floor renders at ~13px). The bar to compare against is
the **redesigned Storehouse inventory** (`cd-inv-window`): a header band carrying the title and tabs,
framed columns, rarity-coloured names, one fixed frame size for every tab, a hint line pinned to the
bottom, and a filled-purple cursor with a gold `▸`. Most of what follows is "this screen is still
on the old `cd-window` recipe": one 620px column centred on a flat void, capped at `max-height: 62%`.

**How it was reviewed.** Driven through the Unity MCP: `ScreenCapture` of the game view, plus
`resolvedStyle` / `worldBound` dumps for the numbers. The hub and title screen ran against the
throwaway `savedata_uxreview` fixture; to see the unbuilt services I built all four lots and marked
the tutorial run cleared **inside that fixture only**. The dungeon and combat ran as a normal
`MainGameScene` boot against the same fixture (the sandbox launch did not take; see the report).
Screenshots are in the session scratchpad, `…/scratchpad/menureview/` — filenames are cited per
finding.

**Not reached — judged from UXML/USS/controller source only:** run-complete (`complete-view`),
the defeat screen (`ShowDeathScreen`), the refuge confirm and result (`ShowConfirm("A Refuge")`),
the captive-joins dialog, the boss and summon banners, the multi-target target picker (`target-panel`
— a single enemy is auto-targeted), and the Forge/Campfire upgrade confirmation (never affordable in
the fixture).

**Already on the roadmap, not re-listed:** painted backdrops for the title, level-entry and story
map (§21), un-animated enemies (§21), and the death screen's missing run summary (§15). Where
materials come from stays a mystery on purpose; nothing here suggests a source hint. (The placeholder
item icons this review saw were replaced the same day: every item now has its own icon.)

---

## Cross-cutting (fix once, every screen benefits)

- [ ] **`.cd-window { max-height: 62% }` crushes list screens into one-row scrollers** (severity: high)
  — the Campfire and the Merchant stack two or three `cd-shop-list` ScrollViews, a subtitle each and a
  block of buttons in one column. At 62% of 900 the window caps at 558px, so flexbox shrinks the
  ScrollViews: Merchant's *Wares* (4 rows) and *Sell Gear* (10 rows) render **70px tall — one row each,
  with a scrollbar** (`25_merchant.png`); Campfire's *Marching out* is 54px for three heroes and shows
  only the Warrior (`15_campfire_party.png`). Fix per screen below (move both to the `cd-inv-window`
  frame); as a stopgap give `.cd-shop-list` `flex-shrink: 0; min-height: 120px` and let the window
  grow to `max-height: 88%` like `.cd-window--tall`.
- [ ] **Every hub service opens on a flat void, the town disappears** (severity: high) —
  `HubManager.ShowLotPanel` / every `OnVisit*` does `SetShown(_hubView, false)`, so the lot panel,
  Merchant, Forge etc. float on `.cd-bg` (`13_lot_merchant.png`, `25_merchant.png`). The town is the
  best-looking thing in the game and the player loses it the moment they click it. Keep `hub-view`
  shown and put a scrim between: a `hub-scrim` element (absolute, full-bleed, `background-color:
  rgba(6,3,14,0.72)`) shown with any service. It also gives the lot panel's *Build* the sprite swap
  it was built to animate (the UXML comment says so — today the swap happens behind the panel).
- [ ] **Window size jumps between states and tabs** (severity: medium) — the recipe the inventory
  fixed (`cd-window--fixed` + px regions) has not reached: the Forge is 445px tall on Abilities, 355px on
  Combos and 310px on an inspected ability (`21_forge.png`, `23_forge_combos.png`, `22_forge_inspect.png`);
  the lot panel is 250px unbuilt, 335px built (`13_…`, `14_campfire_lot.png`). Besides looking loose,
  a re-centred `cd-dock-center` window is the hit-testing trap the Hub guide warns about.
- [ ] **Legacy brown backgrounds survive in a purple theme** (severity: low) —
  `rgba(41, 31, 20, 0.35)` on `.cd-list`, `.cd-grid-scroll`, `.cd-bestiary__list/__detail`,
  `.cd-scan__body` and `rgba(41,31,20,0.22)` on `.cd-effect-row` (parchment-era tokens). They read as
  muddy grey-brown boxes (`21_forge.png`, `26_bestiary.png`, `47_inspect_step.png`). Replace with a
  `--cd-well: rgba(0,0,0,0.30)` token plus the inventory column border (`.cd-inv-col`).
- [ ] **Two different "selected" looks, and a permanent gold border that reads as one** (severity: medium)
  — the title/hub cursor (`.cd-nav--selected`) is a filled purple button with **gold text on
  `rgb(102,48,158)`**, which is the lowest-contrast text in the game (`02_title_cursor.png`); art lots get
  a gold outline instead; and `.hub-road` is *always* drawn with a 3px gold border, so "The Story"
  looks selected at all times (`11_hub_cursor.png`, `12_hub_cursor_merchant.png`). Pick the inventory's
  cursor (purple fill, **white** text, gold `▸`) for buttons, gold outline for world objects, and give
  `.hub-road` the normal `--cd-frame` border until the cursor is on it.
- [ ] **A disabled action never says why** (severity: medium) — Upgrade — 300 gold (Campfire), Upgrade (Forge),
  Activate (Sphere Grid), greyed Ability (combat command menu), Buy on a 75g item: each is dimmed with
  no "you have 50" or "no charges left". Standardise a `cd-reason` label under / beside the button
  (`lot-feedback`, `grid-detail-cost`, the command row's meta slot) that turns red with the shortfall:
  *"Need 300 gold — you have 50"*, *"Need 65 XP — 30 banked"*, *"No ability carried"*.
- [ ] **Hint lines lie by omission** (severity: low) — the Sphere Grid says *"Drag pan · Scroll zoom ·
  Click node · Q/E hero · Esc back"* and the Story map *"… Click a run · Esc back"*, but both are fully
  arrow + Enter driven (`SphereGridUI.OnKeyDown`). Lead with the keyboard, as the inventory does:
  *"←↑↓→ node · Enter activate · Q/E hero · Esc back · drag/scroll to pan"*.

## Title screen and Options (`MainMenu.uxml`)

- [ ] **No cursor until the first key press** (severity: low) — `01_title.png` shows three identical buttons;
  the cursor only appears after an arrow (`02_title_cursor.png`). Pre-select `continue-btn` in
  `MainMenuManager` on show so Enter works and the screen has a focal point.
- [ ] **Options rows float apart** (severity: low) — labels hug the left edge and the − value + cluster
  hugs the right with ~200px of nothing between (`03_options.png`); values are bare numbers. Give
  `.cd-option-row` a fixed-width label column (200px) and put a 10-segment bar between − and +
  (`cd-option-bar`, a row of 10 small `VisualElement`s filled by `AudioOptionsUI`), so the level reads
  without parsing "70%".
- [ ] **"Audio" subtitle is the same size as body copy** (severity: low) — `cd-subtitle` at 16px bold vs
  18px row labels reads as a smaller, lesser label. Use the inventory's `cd-inv-col__title` treatment
  (accent colour + underline rule) for section heads here and in the pause window.
- [ ] **"Sound: On" is the loudest thing on the screen** (severity: low) — a full-width
  `cd-menu-button` for a toggle outweighs the three dials. Make it a fourth `cd-option-row`
  ("Mute  [ Off ]") so the rows align and the button hierarchy is Back > everything else.

## Hub town (`hub-view`, `HubView`)

- [ ] **Forge caption collides with the Bestiary roof** (severity: medium) — once the Forge is built, its
  two-line caption ("Level 1 / upgrade 250g") hangs over the Bestiary sprite below it
  (`24_hub_built.png`, y≈370–405). `CaptionRoom` only checks the town's bottom edge. Either check the
  caption rect against every lot below (flip to `--above` or `--right` on overlap) or collapse built
  lots' captions to one line ("Lv 1 · 250g").
- [ ] **Unaffordable prices are painted the same gold as affordable ones** (severity: medium) —
  "upgrade 250g" / "upgrade 300g" are gold while the player has 50–118 gold (`24_hub_built.png`,
  `58_back_in_hub.png`), so the town advertises actions the lot panel then refuses. In `SetLotNote`, add
  `hub-lot__note--short` (dim, `opacity: .6`) when the cost is not met, keep gold for "you can do this now".
- [ ] **Locked lot says "Locked"** (severity: low) — `11_hub_cursor.png`, Ability Forge: the Hub guide's own
  rule is that a locked lot names the run in its way. The caption should read *"After The Threshold"*;
  "Locked" in grey also sits at the lowest contrast in the town.
- [ ] **No "something new here" signal on lots** (severity: medium) — after a floor the Sphere Hall has XP to
  spend and the Bestiary new entries, but the town looks identical (`58_back_in_hub.png`). Add a small
  gold pip (`hub-lot__badge`) on a lot when its service has something actionable (unspent XP on any
  fielded hero, unseen bestiary entry, affordable upgrade). This is information, not hand-holding.
- [ ] **The road button is an empty card** (severity: low) — `.hub-road` is a 150x190 box with two words in
  it, detached from the painted road it stands for (`10_hub.png`). Drop the box: a label + `▶` sitting *on*
  the road's end (transparent background, gold text with the `hub-lot__caption` chip), cursor draws the
  gold outline around the road's end. If it stays a card, show the active run in it
  (*"The Drowned March · Level 2 of 5"*) so it earns its size.
- [ ] **Bottom bar is thin and disconnected** (severity: low) — Main Menu and "Gold: 50  Essence: 5" sit
  in an unframed strip under the town frame (`10_hub.png`). Use the inventory header's currency chip style
  (gold value, small caps label) and put the bar *inside* the town frame's bottom edge.

## Lot panel (`lot-view`)

- [ ] **The status line repeats the button** (severity: medium) — unbuilt: "Needs 8 Scrap Iron · 2 Rotted
  Timber" and then "Build — 8 Scrap Iron · 2 Rotted Timber" (`13_lot_merchant.png`). Replace `lot-status`
  with a cost table — one row per material with **have / need** (`11 / 8` in white, `1 / 2` in red) and an
  icon — and shorten the button to "Build".
- [ ] **Mixed alignment** (severity: low) — title and blurb are centred, `lot-status` is left-aligned bold,
  `lot-grant` centred gold (`14_campfire_lot.png`). Left-align the body under a centred title or centre
  everything; label the grant ("Next level:") instead of relying on gold alone.
- [ ] **Enter is below Upgrade** (severity: medium) — for a built lot the common action is Enter, but it
  sits second, under a disabled Upgrade (`14_campfire_lot.png`, `19b_after_build.png`). Order: Enter
  (default cursor), Upgrade, Back. Upgrade's label should match the town caption ("300g" vs "300 gold").
- [ ] **No picture of the thing being built** (severity: low) — the panel is text only; show the lot's
  sprite (or its `AbsentSprite` silhouette) at 2x in a framed tile like `cd-inv-detail__icon`.

## Campfire / party select (`party-view`, `PartySelectUI`)

> `PartySelectUI.cs` is modified in the working tree, so some of this may be mid-edit — but the
> screenshot is the current state.

- [ ] **The screen is broken at 1280x720** (severity: high) — `15_campfire_party.png`: `party-share`
  (italic "Each hero earns 33%…") does not wrap and runs **~250px outside the window's right edge**;
  the fielded list is 54px for three heroes so only the Warrior shows; the *Bench* button overlaps the
  Warrior's stat line (which is cut mid-number: "SPR 3 · … 3"); the bench list's buttons are clipped
  in half; "How the run's XP is shared" sits on top of the bench row. Immediate fixes: `.cd-shop-empty
  { white-space: normal; }`, and the list/window height issue above.
- [ ] **Rebuild it on the inventory frame** (severity: high) — this is the screen that decides who goes into
  the dungeon and it is a text column. Proposal: `cd-inv-window` with the header band ("Campfire" + Back),
  **four seat tiles** across the top (portrait, name, HP, Lv; empty seat dashed), the roster as a
  portrait list in the middle column (fielded rows marked with the existing left accent bar), and a
  detail column for the selected hero (stats grid as in the inventory, carried abilities). The XP split
  becomes a row of three `cd-inv-tab`-style toggles in the bottom band with its one-line description.
- [ ] **Stats as a run-on sentence** (severity: medium) — "Warrior (leads) STR 15 · END 11 · AGI 5 · INT 3 ·
  SPR 3 · …" is unscannable and duplicates the inventory's stat grid. Show HP + two role stats on the
  row; the full grid lives in the detail column.
- [ ] **XP split buttons: the chosen mode is a gold outline only** (severity: low) — "Even" has a gold border,
  locked modes read "Mentor · Lv 2" in the same grey as the chosen one. Chosen = filled
  `cd-inv-tab--active`; locked = lock glyph + dim.

## Merchant (`merchant-view`, `MerchantUI`)

- [ ] **Wares and Sell Gear show one row each** (severity: high) — see cross-cutting;
  `25_merchant.png`: 4 wares and 10 sellable items live in two 70px scrollers.
- [ ] **No way to judge an item before buying** (severity: high) — rows are "Simple Sword (Common)" +
  price. No stats, no comparison with what the heroes wear, rarity as a parenthetical instead of colour.
  Move to the inventory frame: tabs **Buy / Sell / Potions** in the header, the item list in the middle
  column with rarity-coloured names (`ItemPresenter` already has the classes), the detail column
  reusing the inventory's "Grants" + delta-vs-equipped block, and Buy/Sell as the detail column's action.
- [ ] **"Enlarge Potion Belt (2 → 3) — 50 gold" is the biggest button** (severity: low) — a rare upgrade sits
  above the shop as a full-width primary. Put it in the Potions tab (or the detail column of the belt)
  and let the wares be the first thing seen.
- [ ] **Essence shown in a shop that never takes it** (severity: low) — `merchant-essence`. Show only the
  currency the screen spends, as the inventory's materials tab does with gold.

## Ability Forge (`forge-view`, `MagicForgeUI`)

- [ ] **A wall of "?"** (severity: high) — `21_forge.png`: 31 identical `?` tiles and one known icon; no
  names, no levels, no cursor, so the only readable fact is "you know one thing". Show the grid as a
  list/grid of **known** abilities first (icon, name, `Lv 0/1` badge on the tile corner), then undiscovered
  ones collapsed into a single "27 undiscovered" row or dim silhouettes at the end. The `?` tiles
  cost the screen its information density and give no mystery that a count does not.
- [ ] **Inspect replaces the grid** (severity: medium) — selecting a tile swaps the grid for
  `forge-inspect` and shrinks the window (`22_forge_inspect.png`); the player loses their place and the
  tabs. Use the inventory's three columns: grid/list left, detail right (always present, "Select an
  ability" when empty), Upgrade in the detail footer.
- [ ] **Effect rows are debug text and disagree with the Storehouse** (severity: high) — the Forge says
  **"Damage 3"** and **"-Bleeding 1 (3t)"**; the Storehouse and the combat picker say **"18 damage
  (Attack 15)"** and **"Bleed 1/turn, 3 turns"** for the same Slash (`22_forge_inspect.png` vs
  `17_inventory_abilities.png`, `61_ability_picker.png`). The leading hyphen, the "(3t)" and the raw base
  number read like a `ToString()`. Route the Forge's `cd-effect-row` text through the same describer the
  inventory detail uses, and show upgrade previews as *"18 → 22 damage"*.
- [ ] **Back and Upgrade are mismatched** (severity: medium) — `inspect-back` is a 200px `cd-button--narrow`,
  `inspect-upgrade` a smaller `cd-cmd` that sits 3px higher (`22_forge_inspect.png`). Same height, same
  class, Upgrade on the right as the primary.
- [ ] **Combos tab centres its tiles, Abilities left-aligns them** (severity: low) — `23_forge_combos.png`
  vs `21_forge.png` (`justify-content: center` on `.cd-grid-scroll` content with a partial last row).
  Use `flex-start` and a fixed column count.

## Bestiary (`bestiary-view`, `BestiaryUI`)

- [ ] **A narrow window with a scrolling detail column** (severity: medium) — at 620px the detail column is
  ~270px wide and has to scroll to reach Resistances (`27_bestiary_entry.png`), while half the screen is
  empty. Move to `cd-inv-window`: list left, **portrait** + name + HP band at the top of the detail (the
  in-combat Inspect page already has `cd-scan__portrait`), Resistances and Abilities as two columns
  below — no scroll for a normal entry.
- [ ] **Nothing is selected on open** (severity: low) — "Select an enemy." with a live list beside it
  (`26_bestiary.png`). `Show()` sets `_selected = -1`; select the first discovered entry.
- [ ] **Undiscovered rows are 4/5 of the list** (severity: low) — eleven "? ? ?" rows each as tall as a
  real one. Keep the count ("2 of 13 discovered" is good) and render unknowns as a compact 24px row or
  group them at the bottom.
- [ ] **Element names are not in element colours** (severity: low) — "Attacks with Ice" is pale blue
  here and "Attacks with **Fire**" is the same pale blue on the Inspect page (`47_inspect_step.png`).
  Add `cd-element--fire/ice/lightning/holy/shadow` classes and use them in both presenters.

## Sphere Grid (`grid-view`, `SphereGridUI`)

- [ ] **Detail text overflows its panel** (severity: high) — `29_sphere_grid_node.png`: the node title
  "Learns Slash — 2 charges per run" runs ~45px past `sg-detail`'s right border and "Known from the
  start — costs nothing" spills past its left border. `grid-detail-name`/`-cost` need
  `white-space: normal` (the `cd-inspect__name` class does not wrap).
- [ ] **Name, kind and payload say the same thing** (severity: medium) — "+10 HP / Stat / +10 HP"
  (`30_sphere_grid_node2.png`), "Learns Slash — 2 charges per run / Known ability / Learns Slash — 2
  charges per run". Title = the thing ("+10 HP", "Slash"), kind = a coloured chip ("Stat", "Ability"),
  payload = what it changes ("HP 38 → 48", the ability's effect lines).
- [ ] **Nodes are 8px dots and the cursor is a hairline** (severity: high) — at 1280x720 you cannot tell a
  stat node from an ability node, or where the cursor is (a 1px white ring, `30_…` at x≈491). Nodes need
  a glyph per kind (stat letter, ability icon), ~24px at default zoom, and the cursor needs the
  inventory's treatment: filled accent + gold ring. Unlocked / affordable / locked should be three
  clearly different fills.
- [ ] **The graph gets a third of the window** (severity: medium) — the pannable area is ~630x210px while
  ~45px of dead band sits between the hint and Back (`28_sphere_grid.png`); the top branches are clipped at
  the viewport edge. Put Back in a header band (inventory style) and let `grid-graph` take the freed
  height; start zoomed to fit the unlocked frontier.
- [ ] **Hero switcher is plain tabs** (severity: low) — the inventory's portrait strip (`cd-inv-heroes`)
  is the house hero picker; reuse it here, with the banked XP under each name so the player sees which
  hero has points to spend without Q/E-ing through them.

## Story map (`campaign-view`, `CampaignMapUI`)

- [ ] **Map crammed into the left half of its canvas** (severity: medium) — `31_campaign.png`: the five
  nodes occupy x≈330–715 of a 200–830 canvas; the right third is empty. Fit-to-bounds on open (the
  floor map already does this), with padding.
- [ ] **"In progress · 5 levels" instead of where you are** (severity: medium) — `59_campaign_active.png`.
  The status line should say *"Level 2 of 5 · Silt Shallows next"*; for an open run *"5 levels"* is fine.
- [ ] **No legend for ✓ ★ ? 🔒** (severity: low) — "?" reads as "unknown" but those runs are open and
  startable. Add a one-line legend like the floor map's `dm-legend`, or use distinct shapes (open = ring,
  optional = dashed ring).
- [ ] **Detail panel has room it does not use** (severity: low) — a blurb and a button in a 300x280 panel.
  Add what the choice costs: party size limit / recommended level, and what a clear unlocks
  (the node's children), since the map is the run-selection screen.

## Run progress / level entry (`progress-view`)

- [ ] **A 490x260 box for the "you are about to go in" moment** (severity: medium) — `32_run_progress.png`.
  Party is a text list "Party (3): Warrior, Paladin, Rogue". Show the party as portrait tiles with HP, a
  **5-pip level strip** for "Level 1 of 5" (filled / current / ahead), and make *Enter Dungeon* the
  visibly primary button (default cursor, accent fill). Backdrop itself is §21.
- [ ] **Back and Enter Dungeon are unequal** (severity: low) — flex-grow on content-sized buttons gives
  186 vs 266px. Set both to `width: 50%` (minus the gap), or Enter full width and Back as `cd-button--narrow`
  underneath.

## Dungeon HUD and room bar (`dungeon-hud`, `main-bar`, `combat-bar`, `nav-hint`)

- [ ] **The keyboard hint drifts off-centre** (severity: medium) — after the first Fight bar, `nav-hint`
  ("← → choose · Enter confirm") sits ~85px left of the bar it belongs to (`44_fight_bar.png` is centred,
  `49_pause.png` and `51_event_room.png` are not; measured x 592–798 ref vs bar centre 800).
  `.cd-nav-hint` centres with `left: 50%; translate: -50% 0`, and the percentage translate is not
  recomputed when the text changes width. Make it `left: 0; right: 0; -unity-text-align: middle-center`
  and drop the translate.
- [ ] **The HUD covers the map** (severity: medium) — `44_fight_bar.png`: the top-left HUD sits on top of
  the room above (and a door behind it). It also grows and shrinks with its text (`55_exit_room.png` is
  wider). Give `.cd-hud` a fixed width, and have the room camera keep a top-left safe area, or collapse
  the HUD to one line while walking ("Collapsed Caverns · 43g · 1 way left · M").
- [ ] **"(banked when you take the stairs)" on every frame** (severity: low) — teaching text that never
  goes away. Show it for the first floor only, or move it into the level-clear breakdown where it is
  already explained.
- [ ] **Room bar verbs are generic** (severity: medium) — a lone "Action" in an event room
  (`52_event_bar.png`) and "Search" in front of a visible chest (`42_cache_room.png`). Label the button
  with the thing: the event's title ("The Treasury"), "Open the cache", "Rest at the refuge". The
  single button in a framed box also looks like a dialog stub; drop the `cd-bar` frame for a one-button
  bar.
- [ ] **Fight / Flee says nothing about the fight** (severity: medium) — `44_fight_bar.png`: the only
  sign of the enemy is a 16px sprite in the room's corner. Add a line above the bar: *"Drakeling"* /
  *"Stone Sentinel + Floating Eye"* (names from the bestiary, "???" if unseen — no stats), so the
  decision the bar asks for is informed.
- [ ] **Party panel is a list of numbers** (severity: low) — "Warrior HP 38/38" in 60px-tall rows with no
  bar (`40_dungeon_start.png`). The inventory already draws a hero portrait; add a portrait and an HP
  bar per row, tighten rows to ~40px, and show level afflictions (below).
- [ ] **Level afflictions are invisible after the dialog** (severity: medium) — the Treasury event gave
  "Rogue: Strength -2 for the rest of the level" (`54_event_result.png`) and nothing on screen remembers
  it. Show a small debuff glyph on the Rogue's party row (tooltip / inspect text "STR -2 until the
  stairs").
- [ ] **Party panel lags the event it reports** (severity: low) — the result says "Rogue takes 3 damage"
  while the panel still shows 18/21 (`54_event_result.png`); it updates only after OK (`55_exit_room.png`
  15/21). Call `RefreshPartyStatus()` when the outcome is applied, not when the dialog closes.

## Room dialogs (`event-window`, `detail-window`)

- [ ] **Event choices don't say which ones are gambles** (severity: medium) — `53_event_window.png`: the
  odds line is prose above the list ("turns on Luck — Rogue has the best of it — an even bet") and the
  three options look identical, although "Take nothing and move on" is safe. Put a right-aligned tag
  on each row (`cd-row__meta`: "Luck · even", "safe"), and colour the odds word by band.
- [ ] **Results don't say whether the check passed** (severity: low) — `54_event_result.png` is flavour text
  then consequences. Add a one-word outcome header chip ("Failed" in red / "Success" in green) above
  the message.
- [ ] **Loot as plain text lines** (severity: low) — the cache (`43_cache_dialog.png`) lists "+15 gold. Found:
  Leather Cap. Salvaged: Cut Stone x3." as sentences. Reuse the victory window's reward rows (icon,
  rarity-coloured name, count right-aligned).
- [ ] **"The Way Down" warns about unfound things when there are none** (severity: low) —
  `56_descend.png`: the HUD says "nothing left unopened", the dialog still says "Anything still unfound on
  this level stays here". Only add that line when the map's frontier/payload count is non-zero, and say
  what ("1 room not entered").
- [ ] **Dialogs have no scrim** (severity: low) — every `cd-dock-center` dialog sits on the live room with
  full-brightness tiles around it (`43_…`, `53_…`, `56_…`). A `rgba(6,3,14,0.55)` scrim behind
  `event-window` / `detail-window` / `map-window` / `pause-window` separates "the game is waiting for you"
  from "you are walking".

## Pause overlay (`pause-window`)

- [ ] **Two cursors on screen** (severity: medium) — pausing in a fight room leaves the Fight/Flee bar and
  its highlighted *Fight* button visible beneath the pause window (`49_pause.png`). Hide `combat-bar`,
  `main-bar` and `nav-hint` while paused (and restore on Resume), or at least drop their
  `cd-nav--selected`.
- [ ] **Resume is third** (severity: medium) — order is audio dials, Sound, Map, **Resume**, Leave. Put
  Resume first with the default cursor, then Map, then the audio block under a subtitle, then Leave
  last (it is already two-press).
- [ ] **Same Options layout issues as the title** (severity: low) — shared rows; the fixes above apply.

## Floor map (`map-window`)

- [ ] **Legend glyphs are tiny and "you are here" is not in it** (severity: low) — `41_floor_map.png`: `◆
  cache` is a 6px diamond; the gold-outlined current room and the empty-outline "not entered" rooms are
  explained only in the hint line. Add both to `dm-legend` as swatches and bump the legend glyphs to
  20px (the `dm-glyph` exemption is for the room boxes, not the legend).
- [ ] **Status line duplicates the HUD** (severity: low) — "1 room explored · 2 ways not yet taken" appears
  in the HUD directly above-left. Use the map's line for something the HUD does not say (exit found / not
  found, caches left).

## Combat

- [ ] **HP bars sit between heroes and the top one is clipped** (severity: high) — each hero has a bar above
  the head and one under the feet; with the column spacing, Paladin's head bar sits right under the
  Warrior's feet, so the eye assigns every bar to the wrong hero, and the Warrior's head bar is cut by the
  top of the screen at y≈0–15 (`45_combat_menu.png`, `61_ability_picker.png`; measured bar anchors at
  y 110 / 266 / 422 for three heroes). One bar per unit, directly under the sprite, and push the hero
  column down (or the camera up) so nothing is within 24px of the top edge.
- [ ] **Enemy HP shown twice** (severity: low) — Drakeling has a bar above and a "Drakeling 18/18"
  nameplate below. Keep the nameplate with a thin bar inside it; drop the floating bar for enemies.
- [ ] **The ability picker covers the acting hero** (severity: medium) — `61_ability_picker.png`: the picker
  (desc + an "(empty)" row + Back) is taller than the command menu and cuts the Rogue's sprite off at the
  shoulders; `.cd-window--picker`'s own comment names this risk. Hide "(empty)" slots, put the description
  to the right of the list instead of under it, and cap the picker at the command window's height.
- [ ] **Command menu: wide box, short words, no reasons** (severity: low) — `45_combat_menu.png`: a 445px
  window for "Attack / Ability / Item / Inspect / Skip" with *Ability* greyed and no explanation. Narrow it
  to ~300px, and put the charge count on the row ("Ability 2/2") or the reason ("Ability — none
  carried") in the row's meta slot.
- [ ] **Turn highlight lags into the enemy's turn** (severity: low) — during the Floating Eye's action the
  party panel still highlights *Warrior* and the target arrow stays on the enemy (`62_target_picker.png`,
  turn order shows Floating Eye current). Clear `cd-party-row--active` and the target arrow on
  `OnTurnExecuted` / when an enemy turn starts.
- [ ] **Item picker has no description** (severity: low) — `46_item_picker.png`: Healing Potion vs Greater
  Healing Potion with the same icon and no numbers. Show `list-desc` for items as it does for abilities
  ("Restores 30 HP to one hero").
- [ ] **Inspect: the drops row reads as broken** (severity: low) — `47_inspect_step.png`: "Drops ???" on
  one line and "Scrap Iron" alone underneath, and four separate "???" rows under Abilities. Put known and
  unknown drops on one list ("Scrap Iron · ???"), and collapse unknown abilities to "3 not yet seen".
- [ ] **Inspect covers the enemy being inspected** (severity: low) — the centred page hides the Drakeling
  and most of the stage (`47_…`). Dock it top-centre (below the turn order's top) or to the right half,
  so the portrait you are reading about is still visible.

## Victory and level clear (`victory-window`, `level-clear-window`)

- [ ] **Victory lists Loot above XP and Gold** (severity: low) — `48_victory.png`: the header "Loot" row
  with an item under it, then XP and Gold rows. Order: Gold, XP (per hero, as the level clear does),
  then Loot with rarity colours and the item's icon at 2x. "XP +10" alone does not say whether it is
  each or shared.
- [ ] **Level clear hides the materials below the fold** (severity: medium) — `57_level_clear.png`: Gold (3
  rows), Essence (2 rows) and XP (4 rows) fill the scroller and *Materials* starts at the bottom edge, so
  what the player went down for needs a scroll. Two columns (currencies left, loot + materials right),
  or collapse the Gold breakdown into one line ("+68 · 43 found, 25 clear bonus").
- [ ] **"Spend it at the Forge to upgrade abilities"** (severity: low) — permanent teaching line under Essence;
  fine for the first clear, noise after. Gate it like the HUD's banking note.

## Defeat and run complete (source only)

- [ ] **Defeat is the generic detail dialog with the raw combat log** (severity: medium) —
  `ShowDeathScreen` puts `result.Log` (the turn-by-turn log) into `detail-message` under "Your Party Has
  Fallen..." with an "Ok" button. Give it the victory window's frame (`cd-victory`, red header) with
  what was lost — the floor's un-banked gold and XP, as the level-clear shows what was gained — and a
  "Back to Town" button. The fuller run summary is §15.
- [ ] **Run complete is two fixed lines** (severity: low) — `complete-view`: "Victory! / You have conquered
  the dungeon…" and Return, regardless of which run. Name the run, show what clearing it unlocked on the
  story map, and reuse the `cd-victory` header band.

## Storehouse inventory (the bar) — small notes

- [ ] **Stat losses are not red** (severity: low) — `16_inventory_equipment.png`: in "Fits Main Hand",
  Oak Staff shows "+5 INT" gold and "−4 STR" pale blue; a loss should read as a loss (`--cd-bad`, red).
- [ ] **Hero strip greyed but present on Materials** (severity: low) — `18_inventory_materials.png`: four
  disabled hero tiles take a band that means nothing on that tab. Fine for the fixed frame; draw them at
  `opacity: .35` without borders so they read as "not applicable" rather than "broken".
- [ ] **"Attack 15" vs "STR 15"** (severity: low) — the ability line "18 damage (Attack 15)" names a stat the
  stat grid calls STR. Use the `StatCatalog` label.

---

## Do these first

1. **Fix the Campfire** — `party-share` wraps, lists stop shrinking; then rebuild it on the inventory
   frame with seat tiles (`15_campfire_party.png`). It is the screen before every run and it is broken.
2. **Merchant onto the inventory frame** — full-height Buy/Sell lists, rarity colours, the Storehouse's
   stat/delta detail column (`25_merchant.png`).
3. **Sphere Grid readability** — wrap the detail labels, kind glyphs on 24px nodes, a visible cursor,
   give the graph the window's height (`29_…`, `30_…`).
4. **Combat HP bars** — one bar under each unit, nothing clipped at the top edge (`45_combat_menu.png`).
5. **Keep the town behind hub services** with a scrim, instead of `SetShown(_hubView, false)` (`13_…`).
6. **Forge: list the known abilities, detail beside the grid, one effect describer** — fixes the "?"
   wall, the jumping window and "Damage 3 / -Bleeding 1 (3t)" in one pass (`21_…`, `22_…`).
7. **Say why a button is disabled** — one `cd-reason` pattern for Upgrade / Activate / Buy / Ability.
8. **Recentre `nav-hint` and hide the room bar under pause** — two small bugs that make the dungeon UI
   look unfinished on every screen (`49_pause.png`, `51_event_room.png`).
