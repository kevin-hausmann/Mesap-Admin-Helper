using M4DBO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private dboTSFilter filter;

        private ISet<QualityCheck> AvailableChecks = FindQualityChecks();
        private ISet<QualityCheck> EnabledChecks = new SortedSet<QualityCheck>();

        private CancellationTokenSource cts;

        public QualityChecks()
        {
            InitializeComponent();
            (((AdminHelper)Application.Current).Windows[0] as MainWindow).Register(this);

            // Bind the list of quality checks to the UI
            _QualityCheckList.ItemsSource = new ObservableCollection<QualityCheck>(AvailableChecks);

            // Filter used to limit the time series' check run on
            filter = ((AdminHelper)Application.Current).database.CreateObject_TSFilter();
        }

        private void SelectCheck(object sender, RoutedEventArgs e)
        {
            String checkId = ((CheckBox)sender).Tag.ToString();
            // Enable/disable quality check according to current status
            if (EnabledChecks.Count(i => i.Id == checkId) > 0) EnabledChecks.Remove(AvailableChecks.Where(i => i.Id == checkId).First());
            else EnabledChecks.Add(AvailableChecks.Where(i => i.Id == checkId).First());

            // Only allow execution if at least one check is selected
            _RunQualityChecks.IsEnabled = EnabledChecks.Count > 0;
        }

        private async void RunQualityChecks(object sender, RoutedEventArgs e)
        {
            // Use filter from quality check view in database, if any
            dboTSViews views = ((AdminHelper)Application.Current).database.CreateObject_TsViews("QualityCheck");
            if (views.Count == 1)
            {
                dboTSView view = MesapAPIHelper.GetFirstView(views);
                filter = view.TsFilterGet();
            }

            // Prepare UI
            (((AdminHelper)Application.Current).Windows[0] as MainWindow).EnableDatabaseSelection(false);
            _RunQualityChecks.IsEnabled = false;
            _CancelQualityChecks.IsEnabled = true;
            _ResultListView.Items.Clear();

            // Find quality check to run
            cts = new CancellationTokenSource();
            Task[] checksRunning = (from check in EnabledChecks select
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
