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
        /// Timespan in ms need to triage a time series in FindWorkload().
        /// This will be used to estimate execution times.
        /// </summary>
        protected abstract int FindWorkloadOverhead { get; }

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
        /// Use the filter given to determine which time series will be checked.
        /// This default implementation returns all time series, sub-classes can
        /// override this behaviour as needed.
        /// </summary>
        /// <param name="filter">Filter to check for eligible time series</param>
        /// <param name="updateCompletion">Whether the method should update the
        /// Completion property while active. If true, make sure to advance Completion
        /// from 0 to 50 while running.</param>
        /// <returns>The list of time series numbers the check should run on.
        /// The list will be processed by RunAsync and each individual series will
        /// be passed to CheckTimeSeries.</returns>
        protected virtual ISet<int> FindWorkload(Filter filter, bool updateCompletion)
        {
            ISet<int> result = new HashSet<int>();
            dboList list = TSNrListFromFilter(filter);
            int count = 0;

            foreach (int number in list)
            {
                result.Add(number);

                if (updateCompletion)
                    Completion = (int)(++count / (float)list.Count * 100) / 2;
            }
            
            return result;
        }
    }
}
