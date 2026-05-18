using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCalendar.Models
{
    public class Schedule
    {
        public DateTime StartDateTime { get; set; }  // 일정 시작 날짜+시간
        public DateTime EndDateTime { get; set; }    // 일정 종료 날짜+시간
        public string Location { get; set; }         // 장소
        public string Memo { get; set; }             // 내용

        public string TransportType { get; set; }

        public bool HasLocation
        {
            get
            {
                return !string.IsNullOrWhiteSpace(Location);
            }
        }
    }
}