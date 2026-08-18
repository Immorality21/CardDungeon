namespace Assets.Scripts.Combat.Audio
{
    /// <summary>
    /// Semantic combat sound events. Mapped to one or more <c>AudioClip</c>s by the
    /// <see cref="SoundBankSO"/> (Resources/CombatSoundBank) and played via <see cref="CombatAudio"/>.
    /// The integer order is serialized into the sound-bank asset — append new values, don't reorder.
    /// </summary>
    public enum CombatSound
    {
        MeleeSwing = 0,   // a weapon swing (plays on the attack lunge)
        Impact = 1,       // a hit landing
        MagicCast = 2,    // casting an equipped magic
        Draw = 3,         // extracting magic from an enemy
        Heal = 4,         // a heal resolving
        ItemUse = 5,      // using a consumable
        BossSignature = 6,// a boss winding up its signature AoE
        EnemyDeath = 7,   // an enemy dying
        Victory = 8,      // combat won
        Defeat = 9,       // party wiped
        CursorMove = 10,  // command-menu cursor movement
        Confirm = 11      // command-menu confirm
    }
}
