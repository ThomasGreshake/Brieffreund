//Copyright Thomas Greshake 2026

namespace Brieffreund.Streetmap
{
    internal interface IUniqueSetEnsurer
    {
        public bool EnsureUniqueConnectedSet();
    }
}

namespace Brieffreund.Streetmap.Functions
{
    internal class UniqueSetEnsurer : IUniqueSetEnsurer
    {
        private readonly IPathMap _map;

        internal UniqueSetEnsurer(IPathMap map)
        { _map = map; }

        public bool EnsureUniqueConnectedSet()
        {
            List<HashSet<StreetPath>> sets = new();

            foreach (var path in _map.GetAllPaths())
            {
                if (sets.Any(s => s.Contains(path)))
                {
                    continue;
                }

                HashSet<StreetPath> set = new HashSet<StreetPath>() { path };
                sets.Add(set);

                AddToSet(set, path.From, path.To);
            }

            if (sets.Count == 1) { return true; }

            HashSet<StreetPath>? longest = sets.MaxBy(s => s.Sum(t => t.Length));
            if (longest == null)
            {
                return false;
            }

            sets.Remove(longest);

            foreach (var set in sets)
            {
                StreetPath.Delete(set);
            }

            return true;
        }

        //Iterative, a recursive search can overflow the stack on large maps
        private static void AddToSet(HashSet<StreetPath> set, params StreetNode[] starts)
        {
            List<StreetNode> front = new(starts);

            while (front.Count > 0)
            {
                int index = front.Count - 1;
                StreetNode node = front[index];
                front.RemoveAt(index);

                foreach (StreetPath path in node.Paths)
                {
                    if (set.Add(path))
                    {
                        front.Add(path.GetOther(node));
                    }
                }
            }
        }
    }
}