namespace Assets.Scripts.Audio
{
    /// <summary>
    /// The three volume dials the player can move. <see cref="Master"/> scales everything the game
    /// plays (it drives <c>AudioListener.volume</c>); the other two scale their own kind on top of it,
    /// so a player who only wants the music down does not have to trade away the combat foley.
    /// The integer order is serialized into <c>Audio.json</c> — append new values, don't reorder.
    /// </summary>
    public enum AudioChannel
    {
        Master = 0,
        Music = 1,
        Sfx = 2
    }
}
