using M4DBO;
using System;
using System.Collections;
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
        private const int CategoryDimensionNumber = 13;

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
                default: return 0;
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

    /// <summary>
    /// A quality check finding represents a potential issue found by a quality check.
    /// </summary>
    public class Finding : IExportable
    {
        public string Title { get; set; }
        public string Description { get; set; }

        public QualityCheck Check { get; set; }
        public bool Exists { get; set; }
        
        public PriorityEnum Priority { get; set; }
        public string PriorityLabel => GetEnumDescription(Priority);

        private ISet<ContactEnum> contacts = new HashSet<ContactEnum>();
        public string ContactLabel => String.Join("|", contacts.Select(contact => GetEnumDescription(contact)));

        public class Category
        {
            public Category(string name, int id)
            {
                Name = name;
                Id = id;
            }

            public string Name { get; }
            public int Id { get; }
        }

        private ISet<Category> categories = new HashSet<Category>();
        public string CategoryLabel => String.Join("|", categories.Select(category => category.Name));
                
        public enum StatusEnum { New = 106, Done = 107, NoChange = 116 }
        public enum PriorityEnum { Blocker = 108, High = 109, Medium = 110, Low = 111 }
        public enum ContactEnum
        {
            [Description("Detlef Rimkus")]
            Rimkus = 7,
            [Description("Stephan Schiller")]
            Schiller = 35,
            [Description("David Kuntze")]
            Kuntze = 72,
            [Description("Kristina Juhrich")]
            Juhrich = 86,
            [Description("Christian Böttcher")]
            Boettcher = 123,
            [Description("Michael Kotzulla")]
            Kotzulla = 124,
            [Description("Kevin Hausmann")]
            Hausmann = 125,
            [Description("Robert Kludt")]
            Kludt = 127,
            [Description("Ulrike Döring")]
            Doering = 222,
            [Description("Jens Reichel")]
            Reichel = 227
        }

        private const int titleItemNr = 77;
        private const int descriptionItemNr = 78;

        private const int categoryItemNr = 79;
        // private const int statusItemNr = 80;
        private const int priorityItemNr = 81;
        private const int contactItemNr = 82;
        // private const int createDateItemNr = 84;
        private const int originItemNr = 92;
        
        public Finding() { }

        public Finding(QualityCheck check, string title, string description,
            ISet<Category> categories, ISet<ContactEnum> contacts, PriorityEnum prio) : this()
        {
            this.Check = check;
            this.Title = title;
            this.Description = description;
            this.categories = categories;
            this.contacts = contacts;
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

            foreach (dboEventItemData contact in dboEvent.EventItemDatas.GetCollection(contactItemNr))
                if (Enum.IsDefined(typeof(ContactEnum), contact.ReferenceData))
                    finding.contacts.Add((ContactEnum)Enum.ToObject(typeof(ContactEnum), contact.ReferenceData));
                else
                    Console.WriteLine(String.Format("Unknown contact reference {0} for finding \"{1}\"", contact.ReferenceData, finding.Title));
            
            foreach (dboEventItemData category in dboEvent.EventItemDatas.GetCollection(categoryItemNr))
            {
                dboTreeObject descriptor = dboEvent.Database.TreeObjects[category.ReferenceData];
                if (descriptor != null)
                   finding.categories.Add(new Category(descriptor.Name, descriptor.ObjNr));
                else
                    Console.WriteLine(String.Format("Unknown descriptor reference {0} for finding \"{1}\"", category.ReferenceData, finding.Title));
            }

            return finding;
        }

        public static void ToDatabaseEntry(dboEvent dboEvent, Finding finding)
        {
            if (dboEvent.IsWriteProtected || !dboEvent.IsModifyEnabled)
                throw new Exception(String.Format("Database event object with number \"{0}\" is locked.", dboEvent.EventNr));
            else if (!FindingHasProperData(finding))
                throw new Exception("Finding does not have all its required fields set.");
            else
            {
                SetFieldValueSecurely(dboEvent, titleItemNr, "title", finding.Title);
                SetFieldValueSecurely(dboEvent, descriptionItemNr, "description", finding.Description);

                SetFieldValueSecurely(dboEvent, originItemNr, "origin", finding.Check.DatabaseReference);
                SetFieldValueSecurely(dboEvent, priorityItemNr, "priority", (int)finding.Priority);

                SetFieldValueSecurely(dboEvent, contactItemNr, "contact", finding.contacts.Select(contact => (int)contact));
                SetFieldValueSecurely(dboEvent, categoryItemNr, "category", finding.categories.Select(category => category.Id));
            }
        }

        private static bool FindingHasProperData(Finding finding)
        {
            return finding != null && !String.IsNullOrWhiteSpace(finding.Title)
                && !String.IsNullOrWhiteSpace(finding.Description)
                && finding.Check != null
                && finding.Priority != 0
                && finding.contacts != null && finding.contacts.FirstOrDefault() != 0;
        }

        private static void SetFieldValueSecurely(dboEvent dboEvent, int fieldNr, string fieldName, object value)
        {
            // First, test the parameters we are given and make sure they make sense for
            // the database we are on. We do not want to create incomplete findings. For
            // any missing piece we throw an exception with a proper description of the problem.
            dboEventInventory inventory = dboEvent.Database.EventInventories[dboEvent.InventoryNr];
            dboEventItem field = inventory.EventItems[fieldNr];

            if (field == null)
                throw new Exception(String.Format("Field with number \"{0}\", supposedly named \"{1}\", not found.", fieldNr, fieldName));
            else
                switch (field.ItemType)
                {
                    case mspEventItemTypeEnum.mspEventItemTypeTextPool:
                        // Make sure the text pool element we want to set exists
                        dboEventItemTextPools pools = inventory.CreateObject_EventItemTextPools();
                        pools.DbReadByItemNr(fieldNr, true);

                        if (!pools.Exist(Int32.Parse(value.ToString())))
                            throw new Exception(String.Format("Text pool value \"{0}\" does not exist for field \"{1}\".", value, fieldName));
                        else break;
                    case mspEventItemTypeEnum.mspEventItemTypeUser:
                        // Make sure all users exist. TODO: Make sure users are in the correct group.
                        foreach(object user in (IEnumerable)value)
                            if (dboEvent.Database.DbAccessUsers[Int32.Parse(user.ToString())] == null)
                                throw new Exception(String.Format("User with number \"{0}\" does not exist in database.", user));

                        break;
                    case mspEventItemTypeEnum.mspEventItemTypeMultiDescriptor:
                        // Make sure all categories exist.
                        foreach (object category in (IEnumerable)value)
                            if (dboEvent.Database.TreeObjects[Int32.Parse(category.ToString())] == null)
                                throw new Exception(String.Format("Category with number \"{0}\" does not exist in database.", category));

                        break;
                }

            // Okay, we made it here, so it is safe to assume that all preconditions are met.
            // Go set values on the event (finding), might either be multiple or a single one.
            if (!(value is string) && value is IEnumerable)
            {
                dboList list = new dboList();
                foreach (object item in (IEnumerable)value)
                    if (item != null)
                        list.Add(item);

                if (list.Count > 0)
                    dboEvent.EventItemDatas.set_Value(fieldNr, list);
            }
            else if (value != null)
                dboEvent.EventItemDatas.let_Value(fieldNr, value);
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
