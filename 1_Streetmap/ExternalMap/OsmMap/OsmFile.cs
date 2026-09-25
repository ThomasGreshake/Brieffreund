//Copyright Thomas Greshake 2026

using Spectre.Console;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Xml;

namespace Brieffreund.Streetmap.Osm
{
    internal class OsmFile : IDisposable
    {
        //Data -------------------------------------------------------------

        private Dictionary<long, OsmNode> _nodes = new(8192);
        internal IDictionary<long, OsmNode> Nodes => _nodes;

        private List<OsmPath> _paths = new(8192);
        internal ReadOnlyCollection<OsmPath> Paths;

        private readonly float[] _lonLatBorders = new float[4]; //min_lon, max_lon, min_lat, max_lat
        internal float Longitude => (_lonLatBorders[0] + _lonLatBorders[1]) / 2f;
        internal float Latitude => (_lonLatBorders[2] + _lonLatBorders[3]) / 2f;

        private Vector2 _zero_position_Meter;

        private double _factor_Lon_To_Meter;

        private bool _success = true;
        internal bool Success => _success;

        //Interface ---------------------------------------------------------
        public void Dispose()
        {
            _nodes = new();
            _paths = new();
        }

        //Internals ---------------------------------------------------------
        internal OsmNode GetNode(long id)
        {
            if (_nodes.TryGetValue(id, out OsmNode? node))
            {
                return node;
            }
            throw new Exception();
        }

        internal bool TryGetNode(long id, [MaybeNullWhen(false)] out OsmNode? node) => _nodes.TryGetValue(id, out node);

        //Setup ------------------------------------------------------------
        internal OsmFile(IUserInput input, string osmFileName)
        {
            Paths = _paths.AsReadOnly();

            DetermineBorders(input, osmFileName);

            if (!_success)
            {
                return;
            }

            ReadXml(osmFileName);
        }

        #region Borders

        private void DetermineBorders(IUserInput input, string osmFileName)
        {
            int addressCount = 0;
            foreach (var list in input.MailAddresses.Values)
            {
                addressCount += list.Count;
            }

            int sampleCount = addressCount / 2;
            HashSet<long> nodes;
            try
            {
                using (XmlReader reader = XmlReader.Create(osmFileName))
                {
                    nodes = FindNodesWithAddress(reader, input, sampleCount);
                }
            }
            catch (Exception e)
            {
                if (e is FileNotFoundException)
                {
                    AnsiConsole.WriteLine("[red]Die Osm-Daten konnten nicht gefunden werden.[/]");
                }
                else
                {
                    AnsiConsole.WriteException(e, ExceptionFormats.NoStackTrace);
                }

                _success = false;
                return;
            }

            if (nodes.Count < sampleCount / 2)
            {
                AnsiConsole.WriteLine("[red]Die Osm-Daten enthalten nicht genug Informationen über den Bezirk.[/]");
                _success = false;
                return;
            }

            FindPositionsOfNodesAndSetBorder(osmFileName, nodes, sampleCount);
        }

        private HashSet<long> FindNodesWithAddress(XmlReader reader, IUserInput input, int sampleCount)
        {
            HashSet<long> sampleNodes = new HashSet<long>(sampleCount);

            List<long> nodes = new();
            Dictionary<string, string> flags = new();

            reader.MoveToContent();
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }
                if (reader.Name == "relation") //Relations come after all Ways
                {
                    break;
                }
                if (reader.Name != "way")
                {
                    continue;
                }

                if (!ReadPath(reader.ReadSubtree(), nodes, flags))
                {
                    continue;
                }

                if (!flags.TryGetValue("addr:postcode", out string? code) || !int.TryParse(code, out int plz) || !input.PostCodes.Contains(plz))
                {
                    continue;
                }
                if (!flags.TryGetValue("addr:city", out string? city) || string.IsNullOrWhiteSpace(city) || !input.Cities.Contains(city.ToLower()))
                {
                    continue;
                }
                if (!flags.TryGetValue("addr:street", out string? sName) || string.IsNullOrWhiteSpace(sName)
                    || !input.Streets.TryGetValue(Street.NameToId(sName), out Street? street)
                    || !input.MailAddresses.TryGetValue(street, out IList<string>? numbers))
                {
                    continue;
                }
                if (!flags.TryGetValue("addr:housenumber", out string? number) || string.IsNullOrWhiteSpace(number) || !numbers.Contains(number))
                {
                    continue;
                }

                long id = nodes[0];
                if (sampleNodes.Contains(id))
                {
                    continue;
                }

                sampleNodes.Add(id);

                if (sampleNodes.Count >= sampleCount)
                {
                    break;
                }
            }

            return sampleNodes;
        }

        private void FindPositionsOfNodesAndSetBorder(string osmFileName, HashSet<long> nodes, int sampleCount)
        {
            List<Vector2> positions;

            using (XmlReader reader = XmlReader.Create(osmFileName))
            {
                positions = FindPositionsOfNodesAndSetBorder(reader, nodes, sampleCount);
            }

            InitialiseBorderWithPosition(positions[0]);
            for (int i = 1; i < positions.Count; i++)
            {
                IncludePositionInBorder(positions[i]);
            }
            AdjustBorder();
        }

        private List<Vector2> FindPositionsOfNodesAndSetBorder(XmlReader reader, HashSet<long> nodes, int sampleCount)
        {
            int positionSampleCount = (sampleCount * 2) / 3;
            List<Vector2> positions = new(positionSampleCount);
            reader.MoveToContent();
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }
                if (reader.Name == "way") //Ways come after all nodes
                {
                    break;
                }
                if (reader.Name != "node" || !long.TryParse(reader.GetAttribute("id"), out long id) || !nodes.Contains(id))
                {
                    continue;
                }

                string? lonString = reader.GetAttribute("lon");
                string? latString = reader.GetAttribute("lat");

                if (string.IsNullOrWhiteSpace(lonString) || string.IsNullOrWhiteSpace(latString))
                {
                    continue;
                }

                if (float.TryParse(lonString.Replace(".", ","), out float lon) && float.TryParse(latString.Replace(".", ","), out float lat))
                {
                    positions.Add(new Vector2(lon, lat));
                }

                if (positions.Count >= positionSampleCount)
                {
                    break;
                }
            }

            return positions;
        }

        private void InitialiseBorderWithPosition(Vector2 position)
        {
            _lonLatBorders[0] = position.X;
            _lonLatBorders[1] = position.X;
            _lonLatBorders[2] = position.Y;
            _lonLatBorders[3] = position.Y;
        }

        private void IncludePositionInBorder(Vector2 position)
        {
            if (_lonLatBorders[0] > position.X)
            {
                _lonLatBorders[0] = position.X;
            }
            else if (_lonLatBorders[1] < position.X)
            {
                _lonLatBorders[1] = position.X;
            }
            if (_lonLatBorders[2] > position.Y)
            {
                _lonLatBorders[2] = position.Y;
            }
            else if (_lonLatBorders[3] < position.Y)
            {
                _lonLatBorders[3] = position.Y;
            }
        }

        private void AdjustBorder()
        {
            Vector2 lonLatPos = 0.5f * new Vector2(_lonLatBorders[0] + _lonLatBorders[1], _lonLatBorders[2] + _lonLatBorders[3]);

            SetFactorAndPosition();

            Vector2 min = new Vector2(_lonLatBorders[0], _lonLatBorders[2]);
            Vector2 max = new Vector2(_lonLatBorders[1], _lonLatBorders[3]);

            Vector2 dimensions = LonLatToMeter(max) - LonLatToMeter(min);

            dimensions = new Vector2(
                Math.Clamp(Math.Abs(dimensions.X) + 2 * Constants.BORDER_METER, Constants.MIN_SIZE_METER, Constants.MAX_SIZE_METER),
                Math.Clamp(Math.Abs(dimensions.Y) + 2 * Constants.BORDER_METER, Constants.MIN_SIZE_METER, Constants.MAX_SIZE_METER));

            min = MeterToLonLat(_zero_position_Meter - 0.5f * dimensions);
            max = MeterToLonLat(_zero_position_Meter + 0.5f * dimensions);

            _lonLatBorders[0] = min.X;
            _lonLatBorders[1] = max.X;
            _lonLatBorders[2] = min.Y;
            _lonLatBorders[3] = max.Y;
        }

        private void SetFactorAndPosition()
        {
            Vector2 lonLatPos = 0.5f * new Vector2(_lonLatBorders[0] + _lonLatBorders[1], _lonLatBorders[2] + _lonLatBorders[3]);
            float lat = MyMath.DegreesToRadians(lonLatPos.Y);
            _factor_Lon_To_Meter = Math.PI * Constants.EARTHRADIUS_METER * Math.Cos(lat) / 180d;
            _zero_position_Meter = LonLatToMeter(lonLatPos);
        }

        #endregion Borders

        #region NodesAndPaths

        private void ReadXml(string osmFileName)
        {
            try
            {
                using (XmlReader reader = XmlReader.Create(osmFileName))
                {
                    ReadXml(reader);
                }
            }
            catch
            {
                _success = false;
                return;
            }
        }

        private void ReadXml(XmlReader reader)
        {
            List<long> nodes = new();
            Dictionary<string, string> flags = new();
            reader.MoveToContent();

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }
                if (reader.Name == "node")
                {
                    if (!ReadNode(reader, flags, out long id, out Vector2 pos) || !IsWithinBorders(pos))
                    {
                        continue;
                    }

                    pos = LonLatToMeter(pos) - _zero_position_Meter;

                    Dictionary<string, string> copiedFlags = new(flags);
                    OsmNode node = new(pos, copiedFlags);

                    _nodes.Add(id, node);
                }
                else if (reader.Name == "way")
                {
                    if (!ReadPath(reader.ReadSubtree(), nodes, flags))
                    {
                        continue;
                    }

                    nodes.RemoveAll(n => !_nodes.ContainsKey(n));

                    if (nodes.Count == 0)
                    {
                        continue;
                    }

                    List<long> copiedNodes = new(nodes);
                    Dictionary<string, string> copiedFlags = new(flags);
                    OsmPath path = new(copiedNodes, copiedFlags);

                    _paths.Add(path);
                }
            }
        }

        private bool IsWithinBorders(Vector2 lonLat) =>
            lonLat.X >= _lonLatBorders[0] && lonLat.X <= _lonLatBorders[1] && lonLat.Y >= _lonLatBorders[2] && lonLat.Y <= _lonLatBorders[3];

        //Warning: x=lon, y=lat
        private bool ReadNode(XmlReader reader, Dictionary<string, string> flags, out long id, out Vector2 position)
        {
            flags.Clear();
            if (long.TryParse(reader.GetAttribute("id"), out id))
            {
                string? lonString = reader.GetAttribute("lon");
                string? latString = reader.GetAttribute("lat");

                if (string.IsNullOrWhiteSpace(lonString) || string.IsNullOrWhiteSpace(latString))
                {
                    position = new();
                    return false;
                }

                if (float.TryParse(lonString.Replace(".", ","), out float lon) && float.TryParse(latString.Replace(".", ","), out float lat))
                {
                    position = new(lon, lat);
                    ReadNodeFlags(reader.ReadSubtree(), flags);
                    return true;
                }
            }

            position = new();
            return false;
        }

        private void ReadNodeFlags(XmlReader reader, Dictionary<string, string> flags)
        {
            reader.MoveToContent();

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element || reader.Name != "tag")
                {
                    continue;
                }
                string? k = reader.GetAttribute("k");
                string? v = reader.GetAttribute("v");
                if (!string.IsNullOrWhiteSpace(k) && !string.IsNullOrWhiteSpace(v))
                {
                    flags.TryAdd(k, v);
                }
            }
        }

        private bool ReadPath(XmlReader reader, List<long> nodes, Dictionary<string, string> flags)
        {
            nodes.Clear();
            flags.Clear();

            reader.MoveToContent();

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }
                if (reader.Name == "nd" && long.TryParse(reader.GetAttribute("ref"), out long nodeId) && !nodes.Contains(nodeId))
                {
                    nodes.Add(nodeId);
                }
                else if (reader.Name == "tag")
                {
                    string? k = reader.GetAttribute("k");
                    string? v = reader.GetAttribute("v");
                    if (!string.IsNullOrWhiteSpace(k) && !string.IsNullOrWhiteSpace(v))
                    {
                        flags.TryAdd(k, v);
                    }
                }
            }

            return nodes.Count > 0 && flags.Count > 0;
        }

        #endregion NodesAndPaths

        //Privates ---------------------------------------------------------
        private Vector2 LonLatToMeter(Vector2 lonLat) => new Vector2((float)(lonLat.X * _factor_Lon_To_Meter), (float)(lonLat.Y * Constants.FACTOR_LAT_TO_METER));

        private Vector2 MeterToLonLat(Vector2 meter) => new Vector2((float)(meter.X / _factor_Lon_To_Meter), (float)(meter.Y / Constants.FACTOR_LAT_TO_METER));
    }
}