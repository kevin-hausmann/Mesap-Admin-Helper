using System;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using M4DBO;

namespace UBA.Mesap.AdminHelper
{
    /// <summary>
    /// Interaction logic for Hitlist.xaml
    /// </summary>
    public partial class Hitlist : UserControl, IDatabaseChangedObserver
    {
        public Hitlist()
        {
            InitializeComponent();
            (((AdminHelper)Application.Current).Windows[0] as MainWindow).Register(this);

            // Default sorting
            _HitlistListView.Items.SortDescriptions.Clear();
            _HitlistListView.Items.SortDescriptions.Add(new SortDescription("Count", ListSortDirection.Descending));
            _HitlistListView.Items.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));

            // Add double click handler
            _HitlistListView.AddHandler(Control.MouseDoubleClickEvent, new RoutedEventHandler(ShowPropertiesDialog));
        }

        private void GenerateHitlist(object sender, RoutedEventArgs e)
        {
            // Prepare UI
            (((AdminHelper)Application.Current).Windows[0] as MainWindow).EnableDatabaseSelection(false);
            _GenerateHitlistButton.IsEnabled = false;
            _HitlistStatusLabel.Visibility = Visibility.Visible;

            _HitlistListView.Items.Clear();

            Action generate = new Action(DoGenerateHitlist);
            generate.BeginInvoke(null, null);
        }

        private void DoGenerateHitlist()
        {
            DateTime start = DateTime.Now;
            dboDimension dimension;

            foreach (dboTreeObject treeObject in ((AdminHelper)Application.Current).database.TreeObjects)
            {
                HitlistEntry entry = new HitlistEntry();
                entry.Nr = treeObject.ObjNr;
                entry.DimNr = treeObject.DimNr;
                entry.Name = treeObject.Name;
                entry.Id = treeObject.ID;

                dimension = ((AdminHelper)Application.Current).database.Dimensions[treeObject.DimNr];
                entry.Dimension = (dimension == null ? "<mehrdimensional>" : dimension.Name);

                entry.Count = ((AdminHelper)Application.Current).database.TreeObjects.DbGetDependentTsCount(treeObject.ObjNr);

                int treeUse = 0;
                mspCheckDeleteEnum checkDelete = mspCheckDeleteEnum.mspChkDelAllowed;
                ((AdminHelper)Application.Current).database.TreeObjects.CheckDelete(treeObject.ObjNr, ref checkDelete, ref treeUse);
                entry.TreeUse = treeUse;

                Action<HitlistEntry, TimeSpan> showProgress = new Action<HitlistEntry, TimeSpan>(ShowProgress);
                Dispatcher.BeginInvoke(DispatcherPriority.Send, showProgress, entry, DateTime.Now.Subtract(start));
            }

            Action<TimeSpan> showFinished = new Action<TimeSpan>(ShowFinished);
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, showFinished, DateTime.Now.Subtract(start));
        }

        private void ShowProgress(HitlistEntry entry, TimeSpan elapsed)
        {
            _HitlistListView.Items.Add(entry);

            int count = _HitlistListView.Items.Count;
            int total = ((AdminHelper)Application.Current).database.TreeObjects.Count;

            if (count % 50 == 0 || count == ((AdminHelper)Application.Current).database.TreeObjects.Count)
                _HitlistListView.Items.Refresh();
            
            _HitlistStatusLabel.Content = "(" + elapsed + " von " + new TimeSpan(elapsed.Ticks * total / count) + ") " + 
                "Deskriptor " + count + " von " + total + ": " + entry.Name;
        }

        private void ShowFinished(TimeSpan elapsed)
        {
            _HitlistStatusLabel.Content = "(" + elapsed + ") Fertig - " + _HitlistListView.Items.Count + " Deskriptoren";
            
            (((AdminHelper)Application.Current).Windows[0] as MainWindow).EnableDatabaseSelection(true);
            _GenerateHitlistButton.IsEnabled = true;
        }

        private void ShowPropertiesDialog(object sender, RoutedEventArgs e)
        {
            if (_HitlistListView.SelectedIndex == -1) return;

            HitlistEntry entry = _HitlistListView.SelectedItem as HitlistEntry;
            int nr = entry.Nr;

            AdminHelper application = ((AdminHelper)Application.Current);
            application.uiRoot.ShowDlgTreeObject(M4UIO.mspDlgActionEnum.mspDlgActionEdit,
                application.database.DbNr, true, application.root.GetFreeLockHandle(),
                ref nr, entry.DimNr, mspObjTypeEnum.mspObjTypeDescriptor);

            int dim = entry.DimNr;
            dboTreeObject treeObject = ((AdminHelper)Application.Current).database.TreeObjects[entry.Nr, dim];

            entry.Name = treeObject.Name;
            entry.Id = treeObject.ID;

            _HitlistListView.Items.Refresh();
        }

        private void DeleteDescriptor(object sender, RoutedEventArgs e)
        {
            if (_HitlistListView.SelectedIndex == -1) return;

            HitlistEntry entry = _HitlistListView.SelectedItem as HitlistEntry;
            int dimension = entry.DimNr;
            dboTreeObject descriptor = ((AdminHelper)Application.Current).database.TreeObjects[entry.Id, dimension];

            String caption = "Deskriptor löschen";
            String question = "Wollen Sie den Deskriptor \"" + descriptor.Name + "\" wirklich löschen?";

            if (entry.TreeUse != 0 || entry.Count != 0)
                MessageBox.Show("Dieser Deskriptor wird verwendet und kann nicht gelöscht werden!",
                    "Löschen nicht möglich", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            else if (MessageBox.Show(question, caption, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                int handle = ((AdminHelper)Application.Current).database.Root.GetFreeLockHandle();
                descriptor.EnableModify(handle);
                ((AdminHelper)Application.Current).database.TreeObjects.Delete(descriptor.ObjNr);
                ((AdminHelper)Application.Current).database.TreeObjects.DbUpdateAll(handle);

                _HitlistListView.Items.Remove(_HitlistListView.SelectedItem);
            }
        }

        private void ShowTimeSeriesDialog(object sender, RoutedEventArgs e)
        {
            if (_HitlistListView.SelectedIndex == -1) return;

            HitlistEntry entry = _HitlistListView.SelectedItem as HitlistEntry;
            dboTSViews views = MesapAPIHelper.GetAdminHelperView();
            dboTSView view = MesapAPIHelper.GetFirstView(views);

            AdminHelper application = ((AdminHelper)Application.Current);
            int handle = application.root.GetFreeLockHandle();
            view.EnableModify(handle);

            dboTreeObjectFilter myFilter = ((AdminHelper)Application.Current).database.CreateObject_TreeObjectFilter();
            myFilter.DimNr = entry.DimNr;
            myFilter.Numbers = Convert.ToString(entry.Nr);

            dboTSFilter filter = ((AdminHelper)Application.Current).database.CreateObject_TSFilter();
            filter.Add(myFilter);
            filter.FilterUsage = mspFilterUsageEnum.mspFilterUsageFilterOnly;
            view.TsFilterSet(filter);
            view.DisableModify(handle);
            views.DbUpdateAll(handle);

            application.uiRoot.ShowFormDataSheet(application.database.DbNr, view.ViewNr, false);
        }

        private void SortByDimensionCountName(object sender, RoutedEventArgs e)
        {
            _HitlistListView.Items.SortDescriptions.Clear();
            _HitlistListView.Items.SortDescriptions.Add(new SortDescription("Dimension", ListSortDirection.Ascending));
            _HitlistListView.Items.SortDescriptions.Add(new SortDescription("Count", ListSortDirection.Descending));
            _HitlistListView.Items.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
            _HitlistListView.Items.Refresh();
        }

        private void SortByDimensionName(object sender, RoutedEventArgs e)
        {
            _HitlistListView.Items.SortDescriptions.Clear();
            _HitlistListView.Items.SortDescriptions.Add(new SortDescription("Dimension", ListSortDirection.Ascending));
            _HitlistListView.Items.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
            _HitlistListView.Items.Refresh();
        }

        private void SortByCountName(object sender, RoutedEventArgs e)
        {
            _HitlistListView.Items.SortDescriptions.Clear();
            _HitlistListView.Items.SortDescriptions.Add(new SortDescription("Count", ListSortDirection.Descending));
            _HitlistListView.Items.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
            _HitlistListView.Items.Refresh();
        }

        private void CopyAll(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(MesapAPIHelper.GetListViewContentsAsCVSString(_HitlistListView));
        }

        #region IDatabaseChangedObserver Members

        public void DatabaseChanged()
        {
            _HitlistListView.Items.Clear();
        }

        #endregion
    }

    class HitlistEntry : IExportable
    {
        public String Dimension { get; set; }
        public String Name { get; set; }
        public String Id { get; set; }
        public int Count { get; set; }
        public int TreeUse { get; set; }
        public int DimNr { get; set; }
        public int Nr { get; set; }

        #region IExportable Members

        public string ToCVSString()
        {
            StringBuilder buffer = new StringBuilder();

            buffer.Append(Dimension + "\t");
            buffer.Append(Name + "\t");
            buffer.Append(Id + "\t");
            buffer.Append(Count + "\t");
            buffer.Append(TreeUse + "\t");
            
            return buffer.ToString();
        }

        #endregion
    }
}
