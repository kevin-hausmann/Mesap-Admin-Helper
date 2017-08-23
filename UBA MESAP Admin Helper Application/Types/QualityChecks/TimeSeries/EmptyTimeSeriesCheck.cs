using System;
using System.Collections.Generic;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    class EmptyTimeSeriesCheck : TimeSeriesQualityCheck
    {
        public override string Id => "Leere_Zeitreihen";

        public override TimeSpan EstimatedExecutionTimePerElement => TimeSpan.FromMilliseconds(75);
        
        protected override short StartYear => 1900;
        protected override short EndYear => 2100;
        
        protected override int[,] FindWorkloadFilter => new int[,] {};
        
        protected override void CheckTimeSeries(TimeSeries series, IProgress<ISet<Finding>> progress)
        {
            // All data we need has already been preloaded for us
            if (series.Object.TSDatas.Count == 0)
                Report(progress, new TimeSeries[] { series },
                    String.Format(FindingTitle, series.ID),
                    String.Format(FindingText, series.Legend));
        }
    }
}
