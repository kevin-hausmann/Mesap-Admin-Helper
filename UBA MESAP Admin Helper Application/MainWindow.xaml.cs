using System.Windows;
using M4DBO;
using System.Collections;
using UBA.Mesap.AdminHelper.Types;
using System.Windows.Controls;
using System.ComponentModel;
using System.Collections.Generic;

namespace UBA.Mesap.AdminHelper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // List of database observers, these will be alerted if database is switched
        private List<IDatabaseChangedObserver> observers = new List<IDatabaseChangedObserver>();

        public MainWindow()
        {
            InitializeComponent();

            UpdateLoginText();
            InitDatabaseSelectionBox();
        }

        /// <summary>
        /// Allows other components and GUI elements to (temporarily)
        /// disable database switching
        /// </summary>
        /// <param name="enable">Whether database switch should be possible</param>
        public void EnableDatabaseSelection(bool enable)
        {
            _DatabaseSelect.IsEnabled = enable;
        }

        /// <summary>
        /// Register a listener.
        /// </summary>
        /// <param name="observer">The listener to be alerted on database switch.</param>
        public void Register(IDatabaseChangedObserver observer)
        {
            observers.Add(observer);
        }

        private void InitDatabaseSelectionBox()
        {
            dboRoot root = ((AdminHelper)Application.Current).root;
            IEnumerator dbs = root.InstalledDbs.GetEnumerator();

            _DatabaseSelect.Items.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Descending));
            
            while (dbs.MoveNext())
            {
                dboInstalledDB db = dbs.Current as dboInstalledDB;
                if (db.Leaf && ((AdminHelper)Application.Current).CanSwitchDatabase(db.ID))
                    _DatabaseSelect.Items.Add(new Database(db));
            }

            _DatabaseSelect.Items.Refresh();
            _DatabaseSelect.SelectedItem = new Database(((AdminHelper)Application.Current).database);
            _DatabaseSelect.AddHandler(ComboBox.SelectionChangedEvent, new RoutedEventHandler(SetDatabase));
        }

        private void UpdateLoginText()
        {
            dboRoot root = ((AdminHelper)Application.Current).root;
            dboDatabase db = ((AdminHelper)Application.Current).database;

            _LoginTextBlock.Text = "Angemeldet an " + root.InstalledDbs[db.DbNr].Name +
                " als " + root.InstalledUsers[root.LoggedInUser.UserNr].Name;
        }

        private void SetDatabase(object sender, RoutedEventArgs e)
        {
            Database newDB = _DatabaseSelect.SelectedItem as Database;

            if (((AdminHelper)Application.Current).SwitchDatabase(newDB.ID))
            {
                UpdateLoginText();

                foreach (IDatabaseChangedObserver observer in observers)
                    observer.DatabaseChanged();
            }
            else MessageBox.Show("Switching the database failed!", "Switch database", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
