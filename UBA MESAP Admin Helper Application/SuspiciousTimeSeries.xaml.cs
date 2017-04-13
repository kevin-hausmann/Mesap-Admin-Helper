using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using M4DBO;
using M4UIO;
using System.Windows.Threading;
using System.ComponentModel;
using UBA.Mesap.AdminHelper.Types;

namespace UBA.Mesap.AdminHelper
{
    /// <summary>
    /// Interaction logic for SuspicicousTimeSeries.xaml
    /// </summary>
    public partial class SuspiciousTimeSeries : UserControl, IDatabaseChangedObserver
    {
        private dboTSFilter filter;
        private int from = 1900; 
        private int to = 2100;

        // UI root handle
        private uioRoot _uiRoot;

        public SuspiciousTimeSeries()
        {
            InitializeComponent();
            (((AdminHelper)Application.Current).Windows[0] as MainWindow).Register(this);

            _uiRoot = ((AdminHelper)Application.Current).uiRoot;
            filter = ((AdminHelper)Application.Current).database.CreateObject_TSFilter();
            //filter.FilterUsage = mspFilterUsageEnum.mspFilterUsageFilterOnly;
            
            // Organize result sorting
            _TimeSeriesListView.Items.SortDescriptions.Clear();
            _TimeSeriesListView.Items.SortDescriptions.Add(new SortDescription("Legend", ListSortDirection.Ascending));

            // Add double click handler
            _TimeSeriesListView.AddHandler(Control.MouseDoubleClickEvent, new RoutedEventHandler(ShowValues));
        }

        private void SearchSuspiciousTimeSeries(object sender, RoutedEventArgs e)
        {
            // Use filter from admin tool view in database, if any
            dboTSViews views = ((AdminHelper)Application.Current).database.CreateObject_TsViews("AdminTool");
            if (views.Count == 1)
            {
                dboTSView view = MesapAPIHelper.GetFirstView(views);
                filter = view.TsFilterGet();
            }
            
            int count = MesapAPIHelper.GetTimeSeriesCount(filter);

            // Check back if long term operation
            if (count < 1000 || (count >= 1000 &&
                MessageBox.Show("Ohne Einschränkung der Suche dauert diese Operation sehr lange - trotzdem starten?",
                "Suche starten", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes))
            {
                // Prepare UI
                (((AdminHelper)Application.Current).Windows[0] as MainWindow).EnableDatabaseSelection(false);
                _StartSearchButton.IsEnabled = false;
                _CurrentTimeSeriesLabel.Visibility = Visibility.Visible;
                _TimeSeriesListView.Items.Clear();

                // Start job
                Action search = new Action(SearchSuspiciousTimeSeries);
                search.BeginInvoke(null, null);
            }
        }

        private void SearchSuspiciousTimeSeries()
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

                Action<String> showProgress = new Action<String>(ShowProgress);
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, showProgress,
                    "(" + DateTime.Now.Subtract(start) + " von " +
                    new TimeSpan(DateTime.Now.Subtract(start).Ticks * total / count) + ") " +
                    "Untersuche Zeitreihe " + (count++) + " von " + total + ": " + timeSeries.Name);

                // Add to list
                Action<SuspectedTimeSeries> addSeries = new Action<SuspectedTimeSeries>(AddTimeSeries);
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, addSeries, new SuspectedTimeSeries(timeSeries, from, to));
            }
           
            Action<String> showFinished = new Action<String>(ShowFinished);
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, showFinished, "Fertig - " + (count-1) + " Zeitreihe(n) untersucht");
        }

        private void AddTimeSeries(SuspectedTimeSeries timeSeries)
        {
            _TimeSeriesListView.Items.Add(timeSeries);
            
            if (Tab.IsScrolledToBottom(_TimeSeriesListView))
                _TimeSeriesListView.ScrollIntoView(timeSeries);
        }

        private void ShowProgress(String message)
        {
            _CurrentTimeSeriesLabel.Content = message;
        }
        
        private void ShowFinished(String message)
        {
            if ((bool) _SortCheck.IsChecked) _TimeSeriesListView.Items.Refresh();

            (((AdminHelper)Application.Current).Windows[0] as MainWindow).EnableDatabaseSelection(true);
            _StartSearchButton.IsEnabled = true;

            _CurrentTimeSeriesLabel.Content = message;
        }

        private void ShowPropertiesDialog(object sender, RoutedEventArgs e)
        {
            SuspectedTimeSeries series = _TimeSeriesListView.SelectedItem as SuspectedTimeSeries;
            if (series == null) return;

            dboTSViews views = MesapAPIHelper.GetAdminHelperView();
            dboTSView view = MesapAPIHelper.GetFirstView(views);

            AdminHelper application = ((AdminHelper)Application.Current);
            int handle = application.root.GetFreeLockHandle();
            view.EnableModify(handle);

            dboTSFilter filter = ((AdminHelper)Application.Current).database.CreateObject_TSFilter();
            filter.TsList = Convert.ToString(series.Object.TsNr);
            filter.FilterUsage = mspFilterUsageEnum.mspFilterUsageListOnly;
            view.TsFilterSet(filter);
            view.DisableModify(handle);
            views.DbUpdateAll(handle);

            _uiRoot.ShowFormTimeseriesEdit(((AdminHelper)Application.Current).database.DbNr, view.ViewNr);
            
            _TimeSeriesListView.Items.Insert(_TimeSeriesListView.SelectedIndex,
                new SuspectedTimeSeries(MesapAPIHelper.GetTimeSeries(series.ID), from, to));
            _TimeSeriesListView.Items.Remove(_TimeSeriesListView.SelectedItem);
        }

        private void ShowValues(object sender, RoutedEventArgs e)
        {
            SuspectedTimeSeries series = _TimeSeriesListView.SelectedItem as SuspectedTimeSeries;

            if (series != null)
            {
                // Find "DeleteValues" tab, switch to it and show time series' contents
                TabControl mainTabControl = (TabControl)((TabItem)this.Parent).Parent;
                foreach (TabItem item in mainTabControl.Items)
                {
                    DeleteValues deleteValuesTab = item.Content as DeleteValues;
                    if (deleteValuesTab != null)
                    {
                        item.IsSelected = true;
                        deleteValuesTab.ShowValues(series.ID);
                        break;
                    }
                }
            }
            else MessageBox.Show("Keine Zeitreihe gewählt", "Werte anzeigen", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void ConsolidateHistory(object sender, RoutedEventArgs e)
        {
            int count = 0;

            SuspectedTimeSeries series = _TimeSeriesListView.SelectedItem as SuspectedTimeSeries;
            foreach (dboTSData data in series.Object.TSDatas)
                count += new DataValue(data).ConsolidateHistory();
            
            MessageBox.Show(count + " Werte aus der Historie gelöscht", "Historie konsolidieren", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ConsolidateAllHistories(object sender, RoutedEventArgs e)
        {
            int count = 0;

            if (MessageBox.Show("Konsolidierung für alle gelisteten Zeitreihen durchführen?",
                "Historie aufräumen", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                foreach (Object item in _TimeSeriesListView.Items)
                {
                    SuspectedTimeSeries series = item as SuspectedTimeSeries;
                    foreach (dboTSData data in series.Object.TSDatas)
                        count += new DataValue(data).ConsolidateHistory();
                }
            }

            MessageBox.Show(count + " Werte aus der Historie gelöscht", "Historie konsolidieren", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CopyAll(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(MesapAPIHelper.GetListViewContentsAsCVSString(_TimeSeriesListView));
        }

        #region IDatabaseChangedObserver Members

        public void DatabaseChanged()
        {
            _TimeSeriesListView.Items.Clear();
            filter = ((AdminHelper)Application.Current).database.CreateObject_TSFilter();
        }

        #endregion
    }

    class SuspectedTimeSeries : TimeSeries, IExportable
    {
        public SuspectedTimeSeries(dboTS series, int from, int to) : base(series, from, to) { }

        public int RealValues
        {
            get
            {
                int count = 0;
                double test;

                foreach (dboTSData data in Object.TSDatas)
                    if (data.Value != null && Double.TryParse(data.Value.ToString(), out test) &&
                        Convert.ToDouble(data.Value) != 0 && data.NoValueReason == 0) count++;

                return count;
            }
        }

        public int ZeroValues
        {
            get
            {
                int count = 0;
                double test;

                foreach (dboTSData data in Object.TSDatas)
                    if (data.Value != null && Double.TryParse(data.Value.ToString(), out test) &&
                        Convert.ToDouble(data.Value) == 0 && data.NoValueReason == 0) count++;

                return count;
            }
        }

        public int KeyValues
        {
            get
            {
                int count = 0;
                
                foreach (dboTSData data in Object.TSDatas)
                    if (data.NoValueReason > 0) count++;

                return count;
            }
        }

        public int DeletedValues
        {
            get
            {
                int count = 0;

                foreach (dboTSData data in Object.TSDatas)
                    if (data.NoValueReason == -1) count++;

                return count;
            }
        }

        public String HasDocumentation
        {
            get
            {
                foreach (dboTSProperty property in Object.TSProperties)
                    if (property.AnnexObjNr > 0) return "Ja";

                return "Nein";
            }
        }

        public int ObsoleteHistoryEntries
        {
            get
            {
                int count = 0;

                foreach (dboTSData data in Object.TSDatas)
                    count += MesapAPIHelper.ConsolidateHistory(new DataValue(data).GetHistory()).Count;

                return count;
            }
        }

        public String HasInvalidUncertainties
        {
            get
            {
                if (!HasUncertaintyDocumentation()) return "Keine";
                else if (!HasCompleteUncertaintyDocumentation()) return "Unvollständig";
                else if (UncertaintyLowerLimit.Equals(0) || UncertaintyUpperLimit.Equals(0)) return "Umax | Umin = 0";
                else if (UncertaintyLowerLimit >= 100) return "Umin >= 100";
                else if (UncertaintyDistribution.Equals(Distribution.normal) &&
                    (!UncertaintyLowerLimit.Equals(UncertaintyUpperLimit))) return "Umax != Umin";
                else return "Okay";
            }
        }

        #region IExportable Members

        public string ToCVSString()
        {
            StringBuilder buffer = new StringBuilder();

            buffer.Append(Name + "\t");
            buffer.Append(ID + "\t");
            buffer.Append(Legend + "\t");
            buffer.Append(RealValues + "\t");
            buffer.Append(ZeroValues + "\t");
            buffer.Append(KeyValues + "\t");
            buffer.Append(DeletedValues + "\t");
            buffer.Append(HasDocumentation + "\t");
            buffer.Append(ObsoleteHistoryEntries + "\t");
            buffer.Append(HasInvalidUncertainties + "\t");
            
            return buffer.ToString();
        }

        #endregion
    }
}
