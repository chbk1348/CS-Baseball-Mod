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
    /// 일정(시안 Schedule): 왼쪽 열 = 내 팀 이달 성적 · 오늘 리그 경기(진행 중인 경기만 눌러 경기 상세로) · 보기 · 이번 주 3연전, 오른쪽 = 월간 달력.
    /// 포스트시즌이 열리면 대진 판을 두 열 아래 전체 폭으로(시안에 자리가 없음).
    /// </summary>
    internal sealed partial class LeagueWindow
    {
        /// <summary>달력이 보여 주는 달(1일). default 면 오늘 달.</summary>
        DateTime calMonth;

        static readonly string[] WeekDays = { "일", "월", "화", "수", "목", "금", "토" };

        float ScheduleTab(Season s, View v, float w)
        {
            if (calMonth == default) calMonth = new DateTime(Today.Year, Today.Month, 1);
            float leftH = ScheduleLeft(s, v, D.SideColW);
            float rightH = Calendar(s, new Rect(D.SideColW + S.Gap, 0f, w - D.SideColW - S.Gap, 0f));
            float y = Mathf.Max(leftH, rightH);
            if (s.Post.Count == 0) return y;
            return Bracket(s, y + S.Gap, w);
        }

        // ---- 왼쪽 열

        float ScheduleLeft(Season s, View v, float w)
        {
            var month = calMonth;
            int mw = 0, ml = 0, rest = 0;
            foreach (var g in s.Games)
                if (Mine(g) && g.Day.Year == month.Year && g.Day.Month == month.Month)
                {
                    int res = League.Result(g, 0);
                    if (res > 0) mw++;
                    else if (res < 0) ml++;
                    if (!g.Played) rest++;
                }
            float tileH = TileH(false);
            float h = PlateH(tileH + S.HeadGap + Button.H);
            var b = Plate(new Rect(0f, 0f, w, h), s.Team(0).Name, TabIcons[TabHome], null, true);
            StatTile(Cell(new Rect(b.x, b.y, b.width, tileH), 2, 0, D.ActionGap), $"{month.Month}월 성적", WinLoss(mw, ml), null, true);
            StatTile(Cell(new Rect(b.x, b.y, b.width, tileH), 2, 1, D.ActionGap), $"남은 {month.Month}월 경기", rest + "경기", null, true);
            Button.At(b.x, b.y + tileH + S.HeadGap + Button.H * 0.5f, null, "오늘로 가기", null, Tone.Primary, null, true, () => calMonth = new DateTime(Today.Year, Today.Month, 1));
            float y = h + S.Gap;

            // 오늘 리그 경기: 눌러 경기 상세(GameLive)
            var today = TodayIndexes(s);
            const string liveNote = "경기를 누르면 경기 상세가 열려요. 달력 칸을 눌러 지난 경기 · 앞으로 경기도 볼 수 있어요";
            float listH = Mathf.Max(1, today.Count) * (S.RowH + D.Gap) - D.Gap;
            float nh = NoteH(liveNote, w - S.Pad * 2f);
            h = PlateH(listH + S.HeadGap + nh);
            b = Plate(new Rect(0f, y, w, h), "오늘 리그 경기", IconBall, today.Count + "경기");
            if (today.Count == 0) Line(new Rect(b.x, b.y, b.width, S.RowH), League.OnBreak(Today) ? BreakText + " · 그동안 경기는 예비일로 옮겼어요" : "오늘은 리그 경기가 없어요", Look.PillSub);
            for (int i = 0; i < today.Count; i++) TodayRow(s, today[i], new Rect(b.x, b.y + (S.RowH + D.Gap) * i, b.width, S.RowH));
            Note(new Rect(b.x, b.y + listH + S.HeadGap, b.width, nh), liveNote);
            y += h + S.Gap;

            // 보기(범례)
            h = PlateH(S.TagH * 4f);
            b = Plate(new Rect(0f, y, w, h), "보기", IconList);
            Legend(new Rect(b.x, b.y, b.width, S.TagH), Look.PastelSubFill, Look.HeroLine, null, "홈 경기");
            Legend(new Rect(b.x, b.y + S.TagH, b.width, S.TagH), S.Plate, S.PlateLine, null, "원정 경기");
            Legend(new Rect(b.x, b.y + S.TagH * 2f, b.width, S.TagH), default, default, 1, "이긴 경기");
            Legend(new Rect(b.x, b.y + S.TagH * 3f, b.width, S.TagH), default, default, -1, "진 경기");
            y += h + S.Gap;

            // 이번 주 3연전: 다음 안 한 경기부터 같은 상대가 이어지는 경기
            var series = new List<Game>();
            foreach (var g in Sorted(s, g => !g.Played && Mine(g)))
            {
                if (series.Count > 0 && Opponent(g) != Opponent(series[0])) break;
                series.Add(g);
            }
            string rule = s.Rule == Rule.KBO
                ? "KBO 방식으로 짜요. 월요일은 쉬고 화~목 · 금~일 3연전, 평일 18:30 · 주말 17:00에 시작해요. 비로 취소되면 예비일이나 더블헤더로 다시 잡아요"
                : "NPB 방식으로 짜요. 월요일은 쉬고 3연전, 평일 18:00 · 주말 14:00에 시작해요. 5 ~ 6월엔 교류전이 있고, 비로 취소되면 예비일로 다시 잡아요";
            nh = NoteH(rule, w - S.Pad * 2f);
            listH = S.RowH * Mathf.Max(1, series.Count);
            h = PlateH(listH + S.HeadGap + nh);
            b = Plate(new Rect(0f, y, w, h), "이번 주 3연전", IconWeek);
            if (series.Count == 0) Line(new Rect(b.x, b.y, b.width, S.RowH), "남은 경기가 없어요", Look.PillSub);
            float dw = Mathf.Ceil(Draw.TextWidth("12/31 18:30", D.FontMin));
            for (int i = 0; i < series.Count; i++)
            {
                var g = series[i];
                var row = new Rect(b.x, b.y + S.RowH * i, b.width, S.RowH);
                float x = row.x + S.RowPadL;
                Line(new Rect(x, row.y, dw, row.height), Md(g.Day) + " " + Clock(g.Minute), Look.PillSub, false, D.FontMin);
                x = Mark(x + dw + S.RowGap, row.center.y, Opponent(g));
                string ha = g.Home == 0 ? "홈" : "원정";
                float hw = Mathf.Ceil(Draw.TextWidth(ha, D.FontMin));
                Line(new Rect(row.xMax - S.RowPadL - hw, row.y, hw, row.height), ha, Look.PillSub, false, D.FontMin, TextAnchor.MiddleRight);
                Line(new Rect(x, row.y, row.xMax - S.RowPadL - hw - S.RowGap - x, row.height), s.Team(Opponent(g)).Name, Look.BarInk, true);
            }
            Note(new Rect(b.x, b.y + listH + S.HeadGap, b.width, nh), rule);
            return y + h;
        }

        /// <summary>오늘 리그 경기 한 줄: 진행 중 = 누르는 칸(주황 '7회말 2 : 3'), 나머지 = 흐린 칸(점수 · 경기 전 · 우천 취소). 원정 vs 홈(닉네임) · 자리 시각.</summary>
        void TodayRow(Season s, int index, Rect row)
        {
            var g = s.Games[index];
            var st = StateOf(s, g, index, out float t);
            bool live = st == Live.State.Live;
            string state;
            if (live)
            {
                var f = Live.At(Live.Of(s, g, index, Today), t);
                state = $"{Half(f.Inning, f.Bottom)} {f.Away} : {f.Home}";
            }
            else if (g.Played) state = $"{g.AwayRuns} : {g.HomeRuns}";
            else if (st == Live.State.Over)
            {
                var f = Live.At(Live.Of(s, g, index, Today), 1f);
                state = $"경기 끝 {f.Away} : {f.Home}";
            }
            else state = st == Live.State.Rain ? "우천 취소" : "경기 전";
            if (Tile.In(row, false, true, "경기 상세 보기")) OpenLive(index);
            float sw = Badge.Pair(0f, 0f, state, Look.Pill, Look.BarOff, false);
            Badge.Pair(row.xMax - S.RowPadR - sw, row.center.y, state, live ? Look.WarnFill : Look.Pill, live ? Look.WarnInk : Look.BarOff);
            float x = row.x + D.CardRowPad, right = row.xMax - S.RowPadR - sw - S.RowGap;
            float top = Mathf.Round(row.center.y - (D.LineBody + D.LineSub) * 0.5f), cy = top + D.LineBody * 0.5f;
            string a = Short(s, g.Away), h = Short(s, g.Home);
            x = Mark(x, cy, g.Away, D.TeamMarkW, D.TeamMarkSmallH, D.IconGap);
            float aw = Mathf.Ceil(Draw.TextWidth(a, D.FontDesc, true));
            Line(new Rect(x, top, aw, D.LineBody), a, Look.BarInk, true, D.FontDesc);
            x += aw + D.Gap;
            float vw = Mathf.Ceil(Draw.TextWidth("vs", D.FontMin));
            Line(new Rect(x, top, vw, D.LineBody), "vs", Look.PillSub, false, D.FontMin);
            x = Mark(x + vw + D.Gap, cy, g.Home, D.TeamMarkW, D.TeamMarkSmallH, D.IconGap);
            Line(new Rect(x, top, Mathf.Max(0f, right - x), D.LineBody), h, Look.BarInk, true, D.FontDesc);
            string tag = g.Series != 0 ? " · " + RoundName(s, s.Post[g.Series - 1]) : g.Double ? " · 더블헤더" : g.Moved ? " · 예비일" : g.Inter ? " · 교류전" : "";
            Line(new Rect(row.x + D.CardRowPad, top + D.LineBody, right - row.x - D.CardRowPad, D.LineSub), Park(s, g.Home) + " " + Clock(g.Minute) + tag, Look.PillSub, false, D.FontMin);
        }

        /// <summary>범례 한 줄: 견본 칸(fill · line) 또는 승패 알약 + 글.</summary>
        static void Legend(Rect r, Color fill, Color line, int? result, string text)
        {
            float x = r.x + S.RowPadL, cy = r.center.y;
            if (result.HasValue)
            {
                ResultBadge(new Rect(x, Mathf.Round(cy - Badge.CountH * 0.5f), Badge.H, Badge.CountH), result.Value);
                x += Badge.H;
            }
            else
            {
                var sw = new Rect(x, Mathf.Round(cy - D.Icon * 0.5f), D.Icon, D.Icon);
                if (Draw.Repaint)
                {
                    Draw.Round(sw, fill, D.Gap);
                    Draw.RoundOutline(sw, line, D.Gap);
                }
                x += sw.width;
            }
            Line(new Rect(x + D.ActionGap, r.y, r.xMax - x - D.ActionGap, r.height), text, Look.BarOff, false, D.FontDesc);
        }

        // ---- 월간 달력

        float Calendar(Season s, Rect area)
        {
            var month = calMonth;
            int lead = (int)month.DayOfWeek, days = DateTime.DaysInMonth(month.Year, month.Month);
            int weeks = (lead + days + 6) / 7;
            float gridH = weeks * (D.CalendarCellH + D.CalendarGap) - D.CalendarGap;
            float h = S.Pad + D.CalendarHeadH + S.Gap + D.CalendarWeekH + S.Gap + gridH + S.Pad;
            var r = new Rect(area.x, area.y, area.width, h);
            S.Frame(r);
            float x = r.x + S.Pad, y = r.y + S.Pad, inner = r.width - S.Pad * 2f;

            // 머리 줄: 지난달 · 다음 달 · '2026년 6월' · 이달 경기 수
            var games = new Dictionary<DateTime, List<Game>>();
            int total = 0, home = 0;
            foreach (var g in Sorted(s, g => Mine(g) && g.Day.Year == month.Year && g.Day.Month == month.Month))
            {
                if (!games.TryGetValue(g.Day, out var list)) games[g.Day] = list = new List<Game>();
                list.Add(g);
                total++;
                if (g.Home == 0) home++;
            }
            float cy = y + D.CalendarHeadH * 0.5f, bh = Button.HCompact;
            if (Button.Square(new Rect(x, Mathf.Round(cy - bh * 0.5f), bh, bh), Icons.Left, "지난달")) calMonth = month.AddMonths(-1);
            if (Button.Square(new Rect(x + bh + D.ActionGap, Mathf.Round(cy - bh * 0.5f), bh, bh), Icons.Right, "다음 달")) calMonth = month.AddMonths(1);
            float tx = x + (bh + D.ActionGap) * 2f + D.Gap;
            string title = $"{month.Year}년 {month.Month}월";
            float tw = Mathf.Ceil(Draw.TextWidth(title, D.CalendarTitleFont, true));
            Line(new Rect(tx, y, tw + D.Gap, D.CalendarHeadH), title, Look.BarInk, true, D.CalendarTitleFont);
            string sub = total == 0 ? "경기 없음" : $"{total}경기 · 홈 {home} · 원정 {total - home}";
            S.Tag(x + inner - S.Tag(0f, 0f, sub, null, null, null, false), cy, sub, Look.Pill, S.SubInk);
            y += D.CalendarHeadH + S.Gap;

            // 요일 줄(일요일 빨강)
            var week = new Rect(x, y, inner, D.CalendarWeekH);
            for (int i = 0; i < WeekDays.Length; i++)
                Line(Cell(week, 7, i, D.CalendarGap), WeekDays[i], i == 0 ? Pal.CatRed : S.SubInk, true, S.SubFont, TextAnchor.MiddleCenter);
            y += D.CalendarWeekH + S.Gap;

            var first = month.AddDays(-lead);
            for (int k = 0; k < weeks * 7; k++)
            {
                var day = first.AddDays(k);
                var row = new Rect(x, y + (D.CalendarCellH + D.CalendarGap) * (k / 7), inner, D.CalendarCellH);
                games.TryGetValue(day, out var list);
                DayCell(s, Cell(row, 7, k % 7, D.CalendarGap), day, day.Month == month.Month, list);
            }
            return h;
        }

        /// <summary>달력 칸: 날짜 원(오늘 = 초록) · 승패 알약 · 아래 상대(vs 홈 · @ 원정) · 점수 또는 시각. 홈 경기 칸은 옅은 초록.</summary>
        void DayCell(Season s, Rect c, DateTime day, bool inMonth, List<Game> list)
        {
            bool today = day == Today, home = list != null && list[0].Home == 0;
            // 경기 있는 칸은 눌러 경기 상세(지난 · 오늘 · 앞으로 경기 모두), 더블헤더는 첫 경기 → 상세 위 줄에서 바꿈
            bool hot = list != null && Draw.Hover(c);
            if (list != null && Draw.Button(c)) OpenLive(s.Games.IndexOf(list[0]));
            if (Draw.Repaint)
            {
                Draw.Round(c, !inMonth ? Look.Pill : home ? Look.PastelSubFill : S.Plate, D.CalendarRadius);
                if (today || hot) Draw.RoundOutline(c, Pal.Primary, D.CalendarRadius, D.CalendarRing);
                else if (inMonth) Draw.RoundOutline(c, home ? Look.HeroLine : S.PlateLine, D.CalendarRadius);
            }
            var inner = new Rect(c.x + D.CalendarPad, c.y + D.CalendarPad, c.width - D.CalendarPad * 2f, c.height - D.CalendarPad * 2f);
            var dot = new Rect(inner.x, inner.y, D.CalendarDayD, D.CalendarDayD);
            if (Draw.Repaint && today) Draw.Round(dot, Pal.Primary, D.CalendarDayD * 0.5f);
            Color ink = today ? Pal.OnPrimary : !inMonth ? Pal.Dim : day.DayOfWeek == DayOfWeek.Sunday ? Pal.CatRed : Look.BarInk;
            Line(dot, day.Day.ToString(), ink, true, D.FontDesc, TextAnchor.MiddleCenter);
            if (list == null) return;
            var g = list[0];
            int res = League.Result(g, 0);
            if (g.Played && list.Count == 1) ResultBadge(new Rect(inner.xMax - Badge.H, Mathf.Round(dot.center.y - Badge.CountH * 0.5f), Badge.H, Badge.CountH), res);
            float subY = inner.yMax - D.LineSub, nameY = subY - D.LineBody;
            float nx = Mark(inner.x, nameY + D.LineBody * 0.5f, Opponent(g), D.TeamMarkW, D.LineSub, D.IconGap);
            Line(new Rect(nx, nameY, inner.xMax - nx, D.LineBody), (home ? "vs " : "@ ") + Short(s, Opponent(g)), inMonth ? Look.BarInk : Look.PillSub, true, D.FontDesc);
            string sub;
            if (list.Count > 1)
            {
                var parts = new List<string>();
                foreach (var o in list) parts.Add(o.Played ? Score(o).Replace(" ", "") : Clock(o.Minute));
                sub = "DH " + string.Join(" · ", parts.ToArray());
            }
            else sub = g.Played ? Score(g) : Clock(g.Minute) + (today ? " · 오늘" : "");
            Line(new Rect(inner.x, subY, inner.width, D.LineSub), sub, today ? Pal.PrimaryInk : Look.PillSub, false, D.FontMin);
        }

        /// <summary>내 팀 점수 먼저('5 : 3').</summary>
        static string Score(Game g) => g.Home == 0 ? $"{g.HomeRuns} : {g.AwayRuns}" : $"{g.AwayRuns} : {g.HomeRuns}";

        // ---- 포스트시즌 대진(두 열 아래 전체 폭)

        float Bracket(Season s, float y, float w)
        {
            float h = PlateH(S.RowH * s.Post.Count);
            var b = Plate(new Rect(0f, y, w, h), "포스트시즌 대진", IconFlag, s.Finished ? "끝남" : "진행 중");
            float nameW = Mathf.Ceil(Draw.TextWidth("클라이맥스 시리즈 퍼스트 · 센트럴 리그", D.FontDesc, true));
            for (int i = 0; i < s.Post.Count; i++)
            {
                var x = s.Post[i];
                var row = new Rect(b.x, b.y + S.RowH * i, b.width, S.RowH);
                if (x.High == 0 || x.Low == 0) S.Mark(row);
                float cx = row.x + S.RowPadL, cy = row.center.y;
                Line(new Rect(cx, row.y, nameW, row.height), RoundName(s, x), S.SubInk, true, D.FontDesc);
                cx += nameW + S.RowGap;
                int win = s.Winner(i);
                string state = win < 0 ? "진행 중" : Short(s, win) + " 승";
                float sw = Badge.Pair(0f, 0f, state, Look.PastelFill, Look.PastelInk, false);
                if (win < 0) Badge.Pair(row.xMax - S.RowPadR - sw, cy, state, Look.Pill, Look.BarOff);
                else Badge.Pair(row.xMax - S.RowPadR - sw, cy, state, Look.PastelFill, Look.PastelInk);
                float half = (row.xMax - S.RowPadR - sw - S.RowGap - cx) * 0.5f;
                string score = $"{s.Wins(i, x.High)} : {s.Wins(i, x.Low)}";
                float scw = Mathf.Ceil(Draw.TextWidth(score, D.FontValue, true));
                float tx = Mark(cx, cy, x.High);
                Line(new Rect(tx, row.y, half - scw * 0.5f - S.RowGap - (tx - cx), row.height), s.Team(x.High).Name, Look.BarInk, x.High == win);
                Line(new Rect(cx + half - scw * 0.5f, row.y, scw, row.height), score, Look.BarInk, true, D.FontValue, TextAnchor.MiddleCenter);
                tx = Mark(cx + half + scw * 0.5f + S.RowGap, cy, x.Low);
                Line(new Rect(tx, row.y, cx + half * 2f - tx, row.height), s.Team(x.Low).Name, Look.BarInk, x.Low == win);
            }
            return y + h;
        }
    }
}
