using System;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using UBA.Mesap.AdminHelper.Types;

namespace UBA.Mesap.AdminHelper
{
    /// <summary>
    /// Interaction logic for History.xaml
    /// This User Control displays the working history
    /// for the selected database.
    /// </summary>
    public partial class History : UserControl, IDatabaseChangedObserver
    {
        public History()
        {
            InitializeComponent();
            (((AdminHelper)Application.Current).Windows[0] as MainWindow).Register(this);

            // Init time values
            _FromDateTimePicker.Value = DateTime.Now.Subtract(DateTime.Now.TimeOfDay);
            _ToDateTimePicker.Value = DateTime.Now.Add(new TimeSpan(TimeSpan.TicksPerDay - 1)).
                Subtract(DateTime.Now.TimeOfDay);
            
            // Organize result sorting
            _HistoryListView.Items.SortDescriptions.Clear();
            _HistoryListView.Items.SortDescriptions.Add(new SortDescription("Date", ListSortDirection.Descending));
            _HistoryListView.Items.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));

            // Add event handlers
            _UserTextBox.KeyDown += new KeyEventHandler(delegate(object s, KeyEventArgs keyEvent)
            {
                if (keyEvent.Key == Key.Return)
                    ShowHistory(null, null);
            });
        }

        /// <summary>
        /// Starts generation of history
        /// </summary>
        /// <param name="sender">Not used</param>
        /// <param name="e">Not used</param>
        private void ShowHistory(object sender, RoutedEventArgs e)
        {
            DateTime start = DateTime.Now;
            Cursor = Cursors.Wait;
            _HistoryListView.Items.Clear();
                        
            // Reports
            ProcessType("Report", "Bericht", 2, 1, 14, "ChangeID", 13);

            // Calculations
            ProcessType("CalculationMethod", "Berechnungsverfahren", 2, 1, 11, "ChangeID", 10);

            // Trees
            ProcessType("Tree", "Baum", 2, 1, 8, "ChangeID", 7);

            // Descriptors
            ProcessType("TreeObject", "Deskriptor", 2, 1, 12, "ChangeID", 11);
            
            // TimeSeries
            ProcessType("TimeSeries", "Zeitreihe", 3, 2, 13, "ChangeID", 11);

            // Views
            ProcessType("TimeSeriesView", "Zeitreihenansicht", 2, 1, 16, "ChangeID", 15);

            // CRF Variables
            ProcessType("CrfVariable", "CRF-Filter", 2, 4, 14, "ChangeName", 13);
            
            // Values (extra code for timeseries relation)
            if ((bool)_ShowValuesCheckBox.IsChecked)
                ProcessValues();
            
            // Update UI
            _HistoryListView.Items.Refresh();
            _StatusLabel.Visibility = Visibility.Visible;
            _StatusLabel.Content = "(" + DateTime.Now.Subtract(start) + ") Fertig - " + 
                _HistoryListView.Items.Count + " Einträge";
            Cursor = Cursors.Arrow;
        }

        /// <summary>
        /// Generates history for a certain type of object
        /// </summary>
        /// <param name="table">Name of table objects are stored in</param>
        /// <param name="type">String to represent type in view</param>
        /// <param name="nameCol">Table column to read object's name from</param>
        /// <param name="idCol">Table column to read object's id from</param>
        /// <param name="dateCol">Table column to read last change date from</param>
        /// <param name="userCol">Table column to read user name from</param>
        private void ProcessType(String table, String type, int nameCol, int idCol, int dateCol, String userColName, int userCol)
        {
            SqlDataReader reader = null;

            try
            {
                reader = ExecuteQuery("SELECT * FROM " + table + " WHERE (ChangeDate > '" + _FromDateTimePicker.Value + "'" +
                    " AND ChangeDate < '" + _ToDateTimePicker.Value + "'" +
                    " AND " + userColName + " LIKE '%" + _UserTextBox.Text + "%')");
                
                while (reader.Read())
                    _HistoryListView.Items.Add(CreateEntry(type, reader.GetString(nameCol),
                        reader.GetString(idCol), reader.GetDateTime(dateCol), reader.GetString(userCol)));
            }
            catch (Exception ex)
            {
                Console.WriteLine("OOPS: " + ex.Message);
                _HistoryListView.Items.Add(CreateEntry(type, "ACHTUNG!", ex.Message, DateTime.Today.AddDays(1), ""));
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
        }

        private void ProcessValues()
        {
            SqlDataReader reader = null;

            try
            {
                reader = ExecuteQuery("SELECT TsNr, PeriodNr, ChangeDate, ChangeName FROM TimeSeriesData WHERE (ChangeDate > '" + _FromDateTimePicker.Value + "'" +
                        " AND ChangeDate < '" + _ToDateTimePicker.Value + "'" +
                        " AND ChangeName LIKE '%" + _UserTextBox.Text + "%')");

                TimeSeries series;
                while (reader.Read())
                {
                    series = new TimeSeries(MesapAPIHelper.GetTimeSeries(reader.GetValue(0).ToString()));
                    _HistoryListView.Items.Add(CreateEntry("Zeitreihenwert " + (reader.GetInt32(1) + 2000),
                    series.Name, series.ID + " - " + series.Legend, reader.GetDateTime(2), reader.GetString(3)));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("OOPS: " + ex.Message);
                _HistoryListView.Items.Add(CreateEntry("Zeitreihenwert", "ACHTUNG!", ex.Message, DateTime.Today.AddDays(1), ""));
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
        }

        private SqlDataReader ExecuteQuery(String query)
        {
            SqlConnection connection = ((AdminHelper)Application.Current).GetDirectDBConnection();
            
            return new SqlCommand(query, connection).ExecuteReader();
        }

        /// <summary>
        /// Create single entry in history and fill fields with data provided
        /// </summary>
        /// <param name="type">Type of this entry</param>
        /// <param name="name">Name of this entry</param>
        /// <param name="id">ID of this entry</param>
        /// <param name="date">Change date of this entry</param>
        /// <param name="user">User name of this entry</param>
        /// <returns></returns>
        private HistoryEntry CreateEntry(String type, String name, String id, DateTime date, String user)
        {
            HistoryEntry entry = new HistoryEntry();

            entry.Type = type;
            entry.Name = name + " (" + id + ")";
            entry.Date = date;
            entry.User = user;

            return entry;
        }

        /// <summary>
        /// Copy handler.
        /// </summary>
        /// <param name="sender">Not used</param>
        /// <param name="e">Not used</param>
        private void CopyAll(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(MesapAPIHelper.GetListViewContentsAsCVSString(_HistoryListView));
        }

        #region IDatabaseChangedObserver Members

        public void DatabaseChanged()
        {
            _HistoryListView.Items.Clear();
            _StatusLabel.Visibility = Visibility.Hidden;
        }

        #endregion
    }

    /// <summary>
    /// Class represents a single entry (i.e. a single row) in history view.
    /// Contains field and some helpers only.
    /// </summary>
    class HistoryEntry : IExportable
    {
        /// <summary>
        /// Type of object represented
        /// </summary>
        public String Type { get; set; }
        
        /// <summary>
        /// Name of object represented
        /// </summary>
        public String Name { get; set; }
        
        /// <summary>
        /// Change date of object represented
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// User who changed object represented
        /// </summary>
        public String User { get; set; }

        /// <summary>
        /// Returns change date as formatted string.
        /// </summary>
        public String FormattedDate
        {
            get
            {
                return "Am " + Date.ToShortDateString() + " um " + Date.ToShortTimeString() + " Uhr";
            }
        }
        
        #region IExportable Members

        public string ToCVSString()
        {
            StringBuilder buffer = new StringBuilder();

            buffer.Append(Type + "\t");
            buffer.Append(Name + "\t");
            buffer.Append(Date + "\t");
            buffer.Append(User + "\t");

            return buffer.ToString();
        }

        #endregion
    }
}
