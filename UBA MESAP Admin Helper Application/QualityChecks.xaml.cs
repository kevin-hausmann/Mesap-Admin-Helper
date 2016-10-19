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
       
        private CancellationTokenSource cts;
        
        public QualityChecks()
        {
            InitializeComponent();
            (((AdminHelper)Application.Current).Windows[0] as MainWindow).Register(this);

            // Bind the list of quality checks to the UI
            _QualityCheckList.ItemsSource = new ObservableCollection<QualityCheck>(AvailableChecks);
        }

        private void SelectCheck(object sender, RoutedEventArgs e)
        {
            // String checkId = ((CheckBox)sender).Tag.ToString();
            UpdateFilter();

            IEnumerable<QualityCheck> activeChecks = AvailableChecks.Where(check => check.Enabled);
            int count = activeChecks.Count();
            _RunQualityChecks.IsEnabled = count > 0;
            _ExecutionTimeLeft.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;

            long executionTime = activeChecks.Sum(check => check.EstimateExecutionTime(filter));
            _ExecutionTimeLeft.Content = String.Format("{0:g} for {1} time series", TimeSpan.FromMilliseconds(executionTime), filter.Count);
        }

        private async void RunQualityChecks(object sender, RoutedEventArgs e)
        {
            UpdateFilter();

            // Prepare UI
            (((AdminHelper)Application.Current).Windows[0] as MainWindow).EnableDatabaseSelection(false);
            _RunQualityChecks.IsEnabled = false;
            _CancelQualityChecks.IsEnabled = true;
            _ResultListView.Items.Clear();

            // Find quality check to run
            cts = new CancellationTokenSource();
            Task[] checksRunning = (from check in AvailableChecks where check.Enabled select
                             check.RunAsync(filter, cts.Token, new Progress<ISet<Finding>>(results => { _ResultListView.Items.Add(results); }))).ToArray();

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
            cts.Dispose();
            cts = null;
        }

        private void CancelQualityChecks(object sender, RoutedEventArgs e)
        {
            try
            {
                cts.Cancel();
            }
            catch (Exception ex)
            {
                // TODO
            }
                
        }
        
        #region IDatabaseChangedObserver Members

        public void DatabaseChanged()
        {
            _ResultListView.Items.Clear();
        }

        #endregion

        private void UpdateFilter()
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
