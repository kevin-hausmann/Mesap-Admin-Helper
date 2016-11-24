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
        
        protected override void CheckTimeSeries(TimeSeries series, IProgress<ISet<Finding>> progress)
        {
            // Check matching PM10, PM2.5 and BC EFs
        }
    }
}
