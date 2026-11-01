using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Cards.Buffs;
using Assets.Scripts.Combat;
using Assets.Scripts.Combat.Triggers;
using Assets.Scripts.Audio;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Behaviors;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using Assets.Scripts.Progression;
using Assets.Scripts.UnitStats;
using ImmoralityGaming.Fundamentals;
using UnityEngine;
using Assets.Scripts.Events;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Rooms
{
    public enum CombatOutcome
    {
        Continue,
        EnemyDown,
        Victory,
        PlayerDied
    }

    public enum HeroAction
    {
        None,
        Attack,
        Skip,
        Cast,
        UseItem,
        Summon,

        // A party-replacing summon's own commands (§4b). Attack is shared with the heroes.
        SummonAbility,
        Dismiss,

        // A hero's Ultra, once their gauge is full (COMBAT_DEPTH §13).
        Ultra
    }

    public class CombatResult
    {
        public CombatOutcome Outcome;
        public int RemainingEnemies;

        // Rewards earned this combat (for the victory summary). Awards, not items: a drop table
        // can pay out several of a material, and "Ember Iron" and "Ember Iron x3" are different
        // pieces of news.
        public List<LootAward> Loot = new List<LootAward>();
        public int XpGained;
        /// <summary>What each fielded hero was paid out of <see cref="XpGained"/>, by display name -
        /// the split is not always even (the campfire can favour one), so "XP +10" alone could not
        /// say whether it was each or shared.</summary>
        public List<KeyValuePair<string, int>> XpByHero = new List<KeyValuePair<string, int>>();
        public int GoldGained;
        public bool LevelCleared;  // this victory cleared the exit room → level complete
        public bool BossDefeated;  // this victory felled a boss (drives the boss victory copy)
        public bool RunCompleted;  // the boss on the final run level fell → the run is won
    }

    public partial class CombatManager : SingletonBehaviour<CombatManager>
    {
        /// <summary>
        /// A fight cut off by a scene change (a quit from the pause overlay mid-fight) never reaches the
        /// end of <c>RunCombat</c>, so the speed it set would otherwise leak into the hub.
        /// </summary>
        private void OnDestroy()
        {
            if (InCombat)
            {
                CombatSpeed.Apply(false);
            }
        }

        [SerializeField] private float _turnDelay = 0.6f;

        [SerializeField] private float _lungeDistance = 0.3f;

        [SerializeField] private float _lungeDuration = 0.12f;

        // Critical hits (heroes and enemies, basic attacks and abilities that can crit). The numbers
        // live in CritRules so the cast path can reach them; these forward, so existing readers - the
        // balance model among them - keep one source.
        /// <summary>Crit chance at zero Luck. Luck adds to this - see <see cref="CritChanceFor"/>.</summary>
        public const float CritChance = CritRules.BaseChance;

        /// <summary>
        /// Beat held after an over-time tick so the number can be read before the next turn starts.
        /// Shorter than <c>_turnDelay</c> deliberately: a tick is upkeep, not an action, and pausing
        /// a full turn's worth for it would make a three-stack poison feel like a cutscene.
        /// </summary>
        private const float OverTimeTickPause = 0.35f;

        /// <summary>Impact weight of a tick — a fraction of a swing, so the camera barely moves.</summary>
        private const float OverTimeTickPunch = 0.35f;

        /// <summary>Most crit chance Luck can add on top of <see cref="CritChance"/>.</summary>
        public const float MaxLuckCritBonus = CritRules.MaxLuckBonus;

        /// <summary>Luck at which half of <see cref="MaxLuckCritBonus"/> is reached.</summary>
        public const float LuckCritConstant = CritRules.LuckConstant;

        /// <summary>
        /// Crit chance for a specific unit. Everything that rolls or models a crit must go through
        /// this (or <see cref="CritRules.ChanceFor"/>, which it forwards to), or Luck silently does nothing.
        /// </summary>
        public static float CritChanceFor(ICombatUnit unit)
        {
            return CritRules.ChanceFor(unit);
        }

        public const float CritMultiplier = CritRules.Multiplier;

        /// <summary>A turn is over and reported: the UI refreshes the party window. The rules-side
        /// view of the same moment is <see cref="TurnEnded"/> on <see cref="Events"/>.</summary>
        public event Action OnTurnExecuted;

        public event Action<CombatResult> OnCombatEnded;

        public event Action<List<ICombatUnit>> OnTurnOrderChanged;

        public event Action<ICombatUnit> OnHeroTurnStarted;

        public event Action<ICombatUnit, List<MagicSlot>> OnMagicSlotsRequested;

        public event Action<ICombatUnit, List<ICombatUnit>> OnAttackTargetRequested;

        public event Action<ICombatUnit, List<ItemSaveData>> OnItemListRequested;

        public event Action<ICombatUnit, List<ICombatUnit>> OnInspectTargetRequested;

        public event Action OnDungeonCleared;

        /// <summary>A hero has summoned: the UI shows the name banner until <see cref="OnSummonEnded"/>.</summary>
        public event Action<ICombatUnit, SummonSO> OnSummonStarted;

        /// <summary>An enemy uses something other than a plain swing: its name, for the ability banner.</summary>
        public event Action<ICombatUnit, string> OnEnemyAbilityUsed;

        public event Action OnSummonEnded;

        /// <summary>A party-replacing summon wants a single enemy for an ability: the selection UI
        /// picks one and answers with <see cref="SubmitSummonAbility"/>.</summary>
        public event Action<ICombatUnit, MagicSO, List<ICombatUnit>> OnSummonAbilityTargetRequested;

        /// <summary>A hero opens their Ultras. Always a list, even of one - the FFX Overdrive menu:
        /// what the move is and what it will do are read before it is spent.</summary>
        public event Action<ICombatUnit, List<UltraSO>> OnUltraListRequested;

        public void RequestUltraList(ICombatUnit hero, List<UltraSO> ultras)
        {
            OnUltraListRequested?.Invoke(hero, ultras);
        }

        /// <summary>A hero opens their summons - always a list, even of one, as the Ultra command does.</summary>
        public event Action<ICombatUnit, List<SummonSlot>> OnSummonListRequested;

        public void RequestSummonList(ICombatUnit hero, List<SummonSlot> summons)
        {
            OnSummonListRequested?.Invoke(hero, summons);
        }

        [SerializeField] private List<MagicComboSO> _cardCombos;

        public bool InCombat { get; private set; }

        public CombatBuffTracker BuffTracker { get; private set; }

        private TurnManager _turnManager = new TurnManager();

        /// <summary>
        /// This fight's threat: who the enemies are angry at. Fed by every hero-side action (see
        /// <see cref="CreditThreat"/>) and read by every single-target enemy pick.
        /// </summary>
        private readonly ThreatTable _threat = new ThreatTable();

        private HeroAction _pendingAction = HeroAction.None;

        private SpellcastAction _pendingCastAction;

        private int _pendingCastSlot;

        // The pending cast is a MultiCast (the Mage's Ultra): it spends his Focus and repeats.
        private bool _pendingMultiCast;

        private ItemSO _pendingUseItem;

        private ICombatUnit _pendingUseItemTarget;

        private ICombatUnit _pendingAttackTarget;

        private SummonSlot _pendingSummon;

        private MagicSO _pendingSummonAbility;

        private List<ICombatUnit> _pendingSummonTargets;

        // The ally a summon aimed at one ally (or one fallen ally) was pointed at; null for the rest.
        private ICombatUnit _pendingSummonTarget;

        private UltraSO _pendingUltra;

        private ICombatUnit _pendingUltraTarget;

        // Summons that left with a parting blow still to land (Ifrit's Hellfire), cast before the turn closes.
        private readonly List<SummonUnit> _parting = new List<SummonUnit>();

        // ---- Ultras (COMBAT_DEPTH §13): each hero's gauge (kept for the run in
        // ---- DungeonManager.UltraGauges, read when the fight opens and written back on every change),
        // ---- the weighted health credited since the gauge was last settled (UltraOps.Credit), and
        // ---- the form a hero is in while a Transform Ultra lasts.
        private readonly Dictionary<Hero, int> _ultraGauge = new Dictionary<Hero, int>();

        private readonly Dictionary<Hero, float> _gaugeCredit = new Dictionary<Hero, float>();

        // ---- Threat: the net health change of every unit standing when the current hero action
        // ---- began, summed from HealthChange; null between actions.
        private Dictionary<ICombatUnit, int> _threatWindow;

        private readonly Dictionary<Hero, ActiveForm> _forms = new Dictionary<Hero, ActiveForm>();

        /// <summary>A hero's Transform Ultra while it lasts.</summary>
        private class ActiveForm
        {
            public UltraSO Ultra;
            public int TurnsLeft;
            public bool TakenThisTurn;
        }

        // The party-replacing summon on the field: one unit for the Golem, several troops for a squad
        // (the Demon Army), empty while the party fights. While any of it stands it is the whole hero
        // side: see HeroSideUnits. The party comes back when the last of it leaves.
        private readonly List<SummonUnit> _squad = new List<SummonUnit>();

        /// <summary>Whether a party-replacing summon (or what is left of its squad) is on the field.</summary>
        private bool ReplacementOut => _squad.Any(u => u != null && u.IsAlive);

        // Summons fighting beside the party, in two roles kept in two lists - the role is which list a
        // unit is in, never a flag on it. Both are part of the hero side while the party is (see
        // HeroSideUnits); neither keeps a fight going on its own (HasAliveHeroes).
        //
        // Guests (SummonKind.JoinParty): one of each kind per summoner, in the order they arrived. Bound
        // to their summoner, laid out in the vanguard column, sent home by Dismiss, by a replacement
        // summon arriving, or by their summoner falling.
        private readonly List<SummonUnit> _allies = new List<SummonUnit>();

        // Stand-ins (UltraKind.Sacrifice): a horror holding a fallen hero's place for the rest of the
        // fight. Bound to nobody, never dismissed, standing where its hero stood, and stepping out
        // *with* the party while a replacement summon fights.
        private readonly List<SummonUnit> _standIns = new List<SummonUnit>();

        // Mechs (UltraKind.Mount), by rider. Each is also one of the guests above - the hero side, its
        // own menu, sent home with its rider - but it stands where the rider stood rather than in the
        // vanguard column, takes the rider's blows (Events.Guards) and acts after the rider's turns.
        private readonly Dictionary<Hero, SummonUnit> _mounts = new Dictionary<Hero, SummonUnit>();

        /// <summary>Every summon fighting beside the party, guests first.</summary>
        private IEnumerable<SummonUnit> BesideParty => _allies.Concat(_standIns);

        /// <summary>The guests that stand in the vanguard column: every living one but the mechs.</summary>
        private List<SummonUnit> ColumnAllies()
        {
            return _allies.Where(a => a != null && a.IsAlive && !_mounts.ContainsValue(a)).ToList();
        }

        private Room _currentCombatRoom;

        private Party _currentParty;

        // Rewards accumulated during the current combat (surfaced in the victory summary).
        private readonly List<LootAward> _combatLoot = new List<LootAward>();

        private int _combatXp;

        private readonly Dictionary<Hero, int> _xpAtCombatStart = new Dictionary<Hero, int>();

        private int _combatGold;

        // The Rogue's Fence: percent more gold from this fight's kills and steals.
        private int _fightGoldBonus;

        // Enemies already robbed this fight (Steal works once per enemy).
        private readonly List<ICombatUnit> _robbed = new List<ICombatUnit>();

        /// <summary>Heroes whose cast in progress costs no charge (the Arcane Circlet). Cleared at every
        /// turn's start, so a mark made by a cast that spends nothing (an Ultra's) never carries over.</summary>
        private static readonly List<ICombatUnit> _freeCasts = new List<ICombatUnit>();

        private Room _lastVictoryRoom;

        private bool _currentCombatHadBoss;

        private MagicTagTracker _tagTracker;

        private MomentumTracker _momentum;

        private FocusTracker _focus;

        private ComboDetector _comboDetector;

        /// <summary>
        /// This fight's event stream (see <see cref="CombatEvents"/>): rebuilt per fight, raised by
        /// <see cref="HealthOps"/>, the death sweep and the turn loop. Null outside a fight.
        /// </summary>
        public CombatEvents Events { get; private set; }

        /// <summary>Units that went down since the last <see cref="ResolveDeaths"/>, in the order they
        /// fell, each with whoever struck the blow. Filled by the <see cref="UnitDefeated"/> event.</summary>
        private readonly List<UnitDefeated> _pendingDeaths = new List<UnitDefeated>();

        /// <summary>Heroes this fight has already run through the death path, so a second notice of
        /// the same fall (the safety-net sweep) cannot hide them twice.</summary>
        private readonly HashSet<Hero> _fallen = new HashSet<Hero>();

        /// <summary>The heroes who took the field this fight (standing when it began) - who a victory
        /// counts for, and who the death sweep watches.</summary>
        private readonly List<Hero> _fielded = new List<Hero>();

        private EffectResolver _calculator = new EffectResolver();

        private EffectPresenter _presenter = new EffectPresenter();

        public void SubmitHeroAction(HeroAction action)
        {
            _pendingAction = action;
        }

        /// <summary>The party-replacing summon on the field, or null when the party is fighting.</summary>
        public SummonUnit ActiveSummon => _squad.FirstOrDefault(u => u != null && u.IsAlive);

        /// <summary>Whether Dismiss can send <paramref name="summon"/> home: anything but a stand-in,
        /// which has nowhere to go - it is what is left of a hero.</summary>
        public bool CanDismiss(SummonUnit summon)
        {
            return summon != null && !_standIns.Contains(summon);
        }

        /// <summary>
        /// <b>The</b> hero side of the fight, and the one place that answers it: the summon alone
        /// while one has taken the party's place (§4b), the living heroes and any allies they have
        /// summoned otherwise. Every read of "the heroes" in combat - enemy planning, area attacks,
        /// random targeting, the telegraph markers and the intent icon - goes through this, because a
        /// single leftover <c>party.Heroes</c> lets an enemy hit someone who is not there. The defeat
        /// check does not: an ally alone is not a party (<see cref="HasAliveHeroes"/>).
        /// </summary>
        public List<ICombatUnit> HeroSideUnits()
        {
            if (ReplacementOut)
            {
                return _squad.Where(u => u != null && u.IsAlive).Cast<ICombatUnit>().ToList();
            }
            if (_currentParty == null)
            {
                return new List<ICombatUnit>();
            }
            return _currentParty.Heroes
                .Where(h => h != null && h.IsAlive)
                .Cast<ICombatUnit>()
                .Concat(BesideParty.Where(a => a != null && a.IsAlive))
                .ToList();
        }

        /// <summary>
        /// Submits one of a party-replacing summon's abilities (an action, or its Signature) at the
        /// given targets. Cast with the summon as the caster, through the Boar's path: no Forge
        /// bonus, no tags, no combos, no charge (§4b).
        /// </summary>
        public void SubmitSummonAbility(MagicSO magic, List<ICombatUnit> targets)
        {
            _pendingSummonAbility = magic;
            _pendingSummonTargets = targets;
            _pendingAction = HeroAction.SummonAbility;
        }

        /// <summary>Uses <paramref name="ultra"/> for the current hero turn, at <paramref name="target"/> when it
        /// takes one (a Sacrifice names the ally). Uses the turn.</summary>
        public void SubmitUltra(UltraSO ultra, ICombatUnit target = null)
        {
            _pendingUltra = ultra;
            _pendingUltraTarget = target;
            _pendingAction = HeroAction.Ultra;
        }

        /// <summary>
        /// Who a Sacrifice may take: every living hero of the party, the user included - but never the
        /// last one standing. A fallen hero stays down for the floor, so a party whose last hero became
        /// a horror would have no one left when the fight ended: the horror is a hero's replacement in
        /// the fight, not a way to carry a party of the dead out of it.
        /// </summary>
        public List<ICombatUnit> SacrificeTargets()
        {
            if (_currentParty == null || ReplacementOut)
            {
                return new List<ICombatUnit>();
            }
            var living = _currentParty.Heroes.Where(h => h != null && h.IsAlive).Cast<ICombatUnit>().ToList();
            return living.Count > 1 ? living : new List<ICombatUnit>();
        }

        /// <summary>The Ultras <paramref name="hero"/> knows, resolved; empty for anything but a hero.</summary>
        public List<UltraSO> KnownUltras(ICombatUnit hero)
        {
            var component = hero as Hero;
            if (component == null)
            {
                return new List<UltraSO>();
            }
            return component.KnownUltras.Select(UltraCatalogSO.Resolve).Where(u => u != null).ToList();
        }

        /// <summary>How full <paramref name="hero"/>'s Ultra gauge is this fight, 0..<see cref="UltraOps.Max"/>.</summary>
        public int UltraGauge(ICombatUnit hero)
        {
            return hero is Hero component && _ultraGauge.TryGetValue(component, out int gauge) ? gauge : 0;
        }

        /// <summary>Whether <paramref name="hero"/> is in an Ultra's form right now.</summary>
        public bool IsTransformed(ICombatUnit hero)
        {
            return hero is Hero component && _forms.ContainsKey(component);
        }

        /// <summary>Whether the Ultra command is open: an Ultra known and ready (a full gauge, or Focus for
        /// a MultiCast) and usable, and the hero not already transformed.</summary>
        public bool CanUseUltra(ICombatUnit hero)
        {
            return InCombat && !IsTransformed(hero) && KnownUltras(hero).Any(u => CanUseUltra(hero, u));
        }

        /// <summary>Whether this one Ultra can be used now: ready (<see cref="UltraOps.IsReady"/>), and a
        /// Sacrifice needs someone to give, a revive someone fallen, a MultiCast an ability to cast.</summary>
        public bool CanUseUltra(ICombatUnit hero, UltraSO ultra)
        {
            if (ultra == null || !UltraOps.IsReady(ultra, UltraGauge(hero), FocusOf(hero)))
            {
                return false;
            }
            if (UltraOps.IsMultiCast(ultra))
            {
                return CanMultiCast(hero);
            }
            return !UltraOps.NeedsTarget(ultra) || UltraTargets(ultra, hero).Count > 0;
        }

        /// <summary>How much Focus <paramref name="hero"/> has built this fight (the Mage's signature).</summary>
        public int FocusOf(ICombatUnit hero)
        {
            return _focus != null ? _focus.FocusOf(hero) : 0;
        }

        /// <summary>A MultiCast needs an ability worth repeating, charged or not, and no Silence.</summary>
        private bool CanMultiCast(ICombatUnit hero)
        {
            var component = hero as Hero;
            if (component == null || BuffTracker == null || BuffTracker.HasStatusEffect(hero, BuffType.Silenced))
            {
                return false;
            }
            return DungeonManager.HasInstance && DungeonManager.Instance.MagicState != null
                   && DungeonManager.Instance.MagicState.HasAnyMultiCastable(component.HeroKey);
        }

        /// <summary>Whom an Ultra that takes a target may be aimed at: the Sacrifice's candidates, the
        /// fallen for a revive, the living party for a single-ally Strike.</summary>
        public List<ICombatUnit> UltraTargets(UltraSO ultra, ICombatUnit user = null)
        {
            if (ultra == null)
            {
                return new List<ICombatUnit>();
            }
            if (ultra.Kind == UltraKind.Sacrifice)
            {
                return SacrificeTargets();
            }
            var allies = PickableAllies(ultra.TargetType);
            // Stepping in front of yourself protects nobody (Divine Intervention).
            if (user != null && UltraOps.ProtectsOthers(ultra))
            {
                allies.Remove(user);
            }
            return allies;
        }

        /// <summary>The allies a picker offers for a one-ally target type: the fallen for a revive,
        /// the living hero side otherwise. Empty for any other type.</summary>
        public List<ICombatUnit> PickableAllies(MagicTargetType type)
        {
            switch (type)
            {
                case MagicTargetType.FallenAlly:
                    return GetFallenHeroes();
                case MagicTargetType.SingleAlly:
                    return HeroSideUnits();
                default:
                    return new List<ICombatUnit>();
            }
        }

        /// <summary>The abilities <paramref name="hero"/>'s current form grants - free to use while it lasts.</summary>
        public List<MagicSO> FormAbilities(ICombatUnit hero)
        {
            if (hero is Hero component && _forms.TryGetValue(component, out var form) && form.Ultra.Abilities != null)
            {
                return form.Ultra.Abilities.Where(a => a != null).ToList();
            }
            return new List<MagicSO>();
        }

        /// <summary>Sends the summon home. Uses its turn, like any other command it has.</summary>
        public void SubmitDismiss()
        {
            _pendingAction = HeroAction.Dismiss;
        }

        /// <summary>Asks the selection UI for a single-enemy target for a summon's ability.</summary>
        public void RequestSummonAbilityTarget(ICombatUnit summon, MagicSO magic, List<ICombatUnit> enemies)
        {
            OnSummonAbilityTargetRequested?.Invoke(summon, magic, enemies);
        }

        /// <summary>
        /// Who a summon's ability lands on when it needs no picker: the whole hero side for a
        /// party-wide ability (just the summon, while it has taken the party's place), the summon
        /// itself for anything else aimed at its own side, every living enemy for an area attack. A
        /// single-enemy ability returns every candidate and the caller picks.
        /// </summary>
        public List<ICombatUnit> SummonAbilityTargets(MagicSO magic, ICombatUnit summon)
        {
            if (magic == null)
            {
                return new List<ICombatUnit>();
            }
            switch (magic.TargetType)
            {
                case MagicTargetType.SingleEnemy:
                case MagicTargetType.AllEnemies:
                    return GetAliveEnemies();
                case MagicTargetType.AllAllies:
                    return HeroSideUnits();
                case MagicTargetType.FallenAlly:
                    // The fallen heroes, picked from (RoomActionUI); revived ones wait off the stage
                    // with the rest of the party until the summon leaves (HandleRevived).
                    return GetFallenHeroes();
                default:
                    return new List<ICombatUnit> { summon };
            }
        }

        public void RequestMagicSlots(ICombatUnit hero, List<MagicSlot> slots)
        {
            OnMagicSlotsRequested?.Invoke(hero, slots);
        }

        public void RequestAttackTargets(ICombatUnit hero, List<ICombatUnit> enemies)
        {
            OnAttackTargetRequested?.Invoke(hero, enemies);
        }

        /// <summary>Raises the consumable picker with the party's carried consumable stacks.</summary>
        public void RequestItemList(ICombatUnit hero, List<ItemSaveData> consumables)
        {
            OnItemListRequested?.Invoke(hero, consumables);
        }

        /// <summary>
        /// Raises the Inspect target picker. Inspect is the one hero command that is <b>free</b>:
        /// it never submits an action, so the turn is still the player's when the window closes. It
        /// reads back knowledge the party already earned in the field rather than granting any, so
        /// charging a turn for it would be charging for the UI.
        /// </summary>
        public void RequestInspectTargets(ICombatUnit hero, List<ICombatUnit> enemies)
        {
            OnInspectTargetRequested?.Invoke(hero, enemies);
        }

        /// <summary>
        /// The action this enemy will take next, for the intent icon on its HP bar — or null when the
        /// answer is not determined yet. Pure, so it has no side effects.
        ///
        /// <para>It reports **only** what is certain (see
        /// <see cref="EnemyActionPlanner.PredictCertain"/>): a telegraph already in flight, or a
        /// single ungated action that is the only thing the enemy can do. Authored behaviours are
        /// probabilistic, and an intent icon that guesses wrong teaches the player to distrust the
        /// telegraph, which is the one tell the fight depends on.</para>
        /// </summary>
        public EnemyActionType? PredictIntent(Enemy enemy)
        {
            if (!InCombat || enemy == null || !enemy.IsAlive || _currentParty == null)
            {
                return null;
            }

            var behavior = enemy.Behavior != null
                ? enemy.Behavior
                : EnemyBehaviorSO.BuiltInPreset(enemy.Archetype);

            var context = new EnemyCombatContext
            {
                // A Vanished hero (the Rogue) is not offered as a target.
                Heroes = TargetableHeroes(),
                Allies = GetAliveEnemies().Where(u => !ReferenceEquals(u, enemy)).ToList(),
                BuffTracker = BuffTracker,
                ChargingEntryIndex = enemy.ChargingEntryIndex,
                SelfTurnCount = enemy.TurnsTaken,
                Spells = enemy.Spells,
                Threat = _threat
            };
            return EnemyActionPlanner.PredictCertain(enemy, context, behavior);
        }

        /// <summary>Submits a basic attack against the chosen target for the current hero turn.</summary>
        public void SubmitAttackAction(ICombatUnit target)
        {
            _pendingAttackTarget = target;
            _pendingAction = HeroAction.Attack;
        }

        /// <summary>Submits casting the magic in <paramref name="slotIndex"/> at the chosen targets.
        /// <paramref name="chargeSlot"/> is the ability a Life Tap refills, when the player picked one.</summary>
        public void SubmitCastAction(MagicSO magic, int slotIndex, ICombatUnit caster, List<ICombatUnit> targets,
            int chargeSlot = SpellcastAction.AnyChargeSlot, bool multiCast = false)
        {
            _pendingMultiCast = multiCast;
            _pendingCastAction = new SpellcastAction
            {
                Magic = magic,
                Caster = caster,
                Targets = targets,
                ChargeSlot = chargeSlot,
                CastSlot = slotIndex
            };
            _pendingCastSlot = slotIndex;
            _pendingAction = HeroAction.Cast;
        }

        /// <summary>
        /// Submits summoning <paramref name="slot"/>'s summon for the current hero turn. It uses the
        /// turn, like casting (§4b); the charge is spent when it resolves.
        /// </summary>
        public void SubmitSummonAction(SummonSlot slot, ICombatUnit target = null)
        {
            _pendingSummon = slot;
            _pendingSummonTarget = target;
            _pendingAction = HeroAction.Summon;
        }

        /// <summary>Submits using a consumable <paramref name="item"/> on <paramref name="target"/>.</summary>
        public void SubmitUseItemAction(ItemSO item, ICombatUnit target)
        {
            _pendingUseItem = item;
            _pendingUseItemTarget = target;
            _pendingAction = HeroAction.UseItem;
        }

        /// <summary>How much threat <paramref name="unit"/> holds in the current fight (0 outside one).</summary>
        public float ThreatOf(ICombatUnit unit)
        {
            return _threat.Get(unit);
        }

        public List<ICombatUnit> GetAliveEnemies()
        {
            if (_currentCombatRoom == null)
            {
                return new List<ICombatUnit>();
            }
            return _currentCombatRoom.Enemies
                .Where(e => e != null && e.IsAlive)
                .Cast<ICombatUnit>()
                .ToList();
        }

        /// <summary>
        /// The living hero side - the summon alone while one has taken the party's place, so every
        /// caller (the selection UI's ally targets included) is right by construction.
        /// </summary>
        public List<ICombatUnit> GetAliveHeroes(Party party)
        {
            if (party == null || ReferenceEquals(party, _currentParty) || ReplacementOut)
            {
                return HeroSideUnits();
            }
            return party.Heroes
                .Where(h => h != null && h.IsAlive)
                .Cast<ICombatUnit>()
                .ToList();
        }

        /// <summary>Heroes who fell in this fight and are still down - who a Revive can reach.</summary>
        public List<ICombatUnit> GetFallenHeroes()
        {
            var sacrificed = Events != null ? Events.Sacrificed : null;
            return _fallen.Where(h => h != null && !h.IsAlive && (sacrificed == null || !sacrificed.Contains(h)))
                .Cast<ICombatUnit>().ToList();
        }

        /// <summary>The fallen a party-wide Revive reaches: the heroes' own, never an enemy's.</summary>
        private List<ICombatUnit> FallenAlliesOf(ICombatUnit caster)
        {
            return caster == null || caster is Enemy ? new List<ICombatUnit>() : GetFallenHeroes();
        }

        // How long a hero's fall takes before the last frame (lying down) holds.
        private const float DeathAnimationSeconds = 0.6f;
    }
}
