using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json; // Newtonsoft로 통일

namespace Team3
{
    public class ScheduleManager
    {
        // 전체 일정 저장 리스트
        private List<Schedule> schedules = new List<Schedule>();

        // 일정 추가 시도
        // true : 추가 성공
        // false : 충돌 등으로 추가 실패
        public bool TryAddSchedule(Schedule newSchedule)
        {
            // 1. 장소가 있는 일정끼리 시간대 자체가 겹치는지 먼저 검사
            if (HasTimeConflict(newSchedule))
            {
                return false;
            }

            // 2. 이동시간 담당 엔진 연결
            var travelService = new TravelService();

            // 현재 보관 중인 schedules 리스트를 넘겨서 이동 거리/시간 계산
            bool canMove = travelService.CanInsert(newSchedule, this.schedules);

            // 이동 불가능 시(예: 30분 만에 강남에서 원주 이동 불가) 추가 실패 반환
            if (!canMove)
            {
                return false;
            }

            // 3. 모든 검증 통과 시 리스트에 추가
            schedules.Add(newSchedule);

            // 시작 시간 기준 오름차순 정렬
            schedules = schedules
                .OrderBy(s => s.StartDateTime)
                .ToList();

            return true;
        }

        // 시간 충돌 검사
        // 장소가 있는 일정끼리만 진짜 충돌로 판단
        private bool HasTimeConflict(Schedule newSchedule)
        {
            foreach (Schedule oldSchedule in schedules)
            {
                // 시간 겹침 검사
                bool isOverlap =
                    newSchedule.StartDateTime < oldSchedule.EndDateTime &&
                    newSchedule.EndDateTime > oldSchedule.StartDateTime;

                // 시간이 겹칠 경우
                if (isOverlap)
                {
                    // 둘 다 장소가 있으면 실제 충돌
                    if (newSchedule.HasLocation && oldSchedule.HasLocation)
                    {
                        return true;
                    }
                }
            }

            // 충돌 없음
            return false;
        }

        // 시간이 겹치는 일정 리스트 반환
        // 장소 없는 일정 처리 시 사용자 확인용
        public List<Schedule> GetOverlappingSchedules(Schedule newSchedule)
        {
            return schedules
            .Where(s =>
                    newSchedule.StartDateTime < s.EndDateTime &&
                    newSchedule.EndDateTime > s.StartDateTime)
                .ToList();
        }

        // 새 일정 기준 바로 이전 일정 찾기
        public Schedule GetPreviousSchedule(Schedule newSchedule)
        {
            return schedules
                .Where(s => s.EndDateTime <= newSchedule.StartDateTime)
                .OrderByDescending(s => s.EndDateTime)
                .FirstOrDefault();
        }

        // 새 일정 기준 바로 다음 일정 찾기
        public Schedule GetNextSchedule(Schedule newSchedule)
        {
            return schedules
                .Where(s => s.StartDateTime >= newSchedule.EndDateTime)
                .OrderBy(s => s.StartDateTime)
                .FirstOrDefault();
        }

        // 전체 일정 리스트 반환
        public List<Schedule> GetSchedules()
        {
            return schedules.ToList();
        }

        // 일정 조회
        public Schedule GetSchedule(DateTime startDateTime, DateTime endDateTime, string memo)
        {
            return schedules
                .FirstOrDefault(s =>
                    s.StartDateTime == startDateTime &&
                    s.EndDateTime == endDateTime &&
                    s.Memo == memo);
        }

        // 일정 삭제
        public bool DeleteSchedule(DateTime startDateTime, DateTime endDateTime, string memo)
        {
            Schedule target = GetSchedule(startDateTime, endDateTime, memo);

            if (target == null)
            {
                return false;
            }

            schedules.Remove(target);
            return true;
        }

        // 강제 일정 추가
        public void ForceAddSchedule(Schedule newSchedule)
        {
            schedules.Add(newSchedule);
            schedules = schedules
                .OrderBy(s => s.StartDateTime)
                .ToList();
        }

        // 일정 파일 저장
        public void SaveSchedules(string filePath)
        {
            string json = JsonConvert.SerializeObject(schedules, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        public void LoadSchedules(string filePath)
        {
            if (!File.Exists(filePath)) { schedules = new List<Schedule>(); return; }
            string json = File.ReadAllText(filePath);
            schedules = JsonConvert.DeserializeObject<List<Schedule>>(json) ?? new List<Schedule>();
            schedules = schedules.OrderBy(s => s.StartDateTime).ToList();
        }
    }
}