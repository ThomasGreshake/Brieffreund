//Copyright Thomas Greshake 2026

using System.Numerics;

namespace Brieffreund
{
    public static class MyMath
    {
        //Counterclockwise
        public static Vector2 Perpendicular(this Vector2 vector) => new Vector2(-vector.Y, vector.X);

        //In radians
        public static float Angle(Vector2 a, Vector2 b)
        {
            float lengths = a.LengthSquared() * b.LengthSquared();
            if (lengths == 0)
            {
                return 0;
            }
            lengths = (float)Math.Sqrt(lengths);
            float dot = Vector2.Dot(a, b);
            float f = Math.Clamp(dot / lengths, -1, 1);

            return (float)Math.Acos(f);
        }

        //Counterclockwise angle from a to b in radians
        public static float FullAngle(Vector2 a, Vector2 b)
        {
            float angle = Angle(a, b);
            return PointsToTheLeft(a, b) ? angle : 2 * (float)Math.PI - angle;
        }

        public static float RadiansToDegrees(float radians) => radians * 180f / (float)Math.PI;

        public static float DegreesToRadians(float degrees) => degrees * (float)Math.PI / 180f;

        public static double RadiansToDegrees(double radians) => radians * 180d / Math.PI;

        public static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;

        //Area of a polygon without holes / no overlapping edges
        public static float PolygonArea(IList<Vector2> nodes) => Math.Abs(SignedPolygonArea(nodes));

        public static bool ClockwisePolygon(IList<Vector2> nodes) => SignedPolygonArea(nodes) <= 0;

        private static float SignedPolygonArea(IList<Vector2> nodes)
        {
            float area = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                int j = (i + 1) % nodes.Count;
                area += (nodes[i].X - nodes[j].X) * (nodes[i].Y + nodes[j].Y);
            }
            return .5f * area;
        }

        public static float PolygonCircumference(IList<Vector2> nodes)
        {
            float c = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                int j = (i + 1) % nodes.Count;
                c += Vector2.Distance(nodes[i], nodes[j]);
            }
            return c;
        }

        //A performant smooth function for inputs between 0 and 1.
        public static float SimpleSmooth(float input)
        {
            if (input < 0)
            {
                return 0;
            }
            if (input > 1)
            {
                return 1;
            }
            float square = input * input;
            return -2 * square * input + 3 * square;
        }

        //A more smooth function.
        public static float Smooth(float input)
        {
            if (input < 0)
            {
                return 0;
            }
            if (input > 1)
            {
                return 1;
            }
            return (float)Math.Sin(input * 0.5f * Math.PI);
        }

        //Manhattan distance
        public static float Manhattan(Vector2 one, Vector2 two) => Math.Abs(one.X - two.X) + Math.Abs(one.Y - two.Y);

        //Min function for params
        public static float Min(params float[] input) => input.Min();

        //Max function for params
        public static float Max(params float[] input) => input.Max();

        //The distance of a point from the line defined by start and end. Line is assumed to be infinite in both directions.
        public static float DistanceLinePoint(Vector2 lineStart, Vector2 lineEnd, Vector2 point, bool isInfinite = false)
        {
            float length = Vector2.Distance(lineStart, lineEnd);
            if (length == 0) { return Vector2.Distance(lineStart, point); }
            float perpDist = Math.Abs((point.X - lineStart.X) * (lineStart.Y - lineEnd.Y) + (point.Y - lineStart.Y) * (lineEnd.X - lineStart.X)) / length;
            if (isInfinite)
            {
                return perpDist;
            }

            float perc = PercentageOnLineUnclamped(lineStart, lineEnd, point);
            if (perc >= 0 && perc <= 1)
            {
                return perpDist;
            }
            return Min(Vector2.Distance(lineStart, point), Vector2.Distance(lineEnd, point));
        }

        //Casts the point on to the line anlong the shortest path and returns the percentage of that point along the way from linestart and lineend
        public static float PercentageOnLineUnclamped(Vector2 lineStart, Vector2 lineEnd, Vector2 point)
        {
            Vector2 dir = lineEnd - lineStart;
            if (dir.LengthSquared() == 0)
            {
                return 0;
            }

            point -= lineStart;

            if (dir.X == 0)
            {
                return point.Y / dir.Y;
            }
            else if (dir.Y == 0)
            {
                return point.X / dir.X;
            }
            else
            {
                return Vector2.Dot(point, dir) / dir.LengthSquared();
            }
        }

        public static float PercentageOnLine(Vector2 lineStart, Vector2 lineEnd, Vector2 point) =>
            Math.Clamp(PercentageOnLineUnclamped(lineStart, lineEnd, point), 0, 1);

        //Is the point to the right or left of a line? Left = True, Right = False
        public static bool IsToTheLeft(Vector2 lineStart, Vector2 lineEnd, Vector2 point) => PointsToTheLeft(lineEnd - lineStart, point - lineStart);

        public static bool PointsToTheLeft(Vector2 comparer, Vector2 pointer) =>
            Vector2.Dot(comparer.Perpendicular(), pointer) > 0;

        public static bool PointsToTheLeft(Vector2 pointer) => PointsToTheLeft(new Vector2(0, 1), pointer);

        //The hitpoint of a line defined by start point and end point with a circle at a given position. Returns Did it hit? Out hitpoint. Just very simple Vectormath
        //Obviously there may be 2 hitPoints, but this will always return the first one.
        public static bool HitpointLineCircle(Vector2 lineStart, Vector2 lineEnd, Vector2 circlePosition, float circleRadius, out Vector2 hit, bool isInfinite = false)
        {
            Vector2 lineDirection = lineEnd - lineStart;
            Vector2 relStart = lineStart - circlePosition;

            float lengthSquared = lineDirection.LengthSquared();
            if (lengthSquared == 0)
            {
                return GetDefault(out hit, out Vector2 _);
            }

            float pHalf = (relStart.X * lineDirection.X + relStart.Y * lineDirection.Y) / lengthSquared;
            float q = (relStart.LengthSquared() - circleRadius * circleRadius) / lengthSquared;

            float D = pHalf * pHalf - q;
            if (D < 0)
            {
                return GetDefault(out hit, out Vector2 _);
            }

            float sqrtD = (float)Math.Sqrt(D);
            float t = -pHalf - sqrtD;
            if (t < 0)
            {
                t = -pHalf + sqrtD;
            }

            hit = lineStart + lineDirection * t;

            return isInfinite || (t < 1 && t >= 0);
        }

        //Gets both hitpoints and hitpoint count
        public static int HitpointsLineCircle(Vector2 lineStart, Vector2 lineEnd, Vector2 circlePosition, float circleRadius, out Vector2 hit1, out Vector2 hit2, bool isInfinite = false)
        {
            Vector2 lineDirection = lineEnd - lineStart;
            Vector2 relStart = lineStart - circlePosition;

            float lengthSquared = lineDirection.LengthSquared();
            if (lengthSquared == 0)
            {
                GetDefault(out hit1, out hit2);
                return 0;
            }

            float pHalf = (relStart.X * lineDirection.X + relStart.Y * lineDirection.Y) / lengthSquared;
            float q = (relStart.LengthSquared() - circleRadius * circleRadius) / lengthSquared;

            float D = pHalf * pHalf - q;
            if (D < 0)
            {
                GetDefault(out hit1, out hit2);
                return 0;
            }

            float sqrtD = (float)Math.Sqrt(D);
            float t1 = -pHalf - sqrtD;
            float t2 = -pHalf + sqrtD;

            hit1 = lineStart + lineDirection * t1;
            hit2 = lineStart + lineDirection * t2;

            int count = 0;
            if (t1 >= 0 && (isInfinite || t1 < 1)) { count += 1; }
            if (t2 >= 0 && (isInfinite || t2 < 1)) { count += 1; }

            if (count == 1)
            {
                if (t1 < 0 || t1 > t2)
                {
                    hit1 = hit2;
                }
                else
                {
                    hit2 = hit1;
                }
            }

            return count;
        }

        //Same as above, but now the circle is an ellipse
        //Warning: The ellipse is aligned with the coordinate system!
        //Warning: Entering an ellipse radius of 0 always results in false and does not treat the ellipse like a line or like a point!
        public static bool HitpointLineEllipse(Vector2 lineStart, Vector2 lineEnd, Vector2 ellipseMiddle, float xRadius, float yRadius, out Vector2 hit, bool isInfinite = false)
        {
            if (xRadius == 0 || yRadius == 0)
            {
                return GetDefault(out hit, out Vector2 _);
            }

            xRadius = Math.Abs(xRadius);
            yRadius = Math.Abs(yRadius);

            Vector2 lineDirection = lineEnd - lineStart;
            Vector2 relStart = lineStart - ellipseMiddle;
            float xRS = xRadius * xRadius;
            float yRS = yRadius * yRadius;

            float d = lineDirection.X * lineDirection.X / xRS + lineDirection.Y * lineDirection.Y / yRS;
            float pHalf = (relStart.X * lineDirection.X / xRS + relStart.Y * lineDirection.Y / yRS) / d;
            float q = (relStart.X * relStart.X / xRS + relStart.Y * relStart.Y / yRS) / d;

            float D = pHalf * pHalf - q;
            if (D < 0)
            {
                return GetDefault(out hit, out Vector2 _);
            }

            float sqrtD = (float)Math.Sqrt(D);
            float t = -pHalf - sqrtD;
            if (t < 0)
            {
                t = -pHalf + sqrtD;
            }

            hit = lineStart + lineDirection * t;

            return isInfinite || (t >= 0 && t < 1);
        }

        //Do 2 circles touch?
        public static bool HitCircleCircle(Vector2 pos1, float radius1, Vector2 pos2, float radius2)
        {
            float x = pos2.X - pos1.X;
            float y = pos2.Y - pos1.Y;
            float r = Math.Abs(radius1) + Math.Abs(radius2);

            return x * x + y * y < r * r;
        }

        //Hitpoints between two circles
        //Warning: Two overlapping circles returns false. Returning just one hitpoint when there would be infinite would not be correct either, so I will leave it like this
        public static bool HitpointsCircleCircle(Vector2 pos1, float radius1, Vector2 pos2, float radius2, out Vector2 hit1, out Vector2 hit2)
        {
            float R = Vector2.Distance(pos1, pos2);
            if (R == 0)
            {
                return GetDefault(out hit1, out hit2);
            }

            float RR = R * R;
            float rSquaredDiff = radius1 * radius1 - radius2 * radius2;
            float D = 2 * (radius1 * radius1 + radius2 * radius2) - rSquaredDiff * rSquaredDiff / RR - RR;

            if (D < 0)
            {
                return GetDefault(out hit1, out hit2);
            }

            Vector2 hitpointCenter = 0.5f * (pos1 + pos2 + rSquaredDiff * (pos2 - pos1) / RR);
            Vector2 centerToHitPoint = 0.5f * (float)Math.Sqrt(D) * new Vector2(pos2.Y - pos1.Y, pos1.X - pos2.X) / R;

            hit1 = hitpointCenter - centerToHitPoint;
            hit2 = hitpointCenter + centerToHitPoint;
            return true;
        }

        //Do two lines hit, if yes where?
        //Warning: Two lines lying on top of each other results in false. Returning just one hitpoint when there would be infinite would not be correct either, so I will leave it like this
        public static bool HitpointLineLine(Vector2 start1, Vector2 end1, Vector2 start2, Vector2 end2, out Vector2 hit, bool infinite1 = false, bool infinite2 = false)
        {
            Vector2 dir1 = end1 - start1;
            Vector2 dir2 = end2 - start2;

            if (dir2.LengthSquared() == 0)
            {
                return GetDefault(out hit, out Vector2 _);
            }

            float t1, t2;
            if (dir2.X == 0)
            {
                if (dir1.X == 0)
                {
                    return GetDefault(out hit, out Vector2 _);
                }

                t1 = (start2.X - start1.X) / dir1.X;
                t2 = (start1.Y - start2.Y + t1 * dir1.Y) / dir2.Y;
            }
            else if (dir2.Y == 0)
            {
                if (dir1.Y == 0)
                {
                    return GetDefault(out hit, out Vector2 _);
                }

                t1 = (start2.Y - start1.Y) / dir1.Y;
                t2 = (start1.X - start2.X + t1 * dir1.X) / dir2.X;
            }
            else
            {
                float d = dir1.X / dir2.X - dir1.Y / dir2.Y;

                if (Math.Abs(d) < 0.00001f)
                {
                    return GetDefault(out hit, out Vector2 _);
                }

                float n = (start1.Y - start2.Y) / dir2.Y + (start2.X - start1.X) / dir2.X;

                t1 = n / d;
                t2 = (start1.X - start2.X + t1 * dir1.X) / dir2.X;
            }

            hit = start1 + t1 * dir1;
            return (infinite1 || (t1 >= 0 && t1 < 1)) && (infinite2 || (t2 >= 0 && t2 < 1));
        }

        //Distance between two lines
        public static float DistanceLineLine(Vector2 start1, Vector2 end1, Vector2 start2, Vector2 end2)
        {
            if (HitpointLineLine(start1, end1, start2, end2, out Vector2 _))
            {
                return 0;
            }
            return new float[4]
            {
                DistanceLinePoint(start1, end1, start2),
                DistanceLinePoint(start1, end1, end2),
                DistanceLinePoint(start2, end2, start1),
                DistanceLinePoint(start2, end2, end1)
            }.Min();
        }

        //Helper
        private static bool GetDefault(out Vector2 out1, out Vector2 out2)
        {
            out1 = default;
            out2 = default;
            return false;
        }
    }
}