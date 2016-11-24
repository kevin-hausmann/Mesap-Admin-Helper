using M4DBO;
using System;
using System.Collections.Generic;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    class InterpolationCheck : TimeSeriesQualityCheck
    {
        public override string Name => "Inter-/Extrapolation";
        public override string Description => "Prüft die Inter- und Extrapolationseinstellungen von Zeitreihen";
        public override short DatabaseReference => -1;

        public override TimeSpan EstimatedExecutionTimePerElement => TimeSpan.FromMilliseconds(50);
        
        protected override short StartYear => -1;
        protected override short EndYear => -1;
        protected override Finding.PriorityEnum DefaultPriority => Finding.PriorityEnum.Medium;

        protected override int[,] FindWorkloadFilter => new int[,]
        {
            {(int)DimensionEnum.Type, (int)DescriptorEnum.EF},
        };
        
        protected override void CheckTimeSeries(TimeSeries series, IProgress<ISet<Finding>> progress)
        {
            series.Object.DbReadRelatedProperties();
            dboTSProperty property = series.Object.TSProperties.GetObject(mspTimeKeyEnum.mspTimeKeyYear, mspTimeKeyTypeEnum.mspTimeKeyTypeUnknown);
            if ((int)property.Interpol > 1)
                Report(progress, series,
                    String.Format("EF-ZR \"{0}\" hat ungewöhnliche Interpolation {1}", series.ID, property.Interpol),
                    String.Format("[{0}]", series.Legend));
        }
    }
}
