using M4DBO;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UBA.Mesap.AdminHelper.Types.QualityChecks
{
    class VirtualFormulaCheck : TimeSeriesQualityCheck
    {
        public override string Id => "Virtuelle";

        public override TimeSpan EstimatedExecutionTimePerElement => TimeSpan.FromMilliseconds(25);
        
        protected override short StartYear => 1990;
        protected override short EndYear => 1995;
        
        protected override int[,] FindWorkloadFilter => new int[,] {};
        
        protected override void CheckTimeSeries(TimeSeries series, IProgress<ISet<Finding>> progress)
        {
            if (series.Object.VirtualType == mspVirtualTsTypeEnum.mspVirtualTsTypeVirtual)
            {
                // Find all IDs in Formula
                string[] ids = GetIDsUsedInFormula(series.Object.VirtualFormula); 

                // Make sure all referenced time series exist
                dboTSs allReferencedSeries = series.Object.Database.CreateObject_TSs(String.Join(",", ids));
                if (allReferencedSeries.Count != ids.Length)
                    Report(progress, new TimeSeries[] { series },
                            String.Format(FindingTitle, series.ID),
                            String.Format(FindingText, series.Object.VirtualFormula, series.Legend));
            }
        }

        private string[] GetIDsUsedInFormula(string formula)
        {
            List<string> ids = new List<string>();

            // Remove all unit stuff
            foreach (Match match in Regex.Matches(formula, @"\[(.*?)\]"))
                formula = formula.Replace(match.ToString(), String.Empty);

            // Extract IDs in double quotes
            foreach (Match match in Regex.Matches(formula, "\"([^\"]*)\""))
            {
                ids.Add(match.ToString().Replace("\"", String.Empty));
                formula = formula.Replace(match.ToString(), String.Empty);
            }
            
            // Remove all math stuff
            var reserved = new string[] { "(", ")", "[", "]", "*", "+", "-", "/", "NaNTo0" };
            foreach (var c in reserved)
                formula = formula.Replace(c, " ");
            
            // Find all non-quoted IDs and drop numerics
            foreach (string id in formula.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
                if (!Double.TryParse(id, out double result) && !ids.Contains(id))
                    ids.Add(id);

            return ids.ToArray();
        }
    }
}
