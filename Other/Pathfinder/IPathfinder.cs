//Copyright Thomas Greshake 2026

using System.Numerics;

namespace Brieffreund
{
    internal interface IPathfinder
    {
        //Implementation -------------------------------------------------------------

        public static IPathfinder<N, P> FindPath<N, P>(N start, N end, Func<P, float> pathMult) where N : class, IPathnode<N, P> where P : class, IPathway<N, P> =>
            new AStar.PathfinderAStar<N, P>(start, end, pathMult);

        public static IPathfinder<N, P> FindPath<N, P>(N start, N end) where N : class, IPathnode<N, P> where P : class, IPathway<N, P> =>
            FindPath<N, P>(start, end, p => 1);
    }

    public interface IPathnode<N, P> where N : class, IPathnode<N, P> where P : class, IPathway<N, P>
    {
        public Vector2 Position { get; }

        public IList<P> GetPaths();
    }

    public interface IPathway<N, P> where N : class, IPathnode<N, P> where P : class, IPathway<N, P>
    {
        public float Length { get; }
        public N Towards { get; }
        public N Origin { get; }
    }

    internal interface IPathfinder<N, P> where N : class, IPathnode<N, P> where P : class, IPathway<N, P>
    {
        //Data -------------------------------------------------------------

        public bool Success { get; }
        public int Count { get; }
        public P this[int index] { get; }

        //Getters -------------------------------------------------------------

        public float GetLength() => GetPaths().Sum(p => p.Length);

        public P First() => this[0];

        public P Last() => this[Count - 1];

        public IEnumerable<P> GetPaths()
        {
            for (int i = 0; i < Count; i++)
            {
                yield return this[i];
            }
        }

        public IEnumerable<N> GetNodes() => GetPaths().Select(p => p.Towards);

        public bool Contains(P path) => GetPaths().Contains(path);

        public bool Contains(N node) => GetNodes().Contains(node);
    }
}