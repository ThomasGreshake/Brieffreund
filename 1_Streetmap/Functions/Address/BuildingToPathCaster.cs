//Copyright Thomas Greshake 2026

using Priority_Queue;
using System.Numerics;

namespace Brieffreund.Streetmap
{
    internal interface IBuildingToPathCaster
    {
        public void CastBuildings();

        public IDictionary<Building, StreetPath> Casts { get; }
    }

    internal interface IStreetIdentifier : IEvent<IStreetIdentifier>
    {
        public IDictionary<StreetPath, Street> IdentifiedStreets { get; }
    }
}

namespace Brieffreund.Streetmap.Functions
{
    internal class BuildingToPathCaster : IBuildingToPathCaster, IStreetIdentifier
    {
        private readonly IStreetMap _map;
        private readonly IList<Building> _buildings;
        private readonly IList<PolygonCollider> _colliders;

        private readonly IDictionary<Building, StreetPath> _casts;
        public IDictionary<Building, StreetPath> Casts => _casts;

        private readonly IDictionary<StreetPath, Street> _identified;
        public IDictionary<StreetPath, Street> IdentifiedStreets => _identified;

        internal BuildingToPathCaster(IStreetMap map, IList<Building> buildings) : this(map, buildings, new List<PolygonCollider>())
        {
        }

        internal BuildingToPathCaster(IStreetMap map, IList<Building> buildings, IList<PolygonCollider> additionalColliders)
        {
            _map = map;
            _buildings = buildings;
            _casts = new Dictionary<Building, StreetPath>(buildings.Count);
            _identified = new Dictionary<StreetPath, Street>(map.Paths.Count / 8);
            _colliders = additionalColliders;
        }

        public void CastBuildings()
        {
            if (_casts.Count > 0) //Already done
            {
                return;
            }

            Dictionary<Building, List<Building>> nearbyBuildings = PrecalcNearbyBuildings();
            Dictionary<Building, Dictionary<StreetPath, float>> scores = PrecalcBuildingToPathDistances(nearbyBuildings);
            IdentifyStreetsByBuildings(scores);
            IdentifyStreetsByConnections();
            ConvertDistancesToScores(scores);
            GroupNeighbours(nearbyBuildings, scores, out Dictionary<Building, List<Building>> directN, out Dictionary<Building, List<Building>> oppositeN);
            AdjustScoresWithNeighboursAndStreets(scores, directN, oppositeN);
            CastToBestPath(scores);
            IdentifyStreetsByCasts();
            AdjustScoresWithIdentifiedStreets(scores);
            CastToBestPath(scores);
            AlignGroups(scores);
            CastToBestPath(scores);

            int outlierDepth = 2;
            for (int i = 0; i < outlierDepth; i++)
            {
                AlignOutliers(scores, directN);
                CastToBestPath(scores);
            }

            AlignWithSegmentNeighbours(scores);
            CastToBestPath(scores);
            FixFarOutliersWithPathfinding(scores, nearbyBuildings);
            CastToBestPath(scores);
        }

        private Dictionary<Building, List<Building>> PrecalcNearbyBuildings()
        {
            Dictionary<Building, List<Building>> neighbours = new(_buildings.Count);
            foreach (Building b in _buildings)
            {
                neighbours.Add(b, new());
            }

            for (int i = 0; i < _buildings.Count; i++)
            {
                Building a = _buildings[i];

                for (int j = i + 1; j < _buildings.Count; j++)
                {
                    Building b = _buildings[j];
                    if (Vector2.Distance(a.Position, b.Position) < Constants.MAX_PATH_DISTANCE_METER)
                    {
                        neighbours[a].Add(b);
                        neighbours[b].Add(a);
                    }
                }
            }

            return neighbours;
        }

        private Dictionary<Building, Dictionary<StreetPath, float>> PrecalcBuildingToPathDistances(Dictionary<Building, List<Building>> neighbours)
        {
            Dictionary<Building, Dictionary<StreetPath, float>> distances = new(_buildings.Count);

            foreach (Building building in _buildings)
            {
                distances.Add(building, PrecalcBuildingPathDistances(building, neighbours[building]));
            }

            return distances;
        }

        private Dictionary<StreetPath, float> PrecalcBuildingPathDistances(Building building, List<Building> neighbours)
        {
            Dictionary<StreetPath, float> distances = new();

            foreach (StreetSegment seg in _map.Segments)
            {
                List<Tuple<StreetPath, float>> potentialPaths = new();
                StreetPath closestPath = seg.Paths[0];
                float shortest = float.MaxValue;

                foreach (var path in seg.Paths)
                {
                    float distance = MyMath.DistanceLinePoint(path.From.Position, path.To.Position, building.Position);
                    if (shortest > distance)
                    {
                        shortest = distance;
                        closestPath = path;
                    }

                    float perc = path.PercentageOnPath(building.Position);
                    if (perc == 0 || perc == 1)
                    {
                        continue;
                    }

                    Tuple<StreetPath, float> potentialPath = Tuple.Create(path, distance);
                    potentialPaths.Add(potentialPath);
                }

                if (!potentialPaths.Any(t => t.Item1 == closestPath) && (potentialPaths.Count == 0 || potentialPaths.All(t => t.Item2 > shortest * 1.35f)))
                {
                    Tuple<StreetPath, float> potentialPath = Tuple.Create(closestPath, shortest);
                    potentialPaths.Add(potentialPath);
                }

                foreach (var potentialPath in potentialPaths)
                {
                    if (potentialPath.Item2 > Constants.MAX_PATH_DISTANCE_METER)
                    {
                        continue;
                    }

                    distances.Add(potentialPath.Item1, potentialPath.Item2);
                }
            }

            RemoveSegmentCollisions(building, distances);
            AddBuildingCollisionLength(building, distances, neighbours);

            return distances;
        }

        private void AddBuildingCollisionLength(Building building, Dictionary<StreetPath, float> distances, List<Building> neighbours)
        {
            foreach (var dist in distances)
            {
                float current = distances[dist.Key];
                current += Constants.BUILDING_COLLISION_FACTOR * GetBuildingCollisionLength(building, dist.Key, neighbours);
                distances[dist.Key] = current;
            }
        }

        private float GetBuildingCollisionLength(Building building, StreetPath path, List<Building> otherBuildings)
        {
            Vector2 cast = path.CastPosition(building.Position);
            float dist = 0;
            int count = 0;
            foreach (Building other in otherBuildings.Where(b => b != building && Vector2.Distance(b.Position, building.Position) < Constants.NEIGHBOUR_COLLISION_METER))
            {
                List<Vector2> hits = other.Collider.GetCollisions(cast, building.Position);
                if (hits.Count == 0 || hits.Count % 2 == 1)
                {
                    continue;
                }

                float mult;
                if (building.Street == other.Street)
                {
                    if (Math.Abs(building.Number - other.Number) < 3)
                    {
                        mult = 0.5f;
                    }
                    else
                    {
                        mult = 1.5f;
                    }
                }
                else
                {
                    mult = 10f;
                }

                for (int i = 0; i < hits.Count; i += 2)
                {
                    dist += Vector2.Distance(hits[i], hits[i + 1]) * mult;
                }

                count++;
            }
            foreach (PolygonCollider collider in _colliders.Where(c => Vector2.Distance(c.Position, building.Position) < Constants.NEIGHBOUR_COLLISION_METER))
            {
                List<Vector2> hits = collider.GetCollisions(cast, building.Position);
                if (hits.Count == 0 || hits.Count % 2 == 1)
                {
                    continue;
                }

                for (int i = 0; i < hits.Count; i += 2)
                {
                    dist += Vector2.Distance(hits[i], hits[i + 1]);
                }

                count++;
            }
            return dist * count;
        }

        private static void RemoveSegmentCollisions(Building building, Dictionary<StreetPath, float> distances)
        {
            if (distances.Count <= 1)
            {
                return;
            }

            List<StreetPath> list = distances.Select(s => s.Key).ToList();
            List<StreetPath> toRemove = new();

            for (int i = 0; i < list.Count; i++)
            {
                StreetPath current = list[i];
                StreetSegment? segment = current.Segment;
                if (segment == null)
                {
                    throw new Exception();
                }

                for (int j = 0; j < list.Count; j++)
                {
                    if (i == j) { continue; }

                    StreetPath other = list[j];

                    if (!toRemove.Contains(other) && LineSegmentCollision(segment, building.Position, other.CastPosition(building.Position)))
                    {
                        toRemove.Add(other);
                    }
                }
            }

            foreach (StreetPath other in toRemove)
            {
                distances.Remove(other);
            }
        }

        private static bool LineSegmentCollision(StreetSegment segment, Vector2 start, Vector2 end)
        {
            foreach (StreetPath path in segment.Paths)
            {
                if (!MyMath.HitpointLineLine(path.From.Position, path.To.Position, start, end, out Vector2 hit))
                {
                    continue;
                }

                if (Vector2.Distance(hit, end) < 0.5f)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private void IdentifyStreetsByBuildings(Dictionary<Building, Dictionary<StreetPath, float>> distances)
        {
            Dictionary<StreetSegment, List<Tuple<StreetPath, Building>>> segmentData = new();

            foreach (var bPaths in distances)
            {
                if (bPaths.Value.Count == 0)
                {
                    continue;
                }

                Building building = bPaths.Key;

                KeyValuePair<StreetPath, float> minimum = bPaths.Value.MinBy(p => p.Value);
                if (minimum.Key.Street != null || bPaths.Value.Where(p => !p.Equals(minimum)).Any(p => p.Value < minimum.Value * 4))
                {
                    continue;
                }

                StreetSegment? segment = minimum.Key.Segment;
                if (segment == null)
                {
                    throw new Exception();
                }

                if (!segmentData.TryGetValue(segment, out var list))
                {
                    list = new();
                    segmentData.Add(segment, list);
                }

                Tuple<StreetPath, Building> data = Tuple.Create(minimum.Key, building);
                list.Add(data);
            }

            foreach (var data in segmentData)
            {
                var list = data.Value;

                if (list.Count <= 1)
                {
                    continue;
                }

                list.Sort((a, b) =>
                    Comparer<float>.Default.Compare(GetPercentageOnSegment(data.Key, a.Item1, a.Item2), GetPercentageOnSegment(data.Key, b.Item1, b.Item2)));

                for (int i = 0; i < list.Count; i++)
                {
                    var current = list[i];
                    if ((i != 0 && list[i - 1].Item2.Street == current.Item2.Street) ||
                        i != list.Count - 1 && list[i + 1].Item2.Street == current.Item2.Street)
                    {
                        _identified.TryAdd(current.Item1, current.Item2.Street);
                    }
                }
            }

            IStreetIdentifier.Call(this);
        }

        private float GetPercentageOnSegment(StreetSegment seg, StreetPath path, Building building)
        {
            float dist = 0;
            for (int i = 0; i < seg.Paths.Count; i++)
            {
                StreetPath current = seg.Paths[i];
                if (current == path)
                {
                    break;
                }
                dist += current.Length;
            }
            dist += path.PercentageOnPath(building.Position) * path.Length;
            return dist / seg.Length;
        }

        private void IdentifyStreetsByConnections()
        {
            List<StreetPath> toCheck = _map.Paths.Where(p => p.Street == null && !_identified.ContainsKey(p)).ToList();

            while (toCheck.Count > 0)
            {
                StreetPath current = toCheck[toCheck.Count - 1];

                List<Street> foundStreets = new();
                List<StreetPath> set = new() { current };

                CreateConnectedSet(current, current.To, set, foundStreets);
                CreateConnectedSet(current, current.From, set, foundStreets);

                foreach (StreetPath path in set)
                {
                    if (!toCheck.Remove(path))
                    {
                        throw new Exception(path.IsActive.ToString());
                    }
                }

                if (foundStreets.Count == 1)
                {
                    Street street = foundStreets[0];
                    foreach (StreetPath path in set)
                    {
                        _identified.Add(path, street);
                    }
                }
            }

            IStreetIdentifier.Call(this);
        }

        private void CreateConnectedSet(StreetPath from, StreetNode to, List<StreetPath> set, List<Street> foundStreets)
        {
            bool foundStreet = false;
            foreach (StreetPath path in to.Paths)
            {
                Street? street = path.Street;
                if (street == null && !_identified.TryGetValue(path, out street))
                {
                    continue;
                }

                foundStreet = true;
                if (!foundStreets.Contains(street))
                {
                    foundStreets.Add(street);
                }
            }

            if (foundStreet)
            {
                return;
            }

            foreach (StreetPath path in to.Paths)
            {
                if (set.Contains(path))
                {
                    continue;
                }

                set.Add(path);
                StreetNode other = path.GetOther(to);
                CreateConnectedSet(path, other, set, foundStreets);
            }
        }

        private void ConvertDistancesToScores(Dictionary<Building, Dictionary<StreetPath, float>> scores)
        {
            foreach (var sc in scores)
            {
                foreach (var dist in sc.Value)
                {
                    float current = sc.Value[dist.Key];
                    current = (float)Math.Sqrt(Math.Max(current, Constants.MIN_PATH_DISTANCE_METER));
                    sc.Value[dist.Key] = current;
                }
            }
        }

        private void GroupNeighbours(Dictionary<Building, List<Building>> neighbours, Dictionary<Building, Dictionary<StreetPath, float>> buildingScores,
            out Dictionary<Building, List<Building>> direct, out Dictionary<Building, List<Building>> opposite)
        {
            direct = new();
            opposite = new();

            foreach (Building b in neighbours.Keys)
            {
                direct.Add(b, new());
                opposite.Add(b, new());
            }

            foreach (var kvp in neighbours)
            {
                Building building = kvp.Key;
                List<StreetSegment> segments = new();
                foreach (StreetPath p in buildingScores[building].Keys)
                {
                    StreetSegment? seg = p.Segment;
                    if (seg != null)
                    {
                        segments.Add(seg);
                    }
                }

                List<Building> directN = direct[building];
                List<Building> oppositeN = opposite[building];

                foreach (Building n in kvp.Value)
                {
                    if (n.Street != building.Street)
                    {
                        continue;
                    }

                    int pathHitCount = segments.Sum(s =>
                    s.Paths.Count(p => MyMath.HitpointLineLine(building.Position, n.Position, p.From.Position, p.To.Position, out Vector2 _)));

                    if (pathHitCount == 0 && Math.Abs(building.Number - n.Number) <= 3 && building.IsEven == n.IsEven)
                    {
                        directN.Add(n);
                    }
                    else if (pathHitCount == 1)
                    {
                        oppositeN.Add(n);
                    }
                }
            }
        }

        private void AdjustScoresWithNeighboursAndStreets(Dictionary<Building, Dictionary<StreetPath, float>> buildingScores,
            Dictionary<Building, List<Building>> directN, Dictionary<Building, List<Building>> oppositeN)
        {
            foreach (var kvp in buildingScores)
            {
                Building building = kvp.Key;

                Dictionary<StreetPath, float> scores = kvp.Value;
                bool isEven = building.IsEven;

                Building? closest = GetClosestNeighbour(building, directN[building], out Building? higher, out Building? lower);
                List<Building> directNeighbours = new List<Building>(2);
                if (higher != null)
                {
                    directNeighbours.Add(higher);
                }
                if (lower != null)
                {
                    directNeighbours.Add(lower);
                }

                foreach (StreetPath p in scores.Keys)
                {
                    float currScore = scores[p];

                    if (p.Type == StreetType.Tiny)
                    {
                        currScore *= 1.1f;
                    }

                    if (p.RestrictedAccess)
                    {
                        currScore *= 1.4f;
                    }

                    float percOnPath = p.PercentageOnPath(building.Position);
                    if (percOnPath == 0 || percOnPath == 1)
                    {
                        StreetNode node = percOnPath == 0 ? p.From : p.To;
                        float distance = Vector2.Distance(building.Position, node.Position);
                        if (node.Paths.Any(p => MyMath.DistanceLinePoint(p.From.Position, p.To.Position, building.Position) + 0.5f < distance))
                        {
                            currScore *= 20f;
                        }
                    }

                    currScore *= GetBuildingStreetScoreMult(building, p);

                    Vector2 pathDiff = p.Lerp(percOnPath) - building.Position;

                    if (directNeighbours.Count > 0)
                    {
                        float bestFactor = float.MaxValue;

                        foreach (Building n in directNeighbours)
                        {
                            Vector2 pointer = n.Position - building.Position;
                            float angle = Math.Abs(MyMath.Angle(pathDiff, pointer) - 0.5f * (float)Math.PI);
                            float adjustedAngle = Math.Max(angle - 0.08f * (float)Math.PI, angle * 0.25f);
                            float factor = 1f + 1f * adjustedAngle / (0.5f * (float)Math.PI);
                            if (n != closest)
                            {
                                factor *= 1.25f;
                            }
                            if (factor < bestFactor)
                            {
                                bestFactor = factor;
                            }
                        }

                        if (higher != null && lower != null)
                        {
                            Vector2 pointer = higher.Position - lower.Position;
                            float angle = Math.Abs(MyMath.Angle(pathDiff, pointer) - 0.5f * (float)Math.PI);
                            float adjustedAngle = Math.Max(angle - 0.08f * (float)Math.PI, angle * 0.25f);
                            float factor = 1.15f * (1f + 1f * adjustedAngle / (0.5f * (float)Math.PI));
                            if (factor < bestFactor)
                            {
                                bestFactor = factor;
                            }
                        }

                        currScore *= bestFactor;
                    }

                    Vector2 pathDir = p.To.Position - p.From.Position;
                    List<Building> oppositeNeighbours = oppositeN[building];
                    if (oppositeNeighbours.Count > 0)
                    {
                        float bestScore = float.MinValue;
                        float bestFactor = 1f;

                        foreach (Building n in oppositeNeighbours)
                        {
                            Vector2 oppositeDiff = n.Position - building.Position;
                            float score = MyMath.Angle(pathDir, oppositeDiff);
                            if (score > 0.5f * Math.PI)
                            {
                                score -= 0.5f * (float)Math.PI;
                            }
                            score /= (float)Math.Sqrt(oppositeDiff.Length());
                            if (n.IsEven != building.IsEven)
                            {
                                score *= 1.2f;
                            }

                            if (score < bestScore)
                            {
                                continue;
                            }

                            bestScore = score;
                            float angle = MyMath.Angle(pathDiff, oppositeDiff);
                            float diffFromIdealAngle = Math.Max(angle - 0.8f * (float)Math.PI, angle * 0.05f);
                            bestFactor = 1f + 0.8f * diffFromIdealAngle / (float)Math.PI;
                        }

                        currScore *= bestFactor;
                    }

                    scores[p] = currScore;
                }
            }
        }

        private float GetBuildingStreetScoreMult(Building building, StreetPath p)
        {
            if (p.Street == building.Street)
            {
                return 0.8f;
            }
            else if (p.Segment != null && p.Segment.Contains(building.Street))
            {
                return 0.95f;
            }
            else
            {
                float percOnPath = p.PercentageOnPath(building.Position);
                float maxSearchDistance = 100;
                if (ConnectsToStreet(p, percOnPath, building.Street, maxSearchDistance, out float streetDist))
                {
                    if (p.Street == null)
                    {
                        return 1f + 0.3f * streetDist / maxSearchDistance;
                    }
                    else
                    {
                        return 1f + 4f * streetDist / maxSearchDistance;
                    }
                }
                else
                {
                    if (p.Street == null)
                    {
                        return 1f;
                    }
                    else
                    {
                        return 5f;
                    }
                }
            }
        }

        private bool ConnectsToStreet(StreetPath path, float percOnPath, Street street, float maxSearchDistance, out float distance)
        {
            if (path.Street == street)
            {
                distance = 0;
                return true;
            }

            float fromDist = percOnPath * path.Length;
            float toDist = (1 - percOnPath) * path.Length;

            HashSet<StreetPath> evaluated = new HashSet<StreetPath>() { path };
            SimplePriorityQueue<StreetNode> nodes = new SimplePriorityQueue<StreetNode>();
            Dictionary<StreetNode, float> scores = new() { { path.From, fromDist }, { path.To, toDist } };

            nodes.Enqueue(path.From, fromDist);
            nodes.Enqueue(path.To, toDist);

            while (nodes.Count > 0)
            {
                StreetNode current = nodes.Dequeue();
                foreach (StreetPath p in current.Paths)
                {
                    if (evaluated.Contains(p))
                    {
                        continue;
                    }

                    distance = scores[current];

                    if (p.Street == street)
                    {
                        return true;
                    }

                    evaluated.Add(p);

                    distance += p.Length;
                    if (distance > maxSearchDistance)
                    {
                        continue;
                    }

                    StreetNode other = p.GetOther(current);

                    if (scores.TryGetValue(other, out float otherScore))
                    {
                        if (distance > otherScore)
                        {
                            continue;
                        }

                        scores[other] = distance;
                        nodes.UpdatePriority(other, distance);
                    }
                    else
                    {
                        scores.Add(other, distance);
                        nodes.Enqueue(other, distance);
                    }
                }
            }

            distance = maxSearchDistance;
            return false;
        }

        private Building? GetClosestNeighbour(Building building, List<Building> directNeighbours, out Building? higher, out Building? lower)
        {
            bool isEven = building.IsEven;

            higher = directNeighbours.Where(b => b.NumberId > building.NumberId).MinBy(b => b.NumberId - building.NumberId);

            lower = directNeighbours.Where(b => b.NumberId < building.NumberId).MinBy(b => building.NumberId - b.NumberId);

            Building? closest = null;
            if (higher != null && lower != null)
            {
                closest = Vector2.Distance(building.Position, higher.Position) < Vector2.Distance(building.Position, lower.Position) ? higher : lower;
            }
            else
            {
                closest = higher == null ? lower : higher;
            }
            return closest;
        }

        private void CastToBestPath(Dictionary<Building, Dictionary<StreetPath, float>> buildingScores)
        {
            _casts.Clear();

            foreach (var bScores in buildingScores)
            {
                Building building = bScores.Key;
                Dictionary<StreetPath, float> scores = bScores.Value;
                if (scores.Count == 0)
                {
                    continue;
                }

                StreetPath final = scores.MinBy(d => d.Value).Key;
                _casts.Add(building, final);
            }
        }

        private void IdentifyStreetsByCasts()
        {
            _identified.Clear();

            Dictionary<StreetSegment, Dictionary<Street, int>> segmentStreetVotes = new();

            foreach (var bScore in _casts)
            {
                Building b = bScore.Key;
                StreetPath p = bScore.Value;

                if (p.Street != null)
                {
                    continue;
                }

                StreetSegment? segment = p.Segment;
                if (segment == null)
                {
                    throw new Exception();
                }

                if (!segmentStreetVotes.TryGetValue(segment, out Dictionary<Street, int>? votes))
                {
                    votes = new Dictionary<Street, int>();
                    segmentStreetVotes.Add(segment, votes);
                }

                if (!votes.ContainsKey(b.Street))
                {
                    votes.Add(b.Street, 1);
                }
                else
                {
                    votes[b.Street] += 1;
                }
            }

            foreach (var seg in segmentStreetVotes)
            {
                StreetSegment segment = seg.Key;
                Dictionary<Street, int> votes = seg.Value;

                if (votes.Count == 0)
                {
                    throw new Exception();
                }

                KeyValuePair<Street, int> best = votes.MaxBy(v => v.Value);
                if (best.Value < 2)
                {
                    continue;
                }

                if (votes.Count > 1)
                {
                    KeyValuePair<Street, int> second = votes.Where(v => v.Key != best.Key).MaxBy(v => v.Value);

                    if (best.Value < 2 * second.Value)
                    {
                        continue;
                    }
                }

                foreach (StreetPath path in segment.Paths.Where(p => p.Street == null))
                {
                    _identified.Add(path, best.Key);
                }
            }

            IdentifyStreetsByConnections();
        }

        private void AdjustScoresWithIdentifiedStreets(Dictionary<Building, Dictionary<StreetPath, float>> buildingScores)
        {
            foreach (var bScore in buildingScores)
            {
                Building building = bScore.Key;
                Dictionary<StreetPath, float> pScores = bScore.Value;

                foreach (var pScore in pScores)
                {
                    if (!_identified.TryGetValue(pScore.Key, out Street? street))
                    {
                        continue;
                    }

                    pScores[pScore.Key] *= GetBuildingStreetScoreMult(building, pScore.Key) * 1.1f;
                }
            }
        }

        private void AlignGroups(Dictionary<Building, Dictionary<StreetPath, float>> buildingScores)
        {
            Dictionary<Street, List<Building>[]> streetBuildings = new();
            foreach (var building in buildingScores.Keys)
            {
                if (!streetBuildings.TryGetValue(building.Street, out List<Building>[]? lists))
                {
                    lists = new List<Building>[2];
                    lists[0] = new List<Building>();
                    lists[1] = new List<Building>();
                    streetBuildings.Add(building.Street, lists);
                }

                lists[building.IsEven ? 0 : 1].Add(building);
            }

            foreach (var streetLists in streetBuildings.Values)
            {
                for (int i = 0; i < 2; i++)
                {
                    List<Building> current = streetLists[i];
                    if (current.Count < 3)
                    {
                        continue;
                    }

                    current.Sort((a, b) => Comparer<int>.Default.Compare(a.NumberId, b.NumberId));
                    List<List<Building>> groups = SplitIntoGroups(current);
                    int leftCastCount = CountCastsToLeft(current);
                    List<bool?> leftCastInfo = GetGroupCastInfo(groups, leftCastCount);
                    AdjustGroupScores(groups, leftCastInfo, buildingScores);
                }
            }
        }

        private List<List<Building>> SplitIntoGroups(List<Building> buildings)
        {
            List<List<Building>> groups = new();
            if (buildings.Count == 0)
            {
                return groups;
            }

            List<Building> group = new();

            for (int i = 0; i < buildings.Count; i++)
            {
                Building current = buildings[i];

                GetNeighbourVectors(buildings, i, out Vector2 prevToCurrent, out Vector2 currentToNext);

                if (prevToCurrent.Length() > 25)
                {
                    if (group.Count > 0)
                    {
                        groups.Add(group);
                    }
                    group = new() { current };
                    continue;
                }

                if (MyMath.Angle(prevToCurrent, currentToNext) < 0.2f * Math.PI)
                {
                    group.Add(current);
                    continue;
                }

                if (prevToCurrent.LengthSquared() < currentToNext.LengthSquared())
                {
                    group.Add(current);
                    groups.Add(group);
                    group = new();
                }
                else
                {
                    if (group.Count > 0)
                    {
                        groups.Add(group);
                    }
                    group = new() { current };
                }
            }

            if (group.Count > 0)
            {
                groups.Add(group);
            }

            return groups;
        }

        private int CountCastsToLeft(List<Building> buildings)
        {
            if (buildings.Count < 2)
            {
                return 0;
            }

            int count = 0;

            for (int i = 0; i < buildings.Count; i++)
            {
                Building current = buildings[i];
                if (!_casts.TryGetValue(current, out StreetPath? castPath))
                {
                    continue;
                }

                GetNeighbourVectors(buildings, i, out Vector2 prevToCurrent, out Vector2 currentToNext);

                Vector2 comparer = prevToCurrent.LengthSquared() < currentToNext.LengthSquared() ? prevToCurrent : currentToNext;
                Vector2 pointer = castPath.CastPosition(current.Position) - current.Position;

                float angle = MyMath.Angle(comparer, pointer);
                if (angle > 0.7f * Math.PI || angle < 0.3f * Math.PI)
                {
                    continue;
                }

                if (MyMath.PointsToTheLeft(comparer, pointer))
                {
                    count += 1;
                }
                else
                {
                    count -= 1;
                }
            }

            return count;
        }

        private List<bool?> GetGroupCastInfo(List<List<Building>> groups, int primer)
        {
            float initial = Math.Sign(primer) * 0.5f;

            List<bool?> result = new List<bool?>(groups.Count);
            foreach (var group in groups)
            {
                float leftVal = CountCastsToLeft(group) + initial;
                if (leftVal == 0)
                {
                    result.Add(null);
                }
                else if (leftVal > 0)
                {
                    result.Add(true);
                }
                else
                {
                    result.Add(false);
                }
            }

            return result;
        }

        private void AdjustGroupScores(List<List<Building>> groups, List<bool?> castInfo, Dictionary<Building, Dictionary<StreetPath, float>> buildingScores)
        {
            for (int j = 0; j < groups.Count; j++)
            {
                bool? leftCast = castInfo[j];
                if (leftCast == null)
                {
                    continue;
                }

                List<Building> buildings = groups[j];
                if (buildings.Count < 2)
                {
                    continue;
                }

                for (int i = 0; i < buildings.Count; i++)
                {
                    Building current = buildings[i];
                    Dictionary<StreetPath, float> scores = buildingScores[current];
                    if (scores.Count < 2)
                    {
                        continue;
                    }

                    GetNeighbourVectors(buildings, i, out Vector2 prevToCurrent, out Vector2 currentToNext);

                    Vector2 comparer = prevToCurrent.LengthSquared() < currentToNext.LengthSquared() ? prevToCurrent : currentToNext;

                    foreach (StreetPath path in scores.Keys)
                    {
                        Vector2 pointer = path.CastPosition(current.Position) - current.Position;
                        if (MyMath.PointsToTheLeft(comparer, pointer) == leftCast)
                        {
                            scores[path] *= 0.8f;
                            continue;
                        }

                        float angle = MyMath.Angle(pointer, comparer);
                        if (angle < 0.1f * Math.PI || angle > 0.9f * Math.PI)
                        {
                            scores[path] *= 0.9f;
                        }
                    }
                }
            }
        }

        private void GetNeighbourVectors(List<Building> buildings, int i, out Vector2 prevToCurrent, out Vector2 currentToNext)
        {
            Building current = buildings[i];
            prevToCurrent = i == 0 ? buildings[i + 1].Collider.Position - current.Collider.Position :
                current.Collider.Position - buildings[i - 1].Collider.Position;
            currentToNext = i == buildings.Count - 1 ? current.Collider.Position - buildings[i - 1].Collider.Position :
                buildings[i + 1].Collider.Position - current.Collider.Position;
        }

        private void AlignOutliers(Dictionary<Building, Dictionary<StreetPath, float>> buildingScores, Dictionary<Building, List<Building>> directN)
        {
            foreach (var bScore in buildingScores)
            {
                Building building = bScore.Key;
                List<Building> closeNeighbours = GetCloseNeighbours(building, directN);
                if (closeNeighbours.Count == 0)
                {
                    continue;
                }

                Dictionary<StreetPath, float> pathScores = buildingScores[building];
                if (pathScores.Count < 2)
                {
                    continue;
                }

                KeyValuePair<StreetPath, float> minimum = pathScores.MinBy(kvp => kvp.Value);
                if (pathScores.All(kvp => kvp.Key == minimum.Key || kvp.Value > minimum.Value * 5f))
                {
                    continue;
                }

                foreach (var path in pathScores.Keys)
                {
                    StreetSegment? segment = path.Segment;
                    if (segment == null)
                    {
                        throw new Exception();
                    }

                    float minAnglePerc = (float)Math.PI;

                    foreach (var bCast in new List<Vector2>() { path.CastPosition(building.Position), segment.From.Position, segment.To.Position })
                    {
                        Vector2 bDiff = bCast - building.Position;

                        foreach (Building n in closeNeighbours)
                        {
                            if (!_casts.TryGetValue(n, out StreetPath? nPath))
                            {
                                continue;
                            }
                            if (nPath.Segment == path.Segment)
                            {
                                minAnglePerc = 0;
                                break;
                            }

                            Vector2 nCast = nPath.CastPosition(n.Position);
                            Vector2 cDiff = nCast - bCast;
                            Vector2 pDiff = n.Position - building.Position;

                            float posAngle = MyMath.Angle(cDiff, pDiff);
                            float posAnglePerc = Math.Max(posAngle - 0.3f * (float)Math.PI, posAngle * 0.15f) / (float)Math.PI;

                            Vector2 nDiff = nCast - n.Position;

                            float castAngle = MyMath.Angle(nDiff, bDiff);
                            float castAnglePerc = Math.Max(castAngle - 0.2f * (float)Math.PI, castAngle * 0.15f) / (float)Math.PI;

                            float anglePerc = 0.7f * posAnglePerc + 2.3f * castAnglePerc;

                            if (anglePerc < minAnglePerc)
                            {
                                minAnglePerc = anglePerc;
                            }
                        }
                    }

                    pathScores[path] *= 1f + 1f * minAnglePerc;
                }
            }
        }

        private List<Building> GetCloseNeighbours(Building building, Dictionary<Building, List<Building>> directN)
        {
            List<Building> neighbours = directN[building];
            List<Building> closeNeighbours = new List<Building>();

            GetClosestNeighbour(building, neighbours, out Building? higher, out Building? lower);
            if (higher == null && lower == null)
            {
                return closeNeighbours;
            }

            if (higher != null) { closeNeighbours.Add(higher); }
            if (lower != null) { closeNeighbours.Add(lower); }

            return closeNeighbours;
        }

        private void AlignWithSegmentNeighbours(Dictionary<Building, Dictionary<StreetPath, float>> buildingScores)
        {
            Dictionary<StreetSegment, List<Building>> castResult = GetCastResult();

            foreach (var kvp in buildingScores)
            {
                Building building = kvp.Key;
                if (building.NumberId % Constants.ADDRESS_ADDON_RANGE != 0) // Has addon
                {
                    continue;
                }

                Dictionary<StreetPath, float> bScores = kvp.Value;

                Building? lower = buildingScores.Keys
                    .Where(b => b.Street == building.Street && b.IsEven == building.IsEven && b.NumberId % Constants.ADDRESS_ADDON_RANGE == 0 && b.NumberId < building.NumberId)
                    .MinBy(b => building.NumberId - b.NumberId);
                Building? higher = buildingScores.Keys
                    .Where(b => b.Street == building.Street && b.IsEven == building.IsEven && b.NumberId % Constants.ADDRESS_ADDON_RANGE == 0 && b.NumberId > building.NumberId)
                    .MinBy(b => b.NumberId - building.NumberId);
                if (lower == null && higher == null)
                {
                    continue;
                }

                foreach (StreetPath path in bScores.Keys)
                {
                    StreetSegment? seg = path.Segment;
                    if (seg == null)
                    {
                        throw new Exception();
                    }

                    if (!castResult.TryGetValue(seg, out List<Building>? list))
                    {
                        continue;
                    }

                    bool containsLower = lower != null && list.Contains(lower);
                    bool containsHigher = higher != null && list.Contains(higher);

                    if (containsLower && containsHigher)
                    {
                        bScores[path] /= 2f;
                    }
                    else if (containsLower || containsHigher)
                    {
                        bScores[path] /= 1.5f;
                    }
                }
            }
        }

        private Dictionary<StreetSegment, List<Building>> GetCastResult()
        {
            Dictionary<StreetSegment, List<Building>> castResult = new Dictionary<StreetSegment, List<Building>>(_map.Segments.Count / 2);

            foreach (var kvp in _casts)
            {
                Building building = kvp.Key;
                StreetSegment? segment = kvp.Value.Segment;
                if (segment == null)
                {
                    throw new Exception();
                }

                if (!castResult.TryGetValue(segment, out List<Building>? list))
                {
                    list = new(16);
                    castResult.Add(segment, list);
                }

                list.Add(building);
            }

            return castResult;
        }

        private void FixFarOutliersWithPathfinding(Dictionary<Building, Dictionary<StreetPath, float>> buildingScores, Dictionary<Building, List<Building>> neighbours)
        {
            List<Building> conflicts = new();

            foreach (Building building in _casts.Keys)
            {
                List<Building> close =
                    neighbours[building].Where(b => b.Street == building.Street && b.NumberId > building.NumberId && b.Number - building.Number < 10)
                    .ToList();
                if (close.Count == 0)
                {
                    continue;
                }

                int count = Math.Min(close.Count, 5);

                List<Building> testNeighbours = new(count);
                for (int i = 0; i < count; i++)
                {
                    Building? toAdd = close.Where(c => !testNeighbours.Contains(c)).MinBy(c => Math.Abs(c.NumberId - building.NumberId));
                    if (toAdd == null)
                    {
                        throw new Exception();
                    }

                    testNeighbours.Add(toAdd);
                }

                StreetPath buildingPath = _casts[building];
                StreetNode buildingNode = buildingPath.GetClosestNode(building.Position);
                foreach (Building n in testNeighbours)
                {
                    if (!_casts.TryGetValue(n, out StreetPath? neighbourPath) || neighbourPath.Segment == buildingPath.Segment)
                    {
                        continue;
                    }

                    StreetNode neighbourNode = neighbourPath.GetClosestNode(n.Position);

                    IPathfinder<StreetNode, StreetPathway> path = IPathfinder.FindPath<StreetNode, StreetPathway>(buildingNode, neighbourNode,
                        w => (w.Path.IsActive ? 1 : -1) * (w.Path.RestrictedAccess ? 1048576 : 1));

                    if (!path.Success)
                    {
                        throw new Exception();
                    }

                    if (path.GetLength() > Vector2.Distance(building.Position, n.Position) * 2 + 100)
                    {
                        conflicts.Add(building);
                        conflicts.Add(n);
                    }
                }
            }

            if (conflicts.Count == 0)
            {
                return;
            }

            while (conflicts.Count > 0)
            {
                Building? current = conflicts.MaxBy(b => conflicts.Count(c => c == b));
                if (current == null)
                {
                    throw new Exception();
                }

                while (true)
                {
                    int index = conflicts.IndexOf(current);
                    if (index < 0)
                    {
                        break;
                    }

                    conflicts.RemoveAt(index);
                    conflicts.RemoveAt(index % 2 == 0 ? index : index - 1);
                }

                List<Building> close =
                    neighbours[current].Where(b => b.Street == current.Street && b.Number - current.Number < 10).ToList();
                if (close.Count == 0)
                {
                    continue;
                }

                Dictionary<StreetPath, float> pathScores = buildingScores[current];

                foreach (StreetPath buildingPath in pathScores.Keys)
                {
                    StreetNode buildingNode = buildingPath.GetClosestNode(current.Position);
                    float multiplier = 1f;

                    foreach (Building n in close)
                    {
                        if (!_casts.TryGetValue(n, out StreetPath? neighbourPath) || neighbourPath.Segment == buildingPath.Segment)
                        {
                            continue;
                        }

                        StreetNode neighbourNode = neighbourPath.GetClosestNode(n.Position);

                        IPathfinder<StreetNode, StreetPathway> path = IPathfinder.FindPath<StreetNode, StreetPathway>(buildingNode, neighbourNode,
                            w => (w.Path.IsActive ? 1 : -1) * (w.Path.RestrictedAccess && w.Path != buildingPath && w.Path != neighbourPath ? 1048576 : 1));

                        if (!path.Success)
                        {
                            throw new Exception();
                        }

                        if (path.GetLength() < Vector2.Distance(current.Position, n.Position) * 2 + 100)
                        {
                            continue;
                        }

                        int conflictCount = conflicts.Count(c => c == n);
                        multiplier += 10f / (conflictCount + 1);
                    }

                    pathScores[buildingPath] *= multiplier;
                }
            }
        }
    }
}