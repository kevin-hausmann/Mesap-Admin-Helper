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
        public override string Id => "AD";

        public override string Name => "Aktivitätsdaten";

        public override string Description => "Something";

        public override Task RunAsync(dboTSFilter filter, CancellationToken cancellationToken, IProgress<ISet<Finding>> progress)
        {
            return Task.Run(() =>
            {
                Completion = 0;
                Finding one = new Types.Finding();
                one.Title = "Eins";
                one.CheckId = Id;
                ISet<Finding> oneSet = new HashSet<Finding>();
                oneSet.Add(one);

                Finding two = new Types.Finding();
                two.Title = "Zwei";
                two.CheckId = Id;
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
