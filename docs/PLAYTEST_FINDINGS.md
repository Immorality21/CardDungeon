# Playtest findings — first fresh-save playthrough (2026-09-28)

> **This file is a to-do list, not documentation.** Tick items off as they are fixed (or strike them
> with a one-line reason if deliberately dropped). **When every item is cleared, delete this file.**
> Anything worth keeping long-term (a decision, a gotcha) goes into the relevant `CLAUDE.md` / plan
> file instead, not here.

**How this was found:** fresh save (`savedata/` deleted), editor play mode at **1280×720**, driven
through the Unity MCP: title → hub → The Threshold level 1 (cleared) → level 2 about 70% (rooms 0→1→3→5→7→9).
Input was simulated (`NavigationSubmitEvent` / synthetic `KeyDownEvent`), so **real mouse and
keyboard input was not exercised**. Not visited: the Forge, the Merchant, the Sphere Grid, the Storehouse and the Campfire screens,
levels 3–4, a defeat. Console showed **no errors or warnings** for the whole session.
Screenshots from the session are not kept; re-capture with `ScreenCapture.CaptureScreenshot` as in
`docs/GAMEPLAY_VALIDATION.md`.

---

## Bugs

- [x] **1. Party panel is stale after a rescue.** After freeing the Paladin (level 1, room 1) the
  bottom-right Party panel kept showing only "Warrior" — `Party.Heroes` already contained the
  Paladin (25/25). It only refreshed after walking into the next room. The panel should rebuild when
  a hero joins.
- [x] **2. Rescuing a captive must not be optional** *(owner's call)*. Today: room bar shows a
  **Rescue** button → "A Prisoner" dialog with **Free them / Cancel** → "Paladin joins you" → **Ok**.
  Clearing the room should free the captive automatically; at most one "Paladin joins you"
  notice. Remove the Rescue button and the Cancel path.
- [x] **3. Everything stacks in the middle of the room on entry.** In level 1 room 1 the Warrior was
  drawn directly over the captive Paladin *and* the Floating Eye; in the exit room the hero stood
  on top of the stairs. You cannot see what is in the room. The party should stand at the door it
  came through, and room contents (enemies, captive, stairs, chest, rest marker) should be placed
  apart from each other and from the party.
- [x] **4. Turn marker is ambiguous with two heroes.** In combat, the gold ▼ for the Paladin's turn
  sits between the two stacked heroes (on the Warrior's feet / above the Paladin's HP bar). It
  reads as pointing at either hero. Anchor it clearly to the acting unit (or highlight the unit
  itself).

## Missing information / feedback

- [x] ~~**5. No first-time guidance.**~~ *Dropped 2026-09-28: folded into the tutorial, `docs/plans/POLISH_CONTENT.md` §20.* A brand-new save drops the player into the hub with nothing
  telling them what to do. The only way forward is **The Story** — a small box on the far right edge.
  At minimum, point at The Story on a new save (ties into the tutorial, `docs/plans/POLISH_CONTENT.md`).
- [x] **6. Clearing a level has no moment.** Descending on level 1 of 4 goes straight back to the hub
  with no "Level cleared" screen. Gold went 0 → 50 and Essence 0 → 5, but only +5 (fight) and +15
  (event) gold were ever shown — the other +30 gold and all Essence were never explained. Add a
  level-complete summary (gold, essence, XP, materials, items, who joined).
- [ ] **7. No dungeon HUD.** In a level there is no gold counter, no level name ("Upper Halls"), no
  "Level 2 of 4", no minimap/room count. The room floats in a black screen. Gold earned from events
  has nowhere to show up.
  - *HUD done 2026-09-28* (level name, run + level N of M, gold found this floor, explored line + M map).
    **Still open: the black backdrop around the room.**
- [x] **8. Abilities have no description.** The Ability picker shows "Slash 2/2" and nothing else —
  no damage, no effect (it applied a bleed), no hint of how it differs from Attack.
- [x] **9. Enemy HP is unreadable.** Enemies get only a thin bar: no name, no number. At 1 HP the bar
  is practically invisible (no dark background track).
- [x] ~~**10. Hub requirements don't say where materials come from.**~~ *Dropped 2026-09-28 (owner's call): where materials come from stays a mystery on purpose. The spend-XP half moved to the tutorial, `docs/plans/POLISH_CONTENT.md` §20.* "Needs 8 Scrap Iron · 2 Rotted
  Timber", "Needs 1 Rotted Timber", "Needs 4 Scrap Iron" — nothing tells the player these drop in
  the dungeon. Also: XP is earned from the first fight, but it cannot be spent until the Sphere Hall
  is built, and nothing says so.
- [x] **11. Story map nodes are anonymous.** Run nodes have no labels (the name only appears in the
  side panel after selecting), and the locked nodes don't say what unlocks them.

## Readability at 1280×720

- [ ] **12. Lots of text is ~8–11px.** Worst offenders: the input hint lines ("Arrows pick a door ·
  Enter walk through · Tab room actions", "↑↓ choose · Enter confirm", "Drag pan · Scroll zoom ·
  Click a run · Esc back"), the building requirement lines in the hub, "Level 1 of 4" / "Party (1):
  Warrior" on the level-entry screen, "Open · 4 levels" on the story map, and dialog body text.
- [ ] **13. Hub labels overlap the art and wrap badly.** "Needs 8 Scrap Iron · 2 / Rotted Timber"
  wraps mid-phrase and sits on top of the fence sprite; the Storehouse label covers the building's
  door. Gold/Essence are tiny in the bottom-right corner.
- [ ] **14. Menus are small boxes on an empty background.** The title screen, the level-entry screen
  ("Dungeon Entrance" / "Upper Halls") and the story map are small panels floating on a flat colour.
  Also:
  - The title still reads **"Card Dungeon"**; the itch page is *Immoral Dungeon*. Pick one name.
  - On a brand-new save the first button says **"Continue"**; it should say "New Game" (or similar).
  - The level-entry screen names the level but not the run ("The Threshold").

## Old / rough controls

- [ ] **15. Inspect panel is cramped.** Small scroll box with Unity's default grey scrollbar;
  resistances show "Physical: -" with no legend for what "-" means.
- [ ] **16. Rest room marker** (big flat green "+") looks like placeholder art; the cleared treasure
  room leaves a grey "+" ghost too.
- [ ] **17. Adjacent rooms with the same tiles read as one room.** Level 2's rest room (7) and
  treasure room (9) are stacked vertically with identical stone; only a tiny door separates them.
  Room borders need to be more distinct.
- [ ] **18. Room events have a redundant exit.** The Treasury offers "Take nothing and move on" *and*
  a **Back** button that do the same thing.

## Gameplay / balance impressions

- [ ] **19. Level 2 of the tutorial run puts a Dragon on both paths from the start** (rooms 1 and 2).
  It has 22 HP and died in two rounds, so it's not dangerous, but the name oversells it and it pays
  exactly what a Floating Eye pays (+20 XP, +5 gold).
- [ ] **20. Enemies always hit the Warrior, never the Paladin.** Across four fights the Paladin
  took 3 damage total; the Warrior went 26 → 13. The Paladin is written as the one who "holds a
  line" — consider threat/taunt so it draws hits (`docs/plans/COMBAT_DEPTH.md`).
- [ ] **21. Same room event two levels running.** The Treasury (`TreasuryHoard`) appeared in level 1
  and again in level 2.
- [ ] **22. Balance regression suite is red.** `BalanceRegressionTests` fails 2 tests on current
  assets (found when setting up CI, so it's excluded there with `-testCategory !Balance`):
  - Unclearable on one health bar: The Drowned March L0 Silt Shallows (158 HP vs 121),
    L1 The Reedcage (180 vs 121), The Warrens L0 Warren Tunnels (140 vs 121).
  - Boss rooms over the 1.40 climax ceiling: The Warrens L1 The Counting Room (1.54),
    The Hollow Vault L0 (3.25, Gilded Hoarder + 3 adds).

## Tooling / docs

- [ ] **23. `docs/GAMEPLAY_VALIDATION.md` §6 documents combat hotkeys that no longer exist.** A/M/D/S
  (and F/R) were removed from `RoomActionUI`; only `M` (map) remains. The command menu is driven by
  ↑/↓ + Enter now. Update the section and the quick-reference list at the bottom.
- [ ] **24. CI logs the local Unity editor out.** The GitHub Actions workflow
  (`.github/workflows/playtest.yml`) activates Unity with the owner's account and returns the
  license at the end of every job; with the entitlement-based Personal license this appears to
  remove the license on the owner's PC ("License removed" in the editor console at 16:52, lining up with
  a CI job ending — not proven). Fix: a separate free Unity account for CI, its credentials in the
  `UNITY_EMAIL` / `UNITY_PASSWORD` secrets.

## What worked well (keep)

Combat screen and backdrop, the turn-order panel, the victory panel, room-event writing (Treasury,
Refuge, Cache), the rest dialog explaining exactly what it does, the Inspect data itself, and the
hub's painted town.
