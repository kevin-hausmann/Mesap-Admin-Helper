using System;
using System.Collections.Generic;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    class EmptyTimeSeriesCheck : TimeSeriesQualityCheck
    {
        public override string Name => "Leere Zeitreihen";
        public override string Description => "Identifiziert Zeitreihen, die keinerlei Werte enthalten";
        public override short DatabaseReference => 118;

        protected override short StartYear => 1900;
        protected override short EndYear => 2100;
        protected override Finding.PriorityEnum DefaultPriority => Finding.PriorityEnum.Low;

        protected override int FindWorkloadOverhead => 0;
        protected override int[,] FindWorkloadFilter => new int[,] {};
        protected override short EstimateExecutionTime() => 75;

        protected override void CheckTimeSeries(TimeSeries series, IProgress<ISet<Finding>> progress)
        {
            // All data we need has already been preloaded for us
            if (series.Object.TSDatas.Count == 0)
                Report(progress, series,
                    String.Format("Zeitreihe {0} ist leer", series.ID),
                    String.Format("Diese Zeitreihe enthält keinerlei Werte [{0}]", series.Legend));
        }
    }
}
