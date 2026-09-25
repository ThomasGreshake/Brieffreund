//Copyright Thomas Greshake 2026

using Brieffreund.Routegenerator;
using Brieffreund.Routegenerator.Functions;
using System.Diagnostics;
using System.Numerics;

namespace Brieffreund
{
    //The routeleafs sit at the ends of the route branch "tree"
    internal class RouteLeaf
    {
        //Data --------------------------------------------------------------------

        private RouteBranch _last;
        internal RouteBranch Last => _last;
        internal RouteNode Routenode => _last.Pathway.Towards;
        internal RouteMap Map => Routenode.Map;

        private int _mailAmountSinceLastStorage;
        internal int MailAmountSinceLastStorage => _mailAmountSinceLastStorage;

        private float _totalLength, _bonusLength;
        internal float TotalLength => _totalLength;
        internal float Score => _totalLength + _bonusLength + Constants.MAIL_TO_SCORE * _mailAmountSinceLastStorage;

        private BigInteger _signature;
        internal BigInteger Signature => _signature;

        //Setup --------------------------------------------------------------------

        internal RouteLeaf(RoutePathway start)
        {
            RouteMap map = start.Towards.Map;
            _last = new(start);
            _signature = start.RoutePath.Signature;

            _totalLength = map.TotalLength;
            _bonusLength = GetInitialBonusLength(map);

            EulerPath? ePath = start.Eulerpath;
            _mailAmountSinceLastStorage = ePath == null ? 0 : ePath.MailAmount;
        }

        internal RouteLeaf(RouteLeaf route)
        {
            _last = route._last;
            _totalLength = route._totalLength;
            _mailAmountSinceLastStorage = route._mailAmountSinceLastStorage;
            _bonusLength = route._bonusLength;
            _signature = route._signature;
        }

        private float GetInitialBonusLength(RouteMap map)
        {
            float length = 0;
            foreach (RoutePath path in map.Paths)
            {
                EulerPathway? way = path.EulerPathway;
                if (way == null)
                {
                    continue;
                }

                float sideScore = -way.GetSideScore();
                length += Constants.SIDEFACTOR_TO_BONUSLENGTH * sideScore;
            }

            return length;
        }

        //Internals --------------------------------------------------------------------

        internal static float GetStorageMailPenalty(float mailAmount, float threashold)
        {
            float perc = Math.Max(mailAmount - threashold, 0) / threashold;
            return Constants.OVERFLOW_PERCENTAGE_TO_DISTANCE_PENALTY * perc * perc;
        }

        internal void AddNext(RoutePathway next, IUserInput input, float newTotalLength)
        {
            ModifyScore(next, input, newTotalLength);
            _last = new RouteBranch(_last, next);
            _signature |= next.RoutePath.Signature;
        }

        internal bool Contains(RoutePath path) => (_signature & path.Signature) == path.Signature;

        internal RoutePathway[] ToPathwayArray() => _last.ToPathwayArray();

        //Privates --------------------------------------------------------------------

        private void ModifyScore(RoutePathway nextWay, IUserInput input, float newTotalLength)
        {
            _totalLength = newTotalLength;

            EulerPathway? next = nextWay.EulerPathway;
            if (next == null || input.DesiredStorageCount == 0 || next.Eulerpath.MailAmount == 0)
            {
                return;
            }

            int totalMail = Map.EulerMap.Streetmap.TotalMailAmount;
            float averageMail = (float)totalMail / (input.DesiredStorageCount + 1);

            _mailAmountSinceLastStorage += next.Eulerpath.MailAmount;
            if (_mailAmountSinceLastStorage <= averageMail * Constants.ACCEPTABLE_MAILAMOUNT_MULTIPLIER && nextWay.Towards != Map.StartAndEnd[1])
            {
                return;
            }

            IStorageDistributor storageDistributor = new StorageDistributor(input, Map);
            storageDistributor.DistributeStorages(ToPathwayArray());

            _mailAmountSinceLastStorage = storageDistributor.FinalMailAmount;
            _bonusLength = storageDistributor.TotalStorageDistance;
        }
    }
}