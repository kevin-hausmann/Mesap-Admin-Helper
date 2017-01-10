using M4DBO;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    /// <summary>
    /// Abstract sub-type of quality check. Use this if your check goes through
    /// a list of time series and checks some property for each of them.
    /// For more flexibility, sub-class the QualityCheck type itself.
    /// </summary>
    public abstract class TimeSeriesQualityCheck : QualityCheck
    {
        /// <summary>
        /// The first year (e.g. 1990) data points will be loaded for. Give any
        /// value less than 0 to prevent any data from being preloaded.
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
        /// Filter definition for the task, CheckTimeSeries() will be called
        /// for matching series only. Format:
        /// [[dimension1, descriptor1, ..., descriptorX], [dimension1, descriptor1, ..., descriptorX], ...]
        /// Matching series have one or more descriptors set for each dimension listed.
        /// Use 0 as wild card, an empty filter matches all series.
        /// </summary>
        protected abstract int[,] FindWorkloadFilter { get; }
        protected enum DimensionEnum
        {
            Type = 1,
            Area = 2,
            Pollutant = 4,
            Category = 13
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
            PM10 = 50471,
            PM2_5 = 50470,
            BC = 52926
        }

        public sealed override Task EstimateExecutionTimeAsync(Filter filter, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                ElementCount = FindWorkload(filter, cancellationToken).Count;
                EstimatedExecutionTime = TimeSpan.FromMilliseconds(ElementCount * EstimatedExecutionTimePerElement.TotalMilliseconds);
            }, cancellationToken);
        }

        public sealed override Task RunAsync(Filter filter, CancellationToken cancellationToken, IProgress<ISet<Finding>> progress)
        {
            return Task.Run(() =>
            {
                Reset();
                Running = true;
                RemainingExecutionTime = EstimatedExecutionTime;
                DateTime start = DateTime.Now;

                ISet<int> workload = FindWorkload(filter, cancellationToken);
                ElementCount = workload.Count;
                cancellationToken.ThrowIfCancellationRequested();

                int total = workload.Count;
                int count = 0;

                DateTime startSeries = DateTime.Now;
                foreach (int seriesId in workload)
                {
                    if (StartYear >= 0 && StartYear <= EndYear)
                        CheckTimeSeries(new TimeSeries(MesapAPIHelper.GetTimeSeries(seriesId), StartYear, EndYear), progress);
                    else
                        CheckTimeSeries(new TimeSeries(MesapAPIHelper.GetTimeSeries(seriesId)), progress);

                    cancellationToken.ThrowIfCancellationRequested();
                    ElementProcessedCount++;
                    Completion = (int)(++count / (float)total * 100);
                    MeasuredExecutionTimePerElement = TimeSpan.FromTicks(DateTime.Now.Subtract(startSeries).Ticks / ElementProcessedCount);
                    RemainingExecutionTime = TimeSpan.FromMilliseconds(MeasuredExecutionTimePerElement.TotalMilliseconds * (total - count));
                }

                Completion = 100;
                Running = false;
                Completed = true;
                MeasuredExecutionTime = DateTime.Now.Subtract(start);
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
        /// will read contact and category from time series object given. Advances FindingCount.
        /// </summary>
        /// <param name="progress">Progress handle to report to</param>
        /// <param name="series">Time series checked. Put main series in first place.</param>
        /// <param name="title">The finding's title</param>
        /// <param name="description">The finding's description</param>
        protected void Report(IProgress<ISet<Finding>> progress, TimeSeries[] series, string title, string description)
        {
            ISet<Finding> result = new HashSet<Finding>();
            result.Add(new Finding(this, series.Select(serie => serie.Object.TsNr).ToArray(),
                title, description, CategoriesForTimeSeries(series[0]), ContactsForTimeSeries(series[0]), DefaultPriority));

            progress.Report(result);
            FindingCount++;
        }

        /// <summary>
        /// Find source category information for given time series.
        /// Assumes the series' related key have already been read.
        /// </summary>
        /// <param name="series">The time series to inspect.</param>
        /// <returns>A number of category objects (likely 1) or an empty set,
        /// if no descriptor is set. Never null.</returns>
        protected ISet<Finding.Category> CategoriesForTimeSeries(TimeSeries series)
        {
            HashSet<Finding.Category> result = new HashSet<Finding.Category>();

            foreach (dboTSKey key in series.Object.TSKeys)
                if (key.DimNr == (int)DimensionEnum.Category)
                {
                    dboTreeObject descriptor = series.Object.Database.TreeObjects[key.ObjNr];
                    if (descriptor != null)
                        result.Add(new Finding.Category(descriptor.Name, descriptor.ObjNr));
                }

            return result;
        }

        protected ISet<Finding.ContactEnum> ContactsForTimeSeries(TimeSeries series)
        {
            HashSet<Finding.ContactEnum> result = new HashSet<Finding.ContactEnum>();

            // Get documentation for this time series
            dboAnnexObjects objects = series.Object.Database.CreateObject_AnnexObjects();
            objects.DbReadByReference_Docu(mspDocuTypeEnum.mspDocuTypeTS, series.Object.TsNr);
            dboAnnexObject annexObject = objects.GetObject_Docu(series.Object.TsNr, mspDocuTypeEnum.mspDocuTypeTS, mspTimeKeyEnum.mspTimeKeyYear, 0, 0, 0);

            if (annexObject != null)
            {
                // Get all documentation components for the time series
                dboAnnexSetLinks links = series.Object.Database.CreateObject_AnnexSetLinks();
                links.DbReadByReference(annexObject.AnnexObjNr, mspAnnexTypeEnum.mspAnnexTypeDocu);

                // Find contact information
                foreach (dboAnnexSetLink link in links)
                    if (link.ComponentNr == 12)
                    {
                        dboAnnexItemDatas datas = series.Object.Database.CreateObject_AnnexItemDatas();
                        datas.DbReadByItemNr(link.AnnexSetNr, 12, 63, false, 0);

                        IEnumerator dataEnum = datas.GetEnumerator();
                        if (dataEnum.MoveNext())
                            result.Add(FindContactForAnnexItemPoolReference((dataEnum.Current as dboAnnexItemData).ReferenceData));
                    }
            }

            if (result.Count == 0)
                result.Add(Finding.ContactEnum.NN);

            return result;
        }

        private Finding.ContactEnum FindContactForAnnexItemPoolReference(int referenceData)
        {
            switch (referenceData)
            {
                case 154: return Finding.ContactEnum.Schiller;
                case 155: return Finding.ContactEnum.Rimkus;
                case 232: return Finding.ContactEnum.Boettcher;
                case 270: return Finding.ContactEnum.Kludt;
                case 278: return Finding.ContactEnum.Kotzulla;
                case 294: return Finding.ContactEnum.Juhrich;
                case 508: return Finding.ContactEnum.Kuntze;
                case 556: return Finding.ContactEnum.Hausmann;
                case 624: return Finding.ContactEnum.Doering;
                case 743: return Finding.ContactEnum.Reichel;
                default: return Finding.ContactEnum.NN;
            }
        }

        /// <summary>
        /// Scan time series filter for series that match the quality check's workload filter definition.
        /// </summary>
        /// <param name="filter">Filter selected by user to run quality check on.</param>
        /// <param name="cancellationToken">Token for canceling the search.</param>
        /// <returns>Set of IDs of time series that are in the user's filter
        ///     and also match the quality check's workload filter.</returns>
        private ISet<int> FindWorkload(Filter filter, CancellationToken cancellationToken)
        {
            ISet<int> result = new HashSet<int>();
            dboTSFilter workload = null;

            if (FindWorkloadFilter.GetLength(0) > 0)
            {
                // Create new filter on the fly and let Mesap do all the work
                workload = filter.Object.Database.CreateObject_TSFilter();
                workload.FilterUsage = mspFilterUsageEnum.mspFilterUsageFilterAndList;
                workload.TsList = filter.Object.GetTSNumbers();
                cancellationToken.ThrowIfCancellationRequested();

                for (int i = 0; i < FindWorkloadFilter.GetLength(0); i++)
                {
                    dboTreeObjectFilter descriptorFilter = workload.Database.CreateObject_TreeObjectFilter();
                    descriptorFilter.DimNr = FindWorkloadFilter[i, 0];

                    for (int j = 1; j < FindWorkloadFilter.GetLength(1); j++)
                        if (FindWorkloadFilter[i, j] > 0)
                            descriptorFilter.Numbers = (j == 1 ? FindWorkloadFilter[i, j].ToString() :
                                descriptorFilter.Numbers + "," + FindWorkloadFilter[i, j]);

                    workload.Add(descriptorFilter);
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            else
            {
                // The quality check does not define a workload filter, short-circuit
                workload = filter.Object;
            }

            // Use workload filter to find all the time series we need to process
            dboList list = new dboList();
            list.FromString(workload.GetTSNumbers(), VBA.VbVarType.vbLong);
            foreach (int number in list)
                result.Add(number);
            
            return result;
        }
    }
}
