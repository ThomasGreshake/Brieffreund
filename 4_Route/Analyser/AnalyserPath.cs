//Copyright Thomas Greshake 2026

namespace Brieffreund.Analyser
{
    internal interface IOnAnalyserPathCreated : IEvent<IOnAnalyserPathCreated>

    { public AnalyserPath GetPath(); }

    internal enum AnalyserPathType
    { LeftOfPath, RightOfPath, CrossesStreet }

    internal class AnalyserPath : IOnAnalyserPathCreated
    {
        internal readonly AnalyserPathway Forward, Backward;
        internal AnalyserNode From => Backward.Towards;
        internal AnalyserNode To => Forward.Towards;
        internal AnalyserMap Map => Forward.Towards.Map;

        internal readonly AnalyserPathType PathType;
        internal readonly TrafficType Traffic;
        internal readonly StreetType StreetType;
        internal readonly DirectionType DirectionType;

        internal readonly float Length;

        public static AnalyserPath Create(AnalyserNode from, AnalyserNode to, StreetPath spath, float length, AnalyserPathType type)
        {
            AnalyserPath path = new(from, to, spath, length, type);
            IOnAnalyserPathCreated.Call(path);
            return path;
        }

        private AnalyserPath(AnalyserNode from, AnalyserNode to, StreetPath path, float length, AnalyserPathType type)
        {
            Forward = new(this, to);
            Backward = new(this, from);
            Traffic = path.Traffic;
            StreetType = path.Type;
            DirectionType = path.DirectionType;
            Length = length;
            PathType = type;
        }

        public AnalyserPath GetPath() => this;

        internal AnalyserNode GetOther(AnalyserNode node)
        {
            if (From == node)
            {
                return To;
            }
            if (To == node)
            {
                return From;
            }
            throw new Exception();
        }
    }
}