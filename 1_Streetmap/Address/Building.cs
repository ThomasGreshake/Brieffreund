//Copyright Thomas Greshake 2026

using System.Numerics;

namespace Brieffreund.Streetmap
{
    internal enum BuildingType : byte
    {
        Home, Secondary, Apartments, Residential, Primary, Address
    }

    internal class Building
    {
        //Data -------------------------------------------------------------
        internal readonly Street Street;

        internal readonly int NumberId;
        internal int Number => Street.NumberIdToNumber(NumberId);
        internal bool IsEven => Number % 2 == 0;

        private Vector2 _entrancePosition;
        internal Vector2 Position => _entrancePosition;

        private float _surfaceArea, _totalArea;
        internal float TotalArea => _totalArea;

        internal readonly BuildingType Type;

        internal readonly PolygonCollider Collider;

        //Setup ------------------------------------------------------------

        internal Building(Street street, int numId, List<Vector2> nodes, Vector2 entrance, BuildingType type, int levels)
        {
            Street = street;
            NumberId = numId;
            Type = type;

            _entrancePosition = entrance;

            if (nodes.Count > 2)
            {
                _surfaceArea = Math.Max(MyMath.PolygonArea(nodes), 30);
            }
            else
            {
                _surfaceArea = 60;
            }

            _totalArea = _surfaceArea * levels;

            Collider = new(nodes);
        }

        internal bool Merge(Building building)
        {
            if (NumberId != building.NumberId || Street != building.Street)
            {
                return false;
            }

            _totalArea += building._totalArea;

            if (Vector2.Distance(_entrancePosition, building._entrancePosition) > 60)
            {
                _entrancePosition = _totalArea > building._totalArea ? _entrancePosition : building._entrancePosition;
            }
            else
            {
                _entrancePosition =
                    (building._entrancePosition * building._surfaceArea * building._surfaceArea + _entrancePosition * _surfaceArea * _surfaceArea)
                    / (building._surfaceArea * building._surfaceArea + _surfaceArea * _surfaceArea);
            }

            Collider.Merge(building.Collider);
            return true;
        }
    }
}