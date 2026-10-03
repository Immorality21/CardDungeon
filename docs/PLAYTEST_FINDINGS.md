# Playtest findings — second fresh-save playtest (2026-10-02)

A fresh save played through the Unity MCP by an agent playing as a player. Route: New Game → **The
Threshold** cleared (all 4 floors and the Abyssal Warden), then **The Drowned March** floors 1–2 of 5
(rescued the Ranger on floor 2). No fight was lost. Most combat turns ran through a small auto-player
(heal or drink a potion when low, cast abilities in bigger fights, focus the weakest enemy) using the
same calls the menus use, so the real keyboard and mouse path still wants a human press.

Work through these and **delete each one as it ships**. Once the file is clear, record it as one
ledger line in `NEXT_STEPS.md` and delete the file, as was done with the 2026-09-28 playtest.
Comments may cite these as "playtest 2 finding N".

**Not covered:** death and retry, Leave the Dungeon from pause, the Merchant, Bestiary and Forge
screens (not built or not affordable), summons, The Warrens, The Blood Stair, Drowned March floors 3–5,
and swapping in a benched hero mid-run (only 2–3 heroes were owned).

**What landed well:** the look (town, swamp backdrop, pixel art), the readable loop, a short
tutorial, and the mid-run rescue of the chained Ranger, which was the best moment. A Bog Shaman
healing itself for 28 while the party raced it was the first fight with real tension. The party lock
(shipped the same day) works: it is open before floor 1, locked after it, and the reason line names
the run.

---

## Bugs

*All four fixed 2026-10-02.*
- **1:** an ability learned mid-run now fills an empty slot at full charges (`SeedFromLoadout` for every hero on later floors).
- **2:** the Storehouse and campfire show the run's real slots and charges left (`MagicLoadoutOps.RunKit`, `RunKitSource`).
- **3:** the boss leads the foe line and is always named.
- **4:** an inventory row's chips now say what they're compared against ("· vs Rusty Sword").

## Polish — clarity and layout

*5–11 fixed 2026-10-02.*
- **5:** an event choice's words and its odds tag share a row, so long choices wrap instead of running under the tag.
- **6:** the spoils window is capped at 88% height and scrolls; the exit fight is "Victory!" (the level ends on the stairs), its gold has no "(banked)", the final boss says "The way out", one-hero XP has no "shared", and both lists pad their last row.
- **7:** stat nodes show their largest gain's short name ("STR", "HP"); the tutorial banner goes compact over the grid.
- **8:** the room camera keeps the room clear of the Fight/Flee bar, the room bar and the party window, not only the HUD (`CameraSafeArea`).
- **9:** a roster row's Leads/Resting tag moved onto the name line, so the stat caption has the full width.
- **10:** a built lot's hammer only shows when its upgrade is affordable.
- **11:** the campfire's lock line is neutral, and the run screen says "Party locked until the run ends."

## Balance and content

*13–17 done 2026-10-02 (`BALANCING.md` §5y).*
- **13:** Threshold floors 2–3 get two new Threshold-only rooms with 2–3 enemies (Watchpost, Sentry Hall); the Warden goes 50 → 65 HP. Kept easy on purpose - it is the tutorial.
- **14:** the Merchant costs 10 Scrap Iron + 1 Rotted Timber (was 8 + 2).
- **15:** each room event is placed at most once per floor.
- **16:** the Treasury chest's two success outcomes pay 40 gold + loot and 30 gold (were 30 + loot and 15 - the same as the safe option).
- **17:** events that hand out level-long buffs or curses are never placed in the exit room.

18. **The rescued hero arrives with nothing to use.** The Ranger joined with no abilities and 198–240
    banked XP that can't be spent until town. This works as designed but feels flat.

*Not a finding:* the agent reported that going back to town heals and refills potions, so Continue is
pointless. Both happen on every fresh floor either way. Continue only gives up the hub, which is by
design.
