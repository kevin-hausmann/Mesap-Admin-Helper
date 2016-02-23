namespace UBA.Mesap.AdminHelper
{
    /// <summary>
    /// Interface for database switch observer pattern.
    /// Observers register to the main window and are
    /// alerted on database change.
    /// </summary>
    public interface IDatabaseChangedObserver
    {
        /// <summary>
        /// Observer method called on database switch.
        /// </summary>
        void DatabaseChanged();
    }
}
