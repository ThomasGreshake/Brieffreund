//Copyright Thomas Greshake 2026

namespace Brieffreund.Streetmap
{
    internal interface IInternalFlagSetter : IEvent<IInternalFlagSetter>
    {
        public IStreetMap Map { get; }

        public InternalPathFlags[] Flags { get; }

        public void SetFlags();
    }
}

namespace Brieffreund.Streetmap.Functions
{
    internal abstract class InternalFlagSetter : IInternalFlagSetter
    {
        private readonly IStreetMap _map;
        public IStreetMap Map => _map;

        private InternalPathFlags[] _flags;
        public InternalPathFlags[] Flags => _flags;

        protected InternalFlagSetter(IStreetMap map)
        {
            _map = map;
            _flags = Array.Empty<InternalPathFlags>();
        }

        public abstract void SetFlags();

        protected void SetCurrentFlags()
        {
            _flags = new InternalPathFlags[_map.Segments.Count];
            for (int i = 0; i < _map.Segments.Count; i++)
            {
                _flags[i] = _map.Segments[i].Paths[0].InternalPathFlags;
            }
        }

        protected bool HasFlag(StreetSegment segment, InternalPathFlags flag) => _flags[segment.Index].HasFlag(flag);

        protected void AddFlag(StreetSegment segment, InternalPathFlags flag) => _flags[segment.Index] |= flag;
    }
}