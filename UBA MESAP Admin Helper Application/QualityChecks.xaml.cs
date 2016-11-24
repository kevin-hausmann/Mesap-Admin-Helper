using M4DBO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using UBA.Mesap.AdminHelper.Types;
using UBA.Mesap.AdminHelper.Types.QualityChecks;

namespace UBA.Mesap.AdminHelper
{
    /// <summary>
    /// Interaction logic for QualityChecks.xaml
    /// </summary>
    public partial class QualityChecks : UserControl, IDatabaseChangedObserver, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private ISet<QualityCheck> AvailableChecks = QualityCheck.FindImplementedChecks();

        private const string InventoryID = "Datenprobleme";
        private dboEventInventory existingInventory;
        private ISet<Finding> existingFindings;

        private const string QualityCheckViewName = "QualityCheck";
        private Filter filter;
       
        private CancellationTokenSource qualityCheckSource;
        private CancellationTokenSource estimateDurationSource;

        private bool running = false;
        public bool Idle
        {
            get { return !running; }
            private set
            {
                running = !value;
                NotifyChange();
            }
        }

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
            _FilterCountLabel.Visibility = activeCount > 0 ? Visibility.Visible : Visibility.Collapsed;

            // Estimate cumulative check execution time
            if (activeCount > 0)
            {
                _FilterCountLabel.Content = "Updating...";

                // First, update the filter information
                await UpdateFilterAsync();

                // Second, check all active checks and sum up execution times
                estimateDurationSource?.Dispose();
                estimateDurationSource = new CancellationTokenSource();
                Task[] checksEnabled = (from check in AvailableChecks where check.Enabled select check.EstimateExecutionTimeAsync(filter, estimateDurationSource.Token)).ToArray();
                await Task.WhenAll(checksEnabled);

                // Third, update UI
                _FilterCountLabel.Content = String.Format("{0} time series", filter.Count);
            }
        }

        private async void RunQualityChecks(object sender, RoutedEventArgs e)
        {
            Idle = false;

            // Prepare UI
            (((AdminHelper)Application.Current).Windows[0] as MainWindow).EnableDatabaseSelection(false);
            _RunQualityChecks.IsEnabled = false;
            _CancelQualityChecks.IsEnabled = true;
            _ResultListView.Items.Clear();

            // Prepare execution
            DateTime total = DateTime.Now;
            DateTime task = DateTime.Now;
            await UpdateFilterAsync();
            task = DateTime.Now;
            await LoadExistingFindingsAsync();
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
                foreach (Task faulted in checksRunning.Where(t => t.IsFaulted))
                {
                    Console.WriteLine("Task \"{0}\" failed!", faulted.Id);
                    Console.WriteLine(faulted.Exception);
                }
            }

            // Execution finished, update to UI
            (((AdminHelper)Application.Current).Windows[0] as MainWindow).EnableDatabaseSelection(true);
            _RunQualityChecks.IsEnabled = true;
            _CancelQualityChecks.IsEnabled = false;
            
            // We cannot reuse the cancel token
            qualityCheckSource.Dispose();
            qualityCheckSource = null;

            Idle = true;
        }

        private Action<ISet<Finding>> AddFinding()
        {
            return results =>
            {
                foreach (Finding finding in results)
                {
                    finding.Exists = existingFindings.Any(existing => finding.Check != null &&
                        finding.Check.Equals(existing.Check) && finding.Check.ConsideredEqual(existing, finding));

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

        private readonly Dictionary<string, PropertyChangedEventArgs> _argsCache = new Dictionary<string, PropertyChangedEventArgs>();
        protected virtual void NotifyChange([System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            if (PropertyChanged != null && _argsCache != null && !string.IsNullOrEmpty(memberName))
            {
                if (!_argsCache.ContainsKey(memberName))
                    _argsCache[memberName] = new PropertyChangedEventArgs(memberName);

                PropertyChanged.Invoke(this, _argsCache[memberName]);
            }
        }
    }
}
