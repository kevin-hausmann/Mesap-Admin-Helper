using M4DBO;
using System;
using System.Collections.Generic;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    class TSPEmissionFactorCheck : TimeSeriesQualityCheck
    {
        public override string Id => "EF_Staubfraktionen";

        public override TimeSpan EstimatedExecutionTimePerElement => TimeSpan.FromMilliseconds(4000);
        
        protected override short StartYear => 1990;
        protected override short EndYear => 2020;
        
        /// <summary>
        /// Number of decimal places checked when comparing values.
        /// </summary>
        private const short Precision = 6;

        protected override int[,] FindWorkloadFilter => new int[,]
        {
            {(int)DimensionEnum.Type, (int)DescriptorEnum.EM},
            {(int)DimensionEnum.Pollutant, (int)DescriptorEnum.TSP}
        };
        
        protected override void CheckTimeSeries(TimeSeries tspSeries, IProgress<ISet<Finding>> progress)
        {
            TimeSeries upperBound = tspSeries;
            TimeSeries pm10Series = FindMatchingSeries(tspSeries, (int)DimensionEnum.Pollutant, (int)DescriptorEnum.PM10);

            if (pm10Series != null)
            {
                AssertAllValuesEqualOrGreater(upperBound, pm10Series, progress);
                upperBound = pm10Series;
            }

            TimeSeries pm25Series = FindMatchingSeries(tspSeries, (int)DimensionEnum.Pollutant, (int)DescriptorEnum.PM2_5);

            if (pm25Series != null)
            {
                AssertAllValuesEqualOrGreater(upperBound, pm25Series, progress);
                upperBound = pm25Series;
            }

            TimeSeries bcSeries = FindMatchingSeries(tspSeries, (int)DimensionEnum.Pollutant, (int)DescriptorEnum.BC);

            if (bcSeries != null)
                AssertAllValuesEqualOrGreater(upperBound, bcSeries, progress);
        }

        /// <summary>
        /// Find time series in the database with the same descriptor key except for the
        /// given descriptor switched out.
        /// </summary>
        /// <param name="series">Series to find sibling for</param>
        /// <param name="dimNr">Dimension of descriptor to swap</param>
        /// <param name="descriptor">Descriptor to swap in</param>
        /// <returns>The time series object or null, if none or multiple series are found</returns>
        private TimeSeries FindMatchingSeries(TimeSeries series, int dimNr, int descriptor)
        {
            dboTSFilter filter = series.Object.Database.CreateObject_TSFilter();
            filter.BuildFromTsNumbers(series.Object.TsNr.ToString());
            filter.FilterUsage = mspFilterUsageEnum.mspFilterUsageFilterOnly;
            filter.Item(dimNr).Numbers = descriptor.ToString();

            dboList list = new dboList();
            list.FromString(filter.GetTSNumbers(), VBA.VbVarType.vbLong);

            if (list.Count == 1)
                return new TimeSeries(MesapAPIHelper.GetTimeSeries(Int32.Parse(list.ToString())), StartYear, EndYear);
            else
            {
                Console.WriteLine("Found {0} series as siblings of {1}, replacing {2} with {3}",
                    list.Count, series.Legend, Enum.GetName(typeof(DimensionEnum), dimNr), Enum.GetName(typeof(DescriptorEnum), descriptor));
                return null;
            }
        }

        private void AssertAllValuesEqualOrGreater(TimeSeries upper, TimeSeries series, IProgress<ISet<Finding>> progress)
        {
            for (int year = StartYear; year <= EndYear; year++)
                try
                {
                    DataValue upperValueObject = upper.RetrieveData(year);
                    DataValue seriesValueObject = series.RetrieveData(year);

                    if (upperValueObject != null && seriesValueObject != null &&
                        // Rounding needed to avoid number conversion artifacts
                        Math.Round(upperValueObject.Object.Value - seriesValueObject.Object.Value, Precision) < 0)
                            Report(progress, new TimeSeries[] { series, upper },
                            String.Format(FindingTitle, year),
                            String.Format(FindingText, year, series.Legend, upper.Legend, upperValueObject.Object.Value, seriesValueObject.Object.Value));
                    
                }
                catch (Exception)
                {
                    // continue
                }                
        }
    }
}
