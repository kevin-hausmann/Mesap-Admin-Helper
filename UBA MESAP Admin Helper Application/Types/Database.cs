using M4DBO;

namespace UBA.Mesap.AdminHelper.Types
{
    /// <summary>
    /// Represents a Mesap database by ID and also holds its name.
    /// </summary>
    public class Database
    {
        public string Name { get; protected set; }
        public string Id { get; protected set; }

        public Database(dboInstalledDB db)
        {
            Name = db.Name;
            Id = db.ID;
        }

        public Database(dboDatabase db)
        {
            Name = db.Root.InstalledDbs[db.DbNr].Name;
            Id = db.Root.InstalledDbs[db.DbNr].ID;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            Database other = obj as Database;
            return other.Id.Equals(Id);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
