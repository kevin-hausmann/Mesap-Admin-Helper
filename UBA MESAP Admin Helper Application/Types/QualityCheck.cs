using M4DBO;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace UBA.Mesap.AdminHelper.Types
{
    public abstract class QualityCheck : IComparable<QualityCheck>, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public abstract string Id { get; }

        public abstract string Name { get; }

        public abstract string Description { get; }

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
        /// Estimate check running time for the set of time series given by a filter.
        /// </summary>
        /// <param name="filter">The filter used to select a sub-set of time series from the database</param>
        /// <returns>Estimated check running time in milli-seconds</returns>
        public abstract long EstimateExecutionTime(Filter filter);

        /// <summary>
        /// Estimate average check running time for a single time series.
        /// </summary>
        /// <returns>Average execution time for a single time series in milli-seconds.</returns>
        protected abstract int EstimateExecutionTime();

        public abstract Task RunAsync(Filter filter, CancellationToken cancellationToken, IProgress<ISet<Finding>> progress);

        public int CompareTo(QualityCheck other)
        {
            return Name.CompareTo(other.Name);
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

    public class Finding
    {
        public String Title { get; set; }
        public String Description { get; set; }

        public CheckEnum Check { get; set; }
        public String CheckLabel => GetEnumDescription(Check);

        public PriorityEnum Priority { get; set; }
        public String PriorityLabel => GetEnumDescription(Priority);

        private ISet<ContactEnum> contacts = new HashSet<ContactEnum>();
        public String ContactLabel
        {
            get
            {
                string result = "";

                foreach (ContactEnum contact in contacts)
                    result += GetEnumDescription(contact) + "|";

                return result.Substring(0, result.Length - 1);
            }
        }

        public String Category { get; set; }
        
        public enum StatusEnum { New = 106, Done = 107, NoChange = 116 }
        public enum PriorityEnum { Blocker = 108, High = 109, Average = 110, Low = 111 }
        public enum CheckEnum
        {
            [Description("Manuell erstellt")]
            Manual = 113,
            [Description("Feinstaub EF-Vergleich")]
            EFPbvsPM10 = 114
        }
        public enum ContactEnum
        {
            [Description("Robert Kludt")]
            Kludt = 127,
            [Description("Kristina Juhrich")]
            Juhrich = 86,
            [Description("Deltef Rimkus")]
            Rimkus = 7
        }

        private const int titleItemNr = 77;
        private const int descriptionItemNr = 78;

        private const int categoryItemNr = 79;
        private const int statusItemNr = 80;
        private const int priorityItemNr = 81;
        private const int contactItemNr = 82;
        private const int createDateItemNr = 84;
        private const int originItemNr = 92;
        

        public static Finding FromDatabaseEntry(dboEvent dboEvent)
        {
            Finding finding = new Finding();

            finding.Title = dboEvent.EventItemDatas.GetObject(titleItemNr).TextData;
            finding.Description = dboEvent.EventItemDatas.GetObject(descriptionItemNr).MemoData;

            finding.Check = (CheckEnum) Enum.ToObject(typeof(CheckEnum), dboEvent.EventItemDatas.GetObject(originItemNr).ReferenceData);
            finding.Priority = (PriorityEnum) Enum.ToObject(typeof(PriorityEnum), dboEvent.EventItemDatas.GetObject(priorityItemNr).ReferenceData);

            dboCollection contacts = dboEvent.EventItemDatas.GetCollection(contactItemNr);
            foreach(dboEventItemData data in contacts)
            {
                finding.contacts.Add((ContactEnum) Enum.ToObject(typeof(ContactEnum), data.ReferenceData));
            }
            
            return finding;
        }

        private static string GetEnumDescription(Enum value)
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
