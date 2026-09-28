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
                _generators.Add(new RouteGenerator(_input, this, map));
            }

            Search();

            SetFinalLeafIndex();

            _maps.Clear();
        }

        //Expands leaves in parallel rounds. Results are merged in round order, so the outcome does not depend on thread timing
        private void Search()
        {
            List<(RouteGenerator Generator, RouteLeaf Leaf)> round = new();

            while (FillRound(round))
            {
                RouteExpansion[] expansions = new RouteExpansion[round.Count];
                ParallelRunner.For(round.Count, i => expansions[i] = round[i].Generator.Expand(round[i].Leaf));

                for (int i = 0; i < round.Count; i++)
                {
                    round[i].Generator.Merge(expansions[i]);
                }
            }
        }

        //Takes one leaf from each unfinished generator in turn, so that every map keeps being explored
        private bool FillRound(List<(RouteGenerator Generator, RouteLeaf Leaf)> round)
        {
            round.Clear();

            List<RouteGenerator> active = _generators.Where(g => !g.IsFinished && g.Count > 0).ToList();
            int size = Math.Max(Constants.SEARCH_ROUND_SIZE, active.Count);

            while (round.Count < size && active.Count > 0)
            {
                for (int i = 0; i < active.Count && round.Count < size;)
                {
                    RouteGenerator generator = active[i];
                    if (generator.TryDequeue(out RouteLeaf? leaf))
                    {
                        round.Add((generator, leaf));
                        i++;
                    }
                    else
                    {
                        active.RemoveAt(i);
                    }
                }
            }

            return round.Count > 0;
        }

        internal void SetFinalRoute(RouteLeaf route)
        {
            if (_finalLeaf == null || _finalLeaf.Score > route.Score)
            {
                _finalLeaf = route;
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