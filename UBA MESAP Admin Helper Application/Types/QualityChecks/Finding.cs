﻿using M4DBO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

using System.Linq;
using System.Reflection;
using System.Text;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    /// <summary>
    /// A quality check finding represents a potential issue found by a quality check.
    /// </summary>
    public class Finding : IExportable
    {
        public string Title { get; protected set; }
        public string Description { get; protected set; }
        
        /// <summary>
        /// IDs of the time series' checked and effected by this finding.
        /// </summary>
        public int[] TimeSeries { get; protected set; }
        public string TimeSeriesLabel => TimeSeries == null ? "None" : String.Join(",", TimeSeries);

        /// <summary>
        /// Check that made this finding
        /// </summary>
        public QualityCheck Check { get; protected set; }
        /// <summary>
        /// Whether or not this finding is already present in the Mesap database.
        /// </summary>
        public bool Exists { get; set; }
        public StatusEnum Status { get; set; }

        public PriorityEnum Priority { get; protected set; }
        public string PriorityLabel => GetEnumDescription(Priority);

        protected ISet<ContactEnum> contacts = new HashSet<ContactEnum>();
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

        protected ISet<Category> categories = new HashSet<Category>();
        public string CategoryLabel => String.Join("|", categories.Select(category => category.Name));

        public enum StatusEnum { New = 106, Done = 107, NoChange = 116 }
        public enum PriorityEnum { Blocker = 108, High = 109, Medium = 110, Low = 111 }
        public enum ContactEnum
        {
            [Description("N.N.")]
            NN = 0,
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
        private const int statusItemNr = 80;
        private const int priorityItemNr = 81;
        private const int contactItemNr = 82;
        // private const int createDateItemNr = 84;
        private const int originItemNr = 97;

        private Finding() { }

        public Finding(QualityCheck check, int[] tsNumbers, string title, string description,
            ISet<Category> categories, ISet<ContactEnum> contacts, PriorityEnum prio) : this()
        {
            this.Check = check;
            this.TimeSeries = tsNumbers;
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
            buffer.Append(TimeSeriesLabel + "\t");
            buffer.Append(Title + "\t");
            buffer.Append(Description + "\t");
            buffer.Append(CategoryLabel + "\t");
            buffer.Append(ContactLabel + "\t");
            buffer.Append(PriorityLabel + "\t");

            return buffer.ToString();
        }

        #endregion

        /// <summary>
        /// Marshal finding instance from an existing Mesap database event record.
        /// </summary>
        /// <param name="dboEvent">Database record to read data from.</param>
        /// <returns>The object with its fields set accordingly.</returns>
        public static Finding FromDatabaseEntry(dboEvent dboEvent)
        {
            Finding finding = new Finding();

            finding.Title = dboEvent.EventItemDatas.GetObject(titleItemNr).TextData;
            finding.Description = dboEvent.EventItemDatas.GetObject(descriptionItemNr).MemoData;

            finding.Check = QualityCheck.ForDatabaseReference((int) dboEvent.EventItemDatas.GetObject(originItemNr).ReferenceData);
            finding.Status = (StatusEnum)Enum.ToObject(typeof(StatusEnum),(int) dboEvent.EventItemDatas.GetObject(statusItemNr).ReferenceData);
            finding.Priority = (PriorityEnum)Enum.ToObject(typeof(PriorityEnum), (int) dboEvent.EventItemDatas.GetObject(priorityItemNr).ReferenceData);

            foreach (dboEventItemData contact in dboEvent.EventItemDatas.GetCollection(contactItemNr))
                if (Enum.IsDefined(typeof(ContactEnum), (int) contact.ReferenceData))
                    finding.contacts.Add((ContactEnum)Enum.ToObject(typeof(ContactEnum), (int) contact.ReferenceData));
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

            finding.Exists = true;
            return finding;
        }

        /// <summary>
        /// Save given finding's field information to an actual Mesap database record. Will make
        /// sure to only change the database if all information is given.
        /// </summary>
        /// <param name="dboEvent">Database object to save to. Needs to be writable.</param>
        /// <param name="finding">Finding to persist. Is checked for required data being set.</param>
        /// <exception cref="Exception">If any of the conditions for this method to execute is not met.</exception>
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
                        foreach (object user in (IEnumerable)value)
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
