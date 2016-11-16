using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using M4DBO;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    class MissingEmissionFactor : QualityCheck
    {
        public override string Name => "Vollständigkeit EF";

        public override string Description => "Die Emissionsfaktoren werden auf Vollständigkeit geprüft. In jeder Zeitreihe sollte entweder ein Wert stehen oder ein Interpolation/Extrapolation hinterlegt sein. In Ausnahmefällen kann auch mal ein Notation Key stehen.";

        public override short DatabaseReference => 117;

        public override Task<int> EstimateExecutionTimeAsync(Filter filter, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                return filter.Count * 20 + GetEmissionFactorSeries(filter).Count * EstimateExecutionTime();
            }, cancellationToken);
        }

        protected override short EstimateExecutionTime() { return 10; }

        public override Task RunAsync(Filter filter, CancellationToken cancellationToken, IProgress<ISet<Finding>> progress)
        {
            return Task.Run(() =>
            {
                Completion = 0;

                int year = 2015;
                ISet<TimeSeries> workload = GetEmissionFactorSeries(filter, year);
                Completion = 50;

                int total = workload.Count;
                int count = 1;

                foreach (TimeSeries series in workload)
                {
                    series.Object.DbReadRelatedProperties(mspTimeKeyEnum.mspTimeKeyYear, mspTimeKeyTypeEnum.mspTimeKeyTypeUnknown);
                    bool extra = series.Object.TSProperties.GetObject(mspTimeKeyEnum.mspTimeKeyYear, mspTimeKeyTypeEnum.mspTimeKeyTypeUnknown).Extrapol != mspExtrapolEnum.mspExtrapolNone;
                    if (series != null && series.Object.TSDatas.Count == 0 && !extra)
                    {
                        ISet<Finding> result = new HashSet<Finding>();
                        result.Add(new Finding(this,
                            "Emissionsfaktor fehlt: " + series.ID + ", " + year,
                            "Kein Emissionsfaktor vorhanden oder gemappt" + " (" + series.Legend + ")",
                            CategoriesForTimeSeries(series),
                            ContactsForTimeSeries(series),
                            Finding.PriorityEnum.Medium));

                        progress.Report(result);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    Completion = 50 + (int)(count++ / (float)total * 100) / 2;
                }

                Completion = 100;
            }, cancellationToken);
        }

        private ISet<TimeSeries> GetEmissionFactorSeries(Filter filter, int year = 0)
        {
            ISet<TimeSeries> result = new HashSet<TimeSeries>();

            dboList list = new dboList();
            list.FromString(filter.Object.GetTSNumbers(), VBA.VbVarType.vbLong);

            foreach (object number in list)
            {
                bool isEF = false;
                bool isGermany = false;
                bool isCorrectPollutant = false;

                dboTS timeSeries = MesapAPIHelper.GetTimeSeries(Convert.ToString(number));
                timeSeries.DbReadRelatedKeys();
                dboTSKeys keys = timeSeries.TSKeys;
                foreach(dboTSKey key in keys)
                {
                    if (key.DimNr == 1 && key.ObjNr == 50003) isEF = true;
                    else if (key.DimNr == 2 && key.ObjNr == 1003) isGermany = true;
                    else if (key.DimNr == 4 && (key.ObjNr == 3001 | key.ObjNr == 3002 | key.ObjNr == 3005 | key.ObjNr == 3031)) isCorrectPollutant = true;
                }

                if (isEF && isGermany && isCorrectPollutant)
                    result.Add(year == 0 ? new TimeSeries(timeSeries) : new TimeSeries(timeSeries, year, year));
            }

            return result;
        }
    }
}
