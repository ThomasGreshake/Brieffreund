//Copyright Thomas Greshake 2026

using System.Numerics;

namespace Brieffreund.Streetmap
{
    internal class PolygonCollider
    {
        private readonly List<List<Vector2>> _nodes = new();

        private Vector2 _position;
        internal Vector2 Position => _position;

        private float _spanRadius, _virtualRadius;
        internal float SpanRadius => _spanRadius;
        internal float VirtualRadius => _virtualRadius;

        internal PolygonCollider(List<Vector2> nodes)
        {
            _nodes.Add(nodes);
            SetSpanRadius();
            SetVirtualRadius();
        }

        internal void Merge(PolygonCollider other)
        {
            _nodes.AddRange(other._nodes);
            SetSpanRadius();
            SetVirtualRadius();
        }

        internal List<Vector2> GetCollisions(Vector2 start, Vector2 end)
        {
            List<Vector2> hits = new List<Vector2>();

            if (!MyMath.HitpointLineCircle(start, end, _position, _spanRadius, out _))
            {
                return hits;
            }

            foreach (List<Vector2> nodes in _nodes)
            {
                hits.AddRange(GetCollisions(nodes, start, end));
            }

            return hits;
        }

        internal int GetCollisions(Vector2 start, Vector2 end, out Vector2 firstHit, out Vector2 lastHit)
        {
            List<Vector2> hits = GetCollisions(start, end);
            if (hits.Count == 0)
            {
                firstHit = default;
                lastHit = default;
                return 0;
            }

            firstHit = hits.MinBy(h => Vector2.DistanceSquared(h, start));
            lastHit = hits.MaxBy(h => Vector2.DistanceSquared(h, start));
            return hits.Count;
        }

        private List<Vector2> GetCollisions(List<Vector2> nodes, Vector2 start, Vector2 end)
        {
            List<Vector2> hits = new List<Vector2>();

            for (int i = 0; i < nodes.Count; i++)
            {
                Vector2 a = nodes[i];
                Vector2 b = nodes[(i + 1) % nodes.Count];

                if (MyMath.HitpointLineLine(a, b, start, end, out Vector2 hit))
                {
                    hits.Add(hit);
                }
            }

            return hits;
        }

        internal float GetDistance(Vector2 position)
        {
            float distance = float.MaxValue;

            foreach (var list in _nodes)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    Vector2 from = list[i];
                    Vector2 to = list[(i + 1) % list.Count];

                    float dist = MyMath.DistanceLinePoint(from, to, position);
                    if (dist < distance)
                    {
                        distance = dist;
                    }
                }
            }

            return distance;
        }

        internal float GetDistance(Vector2 lineStart, Vector2 lineEnd)
        {
            float distance = float.MaxValue;

            foreach (var list in _nodes)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    Vector2 from = list[i];
                    Vector2 to = list[(i + 1) % list.Count];

                    float dist = MyMath.DistanceLineLine(from, to, lineStart, lineEnd);
                    if (dist < distance)
                    {
                        distance = dist;
                    }
                }
            }

            return distance;
        }

        internal float GetDistance(PolygonCollider other)
        {
            float distance = float.MaxValue;

            foreach (var list in _nodes)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    Vector2 from = list[i];
                    Vector2 to = list[(i + 1) % list.Count];

                    float dist = other.GetDistance(from, to);
                    if (dist < distance)
                    {
                        distance = dist;
                    }
                }
            }

            return distance;
        }

        private void SetSpanRadius()
        {
            float[] borders = new float[4];
            borders[0] = float.MaxValue; //Min x
            borders[1] = float.MinValue; //Max x
            borders[2] = float.MaxValue; //Min y
            borders[3] = float.MinValue; //Max y

            foreach (List<Vector2> nodes in _nodes)
            {
                foreach (Vector2 a in nodes)
                {
                    SetBorders(borders, a);
                }
            }

            _position = new Vector2((borders[0] + borders[1]) / 2, (borders[2] + borders[3]) / 2);

            _spanRadius = 0;
            foreach (List<Vector2> nodes in _nodes)
            {
                foreach (Vector2 a in nodes)
                {
                    float r = Vector2.Distance(a, _position);
                    if (r > _spanRadius)
                    {
                        _spanRadius = r;
                    }
                }
            }
        }

        private void SetBorders(float[] borders, Vector2 pos)
        {
            if (borders[0] > pos.X)
            {
                borders[0] = pos.X;
            }
            if (borders[1] < pos.X)
            {
                borders[1] = pos.X;
            }
            if (borders[2] > pos.Y)
            {
                borders[2] = pos.Y;
            }
            if (borders[3] < pos.Y)
            {
                borders[3] = pos.Y;
            }
        }

        private void SetVirtualRadius()
        {
            float surfaceArea = 0;
            foreach (List<Vector2> polygon in _nodes)
            {
                surfaceArea += MyMath.PolygonArea(polygon);
            }
            _virtualRadius = (float)Math.Sqrt(Math.Min(surfaceArea, 1500) / Math.PI);
        }
    }
}