using System;
using System.Collections.Generic;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    class ActitivityDataCheck : TimeSeriesQualityCheck
    {
        public override string Id => "AR_aktuell_fehlt";

        public override TimeSpan EstimatedExecutionTimePerElement => TimeSpan.FromMilliseconds(75);
        
        protected override short StartYear => 2015;
        protected override short EndYear => 2016;
        
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
            
            if ((currentValue == null || !currentValue.IsActualValue()) &&
                (previousValue != null && previousValue.IsActualValue()))
                Report(progress, new TimeSeries[] { series }, 
                    String.Format(FindingTitle, series.ID, EndYear),
                    String.Format(FindingText, EndYear, series.Legend));
        }
    }
}
