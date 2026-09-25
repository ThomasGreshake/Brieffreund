//Copyright Thomas Greshake 2026

namespace Brieffreund.Streetmap
{
    internal interface IStreetPathTrimmer
    {
        public void TrimPaths();
    }
}

namespace Brieffreund.Streetmap.Functions
{
    internal class StreetPathTrimmer : IStreetPathTrimmer
    {
        private readonly IPathMap _map;

        internal StreetPathTrimmer(IPathMap map)
        {
            _map = map;
        }

        public void TrimPaths()
        {
            List<StreetPath> toDelete = new();

            foreach (StreetNode node in _map.GetAllNodes().Where(n => n.Count == 1))
            {
                StreetPath path = node.Paths.First();
                if (path.Segment == null && path.Length <= (path.RestrictedAccess ? 2 : 1) * Constants.PATH_TRIM_LENGTH_METER && path.GetOther(node).Count > 2 &&
                    !toDelete.Contains(path) && path.Storages.Count == 0 && path.MailAddresses.Count == 0)
                {
                    toDelete.Add(path);
                }
            }

            StreetPath.Delete(toDelete);
        }
    }
}