using System;
using System.Collections.Generic;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    class CopyPasteMesapCheck : TimeSeriesQualityCheck
    {
        public override string Id => "CopyPaste Problem Mesap";

        public override TimeSpan EstimatedExecutionTimePerElement => TimeSpan.FromMilliseconds(50);
        
        protected override short StartYear => 1990;
        protected override short EndYear => 2020;
        
        protected override int[,] FindWorkloadFilter => new int[,] {};
        
        protected override void CheckTimeSeries(TimeSeries series, IProgress<ISet<Finding>> progress)
        {
            int minLength = 0;
            int maxLength = 0;
            for (int year = StartYear; year < EndYear; year++)
            {
                DataValue value = series.RetrieveData(year);
                if (value != null && value.IsNumericValue())
                {
                    double raw = value.GetValue();
                    int length = (raw + "").Length;

                    if (minLength == 0 || length < minLength) minLength = length;
                    if (maxLength == 0 || length > maxLength) maxLength = length;
                }   
            }
            
            if (maxLength - minLength > 3)
                Report(progress, new TimeSeries[] { series },
                        String.Format(FindingTitle, series.ID),
                        String.Format(FindingText, series.Legend));
        }
    }
}
