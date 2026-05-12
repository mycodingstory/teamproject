using SmartCalendar.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartCalendar.Services
{
    public enum TransportMode { Car, Train, Bus }

    public class TravelService
    {
        // ─────────────────────────────────────────────
        // 자동차 기준 이동시간 테이블 (단위: 분)
        // ─────────────────────────────────────────────
        private readonly Dictionary<string, Dictionary<string, int>> carTime =
            new Dictionary<string, Dictionary<string, int>>()
        {
            { "원주", new Dictionary<string, int> { {"강남",100}, {"판교",90},  {"수원",110}, {"여주",30},  {"이천",50}  } },
            { "강남", new Dictionary<string, int> { {"원주",100}, {"판교",25},  {"수원",50},  {"여주",80},  {"이천",70}  } },
            { "판교", new Dictionary<string, int> { {"원주",90},  {"강남",25},  {"수원",30},  {"여주",60},  {"이천",50}  } },
            { "수원", new Dictionary<string, int> { {"원주",110}, {"강남",50},  {"판교",30},  {"여주",70},  {"이천",60}  } },
            { "여주", new Dictionary<string, int> { {"원주",30},  {"강남",80},  {"판교",60},  {"수원",70},  {"이천",20}  } },
            { "이천", new Dictionary<string, int> { {"원주",50},  {"강남",70},  {"판교",50},  {"수원",60},  {"여주",20}  } },
        };

        // 기차 기준 이동시간 테이블 (단위: 분)
        private readonly Dictionary<string, Dictionary<string, int>> trainTime =
            new Dictionary<string, Dictionary<string, int>>()
        {
            { "원주", new Dictionary<string, int> { {"강남",70},  {"판교",80},  {"수원",90},  {"여주",40},  {"이천",60}  } },
            { "강남", new Dictionary<string, int> { {"원주",70},  {"판교",30},  {"수원",50},  {"여주",80},  {"이천",80}  } },
            { "판교", new Dictionary<string, int> { {"원주",80},  {"강남",30},  {"수원",40},  {"여주",70},  {"이천",70}  } },
            { "수원", new Dictionary<string, int> { {"원주",90},  {"강남",50},  {"판교",40},  {"여주",90},  {"이천",80}  } },
            { "여주", new Dictionary<string, int> { {"원주",40},  {"강남",80},  {"판교",70},  {"수원",90},  {"이천",20}  } },
            { "이천", new Dictionary<string, int> { {"원주",60},  {"강남",80},  {"판교",70},  {"수원",80},  {"여주",20}  } },
        };

        // 버스 기준 이동시간 테이블 (단위: 분)
        private readonly Dictionary<string, Dictionary<string, int>> busTime =
            new Dictionary<string, Dictionary<string, int>>()
        {
            { "원주", new Dictionary<string, int> { {"강남",100}, {"판교",110}, {"수원",130}, {"여주",40},  {"이천",75}  } },
            { "강남", new Dictionary<string, int> { {"원주",100}, {"판교",35},  {"수원",65},  {"여주",95},  {"이천",95}  } },
            { "판교", new Dictionary<string, int> { {"원주",110}, {"강남",35},  {"수원",55},  {"여주",85},  {"이천",85}  } },
            { "수원", new Dictionary<string, int> { {"원주",130}, {"강남",65},  {"판교",55},  {"여주",105}, {"이천",95}  } },
            { "여주", new Dictionary<string, int> { {"원주",40},  {"강남",95},  {"판교",85},  {"수원",105}, {"이천",25}  } },
            { "이천", new Dictionary<string, int> { {"원주",75},  {"강남",95},  {"판교",85},  {"수원",95},  {"여주",25}  } },
        };

        // ─────────────────────────────────────────────
        // 이동시간 조회 함수
        // from: 출발 장소, to: 도착 장소, mode: 이동 수단
        // 반환값: 이동시간 (분)
        // ─────────────────────────────────────────────
        public int GetTravelTime(string from, string to, TransportMode mode)
        {
            if (from == to) return 0;

            // 이동수단에 맞는 테이블 선택
            Dictionary<string, Dictionary<string, int>> table;
            if (mode == TransportMode.Car)
                table = carTime;
            else if (mode == TransportMode.Train)
                table = trainTime;
            else
                table = busTime;

            if (table.ContainsKey(from) && table[from].ContainsKey(to))
                return table[from][to];

            return 30;
        }

        // ─────────────────────────────────────────────
        // 1단계: 시간 겹침 검사 (이신범 파트 연계)
        // 새 일정이 기존 일정과 시간이 겹치는지 확인
        //
        // 겹침 조건:
        //   new.StartDateTime < old.EndDateTime
        //   && new.EndDateTime > old.StartDateTime
        //   → 둘 다 true면 겹침
        //
        // 반환값: true = 겹침 있음 / false = 겹침 없음
        // ─────────────────────────────────────────────
        public bool IsOverlap(Schedule newSched, List<Schedule> existing)
        {
            foreach (var old in existing)
            {
                // 새 일정 시작이 기존 일정 종료 전이고
                // 새 일정 종료가 기존 일정 시작 후면 → 겹침
                if (newSched.StartDateTime < old.EndDateTime &&
                    newSched.EndDateTime > old.StartDateTime)
                {
                    return true; // 겹치는 일정 발견
                }
            }
            return false; // 겹치는 일정 없음
        }

        // ─────────────────────────────────────────────
        // 2단계: 이동시간 기반 가능 여부 판단 (네 파트 핵심)
        // 겹침 검사 통과 후 이동시간까지 고려해서 최종 판단
        //
        // 판단 기준:
        //   이전 일정 종료 + 이동시간 <= 새 일정 시작  →  앞 조건 통과
        //   새 일정 종료 + 이동시간 <= 다음 일정 시작  →  뒤 조건 통과
        //   둘 다 통과해야 추가 가능
        // ─────────────────────────────────────────────
        public bool CanInsert(Schedule newSched, List<Schedule> existing, TransportMode mode)
        {
            // 1단계: 시간 겹침 먼저 차단
            if (IsOverlap(newSched, existing))
                return false;

            // 기존 일정을 시작 시간 기준으로 오름차순 정렬
            var sorted = existing.OrderBy(s => s.StartDateTime).ToList();

            // 새 일정 시작 전에 끝나는 일정 중 가장 마지막 → 이전 일정
            var prev = sorted.LastOrDefault(s => s.EndDateTime <= newSched.StartDateTime);

            // 새 일정 종료 후에 시작하는 일정 중 가장 처음 → 다음 일정
            var next = sorted.FirstOrDefault(s => s.StartDateTime >= newSched.EndDateTime);

            // 이전 일정 기준 검사
            if (prev != null && prev.Location != null)
            {
                // 새 일정에 장소가 없으면 이동 중 수행 가능 → 이동시간 검사 생략
                if (newSched.Location == null)
                {
                    // 통과 (이동 중 가능한 일정)
                }
                else
                {
                    int travel = GetTravelTime(prev.Location, newSched.Location, mode);
                    if (prev.EndDateTime.AddMinutes(travel) > newSched.StartDateTime)
                        return false;
                }
            }

            // ── 다음 일정 기준 검사 ──
            if (next != null && next.Location != null)
            {
                int travel = GetTravelTime(newSched.Location, next.Location, mode);

                // 새 일정 종료 + 이동시간이 다음 일정 시작보다 늦으면 불가
                if (newSched.EndDateTime.AddMinutes(travel) > next.StartDateTime)
                    return false;
            }

            // 앞뒤 조건 모두 통과 → 일정 추가 가능
            return true;
        }

        // ─────────────────────────────────────────────
        // 대체 시간 추천 함수
        // 일정 추가가 불가능할 때 가장 빠른 시작 가능 시각 반환
        //
        // 계산 기준:
        //   이전 일정 종료 시각 + 이동시간 = 가장 빠른 시작 가능 시각
        // 반환값: 가능한 시작 시각 (DateTime) / 이전 일정 없으면 null
        // ─────────────────────────────────────────────
        public DateTime? SuggestEarliestStart(Schedule newSched, List<Schedule> existing, TransportMode mode)
        {
            var prev = existing
                .Where(s => s.EndDateTime <= newSched.StartDateTime)
                .OrderBy(s => s.EndDateTime)
                .LastOrDefault();

            if (prev != null && prev.Location != null)
            {
                // 이전 일정 종료 + 이동시간 = 가장 빠른 출발 가능 시각
                int travel = GetTravelTime(prev.Location, newSched.Location, mode);
                return prev.EndDateTime.AddMinutes(travel);
            }

            return null;
        }
    }
}