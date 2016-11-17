using System;
using System.Collections.Generic;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    class TSPEmissionFactorCheck : TimeSeriesQualityCheck
    {
        public override string Name => "Plausibilität der Verhältnisse einzelner Staubfraktionen und BC";
        public override string Description => "Vergleicht passende Emissionsfaktoren für Stäube und stellt sicher, dass die Fraktionen nicht zu groß sind.";
        public override short DatabaseReference => 114;

        protected override short StartYear => 1990;
        protected override short EndYear => 2020;

        protected override int FindWorkloadOverhead => 10;
        protected override short EstimateExecutionTime() => 100;

        protected override void CheckTimeSeries(TimeSeries timeSeries, IProgress<ISet<Finding>> progress)
        {
            // Check matching PM10, PM2.5 and BC EFs
        }

        protected override ISet<int> FindWorkload(Filter filter, bool updateCompletion)
        {
            ISet<int> result = new HashSet<int>();
            /*dboList list = TSNrListFromFilter(filter);
            int count = 0;

            foreach (int number in list)
            {
                bool isEF = false;
                bool isTSP = false;
                
                dboTS timeSeries = MesapAPIHelper.GetTimeSeries(number);
                timeSeries.DbReadRelatedKeys();
                dboTSKeys keys = timeSeries.TSKeys;
                foreach (dboTSKey key in keys)
                {
                    if (key.DimNr == 1 && key.ObjNr == 50003) isEF = true;
                    else if (key.DimNr == 2 && key.ObjNr == 1003) isTSP = true;
                }

                if (isEF && isTSP)
                    result.Add(number);

                if (updateCompletion)
                    Completion = (int)(++count / (float)list.Count * 100) / 2;
            }*/

            return result;
        }
    }
}
