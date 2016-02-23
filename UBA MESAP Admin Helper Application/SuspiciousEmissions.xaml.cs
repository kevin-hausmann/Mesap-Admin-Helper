﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using M4DBO;
using M4UIO;
using System.Windows.Threading;
using UBA.Mesap.AdminHelper.Types;

namespace UBA.Mesap.AdminHelper
{
    /// <summary>
    /// Interaction logic for SuspiciousEmissions.xaml
    /// </summary>
    public partial class SuspiciousEmissions : UserControl, IDatabaseChangedObserver
    {
        // Filter determining which series to check
        private dboTSFilter filter;

        // UI root handle
        private uioRoot _uiRoot;

        public SuspiciousEmissions()
        {
            InitializeComponent();
            (((AdminHelper)Application.Current).Windows[0] as MainWindow).Register(this);

            _uiRoot = ((AdminHelper)Application.Current).uiRoot;

            // Set initial filter (filters all!)
            filter = ((AdminHelper)Application.Current).database.CreateObject_TSFilter();
            filter.FilterUsage = mspFilterUsageEnum.mspFilterUsageFilterOnly;

            // Organize result sorting
            //_EmissionsListView.Items.SortDescriptions.Clear();
            //_EmissionsListView.Items.SortDescriptions.Add(new SortDescription("Legend", ListSortDirection.Ascending));
        }

        private void SetFilter(object sender, RoutedEventArgs e)
        {
            if (_uiRoot.ShowDlgTsFilterEdit(ref filter))
            {
                int count = MesapAPIHelper.GetTimeSeriesCount(filter);
                _FilterCountLabel.Content = count + " Zeitreihe(n)";
            }
        }

        private void SearchSuspiciousEmissions(object sender, RoutedEventArgs e)
        {
            int count = MesapAPIHelper.GetTimeSeriesCount(filter);

            // Check back if long term operation
            if (count < 10000 || (count >= 10000 &&
                MessageBox.Show("Ohne (weitere) Einschränkung der Suche dauert diese Operation sehr lange - trotzdem starten?",
                "Suche starten", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes))
            {
                // Prepare UI
                (((AdminHelper)Application.Current).Windows[0] as MainWindow).EnableDatabaseSelection(false);
                _SetFilterButton.IsEnabled = false;
                _StartSearchButton.IsEnabled = false;
                _ChangeAllowedSelect.IsEnabled = false;
                _YearSelect.IsEnabled = false;
                _NeedsChangeTimePicker.Enabled = false;
                _AreaSelect.IsEnabled = false;
                _TestConsistentCheck.IsEnabled = false;
                _CurrentEmissionLabel.Visibility = Visibility.Visible;
                _EmissionsListView.Items.Clear();

                double allowedChange;
                switch (_ChangeAllowedSelect.SelectedIndex)
                {
                    case 0: allowedChange = 0.2; break;
                    case 1: allowedChange = 0.5; break;
                    default: allowedChange = 1; break;
                }

                SearchSuspiciousEmissionsConfiguration config = new SearchSuspiciousEmissionsConfiguration();
                config.areaCode = _AreaSelect.Text;
                config.year = Convert.ToInt32(_YearSelect.Text);
                config.needsChangeAfter = _NeedsChangeTimePicker.Value;
                config.allowedChange = allowedChange;
                config.checkConsistent = (bool)_TestConsistentCheck.IsChecked;

                // Start job
                Action<SearchSuspiciousEmissionsConfiguration> search = new Action<SearchSuspiciousEmissionsConfiguration>(SearchSuspiciousEmissions);
                search.BeginInvoke(config, null, null);
            }
        }

        private void SearchSuspiciousEmissions(SearchSuspiciousEmissionsConfiguration config)
        {
            DateTime start = DateTime.Now;
            dboList list = new dboList();
            list.FromString(filter.GetTSNumbers(), VBA.VbVarType.vbLong);

            int total = MesapAPIHelper.GetTimeSeriesCount(filter);
            int count = 1;
            int candidates = 0;
            int markedConsistent = 0;
            int findings = 0;

            foreach (object number in list)
            {
                dboTS timeSeries = MesapAPIHelper.GetTimeSeries(Convert.ToString(number));
                if (timeSeries == null) continue;

                Action<String> showProgress = new Action<String>(ShowProgress);
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, showProgress,
                    "(" + DateTime.Now.Subtract(start) + " von " +
                    new TimeSpan(DateTime.Now.Subtract(start).Ticks * total / count) + ") " +
                    "Untersuche Zeitreihe " + (count++) + " von " + total + ": " + timeSeries.Name);

                EmissionSeries series = new EmissionSeries(timeSeries);
                if (!(series.Legend.StartsWith(config.areaCode + ",EM,") ||
                    series.Legend.StartsWith(config.areaCode + ",EMI_CO2,"))) continue;
                
                // We have a candidate
                candidates++;

                // Is marked as okay?
                if (series.IsMarkedConsistent()) markedConsistent++;

                // Do we check time series marked consistent?
                if (!config.checkConsistent && series.IsMarkedConsistent()) continue;

                // Check for problems
                else series.Check(config.year, config.needsChangeAfter, config.allowedChange);
                
                // No problems
                if (!series.HasProblem()) continue;

                // Add to list
                findings++;
                Action<EmissionSeries> addSeries = new Action<EmissionSeries>(AddTimeSeries);
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, addSeries, series);
            }

            Action<String> showFinished = new Action<String>(ShowFinished);
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, showFinished, "Fertig - " + candidates + 
                " Emissionszeitreihe(n) untersucht: davon fraglich: " + findings +
                ", davon als konsistent markiert: " + markedConsistent);
        }

        private void AddTimeSeries(EmissionSeries series)
        {
            _EmissionsListView.Items.Add(series);
        }

        private void ShowProgress(String message)
        {
            _CurrentEmissionLabel.Content = message;
        }

        private void ShowFinished(String message)
        {
            _EmissionsListView.Items.Refresh();

            (((AdminHelper)Application.Current).Windows[0] as MainWindow).EnableDatabaseSelection(true);
            _SetFilterButton.IsEnabled = true;
            _StartSearchButton.IsEnabled = true;
            _ChangeAllowedSelect.IsEnabled = true;
            _YearSelect.IsEnabled = true;
            _NeedsChangeTimePicker.Enabled = true;
            _AreaSelect.IsEnabled = true;
            _TestConsistentCheck.IsEnabled = true;

            _CurrentEmissionLabel.Content = message;
        }

        private void Show(object sender, RoutedEventArgs e)
        {
            if (_EmissionsListView.Items.Count == 0) return;

            String list = "";
            foreach (EmissionSeries series in _EmissionsListView.Items)
                list += series.Object.TsNr + ",";

            dboTSViews views = MesapAPIHelper.GetAdminHelperView();
            dboTSView view = MesapAPIHelper.GetFirstView(views);

            AdminHelper application = ((AdminHelper)Application.Current);
            int handle = application.root.GetFreeLockHandle();
            view.EnableModify(handle);

            dboTSFilter filter = ((AdminHelper)Application.Current).database.CreateObject_TSFilter();
            filter.TsList = list.Substring(0, list.Length - 1);
            filter.FilterUsage = mspFilterUsageEnum.mspFilterUsageListOnly;
            view.TsFilterSet(filter);
            view.DisableModify(handle);
            views.DbUpdateAll(handle);

            application.uiRoot.ShowFormDataSheet(application.database.DbNr, view.ViewNr, false);
        }

        private void ResetAllOKFlags(object sender, RoutedEventArgs e)
        {

        }

        #region IDatabaseChangedObserver Members

        public void DatabaseChanged()
        {
            _EmissionsListView.Items.Clear();
            filter = ((AdminHelper)Application.Current).database.CreateObject_TSFilter();
        }

        #endregion
    }

    internal class SearchSuspiciousEmissionsConfiguration
    {
        public String areaCode { get; set; }
        public int year {get; set; }
        public DateTime needsChangeAfter {get; set;}
        public double allowedChange {get; set;}
        public bool checkConsistent { get; set; }
    }

    class EmissionSeries : TimeSeries
    {
        private List<String> _problems = new List<String>();

        public EmissionSeries(dboTS series)
            : base(series)
        { }

        /// <summary>
        /// Checks whether any problems have been detected by the "Check"-Method.
        /// Returns false if the "Check"-Method has not been called before.
        /// </summary>
        /// <returns>true if problem where found, false otherwise.</returns>
        public bool HasProblem()
        {
            return _problems.Count > 0;
        }

        public String Problems
        {
            get
            {
                String result = "";

                foreach (String problem in _problems)
                    result += problem + ", ";

                return result.Substring(0, result.Length - 2);
            }
        }

        public void Check(int year, DateTime needsChangeAfter, double allowedChange)
        {
            // Read values for current year
            ReadData(year, true);

            if (!Exists(year)) return;

            ChangedAfter(year, needsChangeAfter);

            if (!IsZero(year))
            {
                // Read values for the current and previous year
                ReadData(year - 1, year);

                PreviousEmptyOrZero(year);
                HighChange(year, allowedChange);
            }
        }

        private bool Exists(int year)
        {
            // Current year value exists?
            if (_timeSeries.TSDatas.Count == 0)
            {
                _problems.Add("Kein Wert " + year);
                return false;
            }

            return true;
        }

        private void ChangedAfter(int year, DateTime needsChangeAfter)
        {
            // Get data objects
            DataValue dataCurrent = RetrieveData(year);

            // Can not be null beyond this point
            if (dataCurrent == null || dataCurrent.Object.ChangeDate == null) return;

            // Has been calculated?
            if (dataCurrent.Object.ChangeDate.CompareTo(needsChangeAfter) < 0)
                _problems.Add("Wert " + year + " berechnet am " + dataCurrent.Object.ChangeDate.ToShortDateString());
        }

        private bool IsZero(int year)
        {
            DataValue dataCurrent = RetrieveData(year);
            
            if (dataCurrent == null || !dataCurrent.IsActualValue())
            {
                _problems.Add("Wert " + year + " ist \"0\"");
                return true;
            }

            return false;
        }

        private void PreviousEmptyOrZero(int year)
        {
            DataValue dataPrevious = RetrieveData(year - 1);
            
            if (dataPrevious == null || !dataPrevious.IsActualValue())
                _problems.Add("Wert " + (year - 1) + " nicht da oder \"0\"");
        }

        private void HighChange(int year, double allowedChange)
        {
            // Get data objects
            DataValue dataCurrent = RetrieveData(year);
            DataValue dataPrevious = RetrieveData(year - 1);

            if (dataCurrent == null || dataPrevious == null) return;
            if (!dataCurrent.IsActualValue() || !dataPrevious.IsNumericValue()) return;

            // Change margin okay?
            double change = Math.Round(dataPrevious.GetValue() / dataCurrent.GetValue(), 2);
            if (change < 1 - allowedChange || change > 1 + allowedChange)
                _problems.Add("Hohe Abweichung Vorjahr (" + change + ")");
        }
    }
}
