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

                AddToSet(set, path, path.From);
                AddToSet(set, path, path.To);
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

        private static void AddToSet(HashSet<StreetPath> set, StreetPath from, StreetNode to)
        {
            foreach (var path in to.Paths)
            {
                if (set.Contains(path)) { continue; }

                set.Add(path);
                AddToSet(set, path, path.GetOther(to));
            }
        }
    }
}