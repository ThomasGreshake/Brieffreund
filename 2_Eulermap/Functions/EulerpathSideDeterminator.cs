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

        private List<EulerPath> _currentGroup = new();
        private PriorityQueue<bool?[], float> _queue = new();
        private Dictionary<bool?[], float> _scores = new();
        private readonly object _lock = new object();
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
            int threadCount = Constants.THREAD_COUNT;
            Thread[] threads = new Thread[threadCount];
            List<List<EulerPath>> groups = GetGroupings();

            foreach (List<EulerPath> group in groups)
            {
                for (int i = 0; i < threadCount; i++)
                {
                    threads[i] = new Thread(Process);
                }

                Clear();
                _currentGroup = group;

                bool?[] initial = CreateInitialBranch();
                Enqueue(initial, 8192);
                SeedQueue(threadCount);

                for (int i = 0; i < threadCount; i++)
                {
                    threads[i].Start();
                }

                Process();

                for (int i = 0; i < threadCount; i++)
                {
                    threads[i].Join();
                }

                SetSidesOnMap();
            }

            _queue = new();
            _scores = new();
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
                EulerPath currentPath = tinyPaths[0];
                int uncertainCount = int.MaxValue;

                foreach (EulerPath path in tinyPaths)
                {
                    int count = path.From.Paths.Count(p => p.FromLeft == null) + path.To.Paths.Count(p => p.FromLeft == null);
                    if (count < uncertainCount)
                    {
                        currentPath = path;
                        uncertainCount = count;
                    }
                }

                tinyPaths.Remove(currentPath);

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

        private void Process()
        {
            while (_queue.Count > 0 && _result == null)
            {
                ProcessNext();
            }
        }

        private void SeedQueue(int threadCount)
        {
            while (_queue.Count > 0 && _result == null && _queue.Count <= 2 * threadCount + 1)
            {
                ProcessNext();
            }
        }

        private void ProcessNext()
        {
            bool?[] currentBranch;
            float score;

            lock (_lock)
            {
                if (_queue.Count == 0)
                {
                    return;
                }

                currentBranch = _queue.Dequeue();
                score = _scores[currentBranch];
            }

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
                lock (_lock)
                {
                    if (_result == null || _scores[_result] > score)
                    {
                        _result = currentBranch;
                    }
                }
                return;
            }

            bool?[] leftBranch = CreateNewBranch(currentBranch, currentPath, true);
            bool?[] rightBranch = CreateNewBranch(currentBranch, currentPath, false);
            CalculateScores(currentBranch, leftBranch, rightBranch, score, currentPath, out float leftScore, out float rightScore);

            Enqueue(rightBranch, rightScore);
            Enqueue(leftBranch, leftScore);
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
                int leftCount = list.Count(p => oldBranch[2 * path.Index] == true && oldBranch[2 * path.Index + 1] == true);
                int rightCount = list.Count(p => oldBranch[2 * path.Index] == false && oldBranch[2 * path.Index + 1] == false);
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

        private void Enqueue(bool?[] branch, float score)
        {
            lock (_lock)
            {
                _queue.Enqueue(branch, score);
                _scores[branch] = score;
            }
        }

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
                throw new Exception();
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
            _scores.Clear();
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