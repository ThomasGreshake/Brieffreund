//Copyright Thomas Greshake 2026

using System.Numerics;

namespace Brieffreund.Streetmap
{
    internal interface ISidewalkHandler
    {
        public IList<StreetSegment> SegmentsWithSidewalks { get; }

        public void HandleSidewalks();
    }
}

namespace Brieffreund.Streetmap.Functions
{
    internal class SidewalkDeleter : ISidewalkHandler
    {
        private const float MAX_SIDEWALK_DISTANCE_METER = 12f;

        private const int MAX_SIDEWALK_LOOP_COUNT = 10;

        private readonly IStreetMap _map;

        private readonly IList<StreetSegment> _segmentsWithSidewalks = new List<StreetSegment>();
        public IList<StreetSegment> SegmentsWithSidewalks => _segmentsWithSidewalks;

        internal SidewalkDeleter(IStreetMap map)
        { _map = map; }

        public void HandleSidewalks()
        {
            Dictionary<float, List<SegmentPathway>> loops = new();

            foreach (StreetSegment segment in _map.Segments)
            {
                if (segment.Type != StreetType.Tiny)
                {
                    continue;
                }

                if (GetLoop(segment.Forward, out List<SegmentPathway> forwardLoop))
                {
                    ValidateAndAddLoop(forwardLoop, loops);
                }
                if (GetLoop(segment.Backward, out List<SegmentPathway> backwardLoop))
                {
                    ValidateAndAddLoop(backwardLoop, loops);
                }
            }

            List<StreetSegment> toRemove = new();

            foreach (var loop in loops.Values)
            {
                SegmentPathway? longest = loop.Where(s => s.Segment.Type == StreetType.Tiny).MaxBy(s => s.Length);
                if (longest == null)
                {
                    continue;
                }

                StreetSegment segment = longest.Segment;

                float fullLength = loop.Sum(s => s.Length);
                float sideLength = 0.5f * Math.Max(0.5f * fullLength, fullLength - MAX_SIDEWALK_DISTANCE_METER);

                if (longest.Length < 0.8f * sideLength || sideLength < 0.8f * longest.Length)
                {
                    continue;
                }

                foreach (StreetSegment seg in loop.Select(l => l.Segment).Where(s => s.Type != StreetType.Tiny && !_segmentsWithSidewalks.Contains(s)))
                {
                    _segmentsWithSidewalks.Add(seg);
                }

                if (toRemove.Contains(segment))
                {
                    continue;
                }

                toRemove.Add(segment);
            }

            if (toRemove.Any(s => s.ReceivesMail))
            {
                throw new Exception();
            }

            int count = toRemove.Count;
            if (count > 0)
            {
                StreetSegment.Delete(toRemove);
            }
        }

        private static bool GetLoop(SegmentPathway start, out List<SegmentPathway> loop)
        {
            loop = new(MAX_SIDEWALK_LOOP_COUNT + 1) { start };

            SegmentPathway next = start;

            for (int i = 0; i < MAX_SIDEWALK_LOOP_COUNT; i++)
            {
                next = GetNext(next.Towards, next);

                if (next == loop[loop.Count - 1]) { return false; }
                if (next == start) { return true; }

                loop.Add(next);
            }
            return false;
        }

        private static SegmentPathway GetNext(Intersection inter, SegmentPathway way)
        {
            StreetPathway pathway = way.GetIncomingway();
            int index = inter.Streetnode.Pathways.IndexOf(pathway.GetOpposite());

            if (index < 0)
            {
                throw new Exception();
            }

            return inter.Pathways[(index + 1) % inter.Count];
        }

        private static void ValidateAndAddLoop(List<SegmentPathway> loop, Dictionary<float, List<SegmentPathway>> loops)
        {
            if (loop.Count == 0) { return; }

            float circumference = loop.Sum(s => s.Length);
            List<Vector2> positions = new(loop.Sum(l => l.Segment.Paths.Count));

            foreach (SegmentPathway s in loop)
            {
                IList<StreetPath> paths = s.Segment.Paths;
                bool forward = s.Segment.Forward == s;

                for (int i = 0; i < paths.Count; i++)
                {
                    StreetPathway way = forward ? paths[i].Forward : paths[paths.Count - i - 1].Backward;
                    positions.Add(way.Towards.Position);
                }
            }

            float area = (float)Math.Round(MyMath.PolygonArea(positions), 4); //Area double functions as a hash for the loop
            if (loops.ContainsKey(area))
            {
                return;
            }

            float squareLength = circumference / 4f;
            float det = squareLength * squareLength - area;
            if (det < 0) { det = 0; }
            float distance = squareLength - (float)Math.Sqrt(det);

            if (distance > MAX_SIDEWALK_DISTANCE_METER)
            {
                return;
            }

            loops.Add(area, loop);
        }
    }
}