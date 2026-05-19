using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;

namespace Team3
{
    public class ScheduleManager
    {
        // 전체 일정 저장 리스트
        private List<Schedule> schedules = new List<Schedule>();

        // ─────────────────────────────────────────────
        // 일정 추가 시도
        // true : 추가 성공
        // false : 충돌 또는 이동 불가로 추가 실패
        // ─────────────────────────────────────────────
        public bool TryAddSchedule(Schedule newSchedule, TransportMode mode = TransportMode.Car)
        {
            // 1단계: 시간 충돌 검사 (장소 있는 일정끼리만)
            if (HasTimeConflict(newSchedule))
            {
                return false;
            }

            // 2단계: 이동시간 검사 (TravelService 연동)
            var travelService = new TravelService();
            bool canMove = travelService.CanInsert(newSchedule, schedules, mode);

            if (!canMove)
            {
                return false;
            }

            // 일정 추가
            schedules.Add(newSchedule);

            // 시작 시간 기준 오름차순 정렬
            schedules = schedules
                .OrderBy(s => s.StartDateTime)
                .ToList();

            return true;
        }

        // ─────────────────────────────────────────────
        // 시간 충돌 검사
        // 장소가 있는 일정끼리만 진짜 충돌로 판단
        // ─────────────────────────────────────────────
        private bool HasTimeConflict(Schedule newSchedule)
        {
            foreach (Schedule oldSchedule in schedules)
            {
                bool isOverlap =
                    newSchedule.StartDateTime < oldSchedule.EndDateTime &&
                    newSchedule.EndDateTime > oldSchedule.StartDateTime;

                if (isOverlap)
                {
                    if (newSchedule.HasLocation && oldSchedule.HasLocation)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // ─────────────────────────────────────────────
        // 시간이 겹치는 일정 리스트 반환
        // 장소 없는 일정 처리 시 사용자 확인용
        // ─────────────────────────────────────────────
        public List<Schedule> GetOverlappingSchedules(Schedule newSchedule)
        {
            return schedules
                .Where(s =>
                    newSchedule.StartDateTime < s.EndDateTime &&
                    newSchedule.EndDateTime > s.StartDateTime)
                .ToList();
        }

        // ─────────────────────────────────────────────
        // 새 일정 기준 바로 이전 일정 찾기
        // ─────────────────────────────────────────────
        public Schedule GetPreviousSchedule(Schedule newSchedule)
        {
            return schedules
                .Where(s => s.EndDateTime <= newSchedule.StartDateTime)
                .OrderByDescending(s => s.EndDateTime)
                .FirstOrDefault();
        }

        // ─────────────────────────────────────────────
        // 새 일정 기준 바로 다음 일정 찾기
        // ─────────────────────────────────────────────
        public Schedule GetNextSchedule(Schedule newSchedule)
        {
            return schedules
                .Where(s => s.StartDateTime >= newSchedule.EndDateTime)
                .OrderBy(s => s.StartDateTime)
                .FirstOrDefault();
        }

        // ─────────────────────────────────────────────
        // 전체 일정 리스트 반환 (복사본)
        // ─────────────────────────────────────────────
        public List<Schedule> GetSchedules()
        {
            return schedules.ToList();
        }

        // ─────────────────────────────────────────────
        // 일정 조회
        // ─────────────────────────────────────────────
        public Schedule GetSchedule(DateTime startDateTime, DateTime endDateTime, string memo)
        {
            return schedules
                .FirstOrDefault(s =>
                    s.StartDateTime == startDateTime &&
                    s.EndDateTime == endDateTime &&
                    s.Memo == memo);
        }

        // ─────────────────────────────────────────────
        // 일정 삭제
        // ─────────────────────────────────────────────
        public bool DeleteSchedule(DateTime startDateTime, DateTime endDateTime, string memo)
        {
            Schedule target = GetSchedule(startDateTime, endDateTime, memo);
            if (target == null) return false;
            schedules.Remove(target);
            return true;
        }

        // ─────────────────────────────────────────────
        // 강제 일정 추가 (장소 없는 일정 겹침 시 사용자 허용용)
        // ─────────────────────────────────────────────
        public void ForceAddSchedule(Schedule newSchedule)
        {
            schedules.Add(newSchedule);
            schedules = schedules.OrderBy(s => s.StartDateTime).ToList();
        }

        // ─────────────────────────────────────────────
        // 일정 파일 저장 (JSON)
        // ─────────────────────────────────────────────
        public void SaveSchedules(string filePath)
        {
            string json = JsonConvert.SerializeObject(schedules, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        // ─────────────────────────────────────────────
        // 일정 파일 불러오기
        // ─────────────────────────────────────────────
        public void LoadSchedules(string filePath)
        {
            if (!File.Exists(filePath))
            {
                schedules = new List<Schedule>();
                return;
            }

            string json = File.ReadAllText(filePath);
            schedules = JsonConvert.DeserializeObject<List<Schedule>>(json) ?? new List<Schedule>();
            schedules = schedules.OrderBy(s => s.StartDateTime).ToList();
        }
    }
}
