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

- [x] **`.cd-window { max-height: 62% }` crushes list screens into one-row scrollers** — *done 2026-09-30*: the Campfire and the Merchant moved to the fixed inventory frame. (severity: high)
  — the Campfire and the Merchant stack two or three `cd-shop-list` ScrollViews, a subtitle each and a
  block of buttons in one column. At 62% of 900 the window caps at 558px, so flexbox shrinks the
  ScrollViews: Merchant's *Wares* (4 rows) and *Sell Gear* (10 rows) render **70px tall — one row each,
  with a scrollbar** (`25_merchant.png`); Campfire's *Marching out* is 54px for three heroes and shows
  only the Warrior (`15_campfire_party.png`). Fix per screen below (move both to the `cd-inv-window`
  frame); as a stopgap give `.cd-shop-list` `flex-shrink: 0; min-height: 120px` and let the window
  grow to `max-height: 88%` like `.cd-window--tall`.
- [x] **Every hub service opens on a flat void, the town disappears** — *done 2026-09-30*: `hub-scrim` (50%) over a disabled town, via `HubManager.CoverTown` / `HideTown` / `IsTownActive`. (severity: high) —
  `HubManager.ShowLotPanel` / every `OnVisit*` does `SetShown(_hubView, false)`, so the lot panel,
  Merchant, Forge etc. float on `.cd-bg` (`13_lot_merchant.png`, `25_merchant.png`). The town is the
  best-looking thing in the game and the player loses it the moment they click it. Keep `hub-view`
  shown and put a scrim between: a `hub-scrim` element (absolute, full-bleed, `background-color:
  rgba(6,3,14,0.72)`) shown with any service. It also gives the lot panel's *Build* the sprite swap
  it was built to animate (the UXML comment says so — today the swap happens behind the panel).
- [x] **Window size jumps between states and tabs** — *done 2026-09-30*: every hub screen is now a fixed frame — the Forge, Campfire, Merchant, Sphere Hall and Bestiary share the inventory's 1180×720 `cd-inv-window`, and the lot panel is a fixed 760×420 `hub-lotpanel` built or not. (severity: medium) — the recipe the inventory
  fixed (`cd-window--fixed` + px regions) has not reached: the Forge is 445px tall on Abilities, 355px on
  Combos and 310px on an inspected ability (`21_forge.png`, `23_forge_combos.png`, `22_forge_inspect.png`);
  the lot panel is 250px unbuilt, 335px built (`13_…`, `14_campfire_lot.png`). Besides looking loose,
  a re-centred `cd-dock-center` window is the hit-testing trap the Hub guide warns about.
- [ ] **Legacy brown backgrounds survive in a purple theme** (severity: low) —
  `rgba(41, 31, 20, 0.35)` on `.cd-list`, `.cd-grid-scroll`, `.cd-bestiary__list/__detail`,
  `.cd-scan__body` and `rgba(41,31,20,0.22)` on `.cd-effect-row` (parchment-era tokens). They read as
  muddy grey-brown boxes (`21_forge.png`, `26_bestiary.png`, `47_inspect_step.png`). Replace with a
  `--cd-well: rgba(0,0,0,0.30)` token plus the inventory column border (`.cd-inv-col`).
- [x] **Two different "selected" looks, and a permanent gold border that reads as one** — *done 2026-09-30*: `.cd-nav--selected` is purple fill with **white** text; world objects (lots, the signpost) keep the gold outline; the road is now signpost art with no standing border. (severity: medium)
  — the title/hub cursor (`.cd-nav--selected`) is a filled purple button with **gold text on
  `rgb(102,48,158)`**, which is the lowest-contrast text in the game (`02_title_cursor.png`); art lots get
  a gold outline instead; and `.hub-road` is *always* drawn with a 3px gold border, so "The Story"
  looks selected at all times (`11_hub_cursor.png`, `12_hub_cursor_merchant.png`). Pick the inventory's
  cursor (purple fill, **white** text, gold `▸`) for buttons, gold outline for world objects, and give
  `.hub-road` the normal `--cd-frame` border until the cursor is on it.
- [x] **A disabled action never says why** — *done 2026-09-30*: one `cd-reason` line under the Campfire, Merchant, Sphere Grid and Forge actions, and a reason on greyed combat commands. (severity: medium) — Upgrade — 300 gold (Campfire), Upgrade (Forge),
  Activate (Sphere Grid), greyed Ability (combat command menu), Buy on a 75g item: each is dimmed with
  no "you have 50" or "no charges left". Standardise a `cd-reason` label under / beside the button
  (`lot-feedback`, `grid-detail-cost`, the command row's meta slot) that turns red with the shortfall:
  *"Need 300 gold — you have 50"*, *"Need 65 XP — 30 banked"*, *"No ability carried"*.
- [ ] **Hint lines lie by omission** (severity: low) — the Sphere Grid says *"Drag pan · Scroll zoom ·
  Click node · Q/E hero · Esc back"* and the Story map *"… Click a run · Esc back"*, but both are fully
  arrow + Enter driven (`SphereGridUI.OnKeyDown`). Lead with the keyboard, as the inventory does:
  *"←↑↓→ node · Enter activate · Q/E hero · Esc back · drag/scroll to pan"*.

## Title screen and Options (`MainMenu.uxml`)

- [x] **No cursor until the first key press** — *done 2026-09-30*: Continue is selected on arrival (`MainMenuManager.Update` → `SelectFirst`). (severity: low) — `01_title.png` shows three identical buttons;
  the cursor only appears after an arrow (`02_title_cursor.png`). Pre-select `continue-btn` in
  `MainMenuManager` on show so Enter works and the screen has a focal point.
- [ ] **Options rows float apart** (severity: low) — labels hug the left edge and the − value + cluster
  hugs the right with ~200px of nothing between (`03_options.png`); values are bare numbers. Give
  `.cd-option-row` a fixed-width label column (200px) and put a 10-segment bar between − and +
  (`cd-option-bar`, a row of 10 small `VisualElement`s filled by `AudioOptionsUI`), so the level reads
  without parsing "70%".
- [x] **"Audio" subtitle is the same size as body copy** — *done 2026-09-30*: `cd-section-head` (18px, accent, underline rule) on Options and Pause. (severity: low) — `cd-subtitle` at 16px bold vs
  18px row labels reads as a smaller, lesser label. Use the inventory's `cd-inv-col__title` treatment
  (accent colour + underline rule) for section heads here and in the pause window.
- [ ] **"Sound: On" is the loudest thing on the screen** (severity: low) — a full-width
  `cd-menu-button` for a toggle outweighs the three dials. Make it a fourth `cd-option-row`
  ("Mute  [ Off ]") so the rows align and the button hierarchy is Back > everything else.

## Hub town (`hub-view`, `HubView`)

- [x] **Forge caption collides with the Bestiary roof** — *moot*: captions are one line (name + badge) since 2026-09-28, and the new art (2026-09-30) clears it. (severity: medium) — once the Forge is built, its
  two-line caption ("Level 1 / upgrade 250g") hangs over the Bestiary sprite below it
  (`24_hub_built.png`, y≈370–405). `CaptionRoom` only checks the town's bottom edge. Either check the
  caption rect against every lot below (flip to `--above` or `--right` on overlap) or collapse built
  lots' captions to one line ("Lv 1 · 250g").
- [x] **Unaffordable prices are painted the same gold as affordable ones** — *moot since 2026-09-28*: the town prints no prices, only a hammer/lock badge; the lot panel's cost table is red when short. (severity: medium) —
  "upgrade 250g" / "upgrade 300g" are gold while the player has 50–118 gold (`24_hub_built.png`,
  `58_back_in_hub.png`), so the town advertises actions the lot panel then refuses. In `SetLotNote`, add
  `hub-lot__note--short` (dim, `opacity: .6`) when the cost is not met, keep gold for "you can do this now".
- [x] **Locked lot says "Locked"** — *done*: the town shows a padlock badge, and the lot panel names the run in the way (*"Clear The Threshold first."*). (severity: low) — `11_hub_cursor.png`, Ability Forge: the Hub guide's own
  rule is that a locked lot names the run in its way. The caption should read *"After The Threshold"*;
  "Locked" in grey also sits at the lowest contrast in the town.
- [x] **No "something new here" signal on lots** — *done 2026-09-30*: a gold pip (`hub-lot__badge--new`, `HubManager.BadgeWithNews`) on a *built* lot when the Sphere Hall has a node some owned hero can buy now, the Forge an upgrade the purse covers, or the Bestiary more enemies met than when it was last opened (`MetaProgressSaveData.BestiaryViewedCount`, written by `BestiaryUI.Show`). It takes the place of the hammer. (severity: medium) — after a floor the Sphere Hall has XP to
  spend and the Bestiary new entries, but the town looks identical (`58_back_in_hub.png`). Add a small
  gold pip (`hub-lot__badge`) on a lot when its service has something actionable (unspent XP on any
  fielded hero, unseen bestiary entry, affordable upgrade). This is information, not hand-holding.
- [x] **The road button is an empty card** — *done 2026-09-30*: it is a signpost sprite beside the road (`.hub-road--art`). (severity: low) — `.hub-road` is a 150x190 box with two words in
  it, detached from the painted road it stands for (`10_hub.png`). Drop the box: a label + `▶` sitting *on*
  the road's end (transparent background, gold text with the `hub-lot__caption` chip), cursor draws the
  gold outline around the road's end. If it stays a card, show the active run in it
  (*"The Drowned March · Level 2 of 5"*) so it earns its size.
- [ ] **Bottom bar is thin and disconnected** (severity: low) — Main Menu and "Gold: 50  Essence: 5" sit
  in an unframed strip under the town frame (`10_hub.png`). Use the inventory header's currency chip style
  (gold value, small caps label) and put the bar *inside* the town frame's bottom edge.

## Lot panel (`lot-view`)

> **Done 2026-09-30** — one fixed px size (`.hub-lotpanel`, 760×420) in every state; header band with
> the name and a state line (*"Level 1 of 3"*, *"Not built yet"*, *"Locked"*); the building's sprite in a
> framed tile (an unbuilt lot shows what it will become, at 50%); a **have / need** cost table
> (`11 / 8`, red when short, material icons); the grant labelled (*"Once built"* / *"Level 2"*); buttons
> **Enter, Build / Upgrade to Lv N, Back** in that order with a `cd-reason` line (*"Need 179 more gold."*).

- [x] **The status line repeats the button** — cost table; the button says only what it does.
- [x] **Mixed alignment** — left-aligned body under a header band; the grant has a label.
- [x] **Enter is below Upgrade** — Enter first; the first arrow key lands on it.
- [x] **No picture of the thing being built** — see above.

## Campfire / party select (`party-view`, `PartySelectUI`)

> **Done 2026-09-30** — rebuilt on the inventory frame (`cd-inv-window` + `cd-camp-*`): four seat
> tiles (leader tabbed), the roster as rows with a gold accent bar for the marching, a detail column
> (stats grid, XP to spend, carried abilities) with **March out / Stay behind** and a new **Lead the
> party** button, and the XP split as stacked tabs in the right column. A row only selects; acting is
> the detail column's, and a dimmed action says why underneath it. Verified in play mode at 1280x720
> (screenshot + synthetic arrow/Enter/Esc through the hub navigator).

- [x] **The screen is broken at 1280x720** — fixed by the rebuild; no ScrollView is flex-shrunk any more.
- [x] **Rebuild it on the inventory frame** — as proposed, with the XP split in the right column rather
  than the bottom band (three stacked tabs fit there without truncating the locked labels).
- [x] **Stats as a run-on sentence** — rows show HP + the two highest stats; the full grid is in the detail.
- [x] **XP split buttons** — chosen = filled with a gold border and a ✓; locked = dimmed and names the
  Campfire level. The chosen mode stays enabled so the keyboard cursor can land on it.

## Merchant (`merchant-view`, `MerchantUI`)

> **Done 2026-09-30** — on the inventory frame: **Buy / Sell / Potion Belt** tabs in the header, a hero
> strip that picks who gear is compared against (defaults to the leader; muted on the belt tab), the
> list with rarity-coloured names and the price on the right (red when out of reach), and a detail
> column with the item's grants, what the hero wears in that slot now, every stat now → if worn, and
> the one action with a `cd-reason` line (*"Need 175 gold — you have 90."*). Only gold is shown.
> Verified in play mode: buy, sell, the dimmed buy, the belt, the compare switch.

- [x] **Wares and Sell Gear show one row each** — each tab has the full-height list.
- [x] **No way to judge an item before buying** — the detail column, as proposed.
- [x] **"Enlarge Potion Belt" is the biggest button** — moved to its own tab.
- [x] **Essence shown in a shop that never takes it** — gone.
- Found on the way: **rarity never coloured a detail title** (inventory too) — `.cd-inv-detail__name`
  set the plain colour at equal specificity after the rarity classes; doubled selectors fix it.

## Ability Forge (`forge-view`, `MagicForgeUI`)

> **Done 2026-09-30** — on the inventory frame: Abilities / Combos tabs and the essence (with the
> Forge's level cap) in the header; the **known** entries as a list (icon, name, *"Lv 0 of 1"*, the next
> price on the right, red when short, *"Forge"* when the Forge holds it, *"MAX"*), everything
> undiscovered folded into one *"26 undiscovered"* row; the detail column always beside it, with
> *"Upgrade to Lv 1 — 15 essence"* / *"Held at Lv 1 by the Forge"* and a `cd-reason` line under it.
> Effect lines come from the new `AbilityDescriber.ForgeLine` — the picker's and the Storehouse's
> wording, previewing the next level (*"3 damage → 5 damage + STR"*, *"Bleed 1/turn, 3 turns"*);
> effects a higher level unlocks are listed dimmed with their level.

- [x] **A wall of "?"** — known list + one undiscovered-count row.
- [x] **Inspect replaces the grid** — the detail column is always present; the inspect page is gone.
- [x] **Effect rows are debug text** — `AbilityDescriber.ForgeLine`. The Forge has no caster, so it
  names the scaling stat (*"+ STR"*) instead of adding a hero's number.
- [x] **Back and Upgrade are mismatched** — Back is in the header; Upgrade is the detail column's one action.
- [x] **Combos tab centres its tiles** — no tiles any more.

## Bestiary (`bestiary-view`, `BestiaryUI`)

> **Done 2026-09-30** — on the inventory frame: *"4 of 13 discovered"* in the header, met enemies as
> rows (*"Slain x2"*), the unmet as compact unselectable `? ? ?` rows at the bottom, and the page with a
> 96px portrait, *"Health 20 · Slain x2"*, then two columns (Resistances + Immune to | Base stats +
> Abilities) and the drops. Opens on the first enemy met.

- [x] **A narrow window with a scrolling detail column** — see above; a normal entry fits.
- [x] **Nothing is selected on open** — the first met enemy is.
- [x] **Undiscovered rows are 4/5 of the list** — compact rows, grouped at the bottom.
- [ ] **Element names are not in element colours** (severity: low) — still open (needs
  `cd-element--*` classes in `BestiaryLineView` and the combat Inspect page).

## Sphere Grid (`grid-view`, `SphereGridUI`)

> **Done 2026-09-30** — on the inventory frame: Back and the hero's XP in the header band, the
> portrait strip with a gold **"N XP"** tab on every hero who has points to spend, and the graph given
> the freed height (~800x475 at 1280x720, was ~630x210). The screen opens **framed on the frontier**
> (owned + reachable nodes, `SphereGridView.FrameNodes`, zoom held to 0.7-1.1) instead of fitting the
> whole grid. Three node looks: owned = solid accent, affordable = open accent ring, reachable but
> unaffordable = plain frame; locked stays at 45%. The cursor is a 5px gold ring (full opacity even on
> a locked node). Verified in play mode with synthetic arrows / Enter.

- [x] **Detail text overflows its panel** — every detail label wraps (`cd-inv-detail__*` classes).
- [x] **Name, kind and payload say the same thing** — title = the thing (*"+6 HP"*, *"Slash"*, *"+1
  ability slot"*), kind = a chip, payload = what it does (an ability's own description plus its
  charges), and for a stat node a **"Health 26 → 32"** row per stat on this hero. A dimmed Activate
  says why: *"Activate a node next to it first."* / *"Need 65 XP — 30 banked."* / *"Needs 2 Ember Iron."*
- [x] **Nodes are 8px dots and the cursor is a hairline** — see above. Glyphs per kind already existed;
  they were invisible at the fit-everything zoom.
- [x] **The graph gets a third of the window** — see above.
- [x] **Hero switcher is plain tabs** — portrait strip with banked XP.
- [x] **Hint line** — keyboard first: *"Arrows move · Enter activate · Q/E hero · Esc back · drag to pan, scroll to zoom"*.

## Story map (`campaign-view`, `CampaignMapUI`)

> **Done 2026-09-30** — on the inventory frame: Back in the header, the graph given the freed room,
> a legend under it (*"▶ in progress · ★ open · ? side road · ✓ cleared · 🔒 locked"*), and a detail
> column with *"In progress · Level 2 of 4"*, every floor (✓ cleared, ▸ current in gold, · ahead),
> *"Clearing it opens"* with the runs it unlocks, and Begin / Continue at the bottom.
> `CampaignMapUI.Show` takes the active level index for that line.

- [x] **Map crammed into the left half of its canvas** — the graph was already fitted; it had a
  shorter canvas than it has now. It fills the new, taller graph area.
- [x] **"In progress · 5 levels" instead of where you are** — *"In progress · Level 2 of 4"* and the floor list.
- [x] **No legend for ✓ ★ ? 🔒** — one line under the graph.
- [x] **Detail panel has room it does not use** — floors and what a clear opens. (No party-size
  or recommended-level line: the campaign authors neither.)

## Run progress / level entry (`progress-view`)

> **Done 2026-09-30** — one fixed size (`.hub-entry`, 760×440): the run's name and *"Level 2 of 4"* in
> the header, the floor's name large, **a pip per floor** (filled = cleared, gold ring = this one,
> dim = ahead), *"Marching out"* as portrait tiles with each hero's HP (base + grid + gear), and
> **Enter Dungeon, Change Party, Back** in that order — Enter filled and gold-framed as the primary,
> and where the first arrow key lands.

- [x] **A 490x260 box for the "you are about to go in" moment** — see above.
- [x] **Back and Enter Dungeon are unequal** — one row, Enter the widest, Back a header-style button.

## Dungeon HUD and room bar (`dungeon-hud`, `main-bar`, `combat-bar`, `nav-hint`)

- [x] **The keyboard hint drifts off-centre** — *done 2026-09-30*: full-width and text-centred. (severity: medium) — after the first Fight bar, `nav-hint`
  ("← → choose · Enter confirm") sits ~85px left of the bar it belongs to (`44_fight_bar.png` is centred,
  `49_pause.png` and `51_event_room.png` are not; measured x 592–798 ref vs bar centre 800).
  `.cd-nav-hint` centres with `left: 50%; translate: -50% 0`, and the percentage translate is not
  recomputed when the text changes width. Make it `left: 0; right: 0; -unity-text-align: middle-center`
  and drop the translate.
- [~] **The HUD covers the map** — *partly, 2026-09-30*: one fixed width (360px), and the teaching suffix is gone once the tutorial is done, so it no longer grows and shrinks; the camera still has no top-left safe area. (severity: medium) — `44_fight_bar.png`: the top-left HUD sits on top of
  the room above (and a door behind it). It also grows and shrinks with its text (`55_exit_room.png` is
  wider). Give `.cd-hud` a fixed width, and have the room camera keep a top-left safe area, or collapse
  the HUD to one line while walking ("Collapsed Caverns · 43g · 1 way left · M").
- [x] **"(banked when you take the stairs)" on every frame** — *done 2026-09-30*: only while the tutorial is unfinished. (severity: low) — teaching text that never
  goes away. Show it for the first floor only, or move it into the level-clear breakdown where it is
  already explained.
- [x] **Room bar verbs are generic** — *done 2026-09-30*: the event button is the event's title ("A Sealed Tomb"); "Open the cache", "Rest at the refuge", "Take the stairs". The one-button frame stays. (severity: medium) — a lone "Action" in an event room
  (`52_event_bar.png`) and "Search" in front of a visible chest (`42_cache_room.png`). Label the button
  with the thing: the event's title ("The Treasury"), "Open the cache", "Rest at the refuge". The
  single button in a framed box also looks like a dialog stub; drop the `cd-bar` frame for a one-button
  bar.
- [x] **Fight / Flee says nothing about the fight** — *done 2026-09-30*: `fight-foes` above the bar, grouped by kind ("Slag Hound x2 + Cinder Imp"), "???" per unmet kind ("??? + ???", never "??? x2"). (severity: medium) — `44_fight_bar.png`: the only
  sign of the enemy is a 16px sprite in the room's corner. Add a line above the bar: *"Drakeling"* /
  *"Stone Sentinel + Floating Eye"* (names from the bestiary, "???" if unseen — no stats), so the
  decision the bar asks for is informed.
- [x] **Party panel is a list of numbers** — *done 2026-09-30*: portrait, name, HP and a slim bar per row. (severity: low) — "Warrior HP 38/38" in 60px-tall rows with no
  bar (`40_dungeon_start.png`). The inventory already draws a hero portrait; add a portrait and an HP
  bar per row, tighten rows to ~40px, and show level afflictions (below).
- [x] **Level afflictions are invisible after the dialog** — *done 2026-09-30*: a chip per affliction on the hero's row ("-2 Strength", tooltip "until the stairs"). (severity: medium) — the Treasury event gave
  "Rogue: Strength -2 for the rest of the level" (`54_event_result.png`) and nothing on screen remembers
  it. Show a small debuff glyph on the Rogue's party row (tooltip / inspect text "STR -2 until the
  stairs").
- [x] **Party panel lags the event it reports** — *done 2026-09-30*: refreshed as the outcome is applied. (severity: low) — the result says "Rogue takes 3 damage"
  while the panel still shows 18/21 (`54_event_result.png`); it updates only after OK (`55_exit_room.png`
  15/21). Call `RefreshPartyStatus()` when the outcome is applied, not when the dialog closes.

## Room dialogs (`event-window`, `detail-window`)

- [x] **Event choices don't say which ones are gambles** — *done 2026-09-30*: a tag per row ("STR · likely", "LCK · risky" when the read is vague, "WIS · ?" when unknown, "safe", "leave"), coloured by band. (severity: medium) — `53_event_window.png`: the
  odds line is prose above the list ("turns on Luck — Rogue has the best of it — an even bet") and the
  three options look identical, although "Take nothing and move on" is safe. Put a right-aligned tag
  on each row (`cd-row__meta`: "Luck · even", "safe"), and colour the odds word by band.
- [x] **Results don't say whether the check passed** — *done 2026-09-30*: a Success / Failed chip under the title for a stat check. (severity: low) — `54_event_result.png` is flavour text
  then consequences. Add a one-word outcome header chip ("Failed" in red / "Success" in green) above
  the message.
- [x] **Loot as plain text lines** — *done 2026-09-30*: the cache lists its finds as the victory window's reward rows. (severity: low) — the cache (`43_cache_dialog.png`) lists "+15 gold. Found:
  Leather Cap. Salvaged: Cut Stone x3." as sentences. Reuse the victory window's reward rows (icon,
  rarity-coloured name, count right-aligned).
- [x] **"The Way Down" warns about unfound things when there are none** — *done 2026-09-30, compile-verified only* (the test floor's exit was guarded): the line appears only when the map's frontier is non-zero, as "N ways not yet taken will stay unexplored." (severity: low) —
  `56_descend.png`: the HUD says "nothing left unopened", the dialog still says "Anything still unfound on
  this level stays here". Only add that line when the map's frontier/payload count is non-zero, and say
  what ("1 room not entered").
- [x] **Dialogs have no scrim** — *done 2026-09-30*: `dialog-scrim` (55%) under the event, detail, map and pause windows, driven from `RoomActionUI.Update`. (severity: low) — every `cd-dock-center` dialog sits on the live room with
  full-brightness tiles around it (`43_…`, `53_…`, `56_…`). A `rgba(6,3,14,0.55)` scrim behind
  `event-window` / `detail-window` / `map-window` / `pause-window` separates "the game is waiting for you"
  from "you are walking".

## Pause overlay (`pause-window`)

- [x] **Two cursors on screen** — *done 2026-09-30*: `cd-root--paused` hides `main-bar`, `combat-bar`, `hero-bar` and `nav-hint` (visibility, so every "is the bar up" check still holds). (severity: medium) — pausing in a fight room leaves the Fight/Flee bar and
  its highlighted *Fight* button visible beneath the pause window (`49_pause.png`). Hide `combat-bar`,
  `main-bar` and `nav-hint` while paused (and restore on Resume), or at least drop their
  `cd-nav--selected`.
- [x] **Resume is third** — *done 2026-09-30*: Resume, Map, the audio block under "Audio", Leave; the first arrow lands on Resume. (severity: medium) — order is audio dials, Sound, Map, **Resume**, Leave. Put
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

- [x] **HP bars sit between heroes and the top one is clipped** — *done 2026-09-30.* One readout per
  unit, a UITK plate under its feet (`UnitNameplates`); the world bar above the head is gone, and with
  it the top-edge clip. The HP number sits **inside** the bar (the owner's call).
- [x] **Enemy HP shown twice** — *done 2026-09-30*: the nameplate carries the bar, the number is in it.
- [x] **The ability picker covers the acting hero** — *done 2026-09-30*: empty slots are not listed, the description sits right of a 280px list (`cd-picker-body`), and the picker is capped at 40% height, so it stays below the party. (severity: medium) — `61_ability_picker.png`: the picker
  (desc + an "(empty)" row + Back) is taller than the command menu and cuts the Rogue's sprite off at the
  shoulders; `.cd-window--picker`'s own comment names this risk. Hide "(empty)" slots, put the description
  to the right of the list instead of under it, and cap the picker at the command window's height.
- [x] **Command menu: no reasons** — *done 2026-09-30*: a greyed row says why on its right (*"No charges"*, *"None carried"*, *"Silenced"*, *"Spent"*, *"None"*). The width was already fixed. (severity: low) — `45_combat_menu.png`: a 445px
  window for "Attack / Ability / Item / Inspect / Skip" with *Ability* greyed and no explanation. Narrow it
  to ~300px, and put the charge count on the row ("Ability 2/2") or the reason ("Ability — none
  carried") in the row's meta slot.
- [x] **Turn highlight lags into the enemy's turn** — *done 2026-09-30*: the party row drops its highlight when a turn ends (`OnTurnExecuted`). The field arrow was never wrong: it is the turn marker and follows whoever acts. (severity: low) — during the Floating Eye's action the
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
- [x] **Level clear hides the materials below the fold** — *done 2026-09-30*: Materials and Items come first; Gold is one section with one line ("43 found · 25 for the clear"); the Forge hint is gone. (severity: medium) — `57_level_clear.png`: Gold (3
  rows), Essence (2 rows) and XP (4 rows) fill the scroller and *Materials* starts at the bottom edge, so
  what the player went down for needs a scroll. Two columns (currencies left, loot + materials right),
  or collapse the Gold breakdown into one line ("+68 · 43 found, 25 clear bonus").
- [x] **"Spend it at the Forge to upgrade abilities"** — *done 2026-09-30 with the level-clear reorder*: the line is gone. (severity: low) — permanent teaching line under Essence;
  fine for the first clear, noise after. Gate it like the HUD's banking note.

## Defeat and run complete (source only)

- [x] **Defeat is the generic detail dialog with the raw combat log** — *done 2026-09-30*: "The Party Has Fallen", one line of where, then kept/lost rows (gold for the levels reached, what was banked, the floor's gold, XP, items and materials) and "Return to town". The victory-style red frame and the fuller run summary stay with §15. (severity: medium) —
  `ShowDeathScreen` puts `result.Log` (the turn-by-turn log) into `detail-message` under "Your Party Has
  Fallen..." with an "Ok" button. Give it the victory window's frame (`cd-victory`, red header) with
  what was lost — the floor's un-banked gold and XP, as the level-clear shows what was gained — and a
  "Back to Town" button. The fuller run summary is §15.
- [ ] **Run complete is two fixed lines** (severity: low) — `complete-view`: "Victory! / You have conquered
  the dungeon…" and Return, regardless of which run. Name the run, show what clearing it unlocked on the
  story map, and reuse the `cd-victory` header band.

## Storehouse inventory (the bar) — small notes

- [x] **Stat losses are not red** — *done 2026-09-30*: `cd-inv-delta--down` is red in the list chips and the stat grid (Storehouse, Merchant, Campfire). (severity: low) — `16_inventory_equipment.png`: in "Fits Main Hand",
  Oak Staff shows "+5 INT" gold and "−4 STR" pale blue; a loss should read as a loss (`--cd-bad`, red).
- [ ] **Hero strip greyed but present on Materials** (severity: low) — `18_inventory_materials.png`: four
  disabled hero tiles take a band that means nothing on that tab. Fine for the fixed frame; draw them at
  `opacity: .35` without borders so they read as "not applicable" rather than "broken".
- [x] **"Attack 15" vs "STR 15"** — *done 2026-09-30*: the damage line names the caster's attack stat by its `StatCatalog` short name ("29 damage (AGI 26)" for the Rogue, who attacks with Agility). (severity: low) — the ability line "18 damage (Attack 15)" names a stat the
  stat grid calls STR. Use the `StatCatalog` label.

---

## Keyboard pass — 2026-09-30

Every hub screen driven with **synthetic** key events (not the real OS keyboard: say so, and have a
human press through it once). Shared-cursor screens get a seeded 300-press random walk and a list of
visible, enabled buttons it never reached; own-cursor screens get scripted Up/Down/Left/Right/Enter/
Q/E/Esc. Result: every button reachable on the town (8), lot panel, Merchant Buy (15) / Sell / Belt,
Campfire (15), Forge Abilities (8) / Combos; the Bestiary cursor wraps; the Storehouse Left/Right
changes tab; the Sphere Grid Q/E changes hero and arrows walk nodes; the story map walks runs and
Enter opens the level-entry screen, whose cursor reaches Change Party / Enter Dungeon; **Esc closes
every screen and leaves the town interactive** (scrim gone, lots enabled). One fix came out of it:
**the story map reopened on the last node visited**, so Enter could land on a locked run and only say
*"That way is still closed."* — it now always opens on the default pick (the run in progress, else the
first open one). The probe code is in the session log; the recipe is `docs/GAMEPLAY_VALIDATION.md`
gotcha 17.

## Do these first

1. ~~**Fix the Campfire**~~ — **done 2026-09-30**, rebuilt on the inventory frame with seat tiles.
2. ~~**Merchant onto the inventory frame**~~ — **done 2026-09-30**.
3. ~~**Sphere Grid readability**~~ — **done 2026-09-30**.
4. ~~**Combat HP bars**~~ — **done 2026-09-30**: one plate under each unit, the number inside the bar.
5. ~~**Keep the town behind hub services**~~ — **done 2026-09-30**.
6. ~~**Forge**~~ — **done 2026-09-30**.
7. ~~**Say why a button is disabled**~~ — **done 2026-09-30**.
8. ~~**Recentre `nav-hint` and hide the room bar under pause**~~ — **done 2026-09-30**.

All eight shipped on 2026-09-30. What is left in this file is the per-screen detail above that the
list did not cover (title/Options, lot panel, Bestiary, story map, level entry, HUD, dialogs, the
rest of combat, victory screens).
