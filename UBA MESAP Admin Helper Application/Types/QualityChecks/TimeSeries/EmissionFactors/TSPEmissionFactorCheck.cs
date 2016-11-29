using M4DBO;
using System;
using System.Collections.Generic;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    class TSPEmissionFactorCheck : TimeSeriesQualityCheck
    {
        public override string Name => "Plausibilität der Verhältnisse einzelner Staubfraktionen und BC";
        public override string Description => "Vergleicht passende Emissionsfaktoren für Stäube und stellt sicher, dass die Fraktionen nicht zu groß sind.";
        public override short DatabaseReference => 114;

        public override TimeSpan EstimatedExecutionTimePerElement => TimeSpan.FromMilliseconds(100);
        
        protected override short StartYear => 1990;
        protected override short EndYear => 2020;
        protected override Finding.PriorityEnum DefaultPriority => Finding.PriorityEnum.High;

        protected override int[,] FindWorkloadFilter => new int[,]
        {
            {(int)DimensionEnum.Type, (int)DescriptorEnum.EF},
            {(int)DimensionEnum.Pollutant, (int)DescriptorEnum.TSP}
        };
        
        protected override void CheckTimeSeries(TimeSeries tspSeries, IProgress<ISet<Finding>> progress)
        {
            TimeSeries pm10Series = FindMatchingSeries(tspSeries, (int)DimensionEnum.Pollutant, (int)DescriptorEnum.PM10);

            Console.WriteLine("TSP: " + tspSeries.ID);
            Console.WriteLine("PM10: " + pm10Series.ID);

            for (int year = StartYear; year <= EndYear; year++)
            {
                Console.WriteLine(String.Format("TSP {0} {1}", year, tspSeries.RetrieveData(year).GetValue()));
                Console.WriteLine(String.Format("PM10 {0} {1}", year, pm10Series.RetrieveData(year).GetValue()));
            }
        }

        private TimeSeries FindMatchingSeries(TimeSeries series, int dimNr, int descriptor)
        {
            dboTSFilter filter = series.Object.Database.CreateObject_TSFilter();
            filter.BuildFromTsNumbers(series.Object.TsNr.ToString());
            filter.FilterUsage = mspFilterUsageEnum.mspFilterUsageFilterOnly;
            filter.Item(dimNr).Numbers = descriptor.ToString();

            dboList list = new dboList();
            list.FromString(filter.GetTSNumbers(), VBA.VbVarType.vbLong);
            return new TimeSeries(MesapAPIHelper.GetTimeSeries(Int32.Parse(list.ToString())), StartYear, EndYear);
        }
    }
}
