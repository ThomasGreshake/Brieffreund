//Copyright Thomas Greshake 2026

namespace Brieffreund.Eulermap
{
    internal interface IEulerMapValidator
    {
        public bool Validate();
    }
}

namespace Brieffreund.Eulermap.Functions
{
    internal class EulerMapValidator : IEulerMapValidator
    {
        private readonly IEulerMap _map;

        internal EulerMapValidator(IEulerMap map)
        {
            _map = map;
        }

        public bool Validate()
        {
            if (!_map.Success)
            {
                return false;
            }

            if (_map.Nodes.Values.Any(n => n.EulerCount % 2 == 1))
            {
                return false;
            }

            if (_map.Streetmap.Segments.Where(s => s.IsRequiredOnRoute).Any(s => _map.GetPaths(s).Count == 0))
            {
                return false;
            }

            return true;
        }
    }
}