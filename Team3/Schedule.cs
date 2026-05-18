using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Team3
{
    internal class Schedule
    {
        public DateTime StartDateTime;
        public DateTime EndDateTime;
        public string Location;
        public string Memo;
        public string TransportType;

        public bool HasLocation
        {
            get { return !string.IsNullOrWhiteSpace(Location); }
        }
    }
}
