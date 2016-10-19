using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using M4DBO;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    class TSPEmissionFactorCheck : QualityCheck
    {
        public override string Id => "EFTSP";

        public override string Name => "EF BC <= PM2.5 <= PM10 <= TSP";

        public override string Description => "Vergleicht passende Emissionsfaktoren für Stäube und stellt sicher, dass die Fraktionen nicht zu groß sind.";

        public override long EstimateExecutionTime(Filter filter)
        {
            return filter.Count * EstimateExecutionTime();
        }

        protected override int EstimateExecutionTime() { return 112; }

        public override Task RunAsync(Filter filter, CancellationToken cancellationToken, IProgress<ISet<Finding>> progress)
        {
            return Task.Run(() =>
            {
                Finding one = new Types.Finding();
                one.Title = "Drei";
                one.CheckId = Id;
                ISet<Finding> oneSet = new HashSet<Finding>();
                oneSet.Add(one);

                Finding two = new Types.Finding();
                two.Title = "Vier";
                two.CheckId = Id;
                ISet<Finding> twoSet = new HashSet<Finding>();
                twoSet.Add(two);

                Completion = 0;
                Thread.Sleep(TimeSpan.FromSeconds(2));
                cancellationToken.ThrowIfCancellationRequested();
                progress.Report(oneSet);
                Completion = 25;
                Thread.Sleep(TimeSpan.FromSeconds(4));
                cancellationToken.ThrowIfCancellationRequested();
                progress.Report(twoSet);
                Completion = 100;

            }, cancellationToken);
        }
    }
}
