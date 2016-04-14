using System;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using M4DBO;
using System.Collections;
using System.Data.SqlClient;
using System.Data;

namespace UBA.Mesap.AdminHelper
{
    /// <summary>
    /// Interaction logic for DatabaseComparison.xaml
    /// </summary>
    public partial class DatabaseComparison : UserControl, IDatabaseChangedObserver
    {
        public DatabaseComparison()
        {
            InitializeComponent();
            (((AdminHelper)Application.Current).Windows[0] as MainWindow).Register(this);

            // Default sorting
            _ComparisonListView.Items.SortDescriptions.Clear();
            _ComparisonListView.Items.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
        }

        private void GenerateComparison(object sender, RoutedEventArgs e)
        {
            // Prepare UI
            (((AdminHelper)Application.Current).Windows[0] as MainWindow).EnableDatabaseSelection(false);
            _GenerateComparisonButton.IsEnabled = false;
            _ComparisonListView.Items.Clear();

            Action generate = new Action(DoGenerateComparison);
            generate.BeginInvoke(null, null);
        }

        private void DoGenerateComparison()
        {
            DateTime start = DateTime.Now;
            dboRoot root = ((AdminHelper)Application.Current).root;
            IEnumerator dbs = root.InstalledDbs.GetEnumerator();
            
            while (dbs.MoveNext())
            {
                dboInstalledDB db = dbs.Current as dboInstalledDB;
                if (db.Leaf && ((AdminHelper)Application.Current).CanSwitchDatabase(db.ID))
                {
                    SqlConnection databaseConnection = ((AdminHelper)Application.Current).GetDirectDBConnection(db.ID);
                    databaseConnection.Open();
                    
                    ComparisonEntry entry = new ComparisonEntry();
                    entry.Name = db.Name;
                    entry.Dimensions = GetRowCount(databaseConnection, "Dimension");
                    entry.TreeObjects = GetRowCount(databaseConnection, "TreeObject");
                    entry.TimeSeries = GetRowCount(databaseConnection, "TimeSeries");
                    entry.Values = GetRowCount(databaseConnection, "TimeSeriesData");
                    entry.History = GetRowCount(databaseConnection, "TimeSeriesDataHistory");
                    entry.Calcs = GetRowCount(databaseConnection, "CalculationMethod");
                    entry.Views = GetRowCount(databaseConnection, "TimeSeriesView");
                    entry.Reports = GetRowCount(databaseConnection, "Report");
                    entry.Size = GetDatabaseSize(databaseConnection);
                    entry.Zeros = (int)new SqlCommand("Select count(*) from TimeSeriesData where value=0", databaseConnection).ExecuteScalar();

                    databaseConnection.Close();

                    Action<ComparisonEntry, TimeSpan> showProgress = new Action<ComparisonEntry, TimeSpan>(ShowProgress);
                    Dispatcher.BeginInvoke(DispatcherPriority.Send, showProgress, entry, DateTime.Now.Subtract(start));
                }
            }

            Action<TimeSpan> showFinished = new Action<TimeSpan>(ShowFinished);
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, showFinished, DateTime.Now.Subtract(start));
        }

        private void ShowProgress(ComparisonEntry entry, TimeSpan elapsed)
        {
            _ComparisonListView.Items.Add(entry);
            _ComparisonListView.Items.Refresh();
        }

        private void ShowFinished(TimeSpan elapsed)
        {
            (((AdminHelper)Application.Current).Windows[0] as MainWindow).EnableDatabaseSelection(true);
            _GenerateComparisonButton.IsEnabled = true;
        }

        private void CopyAll(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(MesapAPIHelper.GetListViewContentsAsCVSString(_ComparisonListView));
        }

        private int GetRowCount(SqlConnection connection, String table)
        {
            return (int)new SqlCommand("Select count(*) from " + table, connection).ExecuteScalar();
        }

        private String GetDatabaseSize(SqlConnection databaseConnection)
        {
            SqlCommand command = new SqlCommand("sp_spaceused", databaseConnection);
            command.CommandType = CommandType.StoredProcedure;

            SqlDataReader reader = command.ExecuteReader();
            reader.Read();
            String result = reader["database_size"].ToString();
            reader.Close();

            return result;
        }

        #region IDatabaseChangedObserver Members

        public void DatabaseChanged()
        {
            _ComparisonListView.Items.Clear();
        }

        #endregion
    }

    class ComparisonEntry : IExportable
    {
        public String Name { get; set; }
        public int Dimensions { get; set; }
        public int TreeObjects { get; set; }
        public int TimeSeries { get; set; }
        public int Values { get; set; }
        public int History { get; set; }
        public int Calcs { get; set; }
        public int Views { get; set; }
        public int Reports { get; set; }
        public String Size { get; set; }
        public int Zeros { get; set; }

        #region IExportable Members

        public string ToCVSString()
        {
            StringBuilder buffer = new StringBuilder();

            buffer.Append(Name + "\t");
            buffer.Append(Dimensions + "\t");
            buffer.Append(TreeObjects + "\t");
            buffer.Append(TimeSeries + "\t");
            buffer.Append(Values + "\t");
            buffer.Append(History + "\t");
            buffer.Append(Calcs + "\t");
            buffer.Append(Views + "\t");
            buffer.Append(Reports + "\t");
            buffer.Append(Size + "\t");

            return buffer.ToString();
        }

        #endregion
    }
}
