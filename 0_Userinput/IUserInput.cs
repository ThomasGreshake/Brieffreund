//Copyright Thomas Greshake 2026

namespace Brieffreund
{
    public enum AnalysisMode
    { Null, Analyse, AnalyseAndCompare }

    internal interface IUserInput
    {
        public string FileName { get; }

        public string? MapFile { get; }

        public bool Success { get; }

        public IList<int> PostCodes { get; }

        public IList<string> Cities { get; }

        //Form: <Street, Number>
        public Tuple<Street, string>?[] StartAndEnd { get; }

        //Form: <StreetId, Street>
        public IDictionary<string, Street> Streets { get; }

        //Form: <Street, IList<Number>>
        public IDictionary<Street, IList<string>> MailAddresses { get; }

        public IList<Tuple<Street, string, bool>> OrderedAddresses { get; } //Tuple<Street, Number, IsStorage>

        //Form: <Street, Number, Max count of storage usages>
        public IList<Tuple<Street, string, int>> Storages { get; }

        public IDictionary<Street, TrafficType> TrafficTypes { get; }

        public int DesiredStorageCount { get; }

        public int SearchDepth { get; }

        public IList<Tuple<Street, string, Street, string>> Cuts { get; }

        public bool DoNotCrossAlleys { get; }

        public bool CompareAnalysis { get; }
    }
}