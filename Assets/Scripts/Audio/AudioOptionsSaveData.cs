using Assets.Scripts.IO;

namespace Assets.Scripts.Audio
{
    /// <summary>
    /// The player's audio settings on disk (<c>savedata/Audio.json</c>). Deliberately its own file
    /// rather than a field on <c>MetaProgressSaveData</c>: these are machine preferences, not
    /// progress, and they must survive a save being wiped or a run being abandoned.
    /// </summary>
    [System.Serializable]
    public class AudioOptionsSaveData : IWriteable
    {
        public float Master = 0.8f;
        public float Music = 0.7f;
        public float Sfx = 1f;
        public bool Muted;

        public string GetFileName()
        {
            return "Audio";
        }
    }
}
