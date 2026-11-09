# The bot: playing the real game without anyone at the keyboard

`Assets/Scripts/Sandbox/Bot/` plays whole playthroughs in play mode: a new save, floors, fights,
events, the town, the next run. It is the real game at high speed, so it tests combat, events, saves
and the hub. **It does not test the keyboard or how a fight feels.** An early-campaign playthrough
takes about a minute and a half.

No LLM is in the loop. An agent launches a batch, waits, and reads one JSON report, so a batch costs
three tool calls rather than one per button press.

## Running a batch

From a script or the Unity MCP (scripts must be compiled, and no play mode running):

```csharp
Assets.Scripts.Sandbox.Editor.BotLauncher.Launch(new BotSettings
{
    Games = 5,                                   // per profile, each from a new save
    FirstSeed = 2000,
    ReportName = "early-runs",
    Runs = BotLauncher.EarlyRuns,                // run asset names, in play order
    Profiles = BotLauncher.AllProfiles(),
});
```

**Two sizes** (owner, 2026-11-01). Most retests want the first few runs: `BotLauncher.EarlySettings(name)`, or
`Tools ▸ Bot ▸ Early Runs`, or `Runs` set by hand with a run or two added. A start-to-end check is
`BotLauncher.FullCampaignSettings(name)` or `Tools ▸ Bot ▸ Full Campaign`: every run in play order, challenge runs
last, and `PassOverWalls` on, so a run it cannot beat in `MaxAttemptsPerRun` tries is left behind and named in
`Finished` rather than ending the game. Set `Games` and `Profiles` on the result to change the size. The bot never
revisits a run, so the late floors meet a party with less XP than a player who replays would have.

**The model's party:** `BotLauncher.ModelPartySettings(name, seed, runs...)` plays each run alone with the party the
balance model measures it against (heroes, nodes, gear, belt), floor by floor as usual, on the closest profile to the
simulator. `Games` is the tries per run. A gap against `BalanceAnalyzer.SimulateRun` is then the model's, not the
party's. The bot still plays no buffs, and the model does; its summon play is the bot's own, not the model's.

Play stops by itself when the batch ends. The report is rewritten after every attempt at
`persistentDataPath/bot_reports/<ReportName>.json`, so a batch that dies keeps what it had:

```
python tools/bot_summary.py "<path to the report>"
```

Do not edit scripts while it runs: a recompile resets the save-folder override.

## Profiles

A profile is an event habit and a route.

| event habit | |
|---|---|
| `Everything` | every event, whatever the odds; the gamble over the sure thing |
| `OddsOnly` | gambles at 50% or better; sure things unless they mean a fight |
| `Safe` | sure things with nothing to lose, or a gamble at 80% or better |

| route | |
|---|---|
| `ClearEverything` | every room, chest and allowed event; a refuge is gone back to when hurt |
| `ToTheExit` | presses on until it finds the stairs. It uses what is in rooms it passes and makes no detour. It does not know where the stairs are |

| grid plan (`Grid`) | |
|---|---|
| `CheapestFirst` | the default: `SphereGridOps.GreedySpend`, the balance model's spend |
| `SummonFirst` | walks one branch to a summon as fast as possible (`SphereGridOps.PathToSummon`): XP is saved for the next node on that path. `FocusSummon` names the summon ("CairnGolem") for the hero whose grid teaches it; every other hero walks to its cheapest one. XP is only saved while the summon's materials and Essence are in hand; without them it walks what the bank pays for and buys cheapest-first with the rest (hoarding for a node it could not pay for cost a game four wipes at the Counting Room). Once the summon is owned it buys cheapest-first. A boss fight with no summon called records each hero's summons and charges (`SummonNote`). The report's town visits say how many path nodes were bought and which waited (`SummonPathNodes`, `SummonPathBlocked`) |

`BotLauncher.SummonFirstProfiles("CairnGolem")` is every profile on that plan (2026-11-09).

`GearGoldShare` (0.5, the balance model's own assumption) splits the gold in hand between gear and
buildings. The odds used are the true chance, which can be better than the player can read.

## What it plays

- **Fights** (`BotCombat`) in the order of the balance model's Adaptive policy, so a gap between the
  bot and the model is the model's and not a different way of playing: heal whoever is at 45% or
  under, Ultra when ready, damaging abilities while charges last, attack the weakest.
- **Summons** (2026-11-09, `BotCombat.TrySummon` / `ActAsSummon`): a hero calls its first usable summon in a
  boss fight, or when the party is down to half its health; a summon is once per level, so that is when it is
  worth it. A revive is called only with someone fallen. The summoned unit uses its Signature whenever it is
  ready; a move that mends or shields it at 45% health or under; a move that does no damage (a buff, a debuff)
  it has not used in its last 3 turns; a damaging move (the one that hits everyone when there are several
  enemies); else its Attack. It never dismisses. Fights report `SummonsUsed`, `SummonTurns`,
  `SummonSignatures` and `SummonMoves`, and the summary prints them.
- **Potions** by the model's rules: at 45% or under, between rooms as well as in fights, keeping a reserve for
  the floors ahead that it opens only for a hero at 20% or under (`PotionUse`, `BotPlayer.TryDrink`).
- **Town** (`BotHub`): places what it can pay for, fields everyone the party cap allows, buys and
  wears gear, spends XP with `SphereGridOps.GreedySpend`, starts the next open run on the list.
- **It retries** a run it wiped on, like a player, up to `MaxAttemptsPerRun`.
- **Quests** (`BotHub.Quests`, 2026-11-02): once the tavern is built it collects every finished quest,
  checks the payout against the quest (item count, gold, XP on every owned hero), takes everything on the
  board and rerolls only revisit quests, since it never revisits. Each town visit reports `QuestsCollected`,
  `QuestMismatches`, the board and a `QuestLog` line per accept, reroll and collect.

**Not played yet:** the heroes' own non-damaging abilities (buffs, debuffs, shields), combo upgrades,
the potion belt, swapping abilities by hand, revisits (it retries a run it wiped on, but never replays one it cleared). Expect it to play a little worse than a
player in those respects, and to have no judgement: it never saves an Ultra or charges for a boss.

## How it is wired

- `BotLauncher` leaves the batch in `SessionState`; `BotSession` reads it before the first scene,
  points every save at `persistentDataPath/savedata_bot` and starts `BotPlayer`, the same pattern as
  the sandbox. It is editor-only, so it cannot ship and cannot touch the real save.
- `BotPlayer` survives scene loads and looks each frame at what is on screen. `RoomActionUI`'s `BOT`
  region is the seam: what is up, and what a press does. It calls the handlers the buttons call.
- **Stuck detection counts progress, not movement** (rooms seen, windows, turns). Walking does not
  reset it, or a bot pacing between two rooms never counts as stuck. A stuck game goes in the report
  with its state, and the batch moves on.

## Found by it so far

- **A side room trapped the party.** `_doorsLive` needed at least one door and a side room has none,
  so walking, Enter and clicks all stopped inside one (fixed 2026-10-31).
