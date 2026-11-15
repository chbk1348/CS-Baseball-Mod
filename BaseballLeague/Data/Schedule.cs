using System;
using System.Collections.Generic;

namespace BaseballLeague.Data
{
    /// <summary>
    /// 경기 하나. HomeRuns &lt; 0 이면 아직 안 함. Moved = 우천 취소로 다시 잡힘(예비일 · 더블헤더 2차전), Double = 더블헤더 경기(1 · 2차전 둘 다).
    /// Series = 0 정규 시즌, n 이면 포스트시즌 시리즈 Season.Post[n - 1].
    /// </summary>
    public sealed class Game
    {
        public DateTime Day;
        public short Minute;
        public byte Home, Away, Series;
        public sbyte HomeRuns = -1, AwayRuns = -1;
        public bool Inter, Moved, Double;

        /// <summary>박스 스코어 요약(시뮬로 정한 경기만 · 판 4 이전 세이브 · 밖에서 넣은 결과는 null).</summary>
        public Box Box;

        public bool Played => HomeRuns >= 0;
    }

    /// <summary>
    /// 일정 생성기(결정적: 같은 규칙 · 시작일 · 시드면 늘 같은 일정). 월요일 휴식, 화 ~ 목 / 금 ~ 일 3연전 칸.
    /// KBO: 10팀 원형 대진 9라운드 × 6바퀴(3연전 4바퀴 + 2연전 2바퀴) = 상대마다 16(홈 8 · 원정 8) · 팀당 144, 7월 13 ~ 16일 올스타 휴식.
    /// NPB: 리그마다 6팀 원형 대진 5라운드 × 9바퀴(3연전 7 + 2연전 2) = 같은 리그 상대마다 25 · 125, 교류전 3연전 6칸(상대마다 3) = 18 → 143.
    /// </summary>
    public static class Schedule
    {
        /// <summary>올스타 휴식(KBO): 이 날짜에 걸리는 칸은 비움.</summary>
        const int AllStarMonth = 7, AllStarFirst = 13, AllStarLast = 16;
        /// <summary>교류전(NPB): 이 날 뒤 첫 칸부터 6칸(5 ~ 6월).</summary>
        const int InterMonth = 5, InterDay = 26;

        sealed class Slot
        {
            public readonly List<byte> Pairs = new(); // 홈, 원정 차례로 되풀이
            public int Games;
            public bool Inter;
        }

        public static List<Game> Make(Rule rule, DateTime start, int seed)
        {
            var rng = new Random(seed);
            var slots = new List<Slot>();
            int[] gamesPerCycle = rule == Rule.KBO ? new[] { 3, 3, 3, 3, 2, 2 } : new[] { 3, 3, 3, 3, 3, 3, 3, 2, 2 };
            var leagues = new List<int[]>();
            for (int lg = 0; lg < Rules.LeagueCount(rule); lg++)
            {
                var ids = new List<int>();
                for (int t = 0; t < Rules.TeamCount(rule); t++)
                    if (Rules.LeagueOf(rule, t) == lg) ids.Add(t);
                leagues.Add(Shuffle(ids.ToArray(), rng));
            }
            int n = leagues[0].Length, rounds = n - 1;
            for (int c = 0; c < gamesPerCycle.Length; c++)
            {
                int[] order = new int[rounds];
                for (int i = 0; i < rounds; i++) order[i] = i;
                Shuffle(order, rng);
                bool firstHome = HomeFirst(rule, c);
                foreach (int round in order)
                {
                    var s = new Slot { Games = gamesPerCycle[c] };
                    foreach (var teams in leagues)
                        for (int k = 0; k < n / 2; k++)
                        {
                            Circle(teams, round, k, out int a, out int b);
                            s.Pairs.Add((byte)(firstHome ? a : b));
                            s.Pairs.Add((byte)(firstHome ? b : a));
                        }
                    slots.Add(s);
                }
            }

            if (rule == Rule.NPB)
            {
                var inter = new List<Slot>();
                int[] central = leagues[0], pacific = leagues[1];
                for (int r = 0; r < n; r++)
                {
                    var s = new Slot { Games = 3, Inter = true };
                    for (int i = 0; i < n; i++)
                    {
                        int a = central[i], b = pacific[(i + r) % n];
                        s.Pairs.Add((byte)(r % 2 == 0 ? a : b));
                        s.Pairs.Add((byte)(r % 2 == 0 ? b : a));
                    }
                    inter.Add(s);
                }
                // 칸 하나 ≈ 3.5일 · 교류전 날짜가 시즌 밖이면 시즌 가운데
                var interDate = new DateTime(start.Year, InterMonth, InterDay);
                if (interDate < start) interDate = interDate.AddYears(1);
                int at = (int)Math.Ceiling((interDate - start).TotalDays / 3.5);
                if (at > slots.Count) at = slots.Count / 2;
                slots.InsertRange(at, inter);
            }

            var games = new List<Game>();
            DateTime day = start.Date;
            foreach (var s in slots)
            {
                day = NextSlotStart(day);
                while (rule == Rule.KBO && TouchesAllStar(day)) day = NextSlotStart(day.AddDays(1));
                for (int g = 0; g < s.Games; g++)
                {
                    var d = day.AddDays(g);
                    for (int p = 0; p < s.Pairs.Count; p += 2)
                        games.Add(new Game { Day = d, Minute = Rules.StartMinute(rule, d.DayOfWeek), Home = s.Pairs[p], Away = s.Pairs[p + 1], Inter = s.Inter });
                }
                day = day.AddDays(3);
            }
            return games;
        }

        /// <summary>KBO 3연전은 바퀴마다 홈을 바꿈(4바퀴 6 · 6, 2연전 2바퀴 2 · 2 → 8 · 8). NPB 는 3연전 7바퀴 12 · 9 + 2연전 2바퀴 원정 쪽 → 12 · 13.</summary>
        static bool HomeFirst(Rule rule, int cycle) => rule == Rule.KBO ? cycle % 2 == 0 : cycle < 7 && cycle % 2 == 0;

        /// <summary>원형 대진(circle method): 0번 고정, 나머지를 라운드마다 한 칸씩 돌림. k 번째 짝.</summary>
        static void Circle(int[] teams, int round, int k, out int a, out int b)
        {
            int m = teams.Length - 1;
            int Rot(int i) => teams[1 + (i + round) % m];
            a = k == 0 ? teams[0] : Rot(k);
            b = k == 0 ? Rot(0) : Rot(m - k);
        }

        static T[] Shuffle<T>(T[] a, Random rng)
        {
            for (int i = a.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T t = a[i];
                a[i] = a[j];
                a[j] = t;
            }
            return a;
        }

        /// <summary>day 와 같거나 뒤의 첫 화요일 · 금요일.</summary>
        public static DateTime NextSlotStart(DateTime day)
        {
            while (day.DayOfWeek != DayOfWeek.Tuesday && day.DayOfWeek != DayOfWeek.Friday) day = day.AddDays(1);
            return day;
        }

        static bool TouchesAllStar(DateTime slotStart)
        {
            for (int g = 0; g < 3; g++)
            {
                var d = slotStart.AddDays(g);
                if (d.Month == AllStarMonth && d.Day >= AllStarFirst && d.Day <= AllStarLast) return true;
            }
            return false;
        }

        public static bool InAllStar(DateTime d) => d.Month == AllStarMonth && d.Day >= AllStarFirst && d.Day <= AllStarLast;

        /// <summary>
        /// 개발용 자체 검사(assert): KBO 팀마다 144 · 상대마다 16(홈 8) · 올스타 휴식, NPB 팀마다 143 · 같은 리그 125(상대마다 25) · 교류전 18(상대마다 3),
        /// 둘 다 월요일 없음 · 같은 날 한 팀 두 경기 없음 · 시작 시각 · 같은 시드면 같은 일정. 실패마다 check(false, 무엇).
        /// </summary>
        public static void SelfCheck(Action<bool, string> check)
        {
            DateTime[] starts = { new(2026, 3, 24), new(2026, 11, 17), new(2027, 6, 1) };
            foreach (Rule rule in new[] { Rule.KBO, Rule.NPB })
                foreach (var start in starts)
                    for (int seed = 1; seed <= 3; seed++)
                        CheckOne(rule, start, seed, check);
        }

        static void CheckOne(Rule rule, DateTime start, int seed, Action<bool, string> check)
        {
            string tag = $"{rule} {start:yyyy-MM-dd} 시드 {seed}: ";
            var games = Make(rule, start, seed);
            int n = Rules.TeamCount(rule);
            var total = new int[n];
            var inter = new int[n];
            var pair = new int[n, n];
            var home = new int[n, n];
            var busy = new HashSet<long>();
            bool monday = false, twice = false, time = false, allStar = false, early = false;
            foreach (var g in games)
            {
                total[g.Home]++;
                total[g.Away]++;
                if (g.Inter)
                {
                    inter[g.Home]++;
                    inter[g.Away]++;
                }
                pair[g.Home, g.Away]++;
                pair[g.Away, g.Home]++;
                home[g.Home, g.Away]++;
                monday |= g.Day.DayOfWeek == DayOfWeek.Monday;
                long dn = g.Day.Ticks / TimeSpan.TicksPerDay * n;
                twice |= !busy.Add(dn + g.Home) | !busy.Add(dn + g.Away);
                time |= g.Minute != Rules.StartMinute(rule, g.Day.DayOfWeek);
                allStar |= rule == Rule.KBO && InAllStar(g.Day);
                early |= g.Day < start;
            }
            check(!monday, tag + "월요일 경기 있음");
            check(!twice, tag + "같은 날 한 팀 두 경기");
            check(!time, tag + "시작 시각 어긋남");
            check(!allStar, tag + "올스타 휴식에 경기 있음");
            check(!early, tag + "시작일 앞 경기 있음");
            int season = rule == Rule.KBO ? 144 : 143;
            for (int a = 0; a < n; a++)
            {
                check(total[a] == season, tag + $"{a}번 팀 {total[a]}경기(기대 {season})");
                if (rule == Rule.NPB) check(inter[a] == 18 && total[a] - inter[a] == 125, tag + $"{a}번 팀 교류전 {inter[a]} · 같은 리그 {total[a] - inter[a]}");
                for (int b = 0; b < n; b++)
                {
                    if (a == b) continue;
                    bool same = Rules.LeagueOf(rule, a) == Rules.LeagueOf(rule, b);
                    int want = rule == Rule.KBO ? 16 : same ? 25 : 3;
                    check(pair[a, b] == want, tag + $"{a} 대 {b} {pair[a, b]}경기(기대 {want})");
                    if (rule == Rule.KBO) check(home[a, b] == 8, tag + $"{a} 홈 대 {b} {home[a, b]}경기(기대 8)");
                }
            }
            var again = Make(rule, start, seed);
            bool same2 = again.Count == games.Count;
            for (int i = 0; same2 && i < games.Count; i++)
                same2 = again[i].Day == games[i].Day && again[i].Home == games[i].Home && again[i].Away == games[i].Away;
            check(same2, tag + "같은 시드인데 일정이 다름");
        }
    }
}
