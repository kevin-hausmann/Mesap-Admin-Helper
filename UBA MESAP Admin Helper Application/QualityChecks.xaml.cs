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

        private const string ChecksInventoryID = "Checks";
        private ISet<QualityCheck> availableChecks;

        private const string FindingsInventoryID = "Datenprobleme";
        private dboEventInventory existingFindingsInventory;
        private ISet<Finding> existingFindings;

        private const string QualityCheckViewName = "QualityCheck";
        private Filter filter;
       
        private CancellationTokenSource qualityCheckSource;
        private CancellationTokenSource estimateDurationSource;

        public QualityChecks()
        {
            InitializeComponent();
            (((AdminHelper)Application.Current).Windows[0] as MainWindow).Register(this);

            dboEventInventories inventories = ((AdminHelper)Application.Current).database.EventInventories;

            availableChecks = QualityCheck.FindImplementedChecks(inventories[ChecksInventoryID]);
            _QualityCheckList.ItemsSource = new ObservableCollection<QualityCheck>(availableChecks);

            existingFindingsInventory = inventories[FindingsInventoryID];
        }

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

        /// <summary>
        /// Update the list of active checks and re-calculate estimated execution times.
        /// </summary>
        private async void SelectCheck(object sender, RoutedEventArgs e)
        {
            // Cancel all previously started execution time estimation tasks (if any)
            if (estimateDurationSource != null && !estimateDurationSource.IsCancellationRequested)
                estimateDurationSource.Cancel();

            // Determine active check count and adjust UI
            int activeCount = availableChecks.Count(check => check.Enabled);
            _RunQualityChecks.IsEnabled = activeCount > 0;
            _FilterCountLabel.Visibility = activeCount > 0 ? Visibility.Visible : Visibility.Collapsed;

            // Estimate cumulative check execution time
            if (activeCount > 0)
                try
                {
                    _FilterCountLabel.Content = "Updating...";

                    // First, update the filter information
                    await UpdateFilterAsync();

                    // Second, check all active checks and sum up execution times
                    estimateDurationSource?.Dispose();
                    estimateDurationSource = new CancellationTokenSource();
                    Task[] checksEnabled = (from check in availableChecks where check.Enabled select check.EstimateExecutionTimeAsync(filter, estimateDurationSource.Token)).ToArray();
                    await Task.WhenAll(checksEnabled);

                    // Third, update UI
                    _FilterCountLabel.Content = String.Format("{0} time series", filter.Count);
                }
                catch (OperationCanceledException) {}
        }

        /// <summary>
        /// Run all active checks.
        /// </summary>
        /// <see cref="CancelQualityChecks(object, RoutedEventArgs)"/>
        private async void RunQualityChecks(object sender, RoutedEventArgs e)
        {
            Idle = false;

            // Prepare UI
            (((AdminHelper)Application.Current).Windows[0] as MainWindow).EnableDatabaseSelection(false);
            _RunQualityChecks.IsEnabled = false;
            _CancelQualityChecks.IsEnabled = true;
            _ResultListView.Items.Clear();

            // Prepare execution
            await UpdateFilterAsync();
            await LoadExistingFindingsAsync();
            qualityCheckSource = new CancellationTokenSource();

            // Find quality check to run and wait for all active tasks to execute
            Task[] checksRunning = (from check in availableChecks where check.Enabled
                                    select check.RunAsync(filter, qualityCheckSource.Token, new Progress<ISet<Finding>>(AddFinding()))).ToArray();
            try
            {
                await Task.WhenAll(checksRunning);
            }
            catch (Exception)
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

        /// <returns>Handler used to populate UI list of findings with additional items.</returns>
        private Action<ISet<Finding>> AddFinding()
        {
            return findings =>
            {
                foreach (Finding finding in findings)
                {
                    finding.Exists = existingFindings.Any(existing => finding.Check != null &&
                        finding.Check.Equals(existing.Check) && finding.Check.ConsideredEqual(existing, finding));

                    _ResultListView.Items.Add(finding);
                }
            };
        }

        /// <summary>
        /// Interrupt running quality check execution.
        /// </summary>
        /// <see cref="RunQualityChecks(object, RoutedEventArgs)"/>
        private void CancelQualityChecks(object sender, RoutedEventArgs e)
        {
            try
            {
                qualityCheckSource.Cancel();
            }
            catch (Exception) {}
                
        }

        /// <summary>
        /// Store selected findings in active Mesap database.
        /// </summary>
        private void PushFindings(object sender, RoutedEventArgs e)
        {
            if (existingFindingsInventory != null)
            {
                dboEvents events = existingFindingsInventory.CreateObject_Events(mspEventReadMode.mspEventReadModeObjects);
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
                MessageBox.Show("Kein Ereignisinventar mit der ID \"" + FindingsInventoryID + "\" gefunden!",
                    "Ergebnisse speichern", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        private void CopyAll(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(MesapAPIHelper.GetListViewContentsAsCVSString(_ResultListView));
        }

        private void CopyTimeSeries(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(String.Join(",", _ResultListView.Items.Cast<Finding>().Select(item => item.TimeSeriesLabel)));
        }

        #region IDatabaseChangedObserver Members

        public void DatabaseChanged()
        {
            existingFindingsInventory = ((AdminHelper)Application.Current).database.EventInventories[FindingsInventoryID];

            SelectCheck(null, null);
            _ResultListView.Items.Clear();
        }

        #endregion

        private Task UpdateFilterAsync()
        {
            return Task.Run(() =>
            {
                dboTSView qualityView = MesapAPIHelper.GetView(QualityCheckViewName);

                // Use quality check view filter if present, check all series otherwise
                filter = qualityView != null ? new Filter(qualityView.TsFilterGet()) :
                    new Filter(((AdminHelper)Application.Current).database.CreateObject_TSFilter());
            });
        }

        private Task LoadExistingFindingsAsync()
        {
            return Task.Run(() =>
            {
                existingFindings = new HashSet<Finding>();
                
                if (existingFindingsInventory != null)
                {
                    dboEvents list = existingFindingsInventory.CreateObject_Events(mspEventReadMode.mspEventReadModeObjects);
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
