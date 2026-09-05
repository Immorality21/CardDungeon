using System.Collections.Generic;
using Assets.Scripts.Progression;

namespace Assets.Scripts.Hub
{
    /// <summary>
    /// Everything about a save that the hub rules need, in one object: which lots are placed and how
    /// far the campaign has been cleared.
    ///
    /// <para>A context object rather than a growing list of positional arguments, which is what
    /// <c>docs/plans/HUB.md</c> §7 machinery 4 asked for when it saw the same pressure coming for
    /// <c>SphereGridOps</c>. It also keeps <see cref="BuildingOps"/> free of the meta-progress
    /// singleton, so every rule stays a pure function the EditMode tests can drive with no scene.</para>
    /// </summary>
    public sealed class HubProgress
    {
        /// <summary>A save with nothing placed and nothing cleared — a brand-new profile.</summary>
        public static readonly HubProgress Fresh = new HubProgress(null, null);

        private readonly List<string> _completedRunKeys;

        public IReadOnlyList<BuildingProgress> Buildings { get; }
        public IReadOnlyList<string> CompletedRunKeys => _completedRunKeys;

        public HubProgress(
            IReadOnlyList<BuildingProgress> buildings,
            IReadOnlyList<string> completedRunKeys)
        {
            Buildings = buildings ?? new List<BuildingProgress>();
            _completedRunKeys = completedRunKeys != null
                ? new List<string>(completedRunKeys)
                : new List<string>();
        }

        /// <summary>An empty or null run key is treated as no requirement at all, so a
        /// half-authored gate opens rather than locking a lot away forever.</summary>
        public bool HasCleared(string runKey)
        {
            return string.IsNullOrEmpty(runKey) || _completedRunKeys.Contains(runKey);
        }
    }
}
