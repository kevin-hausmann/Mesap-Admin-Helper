using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using M4DBO;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    class EmptyTimeSeriesCheck : QualityCheck
    {
        public override string Id => "Leer";

        public override string Name => "Leere Zeitreihen";

        public override string Description => "Identifiziert Zeitreihen, die keinerlei Werte enthalten";

        public override short DatabaseReference => 119;

        public override Task<int> EstimateExecutionTimeAsync(Filter filter, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                return filter.Count * EstimateExecutionTime();
            }, cancellationToken);
        }

        protected override short EstimateExecutionTime() { return 75; }

        private const short startYear = 1900;
        private const short endYear = 2100;
        private const Finding.PriorityEnum priority = Finding.PriorityEnum.Low;

        public override Task RunAsync(Filter filter, CancellationToken cancellationToken, IProgress<ISet<Finding>> progress)
        {
            return Task.Run(() =>
            {
                Completion = 0;

                dboList list = new dboList();
                list.FromString(filter.Object.GetTSNumbers(), VBA.VbVarType.vbLong);

                int total = MesapAPIHelper.GetTimeSeriesCount(filter.Object);
                int count = 1;

                foreach (object number in list)
                {
                    dboTS timeSeries = MesapAPIHelper.GetTimeSeries(Convert.ToString(number));
                    TimeSeries ts = new TimeSeries(timeSeries, startYear, endYear);
                    if (timeSeries != null && timeSeries.TSDatas.Count == 0)
                    {
                        ISet<Finding> result = new HashSet<Finding>();
                        result.Add(new Finding(this,
                            timeSeries.ID + " ist leer",
                            "Diese Zeitreihe enthält keinerlei Werte " + ts.Legend,
                            Finding.ContactEnum.NN, priority));

                        progress.Report(result);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    Completion = (int)(count++ / (float)total * 100);
                }
            }, cancellationToken);
        }
    }
}
