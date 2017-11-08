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
        
        protected override int[,] FindWorkloadFilter => new int[,] {};
        
        protected override void CheckTimeSeries(TimeSeries series, IProgress<ISet<Finding>> progress)
        {
            int type = 0;
            int area = 0;
            foreach (dboTSKey key in series.Object.TSKeys)
                if (key.DimNr == (int)DimensionEnum.Type)
                    type = key.ObjNr;
                else if (key.DimNr == (int)DimensionEnum.Area && area == 0)
                    area = key.ObjNr;

            series.Object.DbReadRelatedProperties();
            dboTSProperty property = series.Object.TSProperties.GetObject(mspTimeKeyEnum.mspTimeKeyYear, mspTimeKeyTypeEnum.mspTimeKeyTypeUnknown);

            if (type == (int)DescriptorEnum.AD || type == (int)DescriptorEnum.EM)
            {
                if (property.Interpol > mspInterpolEnum.mspInterpolNone || property.Extrapol > mspExtrapolEnum.mspExtrapolNone)
                    Report(progress, new TimeSeries[] { series },
                        String.Format(FindingTitle, type == (int)DescriptorEnum.AD ? "AR-" : "EM-", series.ID, property.Interpol + ", " + property.Extrapol),
                        String.Format(FindingText, series.Legend)); 
            }
            else 
            {
                if (property.Interpol > mspInterpolEnum.mspInterpolLinear)
                    Report(progress, new TimeSeries[] { series },
                        String.Format(FindingTitle, "Nicht-AR/EM-", series.ID, property.Interpol + ", " + property.Extrapol),
                        String.Format(FindingText, series.Legend));
            }
        }
    }
}
