"""Summarises a bot report (docs/BOT.md): python tools/bot_summary.py <report.json>"""
import json
import sys
from collections import defaultdict


def mean(values):
    return sum(values) / len(values) if values else 0.0


report = json.load(open(sys.argv[1]))
games = defaultdict(list)
for game in report["Games"]:
    games[game["Profile"]].append(game)

print(f"{report['Status']}: {len(report['Games'])} games\n")
print(f"{'profile':34}{'games':>6}{'done':>6}{'gave up':>8}{'stuck':>6}{'wipes':>7}{'attempts':>9}{'minutes':>8}")
for profile, played in games.items():
    attempts = [run for game in played for run in game["Runs"]]
    wipes = [run for run in attempts if run["Outcome"].startswith("wiped")]
    print(f"{profile:34}{len(played):>6}"
          f"{sum(g['Finished'].startswith('every') for g in played):>6}"
          f"{sum(g['Finished'].startswith('gave') for g in played):>8}"
          f"{sum(bool(g['Stuck']) for g in played):>6}"
          f"{len(wipes):>7}{len(attempts):>9}{sum(g['RealSeconds'] for g in played) / 60:>8.1f}")

for game in report["Games"]:
    if game["Stuck"]:
        print(f"\nSTUCK {game['Profile']} seed {game['Seed']}: {game['Stuck']}")

# Per floor, across every profile, in the order the floors were first played.
floors = {}
for game in report["Games"]:
    for run in game["Runs"]:
        for floor in run["Floors"]:
            floors.setdefault((run["Run"], floor["Level"]), []).append((game["Profile"], run, floor))

print(f"\n{'run / floor':46}{'plays':>6}{'wipes':>6}{'end hp':>8}{'rooms':>7}")
for (run_name, level), plays in floors.items():
    wiped = [p for p in plays if p[1]["Outcome"] == "wiped on " + level]
    ended = [p[2]["HealthAtEnd"] for p in plays if p[1]["Outcome"] != "wiped on " + level]
    share = mean([p[2]["RoomsSeen"] / max(1, p[2]["RoomsTotal"]) for p in plays])
    print(f"{run_name + ' / ' + level:46}{len(plays):>6}{len(wiped):>6}{mean(ended):>8.0%}{share:>7.0%}")

print(f"\n{'boss fights':30}{'fights':>7}{'lost':>6}{'hp in':>7}{'hp out':>8}{'down':>6}{'turns':>7}{'potions':>8}")
bosses = defaultdict(list)
for game in report["Games"]:
    for run in game["Runs"]:
        for floor in run["Floors"]:
            for fight in floor["Fights"]:
                if fight["Boss"]:
                    bosses[floor["Level"]].append(fight)
for level, fights in bosses.items():
    won = [f for f in fights if f["Outcome"] != "PlayerDied"]
    print(f"{level:30}{len(fights):>7}{len(fights) - len(won):>6}"
          f"{mean([f['HealthBefore'] for f in fights]):>7.0%}{mean([f['HealthAfter'] for f in won]):>8.0%}"
          f"{mean([f['HeroesDown'] for f in fights]):>6.1f}{mean([f['HeroTurns'] for f in fights]):>7.1f}"
          f"{mean([f['PotionsUsed'] for f in fights]):>8.1f}")

fights = [f for game in report["Games"] for run in game["Runs"] for floor in run["Floors"] for f in floor["Fights"]]
summoned = [f for f in fights if f.get("SummonsUsed", 0) > 0]
if summoned:
    print(f"\n{'summons':30}{'fights':>7}{'lost':>6}{'boss':>6}{'turns':>7}{'sig':>6}{'moves':>7}")
    print(f"{'fights with a summon':30}{len(summoned):>7}{sum(f['Outcome'] != 'Victory' for f in summoned):>6}"
          f"{sum(f['Boss'] for f in summoned):>6}{sum(f.get('SummonTurns', 0) for f in summoned):>7}"
          f"{sum(f.get('SummonSignatures', 0) for f in summoned):>6}{sum(f.get('SummonMoves', 0) for f in summoned):>7}")
paths = [(game["Profile"], h.get("Before", ""), h.get("SummonPathNodes", 0), h.get("SummonPathBlocked", ""))
         for game in report["Games"] for h in game["Hub"] if h.get("SummonPathNodes", 0) or h.get("SummonPathBlocked")]
for profile, before, bought, blocked in paths:
    print(f"  {profile}: before {before}: {bought} nodes on the summon path" + (f", waiting on {blocked}" if blocked else ""))

drunk = sum(floor.get("PotionsBetweenRooms", 0) for game in report["Games"] for run in game["Runs"] for floor in run["Floors"])
print(f"\npotions drunk between rooms: {drunk}")

print(f"\n{'event habit: taken / seen':34}{'taken':>7}{'declined':>9}{'avg odds taken':>16}")
events = defaultdict(list)
for game in report["Games"]:
    for run in game["Runs"]:
        for floor in run["Floors"]:
            events[game["Profile"]].extend(floor["Events"])
for profile, seen in events.items():
    taken = [e for e in seen if not e["Declined"]]
    gambles = [e["Chance"] for e in taken if e["Option"] == "StatCheck"]
    print(f"{profile:34}{len(taken):>7}{len(seen) - len(taken):>9}{mean(gambles):>16.0%}")
