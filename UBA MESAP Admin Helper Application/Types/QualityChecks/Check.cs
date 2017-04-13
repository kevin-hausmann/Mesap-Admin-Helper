using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    /// <summary>
    /// Generic check base class.
    /// </summary>
    public abstract class Check : INotifyPropertyChanged
    {
        /// <summary>
        /// Event handler used to propagate property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        private bool enabled = false;
        /// <summary>
        /// Whether or not the check is active and will be executed.
        /// </summary>
        public bool Enabled
        {
            get { return enabled; }
            set
            {
                enabled = value;

                if (!enabled)
                    Reset();

                NotifyChange();
            }
        }

        private bool running = false;
        /// <summary>
        /// Whether or not the check is currently running.
        /// </summary>
        public bool Running
        {
            get { return running; }
            protected set
            {
                running = value;
                NotifyChange();
            }
        }

        private bool completed = false;
        /// <summary>
        /// Whether or not the check has finished at least once.
        /// </summary>
        public bool Completed
        {
            get { return completed; }
            protected set
            {
                completed = value;
                NotifyChange();
            }
        }

        private int percentage = 0;
        /// <summary>
        /// Percentage [0 - 100] of check completion.
        /// </summary>
        public int Completion
        {
            get { return percentage; }
            protected set
            {
                percentage = value;
                NotifyChange();
            }
        }

        private int findingCount = 0;
        /// <summary>
        /// Number of findings the check produced while running or during the last execution.
        /// </summary>
        public int FindingCount
        {
            get { return findingCount; }
            protected set
            {
                findingCount = value;
                NotifyChange();
            }
        }

        protected virtual void Reset()
        {
            Running = false;
            Completed = false;
            Completion = 0;
            FindingCount = 0;
        }

        private readonly Dictionary<string, PropertyChangedEventArgs> _argsCache = new Dictionary<string, PropertyChangedEventArgs>();
        protected virtual void NotifyChange([System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            if (PropertyChanged != null && _argsCache != null && !string.IsNullOrEmpty(memberName))
            {
                if (!_argsCache.ContainsKey(memberName))
                    _argsCache[memberName] = new PropertyChangedEventArgs(memberName);

                PropertyChanged.Invoke(this, _argsCache[memberName]);
            }
        }
    }
}
