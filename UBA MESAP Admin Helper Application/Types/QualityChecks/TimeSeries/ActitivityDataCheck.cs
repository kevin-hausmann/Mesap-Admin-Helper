using System;
using System.Collections.Generic;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    class ActitivityDataCheck : TimeSeriesQualityCheck
    {
        public override string Name => "AR der aktuellen Berichtsrunde fehlt";
        public override string Description => "Untersucht alle AR für das aktuelle Berichtjahr und erzeugt einen Eintrag, wenn der Wert leer ist und im vorigen Jahr nicht leer war.";
        public override short DatabaseReference => 119;

        protected override short StartYear => 2014;
        protected override short EndYear => 2015;

        protected override int FindWorkloadOverhead => 20;
        protected override short EstimateExecutionTime() => 42;

        protected override void CheckTimeSeries(TimeSeries timeSeries, IProgress<ISet<Finding>> progress)
        {
            // Check activity!
        }
    }
}
