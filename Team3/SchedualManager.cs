using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;

namespace Team3
{
    internal class SchedualManager
    {
        // 전체 일정 저장 리스트
        private List<Schedule> schedules = new List<Schedule>();


        // 일정 추가 시도
        // true : 추가 성공
        // false : 충돌 등으로 추가 실패
        public bool TryAddSchedule(Schedule newSchedule)
        {
            // 시간 충돌 검사
            // 둘 다 장소가 있는 일정이면 충돌로 판단
            if (HasTimeConflict(newSchedule))
            {
                return false;
            }

            // 새 일정 기준 이전 일정 찾기
            Schedule prev = GetPreviousSchedule(newSchedule);

            // 새 일정 기준 다음 일정 찾기
            Schedule next = GetNextSchedule(newSchedule);

            // 여기서 4번 이동시간 담당 코드 연결 예정
            // 예시:
            // bool canMove = MoveService.CheckMove(prev, newSchedule, next);

            // 이동 불가능 시 일정 추가 실패
            // if (!canMove)
            // {
            //     return false;
            // }

            // 일정 추가
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
                    if (newSchedule.HasLocation &&
                        oldSchedule.HasLocation)
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
                // 새 일정 시작 전에 끝나는 일정만 선택
                .Where(s => s.EndDateTime <= newSchedule.StartDateTime)

                // 가장 최근 일정 선택
                .OrderByDescending(s => s.EndDateTime)

                // 첫 번째 일정 반환
                .FirstOrDefault();
        }


        // 새 일정 기준 바로 다음 일정 찾기
        public Schedule GetNextSchedule(Schedule newSchedule)
        {
            return schedules
                // 새 일정 종료 이후 시작하는 일정만 선택
                .Where(s => s.StartDateTime >= newSchedule.EndDateTime)

                // 가장 가까운 다음 일정 선택
                .OrderBy(s => s.StartDateTime)

                // 첫 번째 일정 반환
                .FirstOrDefault();
        }


        // 전체 일정 리스트 반환
        // 외부 수정 방지를 위해 복사본 반환
        public List<Schedule> GetSchedules()
        {
            return schedules.ToList();
        }


        // 일정 조회
        // 시작 시간, 종료 시간, 메모가 모두 같은 일정 찾기
        public Schedule GetSchedule(DateTime startDateTime, DateTime endDateTime, string memo)
        {
            return schedules
                .FirstOrDefault(s =>
                    s.StartDateTime == startDateTime &&
                    s.EndDateTime == endDateTime &&
                    s.Memo == memo);
        }
        // 일정 삭제
        // 시작 시간, 종료 시간, 메모가 모두 같은 일정 삭제
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
        // 장소 없는 일정 겹침 시 사용자 허용용
        public void ForceAddSchedule(Schedule newSchedule)
        {
            // 충돌 검사 없이 바로 추가
            schedules.Add(newSchedule);

            // 시간순 정렬
            schedules = schedules
                .OrderBy(s => s.StartDateTime)
                .ToList();
        }


        // 일정 파일 저장
        // JSON 형태로 저장
        public void SaveSchedules(string filePath)
        {
            // schedules 리스트 → JSON 문자열 변환
            string json =
                JsonSerializer.Serialize(schedules);

            // 파일 저장
            File.WriteAllText(filePath, json);
        }


        // 일정 파일 불러오기
        public void LoadSchedules(string filePath)
        {
            // 파일이 없으면 빈 리스트 생성
            if (File.Exists(filePath) == false)
            {
                schedules = new List<Schedule>();
                return;
            }

            // 파일 내용 읽기
            string json = File.ReadAllText(filePath);

            // JSON → 리스트 변환
            schedules = JsonSerializer.Deserialize<List<Schedule>>(json);

            // null 방지
            if (schedules == null)
            {
                schedules = new List<Schedule>();
            }

            // 시간순 정렬
            schedules = schedules
                .OrderBy(s => s.StartDateTime)
                .ToList();
        }
    }
}
