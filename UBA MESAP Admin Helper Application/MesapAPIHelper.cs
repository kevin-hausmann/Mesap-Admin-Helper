using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using M4DBO;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using UBA.Mesap.AdminHelper.Types;

namespace UBA.Mesap.AdminHelper
{
    /// <summary>
    /// Class provides some additional helpers for the MESAP API
    /// used by various user controls.
    /// </summary>
    class MesapAPIHelper
    {
        #region Data sheet aka. "filters" helpers

        /// <summary>
        /// Gets time series view with given identifier.
        /// </summary>
        /// <param name="Id">ID of view requested.</param>
        /// <returns>The view.</returns>
        public static dboTSView GetView(String Id)
        {
            AdminHelper application = ((AdminHelper)Application.Current);

            return GetFirstView(application.database.CreateObject_TsViews(Id));
        }

        /// <summary>
        /// Gets time series view with given identifier.
        /// </summary>
        /// <param name="Id">ID of view requested.</param>
        /// <returns>The view.</returns>
        public static dboTSView GetView(int Id)
        {
            return GetView(Convert.ToString(Id));
        }

        /// <summary>
        /// Gets you the first view in given view collection.
        /// </summary>
        /// <param name="views">Collection to get first view from.</param>
        /// <returns>The first item of the view collection.</returns>
        public static dboTSView GetFirstView(dboTSViews views)
        {
            IEnumerator viewsEnumerator = views.GetEnumerator();
            viewsEnumerator.MoveNext();

            return viewsEnumerator.Current as dboTSView;
        }

        /// <summary>
        /// Calculates the number of time series filtered by a filter.
        /// </summary>
        /// <param name="filter">Filter to evaluate.</param>
        /// <returns>The number of time series' drawn by this filter.</returns>
        public static int GetTimeSeriesCount(dboTSFilter filter)
        {
            int result = 0;

            if (filter != null)
            {
                String numbers = filter.GetTSNumbers();

                if (numbers != null && numbers.Length >= 2)
                    result = numbers.Split(',').Length;
            }

            return result;
        }

        public static dboTSViews GetAdminHelperView()
        {
            const string VIEW_NAME = "$Admin-Helper";
            AdminHelper application = ((AdminHelper)Application.Current);
            dboTSViews views = application.database.CreateObject_TsViews(VIEW_NAME);
            
            // Create view if not exists            
            if (views.Count == 0)
            {
                int handle = application.root.GetFreeLockHandle();

                dboTSView view = views.Add(handle, true, 0, false);
                view.EnableModify(handle);
                view.Name = VIEW_NAME;
                view.ID = VIEW_NAME;
                view.DisableModify(handle);
                views.DbUpdateAll(handle);
            }

            return application.database.CreateObject_TsViews(VIEW_NAME);
        }

        #endregion
        #region Time series helpers

        /// <summary>
        /// Gets time series by identifier.
        /// </summary>
        /// <param name="Id">ID of time series requested.</param>
        /// <returns>The time series or <i>null</i> if no time series with given ID or number exists.</returns>
        public static dboTS GetTimeSeries(String Id)
        {
            AdminHelper application = ((AdminHelper)Application.Current);
            dboTSs series = null;
            try
            {
                series = application.database.CreateObject_TSs(Id);
            }
            catch (Exception)
            {
                return null;
            }

            IEnumerator seriesEnumerator = series.GetEnumerator();

            if (seriesEnumerator.MoveNext()) return seriesEnumerator.Current as dboTS;
            else return null;
        }

        /// <summary>
        /// Gets time series by number.
        /// </summary>
        /// <param name="number">Database number of time series requested.</param>
        /// <returns>The time series or <i>null</i> if no time series with given number exists.</returns>
        public static dboTS GetTimeSeries(int number)
        {
            return ((AdminHelper)Application.Current).database.CreateObject_TSs(number.ToString())[number];
        }

        #endregion

        /// <summary>
        /// Converts a list view's contents to a CVS string.
        /// </summary>
        /// <param name="view">The view to convert. Can not be null. 
        /// Has to contain a grid view control.</param>
        /// <returns>A CVS String object holding the view's contents.</returns>
        public static String GetListViewContentsAsCVSString(ListView view)
        {
            StringBuilder buffer = new StringBuilder();

            if (view != null && view.View != null)
            {
                GridView gridView = view.View as GridView;

                // Headings
                for (int i = 0; i < gridView.Columns.Count; i++)
                    buffer.Append(gridView.Columns[i].Header + "\t");

                buffer.Append("\n");

                // Content
                for (int i = 0; i < view.Items.Count; i++)
                {
                    IExportable item = view.Items[i] as IExportable;
                    buffer.Append(item.ToCVSString() + "\n");
                }
            }

            return buffer.ToString();
        }

        public static List<ValueHistoryEntry> ConsolidateHistory(List<ValueHistoryEntry> history)
        {
            List<ValueHistoryEntry> notNeeded = new List<ValueHistoryEntry>();
            if (history.Count == 0) return notNeeded;

            bool found;
            do
            {
                found = false;

                foreach (ValueHistoryEntry entry in history)
                {
                    int index = history.IndexOf(entry);
                    // Skip first element
                    if (index == 0) continue;
                    // Skip non-numerical values
                    else if (entry.GetValue().Equals(Double.NaN) ||
                        history.ElementAt(index - 1).GetValue().Equals(Double.NaN)) continue;
                    // Test for equal
                    else if (entry.GetValue().Equals(history.ElementAt(index - 1).GetValue()) &&
                        !notNeeded.Contains(history.ElementAt(index - 1)))
                    {
                        notNeeded.Add(history.ElementAt(index - 1));
                        found = true;
                    }
                }
            } while (found);

            ValueHistoryEntry lastEntry = history.ElementAt(history.Count - 1);
            if (lastEntry.NoValueReason().Equals("DEL"))
                notNeeded.Add(lastEntry);

            return notNeeded;
        }
    }
}
