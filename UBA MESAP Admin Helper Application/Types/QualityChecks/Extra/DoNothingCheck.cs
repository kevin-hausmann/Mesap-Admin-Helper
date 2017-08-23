using System;
using System.Collections.Generic;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    class DoNothingCheck : TimeSeriesQualityCheck
    {
        public override string Id => "Faulpelz";

        public override TimeSpan EstimatedExecutionTimePerElement => TimeSpan.FromMilliseconds(10);
        
        protected override short StartYear => -1;
        protected override short EndYear => -1;
        
        protected override int[,] FindWorkloadFilter => new int[,] {};
        
        protected override void CheckTimeSeries(TimeSeries series, IProgress<ISet<Finding>> progress)
        {
            // Do nothing!
        }
    }
}
