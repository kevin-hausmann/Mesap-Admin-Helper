using System;
using System.Data;
using System.Windows;
using M4DBO;
using M4UIO;
using System.Data.SqlClient;

namespace UBA.Mesap.AdminHelper
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// Basic application class. Organizes database access (API and non-API).
    /// </summary>
    public partial class AdminHelper : Application, IDisposable
    {
        // API base objects 
        internal dboRoot root;
        internal uioRoot uiRoot;
        internal dboDatabase database;

        // Direct non-API database access
        private SqlConnection databaseConnection;

        // MESAP client server system connection settings
        bool readOnly = false;
        bool exclusive = false;

        protected override void OnStartup(StartupEventArgs e)
        {
            String defaultDatabaseId = "ZSE_aktuell";
            mspErrUioInitEnum rootErr;

            uiRoot = new M4UIO.uioRoot();
            rootErr = uiRoot.Initialize("", "", mspM4AppEnum.mspM4AppOEM, false, "UBA Mesap Admin Helper");

            if (rootErr == mspErrUioInitEnum.mspErrNone)
            {
                uiRoot.Dbo.LoginWithNTAccount();
                root = uiRoot.Dbo;
                Connect(defaultDatabaseId, readOnly, exclusive);
            }
            else throw new NullReferenceException("MESAP Initialization failed! Error: " + rootErr);
        }

        /// <summary>
        /// Switch to database with given identifier. Effects both API and
        /// non-API access.
        /// </summary>
        /// <param name="id">ID of database to switch to. Do not give non-existent id!</param>
        /// <returns>Whether switch was successful (true) or failed (false).</returns>
        public bool SwitchDatabase(String id)
        {
            if (!CanSwitchDatabase(id)) return false;
            return Connect(id, readOnly, exclusive);
        }

        /// <summary>
        /// Check whether the database is usable with the admin tool,
        /// i.e. we do have the connection details. Does NOT switch!
        /// </summary>
        /// <param name="id">ID of database.</param>
        /// <returns>Whether switch is potentially possible (true) or not (false).</returns>
        public bool CanSwitchDatabase(String id)
        {
            return BuildDBConnectionString(id) != null;
        }
        
        public void Dispose()
        {
            databaseConnection.Dispose();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            root.Logout();
            databaseConnection.Close();

            base.OnExit(e);
        }

        /// <summary>
        /// Gets a direct SQL (non-API) database connection.
        /// </summary>
        /// <returns>Connection to database, already opened.</returns>
        internal SqlConnection GetDirectDBConnection()
        {
            if (databaseConnection.State == ConnectionState.Closed ||
                databaseConnection.State == ConnectionState.Broken)
                databaseConnection.Open();

            return databaseConnection;
        }

        /// <summary>
        /// Get a direct SQL (non-API) database connection.
        /// </summary>
        /// <param name="databaseId">Database you want to connect to. Can be different to the current default.</param>
        /// <returns>New and closed SQL connection object, to be managed by the caller.</returns>
        internal SqlConnection GetDirectDBConnection(String databaseId)
        {
            return new SqlConnection(BuildDBConnectionString(databaseId));
        }

        private bool Connect(String databaseId, bool readOnly, bool exclusive)
        {
            // Open API connection
            if (database != null)
            {
                root.Databases.CloseDb(database.DbNr);
                database = null;
            }

            mspErrDboOpenDbEnum databaseErr = root.Databases.OpenDb(databaseId, readOnly, ref exclusive);

            if (databaseErr == mspErrDboOpenDbEnum.mspErrNone)
                database = root.MainDb;
            else throw new Exception("Failed to connect to database " + databaseId + ": " + databaseErr);

            // Open non-API connection
            if (databaseConnection != null && databaseConnection.State != ConnectionState.Closed)
                databaseConnection.Close();

            databaseConnection = new SqlConnection(BuildDBConnectionString(databaseId));

            return databaseErr == mspErrDboOpenDbEnum.mspErrNone;
        }

        private String BuildDBConnectionString(String databaseId)
        {
            String databaseName;
            switch (databaseId)
            {
                case "ESz":
                case "BEU":
                case "PoSo":
                case "Enerdat":
                    databaseName = databaseId.ToUpper(); break; 
                case "ZSE_aktuell":
                case "ZSE_Schulung":
                    databaseName = databaseId; break;
                case "ZSE_Submission_2017_20170217":
                case "ZSE_Submission_2016_20160203": 
                case "ZSE_Submission_2015_20150428":
                case "ZSE_Submission_2014_20140303":
                case "ZSE_Submission_2013_20130220":
                case "ZSE_Submission_2012_20120305":
                case "ZSE_Submission_2011_20110223":
                case "ZSE_Submission_2010_20100215":
                case "ZSE_Submission_2009_20090211":
                case "ZSE_Submission_2008_20080213":
                case "ZSE_Submission_2007_20070328":
                case "ZSE_Submission_2006_20060411":
                case "ZSE_Submission_2005_20041220":
                case "ZSE_Submission_2004_20040401":
                case "ZSE_Submission_2003_20030328":
                    databaseName = databaseId.Substring(0, 19); break;
                default:
                    Console.WriteLine("OOPS: Connection string for unknown database \"" + databaseId + "\" requested");
                    return null;
            }
            
            return String.Format(Private.MesapServerConnectionTemplate, Private.MesapServerName, databaseName, Private.MesapServerUsername, Private.MesapServerPassword);
        }
    }
}
