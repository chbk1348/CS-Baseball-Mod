using System;
using System.Collections.Generic;
using BaseballLeague.Data;

namespace BaseballLeague.UI
{
    /// <summary>
    /// 진행 중 경기 보여 주기(본 스레드 · 화면 전용): 경기 시작 시각부터 게임 시간 GameMinutes 동안 시뮬 결과를 공 하나씩 앞에서부터 보여 줌.
    /// 결과를 정하는 시점은 그대로(League.Tick — 날짜가 바뀔 때 · 홈구장 이벤트가 끝날 때)이고, 화면은 같은 시드로 미리 돌린 결과를 진행 비율만큼 보여 줌.
    /// 시뮬은 경기마다 한 번만 돌려 담아 둠(날짜 · 시즌이 바뀌거나 경기가 정해지면 다시).
    /// shortcut: 홈구장 이벤트가 경기 중에 끝나 다른 시드 차례로 정해지면 보이던 점수가 바뀔 수 있음, 이벤트 시각을 경기 시각에 맞추게 되면 사라짐.
    /// </summary>
    internal static class Live
    {
        /// <summary>경기 길이(게임 시간 분).</summary>
        public const int GameMinutes = 180, DayMinutes = 24 * 60;

        public enum State
        {
            Before,
            Live,
            Over,
            Rain,
            Done,
        }

        /// <summary>진행 표시 한 장면: 끝난 타석 수 · 볼카운트 · 아웃 · 주자 · 이닝 · 말 · 점수 · 지금 타자 · 투수 · 투구 수 · 이닝 점수(지금까지).</summary>
        public sealed class Frame
        {
            public int Done, Balls, Strikes, Outs, Bases, Inning, Away, Home, Batter, Pitcher, Pitches;
            public bool Bottom, Over;
            public readonly List<int>[] Inn = { new(), new() };
        }

        static Season season;
        static DateTime day;
        static readonly Dictionary<int, Result> results = new();
        static readonly Dictionary<int, bool> played = new();
        /// <summary>결과마다 마지막 장면(같은 공 · 같은 끝 여부면 다시 셈하지 않음).</summary>
        static readonly Dictionary<Result, KeyValuePair<int, Frame>> frames = new();

        /// <summary>그 경기 시뮬(담아 둔 것 · 끝난 경기는 저장한 시드 차례로).</summary>
        public static Result Of(Season s, Game g, int index, DateTime today)
        {
            if (s != season || today != day)
            {
                results.Clear();
                played.Clear();
                frames.Clear();
                season = s;
                day = today;
            }
            if (results.TryGetValue(index, out var r) && played[index] == g.Played) return r;
            r = Sim.Replay(s, g, index);
            results[index] = r;
            played[index] = g.Played;
            return r;
        }

        /// <summary>지금 경기 상태와 진행 비율(0 ~ 1, Live 일 때만).</summary>
        public static State StateOf(Season s, Game g, int index, DateTime now, out float t) => StateOf(g, g.Day == now.Date && League.Rains(s, g, index), now, out t);

        /// <summary>StateOf 와 같음(rains = 그날 비로 취소인지 — 화면은 View 에 담아 둔 값을 넘겨 매번 셈하지 않음).</summary>
        public static State StateOf(Game g, bool rains, DateTime now, out float t)
        {
            t = 0f;
            var today = now.Date;
            if (g.Day > today) return State.Before;
            if (g.Day < today) return g.Played ? State.Done : State.Over;
            if (rains) return State.Rain;
            float m = (float)now.TimeOfDay.TotalMinutes - g.Minute;
            if (m < 0f) return State.Before;
            if (m >= GameMinutes) return g.Played ? State.Done : State.Over;
            t = m / GameMinutes;
            return State.Live;
        }

        /// <summary>진행 비율 t 의 장면(같은 결과 · 같은 공이면 담아 둔 것).</summary>
        public static Frame At(Result r, float t)
        {
            var pitches = r.Pitches;
            int n = pitches.Count;
            int k = t >= 1f ? n - 1 : Math.Max(0, Math.Min(n - 1, (int)(t * n)));
            bool over = t >= 1f;
            if (frames.TryGetValue(r, out var hit) && hit.Key == k && hit.Value.Over == over) return hit.Value;
            var p = pitches[k];
            var plays = r.Plays;
            var f = new Frame { Over = over };
            f.Done = over ? plays.Count : p.Done ? p.Play + 1 : p.Play;
            f.Balls = over || p.Done ? 0 : p.Balls;
            f.Strikes = over || p.Done ? 0 : p.Strikes;
            // 지금 타석(끝났으면 다음 타석 · 교체 줄은 건너뜀)
            int cur = over ? plays.Count - 1 : p.Done ? -1 : p.Play;
            if (cur < 0)
                for (int i = f.Done; i < plays.Count && cur < 0; i++)
                    if (plays[i].Kind != PlayKind.Change) cur = i;
            if (cur < 0) cur = plays.Count - 1;
            var c = plays[cur];
            f.Inning = c.Inning;
            f.Bottom = c.Bottom;
            f.Batter = c.Batter;
            f.Pitcher = c.Pitcher;
            if (f.Done > 0)
            {
                var last = plays[f.Done - 1];
                f.Away = last.Away;
                f.Home = last.Home;
                bool sameHalf = last.Inning == c.Inning && last.Bottom == c.Bottom;
                f.Outs = over ? last.Outs : sameHalf && last.Outs < 3 ? last.Outs : 0;
                f.Bases = over || sameHalf && last.Outs < 3 ? last.Bases : 0;
            }
            f.Pitches = Thrown(plays, over ? plays.Count : f.Done, c.Bottom, c.Pitcher);
            if (!over && !p.Done)
                for (int i = k; i >= 0 && pitches[i].Play == p.Play; i--) f.Pitches++; // 지금 타석에 던진 공
            for (int i = 0; i < f.Done; i++)
            {
                var x = plays[i];
                var inn = f.Inn[x.Bottom ? 1 : 0];
                while (inn.Count < x.Inning) inn.Add(0);
                inn[x.Inning - 1] += x.Runs;
            }
            // 지금 말이 시작됐으면 그 칸(0)도 보임
            var now = f.Inn[f.Bottom ? 1 : 0];
            while (now.Count < f.Inning) now.Add(0);
            if (f.Bottom)
                while (f.Inn[0].Count < f.Inning) f.Inn[0].Add(0);
            frames[r] = new KeyValuePair<int, Frame>(k, f);
            return f;
        }

        /// <summary>
        /// 개발용 자체 검사: 진행 비율이 늘면 끝난 타석 · 점수가 줄지 않음 · 볼 3 · 스트라이크 2 · 아웃 2 넘지 않음(끝 빼고) · 끝 장면 점수 = 결과 ·
        /// 이닝 점수 합 = 점수 · 경기 상태(시작 전 · 진행 · 끝 · 다음 날) 경계.
        /// </summary>
        public static void SelfCheck(Action<bool, string> check)
        {
            var start = new DateTime(2026, 3, 24);
            var s = new Season { Rule = Rule.KBO, Start = start, Seed = 9, Games = Schedule.Make(Rule.KBO, start, 9) };
            bool mono = true, counts = true, end = true, inn = true;
            for (int i = 0; i < 30; i++)
            {
                var g = s.Games[i];
                var r = Of(s, g, i, g.Day);
                int done = 0, score = 0;
                for (int step = 0; step <= 200; step++)
                {
                    var f = At(r, step / 200f);
                    mono &= f.Done >= done && f.Away + f.Home >= score;
                    done = f.Done;
                    score = f.Away + f.Home;
                    if (!f.Over) counts &= f.Balls <= 3 && f.Strikes <= 2 && f.Outs <= 2 && f.Inning >= 1;
                    int a = 0, h = 0;
                    foreach (int x in f.Inn[0]) a += x;
                    foreach (int x in f.Inn[1]) h += x;
                    inn &= a == f.Away && h == f.Home;
                }
                var last = At(r, 1f);
                end &= last.Over && last.Away == r.Away && last.Home == r.Home && last.Done == r.Plays.Count;
            }
            check(mono, "진행 표시: 시간이 가는데 끝난 타석 · 점수가 줄어듦");
            check(counts, "진행 표시: 볼카운트 · 아웃이 범위를 넘음");
            check(end, "진행 표시: 끝 장면 점수가 결과와 다름");
            check(inn, "진행 표시: 이닝 점수 합이 점수와 다름");
            var g0 = s.Games[0];
            var day = g0.Day.Date;
            check(StateOf(s, g0, 0, day.AddMinutes(g0.Minute - 1), out _) == State.Before || League.Rains(s, g0, 0), "진행 표시: 시작 전이 아님");
            var mid = StateOf(s, g0, 0, day.AddMinutes(g0.Minute + GameMinutes / 2), out float t);
            check(League.Rains(s, g0, 0) || mid == State.Live && Math.Abs(t - 0.5f) < 0.01f, "진행 표시: 경기 중간이 진행 50% 아님");
            check(League.Rains(s, g0, 0) || StateOf(s, g0, 0, day.AddMinutes(g0.Minute + GameMinutes), out _) == State.Over, "진행 표시: 끝 시각에 안 끝남");
            check(StateOf(s, g0, 0, day.AddDays(-1), out _) == State.Before, "진행 표시: 전날인데 시작 전이 아님");
        }

        /// <summary>끝난 타석 [0, done) 까지 그 말(bottom) 투수 slot 의 투구 수(바로 앞 같은 쪽 줄이 교체면 0).</summary>
        static int Thrown(List<Play> plays, int done, bool bottom, int slot)
        {
            for (int i = done - 1; i >= 0; i--)
            {
                var x = plays[i];
                if (x.Bottom != bottom) continue;
                return x.Kind == PlayKind.Change || x.Pitcher != slot ? 0 : x.Pitches;
            }
            return 0;
        }

        /// <summary>끝난 타석 [0, done) 에서 slot 타자의 타수 · 안타 · 홈런 · 타점.</summary>
        public static void BatterToday(Result r, int done, bool bottom, int slot, out int ab, out int h)
        {
            ab = h = 0;
            for (int i = 0; i < done; i++)
            {
                var x = r.Plays[i];
                if (x.Bottom != bottom || x.Batter != slot || x.Kind == PlayKind.Change) continue;
                if (x.Kind != PlayKind.Walk && x.Kind != PlayKind.SacFly) ab++;
                if (x.Kind <= PlayKind.HomeRun) h++;
            }
        }

        /// <summary>끝난 타석에서 slot 투수(수비 쪽 = !bottom 이 홈)의 아웃 · 실점.</summary>
        public static void PitcherToday(Result r, int done, bool battingBottom, int slot, out int outs, out int runs)
        {
            outs = runs = 0;
            int prevOuts = 0;
            for (int i = 0; i < done; i++)
            {
                var x = r.Plays[i];
                if (i == 0 || x.Inning != r.Plays[i - 1].Inning || x.Bottom != r.Plays[i - 1].Bottom) prevOuts = 0;
                if (x.Kind == PlayKind.Change) continue;
                if (x.Bottom == battingBottom && x.Pitcher == slot)
                {
                    outs += x.Outs - prevOuts;
                    runs += x.Runs;
                }
                prevOuts = x.Outs;
            }
        }
    }
}
