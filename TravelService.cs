using Newtonsoft.Json.Linq;
using SmartCalendar.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Xml.Linq;

namespace Team3
{
    public class TravelService
    {
        private readonly string _kakaoApiKey = "5503db437c0473d403f26d8d3c463797";
        private readonly string _odsayApiKey = "89evtuid8IhBLTusRYsDWtVF1M/gDeUOmr2xounG+Ss";

        private readonly Dictionary<string, Dictionary<string, int>> carTime =
            new Dictionary<string, Dictionary<string, int>>()
        {
            { "원주", new Dictionary<string, int> { {"강남",100}, {"판교",90},  {"수원",110}, {"여주",30},  {"이천",50}  } },
            { "강남", new Dictionary<string, int> { {"원주",100}, {"판교",25},  {"수원",50},  {"여주",80},  {"이천",70}  } },
            { "판교", new Dictionary<string, int> { {"원주",90},  {"강남",25},  {"수원",30},  {"여주",60},  {"이천",50}  } },
            { "수원", new Dictionary<string, int> { {"원주",110}, {"강남",50},  {"판교",30},  {"여주",70},  {"이천",60}  } },
            { "여주", new Dictionary<string, int> { {"원주",30},  {"강남",80},  {"판교",60},  {"수원",70},  {"이천",20}  } },
            { "이천", new Dictionary<string, int> {    {"원주",50},  {"강남",70},  {"판교",50},  {"수원",60},  {"여주",20}  } },
        };

        private readonly Dictionary<string, Dictionary<string, int>> publicTime =
            new Dictionary<string, Dictionary<string, int>>()
        {
            { "원주", new Dictionary<string, int> { {"강남",90},  {"판교",100}, {"수원",120}, {"여주",40},  {"이천",70}  } },
            { "강남", new Dictionary<string, int> { {"원주",90},  {"판교",30},  {"수원",60},  {"여주",90},  {"이천",90}  } },
            { "판교", new Dictionary<string, int> { {"원주",100}, {"강남",30},  {"수원",50},  {"여주",80},  {"이천",80}  } },
            { "수원", new Dictionary<string, int> { {"원주",120}, {"강남",60},  {"판교",50},  {"여주",100}, {"이천",90}  } },
            { "여주", new Dictionary<string, int> { {"원주",40},  {"강남",90},  {"판교",80},  {"수원",100}, {"이천",20}  } },
            { "이천", new Dictionary<string, int> { {"원주",70},  {"강남",90},  {"판교",80},  {"수원",90},  {"여주",20}  } },
        };

        // 장소명 → 좌표 변환 (카카오 로컬 API)
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
                    var documents = json["documents"] as Newtonsoft.Json.Linq.JArray;
                    if (documents == null || documents.Count == 0) return null;
                    double lng = (double)documents[0]["x"];
                    double lat = double.Parse(documents[0]["y"].ToString());
                    return (lat, lng);
                }
            }
            catch { return null; }
        }

        // 자동차 이동시간 (카카오 모빌리티 API → 실패시 테이블 fallback)
        private int GetCarTravelTimeFromApi(string from, string to)
        {
            try
            {
                var origin = GetCoordinates(from);
                var dest = GetCoordinates(to);
                if (origin == null || dest == null)
                    return GetTableTime(from, to, false);

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
            catch { return GetTableTime(from, to, false); }
        }

        // 대중교통 이동시간 (ODsay API → 실패시 테이블 fallback)
        private int GetPublicTravelTimeFromApi(string from, string to)
        {
            try
            {
                var origin = GetCoordinates(from);
                var dest = GetCoordinates(to);
                if (origin == null || dest == null)
                    return GetTableTime(from, to, true);

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
                    if (json["result"] != null && json["result"]["path"] != null && json["result"]["path"].HasValues)
                    {
                        int totalTime = (int)json["result"]["path"][0]["info"]["totalTime"];
                        if (totalTime > 0) return totalTime;
                    }
                    return GetTableTime(from, to, true);
                }
            }
            catch { return GetTableTime(from, to, true); }
        }

        // 테이블 기반 이동시간 조회
        // isPublic: true → 대중교통 테이블, false → 자차 테이블
        private int GetTableTime(string from, string to, bool isPublic)
        {
            if (from == to) return 0;

            string cleanFrom = from;
            string cleanTo = to;
            string[] registeredKeys = { "원주", "강남", "판교", "수원", "여주", "이천" };
            foreach (var key in registeredKeys)
            {
                if (from.Contains(key)) cleanFrom = key;
                if (to.Contains(key)) cleanTo = key;
            }

            var table = isPublic ? publicTime : carTime;

            if (table.ContainsKey(cleanFrom) && table[cleanFrom].ContainsKey(cleanTo))
                return table[cleanFrom][cleanTo];

            return isPublic ? 120 : 60;
        }

        // 이동시간 조회 (외부 호출용)
        // isPublic: true → 대중교통(ODsay), false → 자차(카카오)
        public int GetTravelTime(string from, string to, bool isPublic)
        {
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
                return isPublic ? 120 : 60;
            if (from == to) return 0;
            if (!isPublic)
                return GetCarTravelTimeFromApi(from, to);
            return GetPublicTravelTimeFromApi(from, to);
        }

        // 시간 겹침 검사
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

        // 일정 추가 가능 여부 판단
        // isPublic: true → 대중교통, false → 자차
        public bool CanInsert(Schedule newSched, List<Schedule> existing, bool isPublic)
        {
            if (IsOverlap(newSched, existing)) return false;

            var sorted = existing.OrderBy(s => s.StartDateTime).ToList();
            var prev = sorted.LastOrDefault(s => s.EndDateTime <= newSched.StartDateTime);
            var next = sorted.FirstOrDefault(s => s.StartDateTime >= newSched.EndDateTime);

            if (prev != null && !string.IsNullOrWhiteSpace(prev.Location))
            {
                if (!string.IsNullOrEmpty(newSched.Location))
                {
                    int travel = GetTravelTime(prev.Location, newSched.Location, isPublic);
                    if (prev.EndDateTime.AddMinutes(travel) > newSched.StartDateTime)
                        return false;
                }
            }

            if (next != null && !string.IsNullOrWhiteSpace(next.Location))
            {
                if (!string.IsNullOrEmpty(newSched.Location))
                {
                    int travel = GetTravelTime(newSched.Location, next.Location, isPublic);
                    if (newSched.EndDateTime.AddMinutes(travel) > next.StartDateTime)
                        return false;
                }
            }

            return true;
        }

        // 대체 시간 추천
        public DateTime? SuggestEarliestStart(Schedule newSched, List<Schedule> existing, bool isPublic)
        {
            var prev = existing
                .Where(s => s.EndDateTime <= newSched.StartDateTime)
                .OrderBy(s => s.EndDateTime)
                .LastOrDefault();

            if (prev != null && !string.IsNullOrWhiteSpace(prev.Location))
            {
                int travel = GetTravelTime(prev.Location, newSched.Location, isPublic);
                return prev.EndDateTime.AddMinutes(travel);
            }
            return null;
        }
    }
}