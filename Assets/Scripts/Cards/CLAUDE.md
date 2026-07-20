# Card System (`Assets.Scripts.Cards`)

- **CardSO** (ScriptableObject): defines a card with `Key`, `DisplayName`, `Description`, `Icon`, `TargetType` (Enemy/Ally/Self/AllEnemies/AllAllies), `Rarity`, `Effects` (list of `CardEffect`), `Tags` (list of `CardTag` enum values), `TagDuration`.
- **CardEffect**: `EffectType` (Damage/Heal/Buff/Debuff), `Power`, `DamageType` (Normal/Fire/Ice/Lightning/Holy/Shadow), `BuffType`, `Duration`.
- **CardCollectionManager** (singleton): manages the player's card collection. Cards are added as loot (50% drop chance via `TryDropCard`) or bought at the merchant (`GetRandomCard`). Cards are assigned to heroes (max 5 per hero deck). Persisted via `CardCollectionSaveData`.
- **DungeonDeckState**: tracks which cards each hero has available during a dungeon run. Cards are single-use per dungeon — `MarkCardUsed` removes availability. Used card state is saved/restored with dungeon saves. **Identifies cards by key string only (no per-copy identity)** — which is why permanent card upgrades are keyed per card type; see the Progression guide.
- **CardEffectCalculator**: executes card effects using the strategy pattern (`IEffectExecutor` per `CardEffectType`). Also handles combo detection and combo bonus effects. `Execute(..., powerBonus)` folds a meta card-upgrade bonus into a *copy* of each Damage/Heal effect (Buff/Debuff power unaffected; `CardSO` never mutated), keeping it pure/testable.
- **Combo system**: `CardComboSO` defines combos with `RequiredTags` and `BonusEffects`. `ComboDetector` checks if playing a card's tags on a target (which already has tags from previous cards) triggers a combo. `CardTagTracker` tracks active tags on units with durations.
- **Buff system**: `CombatBuffTracker` tracks stat buffs (Attack/Defense/Agility) and status effects (Frozen, resistances) with turn-based durations. `BuffType` enum includes stat buffs, elemental resistances, and status effects.

## UI (`Cards/UI`)

- **DeckManagementUI** — assign/unassign cards to hero decks between dungeons; spawns card entries at runtime from a prefab.
- **CardUpgradeUI** — the hub "Forge"; spends Essence to upgrade cards. See the Progression guide.
