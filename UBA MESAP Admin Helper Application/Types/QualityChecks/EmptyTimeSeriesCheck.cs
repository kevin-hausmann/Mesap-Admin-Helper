using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        public override Task RunAsync(dboTSFilter filter, CancellationToken cancellationToken, IProgress<ISet<Finding>> progress)
        {
            return Task.Run(() =>
            {
                Completion = 0;

                dboList list = new dboList();
                list.FromString(filter.GetTSNumbers(), VBA.VbVarType.vbLong);

                int total = MesapAPIHelper.GetTimeSeriesCount(filter);
                int count = 1;

                foreach (object number in list)
                {
                    dboTS timeSeries = MesapAPIHelper.GetTimeSeries(Convert.ToString(number));
                    new TimeSeries(timeSeries, 1900, 2100);
                    if (timeSeries != null && timeSeries.TSDatas.Count == 0)
                    {
                        Finding finding = new Finding();
                        finding.CheckId = Id;
                        finding.Title = timeSeries.ID + " ist leer";
                        ISet<Finding> result = new HashSet<Finding>();
                        result.Add(finding);

                        progress.Report(result);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    Completion = (int)(count++ / (float)total * 100);
                }
            }, cancellationToken);
        }
    }
}
