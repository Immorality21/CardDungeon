using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Events;
using Assets.Scripts.Heroes;
using Assets.Scripts.Hub;
using Assets.Scripts.IO;
using Assets.Scripts.Items;
using Assets.Scripts.Progression;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Sandbox.Bot
{
    /// <summary>
    /// A visit to town, through the same rules the screens use: places what it can pay for, fields
    /// everyone the party cap allows, buys and wears gear, spends XP the way the balance model does
    /// (<c>SphereGridOps.GreedySpend</c>), and points the run save at the next run.
    ///
    /// <para><b>Not played:</b> combo upgrades, the potion belt and swapping abilities by hand.</para>
    /// </summary>
    public static class BotHub
    {
        public static void Visit(PartyRosterSO roster, BotProfile profile, BotHubVisit visit)
        {
            var hub = UnityEngine.Resources.Load<HubSO>(HubSO.ResourcePath);
            int goldForGear = Mathf.RoundToInt(MetaProgressManager.Instance.Gold * profile.GearGoldShare);

            Build(hub, profile, visit, MetaProgressManager.Instance.Gold - goldForGear);
            if (IsBuilt(hub, HubService.Tavern))
            {
                Quests(roster, visit);
            }
            var heroes = Field(roster);
            if (IsBuilt(hub, HubService.Forge))
            {
                Craft(visit);
            }
            if (IsBuilt(hub, HubService.Merchant))
            {
                BuyGear(heroes, Mathf.Min(goldForGear, MetaProgressManager.Instance.Gold), visit);
            }
            WearBest(heroes, visit);
            InventoryManager.Instance.Save();
            if (IsBuilt(hub, HubService.SphereGrid))
            {
                SpendXp(heroes, profile, visit);
            }

            visit.GoldLeft = MetaProgressManager.Instance.Gold;
            visit.XpLeft = heroes.Sum(h => HeroRoster.GetHeroSave(h)?.CurrentXp ?? 0);
            if (!MetaProgressManager.Instance.TutorialFinished)
            {
                MetaProgressManager.Instance.FinishTutorial();
            }
        }

        /// <summary>The first listed run that is open and not yet cleared; null when there is none.</summary>
        public static RunDefinitionSO NextRun(BotSettings settings, PartyRosterSO roster, ICollection<string> passedOver)
        {
            var campaign = UnityEngine.Resources.Load<CampaignSO>(CampaignSO.ResourcePath);
            var completed = MetaProgressManager.Instance.GetCompletedRunKeys();
            var owned = HeroRoster.GetOwnedKeys(roster);
            foreach (var name in settings.Runs)
            {
                var node = campaign.Nodes.FirstOrDefault(n => n != null && n.Run != null && n.Run.name == name);
                if (node == null || passedOver.Contains(name))
                {
                    continue;
                }
                var key = CampaignOps.RunKeyOf(node.Run);
                if (!completed.Contains(key) && CampaignOps.IsUnlocked(node, completed, owned))
                {
                    return node.Run;
                }
            }
            return null;
        }

        /// <summary>The run asset of that name on the campaign map.</summary>
        public static RunDefinitionSO RunNamed(string name)
        {
            var campaign = UnityEngine.Resources.Load<CampaignSO>(CampaignSO.ResourcePath);
            return campaign.Nodes.FirstOrDefault(n => n != null && n.Run != null && n.Run.name == name)?.Run;
        }

        /// <summary>Writes the balance model's party into this save: the heroes, their nodes, their gear
        /// and the belt it assumes. Through the save, so the dungeon boots as it would for a player.</summary>
        public static void FieldModelParty(BotModelRun model)
        {
            var files = new FileHandler();
            var party = files.Load<PartySaveData>();
            party.Heroes.Clear();
            party.OwnedHeroKeys.Clear();
            party.SelectedHeroKeys.Clear();
            foreach (var hero in model.Heroes)
            {
                party.Heroes.Add(new HeroSaveData { HeroKey = hero.Key, ActivatedNodes = new List<string>(hero.Nodes) });
                party.OwnedHeroKeys.Add(hero.Key);
                party.SelectedHeroKeys.Add(hero.Key);
            }
            files.Save(party);

            var inventory = InventoryManager.Instance;
            foreach (var hero in model.Heroes)
            {
                foreach (var key in hero.Gear)
                {
                    var item = inventory.GetItemSO(key);
                    if (item == null)
                    {
                        continue;
                    }
                    inventory.AddItem(item, 1, EconomySource.LevelClear);
                    var entry = inventory.GetBagEquipment().FirstOrDefault(e => e.ItemKey == key && string.IsNullOrEmpty(e.EquippedHeroKey));
                    if (entry != null)
                    {
                        inventory.Equip(entry, item.SlotType, hero.Key);
                    }
                }
            }

            var healing = PotionBeltSO.Load()?.Find(PotionBeltState.HealingKey);
            if (healing != null)
            {
                PotionBeltState.SetCapacity(healing, model.HealingPotions);
                for (int i = PotionBeltState.Potency(healing); i < model.PotencySteps; i++)
                {
                    PotionBeltState.AddPotency(healing);
                }
            }
        }

        /// <summary>What <c>HubManager.OnRunChosen</c> and <c>OnEnterDungeon</c> leave behind.</summary>
        public static void BeginRun(RunDefinitionSO run)
        {
            new FileHandler().Save(new RunSaveData { RunKey = CampaignOps.RunKeyOf(run), CurrentLevelIndex = 0 });
            DungeonManager.ActiveRun = run;
            DungeonManager.RunLevelIndex = 0;
            DungeonManager.LevelToLoad = run.Levels[0].LevelTemplate;
            DungeonManager.SeedToLoad = null;
        }

        private static bool IsBuilt(HubSO hub, HubService service)
        {
            var progress = HubState.Progress();
            return hub.Buildings.Any(b => b != null && b.Service == service && BuildingOps.IsBuilt(b, progress));
        }

        private static void Build(HubSO hub, BotProfile profile, BotHubVisit visit, int goldForBuildings)
        {
            var built = new List<string>();
            foreach (var building in BuildingOps.InDrawOrder(hub))
            {
                var progress = HubState.Progress();
                int next = BuildingOps.NextLevel(building, progress);
                if (next <= 0)
                {
                    continue;
                }
                if (BuildingOps.CanPlace(building, progress))
                {
                    if (!InventoryManager.Instance.CanAfford(building.PlacementCost)
                        || !InventoryManager.Instance.SpendMaterials(building.PlacementCost))
                    {
                        continue;
                    }
                }
                else
                {
                    int cost = BuildingOps.UpgradeCost(building, progress);
                    if (cost > goldForBuildings
                        || !MetaProgressManager.Instance.TrySpendGold(cost, EconomySource.Building))
                    {
                        continue;
                    }
                    goldForBuildings -= cost;
                }
                MetaProgressManager.Instance.SetBuildingLevel(building.SaveKey, next);
                built.Add(building.SaveKey + " " + next);
            }
            visit.Built = string.Join(", ", built);
        }

        /// <summary>
        /// The tavern: collects every finished quest, checking the payout against what the quest
        /// promises (the item, the gold, the XP on every owned hero), then takes every quest on the
        /// board. Never rerolls.
        /// </summary>
        private static void Quests(PartyRosterSO roster, BotHubVisit visit)
        {
            var save = Assets.Scripts.Hub.Quests.QuestRewards.LoadBoard();
            var pool = Assets.Scripts.Hub.Quests.QuestRewards.Pool();
            foreach (var quest in Assets.Scripts.Hub.Quests.QuestOps.BoardQuests(save, pool))
            {
                if (Assets.Scripts.Hub.Quests.QuestOps.StateOf(save, quest) != Assets.Scripts.Hub.Quests.QuestState.Ready)
                {
                    continue;
                }
                var owned = HeroRoster.GetOwnedHeroes(roster);
                var xpBefore = owned.Select(h => HeroRoster.GetHeroSave(h)?.CurrentXp ?? 0).ToList();
                int goldBefore = MetaProgressManager.Instance.Gold;
                int itemBefore = ItemCount(quest.RewardItem);
                bool known = quest.RewardItem != null && InventoryManager.Instance.AlreadyKnown(quest.RewardItem);

                if (!Assets.Scripts.Hub.Quests.QuestRewards.Collect(quest, roster))
                {
                    visit.QuestLog.Add($"{quest.Key}: COLLECT FAILED");
                    continue;
                }

                int gold = MetaProgressManager.Instance.Gold - goldBefore;
                int items = ItemCount(quest.RewardItem) - itemBefore;
                var xpAfter = owned.Select(h => HeroRoster.GetHeroSave(h)?.CurrentXp ?? 0).ToList();
                var xpGains = xpAfter.Select((x, i) => x - xpBefore[i]).ToList();
                int wantItems = quest.RewardItem == null || known ? 0 : quest.RewardItemCount;
                bool ok = gold == quest.RewardGold && items == wantItems && xpGains.All(g => g == quest.RewardXp);
                visit.QuestsCollected++;
                visit.QuestLog.Add($"{quest.Key}: collected - {(quest.RewardItem != null ? quest.RewardItem.Key : "no item")} +{items} (want {wantItems}), "
                                   + $"gold +{gold} (want {quest.RewardGold}), XP +{string.Join("/", xpGains)} on {owned.Count} heroes (want {quest.RewardXp} each) "
                                   + (ok ? "OK" : "MISMATCH"));
                if (!ok)
                {
                    visit.QuestMismatches++;
                }
            }

            // The bot never revisits a run, so a revisit-only quest would hold its slot forever: reroll it.
            save = Assets.Scripts.Hub.Quests.QuestRewards.LoadBoard();
            foreach (var quest in Assets.Scripts.Hub.Quests.QuestOps.BoardQuests(save, pool))
            {
                if (quest.RevisitOnly
                    && Assets.Scripts.Hub.Quests.QuestOps.StateOf(save, quest) == Assets.Scripts.Hub.Quests.QuestState.Offered)
                {
                    int goldBefore = MetaProgressManager.Instance.Gold;
                    bool rerolled = Assets.Scripts.Hub.Quests.QuestRewards.Reroll(quest);
                    visit.QuestLog.Add(rerolled
                        ? $"{quest.Key}: rerolled for {goldBefore - MetaProgressManager.Instance.Gold} gold"
                        : $"{quest.Key}: could not reroll (gold {goldBefore})");
                }
            }

            save = Assets.Scripts.Hub.Quests.QuestRewards.LoadBoard();
            foreach (var quest in Assets.Scripts.Hub.Quests.QuestOps.BoardQuests(save, pool))
            {
                if (Assets.Scripts.Hub.Quests.QuestOps.StateOf(save, quest) == Assets.Scripts.Hub.Quests.QuestState.Offered
                    && !quest.RevisitOnly
                    && Assets.Scripts.Hub.Quests.QuestRewards.Accept(quest))
                {
                    visit.QuestLog.Add($"{quest.Key}: accepted");
                }
            }
            save = Assets.Scripts.Hub.Quests.QuestRewards.LoadBoard();
            visit.QuestBoard = string.Join(", ", Assets.Scripts.Hub.Quests.QuestOps.BoardQuests(save, pool).Select(q =>
                $"{q.Key} {Assets.Scripts.Hub.Quests.QuestOps.ProgressOf(save, q)}/{q.Count}"));
        }

        private static int ItemCount(ItemSO item)
        {
            return item == null ? 0 : InventoryManager.Instance.GetItems().Where(i => i.ItemKey == item.Key).Sum(i => i.Quantity);
        }

        private static List<HeroSO> Field(PartyRosterSO roster)
        {
            int cap = MetaProgressManager.Instance.GetPartyCap();
            HeroRoster.SetSelectedKeys(roster, HeroRoster.GetOwnedKeys(roster), cap);
            return HeroRoster.GetSelectedHeroes(roster, cap);
        }

        private static void SpendXp(List<HeroSO> heroes, BotProfile profile, BotHubVisit visit)
        {
            foreach (var hero in heroes.Where(h => h != null && h.SphereGrid != null))
            {
                var save = HeroRoster.GetHeroSave(hero);
                if (save == null)
                {
                    continue;
                }
                if (profile != null && profile.Grid == BotGridPlan.SummonFirst && !WalkToSummon(hero, profile, visit))
                {
                    continue;   // saving XP for the next step on the path
                }
                save = HeroRoster.GetHeroSave(hero);
                var already = save.ActivatedNodes ?? new List<string>();
                var plan = SphereGridOps.GreedySpend(hero.SphereGrid, already, save.CurrentXp, out _,
                    EquippedMagicState.DefaultSlotCount);
                foreach (var key in plan.Where(k => !already.Contains(k)).ToList())
                {
                    if (!HeroRoster.TryActivateNode(hero, key))
                    {
                        break;
                    }
                    visit.NodesBought++;
                }
            }
        }

        /// <summary>
        /// Buys along the path to the hero's summon (<see cref="BotGridPlan.SummonFirst"/>). False when it
        /// stopped to save XP for the next step - the rest of the bank waits for it. True when the summon is
        /// owned, there is none, or the summon's materials or Essence are not in hand (XP is not what is
        /// short, so the caller spends the bank cheapest-first: a party that hoards XP for a node it cannot
        /// pay for fell 4 times at the Counting Room, 2026-11-09).
        /// </summary>
        private static bool WalkToSummon(HeroSO hero, BotProfile profile, BotHubVisit visit)
        {
            string focus = string.IsNullOrEmpty(profile.FocusSummon)
                || !hero.SphereGrid.Nodes.Any(n => n != null && n.Kind == SphereNodeKind.Summon && n.GrantedSummonKey == profile.FocusSummon)
                ? null : profile.FocusSummon;
            var save = HeroRoster.GetHeroSave(hero);
            var path = SphereGridOps.PathToSummon(hero.SphereGrid, save.ActivatedNodes, focus);
            if (path.Count == 0)
            {
                return true;
            }
            // Saving XP is only worth it when XP is what stands between the hero and the summon: when its
            // materials or Essence are not in hand, walk what the bank pays for and spend the rest.
            var summonNode = SphereGridOps.FindNode(hero.SphereGrid, path[path.Count - 1]);
            bool priceInHand = (!SphereGridOps.HasMaterialCost(summonNode) || HeroRoster.CanPayMaterials(summonNode))
                && (!SphereGridOps.HasEssenceCost(summonNode) || HeroRoster.CanPayEssence(summonNode));
            if (!priceInHand)
            {
                visit.SummonPathBlocked = (visit.SummonPathBlocked + " " + hero.Key + ":" + summonNode.Key + "(materials)").Trim();
            }
            foreach (var key in path)
            {
                var node = SphereGridOps.FindNode(hero.SphereGrid, key);
                save = HeroRoster.GetHeroSave(hero);
                if (save.CurrentXp < node.XpCost)
                {
                    return !priceInHand;
                }
                if (!HeroRoster.TryActivateNode(hero, key))
                {
                    visit.SummonPathBlocked = (visit.SummonPathBlocked + " " + hero.Key + ":" + key).Trim();
                    return true;
                }
                visit.NodesBought++;
                visit.SummonPathNodes++;
            }
            return true;
        }

        private static void BuyGear(List<HeroSO> heroes, int budget, BotHubVisit visit)
        {
            var meta = MetaProgressManager.Instance;
            var inventory = InventoryManager.Instance;
            if (meta.GetShopStock().Count == 0)
            {
                meta.SetShopStock(MerchantUI.GenerateStock(MerchantUI.StockSize));
            }

            foreach (var key in meta.GetShopStock().ToList())
            {
                var item = inventory.GetItemSO(key);
                if (item == null || item.Category != ItemCategory.Equipment)
                {
                    continue;
                }
                int price = ShopPricing.BuyPrice(item);
                if (price > budget || !heroes.Any(h => Score(item) > WornScore(item.SlotType, h))
                    || !meta.TrySpendGold(price, EconomySource.Merchant))
                {
                    continue;
                }
                inventory.AddItem(item, 1, EconomySource.Merchant);
                meta.RemoveFromShopStock(key);
                budget -= price;
                visit.ItemsBought++;
                visit.GoldSpentOnGear += price;
            }
        }

        /// <summary>Makes one of everything it can, best first, skipping the Forge's commons: crafting
        /// is the game's main gold sink, so it goes before the shop.</summary>
        private static void Craft(BotHubVisit visit)
        {
            var inventory = InventoryManager.Instance;
            var recipes = CraftingOps.Recipes(inventory.AllItems())
                .Where(r => CraftingOps.IsKnown(r, inventory.GetItems()) && r.CraftResult.Rarity != ItemRarity.Common)
                .OrderByDescending(r => r.CraftResult.ItemLevel);
            foreach (var recipe in recipes.ToList())
            {
                bool owned = inventory.GetItems().Any(i => i.ItemKey == recipe.CraftResult.Key);
                if (!owned && inventory.TryCraft(recipe, out _))
                {
                    visit.ItemsCrafted++;
                    visit.GoldSpentOnCrafting += recipe.CraftGold;
                }
            }
        }

        private static void WearBest(List<HeroSO> heroes, BotHubVisit visit)
        {
            var inventory = InventoryManager.Instance;
            foreach (var hero in heroes)
            {
                foreach (var entry in inventory.GetBagEquipment().ToList())
                {
                    var item = inventory.GetItemSO(entry.ItemKey);
                    if (item != null && string.IsNullOrEmpty(entry.EquippedHeroKey)
                        && Score(item) > WornScore(item.SlotType, hero))
                    {
                        inventory.Equip(entry, item.SlotType, hero.SaveKey);
                        visit.ItemsEquipped++;
                    }
                }
            }
        }

        private static float WornScore(SlotType slot, HeroSO hero)
        {
            var worn = InventoryManager.Instance.GetEquipped(slot, hero.SaveKey);
            var item = worn != null ? InventoryManager.Instance.GetItemSO(worn.ItemKey) : null;
            return item != null ? Score(item) : 0f;
        }

        /// <summary>A plain read of an item, the way a player compares numbers: health counts for less
        /// because it comes in bigger numbers.</summary>
        private static float Score(ItemSO item)
        {
            float score = 0f;
            foreach (var bonus in item.Bonuses)
            {
                if (bonus != null)
                {
                    score += bonus.Value * (bonus.StatType == StatType.MaxHealth ? 0.25f : 1f);
                }
            }
            return score;
        }
    }
}
