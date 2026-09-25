//Copyright Thomas Greshake 2026

namespace Brieffreund.Eulermap
{
    internal interface IEulerpathCreator
    {
        public void CreatePaths();
    }
}

namespace Brieffreund.Eulermap.Functions
{
    internal class EulerpathCreator : IEulerpathCreator
    {
        private readonly IEulerMap _map;
        private readonly PathingCount[] _pathingMap;

        internal EulerpathCreator(IEulerMap map, PathingCount[] pathingMap)
        {
            _map = map;
            _pathingMap = pathingMap;
        }

        public void CreatePaths()
        {
            foreach (StreetSegment segment in _map.Streetmap.Segments)
            {
                int count = (int)_pathingMap[segment.Index];
                if (count == 0)
                {
                    continue;
                }

                EulerNode from = EulerNode.GetOrCreate(_map, segment.From);
                EulerNode to = EulerNode.GetOrCreate(_map, segment.To);

                bool left = segment.LeftMailAmount > 0;
                bool right = segment.RightMailAmount > 0;

                if (count == 1)
                {
                    if (left && right)
                    {
                        bool startsLeft = segment.SinglePathingOrder[0].LeftOfPath;
                        bool endsLeft = segment.SinglePathingOrder[segment.SinglePathingOrder.Count - 1].LeftOfPath;

                        EulerPath.Create(_map, segment, from, to, startsLeft, endsLeft);
                    }
                    else if (left)
                    {
                        EulerPath.Create(_map, segment, from, to, true, true);
                    }
                    else if (right)
                    {
                        EulerPath.Create(_map, segment, from, to, false, false);
                    }
                    else
                    {
                        EulerPath.Create(_map, segment, from, to, null, null);
                    }
                    continue;
                }

                if (left && right)
                {
                    EulerPath.Create(_map, segment, from, to, true, true);
                    EulerPath.Create(_map, segment, from, to, false, false);
                }
                else
                {
                    EulerPath.Create(_map, segment, from, to, null, null);
                    if (left)
                    {
                        EulerPath.Create(_map, segment, from, to, true, true);
                    }
                    else if (right)
                    {
                        EulerPath.Create(_map, segment, from, to, false, false);
                    }
                    else
                    {
                        EulerPath.Create(_map, segment, from, to, null, null);
                    }
                }

                count -= 2;

                for (int i = 0; i < count; i++)
                {
                    EulerPath.Create(_map, segment, from, to, null, null);
                }
            }
        }
    }
}