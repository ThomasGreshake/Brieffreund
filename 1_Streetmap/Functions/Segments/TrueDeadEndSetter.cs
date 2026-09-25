//Copyright Thomas Greshake 2026

using System.Numerics;

namespace Brieffreund.Streetmap.Functions
{
    internal class TrueDeadEndSetter : InternalFlagSetter
    {
        public TrueDeadEndSetter(StreetMap map) : base(map)
        {
        }

        public override void SetFlags()
        {
            SetCurrentFlags();

            Rectangle rec = new(Map.Intersections.Values.Min(i => i.Position.X), Map.Intersections.Values.Max(i => i.Position.X),
                Map.Intersections.Values.Min(i => i.Position.Y), Map.Intersections.Values.Max(i => i.Position.Y));
            rec.ModifyBorders(-25);

            FindTrueDeadEnds(rec, s => s.Type != StreetType.Tiny && !HasFlag(s, InternalPathFlags.TrueDeadEnd));
            FindTrueDeadEnds(rec, s => !HasFlag(s, InternalPathFlags.TrueDeadEnd));

            IInternalFlagSetter.Call(this);
        }

        private void FindTrueDeadEnds(Rectangle rec, Func<StreetSegment, bool> condition)
        {
            List<Intersection> intersections = Map.Intersections.Values.Where(i =>
                rec.OverlaysPosition(i.Position) && i.Segments.Count(condition) == 1).ToList();

            while (intersections.Count > 0)
            {
                List<Intersection> nextList = new List<Intersection>(intersections.Count / 2 + 4);

                foreach (Intersection inter in intersections)
                {
                    StreetSegment? segment = inter.Segments.FirstOrDefault(condition);
                    if (segment == null)
                    {
                        continue;
                    }
                    Intersection other = segment.GetOther(inter);
                    if (!rec.OverlaysPosition(other.Position))
                    {
                        continue;
                    }

                    AddFlag(segment, InternalPathFlags.TrueDeadEnd);

                    int count = other.Segments.Count(condition);
                    if (count == 1)
                    {
                        nextList.Add(other);
                    }
                    else if (count == 0)
                    {
                        nextList.Remove(other);
                    }
                }

                intersections = nextList;
            }
        }

        private class Rectangle
        {
            private float _minX, _maxX, _minY, _maxY;

            internal Rectangle(float minX, float maxX, float minY, float maxY)
            {
                _minX = minX;
                _maxX = maxX;
                _minY = minY;
                _maxY = maxY;
            }

            internal void ModifyBorders(float change)
            {
                _minX -= change;
                _maxX += change;
                _minY -= change;
                _maxY += change;
            }

            internal bool OverlaysPosition(Vector2 pos) => _minX <= pos.X && _maxX >= pos.X && _minY <= pos.Y && _maxY >= pos.Y;
        }
    }
}