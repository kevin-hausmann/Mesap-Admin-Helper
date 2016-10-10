using M4DBO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        public QualityChecks()
        {
            InitializeComponent();
            (((AdminHelper)Application.Current).Windows[0] as MainWindow).Register(this);

            // Bind the list of quality checks to the UI
            _QualityCheckList.ItemsSource = AvailableChecks;

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

        private void RunQualityChecks(object sender, RoutedEventArgs e)
        {
            // Use filter from quality check view in database, if any
            dboTSViews views = ((AdminHelper)Application.Current).database.CreateObject_TsViews("QualityCheck");
            if (views.Count == 1)
            {
                dboTSView view = MesapAPIHelper.GetFirstView(views);
                filter = view.TsFilterGet();
            }

            int count = MesapAPIHelper.GetTimeSeriesCount(filter);

            // Check back if long term operation
            if (count < 1000 || (count >= 1000 &&
                MessageBox.Show("Ohne Einschränkung dauert die Prüfung sehr lange - trotzdem durchführen?",
                "Starten", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes))
            {
                // Prepare UI
                (((AdminHelper)Application.Current).Windows[0] as MainWindow).EnableDatabaseSelection(false);
                _RunQualityChecks.IsEnabled = false;
                _StatusLabel.Visibility = Visibility.Visible;
                _ResultListView.Items.Clear();

                // Start job
                Action runQualityChecks = new Action(RunQualityChecks);
                runQualityChecks.BeginInvoke(null, null);
            }
        }

        private void RunQualityChecks()
        {
            DateTime start = DateTime.Now;
            dboList list = new dboList();
            list.FromString(filter.GetTSNumbers(), VBA.VbVarType.vbLong);

            int total = MesapAPIHelper.GetTimeSeriesCount(filter);
            int count = 1;

            foreach (object number in list)
            {
                dboTS timeSeries = MesapAPIHelper.GetTimeSeries(Convert.ToString(number));
                if (timeSeries == null) continue;
            }

            Action<String> showFinished = new Action<String>(ShowFinished);
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, showFinished, "Fertig - " + (count - 1) + " Zeitreihe(n) untersucht");
        }

        private void ShowProgress(String message)
        {
            _StatusLabel.Content = message;
        }

        private void ShowFinished(String message)
        {
            (((AdminHelper)Application.Current).Windows[0] as MainWindow).EnableDatabaseSelection(true);
            _RunQualityChecks.IsEnabled = true;

            _StatusLabel.Content = message;
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
