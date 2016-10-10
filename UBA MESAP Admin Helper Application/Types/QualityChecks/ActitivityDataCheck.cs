using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    class ActitivityDataCheck : QualityCheck
    {
        public override string Id
        {
            get
            {
                return "AD";
            }
        }

        public override string Name
        {
            get
            {
                return "AD";
            }
        }

        public override string Description
        {
            get
            {
                return "Something";
            }
        }
    }
}
