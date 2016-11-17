using M4DBO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    /// <summary>
    /// Quality check base class, defines all the interesting methods.
    /// </summary>
    public abstract class QualityCheck : IEquatable<QualityCheck>, IComparable<QualityCheck>, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public abstract string Name { get; }

        public abstract string Description { get; }

        public abstract short DatabaseReference { get; }

        private bool enabled = false;
        public bool Enabled
        {
            get { return enabled; }
            set
            {
                enabled = value;
                NotifyChange();
            }
        }

        private int percentage = 0;
        public int Completion
        {
            get { return percentage; }
            protected set
            {
                percentage = value;
                NotifyChange();
            }
        }

        /// <summary>
        /// Asynchronously estimate check running time for the set of time series given by a filter.
        /// </summary>
        /// <param name="filter">The filter used to select a sub-set of time series from the database</param>
        /// <param name="token">Token to cancel the async operation</param>
        /// <returns>Task to determine estimated check running time in milli-seconds</returns>
        public abstract Task<int> EstimateExecutionTimeAsync(Filter filter, CancellationToken token);

        /// <summary>
        /// Estimate average check running time for a single time series.
        /// </summary>
        /// <returns>Average execution time for a single time series in milli-seconds.</returns>
        protected abstract short EstimateExecutionTime();

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

            return Equals(obj as Finding);
        }

        public override int GetHashCode()
        {
            return DatabaseReference;
        }

        public int CompareTo(QualityCheck other)
        {
            return Name.CompareTo(other.Name);
        }

        protected dboList TSNrListFromFilter(Filter filter)
        {
            dboList result = new dboList();
            result.FromString(filter.Object.GetTSNumbers(), VBA.VbVarType.vbLong);

            return result;
        }

        /// <summary>
        /// Find source category information for given time series.
        /// Assumes the series' related key have already been read.
        /// </summary>
        /// <param name="series">The time series to inspect.</param>
        /// <returns>A number of category objects (likely 1) or an empyt set,
        /// if no descriptor is set. Never null.</returns>
        protected ISet<Finding.Category> CategoriesForTimeSeries(TimeSeries series)
        {
            HashSet<Finding.Category> result = new HashSet<Finding.Category>();
            const int CategoryDimensionNumber = 13;

            foreach (dboTSKey key in series.Object.TSKeys)
                if (key.DimNr == CategoryDimensionNumber)
                {
                    dboTreeObject descriptor = series.Object.Database.TreeObjects[key.ObjNr];
                    if (descriptor != null)
                        result.Add(new Finding.Category(descriptor.Name, descriptor.ObjNr));
                }

            return result;
        }

        protected ISet<Finding.ContactEnum> ContactsForTimeSeries(TimeSeries series)
        {
            HashSet<Finding.ContactEnum> result = new HashSet<Finding.ContactEnum>();
            
            // Get documentation for this time series
            dboAnnexObjects objects = series.Object.Database.CreateObject_AnnexObjects();
            objects.DbReadByReference_Docu(mspDocuTypeEnum.mspDocuTypeTS, series.Object.TsNr);
            dboAnnexObject annexObject = objects.GetObject_Docu(series.Object.TsNr, mspDocuTypeEnum.mspDocuTypeTS, mspTimeKeyEnum.mspTimeKeyYear, 0, 0, 0);

            if (annexObject != null)
            {
                // Get all documentation components for the time series
                dboAnnexSetLinks links = series.Object.Database.CreateObject_AnnexSetLinks();
                links.DbReadByReference(annexObject.AnnexObjNr, mspAnnexTypeEnum.mspAnnexTypeDocu);

                // Find contact information
                foreach (dboAnnexSetLink link in links)
                    if (link.ComponentNr == 12)
                    {
                        dboAnnexItemDatas datas = series.Object.Database.CreateObject_AnnexItemDatas();
                        datas.DbReadByItemNr(link.AnnexSetNr, 12, 63, false, 0);

                        IEnumerator dataEnum = datas.GetEnumerator();
                        if (dataEnum.MoveNext())
                            result.Add(FindEnumValueForAnnexItemPoolReference((dataEnum.Current as dboAnnexItemData).ReferenceData));
                    }
            }

            if (result.Count == 0)
                result.Add(Finding.ContactEnum.NN);
            
            return result;
        }

        private Finding.ContactEnum FindEnumValueForAnnexItemPoolReference(int referenceData)
        {
            switch (referenceData)
            {
                case 154: return Finding.ContactEnum.Schiller;
                case 155: return Finding.ContactEnum.Rimkus;
                case 232: return Finding.ContactEnum.Boettcher;
                case 270: return Finding.ContactEnum.Kludt; 
                case 278: return Finding.ContactEnum.Kotzulla;
                case 294: return Finding.ContactEnum.Juhrich;
                case 508: return Finding.ContactEnum.Kuntze;
                case 556: return Finding.ContactEnum.Hausmann;
                case 624: return Finding.ContactEnum.Doering;
                case 743: return Finding.ContactEnum.Reichel;
                default: return Finding.ContactEnum.NN;
            }
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
