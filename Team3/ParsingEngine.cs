using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ParsingEngine;

namespace Team3
{
    public class ParsingEngineLogic
    {
        // 정규식 객체 컴파일 및 최적화
        private static readonly Regex DateRegex = new Regex(
            @"(?:(?<month>\d{1,2})[월/]\s*)?(?<day>\d{1,2})일", RegexOptions.Compiled);

        private static readonly Regex RelativeDateRegex = new Regex(
            @"(?<relative>오늘|내일|모레)", RegexOptions.Compiled);

        private static readonly Regex TimeRangeRegex = new Regex(
            @"(?<ampm1>오전|오후)?\s*(?<start>\d{1,2}(?::\d{2}|시(?:\s*반|\s*\d{1,2}분)?))\s*[~-]\s*(?<ampm2>오전|오후)?\s*(?<end>\d{1,2}(?::\d{2}|시(?:\s*반|\s*\d{1,2}분)?))", RegexOptions.Compiled);

        private static readonly Regex SingleTimeRegex = new Regex(
            @"(?<ampm>오전|오후)?\s*(?<time>\d{1,2}(?::\d{2}|시(?:\s*반|\s*\d{1,2}분)?))", RegexOptions.Compiled);

        private static readonly Regex BasePlaceRegex = new Regex(
            @"(?<base>\S+?(?:에서))", RegexOptions.Compiled);

        // 장소 확장 추적 시 제외할 조사 목록 (HashSet 최적화)
        private static readonly HashSet<string> JosaFilters = new HashSet<string>
            { "을", "를", "은", "는", "이", "가", "하고", "해서", "하며", "과", "와" };

        public Schedule ParseInput(string input)
        {
            Schedule newSchedule = new Schedule();
            string cleanInput = input;

            string matchedDateText = "";
            string matchedTimeText = "";
            string timeDisplayStr = "";

            DateTime today = DateTime.Today;
            DateTime targetDate = today;

            // ---------------------------------------------------
            // [1] 날짜 파싱 및 지능형 익월 전환 알고리즘
            // ---------------------------------------------------
            Match dateMatch = DateRegex.Match(cleanInput);
            Match relativeMatch = RelativeDateRegex.Match(cleanInput);

            if (dateMatch.Success)
            {
                matchedDateText = dateMatch.Value;
                int day = int.Parse(dateMatch.Groups["day"].Value);

                // "월" 정보가 그룹에 잡혔는지 확인
                if (dateMatch.Groups["month"].Success)
                {
                    int month = int.Parse(dateMatch.Groups["month"].Value);
                    targetDate = new DateTime(today.Year, month, day);
                }
                else
                {
                    // [핵심 보완] 월 없이 "m일"만 입력된 경우 예외 처리
                    if (day < today.Day)
                    {
                        // 입력된 일이 오늘보다 과거라면 자동으로 다음 달로 설정
                        targetDate = today.AddMonths(1);
                        targetDate = new DateTime(targetDate.Year, targetDate.Month, day);
                    }
                    else
                    {
                        // 오늘이거나 오늘보다 미래라면 이번 달로 설정
                        targetDate = new DateTime(today.Year, today.Month, day);
                    }
                }
            }
            else if (relativeMatch.Success)
            {
                matchedDateText = relativeMatch.Value;
                string rel = relativeMatch.Groups["relative"].Value;

                if (rel == "내일") targetDate = today.AddDays(1);
                else if (rel == "모레") targetDate = today.AddDays(2);
            }

            // ---------------------------------------------------
            // [2] 시간 범위 / 단일 시간 및 지능형 24시제 처리
            // ---------------------------------------------------
            Match rangeMatch = TimeRangeRegex.Match(cleanInput);
            Match singleMatch = SingleTimeRegex.Match(cleanInput);

            if (rangeMatch.Success)
            {
                matchedTimeText = rangeMatch.Value;
                string ampm1 = rangeMatch.Groups["ampm1"].Value;
                string startStr = rangeMatch.Groups["start"].Value;
                string ampm2 = rangeMatch.Groups["ampm2"].Value;
                string endStr = rangeMatch.Groups["end"].Value;

                newSchedule.StartDateTime = ConvertToDateTime(targetDate, startStr, ampm1);
                newSchedule.EndDateTime = ConvertToDateTime(targetDate, endStr, string.IsNullOrEmpty(ampm2) ? ampm1 : ampm2);

                if (newSchedule.EndDateTime < newSchedule.StartDateTime && newSchedule.EndDateTime.Hour < 12)
                {
                    newSchedule.EndDateTime = newSchedule.EndDateTime.AddHours(12);
                }

                string displayStart = (string.IsNullOrEmpty(ampm1) ? "" : ampm1 + " ") + startStr;
                string displayEnd = (string.IsNullOrEmpty(ampm2) ? (string.IsNullOrEmpty(ampm1) ? "" : ampm1 + " ") : ampm2 + " ") + endStr;
                timeDisplayStr = $"{displayStart}~{displayEnd}";
            }
            else if (singleMatch.Success)
            {
                matchedTimeText = singleMatch.Value;
                string ampm = singleMatch.Groups["ampm"].Value;
                string timeStr = singleMatch.Groups["time"].Value;

                newSchedule.StartDateTime = ConvertToDateTime(targetDate, timeStr, ampm);
                newSchedule.EndDateTime = newSchedule.StartDateTime.AddHours(1);

                string displayStart = (string.IsNullOrEmpty(ampm) ? "" : ampm + " ") + timeStr;
                timeDisplayStr = $"{displayStart}~{newSchedule.EndDateTime:HH:mm}";
            }
            else
            {
                newSchedule.StartDateTime = new DateTime(targetDate.Year, targetDate.Month, targetDate.Day, DateTime.Now.Hour, DateTime.Now.Minute, 0);
                newSchedule.EndDateTime = newSchedule.StartDateTime.AddHours(1);
                timeDisplayStr = $"{newSchedule.StartDateTime:HH:mm}~{newSchedule.EndDateTime:HH:mm}";
            }

            // ---------------------------------------------------
            // [3] 장소 정밀 추출 및 조사 의존성 우회 추적
            // ---------------------------------------------------
            string finalLocation = "";
            string originalPlaceText = "";

            Match basePlaceMatch = BasePlaceRegex.Match(cleanInput);
            if (basePlaceMatch.Success)
            {
                originalPlaceText = basePlaceMatch.Value;
                string baseWord = basePlaceMatch.Groups["base"].Value.Replace("에서", "").Trim();

                int baseIndex = cleanInput.IndexOf(originalPlaceText);
                string leftContext = cleanInput.Substring(0, baseIndex).Trim();

                if (!string.IsNullOrEmpty(matchedDateText)) leftContext = leftContext.Replace(matchedDateText, "").Trim();
                if (!string.IsNullOrEmpty(matchedTimeText)) leftContext = leftContext.Replace(matchedTimeText, "").Trim();

                string[] leftWords = leftContext.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                List<string> validPlaceParts = new List<string>();

                for (int i = leftWords.Length - 1; i >= 0; i--)
                {
                    string word = leftWords[i];
                    if (JosaFilters.Any(josa => word.EndsWith(josa))) break;
                    validPlaceParts.Insert(0, word);
                }

                if (validPlaceParts.Count > 0)
                {
                    string pathText = string.Join(" ", validPlaceParts);
                    finalLocation = $"{pathText} {baseWord}";
                    originalPlaceText = $"{pathText} {originalPlaceText}";
                }
                else
                {
                    finalLocation = baseWord;
                }
            }

            newSchedule.Location = finalLocation;

            // ---------------------------------------------------
            // [4] 순수 메모 내용 복원 및 정리
            // ---------------------------------------------------
            string memoText = input;
            if (!string.IsNullOrEmpty(matchedDateText)) memoText = memoText.Replace(matchedDateText, "");
            if (!string.IsNullOrEmpty(matchedTimeText)) memoText = memoText.Replace(matchedTimeText, "");
            if (!string.IsNullOrEmpty(originalPlaceText)) memoText = memoText.Replace(originalPlaceText, "");

            newSchedule.Memo = Regex.Replace(memoText, @"\s+", " ").Trim();
            newSchedule.TransportType = timeDisplayStr;

            return newSchedule;
        }

        private DateTime ConvertToDateTime(DateTime baseDate, string timeStr, string ampm)
        {
            int hour = 0, minute = 0;
            try
            {
                string targetStr = timeStr.Replace("반", "30분");

                if (targetStr.Contains(":"))
                {
                    string[] parts = targetStr.Split(':');
                    hour = int.Parse(parts[0]);
                    minute = int.Parse(parts[1]);
                }
                else if (targetStr.Contains("시"))
                {
                    hour = int.Parse(Regex.Match(targetStr, @"\d+").Value);
                    Match minMatch = Regex.Match(targetStr, @"시\s*(\d+)분");
                    if (minMatch.Success)
                    {
                        minute = int.Parse(minMatch.Groups[1].Value);
                    }
                }

                if (string.IsNullOrEmpty(ampm))
                {
                    if (hour >= 1 && hour <= 7) hour += 12;
                }
                else
                {
                    if (ampm == "오후" && hour < 12) hour += 12;
                    if (ampm == "오전" && hour == 12) hour = 0;
                }

                return new DateTime(baseDate.Year, baseDate.Month, baseDate.Day, hour, minute, 0);
            }
            catch
            {
                return baseDate;
            }
        }
    }
}