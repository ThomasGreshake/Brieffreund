//Copyright Thomas Greshake 2026

using System.Numerics;

namespace Brieffreund.Analyser
{
    internal interface IOnAnalyserNodeCreated : IEvent<IOnAnalyserNodeCreated>

    { public AnalyserNode GetNode(); }

    internal class AnalyserNode : IPathnode<AnalyserNode, AnalyserPathway>,
        IOnAnalyserNodeCreated
    {
        internal readonly AnalyserMap Map;

        private readonly Vector2 _position;
        public Vector2 Position => _position;

        private readonly List<AnalyserPathway> _ways = new List<AnalyserPathway>();

        internal static AnalyserNode Create(AnalyserMap map, Vector2 position)
        {
            AnalyserNode node = new(map, position);
            IOnAnalyserNodeCreated.Call(node);
            return node;
        }

        private AnalyserNode(AnalyserMap map, Vector2 position)
        {
            Map = map;
            _position = position;
        }

        static AnalyserNode()
        {
            IOnAnalyserPathCreated.Register(OnAnalysisPathCreated);
        }

        public AnalyserNode GetNode() => this;

        public IList<AnalyserPathway> GetPaths()
        {
            return _ways;
        }

        private static void OnAnalysisPathCreated(IOnAnalyserPathCreated e)
        {
            AnalyserPath path = e.GetPath();
            path.From._ways.Add(path.Forward);
            path.To._ways.Add(path.Backward);
        }
    }
}