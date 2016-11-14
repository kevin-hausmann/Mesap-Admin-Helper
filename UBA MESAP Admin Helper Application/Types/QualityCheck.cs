using M4DBO;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UBA.Mesap.AdminHelper.Types
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

    public class Finding : IExportable
    {
        public string Title { get; set; }
        public string Description { get; set; }

        public QualityCheck Check { get; set; }
        public bool Exists { get; set; }
        
        public PriorityEnum Priority { get; set; }
        public string PriorityLabel => GetEnumDescription(Priority);

        private ISet<ContactEnum> contacts = new HashSet<ContactEnum>();
        public string ContactLabel
        {
            get
            {
                string result = "";

                foreach (ContactEnum contact in contacts)
                    result += GetEnumDescription(contact) + "|";

                return result.Substring(0, result.Length - 1);
            }
        }

        public string Category { get; set; }
        
        public enum StatusEnum { New = 106, Done = 107, NoChange = 116 }
        public enum PriorityEnum { Blocker = 108, High = 109, Medium = 110, Low = 111 }
        public enum ContactEnum
        {
            [Description("Robert Kludt")]
            Kludt = 127,
            [Description("Kristina Juhrich")]
            Juhrich = 86,
            [Description("Detlef Rimkus")]
            Rimkus = 7,
            [Description("N.N.")]
            NN = 0
        }

        private const int titleItemNr = 77;
        private const int descriptionItemNr = 78;

        private const int categoryItemNr = 79;
        private const int statusItemNr = 80;
        private const int priorityItemNr = 81;
        private const int contactItemNr = 82;
        private const int createDateItemNr = 84;
        private const int originItemNr = 92;
        
        public Finding() { }

        public Finding(QualityCheck check, string title, string description, ContactEnum contact, PriorityEnum prio) : this()
        {
            this.Check = check;
            this.Title = title;
            this.Description = description;
            this.contacts.Add(contact);
            this.Priority = prio;
        }

        #region IExportable Members

        public string ToCVSString()
        {
            StringBuilder buffer = new StringBuilder();

            buffer.Append(Check.Name + "\t");
            buffer.Append(Title + "\t");
            buffer.Append(Description + "\t");
            buffer.Append(ContactLabel + "\t");
            buffer.Append(PriorityLabel + "\t");

            return buffer.ToString();
        }

        #endregion

        public static Finding FromDatabaseEntry(dboEvent dboEvent)
        {
            Finding finding = new Finding();

            finding.Title = dboEvent.EventItemDatas.GetObject(titleItemNr).TextData;
            finding.Description = dboEvent.EventItemDatas.GetObject(descriptionItemNr).MemoData;

            finding.Check = QualityCheck.ForDatabaseReference(dboEvent.EventItemDatas.GetObject(originItemNr).ReferenceData);
            finding.Priority = (PriorityEnum) Enum.ToObject(typeof(PriorityEnum), dboEvent.EventItemDatas.GetObject(priorityItemNr).ReferenceData);

            dboCollection contacts = dboEvent.EventItemDatas.GetCollection(contactItemNr);
            foreach(dboEventItemData data in contacts)
            {
                finding.contacts.Add((ContactEnum) Enum.ToObject(typeof(ContactEnum), data.ReferenceData));
            }
            
            return finding;
        }

        public static void ToDatabaseEntry(dboEvent dboEvent, Finding finding)
        {
            dboEvent.EventItemDatas.let_Value(titleItemNr, finding.Title);
            dboEvent.EventItemDatas.let_Value(descriptionItemNr, finding.Description);
        }

        public static string GetEnumDescription(Enum value)
        {
            FieldInfo field = value.GetType().GetField(value.ToString());

            DescriptionAttribute[] attributes =
                (DescriptionAttribute[])field.GetCustomAttributes(typeof(DescriptionAttribute), false);

            if (attributes != null && attributes.Length > 0)
                return attributes[0].Description;
            else
                return value.ToString();
        }
    }
}
