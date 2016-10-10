using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    class TSPEmissionFactorCheck : QualityCheck
    {
        public override string Id
        {
            get
            {
                return "TSP";
            }
        }

        public override string Name
        {
            get
            {
                return "TSP stuff";
            }
        }

        public override string Description
        {
            get
            {
                return "Test";
            }
        }
    }
}
