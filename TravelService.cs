using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace Team3
{
    // 이동시간 검사 결과 클래스
    public class TravelCheckResult
    {
        public bool CanAdd { get; set; }
        public DateTime? SuggestedStartTime { get; set; }
        public string Message { get; set; }
    }

    public class TravelService
    {
        private readonly string _kakaoApiKey = "5503db437c0473d403f26d8d3c463797";
        private readonly string _odsayApiKey = "89evtuid8IhBLTusRYsDWtVF1M/gDeUOmr2xounG+Ss";

        // ─────────────────────────────────────────────
        // Schedule.TransportType을 보고 대중교통 여부 판단
        // ─────────────────────────────────────────────
        private bool IsPublicTransport(Schedule schedule)
        {
            string transport = schedule.TransportType ?? "";
            return transport.Contains("대중교통") ||
                   transport.Contains("버스") ||
                   transport.Contains("지하철") ||
                   transport.Contains("Public") ||
                   transport.Contains("public");
        }

        // ─────────────────────────────────────────────
        // 장소명 → 좌표 변환 (카카오 로컬 API)
        // ─────────────────────────────────────────────
        private (double lat, double lng)? GetCoordinates(string placeName)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    string url = "https://dapi.kakao.com/v2/local/search/keyword.json?query=" + Uri.EscapeDataString(placeName);
                    client.DefaultRequestHeaders.Add("Authorization", "KakaoAK " + _kakaoApiKey);
                    var response = client.GetStringAsync(url).Result;
                    var json = JObject.Parse(response);
                    var documents = json["documents"] as JArray;
                    if (documents == null || documents.Count == 0) return null;
                    double lng = (double)documents[0]["x"];
                    double lat = double.Parse(documents[0]["y"].ToString());
                    return (lat, lng);
                }
            }
            catch { return null; }
        }

        // ─────────────────────────────────────────────
        // 자동차 이동시간 (카카오 모빌리티 API)
        // 실패시 -1 반환
        // ─────────────────────────────────────────────
        private int GetCarTravelTimeFromApi(string from, string to)
        {
            try
            {
                var origin = GetCoordinates(from);
                var dest   = GetCoordinates(to);
                if (origin == null || dest == null) return -1;

                string url = "https://apis-navi.kakaomobility.com/v1/directions" +
                             "?origin=" + origin.Value.lng + "," + origin.Value.lat +
                             "&destination=" + dest.Value.lng + "," + dest.Value.lat;

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", "KakaoAK " + _kakaoApiKey);
                    var response = client.GetStringAsync(url).Result;
                    var json = JObject.Parse(response);
                    int seconds = (int)json["routes"][0]["summary"]["duration"];
                    return seconds / 60;
                }
            }
            catch { return -1; }
        }

        // ─────────────────────────────────────────────
        // 대중교통 이동시간 (ODsay API)
        // 실패시 -1 반환
        // ─────────────────────────────────────────────
        private int GetPublicTravelTimeFromApi(string from, string to)
        {
            try
            {
                var origin = GetCoordinates(from);
                var dest   = GetCoordinates(to);
                if (origin == null || dest == null) return -1;

                string url = "https://api.odsay.com/v1/api/searchPubTransPathT" +
                             "?SX=" + origin.Value.lng +
                             "&SY=" + origin.Value.lat +
                             "&EX=" + dest.Value.lng +
                             "&EY=" + dest.Value.lat +
                             "&apiKey=" + Uri.EscapeDataString(_odsayApiKey);

                using (var client = new HttpClient())
                {
                    var response = client.GetStringAsync(url).Result;
                    var json = JObject.Parse(response);
                    if (json["result"] != null &&
                        json["result"]["path"] != null &&
                        json["result"]["path"].HasValues)
                    {
                        int totalTime = (int)json["result"]["path"][0]["info"]["totalTime"];
                        if (totalTime > 0) return totalTime;
                    }
                    return -1;
                }
            }
            catch { return -1; }
        }

        // ─────────────────────────────────────────────
        // 이동시간 조회 (외부 호출용)
        // 실패시 -1 반환
        // ─────────────────────────────────────────────
        public int GetTravelTime(string from, string to, bool isPublic)
        {
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
                return -1;
            if (from == to) return 0;
            if (!isPublic)
                return GetCarTravelTimeFromApi(from, to);
            return GetPublicTravelTimeFromApi(from, to);
        }

        // ─────────────────────────────────────────────
        // 시간 겹침 검사
        // ─────────────────────────────────────────────
        public bool IsOverlap(Schedule newSched, List<Schedule> existing)
        {
            foreach (var old in existing)
            {
                if (newSched.StartDateTime < old.EndDateTime &&
                    newSched.EndDateTime > old.StartDateTime)
                    return true;
            }
            return false;
        }

        // ─────────────────────────────────────────────
        // 이동시간까지 고려한 일정 추가 가능 여부 검사
        // ─────────────────────────────────────────────
        public TravelCheckResult CheckTravelTime(Schedule newSched, List<Schedule> existing)
        {
            bool isPublic = IsPublicTransport(newSched);
            TimeSpan duration = newSched.EndDateTime - newSched.StartDateTime;
            DateTime originalStart = newSched.StartDateTime;

            List<Schedule> sorted = existing.OrderBy(s => s.StartDateTime).ToList();

            Schedule prev = sorted
                .Where(s => s.EndDateTime <= newSched.StartDateTime)
                .OrderByDescending(s => s.EndDateTime)
                .FirstOrDefault();

            Schedule next = sorted
                .Where(s => s.StartDateTime >= newSched.StartDateTime)
                .OrderBy(s => s.StartDateTime)
                .FirstOrDefault();

            DateTime earliestStart = newSched.StartDateTime;
            DateTime? latestStart = null;

            // 1. 이전 일정 → 새 일정 이동시간 검사
            if (prev != null &&
                !string.IsNullOrWhiteSpace(prev.Location) &&
                !string.IsNullOrWhiteSpace(newSched.Location))
            {
                int travelFromPrev = GetTravelTime(prev.Location, newSched.Location, isPublic);
                if (travelFromPrev < 0)
                {
                    return new TravelCheckResult
                    {
                        CanAdd = false,
                        SuggestedStartTime = null,
                        Message = "이전 일정에서 새 일정까지 이동시간 계산에 실패했습니다."
                    };
                }
                DateTime possibleStart = prev.EndDateTime.AddMinutes(travelFromPrev);
                if (possibleStart > earliestStart)
                    earliestStart = possibleStart;
            }

            // 2. 새 일정 → 다음 일정 이동시간 검사
            if (next != null &&
                !string.IsNullOrWhiteSpace(newSched.Location) &&
                !string.IsNullOrWhiteSpace(next.Location))
            {
                int travelToNext = GetTravelTime(newSched.Location, next.Location, isPublic);
                if (travelToNext < 0)
                {
                    return new TravelCheckResult
                    {
                        CanAdd = false,
                        SuggestedStartTime = null,
                        Message = "새 일정에서 다음 일정까지 이동시간 계산에 실패했습니다."
                    };
                }
                DateTime latestEnd = next.StartDateTime.AddMinutes(-travelToNext);
                latestStart = latestEnd.Subtract(duration);
            }

            // 3. 앞/뒤 조건 동시 만족 불가 → 물리적으로 불가능
            if (latestStart.HasValue && earliestStart > latestStart.Value)
            {
                return new TravelCheckResult
                {
                    CanAdd = false,
                    SuggestedStartTime = null,
                    Message = "앞 일정과 뒤 일정 사이 이동시간을 모두 만족할 수 없어 물리적으로 불가능한 일정입니다."
                };
            }

            // 4. 앞 일정 때문에 늦춰야 하는 경우
            if (earliestStart > originalStart)
            {
                return new TravelCheckResult
                {
                    CanAdd = false,
                    SuggestedStartTime = earliestStart,
                    Message = string.Format(
                        "이전 일정 장소에서 이동하려면 {0} 이후로 시작해야 합니다.",
                        earliestStart.ToString("HH:mm"))
                };
            }

            // 5. 뒤 일정 때문에 당겨야 하는 경우
            if (latestStart.HasValue && originalStart > latestStart.Value)
            {
                return new TravelCheckResult
                {
                    CanAdd = false,
                    SuggestedStartTime = latestStart.Value,
                    Message = string.Format(
                        "다음 일정 장소까지 이동하려면 {0} 이전에 시작해야 합니다.",
                        latestStart.Value.ToString("HH:mm"))
                };
            }

            // 6. 이동 가능
            return new TravelCheckResult
            {
                CanAdd = true,
                SuggestedStartTime = null,
                Message = "이동 가능한 일정입니다."
            };
        }

        // ScheduleManager에서 사용하는 단순 true/false 함수
        public bool CanInsert(Schedule newSched, List<Schedule> existing)
        {
            if (IsOverlap(newSched, existing)) return false;
            return CheckTravelTime(newSched, existing).CanAdd;
        }

        // UI에서 메시지까지 받고 싶을 때 사용하는 함수
        public TravelCheckResult GetTravelCheckResult(Schedule newSched, List<Schedule> existing)
        {
            if (IsOverlap(newSched, existing))
            {
                return new TravelCheckResult
                {
                    CanAdd = false,
                    SuggestedStartTime = null,
                    Message = "시간이 겹치는 일정이 있습니다."
                };
            }
            return CheckTravelTime(newSched, existing);
        }

        // 추천 시작 시간만 필요한 경우 사용
        public DateTime? SuggestEarliestStart(Schedule newSched, List<Schedule> existing)
        {
            return CheckTravelTime(newSched, existing).SuggestedStartTime;
        }
    }
}
