using System;
using System.Collections.Generic;
using BaseballLeague.Data;
using CSCommonUX;
using UnityEngine;
using D = CSCommonUX.CSCommonDesign;
using S = CSCommonUX.Section;

namespace BaseballLeague.UI
{
    /// <summary>
    /// 리그 창(시안 '[CSCommonUX 3.0] 프로야구 리그 모드'): 머리줄(WindowHead) · 우물 몸(판을 위에서 아래로 · ScrollArea, 툴바 높이만큼 안쪽 아래 여백) · 하단 알약 툴바 홈 · 일정 · 팀 · 기록 + 설정 FAB.
    /// 상세 화면 홈구장(홈 › 빠른 실행) · 홈팀 설정(팀 › 내 팀 카드) · 경기 상세(일정 › 오늘 리그 경기 · 진행 중만)는 툴바 왼쪽 뒤로 FAB 로 돌아가고, 다른 탭을 누르면 닫힘. 탭마다 그리기는 LeagueWindow.*.cs.
    /// </summary>
    internal sealed partial class LeagueWindow
    {
        const int TabHome = 0, TabSchedule = 1, TabTeams = 2, TabRecords = 3, TabSettings = 4;

        enum Page
        {
            None,
            Venue,
            HomeTeam,
            Live,
        }

        static readonly string[] Names = { "홈", "일정", "팀", "기록", "설정" };

        // 탭 아이콘(시안 툴바): 집 · 달력 · 사람 · 트로피 · 설정 슬라이더
        static readonly string[] TabIcons =
        {
            "M3 10.5 12 4l9 6.5V20H3zM9 20v-6h6v6",
            "M5 4.5h14a2 2 0 0 1 2 2v13a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-13a2 2 0 0 1 2-2zM3 10h18M8 2.5v4M16 2.5v4",
            "M9 11a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM3 20v-1a4 4 0 0 1 4-4h4a4 4 0 0 1 4 4v1M17 8a3 3 0 0 1 0 6",
            "M8 21h8M12 17v4M7 4h10v5a5 5 0 0 1-10 0zM17 6h3v2a3 3 0 0 1-3 3M7 6H4v2a3 3 0 0 0 3 3",
            "M5 7h9M18 7h1M5 17h1M10 17h9M14 5v4M8 15v4",
        };

        // 판 제목 칩 아이콘(시안)
        const string IconStadium = "M3 9c0-2 4-4 9-4s9 2 9 4v6c0 2-4 4-9 4s-9-2-9-4zM3 9c0 2 4 4 9 4s9-2 9-4";
        const string IconBolt = "M13 3L5 13h6l-1 8 8-10h-6z";
        const string IconClock = "M12 8v4l3 2M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18z";
        const string IconBall = "M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18zM7 5.5c2 2 2 11 0 13M17 5.5c-2 2-2 11 0 13";
        const string IconList = "M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01";
        const string IconWeek = "M5 4.5h14a2 2 0 0 1 2 2v13a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-13a2 2 0 0 1 2-2zM3 10h18";
        const string IconChart = "M4 19h16M7 15l3-4 3 2 4-6";
        const string IconFlag = "M6 21V4M6 5h11l-2 4 2 4H6";
        const string IconPin = "M12 21s-6.5-5.8-6.5-11.2a6.5 6.5 0 0 1 13 0C18.5 15.2 12 21 12 21zM12 12.3a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5z";
        const string IconPulse = "M2.5 12h4l2.5-6 4.5 12 2.5-6h5.5";
        const string IconLock = "M6 11h12v10H6zM8.5 11V7.5a3.5 3.5 0 0 1 7 0V11";
        const string IconEye = "M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12zM12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6z";

        public bool Visible { get; private set; }
        public bool Confirming { get; private set; }

        int tab;
        Page page;
        float confirmAt;

        /// <summary>확인 상자가 묻는 홈구장 후보(null = 새 시즌 확인).</summary>
        Stadium.Venue confirmVenue;

        readonly Func<int, string> tabIcon = i => TabIcons[i], tabName = i => Names[i];

        const string ConfirmTitle = "새 시즌을 시작할까요?";
        const string ConfirmText = "지금 시즌의 일정과 결과가 모두 지워지고, 다음 화요일에 개막하는 새 일정을 짜요. 되돌릴 수 없어요.";
        const string BodyId = "baseball.body";

        static DateTime Now => SimulationManager.instance.m_currentGameTime;
        static DateTime Today => Now.Date;

        /// <summary>홈구장 모습을 자주 새로 읽을 화면(홈구장 · 홈팀 설정 · 설정 상태 진단)이 열려 있음.</summary>
        public bool WantsStadium => Visible && (page == Page.Venue || page == Page.HomeTeam || tab == TabSettings);

        public void Toggle()
        {
            if (Visible) Close();
            else Visible = true;
        }

        /// <summary>창을 열고 그 탭으로(알림 카드 단추).</summary>
        public void Show(int to)
        {
            Visible = true;
            Go(to);
        }

        public const int ShowHome = TabHome, ShowSchedule = TabSchedule, ShowRecords = TabRecords;

        public void Close()
        {
            Visible = false;
            Confirming = false;
        }

        public Rect Rect
        {
            get
            {
                float w = Mathf.Min(D.WindowW, Draw.Width - Modal.Margin * 2f);
                float h = Mathf.Min(D.WindowH, Draw.Height - Modal.Margin * 2f);
                return new Rect(Mathf.Round((Draw.Width - w) * 0.5f), Mathf.Round((Draw.Height - h) * 0.5f), w, h);
            }
        }

        void Go(int to, Page p = Page.None)
        {
            if (to != tab || p != page) ScrollArea.Reset(BodyId);
            tab = to;
            page = p;
        }

        public void OnGUI()
        {
            if (!Visible) return;
            var r = Rect;
            var e = Event.current;
            // 확인 상자: 입력은 먼저 받고 남은 마우스 · 휠 · 키를 써 버림(아래 창이 못 받게), 그림은 맨 위
            if (Confirming && e.type != EventType.Repaint)
            {
                DrawConfirm(r);
                if (Confirming && (e.isMouse || e.type == EventType.ScrollWheel || e.isKey) && e.type != EventType.Layout) e.Use();
            }
            Panel.Frame(r, D.RadiusWindow);
            var s = League.Current;
            bool live = page == Page.Live && s != null;
            if (WindowHead.In(r, null, live ? LiveTitle(s) : HeadTitle(), Mod.Version, live ? LiveSub(s) : HeadSub(s), tab == TabSettings ? "" : DateText(Now), tab == TabSettings ? "" : live ? LiveStatus(s) : StatusText(s), live ? Pal.Warn : Pal.Primary, "닫기(Esc)")) Close();
            DrawBody(r, s);
            DrawBar(r);
            if (Confirming && e.type == EventType.Repaint) DrawConfirm(r);
            if (r.Contains(Draw.Mouse) && (e.type == EventType.MouseDown || e.type == EventType.ScrollWheel)) e.Use();
        }

        // ---- 자기 퀵 버튼(허브가 없거나 '모드 허브 연결'을 껐을 때 · 올림픽 · 기상청 퀵 버튼과 같은 방식: 왼쪽 둥근 버튼 · 끌어 옮김 · 자리는 설정 파일에)

        /// <summary>자기 퀵 버튼을 그리고 막음: 허브가 맡지 않았거나(허브 없음) 연결을 껐을 때.</summary>
        public static bool OwnLauncher => !YukiModBoxEntry.HubOwnsLauncher || !BaseballSetting.Current.HubLinked;

        /// <summary>기본 자리 화면 높이 비율(올림픽 0.53 · 기상청 버튼 아래에 겹치지 않게).</summary>
        const float LauncherTop = 0.62f;

        bool fabPressed, fabDragging;
        Vector2 fabOffset, fabPressAt;

        public Rect LauncherRect
        {
            get
            {
                float d = D.FabSize;
                var s = BaseballSetting.Current;
                float x = s.LauncherX >= 0f ? s.LauncherX : D.FabMargin;
                float y = s.LauncherY >= 0f ? s.LauncherY : Draw.Height * LauncherTop;
                return new Rect(Mathf.Clamp(x, 0f, Mathf.Max(0f, Draw.Width - d)), Mathf.Clamp(y, 0f, Mathf.Max(0f, Draw.Height - d)), d, d);
            }
        }

        /// <summary>퀵 버튼: 누르면 창 열기 · 닫기, 끌면 자리를 옮겨 설정 파일에 남김.</summary>
        public void LauncherGui()
        {
            var r = LauncherRect;
            var e = Event.current;
            var m = Draw.Mouse;
            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(m))
            {
                fabPressed = true;
                fabDragging = false;
                fabPressAt = m;
                fabOffset = m - r.position;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && fabPressed)
            {
                if (!fabDragging && (m - fabPressAt).sqrMagnitude > D.Gap * D.Gap) fabDragging = true;
                if (fabDragging)
                {
                    BaseballSetting.Current.LauncherX = m.x - fabOffset.x;
                    BaseballSetting.Current.LauncherY = m.y - fabOffset.y;
                    e.Use();
                }
            }
            else if (e.rawType == EventType.MouseUp && fabPressed)
            {
                fabPressed = false;
                if (fabDragging)
                {
                    fabDragging = false;
                    BaseballSetting.Save();
                }
                else if (r.Contains(m)) Toggle();
                e.Use();
            }
            if (Draw.Repaint) Fab.In(r, YukiModBoxEntry.Icon, FabTone.Plain, Visible);
            if (!fabDragging && Draw.Hover(r)) Tooltip.Draw(r, "프로야구 리그 · 끌어서 옮기기");
        }

        // ---- 머리줄: 제목(상세 화면이면 그 이름) · 부제 · 날짜 · 상태 알약

        string HeadTitle()
        {
            if (page == Page.Venue) return Stadium.Name.Length > 0 ? "홈구장 · " + Stadium.Name : "홈구장";
            if (page == Page.HomeTeam) return "홈팀 설정";
            return tab == TabSettings ? "프로야구 리그 · 설정" : "프로야구 리그";
        }

        string HeadSub(Season s)
        {
            if (tab == TabSettings) return "바꾼 값은 바로 저장돼요";
            if (s == null) return "";
            if (SaveFormat.Unread != null) return BaseballSave.UnreadText + " · 설정에서 새 시즌을 시작하면 저장돼요";
            var v = ViewOf(s);
            string city = CityName();
            return (city.Length > 0 ? city + " · " : "") + $"{s.Start.Year} 시즌 · {v.MyPlayed} / {v.MyGames}경기 끝남";
        }

        string StatusText(Season s)
        {
            if (s == null) return "";
            var v = ViewOf(s);
            int minute = (int)Now.TimeOfDay.TotalMinutes;
            if (v.Status != null && v.StatusMinute == minute) return v.Status;
            v.StatusMinute = minute;
            var g = v.Next;
            if (s.Finished) v.Status = "시즌 종료";
            else if (League.OnBreak(Today)) v.Status = BreakText;
            else if (g != null && g.Day == Today)
            {
                int left = g.Minute - minute;
                v.Status = left > 0 ? $"경기 날 · 플레이볼 {left / 60}:{left % 60:00} 전" : "경기 날";
            }
            else v.Status = s.Post.Count > 0 ? "포스트시즌" : s.Played > 0 ? "정규 시즌" : "개막 전";
            return v.Status;
        }

        /// <summary>올림픽 휴식 한 줄(머리줄 상태 · 일정 탭 오늘 경기).</summary>
        static string BreakText => $"올림픽 휴식 · {League.BreakTo.Month}월 {League.BreakTo.Day}일까지";

        static string CityName()
        {
            try
            {
                return SimulationManager.instance.m_metaData?.m_CityName ?? "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        static string DateText(DateTime d) => $"{d.Year}년 {d.Month}월 {d.Day}일 {d.Hour:00}:{d.Minute:00}";

        // ---- 하단 툴바 · 뒤로 FAB(상세 화면)

        void DrawBar(Rect r)
        {
            int hit = Toolbar.Bar(r, TabSettings, tab == TabSettings ? -1 : tab, tabIcon, tabName, TabIcons[TabSettings], Names[TabSettings], tab == TabSettings, out bool fab);
            if (fab) hit = TabSettings;
            if (page != Page.None && Toolbar.BackFab(r, TabSettings, Names[tab], page == Page.Venue ? "홈으로" : page == Page.Live ? "일정으로" : "팀으로")) Go(tab);
            if (hit >= 0 && (hit != tab || page != Page.None)) Go(hit);
        }

        // ---- 몸: 우물 안 판을 위에서 아래로(창 바닥까지 · 툴바 높이는 스크롤 안쪽 아래 여백)

        float contentH;

        void DrawBody(Rect r, Season s)
        {
            var view = new Rect(r.x, r.y + WindowHead.H, r.width, r.height - WindowHead.H);
            if (Draw.Repaint) Draw.RoundBottom(view, S.WellFill, D.RadiusWindow);
            const float pad = S.WellPad;
            var inner = new Rect(view.x + pad, view.y + pad, view.width - pad * 2f, view.height - pad);
            ScrollArea.In(inner, BodyId, contentH + Toolbar.SafeH, !Confirming, _ => contentH = DrawPage(s, inner.width));
        }

        float DrawPage(Season s, float w)
        {
            if (s == null)
            {
                Line(Plate(new Rect(0f, 0f, w, PlateH(S.RowH)), Names[tab], null), "도시를 불러오면 시즌이 열려요.", Look.PillSub);
                return PlateH(S.RowH);
            }
            var v = ViewOf(s);
            if (page == Page.Venue) return VenuePage(s, v, w);
            if (page == Page.HomeTeam) return HomeTeamPage(s, v, w);
            if (page == Page.Live) return LivePage(s, v, w);
            switch (tab)
            {
                case TabHome: return HomeTab(s, v, w);
                case TabSchedule: return ScheduleTab(s, v, w);
                case TabTeams: return TeamTab(s, v, w);
                case TabRecords: return RecordsTab(s, v, w);
                default: return SettingsTab(s, v, w);
            }
        }

        // ---- 화면이 같이 쓰는 계산(시즌 · 판번호가 바뀔 때만 다시 셈)

        /// <summary>순위 · 팀 기록 · 내 팀 경기 수. League.Version 이 바뀌면 다시 셈.</summary>
        sealed class View
        {
            public Season Season;
            public int Version;
            public List<Standing>[] Tables;
            public TeamRecord[] Records;
            /// <summary>팀마다 자기 리그 순위(1부터) · 1위와 게임 차 · 전력 순위(1부터).</summary>
            public int[] Rank, PowerRank;
            public float[] Behind;
            /// <summary>팀마다 전력 4칸(타격 · 투수 · 수비 · 홈 이점).</summary>
            public float[][] Parts;
            public int MyGames, MyPlayed;
            /// <summary>만든 날(오늘) · 홈구장 자리를 알았나(관중 단위) — 바뀌면 다시 셈.</summary>
            public DateTime Day;
            public bool StadiumUnit;
            /// <summary>내 팀 다음 경기 · 정규 시즌 홈 경기(날짜 · 시각 차례) · 남은 홈 경기 · 오늘 홈 경기 번호(시각 차례) · 오늘 경기 우천 취소[경기 번호].</summary>
            public Game Next;
            public List<Game> Homes, NextHomes, HomeDone;
            public List<int> TodayHome = new();
            public bool[] Rain;
            /// <summary>홈구장 관중 집계(처음 쓸 때).</summary>
            public League.CrowdSum Crowds;
            /// <summary>머리줄 상태 글(게임 분이 바뀔 때만 다시 지음).</summary>
            public string Status;
            public int StatusMinute = -1;
        }

        View view;

        View ViewOf(Season s)
        {
            var today = Today;
            bool unit = League.HomeSeats > 0;
            if (view != null && view.Season == s && view.Version == League.Version && view.Day == today && view.StadiumUnit == unit) return view;
            int n = Rules.TeamCount(s.Rule);
            var v = new View { Season = s, Version = League.Version, Day = today, StadiumUnit = unit, Tables = new List<Standing>[Rules.LeagueCount(s.Rule)], Records = new TeamRecord[n], Rank = new int[n], PowerRank = new int[n], Behind = new float[n], Parts = new float[n][] };
            var errors = new int[n];
            foreach (var g in s.Games)
                if (g.Series == 0 && g.Played && g.Box != null)
                {
                    errors[g.Away] += g.Box.E[0];
                    errors[g.Home] += g.Box.E[1];
                }
            for (int lg = 0; lg < v.Tables.Length; lg++)
            {
                v.Tables[lg] = League.Standings(s, lg);
                for (int i = 0; i < v.Tables[lg].Count; i++)
                {
                    v.Rank[v.Tables[lg][i].Team] = i + 1;
                    v.Behind[v.Tables[lg][i].Team] = v.Tables[lg][i].GamesBehind;
                }
            }
            for (int t = 0; t < n; t++)
            {
                v.Records[t] = League.Record(s, t);
                int above = 0;
                for (int o = 0; o < n; o++)
                    if (s.Power(o) > s.Power(t)) above++;
                v.PowerRank[t] = above + 1;
                v.Parts[t] = Parts(s, t, v.Records[t], errors[t]);
            }
            foreach (var g in s.Games)
                if (g.Series == 0 && (g.Home == 0 || g.Away == 0))
                {
                    v.MyGames++;
                    if (g.Played) v.MyPlayed++;
                }
            v.Next = NextGame(s, today);
            v.Homes = Sorted(s, g => g.Home == 0 && g.Series == 0);
            v.NextHomes = Sorted(s, g => !g.Played && g.Home == 0);
            v.Rain = new bool[s.Games.Count];
            for (int i = 0; i < s.Games.Count; i++)
            {
                var g = s.Games[i];
                if (g.Day != today) continue;
                v.Rain[i] = League.Rains(s, g, i);
                if (g.Home == 0) v.TodayHome.Add(i);
            }
            v.TodayHome.Sort((a, b) => s.Games[a].Minute.CompareTo(s.Games[b].Minute));
            return view = v;
        }

        /// <summary>경기 진행 상태(오늘 우천 취소는 View 에 담아 둔 값).</summary>
        Live.State StateOf(Season s, Game g, int index, out float t)
        {
            var v = ViewOf(s);
            return Live.StateOf(g, index >= 0 && index < v.Rain.Length && v.Rain[index], Now, out t);
        }

        /// <summary>내 도시 팀(0번)의 day 와 같거나 뒤의 안 한 가장 이른 경기(없으면 null).</summary>
        static Game NextGame(Season s, DateTime day)
        {
            Game best = null;
            foreach (var g in s.Games)
                if (!g.Played && (g.Home == 0 || g.Away == 0) && g.Day >= day && (best == null || Earlier(g, best))) best = g;
            return best;
        }

        static bool Earlier(Game a, Game b) => a.Day != b.Day ? a.Day < b.Day : a.Minute < b.Minute;

        /// <summary>조건에 맞는 경기를 날짜 · 시각 차례로.</summary>
        static List<Game> Sorted(Season s, Predicate<Game> match)
        {
            var list = s.Games.FindAll(match);
            list.Sort((a, b) => Earlier(a, b) ? -1 : Earlier(b, a) ? 1 : 0);
            return list;
        }

        static bool Mine(Game g) => g.Home == 0 || g.Away == 0;

        static int Opponent(Game g) => g.Home == 0 ? g.Away : g.Home;

        /// <summary>팀 닉네임(이름 뒤 낱말 — 시안 '6월 7일 vs 오터스').</summary>
        static string Short(Season s, int team)
        {
            string n = s.Team(team).Name;
            int sp = n.LastIndexOf(' ');
            return sp < 0 ? n : n.Substring(sp + 1);
        }

        /// <summary>팀 홈 경기 자리: 내 도시 팀 = 홈구장(없으면 도시 이름), 원정 팀 = 연고지.</summary>
        static string Park(Season s, int team)
        {
            if (team != 0) return s.Team(team).City ?? "";
            if (Stadium.Name.Length > 0) return Stadium.Name;
            string city = CityName();
            return city.Length > 0 ? city : s.Team(0).Name;
        }

        static string Clock(short minute) => $"{minute / 60:00}:{minute % 60:00}";

        static string Md(DateTime d) => $"{d.Month}/{d.Day}";

        static string Pct(float p) => p >= 1f ? "1.000" : p.ToString(".000");

        static string WinLoss(int w, int l, int t = 0) => t > 0 ? $"{w}승 {l}패 {t}무" : $"{w}승 {l}패";

        static string Gb(float gb) => gb == 0f ? "-" : gb.ToString("0.0");

        static Color TeamColor(int team) => Pal.BallTeams[League.Current?.ColorOf(team) ?? team];

        /// <summary>라운드 이름(NPB 클라이맥스 시리즈는 리그 이름을 붙임).</summary>
        static string RoundName(Season s, Series x)
        {
            string name = Rules.RoundOf(s.Rule, x.Round).Name;
            return s.Rule == Rule.NPB && x.Round < 2 ? $"{name} · {Rules.LeagueName(s.Rule, x.League)}" : name;
        }

        // ---- 그리기 부품(CSCommon Section · Tile · Stat · Badge 를 시안 배치로)

        /// <summary>판 높이(제목 칩 + 몸 body).</summary>
        static float PlateH(float body) => S.Pad + S.ChipH + S.HeadGap + body + S.PadBottom;

        /// <summary>히어로 큰 제목 + 아래 설명 한 줄 높이(Section.HeroTitle).</summary>
        const float HeroHeadH = S.HeroTitleH + S.HeroSubGap + S.HeroSubH;

        /// <summary>판(hero = 옅은 초록 히어로 면 + 1px 선) · 제목 칩 · 오른쪽 보조 알약. 몸 칸을 돌려줌.</summary>
        static Rect Plate(Rect r, string title, string icon, string sub = null, bool hero = false)
        {
            if (hero) HeroFrame(r);
            else S.Frame(r);
            S.Title(new Rect(r.x + S.Pad, r.y + S.Pad, r.width - S.Pad * 2f, S.ChipH), title, sub, icon);
            float top = r.y + S.Pad + S.ChipH + S.HeadGap;
            return new Rect(r.x + S.Pad, top, r.width - S.Pad * 2f, Mathf.Max(0f, r.yMax - S.PadBottom - top));
        }

        static void HeroFrame(Rect r)
        {
            S.Hero(r);
            if (Draw.Repaint) Draw.RoundOutline(r, Look.HeroLine, S.HeroRadius);
        }

        /// <summary>흰 타일(히어로 위) · 회색 타일(판 위) + 이름 · 값 · 설명(Stat).</summary>
        static void StatTile(Rect r, string label, string value, string sub, bool white, Color? color = null)
        {
            if (Draw.Repaint)
            {
                Draw.Round(r, white ? S.Plate : Look.Pill, Tile.Radius);
                if (white) Draw.RoundOutline(r, S.PlateLine, Tile.Radius);
            }
            Stat.In(new Rect(r.x + Tile.PadX, r.y, r.width - Tile.PadX * 2f, r.height), label, value, sub, color);
        }

        static float TileH(bool sub) => Stat.Height(sub) + D.CardRowPad * 2f;

        /// <summary>같은 폭 칸 n 개를 gap 사이로 나눈 i 번째.</summary>
        static Rect Cell(Rect r, int n, int i, float gap)
        {
            float w = (r.width - gap * (n - 1)) / n;
            return new Rect(r.x + (w + gap) * i, r.y, w, r.height);
        }

        static void Line(Rect r, string text, Color c, bool bold = false, float size = D.FontBody, TextAnchor anchor = TextAnchor.MiddleLeft) => Draw.TextFit(r, text, size, c, bold, anchor);

        static void Note(Rect r, string text) => Draw.Text(r, text, D.FontMin, S.SubInk, false, TextAnchor.UpperLeft, true);

        static float NoteH(string text, float w) => Draw.TextHeight(text, D.FontMin, w);

        /// <summary>팀 색 막대(왼쪽 x · 세로 가운데 cy). 다음 글이 시작할 x 를 돌려줌.</summary>
        static float Mark(float x, float cy, int team, float w = D.TeamMarkW, float h = D.TeamMarkH, float gap = S.RowGap)
        {
            if (Draw.Repaint) Draw.Round(new Rect(x, Mathf.Round(cy - h * 0.5f), w, h), TeamColor(team), w * 0.5f);
            return x + w + gap;
        }

        /// <summary>승 · 패 · 무 알약(승 = 초록 파스텔, 패 · 무 = 회색).</summary>
        static void ResultBadge(Rect r, int result)
        {
            if (!Draw.Repaint) return;
            Draw.Round(r, result > 0 ? Look.PastelFill : result < 0 ? S.PlateLine : Look.Pill, r.height * 0.5f);
            Draw.TextPill(r, result > 0 ? "승" : result < 0 ? "패" : "무", D.FontMin, result > 0 ? Look.PastelInk : Look.BarOff, true, TextAnchor.MiddleCenter);
        }

        /// <summary>홈 · 원정 알약(홈 = 초록 파스텔).</summary>
        static float HomeAway(float right, float cy, bool home)
        {
            string t = home ? "홈" : "원정";
            float bw = Badge.Pair(0f, 0f, t, Look.PastelFill, Look.PastelInk, false);
            Badge.Pair(right - bw, cy, t, home ? Look.PastelFill : Look.Pill, home ? Look.PastelInk : Look.BarOff);
            return bw;
        }

        // ---- 새 시즌 확인 상자

        void AskNewSeason() => Ask(null);

        /// <summary>확인 상자 열기: venue = 홈구장으로 바꿀 경기장(null = 새 시즌).</summary>
        void Ask(Stadium.Venue venue)
        {
            confirmVenue = venue;
            Confirming = true;
            confirmAt = Time.realtimeSinceStartup;
        }

        void DrawConfirm(Rect r)
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                Confirming = false;
                e.Use();
                return;
            }
            float k = Modal.Ease((Time.realtimeSinceStartup - confirmAt) / Modal.OpenSeconds);
            Modal.Scrim(r, k, D.RadiusWindow);
            var venue = confirmVenue;
            string text = venue == null ? ConfirmText : $"다음 홈 경기부터 {venue.Name} 경기 이벤트의 승패를 홈 경기 결과로 써요.";
            int hit = venue == null ? Dialog.In(Dialog.Place(r, text, k), ConfirmTitle, text, "새 시즌 시작", "취소", true, k)
                : Dialog.In(Dialog.Place(r, text, k), "홈구장을 바꿀까요?", text, "홈구장으로", "취소", false, k);
            if (hit == 0) Confirming = false;
            else if (hit == 1 && venue != null)
            {
                Confirming = false;
                Stadium.Pick(venue.Id);
                Toast.Show(venue.Name + "을 홈구장으로 정했어요", ToastKind.Success);
            }
            else if (hit == 1)
            {
                Confirming = false;
                var s = League.Current;
                SaveFormat.Discard(); // 못 읽은 세이브 원본을 버리고 다음 저장부터 새 시즌
                League.NewSeason(s?.Rule ?? Rule.KBO, s?.Naming ?? Naming.Korean, Today);
                Toast.Show("새 시즌 일정을 짰어요", ToastKind.Success);
            }
        }
    }
}
