//Copyright Thomas Greshake 2026

using Spectre.Console;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using static System.Formats.Asn1.AsnWriter;

namespace Brieffreund.Eulermap
{
    internal interface IEulerpathSideDeterminator
    {
        public void DetermineSides();
    }
}

namespace Brieffreund.Eulermap.Functions
{
    internal class EulerpathSideDeterminator : IEulerpathSideDeterminator
    {
        private readonly IEulerMap _map;
        private readonly INodeConnectionCreator _connectionCreator;

        private readonly record struct BranchExpansion(bool?[] Left, float LeftScore, bool?[] Right, float RightScore);

        private List<EulerPath> _currentGroup = new();
        private PriorityQueue<bool?[], (float Score, long Order)> _queue = new(); //Order makes ties deterministic
        private long _enqueueCount = 0;
        private bool?[]? _result = null;

        internal EulerpathSideDeterminator(IEulerMap map, INodeConnectionCreator nodeConnectionCreator)
        {
            _map = map;
            _connectionCreator = nodeConnectionCreator;
        }

        public void DetermineSides()
        {
            DetermineNormalPathSides();
            DetermineTinyPathSides();
            CheckResult();
        }

        private void DetermineNormalPathSides()
        {
            List<List<EulerPath>> groups = GetGroupings();

            foreach (List<EulerPath> group in groups)
            {
                Clear();
                _currentGroup = group;

                bool?[] initial = CreateInitialBranch();
                Enqueue(initial, 8192);
                Search();

                SetSidesOnMap();
            }

            _queue = new();
            _currentGroup = new();
        }

        private void DetermineTinyPathSides()
        {
            List<EulerPath> tinyPaths = _map.Paths.Where(p => p.Segment.Type == StreetType.Tiny && p.FromLeft == null).ToList();
            _currentGroup = new List<EulerPath>(tinyPaths);
            if (_result == null)
            {
                _result = CreateInitialBranch();
            }

            while (tinyPaths.Count > 0)
            {
                int index = 0;
                int uncertainCount = int.MaxValue;
                bool?[] result = _result;

                for (int i = 0; i < tinyPaths.Count; i++)
                {
                    EulerPath path = tinyPaths[i];
                    int count = path.From.Paths.Count(p => result[2 * p.Index] == null) + path.To.Paths.Count(p => result[2 * p.Index] == null);
                    if (count < uncertainCount)
                    {
                        index = i;
                        uncertainCount = count;
                    }
                }

                EulerPath currentPath = tinyPaths[index];
                tinyPaths.RemoveAt(index);

                bool?[] leftBranch = CreateNewBranch(_result, currentPath, true);
                bool?[] rightBranch = CreateNewBranch(_result, currentPath, false);
                CalculateScores(_result, leftBranch, rightBranch, 0, currentPath, out float leftScore, out float rightScore);

                if (leftScore < rightScore)
                {
                    _result = leftBranch;
                }
                else
                {
                    _result = rightBranch;
                }
            }

            SetSidesOnMap();
        }

        //Expands the best branches in parallel rounds. Results are merged in queue order, so the outcome does not depend on thread timing
        private void Search()
        {
            List<(bool?[] Branch, float Score)> round = new(Constants.SEARCH_ROUND_SIZE);

            while (_queue.Count > 0 && _result == null)
            {
                round.Clear();
                while (round.Count < Constants.SEARCH_ROUND_SIZE && _queue.TryDequeue(out bool?[]? branch, out var priority))
                {
                    round.Add((branch, priority.Score));
                }

                BranchExpansion?[] expansions = new BranchExpansion?[round.Count];
                ParallelRunner.For(round.Count, i => expansions[i] = Expand(round[i].Branch, round[i].Score));

                for (int i = 0; i < round.Count; i++)
                {
                    BranchExpansion? expansion = expansions[i];
                    if (expansion == null)
                    {
                        _result = round[i].Branch;
                        return;
                    }

                    Enqueue(expansion.Value.Right, expansion.Value.RightScore);
                    Enqueue(expansion.Value.Left, expansion.Value.LeftScore);
                }
            }
        }

        //Returns null if the branch is fully determined
        private BranchExpansion? Expand(bool?[] currentBranch, float score)
        {
            EulerPath? currentPath = null;
            foreach (EulerPath p in _currentGroup)
            {
                if (currentBranch[p.Index * 2] == null)
                {
                    currentPath = p;
                    break;
                }
            }

            if (currentPath == null)
            {
                return null;
            }

            bool?[] leftBranch = CreateNewBranch(currentBranch, currentPath, true);
            bool?[] rightBranch = CreateNewBranch(currentBranch, currentPath, false);
            CalculateScores(currentBranch, leftBranch, rightBranch, score, currentPath, out float leftScore, out float rightScore);

            return new BranchExpansion(leftBranch, leftScore, rightBranch, rightScore);
        }

        private List<List<EulerPath>> GetGroupings()
        {
            List<EulerPath> paths = _map.Paths.Where(p => p.Segment.Type != StreetType.Tiny && p.FromLeft == null).ToList();
            List<List<EulerPath>> groups = new();

            while (paths.Count > 0)
            {
                List<EulerPath> currentGroup = new();
                groups.Add(currentGroup);

                int grabIndex = paths.Count - 1;
                EulerPath start = paths[grabIndex];
                List<EulerPath> front = new() { start };

                while (front.Count > 0)
                {
                    grabIndex = front.Count - 1;
                    EulerPath current = front[grabIndex];
                    front.RemoveAt(grabIndex);
                    currentGroup.Add(current);

                    AddToFront(current.From, currentGroup, front);
                    AddToFront(current.To, currentGroup, front);
                }

                foreach (EulerPath path in currentGroup)
                {
                    bool removed = paths.Remove(path);
                    Debug.Assert(removed);
                }
            }

            return groups;
        }

        private void AddToFront(EulerNode node, List<EulerPath> done, List<EulerPath> front)
        {
            foreach (EulerPath path in node.Paths)
            {
                if (path.FromLeft != null || path.Segment.Type == StreetType.Tiny || done.Contains(path) || front.Contains(path))
                {
                    continue;
                }

                front.Add(path);
            }
        }

        private void CalculateScores(bool?[] oldBranch, bool?[] leftBranch, bool?[] rightBranch, float oldScore, EulerPath path, out float leftScore, out float rightScore)
        {
            oldScore -= _connectionCreator.CreateConnections(path.From, oldBranch);
            oldScore -= _connectionCreator.CreateConnections(path.To, oldBranch);

            leftScore = oldScore;
            leftScore += _connectionCreator.CreateConnections(path.From, leftBranch);
            leftScore += _connectionCreator.CreateConnections(path.To, leftBranch);

            rightScore = oldScore;
            rightScore += _connectionCreator.CreateConnections(path.From, rightBranch);
            rightScore += _connectionCreator.CreateConnections(path.To, rightBranch);

            if (leftScore + 0.001f > rightScore && rightScore + 0.001f > leftScore)
            {
                IList<EulerPath> list = _map.GetPaths(path.Segment);
                int leftCount = list.Count(p => oldBranch[2 * p.Index] == true && oldBranch[2 * p.Index + 1] == true);
                int rightCount = list.Count(p => oldBranch[2 * p.Index] == false && oldBranch[2 * p.Index + 1] == false);
                if (leftCount > rightCount)
                {
                    leftScore += 0.001f;
                }
                else if (rightCount > leftCount)
                {
                    rightScore += 0.001f;
                }
            }
        }

        private void Enqueue(bool?[] branch, float score) => _queue.Enqueue(branch, (score, _enqueueCount++));

        private bool?[] CreateInitialBranch()
        {
            bool?[] initial = new bool?[2 * _map.PathCount];
            foreach (EulerPath path in _map.Paths)
            {
                initial[2 * path.Index] = path.FromLeft;
                initial[2 * path.Index + 1] = path.ToLeft;
            }
            return initial;
        }

        private bool?[] CreateNewBranch(bool?[] original, EulerPath path, bool leftSide)
        {
            bool?[] branch = new bool?[original.Length];
            Array.Copy(original, branch, original.Length);
            branch[path.Index * 2] = leftSide;
            branch[path.Index * 2 + 1] = leftSide;
            return branch;
        }

        private void SetSidesOnMap()
        {
            if (_result == null)
            {
                throw new InvalidOperationException($"No side assignment was found for the current group of {_currentGroup.Count} euler paths.");
            }

            foreach (EulerPath path in _currentGroup)
            {
                bool leftSide = _result[path.Index * 2] == true;
                path.SetSide(leftSide);
            }
        }

        private void Clear()
        {
            _queue.Clear();
            _enqueueCount = 0;
            _result = null;
        }

        private void CheckResult()
        {
            foreach (EulerPath path in _map.Paths)
            {
                Debug.Assert(path.FromLeft != null && path.ToLeft != null);
            }
        }
    }
}