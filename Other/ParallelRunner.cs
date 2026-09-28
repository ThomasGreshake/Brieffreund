//Copyright Thomas Greshake 2026

using System.Runtime.ExceptionServices;

namespace Brieffreund
{
    internal static class ParallelRunner
    {
        private static readonly ParallelOptions OPTIONS = new() { MaxDegreeOfParallelism = Constants.THREAD_COUNT + 1 };

        //Runs body(0) ... body(count - 1) using the calling thread and up to THREAD_COUNT additional threads
        internal static void For(int count, Action<int> body)
        {
            if (Constants.THREAD_COUNT == 0 || count <= 1)
            {
                for (int i = 0; i < count; i++)
                {
                    body(i);
                }
                return;
            }

            try
            {
                Parallel.For(0, count, OPTIONS, body);
            }
            catch (AggregateException e) when (e.InnerExceptions.Count == 1)
            {
                ExceptionDispatchInfo.Capture(e.InnerExceptions[0]).Throw();
            }
        }
    }
}
