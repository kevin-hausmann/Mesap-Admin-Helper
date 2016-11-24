using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    /// <summary>
    /// Quality check base class, defines methods to run tasks.
    /// </summary>
    public abstract class QualityCheck : Check, IEquatable<QualityCheck>, IComparable<QualityCheck>
    {
        public abstract string Name { get; }

        public abstract string Description { get; }

        public abstract short DatabaseReference { get; }       

        private int elementCount = 0;
        /// <summary>
        /// The number of database elements (e.g. time series) whose quality will be checked.
        /// </summary>
        public int ElementCount {
            get { return elementCount; }
            protected set
            {
                elementCount = value;
                NotifyChange();
            }
        }

        private int elementProcessedCount = 0;
        /// <summary>
        /// The number of database elements (e.g. time series) whose quality has already
        /// been checked during the current run.
        /// </summary>
        public int ElementProcessedCount
        {
            get { return elementProcessedCount; }
            protected set
            {
                elementProcessedCount = value;
                NotifyChange();
            }
        }

        private TimeSpan estimatedExecutionTime = TimeSpan.Zero;
        /// <summary>
        /// Expected run time based on the filter set.
        /// Will not be updated when the check runs.
        /// </summary>
        public TimeSpan EstimatedExecutionTime
        {
            get { return estimatedExecutionTime; }
            protected set
            {
                estimatedExecutionTime = value;
                NotifyChange();
            }
        }

        private TimeSpan remainingExecutionTime = TimeSpan.Zero;
        /// <summary>
        /// Expected remaining run time during execution, based on measured performance.
        /// </summary>
        public TimeSpan RemainingExecutionTime
        {
            get { return remainingExecutionTime; }
            protected set
            {
                remainingExecutionTime = value;
                NotifyChange();
            }
        }

        private TimeSpan measuredExecutionTime = TimeSpan.Zero;
        /// <summary>
        /// Total check execution time, available once the check run.
        /// </summary>
        public TimeSpan MeasuredExecutionTime
        {
            get { return measuredExecutionTime; }
            protected set
            {
                measuredExecutionTime = value;
                NotifyChange();
            }
        }

        /// <summary>
        /// Estimated average check running time for a single element.
        /// </summary>
        public abstract TimeSpan EstimatedExecutionTimePerElement { get; }

        private TimeSpan measuredExecutionTimePerElement = TimeSpan.Zero;
        /// <summary>
        /// Measured average check running time for a single element.
        /// </summary>
        public TimeSpan MeasuredExecutionTimePerElement
        {
            get { return measuredExecutionTimePerElement; }
            protected set
            {
                measuredExecutionTimePerElement = value;
                NotifyChange();
            }
        }

        /// <summary>
        /// Asynchronously estimate check running time for the set of time series given by a filter.
        /// Updates the corresponding fields ElementCount and EstimatedExecutionTime.
        /// </summary>
        /// <param name="filter">The filter used to select a sub-set of elements from the database</param>
        /// <param name="token">Token to cancel the async operation</param>
        /// <returns>Task instance that runs the estimation</returns>
        public abstract Task EstimateExecutionTimeAsync(Filter filter, CancellationToken token);
        
        /// <summary>
        /// Asynchronously execute check on all time series given by filter. Findings will
        /// be reported using the progress handle provided. When called, the set send via
        /// progress will contain one or more findings.
        /// </summary>
        /// <param name="filter">The filter used to select a sub-set of time series from the database</param>
        /// <param name="cancellationToken">Token to cancel the async operation</param>
        /// <param name="progress">Progress handle to report findings with</param>
        /// <returns>Task instance that runs the check</returns>
        public abstract Task RunAsync(Filter filter, CancellationToken cancellationToken, IProgress<ISet<Finding>> progress);

        /// <summary>
        /// Determine whether to findings should be considered equal. The default implementation checks the
        /// findings titles. Subclass can overwrite this behaviour.
        /// </summary>
        /// <param name="existingFinding">Finding already present</param>
        /// <param name="newFinding">New finding</param>
        /// <returns>True is the new finding is the same as the one already present</returns>
        public bool ConsideredEqual(Finding existingFinding, Finding newFinding)
        {
            return existingFinding.Title.Equals(newFinding.Title);
        }

        public bool Equals(QualityCheck other)
        {
            return other != null && DatabaseReference == other.DatabaseReference;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            return Equals(obj as QualityCheck);
        }

        public override int GetHashCode()
        {
            return DatabaseReference;
        }

        public int CompareTo(QualityCheck other)
        {
            return Name.CompareTo(other.Name);
        }

        protected override void Reset()
        {
            base.Reset();

            ElementProcessedCount = 0;
        }

        public static QualityCheck ForDatabaseReference(int id)
        {
            if (implementedChecks == null)
                FindImplementedChecks();

            return implementedChecks.FirstOrDefault(check => check.DatabaseReference == id);
        }

        private static SortedSet<QualityCheck> implementedChecks;
        public static ISet<QualityCheck> FindImplementedChecks()
        {
            // Use reflection to find all sub-classes of QualityCheck, create an instance
            // for each of those and return the (cached) result in a sorted set
            if (implementedChecks == null)
            {
                implementedChecks = new SortedSet<QualityCheck>();

                foreach (Type type in Assembly.GetAssembly(typeof(QualityCheck)).GetTypes()
                    .Where(myType => myType.IsClass && !myType.IsAbstract && myType.IsSubclassOf(typeof(QualityCheck))))
                {
                    implementedChecks.Add((QualityCheck)Activator.CreateInstance(type));
                }

            }

            return new SortedSet<QualityCheck>(implementedChecks);
        }        
    }
}
