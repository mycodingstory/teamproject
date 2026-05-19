using SmartCalendar.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;

namespace SmartCalendar.Services
{
    public enum TransportMode { Car, Train, Bus }

    public class TravelService
    {
        // 카카오 REST API 키
        private readonly string _kakaoApiKey = "5503db437c0473d403f26d8d3c463797";

        // ODsay API 키
        private readonly string _odsayApiKey = "89evtuid8IhBLTusRYsDWtVF1M/gDeUOmr2xounG+Ss";

        // ─────────────────────────────────────────────
        // 자동차 기준 이동시간 테이블 (API 실패시 fallback)
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

        // ─────────────────────────────────────────────
        // 기차 기준 이동시간 테이블 (대중교통 API 실패시 fallback)
        // ─────────────────────────────────────────────
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

        // ─────────────────────────────────────────────
        // 버스 기준 이동시간 테이블 (대중교통 API 실패시 fallback)
        // ─────────────────────────────────────────────
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
                    var serializer = new JavaScriptSerializer();
                    var json = serializer.Deserialize<Dictionary<string, object>>(response);
                    var documents = json["documents"] as System.Collections.ArrayList;
                    if (documents == null || documents.Count == 0) return null;
                    var first = documents[0] as Dictionary<string, object>;
                    double lng = double.Parse(first["x"].ToString());
                    double lat = double.Parse(first["y"].ToString());
                    return (lat, lng);
                }
            }
            catch { return null; }
        }

        // ─────────────────────────────────────────────
        // 자동차 이동시간 (카카오 모빌리티 API → 실패시 테이블 fallback)
        // ─────────────────────────────────────────────
        private int GetCarTravelTimeFromApi(string from, string to)
        {
            try
            {
                var origin = GetCoordinates(from);
                var dest = GetCoordinates(to);
                if (origin == null || dest == null)
                    return GetTableTime(from, to, TransportMode.Car);

                string url = "https://apis-navi.kakaomobility.com/v1/directions" +
                             "?origin=" + origin.Value.lng + "," + origin.Value.lat +
                             "&destination=" + dest.Value.lng + "," + dest.Value.lat;

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", "KakaoAK " + _kakaoApiKey);
                    var response = client.GetStringAsync(url).Result;
                    var serializer = new JavaScriptSerializer();
                    var json = serializer.Deserialize<Dictionary<string, object>>(response);
                    var routes = json["routes"] as System.Collections.ArrayList;
                    var route = routes[0] as Dictionary<string, object>;
                    var summary = route["summary"] as Dictionary<string, object>;
                    int seconds = int.Parse(summary["duration"].ToString());
                    return seconds / 60;
                }
            }
            catch { return GetTableTime(from, to, TransportMode.Car); }
        }

        // ─────────────────────────────────────────────
        // 대중교통 이동시간 (ODsay API → 실패시 테이블 fallback)
        // ─────────────────────────────────────────────
        private int GetPublicTravelTimeFromApi(string from, string to, TransportMode mode)
        {
            try
            {
                var origin = GetCoordinates(from);
                var dest = GetCoordinates(to);
                if (origin == null || dest == null)
                    return GetTableTime(from, to, mode);

                string url = "https://api.odsay.com/v1/api/searchPubTransPathT" +
                             "?SX=" + origin.Value.lng +
                             "&SY=" + origin.Value.lat +
                             "&EX=" + dest.Value.lng +
                             "&EY=" + dest.Value.lat +
                             "&apiKey=" + Uri.EscapeDataString(_odsayApiKey);

                using (var client = new HttpClient())
                {
                    var response = client.GetStringAsync(url).Result;
                    var serializer = new JavaScriptSerializer();
                    var json = serializer.Deserialize<Dictionary<string, object>>(response);
                    var result = json["result"] as Dictionary<string, object>;
                    var path = result["path"] as System.Collections.ArrayList;
                    var first = path[0] as Dictionary<string, object>;
                    var info = first["info"] as Dictionary<string, object>;
                    int totalTime = int.Parse(info["totalTime"].ToString());
                    return totalTime;
                }
            }
            catch { return GetTableTime(from, to, mode); }
        }

        // ─────────────────────────────────────────────
        // 테이블 기반 이동시간 조회 (API 실패시 fallback)
        // ─────────────────────────────────────────────
        private int GetTableTime(string from, string to, TransportMode mode)
        {
            if (from == to) return 0;
            Dictionary<string, Dictionary<string, int>> table;
            if (mode == TransportMode.Car) table = carTime;
            else if (mode == TransportMode.Train) table = trainTime;
            else table = busTime;
            if (table.ContainsKey(from) && table[from].ContainsKey(to))
                return table[from][to];
            return 30;
        }

        // ─────────────────────────────────────────────
        // 이동시간 조회 (외부 호출용)
        // 자차: 카카오 API → 실패시 테이블
        // 기차/버스: ODsay API → 실패시 테이블
        // ─────────────────────────────────────────────
        public int GetTravelTime(string from, string to, TransportMode mode)
        {
            if (from == to) return 0;
            if (mode == TransportMode.Car)
                return GetCarTravelTimeFromApi(from, to);
            else if (mode == TransportMode.Train || mode == TransportMode.Bus)
                return GetPublicTravelTimeFromApi(from, to, mode);
            return GetTableTime(from, to, mode);
        }

        // ─────────────────────────────────────────────
        // 시간 겹침 검사
        // new.StartDateTime < old.EndDateTime && new.EndDateTime > old.StartDateTime
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
        // 일정 추가 가능 여부 판단
        // 1단계: 시간 겹침 검사
        // 2단계: 이전/다음 일정 이동시간 검사
        // 장소 없는 일정은 이동 중 수행 가능 → 이동시간 검사 생략
        // ─────────────────────────────────────────────
        public bool CanInsert(Schedule newSched, List<Schedule> existing, TransportMode mode)
        {
            if (IsOverlap(newSched, existing)) return false;

            var sorted = existing.OrderBy(s => s.StartDateTime).ToList();
            var prev = sorted.LastOrDefault(s => s.EndDateTime <= newSched.StartDateTime);
            var next = sorted.FirstOrDefault(s => s.StartDateTime >= newSched.EndDateTime);

            // 이전 일정 기준 검사
            if (prev != null && prev.Location != null)
            {
                if (!string.IsNullOrEmpty(newSched.Location))
                {
                    int travel = GetTravelTime(prev.Location, newSched.Location, mode);
                    if (prev.EndDateTime.AddMinutes(travel) > newSched.StartDateTime)
                        return false;
                }
            }

            // 다음 일정 기준 검사
            if (next != null && next.Location != null)
            {
                if (!string.IsNullOrEmpty(newSched.Location))
                {
                    int travel = GetTravelTime(newSched.Location, next.Location, mode);
                    if (newSched.EndDateTime.AddMinutes(travel) > next.StartDateTime)
                        return false;
                }
            }

            return true;
        }

        // ─────────────────────────────────────────────
        // 대체 시간 추천
        // 이전 일정 종료 + 이동시간 = 가장 빠른 시작 가능 시각
        // ─────────────────────────────────────────────
        public DateTime? SuggestEarliestStart(Schedule newSched, List<Schedule> existing, TransportMode mode)
        {
            var prev = existing
                .Where(s => s.EndDateTime <= newSched.StartDateTime)
                .OrderBy(s => s.EndDateTime)
                .LastOrDefault();

            if (prev != null && prev.Location != null)
            {
                int travel = GetTravelTime(prev.Location, newSched.Location, mode);
                return prev.EndDateTime.AddMinutes(travel);
            }
            return null;
        }
    }
}