//Copyright Thomas Greshake 2026

using System.Diagnostics;
using System.Numerics;

namespace Brieffreund.Routemap
{
    internal interface INodeLoopCreator
    {
        public ICollection<EulerNode> NodeLoops { get; }
        public IDictionary<EulerNode, List<EulerPathway>> SingleLoops { get; }

        public void FindNodeLoops();
    }
}

namespace Brieffreund.Routemap.Functions
{
    internal class NodeLoopCreator : INodeLoopCreator
    {
        private readonly IUserInput _input;
        private readonly IEulerMap _map;
        private readonly List<EulerNode> _nodes;
        private readonly List<PathLoop> _pathLoops;
        private readonly List<PathLoop>[] _nodeToLoops;
        private readonly float[] _nodeScores;
        private readonly HashSet<int> _colorCounter;

        private readonly HashSet<EulerNode> _nodeLoops = new();
        public ICollection<EulerNode> NodeLoops => _nodeLoops;

        private readonly Dictionary<EulerNode, List<EulerPathway>> _singleLoops = new();
        public IDictionary<EulerNode, List<EulerPathway>> SingleLoops => _singleLoops;

        internal NodeLoopCreator(IUserInput input, IEulerMap map, List<PathLoop> pathLoops, List<PathLoop>[] nodeToLoops, float[] nodeScores)
        {
            _input = input;
            _map = map;
            _nodes = _map.Nodes.Values.Where(n => nodeToLoops[n.Index].Count > 1).ToList();
            _pathLoops = pathLoops;
            _nodeToLoops = nodeToLoops;
            _nodeScores = nodeScores;
            _colorCounter = new HashSet<int>(pathLoops.Count);
        }

        public void FindNodeLoops()
        {
            int[] canvas = CreateCanvas();
            AddStorages(canvas);
            GuessGoodNodeLoops(canvas);
            AddPathToStorageLoops();
        }

        private void AddStorages(int[] canvas)
        {
            int desiredCount = _input.DesiredStorageCount;
            if (desiredCount == 0)
            {
                return;
            }

            List<EulerNode> nodesWithStorages = _nodes.Where(n => n.HasStorages && n.StorageUsageCount > 1).OrderBy(n => GetLength(n) + n.Storages.Min(s => s.Item2)).ToList();
            List<PathLoop> loopsWithStorages = new();

            int estimatedStorageUsageCount = 0;

            for (int i = 0; i < nodesWithStorages.Count; i++)
            {
                EulerNode node = nodesWithStorages[i];
                int count = AddToLoopsWithStorages(node, loopsWithStorages);
                estimatedStorageUsageCount += Math.Min(count, node.StorageUsageCount);

                int colorCount = FillColorCounter(node, canvas);
                if (colorCount < 2)
                {
                    continue;
                }

                int index = _nodes.IndexOf(node);

                if (colorCount == 2)
                {
                    AddSingleLoop(node, canvas);
                }
                else
                {
                    _nodeLoops.Add(node);
                }

                PaintCanvas(canvas);

                if (estimatedStorageUsageCount >= desiredCount)
                {
                    return;
                }
            }
        }

        private int AddToLoopsWithStorages(EulerNode node, List<PathLoop> loopsWithStorages)
        {
            int count = 0;
            foreach (PathLoop loop in _nodeToLoops[node.Index])
            {
                if (loopsWithStorages.Contains(loop))
                {
                    continue;
                }

                count++;
                loopsWithStorages.Add(loop);
            }

            return count;
        }

        private void GuessGoodNodeLoops(int[] canvas)
        {
            while (true)
            {
                float bestScore = float.MaxValue;
                int maxCount = 0;
                List<EulerNode> candidates = new();

                for (int i = 0; i < _nodes.Count; i++)
                {
                    EulerNode node = _nodes[i];
                    if (_nodeLoops.Contains(node))
                    {
                        continue;
                    }

                    int colorCount = FillColorCounter(node, canvas);
                    if (colorCount < 2)
                    {
                        continue;
                    }

                    if (colorCount < maxCount)
                    {
                        continue;
                    }

                    float score = GetLength(node);
                    if (colorCount > maxCount)
                    {
                        candidates = new List<EulerNode>() { node };
                        bestScore = score;
                        maxCount = colorCount;
                        continue;
                    }

                    if (score < bestScore)
                    {
                        bestScore = score;
                        candidates.RemoveAll(c => GetLength(c) > 1.1f * score);
                        candidates.Add(node);
                        continue;
                    }

                    if (score < 1.1f * bestScore)
                    {
                        candidates.Add(node);
                    }
                }

                if (candidates.Count == 0)
                {
                    int colorCount = FillColorCounter(canvas);
                    Debug.Assert(colorCount == 1);
                    return;
                }

                EulerNode? bestNode = null;
                float minDist = float.MaxValue;

                //One node is chosen from a list of -almost- equivalent candidates in such a way that the resulting route is predictable and understandable for a human
                //If this way to choose that node actually leads to better routes is currently untested
                foreach (EulerNode node in candidates)
                {
                    Vector2 pos = node.Position;

                    for (int j = 0; j < _nodes.Count; j++)
                    {
                        EulerNode other = _nodes[j];
                        if (!_nodeLoops.Contains(other) && _singleLoops.ContainsKey(other) && !other.HasStorages)
                        {
                            continue;
                        }

                        float dist = Vector2.DistanceSquared(pos, other.Position);
                        if (dist > minDist)
                        {
                            continue;
                        }

                        minDist = dist;
                        bestNode = node;
                    }
                }

                if (bestNode == null) //No preexisting loops or storages
                {
                    bestNode = candidates.MinBy(GetLength);
                    if (bestNode == null)
                    {
                        throw new Exception();
                    }
                }

                if (maxCount == 2)
                {
                    AddSingleLoop(bestNode, canvas);
                }
                else
                {
                    _nodeLoops.Add(bestNode);
                }

                PaintCanvas(canvas);
            }
        }

        private void AddSingleLoop(EulerNode node, int[] canvas)
        {
            FillColorCounter(node, canvas);
            List<int> colors = _colorCounter.ToList();
            if (colors.Count != 2)
            {
                throw new Exception();
            }

            float bestPassing = float.MaxValue;
            Tuple<EulerPathway, EulerPathway>? bestNode = null;

            for (int i = 0; i < _pathLoops.Count; i++)
            {
                PathLoop first = _pathLoops[i];
                if (canvas[i] != colors[0])
                {
                    continue;
                }

                for (int j = 0; j < _pathLoops.Count; j++)
                {
                    PathLoop second = _pathLoops[j];
                    if (canvas[j] != colors[1])
                    {
                        continue;
                    }

                    float passing = GetPassingDistance(first, second, out Tuple<EulerPathway, EulerPathway>? ways);
                    if (ways == null)
                    {
                        continue;
                    }

                    if (passing < bestPassing)
                    {
                        bestPassing = passing;
                        bestNode = ways;
                    }
                }
            }

            if (bestNode == null)
            {
                throw new Exception();
            }

            AddSingleLoop(bestNode.Item1.Towards, bestNode.Item1, bestNode.Item2);
        }

        private bool AddSingleLoop(EulerNode node, EulerPathway first, EulerPathway second)
        {
            if (!_singleLoops.TryGetValue(node, out List<EulerPathway>? ways))
            {
                ways = new();
                _singleLoops.Add(node, ways);
            }

            for (int i = 0; i < ways.Count; i += 2)
            {
                if ((ways[i] == first && ways[i + 1] == second) || (ways[i] == second && ways[i + 1] == first))
                {
                    return false;
                }
            }

            ways.Add(first);
            ways.Add(second);

            return true;
        }

        private float GetPassingDistance(PathLoop first, PathLoop second, out Tuple<EulerPathway, EulerPathway>? bestWays)
        {
            float bestPassing = float.MaxValue;
            bestWays = null;

            foreach (EulerPathway a in first.Ways)
            {
                foreach (EulerPathway b in second.Ways)
                {
                    Tuple<EulerPathway, EulerPathway>? ways = null;
                    if (a.Towards == b.Towards)
                    {
                        ways = Tuple.Create(a, b);
                    }
                    else if (a.Towards == b.Origin)
                    {
                        ways = Tuple.Create(a, b.GetOppositeDirection());
                    }
                    else if (a.Origin == b.Towards)
                    {
                        ways = Tuple.Create(a.GetOppositeDirection(), b);
                    }
                    else if (a.Origin == b.Origin)
                    {
                        ways = Tuple.Create(a.GetOppositeDirection(), b.GetOppositeDirection());
                    }

                    if (ways == null)
                    {
                        continue;
                    }

                    float passing = ways.Item1.Towards.GetPassingDistance(ways.Item1, ways.Item2.GetOppositeDirection());
                    if (passing < bestPassing)
                    {
                        bestPassing = passing;
                        bestWays = ways;
                    }
                }
            }

            return bestPassing;
        }

        private float GetLength(EulerNode node) => node.PassingCircumference - Constants.NODE_SCORE_MULT * _nodeScores[node.Index];

        private int[] CreateCanvas()
        {
            int[] canvas = new int[_pathLoops.Count];
            for (int i = 0; i < canvas.Length; i++)
            {
                canvas[i] = i;
            }

            for (int i = 0; i < _pathLoops.Count; i++)
            {
                PathLoop loop = _pathLoops[i];

                for (int j = i + 1; j < _pathLoops.Count; j++)
                {
                    PathLoop other = _pathLoops[j];

                    if (LoopsShareConnection(loop, other))
                    {
                        ReplaceColor(canvas, j, canvas[i]);
                    }
                }
            }

            return canvas;
        }

        private bool LoopsShareConnection(PathLoop one, PathLoop two)
        {
            foreach (EulerPathway way in one.Ways)
            {
                foreach (EulerPathway other in two.Ways)
                {
                    if (way.Towards == other.Origin && way.Towards.GetPassingDistance(way, other) < Constants.CONNECTS_DIRECTLY_PASSING_DISTANCE)
                    {
                        return true;
                    }
                    if (way.Origin == other.Towards && way.Origin.GetPassingDistance(other, way) < Constants.CONNECTS_DIRECTLY_PASSING_DISTANCE)
                    {
                        return true;
                    }
                    if (way.Towards == other.Towards && way.Towards.GetPassingDistance(way, other.GetOppositeDirection()) < Constants.CONNECTS_DIRECTLY_PASSING_DISTANCE)
                    {
                        return true;
                    }
                    if (way.Origin == other.Origin && way.Origin.GetPassingDistance(way.GetOppositeDirection(), other) < Constants.CONNECTS_DIRECTLY_PASSING_DISTANCE)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void PaintCanvas(int[] canvas)
        {
            foreach (EulerNode node in _nodes)
            {
                if (!_nodeLoops.Contains(node) && !_singleLoops.ContainsKey(node))
                {
                    continue;
                }

                List<PathLoop> loopsOnNode = _nodeToLoops[node.Index];
                if (loopsOnNode.Count < 2)
                {
                    continue;
                }

                int iColor = canvas[loopsOnNode[0].Index];
                for (int j = 1; j < loopsOnNode.Count; j++)
                {
                    int jColor = canvas[loopsOnNode[j].Index];
                    if (iColor == jColor)
                    {
                        continue;
                    }

                    ReplaceColor(canvas, jColor, iColor);
                }
            }
        }

        private void ReplaceColor(int[] canvas, int replace, int replaceWith)
        {
            for (int i = 0; i < canvas.Length; i++)
            {
                if (canvas[i] == replace)
                {
                    canvas[i] = replaceWith;
                }
            }
        }

        private int FillColorCounter(int[] canvas)
        {
            _colorCounter.Clear();

            for (int i = 0; i < canvas.Length; i++)
            {
                int color = canvas[i];
                if (_colorCounter.Contains(color))
                {
                    continue;
                }

                _colorCounter.Add(color);
            }

            return _colorCounter.Count;
        }

        private int FillColorCounter(EulerNode node, int[] canvas)
        {
            _colorCounter.Clear();
            List<PathLoop> loopsOnNode = _nodeToLoops[node.Index];

            for (int i = 0; i < loopsOnNode.Count; i++)
            {
                int color = canvas[loopsOnNode[i].Index];
                if (_colorCounter.Contains(color))
                {
                    continue;
                }

                _colorCounter.Add(color);
            }

            return _colorCounter.Count;
        }

        private void AddPathToStorageLoops()
        {
            int desiredCount = _input.DesiredStorageCount;
            if (desiredCount == 0)
            {
                return;
            }

            int count = _pathLoops.Sum(l => l.Ways.Count);
            Dictionary<EulerPathway, int> mailSinceStorage = new(count);
            Dictionary<EulerPathway, int> mailTowardsStorage = new(count);

            foreach (PathLoop loop in _pathLoops)
            {
                AddMailInfo(loop, mailSinceStorage, mailTowardsStorage);
            }

            float averageMail = _map.Streetmap.TotalMailAmount / (desiredCount + 1);
            float acceptableMail = averageMail * Math.Max(Constants.ACCEPTABLE_MAILAMOUNT_MULTIPLIER * 0.8f, 1);
            float inacceptableMail = averageMail * Math.Max(Constants.INACCEPTABLE_MULTIPLIER * 0.8f, 1);

            Dictionary<PathLoop, int> loopScores = GetLoopScores(mailSinceStorage, mailTowardsStorage, acceptableMail, inacceptableMail);
            if (loopScores.Count == 0)
            {
                return;
            }

            Dictionary<EulerNode, int> storages = new();
            foreach (EulerNode node in _map.Nodes.Values.Where(n => n.HasStorages && n.StorageUsageCount > 1))
            {
                storages.Add(node, node.StorageUsageCount - 1);
            }

            if (storages.Count == 0)
            {
                return;
            }

            Dictionary<EulerPathway, PathLoop> wayToLoop = GetWayToLoop(count);

            while (loopScores.Count > 0)
            {
                PathLoop current = loopScores.MaxBy(kvp => kvp.Value).Key;
                loopScores.Remove(current);

                List<EulerPathway>? pathToStorage =
                    GetPathToStorage(current, storages, wayToLoop, mailSinceStorage, mailTowardsStorage, acceptableMail, inacceptableMail);

                if (pathToStorage == null)
                {
                    continue;
                }

                EulerNode usedStorage = pathToStorage[pathToStorage.Count - 1].Towards;
                UpdateStorages(storages, usedStorage);

                if (!AddNodeLoopsToPath(pathToStorage, current, wayToLoop))
                {
                    continue;
                }

                AddMailInfo(current, mailSinceStorage, mailTowardsStorage);
                int newScore = GetLoopScore(current, mailSinceStorage, mailTowardsStorage, acceptableMail, inacceptableMail);
                if (newScore > 0)
                {
                    loopScores.Add(current, newScore);
                }
            }
        }

        private void AddMailInfo(PathLoop loop, Dictionary<EulerPathway, int> mailSinceStorage, Dictionary<EulerPathway, int> mailTowardsStorage)
        {
            int start = -1;
            for (int i = 0; i < loop.Ways.Count; i++)
            {
                EulerPathway way = loop.Ways[i];
                if (way.Origin.HasStorages || _nodeLoops.Contains(way.Origin))
                {
                    start = i;
                    break;
                }
            }

            if (start == -1)
            {
                int total = loop.Ways.Sum(w => w.Eulerpath.MailAmount);
                foreach (EulerPathway way in loop.Ways)
                {
                    mailSinceStorage[way] = total;
                    mailTowardsStorage[way] = total;
                }
                return;
            }

            int mail = 0;
            for (int i = 0; i < loop.Ways.Count; i++)
            {
                int index = (start + i) % loop.Ways.Count;
                EulerPathway way = loop.Ways[index];
                mail += way.Eulerpath.MailAmount;
                mailSinceStorage[way] = mail;
                if (way.Towards.HasStorages || _nodeLoops.Contains(way.Towards))
                {
                    mail = 0;
                }
            }

            mail = 0;
            for (int i = 0; i < loop.Ways.Count; i++)
            {
                int index = (start - i + loop.Ways.Count - 1) % loop.Ways.Count;
                EulerPathway way = loop.Ways[index];
                mail += way.Eulerpath.MailAmount;
                mailTowardsStorage[way] = mail;
                if (way.Origin.HasStorages || _nodeLoops.Contains(way.Origin))
                {
                    mail = 0;
                }
            }
        }

        private Dictionary<PathLoop, int> GetLoopScores(Dictionary<EulerPathway, int> mailSinceStorage, Dictionary<EulerPathway, int> mailTowardsStorage,
            float acceptableMail, float inacceptableMail)
        {
            Dictionary<PathLoop, int> loopScores = new(_pathLoops.Count);
            foreach (PathLoop loop in _pathLoops)
            {
                int loopScore = GetLoopScore(loop, mailSinceStorage, mailTowardsStorage, acceptableMail, inacceptableMail);
                if (loopScore < 0)
                {
                    continue;
                }
                loopScores.Add(loop, loopScore);
            }
            return loopScores;
        }

        private int GetLoopScore(PathLoop loop, Dictionary<EulerPathway, int> mailSinceStorage, Dictionary<EulerPathway, int> mailTowardsStorage,
            float acceptableMail, float inacceptableMail)
        {
            int bestScore = -1;
            foreach (EulerPathway w in loop.Ways)
            {
                int since = mailSinceStorage[w];
                int to = mailTowardsStorage[w];
                int score = since * to;
                if (bestScore > score || since < acceptableMail || since + to < inacceptableMail)
                {
                    continue;
                }

                bestScore = score;
            }

            return bestScore;
        }

        private Dictionary<EulerPathway, PathLoop> GetWayToLoop(int capacity)
        {
            Dictionary<EulerPathway, PathLoop> wayToLoop = new Dictionary<EulerPathway, PathLoop>(capacity);
            foreach (PathLoop loop in _pathLoops)
            {
                foreach (EulerPathway w in loop.Ways)
                {
                    wayToLoop.Add(w, loop);
                }
            }
            return wayToLoop;
        }

        private List<EulerPathway>? GetPathToStorage(PathLoop current, Dictionary<EulerNode, int> storages, Dictionary<EulerPathway, PathLoop> wayToLoop,
            Dictionary<EulerPathway, int> mailSinceStorage, Dictionary<EulerPathway, int> mailTowardsStorage,
            float acceptableMail, float inacceptableMail)
        {
            List<EulerPathway>? bestPath = null;
            int bestScore = -1;

            foreach (EulerPathway way in current.Ways)
            {
                int since = mailSinceStorage[way];
                int to = mailTowardsStorage[way];
                if (since < acceptableMail || since + to < inacceptableMail)
                {
                    continue;
                }

                if (_nodeToLoops[way.Towards.Index].Count < 2)
                {
                    continue;
                }

                int score = since * to;
                if (score < bestScore)
                {
                    continue;
                }

                float bestLength = float.MaxValue;
                foreach (EulerNode storage in storages.Keys)
                {
                    IPathfinder<EulerNode, EulerPathway> path = IPathfinder.FindPath<EulerNode, EulerPathway>(way.Towards, storage,
                       w => PathWeightFunc(w, wayToLoop));

                    if (!path.Success || path.Count == 0)
                    {
                        continue;
                    }

                    List<EulerPathway> pathList = path.GetPaths().ToList();
                    if (wayToLoop[pathList[0]] == current)
                    {
                        continue;
                    }

                    pathList.Insert(0, way);

                    float length = GetLength(pathList);
                    if (length > bestLength)
                    {
                        continue;
                    }

                    float mail = pathList.Where(w => w != way).Sum(w => w.Eulerpath.MailAmount) * 0.8f;
                    if (mail > to)
                    {
                        continue;
                    }

                    bestLength = length;
                    bestPath = pathList;
                    bestScore = score;
                }
            }

            return bestPath;
        }

        private float GetLength(List<EulerPathway> ways)
        {
            float length = 0;
            for (int i = 1; i < length; i++)
            {
                EulerPathway current = ways[i];
                EulerPathway prev = ways[i - 1];
                length += current.Length;
                length += prev.Towards.GetPassingDistance(prev, current);
            }
            return length;
        }

        private float PathWeightFunc(EulerPathway way, Dictionary<EulerPathway, PathLoop> wayToLoop)
        {
            if (!wayToLoop.ContainsKey(way))
            {
                return -1;
            }

            return way.LeftOfWay(true) == false ? 1 : 1.01f;
        }

        private bool AddNodeLoopsToPath(List<EulerPathway> ways, PathLoop current, Dictionary<EulerPathway, PathLoop> wayToLoop)
        {
            bool added = false;
            for (int i = 0; i < ways.Count - 1; i++)
            {
                EulerPathway way = ways[i];
                EulerNode n = way.Towards;
                EulerPathway next = ways[i + 1];

                if (wayToLoop[way] == wayToLoop[next])
                {
                    continue;
                }

                if (!_nodeLoops.Contains(n))
                {
                    _nodeLoops.Add(n);
                    added = true;
                }
            }
            return added;
        }

        private void UpdateStorages(Dictionary<EulerNode, int> storages, EulerNode usedStorage)
        {
            storages[usedStorage] -= 1;
            if (storages[usedStorage] <= 0)
            {
                storages.Remove(usedStorage);
            }
        }
    }
}