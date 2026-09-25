//Copyright Thomas Greshake 2026

using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Brieffreund.Routegenerator
{
    internal class RouteBranch
    {
        //Data -----------------------------------------------------------------------

        internal readonly RouteBranch Previous;
        internal bool IsStartElement => Previous == this;

        internal readonly RoutePathway Pathway;

        //Setup -----------------------------------------------------------------------
        internal RouteBranch(RouteBranch previous, RoutePathway path)
        {
            Previous = previous;
            Pathway = path;
        }

        internal RouteBranch(RoutePathway start)
        {
            Previous = this;
            Pathway = start;
        }

        //Internals -----------------------------------------------------------------------

        internal bool Contains(RoutePath path)
        {
            RouteBranch current = this;
            while (current.Pathway.RoutePath != path)
            {
                if (current.IsStartElement)
                {
                    return false;
                }
                current = current.Previous;
            }
            return true;
        }

        internal bool TryGetPathway(RoutePath path, [MaybeNullWhen(false)] out RoutePathway way)
        {
            RouteBranch current = this;
            while (true)
            {
                if (current.Pathway.RoutePath == path)
                {
                    way = current.Pathway;
                    return true;
                }
                if (current.IsStartElement)
                {
                    way = null;
                    return false;
                }

                current = current.Previous;
            }
        }

        internal IEnumerable<RoutePathway> GetPathways()
        {
            RouteBranch current = this;

            while (true)
            {
                yield return current.Pathway;
                if (current.IsStartElement)
                {
                    yield break;
                }
                current = current.Previous;
            }
        }

        internal IEnumerable<EulerPathway> GetEulerways()
        {
            RouteBranch current = this;

            while (true)
            {
                EulerPathway? way = current.Pathway.EulerPathway;
                if (way != null)
                {
                    yield return way;
                }
                if (current.IsStartElement)
                {
                    yield break;
                }
                current = current.Previous;
            }
        }

        internal RoutePathway[] ToPathwayArray()
        {
            RouteBranch current = this;
            int index = current.Count();
            RoutePathway[] ways = new RoutePathway[index];

            while (true)
            {
                index -= 1;
                ways[index] = current.Pathway;
                if (index == 0)
                {
                    break;
                }
                current = current.Previous;
            }

            return ways;
        }

        internal int Count()
        {
            RouteBranch current = this;
            int count = 1;
            while (!current.IsStartElement)
            {
                count += 1;
                current = current.Previous;
            }
            return count;
        }

        internal Stack<RoutePathway> GetRoute()
        {
            Stack<RoutePathway> route = new();

            RouteBranch current = this;
            while (true)
            {
                route.Push(current.Pathway);
                if (current.IsStartElement)
                {
                    return route;
                }
                current = current.Previous;
            }
        }

        internal BigInteger GetSignature()
        {
            BigInteger signature = BigInteger.Zero;

            RouteBranch current = this;
            while (true)
            {
                signature |= current.Pathway.RoutePath.Signature;
                if (current.IsStartElement)
                {
                    break;
                }
                current = current.Previous;
            }
            return signature;
        }
    }
}