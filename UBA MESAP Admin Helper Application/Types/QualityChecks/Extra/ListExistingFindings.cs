using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using M4DBO;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    class ListExistingFindings : QualityCheck
    {
        public override string Name => "Bestehende Fundstellen/Datenprobleme";
        public override string Description => "Lädt alle bereits bestehende Einträge von Datenproblemen aus der Datenbank.";
        public override short DatabaseReference => -1;

        private const string InventoryID = "Datenprobleme";

        public override Task<int> EstimateExecutionTimeAsync(Filter filter, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                dboEventInventory inventory = filter.Object.Database.EventInventories[InventoryID];
                dboEvents list = inventory.CreateObject_Events(mspEventReadMode.mspEventReadModeObjects);
                list.DbReadAll();

                return list.Count * EstimateExecutionTime();
            }, cancellationToken);
        }

        protected override short EstimateExecutionTime() { return 1; }

        public override Task RunAsync(Filter filter, CancellationToken cancellationToken, IProgress<ISet<Finding>> progress)
        {
            return Task.Run(() =>
            {
                Completion = 0;
                
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
                    }
                }

                Completion = 100;
            }, cancellationToken);
        }
    }
}
