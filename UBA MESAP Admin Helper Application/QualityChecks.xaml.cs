using M4DBO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using UBA.Mesap.AdminHelper.Types;

namespace UBA.Mesap.AdminHelper
{
    /// <summary>
    /// Interaction logic for QualityChecks.xaml
    /// </summary>
    public partial class QualityChecks : UserControl, IDatabaseChangedObserver
    {
        private ISet<QualityCheck> AvailableChecks = QualityCheck.FindImplementedChecks();

        private const string InventoryID = "Datenprobleme";
        private dboEventInventory existingInventory;
        private ISet<Finding> existingFindings;

        private const string QualityCheckViewName = "QualityCheck";
        private Filter filter;
       
        private CancellationTokenSource qualityCheckSource;
        private CancellationTokenSource estimateDurationSource;

        public QualityChecks()
        {
            InitializeComponent();
            (((AdminHelper)Application.Current).Windows[0] as MainWindow).Register(this);

            existingInventory = ((AdminHelper)Application.Current).database.EventInventories[InventoryID];
            if (!((AdminHelper)Application.Current).database.EventInventories.Exist(InventoryID)) Console.WriteLine("NO INVENTORY FOUND");

            // Bind the list of quality checks to the UI
            _QualityCheckList.ItemsSource = new ObservableCollection<QualityCheck>(AvailableChecks);
        }

        private async void SelectCheck(object sender, RoutedEventArgs e)
        {
            // Cancel all previously start execution time estimation tasks (if any)
            if (estimateDurationSource != null && !estimateDurationSource.IsCancellationRequested)
                estimateDurationSource.Cancel();

            // Determine active check count and adjust UI
            int activeCount = AvailableChecks.Count(check => check.Enabled);
            _RunQualityChecks.IsEnabled = activeCount > 0;
            _ExecutionTimeLeft.Visibility = activeCount > 0 ? Visibility.Visible : Visibility.Collapsed;

            // Estimate cumulative check execution time
            if (activeCount > 0)
            {
                _ExecutionTimeLeft.Content = "Updating...";

                // First, update the filter information
                await UpdateFilterAsync();

                // Second, check all active checks and sum up execution times
                if (estimateDurationSource != null)
                    estimateDurationSource.Dispose();
                estimateDurationSource = new CancellationTokenSource();
                Task<int>[] checksEnabled = (from check in AvailableChecks where check.Enabled select check.EstimateExecutionTimeAsync(filter, estimateDurationSource.Token)).ToArray();
                int[] durations = await Task.WhenAll(checksEnabled);

                // Third, update UI
                _ExecutionTimeLeft.Content = String.Format("{0:g} for {1} time series", TimeSpan.FromMilliseconds(durations.Sum()), filter.Count);
            }
        }

        private async void RunQualityChecks(object sender, RoutedEventArgs e)
        {
            // Prepare UI
            (((AdminHelper)Application.Current).Windows[0] as MainWindow).EnableDatabaseSelection(false);
            _StatusFilter.Content = _StatusFindings.Content = _StatusChecks.Content = _StatusTotal.Content = _StatusPerSeries.Content = "";
            _StatusBar.Visibility = Visibility.Visible;
            _RunQualityChecks.IsEnabled = false;
            _CancelQualityChecks.IsEnabled = true;
            _ResultListView.Items.Clear();

            // Prepare execution
            DateTime total = DateTime.Now;
            DateTime task = DateTime.Now;
            await UpdateFilterAsync();
            _StatusFilter.Content = String.Format("Filter {0}ms", DateTime.Now.Subtract(task).TotalMilliseconds);
            task = DateTime.Now;
            await LoadExistingFindingsAsync();
            _StatusFindings.Content = String.Format("Existing findings {0}ms", DateTime.Now.Subtract(task).TotalMilliseconds);
            task = DateTime.Now;

            // Find quality check to run
            qualityCheckSource = new CancellationTokenSource();
            Task[] checksRunning = (from check in AvailableChecks where check.Enabled
                                    select check.RunAsync(filter, qualityCheckSource.Token, new Progress<ISet<Finding>>(AddFinding()))).ToArray();

            // Start execution and wait for all checks to finish
            try
            {
                await Task.WhenAll(checksRunning);
            }
            catch (Exception ex)
            {
                foreach (Task faulted in checksRunning.Where(t => t.IsFaulted)) { Console.WriteLine("Task failed: " + ex.Message); }
            }

            // Execution finished, update to UI
            (((AdminHelper)Application.Current).Windows[0] as MainWindow).EnableDatabaseSelection(true);
            _RunQualityChecks.IsEnabled = true;
            _CancelQualityChecks.IsEnabled = false;
            _StatusChecks.Content = String.Format("Checks {0}ms", DateTime.Now.Subtract(task).TotalMilliseconds);
            _StatusTotal.Content = String.Format("Total {0}ms", DateTime.Now.Subtract(total).TotalMilliseconds);
            _StatusPerSeries.Content = String.Format("{0}ms for each of {1} serie(s)", ((int) DateTime.Now.Subtract(total).TotalMilliseconds) / filter.Count, filter.Count);

            // We cannot reuse the cancel token
            qualityCheckSource.Dispose();
            qualityCheckSource = null;
        }

        private Action<ISet<Finding>> AddFinding()
        {
            return results =>
            {
                foreach (Finding finding in results)
                {
                    finding.Exists = existingFindings.Any(existing => finding.Check != null &&
                        finding.Equals(existing.Check) && finding.Check.ConsideredEqual(existing, finding));

                    _ResultListView.Items.Add(finding);
                }
            };
        }

        private void CancelQualityChecks(object sender, RoutedEventArgs e)
        {
            try
            {
                qualityCheckSource.Cancel();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Cancel failed: " + ex.Message);
            }
                
        }

        private void PushFindings(object sender, RoutedEventArgs e)
        {
            if (existingInventory != null)
            {
                dboEvents events = existingInventory.CreateObject_Events(mspEventReadMode.mspEventReadModeObjects);
                int handle = ((AdminHelper)Application.Current).database.Root.GetFreeLockHandle();

                foreach (Finding finding in _ResultListView.SelectedItems)
                {
                    if (finding.Exists) continue;

                    dboEvent newEvent = events.Add(handle, 0);
                    try
                    {
                        Finding.ToDatabaseEntry(newEvent, finding);
                        finding.Exists = true;
                    }
                    catch (Exception ex)
                    {
                        events.Delete(newEvent.EventNr);
                        MessageBox.Show(String.Format("Finding \"{0}\" kann nicht angelegt werden:\n{1}", finding.Title, ex.Message),
                            "Ergebnisse speichern", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    }
                }

                events.DbUpdateAll(handle);
                events.DisableModifyAll(handle);

                _ResultListView.Items.Refresh();
            }
            else
            {
                MessageBox.Show("Kein Ereignisinventar mit der ID \"" + InventoryID + "\" gefunden!",
                    "Ergebnisse speichern", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        private void CopyAll(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(MesapAPIHelper.GetListViewContentsAsCVSString(_ResultListView));
        }

        #region IDatabaseChangedObserver Members

        public void DatabaseChanged()
        {
            existingInventory = ((AdminHelper)Application.Current).database.EventInventories[InventoryID];

            SelectCheck(null, null);
            _ResultListView.Items.Clear();
        }

        #endregion

        private Task UpdateFilterAsync()
        {
            return Task.Run(() =>
            {
                // Use filter from quality check view in database, if any
                dboTSViews views = ((AdminHelper)Application.Current).database.CreateObject_TsViews(QualityCheckViewName);
                if (views.Count == 1)
                {
                    dboTSView view = MesapAPIHelper.GetFirstView(views);
                    filter = new Filter(view.TsFilterGet());
                }
                else
                {
                    // Nothing found, use all time series
                    filter = new Filter(((AdminHelper)Application.Current).database.CreateObject_TSFilter());
                }
            });
        }

        private Task LoadExistingFindingsAsync()
        {
            return Task.Run(() =>
            {
                existingFindings = new HashSet<Finding>();
                
                if (existingInventory != null)
                {
                    dboEvents list = existingInventory.CreateObject_Events(mspEventReadMode.mspEventReadModeObjects);
                    list.DbReadAll();

                    foreach (dboEvent entry in list)
                        existingFindings.Add(Finding.FromDatabaseEntry(entry));
                }
            });
        }
    }
}
