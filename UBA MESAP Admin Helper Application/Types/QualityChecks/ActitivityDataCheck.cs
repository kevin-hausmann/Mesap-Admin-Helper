using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using M4DBO;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    class ActitivityDataCheck : QualityCheck
    {
        public override string Id => "ARfehlt";

        public override string Name => "Aktivitätsrate fehlt";

        public override string Description => "Something";

        public override short DatabaseReference => 118;

        public override Task<int> EstimateExecutionTimeAsync(Filter filter, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                return filter.Count * EstimateExecutionTime();
            }, cancellationToken);
        }

        protected override short EstimateExecutionTime() { return 42; }

        public override Task RunAsync(Filter filter, CancellationToken cancellationToken, IProgress<ISet<Finding>> progress)
        {
            return Task.Run(() =>
            {
                Completion = 0;
                Finding one = new Types.Finding();
                one.Title = "Eins";
                one.Check = this;
                ISet<Finding> oneSet = new HashSet<Finding>();
                oneSet.Add(one);

                Finding two = new Types.Finding();
                two.Title = "Zwei";
                two.Check = this;
                ISet<Finding> twoSet = new HashSet<Finding>();
                twoSet.Add(two);

                Thread.Sleep(TimeSpan.FromSeconds(3));
                cancellationToken.ThrowIfCancellationRequested();
                Completion = 30;
                progress.Report(oneSet);
                Thread.Sleep(TimeSpan.FromSeconds(3));
                cancellationToken.ThrowIfCancellationRequested();
                Completion = 60;
                progress.Report(twoSet);
                Completion = 100;

            }, cancellationToken);
        }
    }
}
