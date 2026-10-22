namespace Assets.Scripts.Heroes
{
    /// <summary>
    /// A summon a hero knows, as the sphere grid describes it: the summon's key plus whatever the
    /// hero's activated upgrade nodes add. Keys and numbers only — the Heroes layer does not know
    /// about <c>SummonSO</c> (Cards depends on Heroes, not the other way), exactly as a
    /// <c>MagicKnown</c> node names its magic by key. Built by <see cref="SphereGridOps.SummonsForNodes"/>.
    /// </summary>
    public class SummonGrant
    {
        public string Key;

        /// <summary>Added to every effect's Power (percentage points for a percentage buff).</summary>
        public int PowerBonus;

        /// <summary>Added to every timed effect's Duration.</summary>
        public int DurationBonus;

        /// <summary>Added to the summon's charges per run.</summary>
        public int ChargeBonus;

        /// <summary>A squad summon: troops added to its base size (capped by the summon).</summary>
        public int SizeBonus;

        /// <summary>A squad summon: troop promotions, each raising the weakest troop one tier.</summary>
        public int Promotions;

        /// <summary>Percentage points added to the apply chance of the debuffs its own abilities carry
        /// (an ally's Attack, Signature and actions) - the Auto Turret's blinding rounds.</summary>
        public int ApplyChanceBonus;

        /// <summary>A unit summon (a party replacement, an ally, a mech): percent more damage on every
        /// hit it lands, its Attack and its abilities alike - read through <c>IOutgoingDamage</c>.
        /// A special attack has no unit to carry it; raise that with <see cref="PowerBonus"/>.</summary>
        public int DamagePercent;
    }
}
