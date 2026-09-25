//Copyright Thomas Greshake 2026

namespace Brieffreund.Routegenerator
{
    internal class RouteGeneratorManager
    {
        private readonly IUserInput _input;
        private readonly IList<RouteMap> _maps;

        private readonly List<RouteGenerator> _generators = new List<RouteGenerator>();
        internal int TreeCount => _generators.Sum(d => d.ScoreCount);
        internal int Count => _generators.Sum(d => d.Count);

        private object _lock = new();

        private RouteLeaf? _finalLeaf = null;
        internal RouteLeaf? FinalLeaf => _finalLeaf;

        private int _finalLeafIndex = -1;
        internal int FinalLeafIndex => _finalLeafIndex;

        internal RouteGeneratorManager(IUserInput input, IList<RouteMap> maps)
        {
            _input = input;
            _maps = maps;
        }

        internal void FindBestRoute()
        {
            foreach (RouteMap map in _maps)
            {
                RouteGenerator generator = new RouteGenerator(_input, this, map);
                _generators.Add(generator);
                if (generator.FinalRoute != null)
                {
                    SetFinalRoute(generator.FinalRoute);
                }
            }

            int threadCount = Constants.THREAD_COUNT;
            Thread[] threads = new Thread[threadCount];

            for (int i = 0; i < threadCount; i++)
            {
                threads[i] = new Thread(Process);
            }

            for (int i = 0; i < threadCount; i++)
            {
                threads[i].Start();
            }

            Process();

            for (int i = 0; i < threadCount; i++)
            {
                threads[i].Join();
            }

            SetFinalLeafIndex();

            _maps.Clear();
        }

        private void Process()
        {
            List<RoutePathway> options = new(8);

            while (true)
            {
                RouteGenerator? current = _generators.Where(r => r.FinalRoute == null && r.Count > 0).MinBy(r => r.PickingScore);
                if (current == null)
                {
                    return;
                }

                if (!current.ProcessNext(options))
                {
                    continue;
                }

                RouteLeaf? final = current.FinalRoute;
                if (final == null)
                {
                    throw new Exception();
                }

                SetFinalRoute(final);
            }
        }

        private void SetFinalRoute(RouteLeaf route)
        {
            lock (_lock)
            {
                if (_finalLeaf == null || _finalLeaf.Score > route.Score)
                {
                    _finalLeaf = route;
                }
            }
        }

        private void SetFinalLeafIndex()
        {
            if (_finalLeaf == null)
            {
                _finalLeafIndex = -1;
                return;
            }

            for (int i = 0; i < _generators.Count; i++)
            {
                if (_generators[i].Map == _finalLeaf.Map)
                {
                    _finalLeafIndex = i;
                    return;
                }
            }

            _finalLeafIndex = -1;
            return;
        }
    }
}