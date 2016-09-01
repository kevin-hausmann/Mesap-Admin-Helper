using System;
using System.Collections;
using System.Collections.Generic;
using M4DBO;
using System.Data.SqlClient;
using System.Windows;

namespace UBA.Mesap.AdminHelper.Types
{
    /// <summary>
    /// Base class for a single cell value, wraps dboTSData
    /// </summary>
    class DataValue
    {
        // The base API object wrapped
        protected dboTSData _data;

        /// <summary>
        /// Creates wrapper
        /// </summary>
        /// <param name="data">Data object to wrap</param>
        public DataValue(dboTSData data)
        {
            _data = data;
        }

        /// <summary>
        /// Get the object wrapped
        /// </summary>
        public dboTSData Object
        {
            get { return _data; }
        }

        /// <summary>
        /// Checks whether data value is numeric. If true you
        /// should be able to safely get the value as a double.
        /// </summary>
        /// <returns>true if numeric, false otherwise</returns>
        public bool IsNumericValue()
        {
            if (_data.Value == null) return false;

            double test;
            return Double.TryParse(_data.Value.ToString(), out test); 
        }

        /// <summary>
        /// Checks whether data value is numeric and not zero.
        /// </summary>
        /// <returns>true if value is numeric and not zero, false otherwise</returns>
        public bool IsActualValue()
        {
            if (!IsNumericValue()) return false;

            return !GetValue().Equals(0);
        }

        /// <summary>
        /// Tries to given data value as a double, will raise exception on 
        /// not numeric value, e.g. "IE" or "ERR".
        /// </summary>
        /// <returns>The data value as a double</returns>
        public double GetValue()
        {
            return Convert.ToDouble(_data.Value);
        }

        public double GetScenario()
        {
            return _data.ScenNr;
        }

        /// <summary>
        /// Checks whether this data value is of input type.
        /// In opposition to the calculated type.
        /// </summary>
        /// <returns>true if input, false if calculated</returns>
        public bool IsInput()
        {
            return _data.DataType == mspDataTypeEnum.mspDataTypeInput;
        }

        /// <summary>
        /// Gets the history values of this data value as a list.
        /// </summary>
        /// <returns>The (possibly empty) list of history values. Ordered, first is latest. </returns>
        public List<ValueHistoryEntry> GetHistory()
        {
            List<ValueHistoryEntry> result = new List<ValueHistoryEntry>();
            IEnumerator history = _data.DbGetHistory().GetEnumerator();

            while (history.MoveNext())
            {
                dboTSDataHistory entry = history.Current as dboTSDataHistory;
                result.Add(new ValueHistoryEntry(entry));
            }

            return result;
        }

        public int ConsolidateHistory()
        {
            List<ValueHistoryEntry> obsoleteEntries = MesapAPIHelper.ConsolidateHistory(GetHistory());
            if (obsoleteEntries.Count != 0)
            {
                // Get connection to database
                SqlConnection connection = ((AdminHelper)Application.Current).GetDirectDBConnection();

                foreach (ValueHistoryEntry entry in obsoleteEntries)
                {
                    String deleteQuery = "DELETE FROM TimeSeriesDataHistory WHERE ValueCntNr=" + entry.Object.ValueCntNr +
                        " and ChangeDate=\'" + entry.Object.ChangeDate.ToString("yyyyMMdd HH:mm:ss.fff") + "\'";
                    new SqlCommand(deleteQuery, connection).ExecuteNonQuery();
                }
            }

            return obsoleteEntries.Count;
        }

        // CHECK THIS BEFORE USING IT!
        //public bool SetValue(double value)
        //{
        //    bool success = false;

        //    App application = ((App)Application.Current);
        //    int handle = application.database.Root.GetFreeLockHandle();

        //    dboTS serie = GetTimeSeries(Convert.ToString(_data.TsNr));
        //    serie.DbReadRelatedDatas(false, _data.DataType, _data.TimeKey, _data.PeriodNr, _data.PeriodNr,
        //        null, Convert.ToString(_data.ScenNr), null);
        //    dboTSData myData = serie.TSDatas[_data.CntNr] as dboTSData;

        //    if (myData == null) return success;

        //    success = myData.EnableModify(handle).Equals(mspEnableModifyResultEnum.mspEnableModifySuccess);
        //    myData.Value = value;
        //    myData.DisableModify(handle);
        //    serie.TSDatas.DbUpdateAll(handle);

        //    return success;
        //}
    }

    class ValueHistoryEntry
    {
        private dboTSDataHistory _entry;

        public ValueHistoryEntry(dboTSDataHistory historyEntry)
        {
            _entry = historyEntry;
        }

        /// <summary>
        /// Get the object wrapped
        /// </summary>
        public dboTSDataHistory Object
        {
            get { return _entry; }
        }

        /// <summary>
        /// Gets the numerical value for this data value.
        /// </summary>
        /// <returns>The value or Double.NaN if none available (call NoValueReason() for details)</returns>
        public double GetValue()
        {
            if (_entry.NoValueReason != 0) return Double.NaN;
            else return Double.Parse(_entry.Value.ToString());
        }

        /// <summary>
        /// Gets the reason (i.e. ERR or DEL) if value is not available.
        /// </summary>
        /// <returns>String indicating reason, one of ERR, DEL, or notation key</returns>
        public String NoValueReason()
        {
            switch (_entry.NoValueReason)
            {
                case -2: return "ERR";
                case -1: return "DEL";
                default: return "Notation Key";
            }
        }

        public override bool Equals(object obj)
        {
            ValueHistoryEntry other = obj as ValueHistoryEntry;

            return other.Object.CntNr.Equals(_entry.CntNr);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
