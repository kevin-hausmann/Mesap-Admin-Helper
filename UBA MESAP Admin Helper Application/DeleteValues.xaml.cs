using System;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Windows.Input;
using M4DBO;
using System.Collections;
using System.ComponentModel;
using UBA.Mesap.AdminHelper.Types;

namespace UBA.Mesap.AdminHelper
{
    /// <summary>
    /// Interaction logic for DeleteValues.xaml
    /// User control for the deletion of values. Uses direct database access.
    /// </summary>
    public partial class DeleteValues : UserControl, IDatabaseChangedObserver
    {
        // Lower end of time range considered
        private int from;

        // Upper end of time range considered
        private int to;

        public DeleteValues()
        {
            InitializeComponent();
            (((AdminHelper)Application.Current).Windows[0] as MainWindow).Register(this);

            // Set time range
            from = ((AdminHelper)Application.Current).database.Units.TkDateToPeriod(new DateTime(1900, 1, 1), mspTimeKeyEnum.mspTimeKeyYear, false);
            to = ((AdminHelper)Application.Current).database.Units.TkDateToPeriod(new DateTime(2100, 1, 1), mspTimeKeyEnum.mspTimeKeyYear, false);
            
            // Organize result sorting
            _ValuesListView.Items.SortDescriptions.Clear();
            _ValuesListView.Items.SortDescriptions.Add(new SortDescription("Timepoint", ListSortDirection.Ascending));

            // Add event handlers
            _TimeSeriesTextBox.KeyDown += new KeyEventHandler(delegate(object s, KeyEventArgs keyEvent)
            {
                if (keyEvent.Key == Key.Return)
                    ShowValues(null, null);
            });
            _ValuesListView.AddHandler(Control.MouseDoubleClickEvent, new RoutedEventHandler(DeleteSelectedValue));
        }

        public void ShowValues(String id)
        {
            _TimeSeriesTextBox.Text = id;
            ShowValues(null, null);
        }

        private void ShowValues(object sender, RoutedEventArgs e)
        {
            _ValuesListView.Items.Clear();

            dboTS serie = MesapAPIHelper.GetTimeSeries(_TimeSeriesTextBox.Text);
            if (serie != null)
            {
                serie.DbReadRelatedKeys();
                _TimeSeriesNameLabel.Content = serie.Name + " (" + serie.BuildTsLegend(
                    false, mspNameModeEnum.mspNameModeId, mspSortModeEnum.mspSortModeAlpha) + ")";

                foreach (dboHypo hypo in ((AdminHelper)Application.Current).database.Hypos)
                {
                    // Input
                    serie.DbReadRelatedDatas(false, mspDataTypeEnum.mspDataTypeInput, mspTimeKeyEnum.mspTimeKeyYear,
                        from, to, null, Convert.ToString(hypo.HypoNr), null);

                    // Result
                    serie.DbReadRelatedDatas(false, mspDataTypeEnum.mspDataTypeResult, mspTimeKeyEnum.mspTimeKeyYear,
                        from, to, null, Convert.ToString(hypo.HypoNr + 1), null);
                }

                // Add data items
                foreach (dboTSData data in serie.TSDatas)
                    _ValuesListView.Items.Add(new CheckedDataValue(data));

                _ValuesListView.Items.Refresh();
            }
            else
            {
                _TimeSeriesNameLabel.Content = "";
                MessageBox.Show("Keine Zeitreihe mit dieser ID vorhaben", "Daten auflisten",
                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        private void DeleteSelectedValue(object sender, RoutedEventArgs e)
        {
            CheckedDataValue data = _ValuesListView.SelectedItem as CheckedDataValue;
            if (data == null) return;

            String caption = "Wert löschen";

            if (data.Object.AnnexObjNr > 0)
                MessageBox.Show("Dieser Wert ist dokumentiert und kann nicht gelöscht werden.",
                    caption, MessageBoxButton.OK, MessageBoxImage.Exclamation);
            else
            {
                String question = "Wollen Sie den Wert \"" + data.Timepoint + ": " +
                    data.Value + "\" wirklich löschen?";

                if (MessageBox.Show(question, caption, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    if (DeleteDataValue(data))
                        _ValuesListView.Items.Remove(_ValuesListView.SelectedItem);
                    else MessageBox.Show("Der Löschversuch ist fehlgeschlagen", caption, 
                        MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteAllDELValues(object sender, RoutedEventArgs e)
        {
            String caption = "Alle DEL-Werte löschen";
            String question = "Wollen Sie alle DEL-Werte in der Zeitreihe löschen?";
            ArrayList valuesToBeDeleted = new ArrayList();
            
            // Find candidates
            foreach (CheckedDataValue data in _ValuesListView.Items)
                if (data.Value.Equals("DEL")) valuesToBeDeleted.Add(data);

            // Delete
            DeleteMultipleValues(valuesToBeDeleted, caption, question);
        }

        

        private void DeleteAllERRValues(object sender, RoutedEventArgs e)
        {
            String caption = "Alle ERR-Werte löschen";
            String question = "Wollen Sie alle ERR-Werte in der Zeitreihe löschen?";
            ArrayList valuesToBeDeleted = new ArrayList();

            // Find candidates
            foreach (CheckedDataValue data in _ValuesListView.Items)
                if (data.Value.Equals("ERR")) valuesToBeDeleted.Add(data);

            // Delete
            DeleteMultipleValues(valuesToBeDeleted, caption, question);
        }

        private void DeleteAllInputValues(object sender, RoutedEventArgs e)
        {
            String caption = "Alle eingegebenen Werte löschen";
            String question = "Wollen Sie alle eingegebenen Werte in der Zeitreihe löschen?";
            ArrayList valuesToBeDeleted = new ArrayList();

            // Find candidates
            foreach (CheckedDataValue data in _ValuesListView.Items)
                valuesToBeDeleted.Add(data);

            // Delete
            DeleteMultipleValues(valuesToBeDeleted, caption, question);
        }

        private void DeleteMultipleValues(ArrayList valuesToBeDeleted, string caption, string question)
        {
            question = question + " " + valuesToBeDeleted.Count + " Wert(e) betroffen!";

            if (valuesToBeDeleted.Count == 0) MessageBox.Show("Keine passenden Werte gefunden.", "Keine Werte", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            else if (MessageBox.Show(question, caption, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                // Delete Values
                foreach (CheckedDataValue data in valuesToBeDeleted)
                    if (DeleteDataValue(data))
                        _ValuesListView.Items.Remove(data);
            }
        }

        private void ConsolidateHistory(object sender, RoutedEventArgs e)
        {
            CheckedDataValue data = _ValuesListView.SelectedItem as CheckedDataValue;
            if (data == null) return;

            String caption = "Historie konsolidieren";
            List<ValueHistoryEntry> items = MesapAPIHelper.ConsolidateHistory(data.GetHistory());

            // Has no proposals for deletion
            if (items.Count == 0)
            {
                MessageBox.Show("Es können keine Einträge gelöscht werden.",
                    caption, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            // Has candidates
            else
            {
                String itemList = "";
                foreach (ValueHistoryEntry item in items)
                    itemList += item.GetValue() + " " + item.Object.ChangeDate + " " + item.Object.ChangeName + "\n";

                if (MessageBoxResult.OK.Equals(MessageBox.Show("Obsolete Historieneinträge:\n" + itemList,
                        caption, MessageBoxButton.OKCancel, MessageBoxImage.Exclamation)))
                {
                    // Do it!
                    data.ConsolidateHistory();

                    // Update list
                    ShowValues(null, null);
                }
            }
        }

        private bool DeleteDataValue(CheckedDataValue data)
        {
            bool result = false;
            int handle = ((AdminHelper)Application.Current).database.Root.GetFreeLockHandle();

            // Cannot delete (commented value, is locked)
            if (data.Object.AnnexObjNr > 0 &&
                data.Object.EnableModify(handle) != mspEnableModifyResultEnum.mspEnableModifySuccess)
                return result;

            // Get connection to database
            SqlConnection connection = ((AdminHelper)Application.Current).GetDirectDBConnection();

            try
            {
                int effectedRows = 0;
                int historyCount = data.GetHistory().Count;
                decimal valueId = data.Object.CntNr;

                // Delete History first
                effectedRows = new SqlCommand("DELETE FROM TimeSeriesDataHistory WHERE ValueCntNr=" + valueId,
                    connection).ExecuteNonQuery();

                if (effectedRows == historyCount)
                {
                    // Delete Value
                    effectedRows = new SqlCommand("DELETE FROM TimeSeriesData WHERE CntNr=" + valueId,
                        connection).ExecuteNonQuery();

                    if (effectedRows == 1) result = true;
                }                
            }
            catch (SqlException e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                data.Object.DisableModify(handle);
            }

            return result;
        }

        #region IDatabaseChangedObserver Members

        public void DatabaseChanged()
        {
            _ValuesListView.Items.Clear();
        }

        #endregion

        private void AllHistory(object sender, RoutedEventArgs e)
        {

        }
    }

    /// <summary>
    /// Class represents a search result record and wraps a dboTSData.
    /// </summary>
    class CheckedDataValue : DataValue
    {
        public CheckedDataValue(dboTSData data) : base(data) {}

        public String Timepoint
        {
            get 
            {
                Int32 year = 2000 + Object.PeriodNr;
                
                return  year + ", erfasst (" + Object.ScenNr + ")";
            }
        }

        public String Value
        {
            get 
            {
                if (IsNumericValue()) return Object.Value.ToString();
                else switch (Object.NoValueReason)
                {
                    case -2: return "ERR";
                    case -1: return "DEL";
                    case 1: return "NE";
                    case 2: return "NO";
                    case 3: return "NA";
                    case 4: return "IE";
                    case 5: return "C";
                    default: return "";
                }
            }
        }

        public String History
        {
            get 
            {
                List<ValueHistoryEntry> history = GetHistory();
                if (history.Count == 0) return "---";
                
                String result = history.Count + ": ";

                foreach (ValueHistoryEntry entry in history)
                {
                    double value = entry.GetValue();

                    if (value.Equals(Double.NaN)) result += entry.NoValueReason() + ", ";
                    else result += value + ", ";
                }

                return result.Substring(0, result.Length - 2);
            }
        }

        public String HasAnnex
        {
            get 
            {
                if (Object.AnnexObjNr == 0) return "Nein";
                else return "Ja";
            }
        }

        public String ChangeDate
        {
            get
            {
                return Object.ChangeDate.ToShortDateString();
            }
        }
    }
}
