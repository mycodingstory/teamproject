using SmartCalendar.Models;
using SmartCalendar.Services;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace SmartCalendar
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            //InitializeComponent();

            //var service = new TravelService();

            //// 기존 일정: 강남 오늘 10:00~12:00
            //var existing = new List<Schedule>
            //{
            //    new Schedule
            //    {
            //        StartDateTime = DateTime.Today.AddHours(10),
            //        EndDateTime   = DateTime.Today.AddHours(12),
            //        Location      = "강남",
            //        Memo          = "회의"
            //    }
            //};

            //// ── 테스트 1: 가능한 경우 ──
            //// 강남 12:00 종료 + 이동 25분 = 12:25 → 13:00 시작이므로 가능
            //var sched1 = new Schedule
            //{
            //    StartDateTime = DateTime.Today.AddHours(13),
            //    EndDateTime = DateTime.Today.AddHours(15),
            //    Location = "판교",
            //    Memo = "점심"
            //};

            //// ── 테스트 2: 불가능한 경우 ──
            //// 강남 12:00 종료 + 이동 25분 = 12:25 → 12:10 시작이므로 불가능
            //var sched2 = new Schedule
            //{
            //    StartDateTime = DateTime.Today.AddHours(12).AddMinutes(10),
            //    EndDateTime = DateTime.Today.AddHours(14),
            //    Location = "판교",
            //    Memo = "점심"
            //};

            //// 테스트 1 결과
            //bool result1 = service.CanInsert(sched1, existing, TransportMode.Car);
            //string msg1 = result1
            //    ? "✅ [테스트 1] 13:00 판교 점심 → 추가 가능!"
            //    : "❌ [테스트 1] 13:00 판교 점심 → 추가 불가능";

            //// 테스트 2 결과 + 대체 시간 추천
            //bool result2 = service.CanInsert(sched2, existing, TransportMode.Car);
            //string msg2;
            //if (result2)
            //{
            //    msg2 = "✅ [테스트 2] 12:10 판교 점심 → 추가 가능!";
            //}
            //else
            //{
            //    DateTime? suggest = service.SuggestEarliestStart(sched2, existing, TransportMode.Car);
            //    string suggestStr = suggest.HasValue
            //        ? suggest.Value.ToString("HH:mm") + " 이후 가능"
            //        : "추천 불가";
            //    msg2 = "❌ [테스트 2] 12:10 판교 점심 → 추가 불가능\n💡 대체 시간: " + suggestStr;
            //}

            //MessageBox.Show(msg1 + "\n\n" + msg2, "일정 테스트 결과");
        }
    }
}