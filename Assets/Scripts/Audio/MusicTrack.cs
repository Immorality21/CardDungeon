namespace Assets.Scripts.Audio
{
    /// <summary>
    /// The looping music beds the game asks for. Mapped to clips by <see cref="MusicBankSO"/>
    /// (Resources/MusicBank) and played by <see cref="MusicPlayer"/>.
    ///
    /// <para>Members exist only where something actually requests them — the hub, walking a floor,
    /// a fight, a boss fight. Victory and defeat are deliberately absent: they are one-shot stingers
    /// already played as <see cref="CombatSound"/>s, and the bed is faded out under them rather than
    /// swapped for another loop. The integer order is serialized into the bank asset — append new
    /// values, don't reorder.</para>
    /// </summary>
    public enum MusicTrack
    {
        None = 0,
        Hub = 1,          // the between-runs hub / main menu
        Exploration = 2,  // walking a dungeon floor, out of combat
        Combat = 3,       // an ordinary fight
        BossCombat = 4    // a fight with a boss in the room
    }
}
