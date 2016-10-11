using M4DBO;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

        private int percentage = 0;
        public int Completion {
            get
            {
                return percentage;
            }
            protected set
            {
                percentage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Completion"));
            }
        }

        public abstract Task RunAsync(dboTSFilter filter, CancellationToken cancellationToken, IProgress<ISet<Finding>> progress);

        public int CompareTo(QualityCheck other)
        {
            return Name.CompareTo(other.Name);
        }
    }

    public class Finding
    {
        public String Title { get; set; }
        public String CheckId { get; set; }
    }
}
