using M4DBO;
using System;
using System.Collections.Generic;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    class MissingEmissionFactorCheck : TimeSeriesQualityCheck
    {
        public override string Name => "Vollständigkeit EF";
        public override string Description => "Die Emissionsfaktoren werden auf Vollständigkeit geprüft. In jeder Zeitreihe sollte entweder ein Wert stehen oder ein Interpolation/Extrapolation hinterlegt sein. In Ausnahmefällen kann auch mal ein Notation Key stehen.";
        public override short DatabaseReference => 117;

        public override TimeSpan EstimatedExecutionTimePerElement => TimeSpan.FromMilliseconds(10);
        
        protected override short StartYear => 2015;
        protected override short EndYear => 2015;
        protected override Finding.PriorityEnum DefaultPriority => Finding.PriorityEnum.Medium;

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
            if (series.Object.TSDatas.Count == 0 && property.Extrapol == mspExtrapolEnum.mspExtrapolNone)
                Report(progress, series,
                    String.Format("Emissionsfaktor fehlt: {0}, {1}", series.ID, EndYear),
                    String.Format("Kein Emissionsfaktor vorhanden oder gemappt [{0}]", series.Legend));
        }
    }
}
