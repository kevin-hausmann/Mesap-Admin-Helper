using System;
using System.Collections.Generic;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    class ActitivityDataCheck : TimeSeriesQualityCheck
    {
        public override string Name => "AR der aktuellen Berichtsrunde fehlt";
        public override string Description => "Untersucht alle AR für das aktuelle Berichtjahr und erzeugt einen Eintrag, wenn der Wert leer ist und im vorigen Jahr nicht leer war.";
        public override short DatabaseReference => 119;

        public override TimeSpan EstimatedExecutionTimePerElement => TimeSpan.FromMilliseconds(75);
        
        protected override short StartYear => 2014;
        protected override short EndYear => 2015;
        protected override Finding.PriorityEnum DefaultPriority => Finding.PriorityEnum.High;

        protected override int[,] FindWorkloadFilter => new int[,]
        {
            {(int)DimensionEnum.Type, (int)DescriptorEnum.AD},
            {(int)DimensionEnum.Area, (int)DescriptorEnum.Germany}
        };
        
        protected override void CheckTimeSeries(TimeSeries series, IProgress<ISet<Finding>> progress)
        {
            // Check activity!
            DataValue currentValue = series.RetrieveData(EndYear);
            DataValue previousValue = series.RetrieveData(StartYear);
            
            if (currentValue == null && previousValue != null)
                Report(progress, new TimeSeries[] { series }, 
                    String.Format("AR in ZR \"{0}\" ohne Wert für {1}", series.ID, EndYear),
                    String.Format("Diese Aktivitätsrate hat keinen Wert für {0}, aber für das Vorjahr! [{1}]", EndYear, series.Legend));
        }
    }
}
