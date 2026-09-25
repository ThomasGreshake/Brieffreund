//Copyright Thomas Greshake 2026

using System.Numerics;

namespace Brieffreund.Streetmap
{
    internal interface IStartAndEndFinder : IEvent<IStartAndEndFinder>
    {
        public IStreetMap Map { get; }

        public Intersection[] StartAndEndIntersections { get; }

        public bool FindStartAndEnd();
    }
}

namespace Brieffreund.Streetmap.Functions
{
    internal class StartAndEndFinder : IStartAndEndFinder
    {
        private readonly IStreetMap _map;
        public IStreetMap Map => _map;

        private readonly Intersection[] _startAndEndIntersections = new Intersection[2];
        public Intersection[] StartAndEndIntersections => _startAndEndIntersections;

        internal StartAndEndFinder(IStreetMap map)
        {
            _map = map;
        }

        public bool FindStartAndEnd()
        {
            int existingCount = SetExisting();
            if (existingCount == 2)
            {
                return false;
            }

            int[] counts = GetMinimumPathcounts();
            List<Intersection> uneven = GetUnevenIntersections(counts);
            RemoveOneByOne(uneven, existingCount);
            IStartAndEndFinder.Call(this);
            return true;
        }

        private int[] GetMinimumPathcounts()
        {
            int[] counts = new int[_map.Segments.Count];
            for (int i = 0; i < _map.Segments.Count; i++)
            {
                StreetSegment segment = _map.Segments[i];
                if (!segment.IsRequiredOnRoute)
                {
                    counts[i] = 0;
                    continue;
                }

                if (segment.SinglePathingLength < 0 || segment.SinglePathingLength > 1.9f * segment.Length)
                {
                    counts[i] = 2;
                    continue;
                }

                counts[i] = 1;
            }
            return counts;
        }

        private int SetExisting()
        {
            int count = 0;
            for (int i = 0; i < 2; i++)
            {
                Intersection? inter = _map.StartAndEndIntersections[i];
                if (inter != null)
                {
                    _startAndEndIntersections[i] = inter;
                    count += 1;
                }
            }

            return count;
        }

        private bool IsEven(Intersection inter, int[] counts) =>
            ((_map.StartAndEndIntersections[0] == inter ? 1 : 0)
            + (_map.StartAndEndIntersections[1] == inter ? 1 : 0)
            + inter.Segments.Where(s => s.IsActive).Sum(s => counts[s.Index])) % 2 == 0;

        private List<Intersection> GetUnevenIntersections(int[] counts)
        {
            List<Intersection> uneven = new(2 * _map.Intersections.Count / 3);
            foreach (Intersection inter in _map.Intersections.Values)
            {
                if (IsEven(inter, counts))
                {
                    continue;
                }

                uneven.Add(inter);
            }
            return uneven;
        }

        private void RemoveOneByOne(List<Intersection> uneven, int existingCount)
        {
            if ((uneven.Count + existingCount) % 2 == 1)
            {
                throw new Exception();
            }

            while (uneven.Count > 2)
            {
                Intersection a = uneven[0];
                Intersection b = uneven[1];
                float distance = float.MaxValue;

                for (int i = 0; i < uneven.Count; i++)
                {
                    Intersection iInter = uneven[i];

                    for (int j = i + 1; j < uneven.Count; j++)
                    {
                        Intersection jInter = uneven[j];

                        float dist = Vector2.DistanceSquared(iInter.Position, jInter.Position);
                        if (dist >= distance)
                        {
                            continue;
                        }

                        distance = dist;
                        a = iInter;
                        b = jInter;
                    }
                }

                uneven.Remove(a);
                uneven.Remove(b);
            }

            if (uneven.Count == 1)
            {
                if (existingCount != 1)
                {
                    throw new Exception();
                }

                _startAndEndIntersections[_map.StartAndEndIntersections[0] == null ? 0 : 1] = uneven[0];
                return;
            }

            if (existingCount != 0)
            {
                throw new Exception();
            }

            _startAndEndIntersections[0] = uneven[0];
            _startAndEndIntersections[1] = uneven[1];
        }
    }
}