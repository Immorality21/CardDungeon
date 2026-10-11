using System.Collections.Generic;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    /// <summary>
    /// What a summon does once it answers. Three kinds share one foundation — the grid node that
    /// teaches it, its per-run charges, the Summon command and the presentation — and differ only in
    /// what happens after (docs/plans/SPECIALIZATION.md §4b).
    /// </summary>
    public enum SummonKind
    {
        /// <summary>FF7–9: a big effect lands and it is over. No unit, no change to the turn order.</summary>
        SpecialAttack = 0,

        /// <summary>FFX: the party steps out and the summon fights alone, a player-driven unit whose
        /// stats are ratios of the summoner's. It leaves when its health or its turns run out, or on
        /// Dismiss, and the blow that ends it never reaches the party.</summary>
        ReplaceParty = 1,

        /// <summary>The Warlock's demons: a player-driven unit that fights <b>beside</b> the party,
        /// standing in the vanguard in front of the heroes. Built like a replacement - stats as
        /// ratios of the summoner's, its own menu, a stay in turns - but the party keeps fighting and
        /// enemies can hit either. One of each kind per summoner: calling the same one again sends the
        /// first home. It leaves when its health or its turns run out, on Dismiss, when its summoner
        /// falls, and when a party-replacing summon takes the field.</summary>
        JoinParty = 2
    }

    /// <summary>Which way the creature looks while it is on the stage.</summary>
    public enum SummonFacing
    {
        /// <summary>Toward the enemies (right) - for a summon that attacks.</summary>
        Enemies = 0,

        /// <summary>Toward the party (left) - for a summon that buffs or heals them.</summary>
        Party = 1
    }

    /// <summary>
    /// A summon: the capability a sphere-grid branch pays out (§4b). Learned from a
    /// <c>SphereNodeKind.Summon</c> node that names it by <see cref="Key"/>, always carried (it takes
    /// no ability slot), cast through the hero's <b>Summon</b> command, and limited by charges per
    /// run that refill with ability charges — run start and refuges.
    ///
    /// <para>A special-attack summon's effect is an ordinary list of <see cref="SpellEffect"/>s,
    /// resolved by the same <c>EffectResolver</c> every ability goes through. Upgrade nodes on the
    /// grid add to it (<see cref="SummonOps.EffectsFor"/>); the asset itself is never modified.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "NewSummon", menuName = "SO/Summon")]
    public class SummonSO : ScriptableObject
    {
        [Tooltip("Stable identifier. Grid nodes name the summon they teach or upgrade by this key.")]
        public string Key;

        public string DisplayName;

        [TextArea]
        public string Description;

        [Tooltip("The creature, drawn large on the battle stage while it is summoned.")]
        public Sprite Sprite;

        [Tooltip("Optional: frames the creature loops through while it is on the stage. Empty shows Sprite still.")]
        public Sprite[] AnimationFrames;

        [Min(1f)] public float AnimationFps = 6f;

        [Tooltip("Which way the creature faces on the stage. Art is authored facing right (the enemies); " +
                 "Party mirrors it, and its lurch moves toward the heroes instead.")]
        public SummonFacing Facing = SummonFacing.Enemies;

        [Tooltip("Optional: played as the creature rises on the stage, before the shared roar. Empty " +
                 "plays only the roar.")]
        public AudioClip ArrivalSound;

        [Range(0f, 1f)] public float ArrivalSoundVolume = 0.9f;

        public SummonKind Kind = SummonKind.SpecialAttack;

        [Tooltip("Who the effects land on. The Bloodfang Boar is AllAllies.")]
        public MagicTargetType TargetType = MagicTargetType.AllAllies;

        [Tooltip("SpecialAttack: what lands. A buff authored with PowerMode.PercentOfTargetStat reads " +
                 "Power as a percentage of each target's own stat.")]
        public List<SpellEffect> Effects = new List<SpellEffect>();

        [Tooltip("SpecialAttack, optional: effects that land on ONE random target of the summon's " +
                 "targets, after Effects - the Abyssal Nightmare Silences one enemy at random.")]
        public List<SpellEffect> RandomTargetEffects = new List<SpellEffect>();

        [Tooltip("SpecialAttack, optional: effects that land on the summoner's whole side after Effects - " +
                 "Glacius's frost hide, a barrier on every hero. Duration upgrades lengthen them too.")]
        public List<SpellEffect> PartyEffects = new List<SpellEffect>();

        [Tooltip("SpecialAttack: a SummonPower node raises the first of PartyEffects instead of the first " +
                 "of Effects - for a summon whose growing half is what it gives the party (Glacius).")]
        public bool PowerRaisesPartyEffects;

        [Tooltip("SpecialAttack with UsesTags: the tags its Effects lay on what they hit, as an ability's " +
                 "do - Glacius's claw lays Ice, so the party's Water sets off Freeze.")]
        public List<MagicTag> Tags = new List<MagicTag>();

        [Min(1)] public int TagDuration = 3;

        [Tooltip("Health the summoner pays to call it, as a percentage of their max health (floor 1). " +
                 "Refused when it would leave them at 0. The Blood Idol costs the Cultist blood. 0 = free.")]
        [Min(0)] public int SummonerHealthCostPercent;

        [Tooltip("Scales the threat the summoning's damage and healing earn the summoner, like " +
                 "MagicSO.ThreatMultiplier. 1 = ordinary.")]
        [Min(0f)] public float ThreatMultiplier = 1f;

        [Tooltip("Flat threat the summoner earns per summoning, like MagicSO.BonusThreat. This is how a " +
                 "taunt is authored: enemies bias toward the summoner for the rest of the fight - biased, " +
                 "never certain (ThreatTable). Ignored by a party-replacing summon, whose own abilities " +
                 "carry their own threat settings.")]
        [Min(0)] public int BonusThreat;

        [Tooltip("Reactions the summoned unit carries while it is on the field (docs/plans/EVENTS.md). " +
                 "Only a summon that stays - beside the party or in its place - is around to react.")]
        public List<Combat.Triggers.TriggeredEffect> Triggers = new List<Combat.Triggers.TriggeredEffect>();

        [Tooltip("Recoils when struck, as the heroes do. Off for anything that should read as heavy " +
                 "(the Cairn Golem, the Blood Idol). Presentation only.")]
        public bool Flinches = true;

        [Tooltip("The hit reaction: shown once, briefly, whenever it is struck (if it flinches). Only a " +
                 "summon that stands on the field and gets hit needs one - a party replacement. Same size " +
                 "and facing as its idle frames. Empty = it only recoils.")]
        public Sprite[] HitFrames;

        [Tooltip("Charges per run before any upgrade node. Refilled with ability charges: at run " +
                 "start and when resting in a refuge.")]
        [Min(1)] public int BaseCharges = 1;

        // ---- ReplaceParty and JoinParty: everything below builds the unit. A special attack
        // ---- ignores it, and a unit-building summon ignores TargetType and Effects above.

        [Header("Party replacement / ally")]
        [Tooltip("ReplaceParty / JoinParty: each stat as a PERCENTAGE of the summoner's own (base + gear, no combat " +
                 "buffs), snapshotted on arrival. MaxHealth 250 brings a body two and a half times the " +
                 "summoner's bar. A stat with no entry is 0.")]
        public StatBlock StatPercents = new StatBlock();

        [Tooltip("ReplaceParty: copy the summoner's resistances (innate + grid + gear) onto the summon.")]
        public bool CopySummonerResistances = true;

        [Tooltip("ReplaceParty: how many of its own turns it stays, the immediate first one included.")]
        [Min(1)] public int TurnsActive = 3;

        [Tooltip("ReplaceParty: abilities it may use on any of its turns, cast with the summon as the " +
                 "caster. No charges, no Forge bonus, no tags. Attack and Dismiss are always offered.")]
        public List<MagicSO> Actions = new List<MagicSO>();

        [Tooltip("ReplaceParty: the one big move, usable once per summoning.")]
        public MagicSO Signature;

        [Tooltip("ReplaceParty, optional: what its Attack command does, in place of the plain " +
                 "Strength swing - a single-enemy ability cast with the summon as the caster, unlimited " +
                 "and, being its basic attack, never blocked by Silence. Exatrix's Rend (damage + a turn " +
                 "delay) is the first. Empty keeps the plain Attack.")]
        public MagicSO AttackAbility;

        [Tooltip("Its abilities lay their tags and can set off combos, like a hero's cast (a mech's oil " +
                 "and fire make Ignite) - and a special attack lays its Tags. Off for every other summon: " +
                 "their abilities resolve tagless.")]
        public bool UsesTags;

        [Tooltip("ReplaceParty / JoinParty, optional: cast on the enemies as it leaves while any stand - " +
                 "fallen, dismissed or out of turns (Ifrit's Hellfire). Cast with the summon as the caster, " +
                 "on the Boar's path. Not when the fight is already won.")]
        public MagicSO PartingAbility;

        [Tooltip("A mech (UltraKind.Mount): the idle frames once its rider has climbed in - the mech " +
                 "with the rider drawn aboard. The rider is hidden while these play. Empty keeps the " +
                 "mech's own frames and leaves the rider hidden all the same.")]
        public Sprite[] MountedFrames;

        [Tooltip("JoinParty / ReplaceParty, optional: its Attack command cycles through Actions, one " +
                 "per turn (turn 1 = Actions[0]), and the row is labelled with the ability's name. For a " +
                 "summon that never attacks - the Blood Idol's rotating party rites.")]
        public bool RotateActions;

        [Tooltip("JoinParty: it takes its turns on its own - no command menu - casting its Attack at a " +
                 "random enemy (the Tinkerer's Auto Turret). Dismissing it is not offered; it leaves when " +
                 "its turns or health run out.")]
        public bool ActsAlone;

        [Header("Squad (ReplaceParty only)")]
        [Tooltip("ReplaceParty, optional: the troops this summon brings instead of being one unit itself, " +
                 "weakest tier first (the Demon Army: Imp, Succubus). Each troop is its own SummonSO - " +
                 "stats, Attack, Signature - and fights as its own unit; the stay and the charges are this " +
                 "summon's. Empty: the summon is the single unit, as the Golem is.")]
        public List<SummonSO> SquadTiers = new List<SummonSO>();

        [Tooltip("Squad: how many troops it brings before any SummonSize node.")]
        [Min(1)] public int SquadSize = 3;

        [Tooltip("Squad: the most troops SummonSize nodes can raise it to. The hero side lays out four " +
                 "in two ranks, so more than four would need a new formation.")]
        [Min(1)] public int MaxSquadSize = 4;

        /// <summary>A summon that brings troops (<see cref="SquadTiers"/>) rather than being the unit itself.</summary>
        public bool IsSquad => SquadTiers != null && SquadTiers.Exists(t => t != null);

        public string Label => string.IsNullOrEmpty(DisplayName) ? name : DisplayName;
    }
}
