namespace Assets.Scripts.Cards
{
    /// <summary>Why a party-replacing summon left the field.</summary>
    public enum SummonExit
    {
        None = 0,

        /// <summary>Its health ran out. The blow that did it went no further (§4b).</summary>
        Fell = 1,

        /// <summary>It took all of its turns.</summary>
        TurnsSpent = 2,

        /// <summary>The player sent it back, spending its turn.</summary>
        Dismissed = 3,

        /// <summary>The last enemy fell while it was out.</summary>
        Victory = 4
    }

    /// <summary>
    /// The bookkeeping of one party-replacing summoning (§4b): how many of its own turns it has
    /// taken, whether its once-per-summoning Signature is spent, and whether it has left. Pure, and
    /// shared by <c>CombatManager</c> and <c>EncounterSimulator</c> so the two cannot disagree about
    /// when a summon goes home.
    ///
    /// <para>The immediate first turn it takes on arrival is turn 1 of its <see cref="Turns"/>, and
    /// a turn lost to Frozen still counts: it was the summon's turn, and "N of its own turns" is a
    /// clock, not a count of actions.</para>
    /// </summary>
    public class SummonStay
    {
        public SummonSO Summon { get; }

        /// <summary>How many of its own turns it stays, upgrades included.</summary>
        public int Turns { get; }

        public int TurnsTaken { get; private set; }

        public bool SignatureUsed { get; private set; }

        /// <summary>Set once it has left; nothing un-sets it. A summoning is never resumed.</summary>
        public SummonExit Exit { get; private set; }

        public bool HasLeft => Exit != SummonExit.None;

        public int TurnsLeft => Turns - TurnsTaken;

        public SummonStay(SummonSO summon, int turns)
        {
            Summon = summon;
            Turns = turns < 1 ? 1 : turns;
        }

        /// <summary>Whether the Signature can be used on this turn: it exists and is not spent.</summary>
        public bool CanUseSignature => !HasLeft && !SignatureUsed && Summon != null && Summon.Signature != null;

        public void MarkSignatureUsed()
        {
            SignatureUsed = true;
        }

        /// <summary>
        /// One of its turns is over. Returns true when that was its last, which is the caller's cue to
        /// send it home — after the turn's upkeep, so its own poison ticks on its own final turn.
        /// </summary>
        public bool EndTurn()
        {
            if (HasLeft)
            {
                return false;
            }
            TurnsTaken++;
            return TurnsTaken >= Turns;
        }

        /// <summary>Records why it left. The first reason wins; later calls are ignored.</summary>
        public void Leave(SummonExit exit)
        {
            if (!HasLeft && exit != SummonExit.None)
            {
                Exit = exit;
            }
        }
    }
}
