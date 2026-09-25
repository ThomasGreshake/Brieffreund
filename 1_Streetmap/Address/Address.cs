//Copyright Thomas Greshake 2026

using System.Numerics;

namespace Brieffreund
{
    internal interface IOnAddressCreated : IEvent<IOnAddressCreated>

    { public Address GetAddress(); }

    internal interface IOnAddressAssociated : IEvent<IOnAddressAssociated>

    { public Address GetAddress(); }

    internal abstract class Address : IOnAddressCreated, IOnAddressAssociated
    {
        //Data -------------------------------------------------------------

        internal readonly IStreetMap Map;

        internal readonly Street Street;

        private readonly List<string> _numbers = new();

        private int _numberId;
        internal int NumberId => _numberId;

        private int _number;
        internal int Number => _number;

        internal bool IsEven => Number % 2 == 0;

        internal readonly StreetPath Path;

        internal StreetSegment Segment
        {
            get
            {
                StreetSegment? segment = Path.Segment;
                if (segment == null)
                {
                    throw new Exception();
                }
                return segment;
            }
        }

        internal readonly Vector2 Position;

        internal bool LeftOfPath => Path.IsLeftOfPath(Position);

        internal Vector2 CastPosition => Path.CastPosition(Position);

        internal StreetNode ClosestNode => Path.GetClosestNode(Position);

        internal string Name => Street.Name + " " + _numbers[0];

        public Address GetAddress() => this;

        //Setup -------------------------------------------------------------
        protected Address(IStreetMap map, Street street, string fullNumber, StreetPath path, Vector2 position)
        {
            Map = map;
            Street = street;
            _numbers.Add(fullNumber);
            Street.FullnumberToNumberId(fullNumber, out _numberId);
            _number = Street.NumberIdToNumber(_numberId);
            Path = path;
            Position = position;
        }

        //Internals -------------------------------------------------------------

        internal float GetDistanceOnSegment()
        {
            StreetSegment segment = Segment;

            float dist = 0;

            foreach (StreetPath p in segment.Paths)
            {
                if (p == Path) { break; }
                dist += p.Length;
            }

            dist += Path.PercentageOnPath(Position) * Path.Length;

            return dist;
        }

        internal float GetPercentageOnSegment()
        {
            StreetSegment? segment = Segment;
            if (segment == null) { return 0; }
            float dist = GetDistanceOnSegment();
            return dist / segment.Length;
        }

        internal Intersection GetClosestIntersection()
        {
            StreetSegment segment = Segment;
            return GetPercentageOnSegment() > 0.5f ? segment.To : segment.From;
        }

        internal int GetSidewalkId() => StreetSegment.GetSidewalkId(Segment, LeftOfPath);

        internal void Add(string fullNumber)
        {
            if (SortIntoNumbers(fullNumber))
            {
                IOnAddressAssociated.Call(this);
            }
        }

        internal bool Contains(string fullNumber) => _numbers.Contains(fullNumber);

        internal List<string> GetNumbers(bool ascending)
        {
            List<string> numbers = new List<string>(_numbers);
            if (!ascending)
            {
                numbers.Reverse();
            }
            return numbers;
        }

        //Privates -------------------------------------------------------------

        private bool SortIntoNumbers(string fullNumber)
        {
            if (!Street.FullnumberToNumberId(fullNumber, out int numberId))
            {
                return false;
            }

            if (_numberId > numberId)
            {
                _numberId = numberId;
            }

            int number = Street.NumberIdToNumber(numberId);

            if (_number > number)
            {
                _number = number;
            }

            for (int i = 0; i < _numbers.Count; i++)
            {
                string current = _numbers[i];
                Street.FullnumberToNumberId(current, out int id);
                if (id > numberId)
                {
                    _numbers.Insert(i, fullNumber);
                    return true;
                }
            }

            _numbers.Add(fullNumber);
            return true;
        }
    }
}