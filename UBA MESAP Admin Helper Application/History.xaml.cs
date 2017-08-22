using System;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using UBA.Mesap.AdminHelper.Types;
using System.Data;

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
                        
            // Go look for changes for each type of database object
            ProcessType("Bericht", "Report");
            ProcessType("Berechnungsverfahren", "CalculationMethod");
            ProcessType("Baum", "Tree");
            ProcessType("Deskriptor", "TreeObject");
            ProcessType("Zeitreihe", "TimeSeries");
            ProcessType("Zeitreihenansicht", "TimeSeriesView");
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
        /// <param name="type">String to represent type in view</param>
        /// <param name="table">Name of table objects are stored in</param>
        /// <param name="nameCol">Table column to read object's name from</param>
        /// <param name="idCol">Table column to read object's id from</param>
        /// <param name="dateCol">Table column to read last change date from</param>
        /// <param name="userCol">Table column to read user name from</param>
        private void ProcessType(String type, String table, String nameCol = "Name", String idCol = "Id",
            String dateCol = "ChangeDate", String userCol = "ChangeID")
        {
            SqlDataReader reader = null;

            try
            {
                String columns = String.Join(", ", new string[] { nameCol, idCol, dateCol, userCol });
                String query = "SELECT " + columns + " FROM " + table +
                    " WHERE ChangeDate > @after AND ChangeDate < @before AND " + userCol + " LIKE @user";

                reader = ExecuteQuery(query);
                while (reader.Read())
                    _HistoryListView.Items.Add(CreateEntry(type, reader.GetString(0), reader.GetString(1), reader.GetDateTime(2), reader.GetString(3)));
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
                reader = ExecuteQuery("SELECT TsNr, PeriodNr, ChangeDate, ChangeName FROM TimeSeriesData" +
                        " WHERE ChangeDate > @after AND ChangeDate < @before AND ChangeName LIKE @user");

                TimeSeries series;
                while (reader.Read())
                {
                    series = new TimeSeries(MesapAPIHelper.GetTimeSeries(reader.GetValue(0).ToString()));
                    _HistoryListView.Items.Add(CreateEntry(
                        "Zeitreihenwert " + (reader.GetInt32(1) + 2000),
                        series.Name,
                        series.ID + " - " + series.Legend,
                        reader.GetDateTime(2), reader.GetString(3)));
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
            SqlCommand command = new SqlCommand(query, connection);
            
            command.Parameters.Add("@after", SqlDbType.DateTime).Value = _FromDateTimePicker.Value;
            command.Parameters.Add("@before", SqlDbType.DateTime).Value = _ToDateTimePicker.Value;
            command.Parameters.Add("@user", SqlDbType.NChar).Value = '%' + _UserTextBox.Text + '%';
                        
            return command.ExecuteReader();
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
