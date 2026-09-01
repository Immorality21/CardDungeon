using UnityEditor;

namespace Assets.Scripts.Balance.Editor
{
    /// <summary>
    /// A monotonic counter that ticks whenever the asset database changes, so the balance window can
    /// answer "has anything moved since I last measured?" without measuring to find out.
    ///
    /// <para>This exists because <see cref="BalanceWindow"/> used to re-measure on every
    /// <c>OnFocus</c>, on the reasoning that assets can be edited elsewhere while the window is open.
    /// That reasoning is right; the unconditional part was not. A full analysis with simulation on
    /// costs roughly nineteen seconds on this project, and it was being paid every time the window
    /// was clicked into — including the overwhelmingly common case where nothing had changed at
    /// all.</para>
    ///
    /// <para>Deliberately coarse: any import, delete or move ticks it, so an unrelated texture
    /// reimport costs one re-analysis. That is the right side to err on — a spurious re-measure is a
    /// wasted moment, a missed one is a window quietly showing numbers that no longer describe the
    /// project.</para>
    /// </summary>
    internal sealed class BalanceAssetWatcher : AssetPostprocessor
    {
        /// <summary>Starts at 1 so a window whose "last analysed" version is 0 always measures once.</summary>
        public static int Version { get; private set; } = 1;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (importedAssets.Length == 0 && deletedAssets.Length == 0 && movedAssets.Length == 0)
            {
                return;
            }

            Version++;
        }
    }
}
