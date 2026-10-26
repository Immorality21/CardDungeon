# Playtest 3 — 2026-10-26

Findings from the owner's playtest on 2026-10-26, gathered as they came in and not yet worked on.
Cite them in code comments as **"playtest 3 finding N"**, like the earlier playtests; delete this file
once everything is shipped or moved into a plan (`docs/NEXT_STEPS.md` keeps the ledger).

**Answered during the session (no action):** health refilling on every new floor is intended. Health
lasts one floor and refills on a fresh floor or in a refuge (`Rooms/CLAUDE.md`, `RoomActionUI.cs`);
it has been that way since before 2026-09-06.

**Grouping:** bugs: 4, 8, 17 (button), 22 (to verify) · balance: 10, 13 · potions and the belt: 5 ·
hero kits and combat rules: 6, 9, 11 · combat presentation: 3, 7, 12, 21 · art: 14, 15, 18 ·
hub and UI: 1, 2, 16, 17, 19, 20.


1. ✅ *Done 2026-10-26: a Reassign / Cancel dialog in the Storehouse (Enter, arrows, Esc), and "Worn by X" in amber.* **Gear reassign confirmation** (hub inventory / equip): equipping an item another hero already wears should show a small confirm — "This is already equipped by [hero name]. Reassign it?"
   - **Make equipped items easier to spot in the list**: e.g. give the item name a different text colour when someone has it equipped. At a glance it's hard to tell equipped gear from unequipped gear.
2. **Tutorial — return to the Hall of Progression after the gear step**: once the equip-gear tutorial step is done, guide the player back to the Hall of Progression to spend XP on both the Warrior and the Paladin. Point them at the building; on open, auto-select the Paladin (there are 2 heroes now). Spending is optional, so don't force a purchase to advance.
3. ✅ *Fixed 2026-10-26: `Combat/HealthDisplay` holds each plate until its hit lands (hero and enemy casts; summons/Ultras not yet).* **Floating Eye party-wide magic attack — damage lands too early**: HP bars and damage numbers update before the projectiles are fired. They should apply on hit (on the flinch), per target.
4. ✅ *Fixed 2026-10-26: the trunk slot (and a few stat nodes) moved off other branches' lines on all 9 grids; the slot glyph reads "+1"; `SphereGridLayoutTests` guards it.* **BUG — sphere grid node layout (Warrior, Paladin, likely all grids)**: the first node above the central hub node (the one that splits into the 3 branches) is drawn on a branch path, e.g. Hamstring's, after an Ability-slot node. But that Ability slot can be bought directly without unlocking it, so the node is drawn in the wrong place, or the path lines are misleading. The same node is also labelled "M", which means nothing to the player.
5. **Potions — rework** (today: belt cap 2, raised at the Merchant for 25 gold × current size; basic Healing Potions refill to the cap on every fresh floor; heal 2 + 30% max HP, Greater 3 + 45%; used in combat only).
   - **Refill per run, not per floor.** The belt tops up when a run starts, and potions drunk on floor 1 stay gone for floor 2.
   - **Do NOT change the balance model.** The analyzer and simulator keep assuming a refill every floor. The owner rarely drinks potions and clears floors easily, so they think the model still errs on the easy side; the per-run refill is meant to make the real game harder than the model says.
   - **Belt cap starts at 2.** Upgrades at the Merchant cost gold **plus materials**, with later upgrades needing materials that only become available later, so belt growth is gated behind progression.
   - **No found potion variants.** Greater Healing Potions (and the like) must not drop or be findable. The only potion is the Healing Potion, and it comes from the belt.
   - **Potency upgrade at the Merchant.** The Healing Potion stays one item, but its heal can be upgraded there.
   - **Unique materials from re-runs.** The materials for the later upgrades drop only in re-runs of earlier levels, i.e. at a raised Fear level, as the reason to replay.
   - **Tutorial step later** that points the player at where to get those materials. Keep it light; per the mystery-over-hand-holding rule, guide only where the player would otherwise be stuck.
   - **Idea — a belt of typed slots.** The belt has a number of slots (e.g. 4), each holding one potion type with its own capacity. Example:
     | slot | potion | type | capacity |
     |---|---|---|---|
     | 1 | Healing Potion | healing | 4 |
     | 2 | Antidote | ailment | 2 |
     | 3 | Energy Potion | energy | 1 |
     | 4 | Elixir | healing_energy | 1 |
     - The **type** is the category, and it is what the player chooses when filling a slot. Several potions can share a type (e.g. *resistance*: Frost Resistance, later Fire, ...). Healing, energy and elixir are probably the only potion in their type.
     - **Upgrades (potency, capacity, ...) apply per type**, so every potion of that type benefits.
     - **New potion types are unlocked** through exploration, the Merchant, or quests (quests are later on the roadmap). Example: completing a quest unlocks Frost Resistance (type: resistance).
     - Fits the per-run refill and the material-gated Merchant upgrades above: each slot refills once per run, and upgrades cost gold plus materials.
     - **Slot count grows** through upgrades (how is still to decide; likely the Merchant, with gold plus materials like the other upgrades).
     - **Potions are picked for the slots at the campfire** (`PartySelectUI`), where the party is chosen before a run. Tentative ("maybe").
     - **Energy Potion restores 1 ability charge**, upgradeable to restore more. `RestoreChargeEffectExecutor` already exists and may be reusable.
     - *Still open (slots):* how slots are unlocked and what they cost; whether Antidote Salves are still found as drops or are belt-only like healing.
   - *Open questions (potions):* should Bog Shamans still drop Healing Potions (today's only potion drop), or do drops go away entirely? Do Antidote Salves keep dropping? Which materials does each belt or potency tier cost, and which revisit Fear level drops each one?
6. **Paladin starting kit — rework.** Today the free signature node (`paladin-sig`) grants Holy Touch (heal one ally or self, power 4), and the healer branch's 65 XP node (`paladin-c-3`) grants Heal, which targets **self only** (power 6). So the healer branch's payoff is a self-heal that is barely stronger than the free one.
   - **Signature node → a new holy melee attack** (Strength attack, Holy damage). This needs a new ability asset.
   - **Healer branch → Holy Touch** in place of Heal. Its power may need raising (it's 4 today vs Heal's 6), since it is now a paid branch reward.
   - Leave the shared `Heal` asset alone: the Mirefather enemy and the Cleric hero use it.
   - Check the Hero Workbook and `Paladin.asset`'s Vision (healer branch "Heal -> Renew") before changing anything. The Vision is the owner's: fill empty fields only, never rewrite it.
   - Expect the Paladin's early healing to drop, which affects balance on floor 1 and in the tutorial.
7. **Hero death presentation (all heroes).** When a hero dies in combat, play a dying animation and leave the hero lying on the floor for the rest of the fight. In later fights the downed hero's body is not shown at all; it only lies on the floor in the fight where it fell. Needs a death/lying pose per hero; see `docs/PIXEL_ART.md`.
8. ✅ *Fixed 2026-10-26: every Wight frame mirrored in place; the other 21 enemies checked and fine.* **BUG — Wight sprite faces the wrong way.** The Wight is an enemy (`ScriptableObjects/Enemies/Wight.asset`). It should face the party like the other enemies. Fix by flipping the sprite or the strip; see `docs/PIXEL_ART.md`.
9. **Downed heroes stay down between floors.** Today `Party.HealAll()` on a fresh floor brings everyone back to full, the fallen included. Living heroes should still refill to full on a new floor, but downed ones stay down.
   - Ties in with #7: a downed hero's body is not shown in later fights.
   - **Back at the hub:** a downed hero is restored once the party returns to the hub.
   - **Revive only in the fight where they fell.** Once that fight ends they can't be revived until the hub.
   - **Refuges do not revive.**
   - *Still open:* does the balance model change for this, or does it keep assuming a full-party floor start (like the potions in #5)?
   - The dungeon save already stores health per hero (`HeroHealth`), so a 0 should survive a quit and resume.
10. **Catacombs too easy, at least a minor difficulty bump.** With gear equipped and Hall of Progression nodes bought, the owner has not wiped once. Suspicion: equipment is a big swing that the balance tool under-counts.
    - Quick check (2026-10-26): `BalanceRulesSO.ReferencePartyGoldBudget` defaults to **0**, and there is no rules asset overriding it. So the reference party the analyzer, the regression bands and the floor simulator measure wears **no gear**; only the investment frontier treats gold/gear as an axis. That supports the suspicion: the "target difficulty" numbers describe an unequipped party.
    - Options to weigh in the balance pass: give the reference party a realistic gold budget per campaign tier (what a player has actually earned by then); and/or bump Catacombs directly (enemy Difficulty, spawn tables). Read `docs/BALANCING.md` first; measure, don't guess.
    - Related: #5 (potions per run) and #9 (downed heroes stay down) both make runs harder in play without changing the model.
11. ✅ *Done 2026-10-26: Shield Up adds 20 threat per cast (Sacred Shield left alone).* **Warrior's Shield Up should draw aggro**, making the Warrior more likely to be attacked.
    - Already supported: threat targeting shipped 2026-09-28 (`Combat/ThreatTable.cs`, `docs/plans/COMBAT_DEPTH.md` §11), and `MagicSO.BonusThreat` adds flat threat per cast. `ShieldUp.asset` has none today (0).
    - Likely a data change only: give Shield Up a `BonusThreat`. For scale, Idol abilities have 8-12, Horror Engulf 30, Aegis Lion 60. Base threat is 10 per unit. Check the threat formula for how much a given value shifts the hit odds.
    - Maybe Sacred Shield (Paladin) too, if it plays the same role.
12. ✅ *Done 2026-10-26: top-centre banner for every enemy spell and named action (`EnemyActionEntry.DisplayName`); boss signatures got placeholder names (Bog Surge, Abyssal Tide, Death Nova, Acid Deluge, Coin Storm). Plain swings and wind-up turns show nothing.* **Enemy ability name banner (FFX-style).** When an enemy uses anything other than its normal attack, show the ability's name on screen (FFX's top-centre banner) as it acts. Today the enemy does something and the player can't tell what.
    - Keep it short: the name only (minimal combat text rule). Probably also for summons/Ultras if they lack it; check what heroes' casts already show.
13. **Drowned March, every floor, is too easy and too sparse.** Floor 1 had at most 1 enemy per room, each dying in one hit, and the later floors are also too easy with too few enemies.
    - From the Drowned March on, the party is **4 heroes**, so a room should hold **at least 2-3 enemies**. That keeps the action economy close: today 4 heroes act for every 1 enemy turn.
    - Make enemy count scale with party size (or set per-level minimums), and check the spawn tables and Difficulty in the balance pass. Probably the same root cause as #10 (the model's reference party wears no gear), plus the model may not weight the action-economy gap of 4 vs 1.
14. **New art for the stairs and treasure chest, with variants.** Both current sprites look out of place next to the rest of the art.
    - **Chests:** several variants (e.g. by rarity or reward size, or simply for variety), plus **level-themed** ones, such as a sunken, barnacled chest for the Drowned March.
    - **Stairs:** a new sprite, and probably themed per level too.
    - Needs a way to pick the art per level (e.g. a field on the level or run definition) and per chest variant. Follow `docs/PIXEL_ART.md` (PixelLab; match the size of the sprite being replaced).
15. **Poison Dart and Hunter's Mark share one icon, and it looks old and ugly.** Make a new, distinct icon for each. Worth a sweep for other abilities sharing an icon at the same time (e.g. group `MagicSO.Icon` references).
16. ✅ *Done 2026-10-26: a lot you can build or raise now gets a lit hammer on a gold disc and a gold name; one on offer but unaffordable keeps a dimmed hammer.* **Hub: show when a building can be built.** Today nothing in the town says "you can afford this now". Add a visible cue on the building plot when its cost (gold/materials) is met, e.g. a glow, a marker or a pulsing icon. Consider the same cue for affordable sphere-grid nodes and Merchant upgrades.
17. ✅ *Done 2026-10-26 (it was the defeat screen): rows cut, button fits, "banked" removed from all player text incl. the tutorial.* **Run end screens (victory / defeat) cleanup.**
    - **BUG:** the "Return to Town" button is off-screen on the victory screen.
    - Remove the "This run: X felled, X fights won" stats line, and the "Fell to ..." line too. The owner doesn't care about them.
    - XP line: show just **"100 XP Gained"**.
    - **Never use the word "banked" in player-facing text**, anywhere in the game (sweep the UXML/strings for it).
18. **Hero sprites have inconsistent proportions.** The Rogue, Ranger and Warrior have bigger (chibi-style) heads than the Paladin. Audit every hero side by side and settle on one proportion, then regenerate the outliers. Keep them 32x32 (`docs/PIXEL_ART.md`). Check which proportion the majority uses before picking the target.
19. ✅ *Done 2026-10-26 for the sphere grid ("Mire Hide 1/3" in red, a covered one in green); the building panel already had a have/need table; no Merchant item costs materials yet.* **Material costs: colour each ingredient by whether you have it.** On the sphere grid (seen buying the Cairn Golem), the cost line "Needs 2 Void Shards · 3 Mire Hide" is all red, so you can't tell which ingredient is short. Colour each one separately: red if you don't have enough, green (or neutral) if you do. Showing "have/need" counts would help too (e.g. "Mire Hide 1/3"). Apply it everywhere a material cost appears: grid nodes, hub buildings, Merchant upgrades.
20. ✅ *Done 2026-10-26 (the run history is still recorded, just not shown).* **Remove the "Your attempts" section** from the run selection screen (campaign map / run start), the one listing where you died before. It isn't needed. Fits #17: the owner doesn't want death/attempt stats shown.
21. **Combat speed setting (x1 / x2 / x3).** A player option in the Options menu (title screen, next to the audio settings), saved like `Audio.json`. It speeds up combat animations, projectiles and delays. For playtesting, and likely kept as a general setting.
    - Watch out: `Time.timeScale` also speeds up anything time-based outside combat, and floating text or UI tweens may use unscaled time and not speed up. Maybe also a hotkey to cycle the speed mid-fight.
22. **TO VERIFY** *(2026-10-26: code read, nothing found; the save on this machine predates the playtest)*: the Rogue seemed unlocked, then was lost after wiping on a LATER floor.**
    - How it should work: a rescued hero joins the live party at once, but ownership is **deferred** (`Party.MarkOwnedDeferred`) and written to disk only when that floor is cleared (`DungeonManager.OnDungeonCleared` → `Party.CommitProgress`). Once the rescue floor is cleared, a wipe on a later floor should not remove the hero. The code read on 2026-10-26 found nothing that undoes it.
    - Likely explanations to rule out: (a) the wipe happened on the same floor as the rescue, which forfeits the hero by design (and the player isn't told); (b) the Rogue is owned but not **fielded**, because the party cap was full or the campfire selection doesn't include them, so they look gone; (c) a real bug, e.g. something rewriting `PartySaveData` from a stale copy on death.
    - Next step: check `savedata/Party.json` (`OwnedHeroKeys`, `SelectedHeroKeys`) on the owner's save, then reproduce in the sandbox: rescue, clear the floor, die on the next one.
    - If (a): make the forfeit visible, e.g. the defeat screen says "the Rogue was not saved", or the rescue text warns that the hero is only kept once the floor is cleared.
