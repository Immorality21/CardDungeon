using System;
using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Heroes;

namespace Assets.Scripts.Balance
{
    /// <summary>
    /// Remembers <see cref="SphereGridOps.GreedySpend"/> for the length of one analysis. The spend is
    /// pure, and the model asks for the same grid at the same budget once per hero, per floor, per mix
    /// of a frontier sweep. Outside a <see cref="Scope"/> nothing is kept: a grid can be edited between
    /// two analyses, or by a test, and a stale spend would be silently wrong.
    /// </summary>
    public static class GridSpendMemo
    {
        private static Dictionary<(SphereGridSO grid, int budget), List<string>> _spends;

        /// <summary>Remembers until disposed. A scope inside a scope changes nothing.</summary>
        public static IDisposable Scope()
        {
            if (_spends != null)
            {
                return new Closing(false);
            }
            _spends = new Dictionary<(SphereGridSO, int), List<string>>();
            return new Closing(true);
        }

        /// <summary>The player-like spend the model uses: a copy, so a caller may keep or change it.</summary>
        public static List<string> Spend(SphereGridSO grid, int budget)
        {
            if (_spends == null || grid == null)
            {
                return SphereGridOps.GreedySpend(grid, null, budget, out _, EquippedMagicState.DefaultSlotCount);
            }
            if (!_spends.TryGetValue((grid, budget), out var nodes))
            {
                nodes = SphereGridOps.GreedySpend(grid, null, budget, out _, EquippedMagicState.DefaultSlotCount);
                _spends[(grid, budget)] = nodes;
            }
            return new List<string>(nodes);
        }

        private sealed class Closing : IDisposable
        {
            private readonly bool _clears;

            public Closing(bool clears)
            {
                _clears = clears;
            }

            public void Dispose()
            {
                if (_clears)
                {
                    _spends = null;
                }
            }
        }
    }
}
