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
        private ISet<QualityCheck> AvailableChecks = FindQualityChecks();

        private const string QualityCheckViewName = "QualityCheck";
        private Filter filter;
       
        private CancellationTokenSource qualityCheckSource;
        private CancellationTokenSource estimateDurationSource;

        public QualityChecks()
        {
            InitializeComponent();
            (((AdminHelper)Application.Current).Windows[0] as MainWindow).Register(this);

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
            _RunQualityChecks.IsEnabled = false;
            _CancelQualityChecks.IsEnabled = true;
            _ResultListView.Items.Clear();

            // Find quality check to run
            qualityCheckSource = new CancellationTokenSource();
            await UpdateFilterAsync();
            Task[] checksRunning = (from check in AvailableChecks where check.Enabled select
                             check.RunAsync(filter, qualityCheckSource.Token, new Progress<ISet<Finding>>(results => { _ResultListView.Items.Add(results.First()); }))).ToArray();

            // Start execution and wait for all checks to finish
            try
            {
                await Task.WhenAll(checksRunning);
            }
            catch (Exception ex)
            { 
                foreach (Task faulted in checksRunning.Where(t => t.IsFaulted)) { /* TODO */ }
            }

            // Execution finished, update to UI
            (((AdminHelper)Application.Current).Windows[0] as MainWindow).EnableDatabaseSelection(true);
            _RunQualityChecks.IsEnabled = true;
            _CancelQualityChecks.IsEnabled = false;
            // We cannot reuse the cancel token
            qualityCheckSource.Dispose();
            qualityCheckSource = null;
        }

        private void CancelQualityChecks(object sender, RoutedEventArgs e)
        {
            try
            {
                qualityCheckSource.Cancel();
            }
            catch (Exception ex)
            {
                // TODO
            }
                
        }

        private void CopyAll(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(MesapAPIHelper.GetListViewContentsAsCVSString(_ResultListView));
        }

        #region IDatabaseChangedObserver Members

        public void DatabaseChanged()
        {
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

        private static ISet<QualityCheck> FindQualityChecks()
        {
            // Use reflection to find all sub-classes of QualityCheck, create an instance
            // for each of those and return the result in a sorted set
            SortedSet<QualityCheck> checks = new SortedSet<QualityCheck>();

            foreach (Type type in Assembly.GetAssembly(typeof(QualityCheck)).GetTypes()
                .Where(myType => myType.IsClass && !myType.IsAbstract && myType.IsSubclassOf(typeof(QualityCheck))))
            {
                checks.Add((QualityCheck)Activator.CreateInstance(type));
            }
            
            return checks;
        }
    }
}
