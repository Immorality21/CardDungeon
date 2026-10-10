namespace Assets.Scripts.Cards
{
    public enum MagicTargetType
    {
        SingleEnemy,
        AllEnemies,
        Self,
        SingleAlly,
        AllAllies,
        /// <summary>One fallen hero of the caster's side - a Revive. Appended: serialized by ordinal.</summary>
        FallenAlly
    }
}
