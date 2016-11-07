using M4DBO;
using System.Runtime.CompilerServices;

namespace UBA.Mesap.AdminHelper.Types
{
    /// <summary>
    /// Base class for a filter, wrapping dboTSFilter.
    /// </summary>
    public class Filter
    {
        // The base API object wrapped
        protected dboTSFilter _filter;

        // This is a cache because calculation of numbers takes a lot of time
        // Negative value indicates that cache is empty and needs update on request 
        protected int _countCache = -1;

        /// <summary>
        /// Creates new wrapper.
        /// </summary>
        /// <param name="filter">Filter to wrap</param>
        public Filter(dboTSFilter filter)
        {
            _filter = filter;
        }

        /// <summary>
        /// Creates new wrapper with pre-set count.
        /// </summary>
        /// <param name="filter">Filter to wrap</param>
        /// <param name="count">Count to pre-set. WILL NOT BE CHECKED. Giving a value 
        /// less then zero triggers calculation on count request. Values greater then
        /// zero will be taken as actual value.</param>
        public Filter(dboTSFilter filter, int count)
            : this(filter)
        {
            _countCache = count;
        }

        /// <summary>
        /// Get the API object wrapped
        /// </summary>
        public dboTSFilter Object
        {
            get { return _filter; }
        }

        /// <summary>
        /// Calculates the number of timeseries filtered by filter wrapped.
        /// Value is cached. Call ResetCountCache to reset and recalculate.
        /// Returns negative value if filter is invalid.
        /// </summary>
        public int Count
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                if (_countCache < 0 && _filter != null) {
                    _countCache = 0;

                    dboList list = new dboList();
                    list.FromString(_filter.GetTSNumbers(), VBA.VbVarType.vbLong);
                    foreach (object number in list) _countCache++;
                }

                return _countCache;
            }
        }

        /// <summary>
        /// Resets count cache. Upon next request the calculation of the 
        /// count value is triggered.
        /// </summary>
        public void ResetCountCache()
        {
            _countCache = -1;
        }
    }
}
