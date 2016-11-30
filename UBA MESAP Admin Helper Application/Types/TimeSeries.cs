using System;
using M4DBO;
using System.Collections;

namespace UBA.Mesap.AdminHelper.Types
{
    /// <summary>
    /// Base class for time series, wrapping dboTS
    /// </summary>
    public class TimeSeries
    {
        // The base API object wrapped
        protected dboTS _timeSeries;

        // Distribution options and their value representation in the MESAP database
        public enum Distribution : int
        { 
            // No uncertainty information available 
            none = -1,
            // Real options follow below
            normal = 613, 
            lognormal = 614, 
            triangle = 615, 
            uniform = 616
        }

        // Uncertainty documentation component and its fields
        // represented as values as in MESAP database
        public enum UncertaintyComponent : int
        {
            // The component itself
            self = 18,
            // The component's fields
            umax = 102,
            umin = 103,
            distribution = 104,
            source = 105
        }

        /// <summary>
        /// Creates wrapper, reads the basic series information
        /// </summary>
        /// <param name="series">The series to wrap</param>
        public TimeSeries(dboTS series)
        {
            _timeSeries = series;
            _timeSeries.DbReadRelatedKeys();
        }

        /// <summary>
        /// Creates wrapper, reads the basic series information and
        /// data for period given.
        /// </summary>
        /// <param name="series">The series to wrap</param>
        /// <param name="from">Year value for start of period, e.g. 1990</param>
        /// <param name="to">Year value for end of period, e.g. 2007</param>
        public TimeSeries(dboTS series, int from, int to)
            : this(series)
        {
            ReadData(from, to);
        }

        /// <summary>
        /// Get the API object wrapped
        /// </summary>
        public dboTS Object
        {
            get { return _timeSeries; }
        }

        /// <summary>
        /// Get the name of wrapped series
        /// </summary>
        public string Name
        {
            get { return _timeSeries.Name; }
        }

        /// <summary>
        /// Get the ID of wrapped series
        /// </summary>
        public string ID
        {
            get { return _timeSeries.ID; }
        }

        /// <summary>
        /// Get the legend of wrapped series 
        /// (only existing descriptor's ids sorted alphabetically)
        /// </summary>
        public string Legend
        {
            get { return _timeSeries.BuildTsLegend(false, mspNameModeEnum.mspNameModeId, mspSortModeEnum.mspSortModeAlpha); }
        }

        /// <summary>
        /// Get the uncertainty distribution for this time series.
        /// </summary>
        public Distribution UncertaintyDistribution
        {
            get
            {
                return (Distribution)ExtractUncertaintyInformation(UncertaintyComponent.distribution).ReferenceData;
            }
        }

        /// <summary>
        /// Get the uncertainty upper limit
        /// </summary>
        public double UncertaintyUpperLimit
        {
            get
            {
                return Double.Parse(ExtractUncertaintyInformation(UncertaintyComponent.umax).NumberData.ToString());
            }
        }

        /// <summary>
        /// Get the uncertainty lower limit
        /// </summary>
        public double UncertaintyLowerLimit
        {
            get
            {
                return Double.Parse(ExtractUncertaintyInformation(UncertaintyComponent.umin).NumberData.ToString());
            }
        }

        /// <summary>
        /// Queries the time series for a uncertainty documentation.
        /// </summary>
        /// <returns>If any documentation component for uncertainties can be found</returns>
        public bool HasUncertaintyDocumentation()
        {
            // If there is no distribution, there is no uncertainty information at all
            return ExtractUncertaintyInformation(UncertaintyComponent.distribution) != null;
        }

        /// <summary>
        /// Check if the time series has all three uncertainty documentation components (min, max, distribution).
        /// </summary>
        /// <returns>If a full documentation component for uncertainties can be found</returns>
        public bool HasCompleteUncertaintyDocumentation()
        {
            double dummy;

            return ExtractUncertaintyInformation(UncertaintyComponent.distribution).ReferenceData > 0 &&
                Double.TryParse(ExtractUncertaintyInformation(UncertaintyComponent.umax).NumberData.ToString(), out dummy) &&
                Double.TryParse(ExtractUncertaintyInformation(UncertaintyComponent.umin).NumberData.ToString(), out dummy); 
        }

        /// <summary>
        /// Check whether this time series is set as "okay" in its properties
        /// </summary>
        /// <returns>The current flag status</returns>
        public bool IsMarkedConsistent()
        {
            _timeSeries.DbReadRelatedProperties(mspTimeKeyEnum.mspTimeKeyYear, mspTimeKeyTypeEnum.mspTimeKeyTypeUnknown);
            return _timeSeries.TSProperties.GetObject(
                     mspTimeKeyEnum.mspTimeKeyYear, mspTimeKeyTypeEnum.mspTimeKeyTypeUnknown).IsTsOk;
        }

        /// <summary>
        /// Set the series' property "is okay" to given value
        /// </summary>
        /// <param name="consistent">Value to set</param>
        /// <returns>Whether the property was updated alright</returns>
        /* UNTESTED CODE! DO NOT USE AS IS!
        public bool SetMarkedConsistent(bool consistent)
        {
            if (IsMarkedConsistent().Equals(consistent)) return true;

            dboTSProperty property = _timeSeries.TSProperties.GetObject(mspTimeKeyEnum.mspTimeKeyYear, mspTimeKeyTypeEnum.mspTimeKeyTypeUnknown);
            bool success = false;

            // Apply if not locked
            int handle = _timeSeries.Database.Root.GetFreeLockHandle();
            success = property.EnableModify(handle).Equals(mspEnableModifyResultEnum.mspEnableModifySuccess);

            if (success)
            {
                property.IsTsOk = consistent;
                
                _timeSeries.TSProperties.DbUpdateAll(handle);
                _timeSeries.TSProperties.DisableModifyAll(handle);
            }

            return success;
        }*/

        /// <summary>
        /// Reads data (input and result) for given period into wrapper.
        /// </summary>
        /// <param name="yearFrom">Year value for start of period, e.g. 1990</param>
        /// <param name="yearTo">Year value for end of period, e.g. 2007</param>
        protected void ReadData(int yearFrom, int yearTo)
        {
            ReadData(yearFrom, yearTo, false);
        }

        /// <summary>
        /// Reads data (input and result) for given period into wrapper.
        /// </summary>
        /// <param name="yearFrom">Year value for start of period, e.g. 1990</param>
        /// <param name="yearTo">Year value for end of period, e.g. 2007</param>
        /// <param name="clear">Whether priorly read values should be erased</param>
        protected void ReadData(int yearFrom, int yearTo, bool clear)
        {
            int from = _timeSeries.Database.Units.TkDateToPeriod(new DateTime(yearFrom, 1, 1), mspTimeKeyEnum.mspTimeKeyYear, false);
            int to = _timeSeries.Database.Units.TkDateToPeriod(new DateTime(yearTo, 1, 1), mspTimeKeyEnum.mspTimeKeyYear, false);

            _timeSeries.DbReadRelatedDatas(clear, mspDataTypeEnum.mspDataTypeInput,
                mspTimeKeyEnum.mspTimeKeyYear, from, to, null, "1", null);
            _timeSeries.DbReadRelatedDatas(false, mspDataTypeEnum.mspDataTypeInput,
                mspTimeKeyEnum.mspTimeKeyYear, from, to, null, "2", null);
            _timeSeries.DbReadRelatedDatas(false, mspDataTypeEnum.mspDataTypeResult,
                mspTimeKeyEnum.mspTimeKeyYear, from, to, null, "1", null);
            _timeSeries.DbReadRelatedDatas(false, mspDataTypeEnum.mspDataTypeResult,
                mspTimeKeyEnum.mspTimeKeyYear, from, to, null, "2", null);
        }

        /// <summary>
        /// Reads data (input and result) for given year into wrapper.
        /// </summary>
        /// <param name="year">Year to read, e.g. 2007</param>
        protected void ReadData(int year)
        {
            ReadData(year, false);
        }

        /// <summary>
        /// Reads data (input and result) for given year into wrapper.
        /// </summary>
        /// <param name="year">Year to read, e.g. 2007</param>
        /// <param name="clear">Whether priorly read values should be erased</param>
        protected void ReadData(int year, bool clear)
        {
            ReadData(year, year, clear);
        }

        /// <summary>
        /// Gets data read before (Does not READ FROM DATABASE, call Read method before!).
        /// If for given year input and result data exists, input data is selected.
        /// </summary>
        /// <param name="year">Year to retrieve</param>
        /// <returns>Data object, may be null</returns>
        public DataValue RetrieveData(int year)
        {
            int period = _timeSeries.Database.Units.TkDateToPeriod(new DateTime(year, 1, 1), mspTimeKeyEnum.mspTimeKeyYear, false);
            dboTSData data = _timeSeries.TSDatas.GetObject(period, mspTimeKeyEnum.mspTimeKeyYear, mspDataTypeEnum.mspDataTypeInput, 1);

            return data == null ? null : new DataValue(data);
        }

        protected dboAnnexItemData ExtractUncertaintyInformation(UncertaintyComponent field)
        {
            // Result object, set below
            dboAnnexItemData result = null;

            // Get documentation for this time series
            dboAnnexObjects objects = _timeSeries.Database.CreateObject_AnnexObjects("");
            objects.DbReadByReference_Docu(mspDocuTypeEnum.mspDocuTypeTS, _timeSeries.TsNr, mspTimeKeyEnum.mspTimeKeyYear, 0, 0, 0, false);
            dboAnnexObject annexObject = objects.GetObject_Docu(_timeSeries.TsNr, mspDocuTypeEnum.mspDocuTypeTS, mspTimeKeyEnum.mspTimeKeyYear, 0, 0, 0);

            // No documentation -> return "null"
            if (annexObject == null) return null;

            // Get all documentation components for the time series
            dboAnnexSetLinks links = _timeSeries.Database.CreateObject_AnnexSetLinks("");
            links.DbReadByReference(annexObject.AnnexObjNr, mspAnnexTypeEnum.mspAnnexTypeDocu, true);
            
            // ... and search them for uncertainties
            IEnumerator linksEnum = links.GetEnumerator();
            while (linksEnum.MoveNext())
            {
                dboAnnexSetLink link = linksEnum.Current as dboAnnexSetLink;
                // Is this component for uncertainties?
                if (link.ComponentNr == (int)UncertaintyComponent.self)
                {
                    // Extract component data
                    dboAnnexItemDatas datas = _timeSeries.Database.CreateObject_AnnexItemDatas("");
                    datas.DbReadByItemNr(link.AnnexSetNr, (int)UncertaintyComponent.self, (int)field, false, link.AnnexSetLinkNr, true);

                    // Set first item (there should only be one) as return value
                    IEnumerator dataEnum = datas.GetEnumerator();
                    dataEnum.MoveNext();
                    result = dataEnum.Current as dboAnnexItemData;
                    break;
                }
            }

            return result;
        }
    }
}
