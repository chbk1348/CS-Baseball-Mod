using System;
using System.Collections.Generic;

namespace BaseballLeague.Data
{
    /// <summary>포스트시즌 시리즈 하나: 라운드(Rules.RoundOf) · 리그(NPB 클라이맥스 시리즈만 0 센트럴 · 1 퍼시픽, 나머지 0) · 상위 시드 · 하위 시드. 경기는 Season.Games 에 Game.Series 로.</summary>
    public sealed class Series
    {
        public byte Round, League, High, Low;
    }

    /// <summary>우승 이력 한 줄: 연도(결승 마지막 경기 날) · 우승 · 준우승 팀 번호(이름은 지금 이름 방식으로 보임) · 우승 팀 정규 시즌 승패 · 시즌 MVP(이름 · 팀, 판 3 이전 세이브는 빈 글).</summary>
    public sealed class Title
    {
        public int Year;
        public byte Champion, Runner;
        public short W, L;
        public string Mvp = "";
        public byte MvpTeam;
    }

    /// <summary>
    /// 한 시즌: 규칙 · 이름 방식 · 시작일 · 시드 · 경기 목록(결과 포함 · 포스트시즌 경기도) · 포스트시즌 시리즈 · 지난 시즌 우승 이력.
    /// 새 시즌은 통째로 새 객체로 갈아 끼움(League.Current).
    /// </summary>
    public sealed class Season
    {
        public Rule Rule;
        public Naming Naming;
        public DateTime Start;
        public int Seed;
        public List<Game> Games = new();
        public List<Series> Post = new();

        /// <summary>지난 시즌들 우승 이력(이번 시즌 우승은 Champion 으로 따로 · Titles 가 합침).</summary>
        public List<Title> History = new();

        /// <summary>홈팀 편집값 · 스위치(새 시즌에도 이어 감). 고칠 땐 Club.Clone 으로 통째로.</summary>
        public Club Club = new();

        /// <summary>이 시즌을 열 때 정한 내 팀 시작 전력(Club.Power 0 · 1 · 2).</summary>
        public byte Power0 = 1;

        /// <summary>팀마다 명단(없으면 처음 쓸 때 시드로 지음 — 판 3 이전 세이브). 이름 방식을 바꾸면 통째로 갈아 끼움.</summary>
        public Roster[] Rosters;

        /// <summary>정규 시즌 선수 기록 합 [팀][명단 자리](경기를 정할 때 Sync 안에서 더함).</summary>
        public Line[][] Stats;

        public Roster RosterOf(int team)
        {
            var r = Rosters;
            if (r == null || team >= r.Length) Rosters = r = Data.Roster.Make(Naming, Seed, Rules.TeamCount(Rule), Power);
            return r[team];
        }

        public Line StatOf(int team, int slot)
        {
            var st = Stats;
            if (st == null || team >= st.Length) Stats = st = NewStats(Rules.TeamCount(Rule));
            return st[team][slot];
        }

        public static Line[][] NewStats(int teams)
        {
            var st = new Line[teams][];
            for (int t = 0; t < teams; t++)
            {
                st[t] = new Line[Data.Roster.Size];
                for (int i = 0; i < st[t].Length; i++) st[t][i] = new Line();
            }
            return st;
        }

        /// <summary>점수판 줄임 이름(편집값, 없으면 Rules.Abbr).</summary>
        public string Abbr(int team)
        {
            var a = Club.Abbr;
            return team < a.Length && !string.IsNullOrEmpty(a[team]) ? a[team] : Rules.Abbr(team);
        }

        /// <summary>감독(내 팀은 편집값이 있으면 그것).</summary>
        public string Manager(int team) => team == 0 && Club.Manager.Length > 0 ? Club.Manager : RosterOf(team).Manager;

        /// <summary>팀 색 번호(Pal.BallTeams): 내 팀은 편집값이 있으면 그것.</summary>
        public int ColorOf(int team) => team == 0 && Club.Color >= 0 ? Club.Color : team;

        public int Played
        {
            get
            {
                int n = 0;
                foreach (var g in Games)
                    if (g.Played) n++;
                return n;
            }
        }

        /// <summary>진행 중(한 경기라도 했고 포스트시즌 우승이 안 정해짐): 리그 규칙 잠김.</summary>
        public bool InProgress => !Finished && Played > 0;

        /// <summary>시즌 종료 = 마지막 라운드(한국시리즈 · 일본시리즈) 승부가 남.</summary>
        public bool Finished => Champion(out _);

        /// <summary>시리즈 index 에서 team 의 승 수(상위 시드는 안고 시작하는 승 포함).</summary>
        public int Wins(int index, int team)
        {
            var x = Post[index];
            int w = team == x.High ? Rules.RoundOf(Rule, x.Round).Edge : 0;
            foreach (var g in Games)
                if (g.Series == index + 1 && g.Played && g.HomeRuns != g.AwayRuns && (g.HomeRuns > g.AwayRuns ? g.Home : g.Away) == team) w++;
            return w;
        }

        /// <summary>시리즈 index 승자(선승 수에 먼저 닿은 팀), 아직이면 -1.</summary>
        public int Winner(int index)
        {
            var x = Post[index];
            int need = Rules.RoundOf(Rule, x.Round).Need;
            return Wins(index, x.High) >= need ? x.High : Wins(index, x.Low) >= need ? x.Low : -1;
        }

        /// <summary>시리즈 index 의 마지막 경기 날.</summary>
        public DateTime LastDay(int index)
        {
            DateTime d = default;
            foreach (var g in Games)
                if (g.Series == index + 1 && g.Day > d) d = g.Day;
            return d;
        }

        /// <summary>이번 시즌 우승(마지막 라운드 승부가 났을 때만).</summary>
        public bool Champion(out Title t)
        {
            t = null;
            int last = Post.Count - 1;
            if (last < 0 || Post[last].Round != Rules.RoundCount(Rule) - 1) return false;
            int w = Winner(last);
            if (w < 0) return false;
            t = new Title { Year = LastDay(last).Year, Champion = (byte)w, Runner = w == Post[last].High ? Post[last].Low : Post[last].High };
            return true;
        }

        /// <summary>지난 이력 + 이번 시즌 우승(새 목록 · 이번 시즌 줄엔 정규 시즌 승패 · MVP 를 채움).</summary>
        public List<Title> Titles()
        {
            var list = new List<Title>(History);
            if (Champion(out var t))
            {
                var r = League.Record(this, t.Champion);
                t.W = (short)r.W;
                t.L = (short)r.L;
                if (League.Mvp(this, out int team, out int slot))
                {
                    t.Mvp = RosterOf(team).P[slot].Name;
                    t.MvpTeam = (byte)team;
                }
                list.Add(t);
            }
            return list;
        }

        /// <summary>팀 전력 범위: PowerMin ~ PowerMin + PowerSpan · 내 팀 시작 전력 한 단계 폭.</summary>
        public const float PowerMin = 0.40f, PowerSpan = 0.20f, PowerStep = 0.06f;

        /// <summary>팀 전력 0.40 ~ 0.60(시드로 정함 · 저장하지 않음). 내 팀은 시작 전력(Power0)만큼 더하거나 뺌.</summary>
        public float Power(int team)
        {
            var rng = new Random(unchecked(Seed * 31 + team * 7919));
            float p = PowerMin + (float)rng.NextDouble() * PowerSpan;
            if (team == 0) p = Math.Max(PowerMin, Math.Min(PowerMin + PowerSpan, p + (Power0 - 1) * PowerStep));
            return p;
        }

        Club mineFor;
        Team mine;

        /// <summary>팀(내 팀은 편집한 이름이 있으면 그 이름 · 같은 Club 이면 같은 객체).</summary>
        public Team Team(int index)
        {
            var t = Rules.TeamOf(Naming, index);
            var c = Club;
            if (index != 0 || c.Name.Length == 0) return t;
            if (mineFor != c || mine == null || mine.City != t.City || mine.Stadium != t.Stadium)
            {
                mine = new Team(c.Name, t.City, t.Stadium);
                mineFor = c;
            }
            return mine;
        }
    }

    /// <summary>순위표 한 줄(정규 시즌만). 승률 = 승 / (승 + 패)(무 제외 · KBO 방식), 게임 차 = ((1위 승 - 승) + (패 - 1위 패)) / 2, Runs = 득점.</summary>
    public sealed class Standing
    {
        public int Team, W, L, T, Runs;
        public float Pct => W + L == 0 ? 0f : (float)W / (W + L);
        public float GamesBehind;

        /// <summary>승률이 같은 팀끼리의 상대 전적 승률(동률 처리용).</summary>
        internal float Versus;
    }

    /// <summary>팀 한 시즌 기록(정규 시즌 끝난 경기만): 홈 · 원정 승패 · 득점 · 실점 · 연속(+ 연승 · - 연패 · 0 = 무승부 뒤 또는 경기 없음) · 최근 10경기(오래된 것 → 최근).</summary>
    public sealed class TeamRecord
    {
        public int W, L, T, HomeW, HomeL, AwayW, AwayL, Runs, Against, Streak;
        public readonly List<Game> Last10 = new();
    }

    /// <summary>
    /// 리그 진행(본 스레드에서만 고침 · 경기를 고칠 땐 Sync 를 잡음 — 저장이 반쯤 고친 경기를 쓰지 않게): 게임 날짜가 바뀌면 지난 날까지의 남은 경기를 팀 전력 확률로 정함.
    /// 홈구장 경기 이벤트가 끝나면(시뮬레이션 스레드 → QueueHomeResult) 그 승패를 이벤트 시작일 · 다음 날 홈 경기 결과로 먼저 넣음(Tick · 그 밖이면 버림).
    /// 우천 취소(3%)는 KBO 면 같은 3연전 다음 경기 날 더블헤더로, 못 붙이거나 NPB 면 시즌 끝 뒤 예비일(월요일 · 두 팀 경기 없는 날)로 다시 잡음.
    /// 정규 시즌이 다 끝나면 포스트시즌(Advance): 시리즈 경기는 하루에 하나씩 이어 붙이고, 승부가 나면 하루 쉬고 다음 라운드.
    /// </summary>
    public static class League
    {
        /// <summary>지금 시즌. 세이브(BaseballSave)가 저장 스레드에서 읽으므로 고칠 땐 새 객체로 통째로 바꿈.</summary>
        public static Season Current;

        /// <summary>바뀔 때마다 늘어남(창이 다시 그릴지 봄).</summary>
        public static int Version;

        /// <summary>경기 고치기 · 저장(BaseballSave) · 이벤트 대기열이 함께 잡는 자물쇠.</summary>
        public static readonly object Sync = new();

        /// <summary>홈구장 이벤트 결과(이벤트 시작 날짜 · 홈 승리 · 게임이 센 관중 · 자리, 모르면 0). 시뮬레이션 스레드가 넣고 본 스레드 Tick 이 꺼냄.</summary>
        sealed class HomeResult
        {
            public DateTime Day;
            public bool HomeWins;
            public int Crowd, Seats;
        }

        static readonly List<HomeResult> pending = new();

        /// <summary>전력 차(±0.20) 1 당 승률 점수: 최대 ±10점.</summary>
        public const float PowerPoints = 50f;

        const double RainChance = 0.03;

        /// <summary>
        /// 관중 추정(게임 이벤트가 센 홈구장 경기 밖): 자리 × (기본 + 주말 + 두 팀 순위) ± 흔들림, 포스트시즌은 만원.
        /// 홈구장 자리 = 방문 칸 × 5(Stadium.Read) = 게임 EventAI.CountVisitors 의 최대 방문객(같은 셈)이라, 홈구장 추정과 실측은 같은 단위. 홈구장을 모르면 ParkSeats(가상 단위 · Box.FlagPark).
        /// shortcut: 원정 구장은 모두 ParkSeats 자리 · 날씨 · 요금 · 연고 인구 없음, 구장마다 자리를 두게 되면 Rules 팀 표에.
        /// </summary>
        public const int ParkSeats = 24000;
        const float CrowdBase = 0.62f, CrowdWeekend = 0.12f, CrowdTop = 0.08f, CrowdBottom = 0.06f, CrowdRival = 0.05f, CrowdNoise = 0.06f, CrowdMin = 0.3f;
        public const int TopRanks = 3;

        /// <summary>내 홈구장 방문 자리(게임 값 = 이벤트 최대 방문객 · 본 스레드가 넣음, 0 = 모름 → ParkSeats).</summary>
        public static volatile int HomeSeats;

        static DateTime lastDay;

        /// <summary>올림픽 휴식 첫날 · 끝날(둘 다 포함, BreakTo = default 면 없음). 본 스레드에서만(LeagueController 가 날짜 바뀔 때 Olympic.Read 로 넣음).</summary>
        public static DateTime BreakFrom, BreakTo;

        public static bool OnBreak(DateTime day) => BreakTo != default && day >= BreakFrom && day <= BreakTo;

        /// <summary>새 시즌: today 와 같거나 뒤의 첫 화요일에 개막. 시드는 시작일 · 규칙에서. 우승 이력 · 홈팀 편집값은 이어 감, 명단은 이름 방식 · 팀 수가 같으면 이름을 남기고 능력을 새 전력으로 다시 정함.</summary>
        public static Season NewSeason(Rule rule, Naming naming, DateTime today)
        {
            var start = Schedule.NextSlotStart(today.Date);
            if (start.DayOfWeek != DayOfWeek.Tuesday) start = Schedule.NextSlotStart(start.AddDays(1));
            int seed = unchecked((int)(start.Ticks / TimeSpan.TicksPerDay) * 397 + (int)rule);
            var old = Current;
            var club = old?.Club ?? new Club();
            int n = Rules.TeamCount(rule);
            if (club.Color > 0 && club.Color < n)
            {
                // 내 팀 색이 새 규칙의 다른 팀 색(팀 번호 차례)과 겹치면 비어 있는 색(팀 수 뒤, 없으면 기본 색)으로
                club = club.Clone();
                club.Color = (sbyte)(n < Rules.TeamCount(Rule.NPB) ? n : -1);
            }
            var s = new Season { Rule = rule, Naming = naming, Start = start, Seed = seed, Games = Schedule.Make(rule, start, seed), History = old?.Titles() ?? new List<Title>(), Club = club, Power0 = club.Power };
            bool keep = old?.Rosters != null && old.Naming == naming && old.Rosters.Length == n;
            s.Rosters = Roster.Make(naming, seed, n, s.Power, keep ? old.Rosters : null);
            Current = s;
            lastDay = default;
            Version++;
            return s;
        }

        /// <summary>이름 방식만 바꿈(팀은 번호로 같아서 결과 그대로 · 선수 이름은 새 방식으로 다시 지음 · 능력 · 기록 그대로).</summary>
        public static void SetNaming(Naming naming)
        {
            var s = Current;
            if (s == null) return;
            lock (Sync)
            {
                s.Naming = naming;
                if (s.Rosters != null) s.Rosters = Roster.Rename(s.Rosters, naming, s.Seed);
            }
            Version++;
        }

        /// <summary>홈팀 편집값 · 스위치를 통째로 바꿈(본 스레드).</summary>
        public static void SetClub(Club club)
        {
            var s = Current;
            if (s == null) return;
            s.Club = club;
            Version++;
        }

        public static void Reset()
        {
            Current = null;
            lastDay = default;
            BreakFrom = BreakTo = default;
            lock (Sync) pending.Clear();
        }

        /// <summary>매 프레임: 이벤트 결과를 먼저 넣고, 날짜가 바뀐 때만 now 앞 날짜의 안 한 경기를 정함.</summary>
        public static void Tick(DateTime now)
        {
            var s = Current;
            if (s == null) return;
            if (pending.Count > 0) TakeResults(s);
            if (now.Date == lastDay) return;
            lastDay = now.Date;
            bool any = false, more = true;
            int[] ranks = null; // 관중 추정용(그날 처음 정할 때 한 번)
            lock (Sync)
                while (more) // 포스트시즌 · 우천 재편성이 지난 날짜에 경기를 붙이면 그것도 정함(여러 날을 건너뛸 때)
                {
                    bool moved = Break(s);
                    any |= moved;
                    for (int i = 0; i < s.Games.Count; i++)
                    {
                        var g = s.Games[i];
                        if (g.Played || g.Day >= lastDay) continue;
                        moved |= Play(s, g, i, ranks ??= Ranks(s)) && g.Day < lastDay;
                        any = true;
                    }
                    more = Advance(s) | moved;
                    any |= more;
                }
            if (any) Version++;
        }

        /// <summary>시뮬레이션 스레드: 홈구장 경기 이벤트가 끝남(day = 이벤트 시작 날짜 · crowd · seats = 게임이 센 관중 · 자리, 모르면 0).</summary>
        public static void QueueHomeResult(DateTime day, bool homeWins, int crowd = 0, int seats = 0)
        {
            lock (Sync) pending.Add(new HomeResult { Day = day, HomeWins = homeWins, Crowd = crowd, Seats = seats });
        }

        /// <summary>이벤트 결과 → 맞출 홈 경기를 시뮬로 정함: 승패가 같아질 때까지 시드 차례를 바꿔 다시 돌림(MaxAttempts 까지).</summary>
        static void TakeResults(Season s)
        {
            HomeResult[] taken;
            lock (Sync)
            {
                taken = pending.ToArray();
                pending.Clear();
            }
            bool any = false;
            foreach (var r in taken)
                lock (Sync)
                {
                    int i = HomeTarget(s, r.Day);
                    if (i < 0) continue;
                    var g = s.Games[i];
                    var res = Matched(s, g, i, r.HomeWins);
                    var box = res.Box;
                    if (r.Crowd > 0)
                    {
                        box.Crowd = (ushort)Math.Min(ushort.MaxValue, r.Crowd);
                        box.Flags |= Box.FlagReal;
                        if (r.Seats > 0 && r.Crowd >= r.Seats) box.Flags |= Box.FlagFull;
                    }
                    else Crowd(s, g, i, box, Ranks(s));
                    Record(s, g, res);
                    Advance(s); // 포스트시즌 홈 경기면 다음 경기 · 라운드
                    any = true;
                }
            if (any) Version++;
        }

        /// <summary>
        /// 홈 승패 homeWins 에 맞는 시뮬 결과(무승부 아님). 상한 안에 못 맞추면 마지막 결과의 이긴 쪽 마지막 이닝에 점수를 더해 맞춤(점수 맞춤 표시).
        /// shortcut: 맞춘 점수는 선수 기록에 없음(박스만), 시뮬 승률을 게임 승률에 맞추게 되면 여기서 다시 돌릴 일이 줄어듦.
        /// </summary>
        public static Result Matched(Season s, Game g, int index, bool homeWins, int attempts = Sim.MaxAttempts)
        {
            Result res = null;
            for (int k = 0; k < attempts; k++)
            {
                res = Sim.Run(s, g, index, k);
                if (res.Home != res.Away && res.Home > res.Away == homeWins) return res;
            }
            var box = res.Box;
            int win = homeWins ? 1 : 0, need = res.Box.Runs(1 - win) + 1 - res.Box.Runs(win);
            var inn = box.Inn[win];
            if (inn.Length == 0) box.Inn[win] = inn = new byte[1];
            inn[inn.Length - 1] = (byte)(inn[inn.Length - 1] + need);
            // 원정이 마지막 초에 앞서게 됐는데 홈이 그 말을 안 쳤으면(9회말 없음) 0점 말을 붙임 — 홈이 지는 경기는 마지막 말까지 침
            if (win == 0 && box.Inn[1].Length < box.Inn[0].Length)
            {
                var home = new byte[box.Inn[0].Length];
                Array.Copy(box.Inn[1], home, box.Inn[1].Length);
                box.Inn[1] = home;
            }
            box.Flags |= Box.FlagFixed;
            box.Win = box.Lose = box.Save = -1;
            return res;
        }

        /// <summary>이벤트가 맞출 홈 경기(결과 · 승률 조정 · 확장점이 함께 씀): day(이벤트 시작 게임 날짜) 또는 그 다음 날의 안 한 내 도시 팀(0번) 홈 경기 중 가장 이른 것. 없으면 -1(결과를 버림). played = 이미 정한 경기도 봄(끝난 이벤트가 맞춘 경기 찾기).</summary>
        public static int HomeTarget(Season s, DateTime day, bool played = false)
        {
            day = day.Date;
            var last = day.AddDays(HomeTargetDays);
            int best = -1;
            for (int i = 0; i < s.Games.Count; i++)
            {
                var g = s.Games[i];
                if (g.Home != 0 || g.Played && !played || g.Day < day || g.Day > last) continue;
                if (best < 0 || g.Day < s.Games[best].Day) best = i;
            }
            return best;
        }

        /// <summary>
        /// 경기장 정보 창에 보일 이벤트 상대(팀 번호 · 없으면 -1 → 게임 이름 그대로): 안 끝난 이벤트는 결과를 넣을 홈 경기(HomeTarget), 끝난 이벤트는 그날 · 다음 날 홈 경기 중 가장 이른 것(이미 정했어도).
        /// shortcut: 끝난 이벤트는 그날 경기가 이벤트보다 먼저 정해졌으면(이벤트가 다음 날 경기에 맞춤) · 더블헤더면 다른 경기를 볼 수 있음, 맞춘 경기를 박스에 남기게 되면 그걸로.
        /// </summary>
        public static int EventOpponent(Season s, DateTime day, bool ended)
        {
            int i = HomeTarget(s, day, ended);
            return i < 0 ? -1 : s.Games[i].Away;
        }

        /// <summary>day 부터 days 일 안에 안 한 내 도시 팀 홈 경기가 있나(홈구장 경기를 쉬지 않고 열지 정함 · 비시즌이면 게임대로).</summary>
        public static bool HomeGameSoon(Season s, DateTime day, int days)
        {
            day = day.Date;
            var last = day.AddDays(days);
            lock (Sync) // 시뮬레이션 스레드에서도 부름 · 본 스레드 Tick 이 경기를 붙이는 중일 수 있음
                foreach (var g in s.Games)
                    if (g.Home == 0 && !g.Played && g.Day >= day && g.Day <= last) return true;
            return false;
        }

        /// <summary>이벤트 시작일 뒤 며칠까지의 홈 경기에 맞추나(그 뒤 경기는 그날 모드가 정함).</summary>
        public const int HomeTargetDays = 1;

        /// <summary>시뮬레이션 스레드: 게임 승률(0 ~ 100)을 그 이벤트가 맞출 홈 경기의 상대 전력으로 조정. 맞출 경기가 없으면 그대로.</summary>
        public static float HomeWinChance(float gameChance, DateTime day)
        {
            var s = Current;
            if (s == null) return gameChance;
            int away;
            lock (Sync)
            {
                int i = HomeTarget(s, day);
                if (i < 0) return gameChance;
                away = s.Games[i].Away;
            }
            return s.Club.AdjustWin ? WinChance(gameChance, s.Power(0), s.Power(away)) : gameChance;
        }

        /// <summary>승률 조정: 전력 차 × PowerPoints 를 더하고 게임 범위 0 ~ 100 으로 자름.</summary>
        public static float WinChance(float gameChance, float homePower, float awayPower) => Math.Max(0f, Math.Min(100f, gameChance + (homePower - awayPower) * PowerPoints));

        static Random Rng(Season s, Game g, int index) => new(unchecked(s.Seed * 31 + index * 7919 + (int)(g.Day.Ticks / TimeSpan.TicksPerDay)));

        /// <summary>
        /// 그날 비로 취소될 경기인지(결정적 · 경기를 정하기 전 날짜로 봄 — 화면도 같은 값으로 '우천 취소'를 미리 보임).
        /// shortcut: 다시 잡힌 경기 · 더블헤더 · 포스트시즌은 비로 취소하지 않음(취소 사슬 없음), 날씨 연동 때 다시 봄.
        /// </summary>
        public static bool Rains(Season s, Game g, int index) => !g.Played && g.Series == 0 && !g.Moved && !g.Double && Rng(s, g, index).NextDouble() < RainChance;

        /// <summary>경기 하나를 정함. 비로 취소돼 다른 날로 옮겼으면 true(옮긴 경기는 다시 비로 취소되지 않음).</summary>
        static bool Play(Season s, Game g, int index, int[] ranks)
        {
            if (Rains(s, g, index))
            {
                Rain(s, g);
                return true;
            }
            var res = Sim.Run(s, g, index, 0);
            Crowd(s, g, index, res.Box, ranks);
            Record(s, g, res);
            return false;
        }

        /// <summary>시뮬 결과를 경기에 넣음(Sync 안): 정규 시즌이면 선수 기록에 더하고, 박스 → 점수 차례로(Played 는 마지막에 HomeRuns 로).</summary>
        static void Record(Season s, Game g, Result res)
        {
            var box = res.Box;
            if (g.Series == 0)
                for (int side = 0; side < 2; side++)
                {
                    int team = side == 0 ? g.Away : g.Home;
                    for (int i = 0; i < Roster.Size; i++)
                    {
                        var line = res.Lines[side][i];
                        if (box.Fixed) line.V[Line.W] = line.V[Line.L] = line.V[Line.SV] = 0; // 맞춘 승패는 투수 기록에 넣지 않음
                        s.StatOf(team, i).Add(line);
                    }
                }
            g.Box = box;
            SetResult(g, box.Runs(1), box.Runs(0));
        }

        /// <summary>팀마다 자기 리그 순위(1부터).</summary>
        public static int[] Ranks(Season s)
        {
            var rank = new int[Rules.TeamCount(s.Rule)];
            for (int lg = 0; lg < Rules.LeagueCount(s.Rule); lg++)
            {
                var t = Standings(s, lg);
                for (int i = 0; i < t.Count; i++) rank[t[i].Team] = i + 1;
            }
            return rank;
        }

        /// <summary>그 경기 구장 자리(내 홈구장 = 게임 값, 나머지 = ParkSeats).</summary>
        public static int Seats(Game g) => g.Home == 0 && HomeSeats > 0 ? HomeSeats : ParkSeats;

        /// <summary>예상 관중(흔들림 없음 · 화면 '예상'): 자리 × 비율.</summary>
        public static int Expected(Season s, Game g, int[] ranks) => (int)(Seats(g) * CrowdShare(s, g, ranks, 0f));

        static float CrowdShare(Season s, Game g, int[] ranks, float noise)
        {
            if (g.Series != 0) return 1f;
            int n = Rules.TeamCount(s.Rule) / Rules.LeagueCount(s.Rule);
            float k = CrowdBase + (Rules.Weekend(g.Day.DayOfWeek) ? CrowdWeekend : 0f) + noise;
            int h = ranks[g.Home], a = ranks[g.Away];
            if (h > 0 && h <= TopRanks) k += CrowdTop;
            else if (h >= n - 1) k -= CrowdBottom;
            if (a > 0 && a <= TopRanks) k += CrowdRival;
            return Math.Max(CrowdMin, Math.Min(1f, k));
        }

        /// <summary>추정 관중을 박스에(만원 = 자리만큼).</summary>
        static void Crowd(Season s, Game g, int index, Box box, int[] ranks)
        {
            float noise = (float)((Rng(s, g, index + 1).NextDouble() * 2.0 - 1.0) * CrowdNoise);
            int seats = Seats(g), crowd = (int)(seats * CrowdShare(s, g, ranks, noise));
            box.Crowd = (ushort)Math.Min(ushort.MaxValue, crowd);
            if (crowd >= seats) box.Flags |= Box.FlagFull;
            if (!StadiumUnit(g)) box.Flags |= Box.FlagPark;
        }

        /// <summary>그 경기 자리가 홈구장(게임 이벤트) 단위인가: 내 홈 경기이고 홈구장 자리를 앎.</summary>
        static bool StadiumUnit(Game g) => g.Home == 0 && HomeSeats > 0;

        /// <summary>박스 관중이 지금 홈구장 단위와 같은가(실측 · 홈구장 자리로 추정 = 경기장 단위, FlagPark = 가상 단위). 홈 화면 · 홈구장 판이 같은 단위끼리만 모음.</summary>
        public static bool SameUnit(Box b) => ((b.Flags & (Box.FlagReal | Box.FlagPark)) == Box.FlagPark) == (HomeSeats <= 0);

        /// <summary>홈구장 관중 집계(정규 시즌 끝난 내 홈 경기 · 지금 단위만): 실측(Real)이 있으면 실측만, 없으면 추정만. 평균 · 최다 · 만원 · 자리.</summary>
        public sealed class CrowdSum
        {
            public bool Real;
            public int Games, Total, Full, Seats;
            public Game Best;
            public int Avg => Games == 0 ? 0 : Total / Games;
        }

        public static CrowdSum HomeCrowds(Season s)
        {
            var c = new CrowdSum { Seats = Seats(new Game()) };
            foreach (var g in s.Games)
                if (g.Home == 0 && g.Series == 0 && g.Played && g.Box != null && SameUnit(g.Box) && (g.Box.Flags & Box.FlagReal) != 0) c.Real = true;
            foreach (var g in s.Games)
            {
                var b = g.Box;
                if (g.Home != 0 || g.Series != 0 || !g.Played || b == null || !SameUnit(b) || ((b.Flags & Box.FlagReal) != 0) != c.Real) continue;
                c.Games++;
                c.Total += b.Crowd;
                if ((b.Flags & Box.FlagFull) != 0) c.Full++;
                if (c.Best == null || b.Crowd > c.Best.Box.Crowd) c.Best = g;
            }
            return c;
        }

        /// <summary>
        /// 시즌 MVP(정규 시즌 선수 기록에서 가장 높은 점수). 기록이 없으면 false.
        /// shortcut: 단순 점수식 — 타자 = 루타 + 타점 + 득점 + 볼넷 / 2, 투수 = 아웃 / 2 + 탈삼진 / 2 + 승 × 4 + 세이브 × 3 − 실점 × 2, 투표 · WAR 이 필요해지면 여기서.
        /// </summary>
        public static bool Mvp(Season s, out int team, out int slot)
        {
            team = slot = -1;
            if (s.Stats == null) return false;
            float best = 0f;
            for (int t = 0; t < s.Stats.Length; t++)
                for (int i = 0; i < Roster.Size; i++)
                {
                    float v = MvpScore(s.Stats[t][i], i >= Roster.FirstPitcher);
                    if (v <= best) continue;
                    best = v;
                    team = t;
                    slot = i;
                }
            return team >= 0;
        }

        public static float MvpScore(Line l, bool pitcher)
        {
            if (pitcher) return l[Line.Outs] * 0.5f + l[Line.K] * 0.5f + l[Line.W] * 4f + l[Line.SV] * 3f - l[Line.RA] * 2f;
            int tb = l[Line.H] + l[Line.D] + l[Line.T] * 2 + l[Line.HR] * 3;
            return tb + l[Line.RBI] + l[Line.R] + l[Line.BB] * 0.5f;
        }

        static void SetResult(Game g, int homeRuns, int awayRuns)
        {
            // 원정 먼저: Played 는 HomeRuns 로 봄(저장 스레드가 반쯤 쓴 결과를 끝난 경기로 읽지 않게)
            g.AwayRuns = (sbyte)Math.Max(0, Math.Min(sbyte.MaxValue, awayRuns));
            g.HomeRuns = (sbyte)Math.Max(0, Math.Min(sbyte.MaxValue, homeRuns));
        }

        /// <summary>우천 취소: KBO 는 더블헤더(그날 경기가 1차전 14:00, 옮긴 경기가 2차전 17:00), 못 붙이거나 NPB 면 예비일.</summary>
        static void Rain(Season s, Game g)
        {
            var p = Rules.DoubleHeader(s.Rule) ? DoubleDay(s, g) : null;
            if (p != null)
            {
                g.Day = p.Day;
                p.Minute = Rules.DoubleFirstMinute;
                g.Minute = Rules.DoubleSecondMinute;
                p.Double = g.Double = true;
            }
            else
            {
                g.Day = ReserveDay(s, g);
                g.Minute = Rules.StartMinute(s.Rule, g.Day.DayOfWeek);
            }
            g.Moved = true;
        }

        /// <summary>
        /// 더블헤더 짝: 같은 3연전(다음 칸 시작 전) 안의 뒤 날 같은 대진 안 한 경기 중 두 팀 다 그날 한 경기뿐인 것, 주말 먼저 그다음 이른 날. 없으면 null.
        /// shortcut: 공휴일 달력이 없어 주말만 우선, 공휴일을 넣게 되면 Rules.Weekend 옆에 휴일 표로.
        /// </summary>
        static Game DoubleDay(Season s, Game g)
        {
            var slotEnd = Schedule.NextSlotStart(g.Day.AddDays(1));
            Game best = null;
            foreach (var p in s.Games)
            {
                if (p == g || p.Played || p.Double || p.Series != 0 || p.Home != g.Home || p.Away != g.Away || p.Day <= g.Day || p.Day >= slotEnd) continue;
                if (GamesOn(s, p.Day, g.Home, g) >= 2 || GamesOn(s, p.Day, g.Away, g) >= 2) continue;
                bool wk = Rules.Weekend(p.Day.DayOfWeek);
                if (best == null || wk && !Rules.Weekend(best.Day.DayOfWeek) || wk == Rules.Weekend(best.Day.DayOfWeek) && p.Day < best.Day) best = p;
            }
            return best;
        }

        static int GamesOn(Season s, DateTime day, int team, Game except)
        {
            int n = 0;
            foreach (var g in s.Games)
                if (g != except && g.Day == day && (g.Home == team || g.Away == team)) n++;
            return n;
        }

        /// <summary>
        /// 포스트시즌 진행(Sync 안 · 본 스레드): 정규 시즌이 다 끝나면 첫 라운드(정규 끝 다음 날), 승부가 안 났고 남은 경기가 없는 시리즈엔 다음 날 경기,
        /// 승부가 난 시리즈 뒤엔 하루 쉬고 다음 라운드. 무엇이든 더했으면 true.
        /// shortcut: 시리즈 안 이동일 · 우천 없음, 공식 일정(이동일 · 예비일)을 따르게 되면 AddGame 날짜 계산에서.
        /// </summary>
        static bool Advance(Season s)
        {
            DateTime end = default;
            foreach (var g in s.Games)
            {
                if (g.Series != 0) continue;
                if (!g.Played) return false;
                if (g.Day > end) end = g.Day;
            }
            if (s.Post.Count == 0)
            {
                // KBO 4위 대 5위 · NPB 리그마다 2위 대 3위
                for (int lg = 0; lg < Rules.LeagueCount(s.Rule); lg++)
                {
                    var t = Standings(s, lg);
                    int high = s.Rule == Rule.KBO ? 3 : 1;
                    AddSeries(s, 0, lg, t[high].Team, t[high + 1].Team, end.AddDays(1));
                }
                return true;
            }
            bool added = false;
            int count = s.Post.Count;
            for (int i = 0; i < count; i++)
            {
                var x = s.Post[i];
                int w = s.Winner(i);
                var last = s.LastDay(i);
                if (w < 0)
                {
                    if (!HasOpenGame(s, i))
                    {
                        AddGame(s, i, last.AddDays(1));
                        added = true;
                    }
                    continue;
                }
                int next = x.Round + 1;
                bool japan = s.Rule == Rule.NPB && next == 2;
                if (next >= Rules.RoundCount(s.Rule) || Find(s, next, japan ? 0 : x.League) >= 0) continue;
                if (s.Rule == Rule.KBO)
                    AddSeries(s, next, 0, Standings(s, 0)[3 - next].Team, w, last.AddDays(2)); // 3위 · 2위 · 1위가 차례로 기다림
                else if (!japan)
                    AddSeries(s, next, x.League, Standings(s, x.League)[0].Team, w, last.AddDays(2));
                else
                {
                    // 일본시리즈: 두 리그 파이널이 다 끝나야. shortcut: 센트럴 우승 팀을 상위 시드(홈 우선)로 고정, 해마다 번갈아 가게 되면 여기서
                    int other = Find(s, 1, 1 - x.League);
                    if (other < 0 || s.Winner(other) < 0) continue;
                    int central = x.League == 0 ? i : other, pacific = x.League == 0 ? other : i;
                    var o = s.LastDay(other);
                    AddSeries(s, 2, 0, s.Winner(central), s.Winner(pacific), (o > last ? o : last).AddDays(2));
                }
                added = true;
            }
            return added;
        }

        static int Find(Season s, int round, int league)
        {
            for (int i = 0; i < s.Post.Count; i++)
                if (s.Post[i].Round == round && s.Post[i].League == league) return i;
            return -1;
        }

        static bool HasOpenGame(Season s, int index)
        {
            foreach (var g in s.Games)
                if (g.Series == index + 1 && !g.Played) return true;
            return false;
        }

        static void AddSeries(Season s, int round, int league, int high, int low, DateTime day)
        {
            s.Post.Add(new Series { Round = (byte)round, League = (byte)league, High = (byte)high, Low = (byte)low });
            AddGame(s, s.Post.Count - 1, day);
        }

        /// <summary>시리즈 index 의 다음 경기(차전 번호로 홈을 정함 · 시작 시각은 평일 / 주말 값).</summary>
        static void AddGame(Season s, int index, DateTime day)
        {
            var x = s.Post[index];
            if (OnBreak(day)) day = BreakTo.AddDays(1);
            int k = 0;
            foreach (var g in s.Games)
                if (g.Series == index + 1) k++;
            bool high = Rules.HighHome(Rules.RoundOf(s.Rule, x.Round), k);
            s.Games.Add(new Game { Day = day, Minute = Rules.StartMinute(s.Rule, day.DayOfWeek), Home = high ? x.High : x.Low, Away = high ? x.Low : x.High, Series = (byte)(index + 1) });
        }

        /// <summary>올림픽 휴식: 휴식 기간의 안 한 경기를 밖으로 — 정규 시즌(더블헤더 · 예비일 포함)은 예비일, 포스트시즌은 휴식 다음 날. 옮겼으면 true.</summary>
        static bool Break(Season s)
        {
            if (BreakTo == default) return false;
            bool any = false;
            foreach (var g in s.Games)
            {
                if (g.Played || !OnBreak(g.Day)) continue;
                g.Double = false;
                if (g.Series == 0)
                {
                    g.Day = ReserveDay(s, g);
                    g.Moved = true;
                }
                else g.Day = BreakTo.AddDays(1);
                g.Minute = Rules.StartMinute(s.Rule, g.Day.DayOfWeek);
                any = true;
            }
            return any;
        }

        /// <summary>정규 일정 끝 다음 날부터: 월요일 · 올림픽 휴식이 아니고 두 팀 다 경기 없는 첫날.</summary>
        static DateTime ReserveDay(Season s, Game moving)
        {
            DateTime end = s.Start;
            foreach (var g in s.Games)
                if (!g.Moved && g.Day > end) end = g.Day;
            var busy = new HashSet<DateTime>();
            foreach (var g in s.Games)
                if (g != moving && (g.Home == moving.Home || g.Away == moving.Home || g.Home == moving.Away || g.Away == moving.Away)) busy.Add(g.Day);
            var d = end.AddDays(1);
            while (d.DayOfWeek == DayOfWeek.Monday || busy.Contains(d) || OnBreak(d)) d = d.AddDays(1);
            return d;
        }

        /// <summary>
        /// 확장점(홈구장 경기 이벤트): 내 도시 팀(0번)의 홈 경기 결과를 밖에서 넣음. 본 스레드에서만.
        /// day 또는 그 다음 날의 안 한 홈 경기(HomeTarget)가 있으면 결과를 넣고 true, 없으면 false.
        /// </summary>
        public static bool ReportHomeGame(DateTime day, int homeRuns, int awayRuns)
        {
            var s = Current;
            if (s == null) return false;
            lock (Sync)
            {
                int i = HomeTarget(s, day);
                if (i < 0) return false;
                SetResult(s.Games[i], homeRuns, awayRuns);
                Advance(s); // 포스트시즌 홈 경기면 다음 경기 · 라운드
            }
            Version++;
            return true;
        }

        /// <summary>
        /// 리그 league 정규 시즌 순위: 승률 → 승률 같은 팀끼리 상대 전적 승률 → 다득점 → 팀 번호.
        /// shortcut: 3팀 이상 동률은 그 팀들끼리 전적 합 한 번으로 가름(공식은 더 세분 · 그다음 전년도 순위 등), 공식 절차가 필요해지면 여기서.
        /// </summary>
        public static List<Standing> Standings(Season s, int league)
        {
            int n = Rules.TeamCount(s.Rule);
            var rows = new Dictionary<int, Standing>();
            var beat = new int[n, n]; // [이긴 팀, 진 팀]
            for (int t = 0; t < n; t++)
                if (Rules.LeagueOf(s.Rule, t) == league) rows[t] = new Standing { Team = t };
            foreach (var g in s.Games)
            {
                if (!g.Played || g.Series != 0) continue;
                rows.TryGetValue(g.Home, out var h);
                rows.TryGetValue(g.Away, out var a);
                if (h != null) h.Runs += g.HomeRuns;
                if (a != null) a.Runs += g.AwayRuns;
                if (g.HomeRuns == g.AwayRuns)
                {
                    if (h != null) h.T++;
                    if (a != null) a.T++;
                    continue;
                }
                bool homeWon = g.HomeRuns > g.AwayRuns;
                if (homeWon) beat[g.Home, g.Away]++;
                else beat[g.Away, g.Home]++;
                if (h != null)
                {
                    if (homeWon) h.W++;
                    else h.L++;
                }
                if (a != null)
                {
                    if (homeWon) a.L++;
                    else a.W++;
                }
            }
            var list = new List<Standing>(rows.Values);
            foreach (var r in list)
            {
                int w = 0, l = 0;
                foreach (var o in list)
                    if (o != r && o.Pct == r.Pct)
                    {
                        w += beat[r.Team, o.Team];
                        l += beat[o.Team, r.Team];
                    }
                r.Versus = w + l == 0 ? 0.5f : (float)w / (w + l);
            }
            list.Sort((x, y) => x.Pct != y.Pct ? y.Pct.CompareTo(x.Pct) : x.Versus != y.Versus ? y.Versus.CompareTo(x.Versus) : x.Runs != y.Runs ? y.Runs.CompareTo(x.Runs) : x.Team.CompareTo(y.Team));
            if (list.Count > 0)
                foreach (var r in list)
                    r.GamesBehind = ((list[0].W - r.W) + (r.L - list[0].L)) * 0.5f;
            return list;
        }

        /// <summary>끝난 경기 g 에서 team 의 결과: 1 승 · -1 패 · 0 무(또는 안 한 경기 · 그 팀 경기 아님).</summary>
        public static int Result(Game g, int team)
        {
            if (!g.Played || g.HomeRuns == g.AwayRuns || g.Home != team && g.Away != team) return 0;
            return (g.HomeRuns > g.AwayRuns) == (g.Home == team) ? 1 : -1;
        }

        /// <summary>team 의 정규 시즌 기록(날짜 · 시각 차례로 셈).</summary>
        public static TeamRecord Record(Season s, int team)
        {
            var r = new TeamRecord();
            var games = s.Games.FindAll(g => g.Played && g.Series == 0 && (g.Home == team || g.Away == team));
            games.Sort((a, b) => a.Day != b.Day ? a.Day.CompareTo(b.Day) : a.Minute.CompareTo(b.Minute));
            foreach (var g in games)
            {
                bool home = g.Home == team;
                r.Runs += home ? g.HomeRuns : g.AwayRuns;
                r.Against += home ? g.AwayRuns : g.HomeRuns;
                int res = Result(g, team);
                if (res == 0)
                {
                    r.T++;
                    r.Streak = 0;
                }
                else if (res > 0)
                {
                    r.W++;
                    if (home) r.HomeW++;
                    else r.AwayW++;
                    r.Streak = r.Streak > 0 ? r.Streak + 1 : 1;
                }
                else
                {
                    r.L++;
                    if (home) r.HomeL++;
                    else r.AwayL++;
                    r.Streak = r.Streak < 0 ? r.Streak - 1 : -1;
                }
            }
            int n = Math.Min(10, games.Count);
            r.Last10.AddRange(games.GetRange(games.Count - n, n));
            return r;
        }

        /// <summary>개발용 자체 검사: 순위 · 게임 차 · 팀 기록 · 확장점 · 이벤트 → 결과 연결 · 승률 조정.</summary>
        public static void SelfCheck(Action<bool, string> check)
        {
            var s = new Season { Rule = Rule.KBO, Start = new DateTime(2026, 3, 24) };
            s.Games.Add(new Game { Day = s.Start, Home = 0, Away = 1, HomeRuns = 5, AwayRuns = 2 });
            s.Games.Add(new Game { Day = s.Start.AddDays(1), Home = 0, Away = 1, HomeRuns = 3, AwayRuns = 3 });
            s.Games.Add(new Game { Day = s.Start.AddDays(2), Home = 2, Away = 1, HomeRuns = 1, AwayRuns = 4 });
            s.Games.Add(new Game { Day = s.Start.AddDays(3), Home = 0, Away = 2 });
            var old = Current;
            Current = s;
            check(ReportHomeGame(s.Start.AddDays(3), 7, 1), "확장점: 홈 경기 결과를 못 넣음");
            check(!ReportHomeGame(s.Start.AddDays(3), 7, 1), "확장점: 끝난 경기에 또 넣음");
            // 여러 날 건너뛰기: 비로 지난 날짜(더블헤더)에 옮긴 경기도 같은 Tick 에서 정함
            var jump = new Season { Rule = Rule.KBO, Start = s.Start, Seed = 2, Games = Schedule.Make(Rule.KBO, s.Start, 2) };
            var oldDay = lastDay;
            Current = jump;
            lastDay = default;
            var to = jump.Start.AddDays(60);
            Tick(to);
            check(jump.Games.Exists(g => g.Double && g.Day < to) && jump.Games.TrueForAll(g => g.Played || g.Day >= to), "진행: 여러 날 건너뛸 때 지난 날짜 더블헤더가 남음");
            lastDay = oldDay;
            Current = old;
            EventCheck(check);
            CrowdCheck(check);
            TieCheck(check);
            DoubleCheck(check);
            PostCheck(check);
            RosterCheck(check);
            int doubles = 0;
            foreach (Rule rule in new[] { Rule.KBO, Rule.NPB })
                for (int seed = 1; seed <= 3; seed++)
                    doubles += RunCheck(rule, seed, check);
            check(doubles > 0, "시즌: KBO 세 시즌에 더블헤더가 하나도 없음");
            // 올림픽 휴식: 입촌(개막 7일 전)에 알게 돼 이미 잡힌 더블헤더 · 예비일까지 옮김 · 시즌 끝에 걸리는 휴식
            RunCheck(Rule.KBO, 1, check, new DateTime(2026, 7, 24));
            RunCheck(Rule.KBO, 2, check, new DateTime(2026, 9, 18));
            RunCheck(Rule.NPB, 1, check, new DateTime(2026, 8, 7));
            BreakCheck(check);
            Olympic.SelfCheck(check);
            var t = Standings(s, 0);
            check(t[0].Team == 0 && t[0].W == 2 && t[0].L == 0 && t[0].T == 1 && t[0].Pct == 1f, "순위: 1위 0번 2승 0패 1무 1.000");
            var one = t.Find(r => r.Team == 1);
            check(one.W == 1 && one.L == 1 && one.T == 1 && one.Pct == 0.5f && one.GamesBehind == 1f, "순위: 1번 1승 1패 1무 .500 · 게임 차 1");
            var two = t.Find(r => r.Team == 2);
            check(two.L == 2 && two.GamesBehind == 2f, "순위: 2번 0승 2패 · 게임 차 2");
            var r0 = Record(s, 0);
            check(r0.W == 2 && r0.L == 0 && r0.T == 1 && r0.HomeW == 2 && r0.Runs == 15 && r0.Against == 6 && r0.Streak == 1 && r0.Last10.Count == 3, "팀 기록: 0번 2승 1무 · 득실 15 / 6 · 1연승");
            var r1 = Record(s, 1);
            check(r1.W == 1 && r1.L == 1 && r1.T == 1 && r1.AwayW == 1 && r1.AwayL == 1 && r1.Streak == 1 && Result(s.Games[2], 1) == 1 && Result(s.Games[2], 0) == 0, "팀 기록: 1번 원정 1승 1패 · 1연승");
        }

        /// <summary>이벤트 → 결과: 같은 날 · 다음 날 홈 경기에만 맞춤(이틀 뒤면 버림), 원정 · 끝난 경기 건너뜀, 승패대로 점수, 맞출 경기 없으면 버림, 승률 조정이 같은 경기를 봄 · 범위.</summary>
        static void EventCheck(Action<bool, string> check)
        {
            var d = new DateTime(2026, 4, 7);
            var s = new Season { Rule = Rule.KBO, Start = d, Seed = 3 };
            s.Games.Add(new Game { Day = d, Home = 1, Away = 0 });            // 0: 원정
            s.Games.Add(new Game { Day = d.AddDays(2), Home = 0, Away = 3 }); // 1: 홈
            s.Games.Add(new Game { Day = d.AddDays(1), Home = 0, Away = 2 }); // 2: 홈(목록에선 뒤지만 더 이름)
            s.Games.Add(new Game { Day = d.AddDays(3), Home = 0, Away = 4, HomeRuns = 1, AwayRuns = 0 }); // 3: 끝난 홈
            check(HomeTarget(s, d) == 2, "이벤트: 원정 건너뛰고 가장 이른 홈 경기");
            check(HomeGameSoon(s, d, 1) && !HomeGameSoon(s, d.AddDays(3), 7) && !HomeGameSoon(s, d.AddDays(-5), 3), "홈 경기 곧: 안 한 홈 경기만 · 기간 안만");
            check(HomeTarget(s, d.AddDays(2).AddHours(15)) == 1, "이벤트: 같은 날 홈 경기(시각 무시)");
            check(HomeTarget(s, d.AddDays(3)) == -1, "이벤트: 끝난 경기에 맞춤");
            check(HomeTarget(s, d.AddDays(-1)) == -1, "이벤트: 이틀 뒤 홈 경기에 맞춤");
            check(EventOpponent(s, d, false) == 2 && EventOpponent(s, d.AddDays(1).AddHours(15), false) == 2, "다음 홈 경기 상대: 결과를 넣을 홈 경기 상대가 아님");
            check(EventOpponent(s, d.AddDays(3), false) == -1 && EventOpponent(s, d.AddDays(3), true) == 4, "다음 홈 경기 상대: 끝난 이벤트가 정한 경기 상대를 못 찾음");
            check(EventOpponent(s, d.AddDays(2), true) == 3, "다음 홈 경기 상대: 끝난 이벤트에 가장 이른 홈 경기가 아님");
            check(EventOpponent(s, d.AddDays(-1), true) == -1 && EventOpponent(s, d.AddDays(5), false) == -1, "다음 홈 경기 상대: 원정 · 기간 밖 경기 상대를 씀");
            var old = Current;
            var oldDay = lastDay;
            Current = s;
            check(Math.Abs(HomeWinChance(50f, d) - WinChance(50f, s.Power(0), s.Power(2))) < 0.001f && HomeWinChance(50f, d.AddDays(-1)) == 50f, "이벤트: 승률 조정이 결과와 다른 경기를 봄");
            lastDay = d.AddDays(1); // 날짜 진행은 막고 대기열만
            QueueHomeResult(d, true);
            QueueHomeResult(d.AddDays(1).AddHours(20), false);
            QueueHomeResult(d.AddDays(9), true);
            Tick(d.AddDays(1));
            Game win = s.Games[2], lose = s.Games[1];
            check(win.Played && win.HomeRuns > win.AwayRuns && win.Box != null && win.Box.Runs(1) == win.HomeRuns, "이벤트: 승리가 홈 승으로(박스 점수와 같게)");
            check(lose.Played && lose.HomeRuns < lose.AwayRuns && lose.Box != null && lose.Box.Runs(0) == lose.AwayRuns, "이벤트: 패배가 다음 홈 경기 홈 패로(박스 점수와 같게)");
            // 승패 맞춤: 여러 경기 · 두 승패 모두 맞음, 시도 1번이면 점수 맞춤(표시 · 승패 투수 없음)도 승패가 맞음
            bool match = true, fixedOk = true, sameAgain = true;
            int fixes = 0;
            for (int i = 0; i < 40; i++)
            {
                var g = new Game { Day = d.AddDays(i), Home = 0, Away = (byte)(1 + i % 9) };
                bool want = i % 2 == 0;
                var r = Matched(s, g, 100 + i, want);
                match &= r.Home != r.Away && r.Home > r.Away == want;
                var again = Matched(s, g, 100 + i, want);
                sameAgain &= again.Home == r.Home && again.Away == r.Away && again.Box.Attempt == r.Box.Attempt;
                if (r.Box.Attempt > 0 && !r.Box.Fixed) check(Sim.Run(s, g, 100 + i, r.Box.Attempt).Home == r.Home, "이벤트: 고른 시드 차례로 다시 돌렸는데 점수가 다름");
                var one = Matched(s, g, 100 + i, want, 1);
                if (one.Box.Fixed)
                {
                    fixes++;
                    fixedOk &= one.Home != one.Away && one.Home > one.Away == want && one.Box.Win < 0 && one.Box.Lose < 0 && one.Box.Runs(1) == one.Home && one.Box.Runs(0) == one.Away
                        && (want || one.Box.Inn[1].Length == one.Box.Inn[0].Length); // 홈이 지면 마지막 말까지 침
                }
            }
            check(match, "이벤트: 시뮬 결과가 이벤트 승패와 다름");
            check(sameAgain, "이벤트: 같은 이벤트 결과를 다시 맞추면 결과가 다름");
            check(fixes > 0 && fixedOk, "이벤트: 상한을 넘은 경기의 점수 맞춤이 승패와 다름");
            check(!s.Games[0].Played && pending.Count == 0, "이벤트: 원정에 들어감 · 대기열 남음");
            Current = old;
            lastDay = oldDay;
            check(Math.Abs(WinChance(50f, 0.6f, 0.4f) - 60f) < 0.01f && Math.Abs(WinChance(50f, 0.4f, 0.6f) - 40f) < 0.01f, "승률: 전력 차 ±0.2 → ±10점");
            check(WinChance(95f, 0.6f, 0.4f) == 100f && WinChance(3f, 0.4f, 0.6f) == 0f, "승률: 0 ~ 100 으로 자름");
        }

        /// <summary>관중 단위: 홈구장을 모르면 추정에 가상 단위 표시, 홈구장 집계는 지금 단위끼리 · 실측이 있으면 실측만.</summary>
        static void CrowdCheck(Action<bool, string> check)
        {
            int oldSeats = HomeSeats;
            var d = new DateTime(2026, 4, 7);
            var s = new Season { Rule = Rule.KBO, Start = d, Seed = 4 };
            Game Home(int crowd, byte flags)
            {
                var g = new Game { Day = d, Home = 0, Away = 1, HomeRuns = 2, AwayRuns = 1, Box = new Box { Crowd = (ushort)crowd, Flags = flags } };
                s.Games.Add(g);
                return g;
            }
            var ranks = new int[Rules.TeamCount(s.Rule)];
            HomeSeats = 0;
            var park = new Box();
            Crowd(s, new Game { Day = d, Home = 0, Away = 1 }, 0, park, ranks);
            HomeSeats = 1000;
            var venue = new Box();
            Crowd(s, new Game { Day = d, Home = 0, Away = 1 }, 0, venue, ranks);
            check((park.Flags & Box.FlagPark) != 0 && (venue.Flags & Box.FlagPark) == 0 && venue.Crowd <= 1000 && park.Crowd > 1000, "관중: 홈구장 추정이 이벤트 자리 단위가 아님 · 가상 단위 표시가 다름");
            Home(900, Box.FlagReal | Box.FlagFull);
            Home(700, 0);
            Home(15000, Box.FlagPark);
            var c = HomeCrowds(s);
            check(c.Real && c.Games == 1 && c.Avg == 900 && c.Full == 1 && c.Seats == 1000 && c.Best.Box.Crowd == 900, "관중: 실측이 있는데 추정 · 다른 단위와 섞음");
            s.Games.RemoveAt(0);
            c = HomeCrowds(s);
            check(!c.Real && c.Games == 1 && c.Avg == 700, "관중: 실측이 없을 때 홈구장 단위 추정만 모으지 않음");
            HomeSeats = 0;
            c = HomeCrowds(s);
            check(!c.Real && c.Games == 1 && c.Avg == 15000 && c.Seats == ParkSeats, "관중: 홈구장을 모를 때 가상 단위만 모으지 않음");
            HomeSeats = oldSeats;
        }

        /// <summary>동률: 승률 같으면 상대 전적, 그것도 같으면 다득점.</summary>
        static void TieCheck(Action<bool, string> check)
        {
            var d = new DateTime(2026, 4, 7);
            var s = new Season { Rule = Rule.KBO, Start = d };
            void Add(int home, int away, int hr, int ar) => s.Games.Add(new Game { Day = d, Home = (byte)home, Away = (byte)away, HomeRuns = (sbyte)hr, AwayRuns = (sbyte)ar });
            Add(5, 2, 5, 1); // 5 가 2 를 이김
            Add(2, 7, 9, 0); // 2 득점이 더 많음(10 대 8)
            Add(8, 5, 4, 3);
            Add(3, 9, 2, 1); // 1.000 셋(8 · 3 · 4)은 서로 안 붙음 → 득점 4 > 8 > 3
            Add(4, 6, 6, 0);
            var t = Standings(s, 0);
            int At(int team) => t.FindIndex(r => r.Team == team);
            check(At(4) == 0 && At(8) == 1 && At(3) == 2, "동률: 상대 전적 없으면 다득점 순 아님");
            check(At(5) < At(2), "동률: 승률 같은데 상대 전적이 득점보다 먼저 아님");
        }

        /// <summary>더블헤더: 같은 3연전 다음 날 · 1차전 14:00 · 2차전 17:00, 3연전 끝이면 예비일, 한 팀 하루 2경기 넘으면 다른 날, NPB 는 예비일만.</summary>
        static void DoubleCheck(Action<bool, string> check)
        {
            var tue = new DateTime(2026, 4, 7);
            var s = new Season { Rule = Rule.KBO, Start = tue };
            void Add(Season x, int day, int home, int away) => x.Games.Add(new Game { Day = tue.AddDays(day), Minute = Rules.StartMinute(x.Rule, tue.AddDays(day).DayOfWeek), Home = (byte)home, Away = (byte)away });
            for (int k = 0; k < 3; k++) Add(s, k, 0, 1); // 화 ~ 목
            for (int k = 3; k < 6; k++) Add(s, k, 2, 0); // 금 ~ 일
            Add(s, 4, 3, 0); // 토요일 0번 한 경기 더(일부러) → 토요일엔 못 붙임
            Game tu = s.Games[0], we = s.Games[1], th = s.Games[2], fr = s.Games[3];
            Rain(s, tu);
            check(tu.Day == we.Day && tu.Double && we.Double && tu.Moved, "더블헤더: 화요일 취소가 수요일로 안 붙음");
            check(we.Minute == Rules.DoubleFirstMinute && tu.Minute == Rules.DoubleSecondMinute, "더블헤더: 1차전 14:00 · 2차전 17:00 아님");
            Rain(s, th);
            check(!th.Double && th.Moved && th.Day == tue.AddDays(7), "더블헤더: 3연전 마지막 날 취소가 예비일(다음 화요일)로 안 감");
            Rain(s, fr);
            check(fr.Day == tue.AddDays(5) && fr.Double, "더블헤더: 한 팀 하루 2경기 넘는 토요일을 피해 일요일로 안 감");
            var n = new Season { Rule = Rule.NPB, Start = tue };
            for (int k = 0; k < 3; k++) Add(n, k, 0, 1);
            Rain(n, n.Games[0]);
            check(!n.Games[0].Double && n.Games[0].Day > tue.AddDays(2), "더블헤더: NPB 는 예비일만 아님");
        }

        /// <summary>정규 시즌을 다 한 시즌: 같은 리그끼리 한 번씩 붙어 번호 작은 팀이 이김(순위 = 번호 차례).</summary>
        static Season Seeded(Rule rule)
        {
            var d = new DateTime(2026, 9, 29);
            var s = new Season { Rule = rule, Start = d, Seed = 5 };
            int n = Rules.TeamCount(rule);
            for (int a = 0; a < n; a++)
                for (int b = a + 1; b < n; b++)
                    if (Rules.LeagueOf(rule, a) == Rules.LeagueOf(rule, b))
                        s.Games.Add(new Game { Day = d, Home = (byte)a, Away = (byte)b, HomeRuns = 3, AwayRuns = 1 });
            return s;
        }

        /// <summary>시리즈 index 의 남은 경기를 정하고(상위 시드 승 / 패) 진행.</summary>
        static void Decide(Season s, int index, bool highWins)
        {
            foreach (var g in s.Games)
                if (g.Series == index + 1 && !g.Played)
                {
                    bool homeWins = g.Home == s.Post[index].High == highWins;
                    SetResult(g, homeWins ? 4 : 2, homeWins ? 2 : 4);
                    Advance(s);
                    return;
                }
        }

        static List<Game> SeriesGames(Season s, int index) => s.Games.FindAll(g => g.Series == index + 1);

        /// <summary>대진 · 1승 어드밴티지 · 선승 종료 · 홈 배분 · 하루 쉼 · 우승 · 잠금 풀림 · 이력 이어 감.</summary>
        static void PostCheck(Action<bool, string> check)
        {
            var s = Seeded(Rule.KBO);
            check(Advance(s) && s.Post.Count == 1 && s.Post[0].High == 3 && s.Post[0].Low == 4, "KBO: 와일드카드가 4위 대 5위 아님");
            var g = SeriesGames(s, 0)[0];
            check(g.Home == 3 && g.Day == s.Start.AddDays(1) && g.Minute == Rules.StartMinute(Rule.KBO, g.Day.DayOfWeek), "KBO: 와일드카드 1차전 4위 홈 · 정규 끝 다음 날 · 시각 아님");
            check(s.Wins(0, 3) == 1 && s.Wins(0, 4) == 0 && !Advance(s), "KBO: 4위 1승 안고 시작 아님 · 남은 경기 있는데 더함");
            Decide(s, 0, true);
            check(s.Winner(0) == 3 && SeriesGames(s, 0).Count == 1, "KBO: 4위가 1차전 이겼는데 안 끝남");
            check(s.Post.Count == 2 && s.Post[1].Round == 1 && s.Post[1].High == 2 && s.Post[1].Low == 3 && s.LastDay(1) == s.LastDay(0).AddDays(2), "KBO: 준PO 3위 대 WC 승자 · 하루 쉼 아님");
            for (int k = 0; k < 3; k++) Decide(s, 1, true);
            var semi = SeriesGames(s, 1);
            check(s.Winner(1) == 2 && semi.Count == 3, "KBO: 준PO 3선승에 안 끝남");
            check(semi[0].Home == 2 && semi[1].Home == 2 && semi[2].Home == 3 && semi[2].Day == semi[1].Day.AddDays(1), "KBO: 준PO 홈 배분(1 · 2차전 상위 · 3차전 하위) · 연속 날짜 아님");
            check(s.Post.Count == 3 && s.Post[2].High == 1 && s.Post[2].Low == 2, "KBO: PO 가 2위 대 준PO 승자 아님");
            for (int k = 0; k < 4; k++) Decide(s, 2, k % 2 == 0);
            check(s.Winner(2) < 0 && SeriesGames(s, 2).Count == 5, "KBO: PO 2승 2패 뒤 5차전 없음");
            Decide(s, 2, false);
            check(s.Winner(2) == 2 && s.Post.Count == 4 && s.Post[3].High == 0 && s.Post[3].Low == 2, "KBO: 한국시리즈가 1위 대 PO 승자 아님");
            check(!s.Finished && s.InProgress, "KBO: 한국시리즈 전에 잠금 풀림");
            for (int k = 0; k < 6; k++) Decide(s, 3, k % 2 == 0);
            var ks = SeriesGames(s, 3);
            check(s.Winner(3) < 0 && ks.Count == 7 && ks[3].Home == 2 && ks[4].Home == 0 && ks[5].Home == 0 && ks[6].Home == 0, "KBO: 한국시리즈 3승 3패 뒤 7차전 · 홈 배분(5 · 6 · 7차전 상위) 아님");
            Decide(s, 3, false);
            check(s.Finished && !s.InProgress && s.Champion(out var t) && t.Champion == 2 && t.Runner == 0 && SeriesGames(s, 3).Count == 7, "KBO: 우승이 안 정해짐 · 잠금 안 풀림");

            s = Seeded(Rule.KBO);
            Advance(s);
            Decide(s, 0, false);
            var wc = SeriesGames(s, 0);
            check(s.Winner(0) < 0 && wc.Count == 2 && wc[1].Home == 3 && wc[1].Day == wc[0].Day.AddDays(1), "KBO: 5위가 1차전 이겼는데 2차전(4위 홈 · 다음 날) 없음");
            Decide(s, 0, false);
            check(s.Winner(0) == 4 && SeriesGames(s, 0).Count == 2 && s.Post[1].Low == 4, "KBO: 와일드카드 최대 2경기 · 5위 진출 아님");

            s = Seeded(Rule.NPB);
            Advance(s);
            check(s.Post.Count == 2 && s.Post[0].High == 1 && s.Post[0].Low == 2 && s.Post[1].High == 7 && s.Post[1].Low == 8 && s.Post[1].League == 1, "NPB: 퍼스트가 리그마다 2위 대 3위 아님");
            Decide(s, 0, false);
            Decide(s, 0, true);
            Decide(s, 0, false);
            check(s.Winner(0) == 2 && SeriesGames(s, 0).TrueForAll(x => x.Home == 1) && SeriesGames(s, 0).Count == 3, "NPB: 퍼스트 3전 2선승 · 2위 전 경기 홈 아님");
            check(s.Post.Count == 3 && s.Post[2].Round == 1 && s.Post[2].High == 0 && s.Post[2].Low == 2 && s.Wins(2, 0) == 1, "NPB: 파이널 1위 1승 안고 대 퍼스트 승자 아님");
            for (int k = 0; k < 3; k++) Decide(s, 2, true);
            check(s.Winner(2) == 0 && SeriesGames(s, 2).Count == 3 && SeriesGames(s, 2).TrueForAll(x => x.Home == 0), "NPB: 파이널 1위 3승(+1)에 안 끝남 · 1위 전 경기 홈 아님");
            check(s.Post.Count == 3, "NPB: 퍼시픽이 안 끝났는데 일본시리즈");
            Decide(s, 1, true);
            Decide(s, 1, true);
            for (int k = 0; k < 6; k++) Decide(s, 3, k >= 3);
            check(s.Post[3].High == 6 && s.Post[3].Low == 7 && s.Winner(3) == 6 && SeriesGames(s, 3).Count == 6, "NPB: 파이널 최대 6경기(0승 3패 뒤 3연승) 아님");
            check(s.Post.Count == 5 && s.Post[4].Round == 2 && s.Post[4].High == 0 && s.Post[4].Low == 6 && SeriesGames(s, 4)[0].Day == s.LastDay(3).AddDays(2), "NPB: 일본시리즈 센트럴 대 퍼시픽 · 하루 쉼 아님");
            for (int k = 0; k < 4; k++) Decide(s, 4, true);
            check(s.Finished && !s.InProgress && SeriesGames(s, 4).Count == 4, "NPB: 일본시리즈 4선승에 안 끝남");

            var old = Current;
            var oldDay = lastDay;
            Current = s;
            var next = NewSeason(Rule.KBO, Naming.Korean, s.LastDay(4).AddDays(1));
            Current = old;
            lastDay = oldDay;
            check(next.History.Count == 1 && next.History[0].Champion == 0 && next.History[0].Runner == 6 && !next.InProgress, "새 시즌: 우승 이력이 안 이어짐");
        }

        /// <summary>올림픽 휴식 × 포스트시즌: 새로 잡는 경기 · 이미 잡힌 경기 모두 휴식 다음 날로, 다음 경기는 그 뒤 날. 휴식이 없으면 그대로.</summary>
        static void BreakCheck(Action<bool, string> check)
        {
            DateTime f0 = BreakFrom, t0 = BreakTo;
            var s = Seeded(Rule.KBO);
            BreakFrom = s.Start.AddDays(1);
            BreakTo = s.Start.AddDays(5);
            Advance(s);
            var g = SeriesGames(s, 0)[0];
            check(g.Day == BreakTo.AddDays(1) && g.Minute == Rules.StartMinute(Rule.KBO, g.Day.DayOfWeek), "올림픽: 포스트시즌 첫 경기가 휴식 다음 날 · 그날 시각 아님");
            s = Seeded(Rule.KBO);
            BreakTo = default;
            Advance(s);
            check(!Break(s) && SeriesGames(s, 0)[0].Day == s.Start.AddDays(1), "올림픽: 휴식이 없는데 경기를 옮김");
            BreakTo = s.Start.AddDays(5);
            check(Break(s) && SeriesGames(s, 0)[0].Day == BreakTo.AddDays(1), "올림픽: 이미 잡힌 포스트시즌 경기가 휴식에 남음");
            Decide(s, 0, false);
            check(SeriesGames(s, 0)[1].Day == BreakTo.AddDays(2), "올림픽: 휴식 뒤 다음 경기가 이어지지 않음");
            BreakFrom = f0;
            BreakTo = t0;
        }

        /// <summary>명단: 팀마다 타자 13 · 투수 12 · 감독, 시즌 안 이름 중복 없음 · 금지 이름 없음, 같은 시드 같은 명단, 능력이 전력 순서를 따름, 새 시즌에 이름 유지 · 이름 방식 바꾸면 새로.</summary>
        static void RosterCheck(Action<bool, string> check)
        {
            var old = Current;
            var oldDay = lastDay;
            var s = NewSeason(Rule.NPB, Naming.Korean, new DateTime(2026, 3, 20));
            var names = new HashSet<string>();
            bool shape = true;
            int total = 0;
            for (int t = 0; t < Rules.TeamCount(s.Rule); t++)
            {
                var r = s.RosterOf(t);
                shape &= r.P.Length == Roster.Batters + Roster.Pitchers && Roster.Batters == 13 && Roster.Pitchers == 12 && r.Manager.Length > 0;
                names.Add(r.Manager);
                total++;
                foreach (var p in r.P)
                {
                    names.Add(p.Name);
                    total++;
                }
            }
            check(shape, "명단: 타자 13 · 투수 12 · 감독 아님");
            check(names.Count == total, "명단: 시즌 안에 같은 이름");
            check(!names.Contains("이승엽") && !names.Contains("류현진"), "명단: 금지 이름이 나옴");
            var again = Roster.Make(s.Naming, s.Seed, Rules.TeamCount(s.Rule), s.Power);
            check(again[4].P[7].Name == s.RosterOf(4).P[7].Name && again[4].P[7].A == s.RosterOf(4).P[7].A, "명단: 같은 시드인데 명단이 다름");
            int strong = 0, weak = 0;
            for (int t = 1; t < Rules.TeamCount(s.Rule); t++)
            {
                if (s.Power(t) > s.Power(strong)) strong = t;
                if (s.Power(t) < s.Power(weak)) weak = t;
            }
            Func<Player, float> all = p => p.Af + p.Bf + p.Cf;
            check(s.RosterOf(strong).Mean(0, Roster.Size, all) > s.RosterOf(weak).Mean(0, Roster.Size, all), "명단: 가장 센 팀 능력이 가장 약한 팀보다 낮음");
            string keep = s.RosterOf(2).P[3].Name;
            var next = NewSeason(Rule.NPB, Naming.Korean, s.Start.AddDays(300));
            check(next.RosterOf(2).P[3].Name == keep && next.RosterOf(2).P[3].A != 0, "명단: 새 시즌에 이름이 안 남음");
            SetNaming(Naming.Japanese);
            check(next.RosterOf(2).P[3].Name != keep && next.RosterOf(2).P[3].Name.Contains(" "), "명단: 이름 방식을 바꿨는데 이름이 그대로");
            var club = next.Club.Clone();
            club.Power = 2;
            SetClub(club);
            var strongStart = NewSeason(Rule.KBO, Naming.Japanese, next.Start.AddDays(300));
            check(strongStart.Power0 == 2 && strongStart.RosterOf(0).P[0].Name != "" && strongStart.Club.Power == 2, "명단: 시작 전력이 새 시즌에 안 들어감");
            // 팀 색: KBO 에서 고른 빈 색(10번)은 KBO 새 시즌에 남고, NPB 로 바꾸면 겹치니 기본 색으로
            club = strongStart.Club.Clone();
            club.Color = 10;
            SetClub(club);
            check(NewSeason(Rule.KBO, Naming.Japanese, strongStart.Start.AddDays(300)).Club.Color == 10, "팀 색: 같은 규칙 새 시즌에 고른 색이 바뀜");
            var npb = NewSeason(Rule.NPB, Naming.Japanese, strongStart.Start.AddDays(600));
            check(npb.Club.Color == -1 && club.Color == 10, "팀 색: NPB 로 바꿨는데 다른 팀 색과 겹침 · 옛 편집값을 고침");
            check(Math.Abs(strongStart.Power(0) - Math.Min(Season.PowerMin + Season.PowerSpan, new Season { Seed = strongStart.Seed }.Power(0) + Season.PowerStep)) < 0.0001f, "명단: 강한 시작 전력이 한 단계 더해지지 않음");
            Current = old;
            lastDay = oldDay;
        }

        /// <summary>
        /// 시즌 하나를 Tick 으로 끝까지: 잠금 · 우승 · 한 팀 하루 2경기 이하 · 월요일 없음 · 더블헤더 시각 · 포스트시즌 무승부 없음 · 시리즈 선승 · 홈 · 날짜. 더블헤더 경기 수를 돌려줌.
        /// olympic(개막일)이 있으면 그 7일 전(입촌)부터 폐회일까지 휴식을 넣고(입촌 날 알게 됨), 휴식에 경기 없음 · 정규 경기 수 그대로를 더 봄.
        /// </summary>
        static int RunCheck(Rule rule, int seed, Action<bool, string> check, DateTime olympic = default)
        {
            string tag = $"{rule} 시드 {seed} 시즌{(olympic != default ? " · 올림픽 " + olympic.ToString("M월 d일") : "")}: ";
            var start = new DateTime(2026, 3, 24);
            var s = new Season { Rule = rule, Start = start, Seed = seed, Games = Schedule.Make(rule, start, seed) };
            var old = Current;
            var oldDay = lastDay;
            Current = s;
            lastDay = default;
            bool locked = false;
            DateTime f0 = BreakFrom, t0 = BreakTo, from = olympic == default ? default : olympic.AddDays(-7), to = olympic.AddDays(16);
            int regular = s.Games.Count;
            for (var d = start; d < start.AddDays(400) && !s.Finished; d = d.AddDays(1))
            {
                if (olympic != default && d == from)
                {
                    BreakFrom = from;
                    BreakTo = to;
                }
                if (olympic != default && d == to.AddDays(1)) BreakTo = default;
                Tick(d);
                if (d == start.AddDays(30)) locked = s.InProgress;
            }
            Current = old;
            lastDay = oldDay;
            BreakFrom = f0;
            BreakTo = t0;
            if (olympic != default)
                check(s.Games.FindAll(g => g.Series == 0).Count == regular && !s.Games.Exists(g => g.Day >= from && g.Day <= to), tag + "휴식에 경기가 남음 · 정규 경기 수가 바뀜");
            check(locked, tag + "시즌 중에 안 잠김");
            check(s.Finished && !s.InProgress && s.Titles().Count == 1, tag + "포스트시즌이 안 끝남 · 잠금 안 풀림");
            int n = Rules.TeamCount(rule), doubles = 0;
            var perDay = new Dictionary<long, int>();
            bool over = false, time = false, tie = false, monday = false;
            DateTime end = default;
            foreach (var g in s.Games)
            {
                long dn = g.Day.Ticks / TimeSpan.TicksPerDay * n;
                foreach (long key in new[] { dn + g.Home, dn + g.Away })
                {
                    perDay.TryGetValue(key, out int c);
                    perDay[key] = ++c;
                    over |= c > 2;
                }
                if (g.Double)
                {
                    doubles++;
                    time |= g.Minute != Rules.DoubleFirstMinute && g.Minute != Rules.DoubleSecondMinute;
                }
                tie |= g.Series != 0 && g.HomeRuns == g.AwayRuns;
                monday |= g.Series == 0 && g.Day.DayOfWeek == DayOfWeek.Monday;
                if (g.Series == 0 && g.Day > end) end = g.Day;
            }
            check(!over, tag + "한 팀 하루 3경기 이상");
            check(!monday, tag + "정규 시즌 월요일 경기");
            // 박스 ↔ 선수 기록 합(정규 시즌 · 점수 맞춤 경기 빼고 — 이 검사엔 이벤트가 없어 맞춤 경기 없음)
            int boxRuns = 0, boxHits = 0, boxWalks = 0, decided = 0, statRuns = 0, statHits = 0, statWalks = 0, wins = 0, losses = 0, ra = 0, outs = 0, need = 0;
            bool boxes = true;
            foreach (var g in s.Games)
            {
                boxes &= g.Box != null && g.Box.Runs(1) == g.HomeRuns && g.Box.Runs(0) == g.AwayRuns;
                if (g.Series != 0 || g.Box == null) continue;
                boxRuns += g.HomeRuns + g.AwayRuns;
                boxHits += g.Box.H[0] + g.Box.H[1];
                boxWalks += g.Box.B[0] + g.Box.B[1];
                if (g.HomeRuns != g.AwayRuns) decided++;
                need += (g.Box.Inn[0].Length + g.Box.Inn[1].Length) * 3;
            }
            foreach (var team in s.Stats)
                foreach (var l in team)
                {
                    statRuns += l[Line.R];
                    statHits += l[Line.H];
                    statWalks += l[Line.BB];
                    wins += l[Line.W];
                    losses += l[Line.L];
                    ra += l[Line.RA];
                    outs += l[Line.Outs];
                }
            check(boxes, tag + "끝난 경기에 박스가 없거나 점수가 다름");
            check(statRuns == boxRuns && ra == boxRuns && statHits == boxHits && statWalks == boxWalks, tag + $"선수 기록 합이 박스와 다름 득점 {statRuns} · 실점 {ra} / {boxRuns} · 안타 {statHits} / {boxHits}");
            check(wins == decided && losses == decided, tag + $"승 {wins} · 패 {losses} 투수 수가 승부 난 경기 {decided} 와 다름");
            check(outs <= need && outs >= need - decided * 3, tag + "아웃 합이 이닝 수와 안 맞음");
            check(Mvp(s, out int mt, out int ms) && s.Titles()[0].Mvp == s.RosterOf(mt).P[ms].Name && s.Titles()[0].W > 0, tag + "MVP · 우승 팀 정규 승패가 안 정해짐");
            SaveFormat.SizeCheck(s, tag, check);
            check(!time, tag + "더블헤더 시각 어긋남");
            check(!tie, tag + "포스트시즌 무승부");
            check(rule == Rule.KBO || doubles == 0, tag + "NPB 에 더블헤더");
            if (rule == Rule.KBO) check(s.Post.Count > 0 && s.Post[0].High == Standings(s, 0)[3].Team && s.Post[0].Low == Standings(s, 0)[4].Team, tag + "와일드카드가 4위 대 5위 아님");
            for (int i = 0; i < s.Post.Count; i++)
            {
                var x = s.Post[i];
                var r = Rules.RoundOf(rule, x.Round);
                var games = SeriesGames(s, i);
                int w = s.Winner(i), lose = w == x.High ? x.Low : x.High;
                check(w >= 0 && s.Wins(i, w) == r.Need && s.Wins(i, lose) < r.Need && games.Count <= r.MaxGames, tag + $"{r.Name} 선승 · 경기 수 어긋남");
                bool order = x.Round > 0 || games[0].Day == end.AddDays(1);
                for (int k = 0; k < games.Count; k++)
                    order &= games[k].Day == games[0].Day.AddDays(k) && games[k].Home == (Rules.HighHome(r, k) ? x.High : x.Low);
                check(order, tag + $"{r.Name} 날짜 · 홈 배분 어긋남");
            }
            return doubles;
        }
    }
}
