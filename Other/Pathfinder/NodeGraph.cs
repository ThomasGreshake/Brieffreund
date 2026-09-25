//Copyright Thomas Greshake 2026

//CURRENTLY BUGGED + not even that much faster

using Priority_Queue;
using System.Numerics;

namespace Brieffreund.AStar
{
    [Obsolete] //obsolete until bug is found
    internal class NodeGraph<N, P> : IPathfinder<N, P> where N : class, IPathnode<N, P> where P : class, IPathway<N, P>
    {
        private bool _success = true, _cleared = true;
        public bool Success => _success;

        private readonly IList<N> _nodes;
        private readonly PathNodeData[] _nodeData;

        private readonly IList<P> _pathways;
        private readonly PathwayData[] _wayData;

        private IPriorityQueue<int> _openSet = new SimplePriorityQueue<int>();

        private readonly List<P> _path = new();
        public P this[int index] => _path[_path.Count - index - 1];
        public int Count => _path.Count;

        internal NodeGraph(N node, Func<P, float> pathCostMultFunc) : this(FloodFill(node), pathCostMultFunc)
        {
        }

        internal NodeGraph(IList<N> nodes, Func<P, float> pathCostMultFunc)
        {
            _nodes = nodes;
            _nodeData = new PathNodeData[nodes.Count];

            int estimatedWayCount = nodes.Count * 2;
            _pathways = new List<P>(estimatedWayCount);
            List<PathwayData> wayDataList = new List<PathwayData>(estimatedWayCount);
            for (int i = 0; i < nodes.Count; i++)
            {
                N node = nodes[i];
                IList<P> nodePaths = node.GetPaths();
                int startIndex = _pathways.Count;

                for (int j = 0; j < nodePaths.Count; j++)
                {
                    P pathway = nodePaths[j];
                    float pathCost = pathCostMultFunc(pathway) * pathway.Length;

                    PathwayData wayData = new PathwayData(_pathways.Count, i, nodes.IndexOf(pathway.Towards), pathCost);
                    _pathways.Add(pathway);
                    wayDataList.Add(wayData);
                }

                PathNodeData nodeData = new PathNodeData(i, node.Position, startIndex, _pathways.Count);
                _nodeData[i] = nodeData;
            }

            _wayData = wayDataList.ToArray();
        }

        internal NodeGraph(NodeGraph<N, P> graph)
        {
            _nodes = graph._nodes;
            _nodeData = new PathNodeData[_nodes.Count];
            for (int i = 0; i < _nodeData.Length; i++)
            {
                _nodeData[i] = graph._nodeData[i];
            }

            _pathways = graph._pathways;
            _wayData = new PathwayData[_pathways.Count];
            for (int i = 0; i < _wayData.Length; i++)
            {
                _wayData[i] = graph._wayData[i];
            }

            _cleared = graph._cleared;
            Clear();
        }

        public IPathfinder<N, P> FindPath(N start, N end)
        {
            Clear();
            _cleared = false;

            int startIndex = _nodes.IndexOf(start);
            if (startIndex < 0)
            {
                _success = false;
                return this;
            }

            int endIndex = _nodes.IndexOf(end);
            if (endIndex < 0)
            {
                _success = false;
                return this;
            }

            PathNodeData[] nodeData = _nodeData;
            PathwayData[] wayData = _wayData;
            PathNodeData startData = nodeData[startIndex];

            startData.Length = 0;
            startData.EstimatedLength = Vector2.Distance(startData.Position, end.Position);
            nodeData[startIndex] = startData;

            IPriorityQueue<int> openSet = _openSet;
            openSet.Enqueue(startIndex, 0);

            while (openSet.Count > 0)
            {
                int currentIndex = openSet.Dequeue();
                PathNodeData current = nodeData[currentIndex];

                if (currentIndex == endIndex)
                {
                    PathNodeData last = ConstructPath(current);

                    if (last.Index != startIndex)
                    {
                        throw new Exception(); // <--------------------------------------------------------- Sometimes throws
                    }

                    return this;
                }

                current.IsClosed = true;
                nodeData[currentIndex] = current;

                for (int i = current.StartIndex; i < current.EndIndex; i++)
                {
                    PathwayData way = wayData[i];
                    if (way.PathCost < 0 || way.TowardsIndex < 0)
                    {
                        continue;
                    }

                    PathNodeData node = nodeData[way.TowardsIndex];
                    if (node.IsClosed)
                    {
                        continue;
                    }

                    int nIndex = node.Index;
                    float length = current.Length + way.PathCost;
                    if (length >= node.Length)
                    {
                        continue;
                    }

                    node.CameFromWayIndex = i;
                    node.Length = length;
                    float estimate = length + Vector2.Distance(node.Position, end.Position);
                    node.EstimatedLength = estimate;

                    nodeData[nIndex] = node;

                    if (openSet.Contains(nIndex))
                    {
                        openSet.UpdatePriority(nIndex, estimate);
                    }
                    else
                    {
                        openSet.Enqueue(nIndex, estimate);
                    }
                }
            }

            _success = false;
            return this;
        }

        public void ChangePathcost(P path, float cost)
        {
            int index = _pathways.IndexOf(path);
            if (index < 0)
            {
                return;
            }

            PathwayData data = _wayData[index];
            _wayData[index] = new PathwayData(data.Index, data.OriginIndex, data.TowardsIndex, cost);
        }

        public void ChangePathcost(P path, Func<P, float> pathCostMultFunc) => ChangePathcost(path, pathCostMultFunc(path) * path.Length);

        public void ChangePathcosts(Func<P, float> pathCostMultFunc)
        {
            for (int i = 0; i < _pathways.Count; i++)
            {
                P path = _pathways[i];
                float cost = pathCostMultFunc(path) * path.Length;
                PathwayData wayData = _wayData[i];
                _wayData[i] = new PathwayData(wayData.Index, wayData.OriginIndex, wayData.TowardsIndex, cost);
            }
        }

        private void Clear()
        {
            if (_cleared)
            {
                return;
            }

            _success = true;
            _cleared = true;
            _path.Clear();
            _openSet.Clear();

            for (int i = 0; i < _nodeData.Length; i++)
            {
                PathNodeData nodeData = _nodeData[i];
                nodeData.CameFromWayIndex = -1;
                nodeData.Length = float.MaxValue;
                nodeData.EstimatedLength = float.MaxValue;
                nodeData.IsClosed = false;
                _nodeData[i] = nodeData;
            }
        }

        private PathNodeData ConstructPath(PathNodeData end)
        {
            PathNodeData current = end;
            while (current.CameFromWayIndex > 0)
            {
                PathwayData wayData = _wayData[current.CameFromWayIndex];
                P pathway = _pathways[wayData.Index];
                _path.Add(pathway);
                current = _nodeData[wayData.OriginIndex];
            }
            return current;
        }

        private static List<N> FloodFill(N node)
        {
            List<N> nodes = new();
            List<N> front = new() { node };

            while (front.Count > 0)
            {
                int index = front.Count - 1;
                N current = front[index];
                front.RemoveAt(index);
                nodes.Add(current);
                IList<P> paths = current.GetPaths();
                for (int i = 0; i < paths.Count; i++)
                {
                    N other = paths[i].Towards;
                    if (front.Contains(other) || nodes.Contains(other))
                    {
                        continue;
                    }
                    front.Add(other);
                }
            }

            return nodes;
        }

        private struct PathNodeData
        {
            internal readonly int Index;
            internal readonly int StartIndex, EndIndex;
            internal readonly Vector2 Position;

            internal int CameFromWayIndex = -1;
            internal float Length = float.MaxValue;
            internal float EstimatedLength = float.MaxValue;
            internal bool IsClosed = false;

            internal PathNodeData(int index, Vector2 position, int startIndex, int endIndex)
            {
                Index = index;
                Position = position;
                StartIndex = startIndex;
                EndIndex = endIndex;
            }
        }

        private struct PathwayData
        {
            internal readonly int Index, TowardsIndex, OriginIndex;
            internal readonly float PathCost;

            internal PathwayData(int index, int originIndex, int towardsIndex, float pathCost)
            {
                Index = index;
                OriginIndex = originIndex;
                TowardsIndex = towardsIndex;
                PathCost = pathCost;
            }
        }
    }
}