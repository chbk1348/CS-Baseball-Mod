using System;
using System.Reflection;

namespace BaseballLeague.Data
{
    /// <summary>
    /// 올림픽 모드(CS-Olympic-Mod)와 선택 연결: 우리 도시 대회가 확정됐으면(선수단 입촌 · 개최 중 — 준비 중엔 반납할 수 있어 아직 아님) 입촌일 ~ 폐회일을 읽음.
    /// 올림픽 모드가 게임 경기를 미루기 시작하는 때가 입촌(Runner.ArrivalAt)이라 휴식도 그날부터.
    /// 모드가 없거나 이름이 바뀌었거나 읽다 실패하면 없음(리그는 지금처럼). 올림픽 쪽 Runner 는 internal 이라 리플렉션으로 읽음.
    /// shortcut: 패럴림픽 기간은 쉬지 않음(올림픽 모드가 패럴림픽엔 게임 경기를 미루지 않음), 미루게 되면 Para.OpenDate 도 읽기.
    /// </summary>
    public static class Olympic
    {
        public const string AssemblyName = "CS-Olympic-Mod", RunnerName = "OlympicGames.Logic.Runner";

        const BindingFlags Any = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>읽다 난 마지막 오류(없으면 null) · 본체가 로그에 한 번 남김.</summary>
        public static string Error;

        /// <summary>올림픽 모드의 Runner 형(없으면 null).</summary>
        public static Type Find()
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                if (a.GetName().Name == AssemblyName) return a.GetType(RunnerName, false);
            return null;
        }

        /// <summary>runner(Runner 형 · null = 모드 없음)의 Phase 가 Arrival · Hosting 이면 입촌일(ArrivalAt, 없으면 OpenAt - ArrivalDays) · 폐회일(CloseAt)을 넣고 true. 없거나 실패하면 false(둘 다 default).</summary>
        public static bool Read(Type runner, out DateTime from, out DateTime to)
        {
            from = to = default;
            if (runner == null) return false;
            try
            {
                string phase = runner.GetProperty("Phase", Any)?.GetValue(null, null)?.ToString();
                if (phase != "Arrival" && phase != "Hosting") return false;
                if (!(runner.GetProperty("OpenAt", Any)?.GetValue(null, null) is DateTime open) || !(runner.GetProperty("CloseAt", Any)?.GetValue(null, null) is DateTime close) || close < open)
                {
                    Error = "OpenAt · CloseAt 을 못 읽음";
                    return false;
                }
                var arrival = runner.GetProperty("ArrivalAt", Any)?.GetValue(null, null);
                if (arrival is DateTime at) open = at;
                else if (runner.GetField("ArrivalDays", Any)?.GetValue(null) is int days) open = open.Date.AddDays(-days);
                else
                {
                    Error = "ArrivalAt · ArrivalDays 를 못 읽음";
                    return false;
                }
                from = open.Date;
                to = close.Date;
                return true;
            }
            catch (Exception e)
            {
                Error = (e.InnerException ?? e).GetType().Name + ": " + (e.InnerException ?? e).Message;
                return false;
            }
        }

        // ---- 자체 검사용 가짜 Runner(이름 · 모양만 같게)
        enum Phase { None, Preparing, Arrival, Hosting, Departure, Finished }

        static class FakeRunner
        {
            public static Phase Now;
            public static bool Throw, NoArrival;
            public const int ArrivalDays = 7;
            static object ArrivalAt => NoArrival ? null : new DateTime(2026, 7, 17);
            static Phase Phase => Throw ? throw new InvalidOperationException("시험") : Now;
            public static DateTime OpenAt => new(2026, 7, 24, 20, 0, 0);
            public static DateTime CloseAt => new(2026, 8, 9, 23, 0, 0);
        }

        /// <summary>모드 없음 · 준비 중 · 끝난 뒤는 없음, 입촌 · 개최 중은 입촌일 ~ 폐회일(ArrivalAt 이 없으면 OpenAt - ArrivalDays), 읽기 실패는 없음.</summary>
        public static void SelfCheck(Action<bool, string> check)
        {
            check(!Read(null, out var f, out var t) && f == default && t == default, "올림픽: 모드 없을 때 기간이 생김");
            check(Find() == null, "올림픽: 검사 실행기에서 올림픽 모드를 찾음");
            foreach (var p in new[] { Phase.None, Phase.Preparing, Phase.Departure, Phase.Finished })
            {
                FakeRunner.Now = p;
                check(!Read(typeof(FakeRunner), out f, out t) && t == default, $"올림픽: {p} 인데 휴식");
            }
            foreach (var p in new[] { Phase.Arrival, Phase.Hosting })
            {
                FakeRunner.Now = p;
                FakeRunner.NoArrival = false;
                check(Read(typeof(FakeRunner), out f, out t) && f == new DateTime(2026, 7, 17) && t == new DateTime(2026, 8, 9), $"올림픽: {p} 인데 기간이 입촌일 ~ 폐회일 아님");
                FakeRunner.NoArrival = true;
                check(Read(typeof(FakeRunner), out f, out t) && f == new DateTime(2026, 7, 17) && t == new DateTime(2026, 8, 9), $"올림픽: {p} · ArrivalAt 없을 때 OpenAt - ArrivalDays 아님");
            }
            FakeRunner.NoArrival = false;
            FakeRunner.Throw = true;
            Error = null;
            check(!Read(typeof(FakeRunner), out f, out t) && t == default && Error != null, "올림픽: 읽기 실패인데 휴식 · 오류 안 남김");
            FakeRunner.Throw = false;
            Error = null;
        }
    }
}
