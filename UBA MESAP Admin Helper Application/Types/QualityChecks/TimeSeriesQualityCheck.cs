using M4DBO;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    /// <summary>
    /// Abstract sub-type of quality check. Use this if your check
    /// goes through a list of time series and checks some property
    /// for each of them. For more flexibility, sub-class the QualityCheck
    /// type itself.
    /// </summary>
    public abstract class TimeSeriesQualityCheck : QualityCheck
    {
        /// <summary>
        /// The first year (e.g. 1990) data points will be loaded for. Give any
        /// value less than 0 to prevent any data from being pre-loaded.
        /// </summary>
        protected abstract short StartYear { get; }
        /// <summary>
        /// The latest year data points will be loaded for.
        /// </summary>
        protected abstract short EndYear { get; }

        /// <summary>
        /// Priority values used for findings created by Report().
        /// </summary>
        protected abstract Finding.PriorityEnum DefaultPriority { get; }

        /// <summary>
        /// Timespan in ms need to triage a time series in FindWorkload().
        /// This will be used to estimate execution times.
        /// </summary>
        protected abstract int FindWorkloadOverhead { get; }

        /// <summary>
        /// Filter definition for the task, CheckTimeSeries() will be called
        /// for matching series only. Format:
        /// [[dimension1, descriptor1, ..., descriptorX], [dimension1, descriptor1, ..., descriptorX], ...]
        /// Matching series have one or more descriptors set for each dimension listed.
        /// Use -1 as wildcard, an empty filter matches all series.
        /// </summary>
        protected abstract int[,] FindWorkloadFilter { get; }
        protected enum DimensionEnum
        {
            Type = 1,
            Area = 2,
            Pollutant = 4
        }
        protected enum DescriptorEnum
        {
            Wildcard = 0,
            AD = 50001,
            EF = 50003,
            EM = 50004,
            ABL = 1001,
            NBL = 1002,
            Germany = 1003,
            TSP = 3031,
        }

        public sealed override Task<int> EstimateExecutionTimeAsync(Filter filter, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                return FindWorkloadOverhead > 0 ?
                    filter.Count * FindWorkloadOverhead + FindWorkload(filter, false).Count * EstimateExecutionTime() :
                    filter.Count * EstimateExecutionTime();
            }, cancellationToken);
        }

        public sealed override Task RunAsync(Filter filter, CancellationToken cancellationToken, IProgress<ISet<Finding>> progress)
        {
            return Task.Run(() =>
            {
                Completion = 0;
                ISet<int> workload = FindWorkload(filter, true);
                Completion = 50;

                int total = workload.Count;
                int count = 0;

                foreach (int seriesId in workload)
                {
                    if (StartYear >= 0 && StartYear <= EndYear)
                        CheckTimeSeries(new TimeSeries(MesapAPIHelper.GetTimeSeries(seriesId), StartYear, EndYear), progress);
                    else
                        CheckTimeSeries(new TimeSeries(MesapAPIHelper.GetTimeSeries(seriesId)), progress);

                    cancellationToken.ThrowIfCancellationRequested();
                    Completion = 50 + (int)(++count / (float)total * 100) / 2;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Check given time series and report findings, if any.
        /// </summary>
        /// <param name="series">Time series to check. Will have related keys and data
        /// between StartYear and EndYear preloaded</param>
        /// <param name="progress">Handle to report findings with</param>
        protected abstract void CheckTimeSeries(TimeSeries series, IProgress<ISet<Finding>> progress);

        /// <summary>
        /// Convenience to report a finding with common values. Uses the checks DefaultPriority and
        /// will read contact and category from time series object given.
        /// </summary>
        /// <param name="progress">Progress handle to report to</param>
        /// <param name="series">Time series checked</param>
        /// <param name="title">The finding's title</param>
        /// <param name="description">The finding's description</param>
        protected void Report(IProgress<ISet<Finding>> progress, TimeSeries series, string title, string description)
        {
            ISet<Finding> result = new HashSet<Finding>();
            result.Add(new Finding(this, series.Object.TsNr,
                title, description, CategoriesForTimeSeries(series), ContactsForTimeSeries(series), DefaultPriority));

            progress.Report(result);
        }

        private ISet<int> FindWorkload(Filter filter, bool updateCompletion)
        {
            ISet<int> result = new HashSet<int>();
            dboList list = TSNrListFromFilter(filter);
            int count = 0;

            foreach (int number in list)
            {
                if (FindWorkloadFilter.Length == 0 || MatchesWorkloadFilter(number))
                    result.Add(number);

                if (updateCompletion)
                    Completion = (int)(++count / (float)list.Count * 100) / 2;
            }

            return result;
        }

        private bool MatchesWorkloadFilter(int number)
        {
            dboTS timeSeries = MesapAPIHelper.GetTimeSeries(number);
            timeSeries.DbReadRelatedKeys();
            dboTSKeys keys = timeSeries.TSKeys;

            for (int i = 0; i < FindWorkloadFilter.GetLength(0); i++)
            {
                dboCollection descriptors = keys.GetCollection(0, FindWorkloadFilter[i, 0]);

                bool found = false;
                for (int j = 1; j < FindWorkloadFilter.GetLength(1); j++)
                    foreach (dboTSKey key in descriptors)
                        if (key.ObjNr == FindWorkloadFilter[i, j])
                            found = true;
                
                if (!found)
                    return false;
            }               

            return true;
        }
    }
}
