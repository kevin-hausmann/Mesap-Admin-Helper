using M4DBO;
using System.Runtime.CompilerServices;

namespace UBA.Mesap.AdminHelper.Types
{
    /// <summary>
    /// Base class for a filter, wrapping dboTSFilter.
    /// </summary>
    public class Filter
    {
        /// <summary>
        /// The base API object wrapped.
        /// </summary>
        public dboTSFilter Object { get; protected set; }

        // This is a cache because calculation of numbers takes a lot of time
        // Negative value indicates that cache is empty and needs update on request 
        protected int _countCache = -1;

        /// <summary>
        /// Creates new wrapper.
        /// </summary>
        /// <param name="filter">Filter to wrap</param>
        public Filter(dboTSFilter filter)
        {
            Object = filter;
        }

        /// <summary>
        /// Creates new wrapper with preset count.
        /// </summary>
        /// <param name="filter">Filter to wrap</param>
        /// <param name="count">Count to preset. WILL NOT BE CHECKED. Giving a value 
        /// less then zero triggers calculation on count request. Values greater then
        /// zero will be taken as actual value.</param>
        public Filter(dboTSFilter filter, int count)
            : this(filter)
        {
            _countCache = count;
        }

        /// <summary>
        /// Calculates the number of time series filtered by filter wrapped.
        /// Value is cached. Call ResetCountCache() to reset and recalculate.
        /// Returns negative value if filter is invalid.
        /// </summary>
        public int Count
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                if (_countCache < 0 && Object != null) {
                    _countCache = 0;

                    dboList list = new dboList();
                    list.FromString(Object.GetTSNumbers(), VBA.VbVarType.vbLong);
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
