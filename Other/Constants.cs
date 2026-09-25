//Copyright Thomas Greshake 2026

namespace Brieffreund
{
    internal static class Constants
    {
        internal const string NAME = "Brieffreund";

        internal static readonly bool PRINT_ALL = false;

        internal const string DEFAULT_DISTRICT_FILE = "Bezirk.txt", DEFAULT_OSM_FILE = "map.osm";

        internal const int DEFAULT_SEARCHDEPTH = 16;

        internal const float MAX_STREETPATH_LENGTH = 15f;

        //Dimensions added to the computed borders of the district given in the user input
        internal const float BORDER_METER = 200;

        //Minimum and maximum dimensions of the district
        internal const float MIN_SIZE_METER = 500, MAX_SIZE_METER = 5000;

        internal const double EARTHRADIUS_METER = 6371009d;
        internal const double FACTOR_LAT_TO_METER = Math.PI * EARTHRADIUS_METER / 180d;

        internal const string COMMENT_KEY = "#";

        internal const int PATHINGCOUNT_SEARCHDEPTH_MULT = 4;

        internal const float TRAFFIC_SIGNAL_DISTANCE = 400f;

        internal const float TINY_MAILAMOUNT_MULTIPLIER = 0.3f,
            ACCEPTABLE_MAILAMOUNT_MULTIPLIER = 1.6f,
            VERY_GOOD_MULTIPLIER = 1.4f,
            INACCEPTABLE_MULTIPLIER = 1.9f,
            OVERFLOW_PERCENTAGE_TO_DISTANCE_PENALTY = 100000f;

        internal const float WIDTH_TINY = 1f, WIDTH_MINOR = 8f, WIDTH_TERTIARY = 10f, WIDTH_SECONDARY = 12f, WIDTH_PRIMARY = 14f;

        internal const float INACTIVE_PASSING_MULT = 0.99f, DEADEND_MULT = 0.45f,
            PASSING_VERY_LIGHT_AND_TINY = 1f, PASSING_VERY_LIGHT = 5f, PASSING_LIGHT = 9f, PASSING_MEDIUM = 14f, PASSING_HEAVY = 20f, PASSING_VERY_HEAVY = 30f,
            PASSING_SINGLEPATH_MALUS = 3f;

        internal const float PATH_TRIM_LENGTH_METER = 5;

        internal const float CONNECTS_DIRECTLY_PASSING_DISTANCE = WIDTH_TINY + PASSING_VERY_LIGHT_AND_TINY + .5f;

        internal const float NODE_SCORE_MULT = 0.5f;

        internal const bool EULERMAP_SPEED_OVER_ORDER = false;

        internal const int ADDRESS_ADDON_RANGE = 30;

        internal const float MAX_DISTANCE_CLOSEST_INTERSECTION = 50;

        internal const float MAXIMUM_NOBUILDINGS_DISTANCE = 35f;

        internal const float MAX_PATH_DISTANCE_METER = 75, MIN_PATH_DISTANCE_METER = 7.5f, NEIGHBOUR_COLLISION_METER = 30, BUILDING_COLLISION_FACTOR = 2;

        internal const float TRIM_LENGTH_METER = 8f, TINY_TRIM_LENGTH_METER = 16f;

        internal const int THREAD_COUNT = 8;

        internal const int STREET_PADDING_FOR_PRINT = 21;

        internal const int ADDRESS_LINE_LENGTH = 100;

        internal const float MAIL_TO_SCORE = 0.0001f;

        internal const float STORAGE_FORGIVENESS_DISTANCE = 10f;

        internal const float USELESS_SEGMENT_FACTOR = 2f, USELESS_TINY_SEGMENT_FACTOR = 3f;

        internal const float MAX_STORAGE_DISTANCE_METER = 200, STORAGE_RESCALE_DISTANCE = 100, STORAGE_RESCALE_FACTOR = 2;

        internal const int EARLY_RETURN_COUNT = 32768;

        internal const float SIDEFACTOR_TO_BONUSLENGTH = .15f;
    }
}