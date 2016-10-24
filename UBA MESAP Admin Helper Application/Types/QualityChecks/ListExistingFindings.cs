using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using M4DBO;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    class ListExistingFindings : QualityCheck
    {
        public override string Id => "XYZ";

        public override string Name => "Bestehende Fundstellen/Datenprobleme";

        public override string Description => "Lädt alle bereits bestehende Einträge von Datenproblemen aus der Datenbank.";

        public override long EstimateExecutionTime(Filter filter)
        {
            return filter.Count * EstimateExecutionTime();
        }

        protected override int EstimateExecutionTime() { return 1; }

        public override Task RunAsync(Filter filter, CancellationToken cancellationToken, IProgress<ISet<Finding>> progress)
        {
            return Task.Run(() =>
            {
                Completion = 0;
                const string InventoryID = "Datenprobleme";

                dboDatabase db = filter.Object.Database;
                if (db.EventInventories.Exist(InventoryID))
                {
                    dboEventInventory inventory = db.EventInventories[InventoryID];
                    dboEvents list = inventory.CreateObject_Events(mspEventReadMode.mspEventReadModeObjects);
                    list.DbReadAll();

                    int count = 0;
                    foreach (dboEvent entry in list)
                    {
                        ISet<Finding> result = new HashSet<Finding>();
                        result.Add(Finding.FromDatabaseEntry(entry));

                        progress.Report(result);
                        cancellationToken.ThrowIfCancellationRequested();
                        Completion = (int)(count++ / (float)list.Count * 100);
                        Thread.Sleep(TimeSpan.FromMilliseconds(100));
                    }
                }

                Completion = 100;
            }, cancellationToken);
        }
    }
}
