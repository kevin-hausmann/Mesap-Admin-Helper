using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UBA.Mesap.AdminHelper.Types
{
    public abstract class QualityCheck : IComparable<QualityCheck>
    {
        public abstract string Id
        {
            get;
        }

        public abstract string Name
        {
            get;
        }

        public abstract string Description
        {
            get;
        }

        public int CompareTo(QualityCheck other)
        {
            return Name.CompareTo(other.Name);
        }
    }
}
