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

        protected override int FindWorkloadOverhead => 0;
        protected override short EstimateExecutionTime() => 75;

        protected override void CheckTimeSeries(TimeSeries timeSeries, IProgress<ISet<Finding>> progress)
        {
            // All data we need has already been preloaded for us
            if (timeSeries.Object.TSDatas.Count == 0)
            {
                ISet<Finding> result = new HashSet<Finding>();
                result.Add(new Finding(this, timeSeries.Object.TsNr,
                    String.Format("Zeitreihe {0} ist leer", timeSeries.ID),
                    String.Format("Diese Zeitreihe enthält keinerlei Werte [{0}]", timeSeries.Legend),
                    CategoriesForTimeSeries(timeSeries),
                    ContactsForTimeSeries(timeSeries),
                    Finding.PriorityEnum.Low));

                progress.Report(result);
            }
        }
    }
}
