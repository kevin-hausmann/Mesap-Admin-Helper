using M4DBO;
using System;
using System.Collections.Generic;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    class InterpolationCheck : TimeSeriesQualityCheck
    {
        public override string Id => "Interpolation";

        public override TimeSpan EstimatedExecutionTimePerElement => TimeSpan.FromMilliseconds(50);
        
        protected override short StartYear => -1;
        protected override short EndYear => -1;
        
        protected override int[,] FindWorkloadFilter => new int[,]
        {
            {(int)DimensionEnum.Type, (int)DescriptorEnum.EF},
        };
        
        protected override void CheckTimeSeries(TimeSeries series, IProgress<ISet<Finding>> progress)
        {
            series.Object.DbReadRelatedProperties();
            dboTSProperty property = series.Object.TSProperties.GetObject(mspTimeKeyEnum.mspTimeKeyYear, mspTimeKeyTypeEnum.mspTimeKeyTypeUnknown);
            if ((int)property.Interpol > 1)
                Report(progress, new TimeSeries[] { series },
                    String.Format(FindingTitle, series.ID, property.Interpol),
                    String.Format(FindingText, series.Legend));
        }
    }
}
