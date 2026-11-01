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
    /// <para><b>Not played:</b> the Forge, crafting, the potion belt and swapping abilities by hand.</para>
    /// </summary>
    public static class BotHub
    {
        public static void Visit(PartyRosterSO roster, BotProfile profile, BotHubVisit visit)
        {
            var hub = UnityEngine.Resources.Load<HubSO>(HubSO.ResourcePath);
            int goldForGear = Mathf.RoundToInt(MetaProgressManager.Instance.Gold * profile.GearGoldShare);

            Build(hub, profile, visit, MetaProgressManager.Instance.Gold - goldForGear);
            var heroes = Field(roster);
            if (IsBuilt(hub, HubService.Merchant))
            {
                BuyGear(heroes, goldForGear, visit);
            }
            WearBest(heroes, visit);
            InventoryManager.Instance.Save();
            if (IsBuilt(hub, HubService.SphereGrid))
            {
                SpendXp(heroes, visit);
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

        private static List<HeroSO> Field(PartyRosterSO roster)
        {
            int cap = MetaProgressManager.Instance.GetPartyCap();
            HeroRoster.SetSelectedKeys(roster, HeroRoster.GetOwnedKeys(roster), cap);
            return HeroRoster.GetSelectedHeroes(roster, cap);
        }

        private static void SpendXp(List<HeroSO> heroes, BotHubVisit visit)
        {
            foreach (var hero in heroes.Where(h => h != null && h.SphereGrid != null))
            {
                var save = HeroRoster.GetHeroSave(hero);
                if (save == null)
                {
                    continue;
                }
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
