//Copyright Thomas Greshake 2026

using Brieffreund.Eulermap;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;

namespace Brieffreund.Routemap
{
    internal class PathLoop
    {
        internal static readonly PathLoop Empty = new();

        //Data -----------------------------------------------------------------------

        private int _index = -1;
        internal int Index => _index;

        internal readonly ReadOnlyCollection<EulerPathway> Ways;

        internal readonly bool IsClosed;

        //Setup -----------------------------------------------------------------------

        internal static List<PathLoop> CreateLoops(IEulerMap map, INodeConnectionCreator connectionCreator,
            out PathLoop[] pathToLoop, out List<PathLoop>[] nodeToLoops, out float[] nodeScores)
        {
            pathToLoop = new PathLoop[map.PathCount];
            nodeToLoops = new List<PathLoop>[map.NodeCount];

            int[][] connections = connectionCreator.CreateConnections(map, out nodeScores);
            List<PathLoop> loops = CreateLoops(map, connections, pathToLoop, nodeToLoops);

            //Inverts the connections on nodes that connect loops on dead ends with the rest of the district, eliminating these loops in the process by connecting dead ends to the rest
            ConnectDeadendsByInvertingNodeConnections(map, connectionCreator, loops, connections, pathToLoop, nodeToLoops, nodeScores);

            //Inverts some back to the original state, if the original inversion did not solve the connection issue, then the original state is preferable
            ConnectDeadendsByInvertingNodeConnections(map, connectionCreator, loops, connections, pathToLoop, nodeToLoops, nodeScores);

            for (int i = 0; i < loops.Count; i++)
            {
                PathLoop loop = loops[i];
                loop._index = i;
            }

            Debug.Assert(nodeToLoops.All(n => n.Count > 0 && !n.Contains(Empty)));
            Debug.Assert(pathToLoop.All(l => l != null && l != Empty));

            return loops;
        }

        private static List<PathLoop> CreateLoops(IEulerMap map, int[][] connections, PathLoop[] pathToLoop, List<PathLoop>[] nodeToLoops)
        {
            List<EulerPath> allPaths = map.Paths.Where(p => pathToLoop[p.Index] == null || pathToLoop[p.Index] == Empty).ToList();
            List<PathLoop> loops = new();

            EulerPath initial;
            EulerNode start = map.StartAndEndNode[0];
            bool initialIsClosed = start.Count % 2 == 0;

            if (!initialIsClosed)
            {
                int[] connection = connections[start.Index];
                int index = 0;

                for (int i = 0; i < start.Count; i++)
                {
                    EulerPathway incoming = start.Pathways[i].GetOppositeDirection();
                    EulerPathway outgoing = start.Pathways[connection[i]];

                    if (incoming.GetOppositeDirection() != outgoing)
                    {
                        continue;
                    }

                    index = i;
                    break;
                }

                initial = start.Pathways[index].GetOppositeDirection().Eulerpath;
            }
            else
            {
                initial = start.Paths.First();
            }

            if (pathToLoop[initial.Index] == null || pathToLoop[initial.Index] == Empty)
            {
                List<EulerPathway> initialPaths = new();

                bool forward = initial.From == start;
                FollowPath(forward ? initial.Forward : initial.Backward, connections, initialPaths);

                PathLoop initialLoop = Create(initialPaths, initialIsClosed, pathToLoop, nodeToLoops);
                loops.Add(initialLoop);

                foreach (EulerPathway way in initialPaths)
                {
                    bool removed = allPaths.Remove(way.Eulerpath);
                    Debug.Assert(removed);
                }
            }

            while (allPaths.Count > 0)
            {
                List<EulerPathway> loopPaths = new();

                EulerPath startPath = allPaths[allPaths.Count - 1];
                FollowPath(startPath.Forward, connections, loopPaths);

                PathLoop loop = Create(loopPaths, true, pathToLoop, nodeToLoops);
                loops.Add(loop);

                foreach (EulerPathway way in loopPaths)
                {
                    bool removed = allPaths.Remove(way.Eulerpath);
                    Debug.Assert(removed);
                }
            }

            return loops;
        }

        private static void FollowPath(EulerPathway current, int[][] connections, List<EulerPathway> paths)
        {
            paths.Add(current);

            EulerNode nextNode = current.Towards;
            int currentIndex = nextNode.Pathways.IndexOf(current.GetOppositeDirection());
            int nextIndex = connections[nextNode.Index][currentIndex];
            EulerPathway next = nextNode.Pathways[nextIndex];

            if (current.GetOppositeDirection() == next)
            {
                Debug.Assert(nextNode.Intersection.IsEnd);
                return;
            }

            if (paths.Contains(next))
            {
                Debug.Assert(paths[0] == next);
                return;
            }

            FollowPath(next, connections, paths);
        }

        private static PathLoop Create(List<EulerPathway> ways, bool isClosed, PathLoop[] pathToLoop, List<PathLoop>[] nodeToLoops)
        {
            ways = HandleDirection(ways, isClosed);

            PathLoop loop = new(ways, isClosed);

            foreach (var way in ways)
            {
                pathToLoop[way.Index] = loop;
            }
            foreach (var node in loop.GetNodes())
            {
                if (nodeToLoops[node.Index] == null)
                {
                    nodeToLoops[node.Index] = new List<PathLoop>();
                }

                List<PathLoop> nodeLoops = nodeToLoops[node.Index];
                if (!nodeLoops.Contains(loop))
                {
                    nodeLoops.Add(loop);
                }
            }

            return loop;
        }

        private PathLoop(List<EulerPathway> paths, bool isClosed)
        {
            Ways = paths.AsReadOnly();
            IsClosed = isClosed;
        }

        private PathLoop()
        {
            Ways = ReadOnlyCollection<EulerPathway>.Empty;
            IsClosed = true;
        }

        private static void ConnectDeadendsByInvertingNodeConnections(IEulerMap map, INodeConnectionCreator connectionCreator,
            List<PathLoop> pathLoops, int[][] connections, PathLoop[] pathToLoop, List<PathLoop>[] nodeToLoops, float[] nodeScores)
        {
            List<EulerNode> inverted = new List<EulerNode>();

            while (true)
            {
                EulerNode? node = map.Nodes.Values.Where(n => n.Intersection.Segments.All(s => s.IsRouteDeadEnd) && n.Count % 2 != 1
                      && nodeToLoops[n.Index].Count > 1 && !inverted.Contains(n))
                    .MinBy(n => n.PassingCircumference - Constants.NODE_SCORE_MULT * nodeScores[n.Index]);

                if (node == null)
                {
                    return;
                }

                inverted.Add(node);
                connectionCreator.CreateConnectionsDeadEnd(node, out int[] cons, out int[] opp);
                connections[node.Index] = opp;
                nodeScores[node.Index] = node.PassingCircumference - nodeScores[node.Index];

                foreach (PathLoop loop in nodeToLoops[node.Index].ToList())
                {
                    pathLoops.Remove(loop);
                    foreach (EulerPathway way in loop.Ways)
                    {
                        pathToLoop[way.Index] = Empty;
                    }
                    foreach (EulerNode n in loop.GetNodes())
                    {
                        nodeToLoops[n.Index].Remove(loop);
                    }
                }

                pathLoops.AddRange(CreateLoops(map, connections, pathToLoop, nodeToLoops));
            }
        }

        private static List<EulerPathway> HandleDirection(List<EulerPathway> ways, bool isClosed)
        {
            List<object> subLoops = SplitIntoSubLoopsAndHandleDirection(new List<object>(ways), isClosed);
            return RecombineSubLoops(subLoops);
        }

        private static List<object> SplitIntoSubLoopsAndHandleDirection(List<object> subLoop, bool isClosed)
        {
            int start = -1;
            int end = -1;

            for (int i = 0; i < subLoop.Count; i++)
            {
                if (subLoop[i] is not EulerPathway iWay)
                {
                    continue;
                }

                for (int j = subLoop.Count - 1; j >= i; j--)
                {
                    if (subLoop[j] is not EulerPathway jWay)
                    {
                        continue;
                    }

                    if ((i != 0 || j != subLoop.Count - 1) && iWay.Origin == jWay.Towards &&
                        jWay.Towards.GetPassingDistance(jWay, iWay) < Constants.CONNECTS_DIRECTLY_PASSING_DISTANCE)
                    {
                        start = i;
                        end = j;
                        break;
                    }
                    else if (j > i && iWay.Towards == jWay.Towards &&
                        iWay.Towards.GetPassingDistance(iWay, jWay.GetOppositeDirection()) < Constants.CONNECTS_DIRECTLY_PASSING_DISTANCE)
                    {
                        start = i + 1;
                        end = j;
                        break;
                    }
                    else if (j > i && iWay.Origin == jWay.Origin &&
                        iWay.Origin.GetPassingDistance(iWay.GetOppositeDirection(), jWay) < Constants.CONNECTS_DIRECTLY_PASSING_DISTANCE)
                    {
                        start = i;
                        end = j - 1;
                        break;
                    }
                    else if (j > i + 1 && iWay.Towards == jWay.Origin &&
                        iWay.Towards.GetPassingDistance(iWay, jWay) < Constants.CONNECTS_DIRECTLY_PASSING_DISTANCE)
                    {
                        start = i + 1;
                        end = j - 1;

                        bool ok = false;
                        for (int k = start; k <= end; k++)
                        {
                            if (subLoop[k] is EulerPathway)
                            {
                                ok = true;
                                break;
                            }
                        }

                        if (ok)
                        {
                            break;
                        }
                        else
                        {
                            start = -1;
                            end = -1;
                        }
                    }
                }

                if (start >= 0 && end >= 0)
                {
                    break;
                }
            }

            if (start == -1 || end == -1)
            {
                if (isClosed)
                {
                    HandleLoopDirection(subLoop);
                }
                return subLoop;
            }

            List<object> firstList = new();
            List<object> secondList = new();

            for (int i = 0; i < start; i++)
            {
                firstList.Add(subLoop[i]);
            }

            for (int i = start; i <= end; i++)
            {
                secondList.Add(subLoop[i]);
            }

            secondList = SplitIntoSubLoopsAndHandleDirection(secondList, true);
            firstList.Add(secondList);

            for (int i = end + 1; i < subLoop.Count; i++)
            {
                firstList.Add(subLoop[i]);
            }

            return SplitIntoSubLoopsAndHandleDirection(firstList, isClosed);
        }

        private static bool HandleLoopDirection(List<object> subLoop)
        {
            float rightScore = 0f;

            for (int i = 0; i < subLoop.Count; i++)
            {
                if (subLoop[i] is not EulerPathway way)
                {
                    continue;
                }

                rightScore += way.GetSideScore();
            }

            if (rightScore >= 0)
            {
                return false;
            }

            subLoop.Reverse();

            for (int i = 0; i < subLoop.Count; i++)
            {
                if (subLoop[i] is not EulerPathway way)
                {
                    continue;
                }

                subLoop[i] = way.GetOppositeDirection();
            }

            return true;
        }

        private static List<EulerPathway> RecombineSubLoops(List<object> subLoop)
        {
            List<EulerPathway> list = new();
            foreach (object o in subLoop)
            {
                if (o is EulerPathway way)
                {
                    list.Add(way);
                }
                else if (o is List<object> innerLoop)
                {
                    list.AddRange(RecombineSubLoops(innerLoop));
                }
                else
                {
                    throw new Exception();
                }
            }
            return list;
        }

        private IEnumerable<EulerNode> GetNodes()
        {
            if (!IsClosed)
            {
                yield return Ways[0].Origin;
            }
            foreach (EulerPathway way in Ways)
            {
                yield return way.Towards;
            }
        }
    }
}