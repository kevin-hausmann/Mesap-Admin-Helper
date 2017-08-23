using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using M4DBO;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    class ListExistingFindings : QualityCheck
    {
        public override string Id => "Bestehende";

        public override TimeSpan EstimatedExecutionTimePerElement => TimeSpan.FromMilliseconds(18);
        
        private const string InventoryID = "Datenprobleme";

        public override Task EstimateExecutionTimeAsync(Filter filter, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                dboEventInventory inventory = filter.Object.Database.EventInventories[InventoryID];
                dboEvents list = inventory.CreateObject_Events(mspEventReadMode.mspEventReadModeObjects);
                list.DbReadAll();

                ElementCount = list.Count;
                EstimatedExecutionTime = TimeSpan.FromMilliseconds(list.Count * EstimatedExecutionTimePerElement.TotalMilliseconds);
            }, cancellationToken);
        }

        public override Task RunAsync(Filter filter, CancellationToken cancellationToken, IProgress<ISet<Finding>> progress)
        {
            return Task.Run(() =>
            {
                Reset();
                Running = true;
                DateTime start = DateTime.Now;
                Completion = 0;
                
                dboDatabase db = filter.Object.Database;
                if (db.EventInventories.Exist(InventoryID))
                {
                    dboEventInventory inventory = db.EventInventories[InventoryID];
                    dboEvents list = inventory.CreateObject_Events(mspEventReadMode.mspEventReadModeObjects);
                    list.DbReadAll();
                    ElementCount = list.Count;

                    int count = 0;
                    foreach (dboEvent entry in list)
                    {
                        ISet<Finding> result = new HashSet<Finding>();
                        result.Add(Finding.FromDatabaseEntry(entry));

                        progress.Report(result);
                        cancellationToken.ThrowIfCancellationRequested();

                        ElementProcessedCount++;
                        FindingCount++;
                        Completion = (int)(count++ / (float)list.Count * 100);
                        MeasuredExecutionTimePerElement = TimeSpan.FromTicks(DateTime.Now.Subtract(start).Ticks / ElementProcessedCount);
                        RemainingExecutionTime = TimeSpan.FromMilliseconds(MeasuredExecutionTimePerElement.TotalMilliseconds * (list.Count - count));
                    }
                }

                Completion = 100;
                Running = false;
                Completed = true;
                MeasuredExecutionTime = DateTime.Now.Subtract(start);
            }, cancellationToken);
        }
    }
}
