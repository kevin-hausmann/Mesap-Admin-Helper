using M4DBO;
using System;
using System.Collections.Generic;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    class MissingEmissionFactor : TimeSeriesQualityCheck
    {
        public override string Name => "Vollständigkeit EF";
        public override string Description => "Die Emissionsfaktoren werden auf Vollständigkeit geprüft. In jeder Zeitreihe sollte entweder ein Wert stehen oder ein Interpolation/Extrapolation hinterlegt sein. In Ausnahmefällen kann auch mal ein Notation Key stehen.";
        public override short DatabaseReference => 117;

        protected override short StartYear => 2014;
        protected override short EndYear => 2015;

        protected override int FindWorkloadOverhead => 20;
        protected override short EstimateExecutionTime() => 10;

        protected override void CheckTimeSeries(TimeSeries timeSeries, IProgress<ISet<Finding>> progress)
        {
            timeSeries.Object.DbReadRelatedProperties(mspTimeKeyEnum.mspTimeKeyYear, mspTimeKeyTypeEnum.mspTimeKeyTypeUnknown);
            bool extra = timeSeries.Object.TSProperties.GetObject(mspTimeKeyEnum.mspTimeKeyYear, mspTimeKeyTypeEnum.mspTimeKeyTypeUnknown).Extrapol != mspExtrapolEnum.mspExtrapolNone;
            if (timeSeries != null && timeSeries.Object.TSDatas.Count == 0 && !extra)
            {
                ISet<Finding> result = new HashSet<Finding>();
                result.Add(new Finding(this, timeSeries.Object.TsNr,
                    "Emissionsfaktor fehlt: " + timeSeries.ID + ", " + EndYear,
                    "Kein Emissionsfaktor vorhanden oder gemappt" + " (" + timeSeries.Legend + ")",
                    CategoriesForTimeSeries(timeSeries),
                    ContactsForTimeSeries(timeSeries),
                    Finding.PriorityEnum.Medium));

                progress.Report(result);
            }
        }

        protected override ISet<int> FindWorkload(Filter filter, bool updateCompletion)
        {
            ISet<int> result = new HashSet<int>();
            dboList list = TSNrListFromFilter(filter);
            int count = 0;

            foreach (int number in list)
            {
                bool isEF = false;
                bool isGermany = false;
                bool isCorrectPollutant = false;

                dboTS timeSeries = MesapAPIHelper.GetTimeSeries(number);
                timeSeries.DbReadRelatedKeys();
                dboTSKeys keys = timeSeries.TSKeys;
                foreach (dboTSKey key in keys)
                {
                    if (key.DimNr == 1 && key.ObjNr == 50003) isEF = true;
                    else if (key.DimNr == 2 && key.ObjNr == 1003) isGermany = true;
                    else if (key.DimNr == 4 && (key.ObjNr == 3001 | key.ObjNr == 3002 | key.ObjNr == 3005 | key.ObjNr == 3031)) isCorrectPollutant = true;
                }

                if (isEF && isGermany && isCorrectPollutant)
                    result.Add(number);

                if (updateCompletion)
                    Completion = (int)(++count / (float)list.Count * 100) / 2;
            }

            return result;
        }
    }
}
