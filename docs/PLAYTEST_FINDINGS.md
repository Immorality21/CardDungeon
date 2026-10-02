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

13. **The Threshold is far too easy.**
    - Floors 1–3 were one or two enemies a fight, mostly a lone Floating Eye beaten by pressing
      Attack.
    - No potion was needed all run.
    - The Abyssal Warden has 50 HP and died in 3 rounds while the party had no abilities left.

    Add 2–3-enemy rooms by floor 2, and make the Warden survive a round without abilities. Measure
    first: see `docs/BALANCING.md` and `plans/BALANCE_OPEN.md`.
14. **The Merchant is blocked by timber.** After 6 floors the party had 544 gold and 13 Scrap Iron but
    only 1 of the 2 Rotted Timber it needs. Timber only comes from caches, so the only gold sink was the
    300-gold campfire upgrade. Drop the second timber, or guarantee one on Threshold floor 2–3.
15. **The same event repeats on one floor.** The Treasury event came up 4 times across Threshold floors
    2 and 3, 3 of them on floor 2.
16. **Risky event options don't pay more.** The "dangerous" Treasury choice paid the same +15 gold as
    the safe one. 3 of 4 stat-check gambles were lost.
17. **A level affliction landed in the exit room.** STR −2 "for the rest of the level" cost nothing
    there. Keep afflicting outcomes out of the exit room, or carry them to the next floor.
18. **The rescued hero arrives with nothing to use.** The Ranger joined with no abilities and 198–240
    banked XP that can't be spent until town. This works as designed but feels flat.

*Not a finding:* the agent reported that going back to town heals and refills potions, so Continue is
pointless. Both happen on every fresh floor either way. Continue only gives up the hub, which is by
design.
