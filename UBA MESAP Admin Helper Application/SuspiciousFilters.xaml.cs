using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Input;
using M4DBO;
using M4UIO;
using System.Text;
using UBA.Mesap.AdminHelper.Types;

namespace UBA.Mesap.AdminHelper
{
    /// <summary>
    /// Interaction logic for SuspiciousFilters.xaml
    /// </summary>
    public partial class SuspiciousFilters : UserControl, IDatabaseChangedObserver
    {
        // UI root handle
        private uioRoot _uiRoot;
        
        // Database ID of search folder is given, -1 otherwise
        private int _searchFolderNumber = -1;

        public SuspiciousFilters()
        {
            InitializeComponent();
            (((AdminHelper)Application.Current).Windows[0] as MainWindow).Register(this);

            _uiRoot = ((AdminHelper)Application.Current).uiRoot;
            
            // Organize result sorting
            _SuspicousFiltersListView.Items.SortDescriptions.Clear();
            _SuspicousFiltersListView.Items.SortDescriptions.Add(new SortDescription("Path", ListSortDirection.Ascending));
            _SuspicousFiltersListView.Items.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));

            // Add double click handler
            _SuspicousFiltersListView.AddHandler(Control.MouseDoubleClickEvent, new RoutedEventHandler(ShowPropertiesDialog));
        }

        private void SelectSearchFolder(object sender, RoutedEventArgs e)
        {
            // Search folder field is set by giving reference!
            if (_uiRoot.ShowDlgFolderSelector(((AdminHelper)Application.Current).database.DbNr, M4UIO.mspUioFolderObjEnum.mspUioFolderTSView, 0, ref _searchFolderNumber))
                _SearchFolderTextBox.Text = MesapAPIHelper.GetView(Convert.ToString(_searchFolderNumber)).Name;
        }

        private void SearchSuspicousFilters(object sender, RoutedEventArgs e)
        {
            //_searchFolderNumber = Int32.Parse(_SearchFolderTextBox.Text);

            // Check back if long term operation
            if (_searchFolderNumber > -1 || (_searchFolderNumber == -1 &&
                MessageBox.Show("Ohne Einschränkung der Suche dauert diese Operation sehr lange - trotzdem starten?",
                "Suche starten", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes))
            {
                // Prepare UI
                (((AdminHelper)Application.Current).Windows[0] as MainWindow).EnableDatabaseSelection(false);
                _SearchSuspicousFiltersButton.IsEnabled = false;
                _SelectSearchFolderButton.IsEnabled = false;
                _HighCountSelect.IsEnabled = false;
                _CurrentFilterLabel.Visibility = Visibility.Visible;
                _SuspicousFiltersListView.Items.Clear();

                // Start job
                Action<int> search = new Action<int>(SearchSuspicousFilters);
                search.BeginInvoke(Convert.ToInt32(_HighCountSelect.Text), null, null);
            }
        }

        private void SearchSuspicousFilters(int highCount)
        {
            dboTSViews views = ((AdminHelper)Application.Current).database.CreateObject_TsViews(null);
            DateTime start = DateTime.Now;

            // No folder selected
            if (_searchFolderNumber == -1) views.DbReadAll();
            // Folder selected
            else views.DbReadOneFolder(_searchFolderNumber, true);

            int total = views.Count;
            int count = 1;
            int found = 0;

            foreach (dboTSView view in views)
            {
                Action<String> showProgress = new Action<String>(ShowProgress);
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, showProgress,
                    "(" + DateTime.Now.Subtract(start) + " von " +
                    new TimeSpan(DateTime.Now.Subtract(start).Ticks * total / count) + ") " +
                    "Untersuche Filter " + (count++) + " von " + total + ": " + view.Name);

                // Analyse views only (i.e. no folders)
                if (view.Leaf)
                {
                    // Create result record
                    View filter = new View(view);
                    filter.IsHighCount = filter.Count > highCount;

                    // Is suspicous?
                    if (filter.Count == 0) found++;
                                        
                    // Add to list
                    Action<View> addFilter = new Action<View>(AddFilter);
                    Dispatcher.BeginInvoke(DispatcherPriority.Normal, addFilter, filter);
                }
            }

            Action<String> showFinished = new Action<String>(ShowFinished);
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, showFinished,
                "(" + DateTime.Now.Subtract(start) + ") " +
                "Fertig - " + found + " verdächtige Filter gefunden, " + total + " insgesamt");
        }

        private void AddFilter(View filter)
        {
            _SuspicousFiltersListView.Items.Add(filter);
        }

        private void ShowProgress(String message)
        {
            _CurrentFilterLabel.Content = message;
        }

        private void ShowFinished(String message)
        {
            //_SuspicousFiltersListView.Items.Refresh();

            (((AdminHelper)Application.Current).Windows[0] as MainWindow).EnableDatabaseSelection(true);
            _SearchSuspicousFiltersButton.IsEnabled = true;
            _SelectSearchFolderButton.IsEnabled = true;
            _HighCountSelect.IsEnabled = true;
            _CurrentFilterLabel.Content = message;
        }

        private void ShowPropertiesDialog(object sender, RoutedEventArgs e)
        {
            View filter = _SuspicousFiltersListView.SelectedItem as View;
            dboTSViews views = ((AdminHelper)Application.Current).database.CreateObject_TsViews(filter.Id);
            dboTSView view = MesapAPIHelper.GetFirstView(views);
            int number = view.ViewNr;

            if (_uiRoot.ShowDlgTsView(views, M4UIO.mspDlgActionEnum.mspDlgActionEdit,
                ((AdminHelper)Application.Current).database.DbNr, true, _uiRoot.Dbo.GetFreeLockHandle(), ref number, view.Predecessor, true))
            {
                _SuspicousFiltersListView.Items.Insert(_SuspicousFiltersListView.SelectedIndex, 
                    new View(MesapAPIHelper.GetView(number), filter.Count));
                _SuspicousFiltersListView.Items.Remove(_SuspicousFiltersListView.SelectedItem);
            }
        }

        private void ShowFilterDialog(object sender, RoutedEventArgs e)
        {
            View filter = _SuspicousFiltersListView.SelectedItem as View;

            if (_uiRoot.ShowDlgTsViewSettings(((AdminHelper)Application.Current).database.DbNr, filter.Number))
            {
                _SuspicousFiltersListView.Items.Insert(_SuspicousFiltersListView.SelectedIndex, 
                    new View(MesapAPIHelper.GetView(filter.Id)));
                _SuspicousFiltersListView.Items.Remove(_SuspicousFiltersListView.SelectedItem);
            }
        }

        private void ApplyStandardSettingsAll(object sender, RoutedEventArgs e)
        {
            if (_SuspicousFiltersListView.Items.Count != 0 && 
                MessageBox.Show("Diese Aktion ändert die Einstellungen für alle gezeigten Sichten! Fortsetzen?",
                "Standardeinstellungen zurücksetzen", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Cursor = Cursors.Wait;
                int selectedIndex = _SuspicousFiltersListView.SelectedIndex;

                foreach (View filter in _SuspicousFiltersListView.Items)
                {
                    _SuspicousFiltersListView.SelectedItem = filter;
                    ApplyStandardSettings(null, null);
                }

                _SuspicousFiltersListView.SelectedIndex = selectedIndex;
                Cursor = Cursors.Arrow;
            }
        }

        private void ApplyStandardSettings(object sender, RoutedEventArgs e)
        {
            View filter = _SuspicousFiltersListView.SelectedItem as View;
            if (filter == null)
            {
                MessageBox.Show("Kein Filter gewählt", "Einstellungen zurücksetzen", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            dboTSViews views = ((AdminHelper)Application.Current).database.CreateObject_TsViews(filter.Id);
            dboTSView view = MesapAPIHelper.GetFirstView(views);
            
            // Time settings
            dboTsViewTimeSettings timeSettings = view.TimeSettingsGet();
            timeSettings.set_RangeFromPeriod(mspTimeKeyEnum.mspTimeKeyYear, -20);
            timeSettings.set_RangeToPeriod(mspTimeKeyEnum.mspTimeKeyYear, 50);
            timeSettings.RangeOnlyUsed = true;

            // View settings
            dboTSViewViewSettings viewSettings = view.ViewSettingsGet();
            viewSettings.DataMode = mspGridDataModeEnum.mspGridDataModeInputValues;

            viewSettings.DimColumns.RemoveAll();
            viewSettings.DimColumns.Add(1);
            viewSettings.DimColumns.Add(13);
            viewSettings.DimColumns.Add(4);
            viewSettings.DimModeAuto = false;

            viewSettings.ShapeColumns.RemoveAll();

            foreach (dboTsViewColumn col in viewSettings.DimColumns)
            {
                col.IsVisible = true;
                col.NameMode = mspNameModeEnum.mspNameModeName;
            }

            viewSettings.ColWidthAuto = true;
            viewSettings.SubtotalsOn = true;
            viewSettings.SubtotalsUpToCol = 1;

            // Apply if not locked
            int handle = ((AdminHelper)Application.Current).database.Root.GetFreeLockHandle();
            if (view.EnableModify(handle).Equals(mspEnableModifyResultEnum.mspEnableModifySuccess))
            {
                view.TimeSettingsSet(timeSettings);
                view.ViewSettingsSet(viewSettings);

                if (view.TsFilterGet().FilterUsage == mspFilterUsageEnum.mspFilterUsageFilterOnly)
                {
                    dboTSFilter myFilter = view.TsFilterGet();
                    myFilter.TsList = String.Empty;
                    view.TsFilterSet(myFilter);
                }

                views.DbUpdateAll(handle);
            }
            else MessageBox.Show("Die Sicht \"" + view.Name + "\" ist gespeert und kann derzeit nicht bearbeitet werden.",
                    "Standardeinstellungen zurücksetzen", MessageBoxButton.OK, MessageBoxImage.Exclamation);

            views.DisableModifyAll(handle);
        }

        private void DeleteFilter(object sender, RoutedEventArgs e)
        {
            View filter = _SuspicousFiltersListView.SelectedItem as View;
            dboTSViews views = ((AdminHelper)Application.Current).database.CreateObject_TsViews(filter.Id);
            dboTSView view = MesapAPIHelper.GetFirstView(views);

            String caption = "Filter/Sicht löschen";
            String question = "Wollen Sie den Filter (die Sicht) \"" + view.Name + "\" wirklich löschen?";

            if (MessageBox.Show(question, caption, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                int handle = ((AdminHelper)Application.Current).database.Root.GetFreeLockHandle();
                
                if (view.EnableModify(handle).Equals(mspEnableModifyResultEnum.mspEnableModifySuccess))
                {
                    views.Delete(view.ViewNr);
                    views.DbUpdateAll(handle);

                    _SuspicousFiltersListView.Items.Remove(_SuspicousFiltersListView.SelectedItem);
                }
                else MessageBox.Show("Filter (Sicht) ist gespeert und kann nicht gelöscht werden.",
                    caption, MessageBoxButton.OK, MessageBoxImage.Exclamation);

                views.DisableModifyAll(handle);
            }
        }

        private void CopyAll(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(MesapAPIHelper.GetListViewContentsAsCVSString(_SuspicousFiltersListView));
        }

        #region IDatabaseChangedObserver Members

        public void DatabaseChanged()
        {
            _SuspicousFiltersListView.Items.Clear();
            _searchFolderNumber = -1;
        }

        #endregion
    }

    /// <summary>
    /// Class represents a search result record and wraps a dboTSView.
    /// </summary>
    class View : IExportable
    {
        private dboTSView _view;
        private Filter _filter;

        /// <summary>
        /// Creates new filter.
        /// </summary>
        /// <param name="view">Time series view to wrap.</param>
        public View(dboTSView view)
        {
            _view = view;
            _filter = new Filter(view.TsFilterGet());
        }

        /// <summary>
        /// Creates new filter with count cache prefilled.
        /// </summary>
        /// <param name="view">Time series view to wrap.</param>
        /// <param name="timeSeriesCount">Predefinied count value. Will not be checked.</param>
        public View(dboTSView view, int timeSeriesCount)
        {
            _view = view;
            _filter = new Filter(view.TsFilterGet(), timeSeriesCount);
        }

        /// <summary>
        /// Get the database ID of wrapped view.
        /// </summary>
        public int Number
        {
            get { return _view.ViewNr; }
        }

        /// <summary>
        /// Get the name of wrapped view
        /// </summary>
        public string Name
        {
            get { return _view.Name; }
        }

        /// <summary>
        /// Get the ID of wrapped view
        /// </summary>
        public string Id
        {
            get { return _view.ID; }
        }

        /// <summary>
        /// Calculates and returns the folder path to view wrapped
        /// </summary>
        public string Path
        {
            get
            {
                String path = "";

                int predecessor = _view.Predecessor;
                while (predecessor > 0)
                {
                    dboTSView parent = MesapAPIHelper.GetView(predecessor);
                    path = parent.Name + "/" + path;
                    predecessor = parent.Predecessor;
                }

                return path;
            }
        }

        /// <summary>
        /// Calculates the number of timeseries filtered by view wrapped. Cached.
        /// </summary>
        public int Count
        {
            get
            {
                return _filter.Count;
            }
        }

        /// <summary>
        /// Check whether the number of timeseries returned is considered high.
        /// </summary>
        public bool IsHighCount { get; set; }

        /// <summary>
        /// Filter mode of wrapped view, i.e. usage of filter and/or list
        /// </summary>
        public String Mode
        {
            get
            {
                String mode = "";

                switch (_filter.Object.FilterUsage)
                {
                    case mspFilterUsageEnum.mspFilterUsageFilterAndList:
                        mode = "Filter und Liste"; break;
                    case mspFilterUsageEnum.mspFilterUsageFilterOnly:
                        mode = "Nur Filter"; break;
                    case mspFilterUsageEnum.mspFilterUsageFilterOrList:
                        mode = "Filter oder Liste"; break;
                    case mspFilterUsageEnum.mspFilterUsageListOnly:
                        mode = "Nur Liste"; break;
                }

                return mode;
            }
        }

        /// <summary>
        /// Returns view's creation date duly formatted.
        /// </summary>
        public String CreateDate
        {
            get
            {
                return _view.CreateDate.ToShortDateString();
            }
        }

        /// <summary>
        /// Returns view's change date duly formatted.
        /// </summary>
        public String ChangeDate
        {
            get
            {
                return _view.ChangeDate.ToShortDateString();
            }
        }

        #region IExportable Members

        public string ToCVSString()
        {
            StringBuilder buffer = new StringBuilder();

            buffer.Append(Path + "\t");
            buffer.Append(Name + "\t");
            buffer.Append(Id + "\t");
            buffer.Append(Count + "\t");
            buffer.Append(Mode + "\t");
            buffer.Append(CreateDate + "\t");
            buffer.Append(ChangeDate + "\t");

            return buffer.ToString();
        }

        #endregion
    }
}
