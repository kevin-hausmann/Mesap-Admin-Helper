using System;

namespace UBA.Mesap.AdminHelper
{
    /// <summary>
    /// Interface to be implemented by listview object that can be
    /// copied and exported to other table view (e.g. MS Excel).
    /// </summary>
    interface IExportable
    {
        /// <summary>
        /// Provides a CVS-View on the data of this object.
        /// </summary>
        /// <returns>This objects field in specific order seperated by 'tab' (\t)</returns>
        String ToCVSString();
    }
}
