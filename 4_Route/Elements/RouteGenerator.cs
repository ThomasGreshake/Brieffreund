//Copyright Thomas Greshake 2026

using Brieffreund.Eulermap;
using Brieffreund.Eulermap.Functions;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Brieffreund.Routegenerator
{
    //Either a completed route or the branches following a leaf
    internal readonly record struct RouteExpansion(RouteLeaf? CompletedRoute, List<(RouteLeaf Leaf, RoutePathway Added)> Branches);

    internal class RouteGenerator
    {
        //Data --------------------------------------------------------------------

        internal readonly RouteMap Map;

        private readonly IUserInput _input;
        private readonly INodeConnectionCreator _evaluator;
        private readonly RouteGeneratorManager _generator;

        private float _currentScore;
        private readonly PriorityQueue<RouteLeaf, (float Score, long Order)> _queue = new(1024); //Order makes ties deterministic
        private long _enqueueCount = 0;
        internal int Count => _queue.Count;
        internal float ComplexityScore => _currentScore * (float)Math.Log(_queue.Count + 1);

        private readonly BigInteger _eulerPathSignature;
        private readonly Dictionary<BigInteger, float> _scores = new(8192);
        internal int ScoreCount => _scores.Count;

        private RouteLeaf? _finalRoute = null;
        internal RouteLeaf? FinalRoute => _finalRoute;

        //Also set when the search is abandoned because another map already has a better route
        private bool _isFinished = false;
        internal bool IsFinished => _isFinished;

        //Setup --------------------------------------------------------------------

        internal RouteGenerator(IUserInput input, RouteGeneratorManager generator, RouteMap map)
        {
            _input = input;
            _evaluator = new NodeConnectionCreator();
            _generator = generator;
            Map = map;
            _currentScore = map.Length;

            _eulerPathSignature = BigInteger.Zero;
            foreach (RoutePath path in map.Paths.Where(p => p.EulerPathway != null))
            {
                _eulerPathSignature |= path.Signature;
            }

            List<RoutePathway> options = new(map.StartAndEnd[0].Pathways);
            if (options.Count > 1)
            {
                options.RemoveAll(p => !IsValidStartOption(p));
            }

            foreach (RoutePathway way in options)
            {
                RouteLeaf initial = new(way);
                Enqueue(initial);
                UpdateScores(initial, way);
            }

            while (_queue.Count > 0 && _queue.Count < Constants.INITIAL_ROUTE_QUEUE_COUNT && !_isFinished)
            {
                if (!TryDequeue(out RouteLeaf? leaf))
                {
                    break;
                }

                Merge(Expand(leaf));
            }
        }

        private static bool IsValidStartOption(RoutePathway way)
        {
            IPathfinder<RouteNode, RoutePathway> path =
                IPathfinder.FindPath<RouteNode, RoutePathway>(way.Towards, way.Origin, w => way.RoutePath == w.RoutePath ? -1 : 1);
            return path.Success;
        }

        //Internals --------------------------------------------------------------------

        //Must not be called concurrently with Merge. Returns false if this generator is finished
        internal bool TryDequeue([NotNullWhen(true)] out RouteLeaf? leaf)
        {
            leaf = null;
            if (_isFinished || !_queue.TryDequeue(out RouteLeaf? current, out _))
            {
                return false;
            }

            _currentScore = current.Score;

            RouteLeaf? globalRoute = _generator.FinalLeaf;
            if (globalRoute != null && (globalRoute.Score < current.Score || _queue.Count > Constants.EARLY_RETURN_COUNT))
            {
                _isFinished = true;
                return false;
            }

            leaf = current;
            return true;
        }

        //Does not change the state of the generator, so it may run in parallel
        internal RouteExpansion Expand(RouteLeaf current)
        {
            RouteMap map = current.Map;

            List<RoutePathway> options = new(8);
            GetOptions(current, options);

            List<(RouteLeaf Leaf, RoutePathway Added)> branches = new(options.Count);

            if (options.Count == 0)
            {
                if (current.Routenode != map.StartAndEnd[1])
                {
                    throw new InvalidOperationException($"Bad route end: route ran out of options at {current.Routenode.Position} instead of at the end node {map.StartAndEnd[1].Position}.");
                }

                return new RouteExpansion(current, branches);
            }

            float baseLength = GetBaseLength(current);

            for (int i = 0; i < options.Count; i++)
            {
                RouteLeaf branch = (i == options.Count - 1) ? current : new(current);
                RoutePathway next = options[i];
                float newTotal = GetNewTotalLength(branch, next, baseLength);
                branch.AddNext(next, _input, newTotal);
                branches.Add((branch, next));
            }

            return new RouteExpansion(null, branches);
        }

        //Must not be called concurrently with TryDequeue or Merge
        internal void Merge(RouteExpansion expansion)
        {
            if (expansion.CompletedRoute != null)
            {
                SetFinalRoute(expansion.CompletedRoute);
                _generator.SetFinalRoute(expansion.CompletedRoute);
                return;
            }

            foreach ((RouteLeaf branch, RoutePathway added) in expansion.Branches)
            {
                if (UpdateScores(branch, added))
                {
                    Enqueue(branch);
                }
            }
        }

        private void Enqueue(RouteLeaf leaf) => _queue.Enqueue(leaf, (leaf.Score, _enqueueCount++));

        private float GetBaseLength(RouteLeaf leaf)
        {
            EulerNode node = leaf.Routenode.Eulernode;
            int[] connections = GetCurrentConnections(leaf, node, null);
            return leaf.TotalLength - _evaluator.EvaluateOptionScore(node, connections);
        }

        private float GetNewTotalLength(RouteLeaf leaf, RoutePathway nextWay, float baseLength)
        {
            EulerPathway? next = nextWay.EulerPathway;
            if (next == null)
            {
                return leaf.TotalLength;
            }

            EulerPathway? last = leaf.Last.GetEulerways().FirstOrDefault();
            if (last == null)
            {
                return leaf.TotalLength;
            }

            EulerNode node = last.Towards;
            int[] connections = GetCurrentConnections(leaf, node, next);

            return baseLength + _evaluator.EvaluateOptionScore(node, connections);
        }

        private int[] GetCurrentConnections(RouteLeaf leaf, EulerNode node, EulerPathway? include)
        {
            int count = node.Count;
            int[] connections = new int[count];
            for (int i = 0; i < connections.Length; i++)
            {
                connections[i] = -1;
            }

            int index = include == null ? -1 : node.Pathways.IndexOf(include);
            foreach (EulerPathway way in leaf.Last.GetEulerways())
            {
                if (way.Towards == node && index != -1)
                {
                    int other = node.Pathways.IndexOf(way.GetOppositeDirection());
                    connections[index] = other;
                    connections[other] = index;
                }

                if (way.Origin == node)
                {
                    index = node.Pathways.IndexOf(way);
                }
                else
                {
                    index = -1;
                }
            }

            return connections;
        }

        private static void GetOptions(RouteLeaf route, List<RoutePathway> options)
        {
            options.Clear();

            options.AddRange(route.Routenode.Pathways);

            options.RemoveAll(p => route.Contains(p.RoutePath));

            if (options.Count <= 1)
            {
                return;
            }

            RemoveImpliedDirectionOpposites(route, options);

            if (options.Count <= 1)
            {
                return;
            }

            options.RemoveAll(p => !IsValidOption(route, p));
        }

        private static void RemoveImpliedDirectionOpposites(RouteLeaf route, List<RoutePathway> options)
        {
            if (options.All(w => !w.RoutePath.GoesBothWays))
            {
                return;
            }

            foreach (RoutePath path in route.Map.GetBothwayPathsOnNode(route.Routenode.Eulernode))
            {
                if (!route.Last.TryGetPathway(path, out RoutePathway? way))
                {
                    continue;
                }

                bool forward = way.RoutePath.Forward == way;

                options.RemoveAll(w => w.RoutePath.GoesBothWays && ((w.RoutePath.Forward == w) != forward));

                return;
            }
        }

        private static bool IsValidOption(RouteLeaf route, RoutePathway way)
        {
            IPathfinder<RouteNode, RoutePathway> path =
                IPathfinder.FindPath<RouteNode, RoutePathway>(way.Towards, way.Origin, w => way.RoutePath == w.RoutePath
                || route.Contains(w.RoutePath) ? -1 : 1);
            return path.Success;
        }

        private void SetFinalRoute(RouteLeaf route)
        {
            _isFinished = true;
            if (_finalRoute == null || _finalRoute.Score > route.Score)
            {
                _finalRoute = route;
            }
        }

        private bool UpdateScores(RouteLeaf leaf, RoutePathway added)
        {
            if (added.EulerPathway == null)
            {
                return true;
            }

            BigInteger signature = leaf.Signature & _eulerPathSignature;
            float score = leaf.Score;

            if (!_scores.TryGetValue(signature, out float currentScore) || score < currentScore)
            {
                _scores[signature] = score;
                return true;
            }

            return false;
        }
    }
}