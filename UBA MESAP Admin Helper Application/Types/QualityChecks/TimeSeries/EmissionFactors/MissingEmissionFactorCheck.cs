using M4DBO;
using System;
using System.Collections.Generic;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    class MissingEmissionFactorCheck : TimeSeriesQualityCheck
    {
        public override string Id => "EF_vollstaendig";

        public override TimeSpan EstimatedExecutionTimePerElement => TimeSpan.FromMilliseconds(35);
        
        protected override short StartYear => 2016;
        protected override short EndYear => 2050;
        
        protected override int[,] FindWorkloadFilter => new int[,]
        {
            {(int)DimensionEnum.Type, (int)DescriptorEnum.EF, (int)DescriptorEnum.Wildcard, (int)DescriptorEnum.Wildcard, (int)DescriptorEnum.Wildcard},
            {(int)DimensionEnum.Area, (int)DescriptorEnum.Germany, (int)DescriptorEnum.Wildcard, (int)DescriptorEnum.Wildcard, (int)DescriptorEnum.Wildcard},
            /*{(int)DimensionEnum.Pollutant, (int)DescriptorEnum.Wildcard, (int)DescriptorEnum.Wildcard, (int)DescriptorEnum.Wildcard, (int)DescriptorEnum.Wildcard}*/
        };
        
        protected override void CheckTimeSeries(TimeSeries series, IProgress<ISet<Finding>> progress)
        {
            series.Object.DbReadRelatedProperties();
            dboTSProperty property = series.Object.TSProperties.GetObject(mspTimeKeyEnum.mspTimeKeyYear, mspTimeKeyTypeEnum.mspTimeKeyTypeUnknown);
            
            if (series.Object.VirtualType != mspVirtualTsTypeEnum.mspVirtualTsTypeVirtual && // skip virtual time series
                (series.RetrieveData(StartYear) == null) && // skip times series with existing value
                ((series.Object.TSDatas.Count == 0 && property.Extrapol == mspExtrapolEnum.mspExtrapolNone) ||
                (series.Object.TSDatas.Count > 0 && property.Interpol == mspInterpolEnum.mspInterpolNone))) // no mapped value
                Report(progress, new TimeSeries[] { series },
                    String.Format(FindingTitle, series.ID, StartYear),
                    String.Format(FindingText, series.Legend));
        }
    }
}
