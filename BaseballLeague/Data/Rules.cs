using System;

namespace BaseballLeague.Data
{
    /// <summary>리그 규칙: KBO(한 리그 10팀 · 144경기) · NPB(센트럴 6 · 퍼시픽 6 · 143경기).</summary>
    public enum Rule : byte
    {
        KBO,
        NPB,
    }

    /// <summary>팀 이름 방식(가상 이름만 · 실제 구단 이름 · 닉네임 없음).</summary>
    public enum Naming : byte
    {
        Korean,
        Japanese,
    }

    /// <summary>가상 팀. 0번이 내 도시 팀. Stadium 은 아는 곳만(지금은 한국형 내 도시 팀).</summary>
    public sealed class Team
    {
        public readonly string Name, City, Stadium;

        public Team(string name, string city, string stadium = null)
        {
            Name = name;
            City = city;
            Stadium = stadium;
        }
    }

    /// <summary>README '리그 규칙' 표를 코드로: 팀 수 · 리그 나누기 · 시작 시각 · 팀 이름.</summary>
    public static class Rules
    {
        public static int TeamCount(Rule r) => r == Rule.KBO ? 10 : 12;

        /// <summary>팀당 정규 시즌 경기 수(KBO 144 · NPB 143).</summary>
        public static int RegularGames(Rule r) => r == Rule.KBO ? 144 : 143;

        public static int LeagueCount(Rule r) => r == Rule.KBO ? 1 : 2;

        /// <summary>NPB: 0 ~ 5 센트럴(내 도시 팀 포함) · 6 ~ 11 퍼시픽.</summary>
        public static int LeagueOf(Rule r, int team) => r == Rule.NPB && team >= 6 ? 1 : 0;

        public static string LeagueName(Rule r, int league) => r == Rule.KBO ? "KBO 리그" : league == 0 ? "센트럴 리그" : "퍼시픽 리그";

        public static bool Weekend(DayOfWeek d) => d == DayOfWeek.Saturday || d == DayOfWeek.Sunday;

        /// <summary>경기 시작 시각(자정부터 분): KBO 평일 18:30 · 주말 17:00, NPB 평일 18:00 · 주말 14:00.</summary>
        public static short StartMinute(Rule r, DayOfWeek d)
        {
            if (r == Rule.KBO) return (short)(Weekend(d) ? 17 * 60 : 18 * 60 + 30);
            return (short)(Weekend(d) ? 14 * 60 : 18 * 60);
        }

        /// <summary>정규 시즌 연장 상한(이 회까지 동점이면 무승부 · 포스트시즌은 끝날 때까지).</summary>
        public static int MaxInnings(Rule r) => r == Rule.KBO
            ? 12 // 규정 확인 필요: KBO 연장 이닝
            : 12; // 규정 확인 필요: NPB 연장 이닝

        /// <summary>더블헤더(KBO 만): 우천 취소 경기를 같은 3연전 다음 경기 날에 붙임. NPB 는 예비일만.</summary>
        public static bool DoubleHeader(Rule r) => r == Rule.KBO;

        /// <summary>더블헤더 시작 시각(분): 1차전 14:00 · 2차전 17:00.</summary>
        public const short DoubleFirstMinute = 14 * 60, DoubleSecondMinute = 17 * 60;

        /// <summary>포스트시즌 라운드: 차례 · 이름 · 선승 수 · 상위 시드가 안고 시작하는 승 수 · 상위 시드가 모든 경기 홈인지.</summary>
        public sealed class Round
        {
            public readonly string Name;
            public readonly int Need, Edge;
            public readonly bool AllHigh;

            public Round(string name, int need, int edge, bool allHigh)
            {
                Name = name;
                Need = need;
                Edge = edge;
                AllHigh = allHigh;
            }

            /// <summary>최대 경기 수(무승부 없음): 와일드카드 2 · 퍼스트 3 · 5전 · 파이널 6 · 7전.</summary>
            public int MaxGames => Need * 2 - 1 - Edge;
        }

        // KBO: 5팀 계단식. NPB: 리그마다 퍼스트(2위 대 3위) → 파이널(1위 대 퍼스트 승자) → 일본시리즈(두 리그 우승 팀).
        static readonly Round[] KboRounds =
        {
            new("와일드카드 결정전", 2, 1, true),
            new("준플레이오프", 3, 0, false),
            new("플레이오프", 3, 0, false),
            new("한국시리즈", 4, 0, false),
        };

        static readonly Round[] NpbRounds =
        {
            new("클라이맥스 시리즈 퍼스트", 2, 0, true),
            new("클라이맥스 시리즈 파이널", 4, 1, true),
            new("일본시리즈", 4, 0, false),
        };

        public static Round RoundOf(Rule r, int round) => (r == Rule.KBO ? KboRounds : NpbRounds)[round];

        public static int RoundCount(Rule r) => r == Rule.KBO ? KboRounds.Length : NpbRounds.Length;

        /// <summary>
        /// 시리즈 game 번째(0부터) 경기가 상위 시드 홈인지: 전 경기 홈 라운드가 아니면 1 · 2 · 5 · 6 · 7차전 상위 시드 홈.
        /// shortcut: 2-3-2 / 2-2-1 홈 배분을 하나로 묶음, 공식 배분을 따로 따지게 되면 라운드마다 홈 표로.
        /// </summary>
        public static bool HighHome(Round x, int game) => x.AllHigh || game != 2 && game != 3;

        // 10 · 11번은 NPB 12팀에만(KBO 는 앞 10팀)
        static readonly Team[] Korean =
        {
            new("한빛 펠리컨스", null, "한강 스타디움"),
            new("청솔 팰컨스", "청주"),
            new("누리 볼츠", "전주"),
            new("대성 레이븐스", "포항"),
            new("미래 코메츠", "천안"),
            new("한결 머스탱스", "원주"),
            new("동해 웨일스", "강릉"),
            new("새봄 오터스", "춘천"),
            new("태산 스톰즈", "안산"),
            new("가온 아이언스", "제주"),
            new("해솔 돌핀스", "목포"),
            new("은하 그리폰스", "여수"),
        };

        static readonly Team[] Japanese =
        {
            new("쓰바사 펠리컨스", null),
            new("아사기리 팰컨스", "니가타"),
            new("시라누이 볼츠", "구마모토"),
            new("토키와 레이븐스", "아오모리"),
            new("아마노 코메츠", "시즈오카"),
            new("아카네 머스탱스", "오카야마"),
            new("나기사 웨일스", "가고시마"),
            new("카에데 오터스", "나가노"),
            new("오보로 스톰즈", "가나자와"),
            new("쓰루기 아이언스", "마쓰야마"),
            new("미나모 돌핀스", "센다이"),
            new("호무라 그리폰스", "하코다테"),
        };

        public static Team TeamOf(Naming n, int index) => (n == Naming.Japanese ? Japanese : Korean)[index];

        /// <summary>점수판 줄임 이름(영문 3자 · 닉네임에서, 두 이름 방식 같음).</summary>
        static readonly string[] Abbrs = { "PEL", "FAL", "BOL", "RAV", "COM", "MUS", "WHA", "OTT", "STO", "IRN", "DOL", "GRI" };

        public static string Abbr(int index) => Abbrs[index];
    }
}
