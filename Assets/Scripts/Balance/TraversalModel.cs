using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Balance
{
    /// <summary>
    /// Which slice of a floor the run curve should price.
    /// </summary>
    public enum TraversalMode
    {
        /// <summary>Every room. What the model assumed before 2026-08-29 — the optimistic-for-danger end.</summary>
        FullClear = 0,

        /// <summary>What a player with no map is expected to enter before stumbling into the exit.</summary>
        Explorer = 1,

        /// <summary>The only road to the exit. What a player who knows the way, or who is running, pays.</summary>
        Beeline = 2,
    }

    /// <summary>
    /// How much of a floor the player actually walks through.
    ///
    /// <para><b>Why this exists.</b> <c>RunCurveModel</c> used to spread a level's spawn expectation
    /// across every generated room, which silently assumed a full clear. It is not one.
    /// <c>RoomManager.GenerateGraph</c> attaches every new node to exactly one existing parent and
    /// <c>GenerateDungeon</c> creates doors only for <c>_placementPairs</c> — one edge per placed
    /// child — so a finished dungeon is a <b>tree with no loops</b>. <c>DungeonManager.DesignateExitRoom</c>
    /// then makes the BFS-farthest room the exit. In a tree that means the route from start to exit is
    /// <b>unique</b>, and every other room hangs off it as an optional branch the player may never open.</para>
    ///
    /// <para>Measured against the real generator, a 31-room floor puts only ~33% of itself on the road
    /// to the exit and hands a blind explorer ~66%. The shortfall grows with floor length, which makes
    /// room count the lever with the worst marginal efficiency — see <c>docs/BALANCING.md</c> §5l.</para>
    ///
    /// <para>This is a topology model: room *contents* never matter, only how many rooms the player
    /// opens, so it takes a count and a chain bias and nothing else. It mirrors the generator rather
    /// than approximating it with a curve fit, so it stays correct if the generator is retuned.</para>
    ///
    /// <para>Pure and deterministic: it runs its own LCG rather than <c>UnityEngine.Random</c>, so it
    /// neither consumes the global sequence nor makes a test flaky, and the same floor always reports
    /// the same band.</para>
    /// </summary>
    public static class TraversalModel
    {
        /// <summary>Layouts sampled per measurement. 400 holds the mean inside ~1% at these sizes.</summary>
        public const int DefaultTrials = 400;

        public const int DefaultSeed = 20260829;

        private static readonly Dictionary<long, TraversalBand> Cache = new Dictionary<long, TraversalBand>();

        /// <summary>
        /// Rooms the player is expected to open on a floor of <paramref name="roomCount"/> rooms
        /// generated at <paramref name="chainBias"/>.
        /// </summary>
        public static TraversalBand Measure(
            int roomCount,
            float chainBias,
            int trials = DefaultTrials,
            int seed = DefaultSeed)
        {
            var band = new TraversalBand { FullClear = Mathf.Max(0, roomCount) };
            if (roomCount <= 1)
            {
                band.Beeline = band.FullClear;
                band.Explorer = band.FullClear;
                return band;
            }

            trials = Mathf.Max(1, trials);
            chainBias = Mathf.Clamp01(chainBias);

            long key = ((long)roomCount << 40)
                       ^ ((long)Mathf.RoundToInt(chainBias * 1000f) << 24)
                       ^ ((long)trials << 8)
                       ^ (uint)seed;
            if (Cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var rng = new Lcg((uint)seed);
            var graph = new Graph(roomCount);
            long beeline = 0;
            long explorer = 0;

            for (int t = 0; t < trials; t++)
            {
                graph.Generate(chainBias, rng);
                int exit = graph.FarthestFromStart();
                beeline += graph.DistanceToStart(exit) + 1;
                explorer += graph.ExploreUntil(exit, rng);
            }

            band.Beeline = (float)beeline / trials;
            band.Explorer = (float)explorer / trials;
            Cache[key] = band;
            return band;
        }

        /// <summary>
        /// The share of a level's <b>non-starting</b> rooms the player is expected to open. The party
        /// always stands in the start room and <c>EnemyManager</c> never spawns there, so the run curve
        /// has already taken it off the total — which makes <c>(visited - 1) / (rooms - 1)</c>, not
        /// <c>visited / rooms</c>, the factor that belongs on the remainder.
        /// </summary>
        public static float PopulatedFraction(
            int roomCount,
            float chainBias,
            TraversalMode mode,
            int trials = DefaultTrials,
            int seed = DefaultSeed)
        {
            if (mode == TraversalMode.FullClear || roomCount <= 1)
            {
                return 1f;
            }

            var band = Measure(roomCount, chainBias, trials, seed);
            float visited = mode == TraversalMode.Beeline ? band.Beeline : band.Explorer;
            return Mathf.Clamp01((visited - 1f) / (roomCount - 1f));
        }

        /// <summary>Drops the memo table. Only tests should need this.</summary>
        public static void ClearCache()
        {
            Cache.Clear();
        }

        /// <summary>
        /// A tree of <c>size</c> rooms, reused across trials so a measurement allocates once.
        /// Mirrors <c>RoomManager.GenerateGraph</c> exactly: node 0 is the party's start room, and
        /// every later node attaches to one already-placed parent — a leaf (degree &lt;= 1) with
        /// probability <c>chainBias</c>, otherwise any placed node at all.
        /// </summary>
        private sealed class Graph
        {
            private readonly int _size;
            private readonly int[] _degree;
            private readonly int[][] _adjacency;
            private readonly int[] _distance;
            private readonly int[] _queue;
            private readonly int[] _leaves;
            private readonly bool[] _seen;
            private readonly int[] _stack;

            public Graph(int size)
            {
                _size = size;
                _degree = new int[size];
                _adjacency = new int[size][];
                for (int i = 0; i < size; i++)
                {
                    // A tree of n nodes has n-1 edges, so no node can exceed n-1 neighbours.
                    _adjacency[i] = new int[size];
                }
                _distance = new int[size];
                _queue = new int[size];
                _leaves = new int[size];
                _seen = new bool[size];
                _stack = new int[size];
            }

            public void Generate(float chainBias, Lcg rng)
            {
                for (int i = 0; i < _size; i++)
                {
                    _degree[i] = 0;
                }

                for (int i = 1; i < _size; i++)
                {
                    int parent;
                    if (rng.NextFloat() < chainBias)
                    {
                        // Chain bias: prefer leaves, which is what makes the dungeon stringy.
                        int count = 0;
                        for (int n = 0; n < i; n++)
                        {
                            if (_degree[n] <= 1)
                            {
                                _leaves[count++] = n;
                            }
                        }
                        parent = count > 0 ? _leaves[rng.NextInt(count)] : rng.NextInt(i);
                    }
                    else
                    {
                        parent = rng.NextInt(i);
                    }

                    _adjacency[parent][_degree[parent]++] = i;
                    _adjacency[i][_degree[i]++] = parent;
                }
            }

            /// <summary>
            /// The exit room. <c>DungeonManager.DesignateExitRoom</c> BFSes from the start and takes
            /// the last room to increase the distance, so this must break ties the same way.
            /// </summary>
            public int FarthestFromStart()
            {
                for (int i = 0; i < _size; i++)
                {
                    _distance[i] = -1;
                }

                int head = 0, tail = 0;
                _distance[0] = 0;
                _queue[tail++] = 0;
                int farthest = 0;
                int max = 0;

                while (head < tail)
                {
                    int current = _queue[head++];
                    int degree = _degree[current];
                    for (int e = 0; e < degree; e++)
                    {
                        int neighbour = _adjacency[current][e];
                        if (_distance[neighbour] >= 0)
                        {
                            continue;
                        }

                        _distance[neighbour] = _distance[current] + 1;
                        _queue[tail++] = neighbour;
                        if (_distance[neighbour] > max)
                        {
                            max = _distance[neighbour];
                            farthest = neighbour;
                        }
                    }
                }

                return farthest;
            }

            /// <summary>Doors between the start room and <paramref name="node"/>. Call after <see cref="FarthestFromStart"/>.</summary>
            public int DistanceToStart(int node)
            {
                return _distance[node];
            }

            /// <summary>
            /// Rooms a player with no map opens before walking into <paramref name="exit"/>. Modelled
            /// as a depth-first walk that picks an unopened door at random and stops the moment the
            /// exit is entered — the player cannot see where the exit is, but they can see which doors
            /// they have already been through.
            /// </summary>
            public int ExploreUntil(int exit, Lcg rng)
            {
                for (int i = 0; i < _size; i++)
                {
                    _seen[i] = false;
                }

                _seen[0] = true;
                int visited = 1;
                if (exit == 0)
                {
                    return visited;
                }

                int top = 0;
                _stack[top++] = 0;

                while (top > 0)
                {
                    int current = _stack[top - 1];
                    int degree = _degree[current];

                    // Collect the doors out of this room that lead somewhere new.
                    int options = 0;
                    for (int e = 0; e < degree; e++)
                    {
                        int neighbour = _adjacency[current][e];
                        if (!_seen[neighbour])
                        {
                            _leaves[options++] = neighbour;
                        }
                    }

                    if (options == 0)
                    {
                        // Dead end — back out the way we came. Backtracking costs no new rooms.
                        top--;
                        continue;
                    }

                    int next = _leaves[rng.NextInt(options)];
                    _seen[next] = true;
                    visited++;
                    if (next == exit)
                    {
                        return visited;
                    }

                    _stack[top++] = next;
                }

                return visited;
            }
        }

        /// <summary>
        /// A tiny linear congruential generator (Numerical Recipes constants). Deterministic and
        /// self-contained so a measurement never depends on, or disturbs, <c>UnityEngine.Random</c>.
        /// </summary>
        private sealed class Lcg
        {
            private uint _state;

            public Lcg(uint seed)
            {
                _state = seed == 0 ? 1u : seed;
            }

            public uint Next()
            {
                _state = unchecked(_state * 1664525u + 1013904223u);
                return _state;
            }

            public float NextFloat()
            {
                return (Next() >> 8) / 16777216f;
            }

            public int NextInt(int exclusiveMax)
            {
                return exclusiveMax <= 1 ? 0 : (int)(Next() % (uint)exclusiveMax);
            }
        }
    }

    /// <summary>
    /// How much of one floor the player opens, as a band rather than a point: a floor is not one
    /// number, because a healthy party clears it for the XP and a hurt one runs for the exit.
    /// </summary>
    public struct TraversalBand
    {
        /// <summary>Rooms on the only road to the exit, including the start room.</summary>
        public float Beeline;

        /// <summary>Rooms a player with no map is expected to open, including the start room.</summary>
        public float Explorer;

        /// <summary>Every room the level generates.</summary>
        public int FullClear;

        public float BeelineFraction => FullClear > 0 ? Beeline / FullClear : 1f;

        public float ExplorerFraction => FullClear > 0 ? Explorer / FullClear : 1f;

        public override string ToString()
        {
            return $"{Beeline:0.0}–{Explorer:0.0} of {FullClear}";
        }
    }
}
