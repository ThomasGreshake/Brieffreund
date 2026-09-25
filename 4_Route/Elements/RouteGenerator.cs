//Copyright Thomas Greshake 2026

using Brieffreund.Eulermap;
using Brieffreund.Eulermap.Functions;
using System.Numerics;

namespace Brieffreund.Routegenerator
{
    internal class RouteGenerator
    {
        //Data --------------------------------------------------------------------

        internal readonly RouteMap Map;

        private readonly IUserInput _input;
        private readonly INodeConnectionCreator _evaluator;
        private readonly RouteGeneratorManager _generator;

        private float _currentScore;
        private readonly PriorityQueue<RouteLeaf, float> _queue = new(1024);
        internal int Count => _queue.Count;
        internal float ComplexityScore => _currentScore * (float)Math.Log(_queue.Count + 1);

        private int _workingCores = 0;
        internal int WorkingCores => _workingCores;
        internal float PickingScore => _currentScore * (float)Math.Log(_workingCores + 1);

        private readonly BigInteger _eulerPathSignature;
        private readonly Dictionary<BigInteger, float> _scores = new(8192);
        internal int ScoreCount => _scores.Count;

        private readonly object _lock = new object();

        private RouteLeaf? _finalRoute = null;
        internal RouteLeaf? FinalRoute => _finalRoute;

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
                _queue.Enqueue(initial, initial.Score);
                UpdateScores(initial, way);
            }

            int qCount = 4 * Math.Max(Constants.THREAD_COUNT, 8);
            while (_queue.Count > 0 && _queue.Count < qCount && _finalRoute == null)
            {
                ProcessNext(options);
            }
        }

        private static bool IsValidStartOption(RoutePathway way)
        {
            IPathfinder<RouteNode, RoutePathway> path =
                IPathfinder.FindPath<RouteNode, RoutePathway>(way.Towards, way.Origin, w => way.RoutePath == w.RoutePath ? -1 : 1);
            return path.Success;
        }

        //Internals --------------------------------------------------------------------

        internal bool ProcessNext(List<RoutePathway> options)
        {
            float minScore;

            bool returnValue = false;
            lock (_lock)
            {
                _workingCores += 1;
            }

            do
            {
                RouteLeaf current;
                lock (_lock)
                {
                    if (_queue.Count == 0)
                    {
                        break;
                    }

                    current = _queue.Dequeue();
                    _currentScore = current.Score;
                }

                RouteLeaf? globalRoute = _generator.FinalLeaf;
                if (globalRoute != null && (globalRoute.Score < current.Score || _queue.Count > Constants.EARLY_RETURN_COUNT))
                {
                    SetFinalRoute(globalRoute);
                    break;
                }

                RouteMap map = current.Map;

                GetOptions(current, options);

                if (options.Count == 0)
                {
                    if (current.Routenode != map.StartAndEnd[1])
                    {
                        throw new Exception("Bad route end");
                    }

                    SetFinalRoute(current);
                    returnValue = true;
                    break;
                }

                minScore = float.MaxValue;
                float baseLength = GetBaseLength(current);

                for (int i = 0; i < options.Count; i++)
                {
                    RouteLeaf branch = (i == options.Count - 1) ? current : new(current);
                    RoutePathway next = options[i];
                    float newTotal = GetNewTotalLength(branch, next, baseLength);
                    branch.AddNext(next, _input, newTotal);

                    if (!UpdateScores(branch, next))
                    {
                        continue;
                    }

                    minScore = Math.Min(minScore, branch.Score);

                    lock (_lock)
                    {
                        _queue.Enqueue(branch, branch.Score);
                    }
                }
            }
            while (_finalRoute != null && minScore < _finalRoute.Score); //Prevents a race condition

            lock (_lock)
            {
                _workingCores -= 1;
            }

            return returnValue;
        }

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
            lock (_lock)
            {
                if (_finalRoute == null || _finalRoute.Score > route.Score)
                {
                    _finalRoute = route;
                }
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

            lock (_lock)
            {
                if (!_scores.TryGetValue(signature, out float currentScore) || score < currentScore)
                {
                    _scores[signature] = score;
                    return true;
                }

                return false;
            }
        }
    }
}