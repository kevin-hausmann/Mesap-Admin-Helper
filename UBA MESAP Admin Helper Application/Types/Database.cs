using System;
using M4DBO;

namespace UBA.Mesap.AdminHelper.Types
{
    class Database
    {
        private String _name;
        private String _id;

        public Database(dboInstalledDB db)
        {
            _name = db.Name;
            _id = db.ID;
        }

        public Database(dboDatabase db)
        {
            _name = db.Root.InstalledDbs[db.DbNr].Name;
            _id = db.Root.InstalledDbs[db.DbNr].ID;
        }

        public String Name
        {
            get { return _name; }
        }

        public String ID
        {
            get { return _id; }
        }

        public override bool Equals(object obj)
        {
            Database other = obj as Database;
            
            return other.ID.Equals(ID);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
