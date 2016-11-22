using System;
using System.Collections.Generic;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    class DoNothingCheck : TimeSeriesQualityCheck
    {
        public override string Name => "Faulpelz";
        public override string Description => "Tut gar nix nimmer.";
        public override short DatabaseReference => -1;

        protected override short StartYear => -1;
        protected override short EndYear => -1;
        protected override Finding.PriorityEnum DefaultPriority => Finding.PriorityEnum.Medium;

        protected override int FindWorkloadOverhead => 0;
        protected override int[,] FindWorkloadFilter => new int[,] {};
        protected override short EstimateExecutionTime() => 15;

        protected override void CheckTimeSeries(TimeSeries series, IProgress<ISet<Finding>> progress)
        {
            // Do nothing!
        }
    }
}
