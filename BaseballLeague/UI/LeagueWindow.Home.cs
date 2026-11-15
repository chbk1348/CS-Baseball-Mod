using System;
using BaseballLeague.Data;
using CSCommonUX;
using UnityEngine;
using D = CSCommonUX.CSCommonDesign;
using S = CSCommonUX.Section;

namespace BaseballLeague.UI
{
    /// <summary>
    /// 홈(시안 Main): 오늘 · 다음 경기 히어로(두 팀 · 플레이볼까지 · 승률 조정 타일 · 경기 진행 단계 띠) · 빠른 실행 5칸 · 순위 · 최근 경기 · 다음 경기 세 열 · 홈 경기 관중(누적 · 최근 · 지난 경기 대비 · 월별 평균).
    /// </summary>
    internal sealed partial class LeagueWindow
    {
        float HomeTab(Season s, View v, float w)
        {
            float y = HomeHero(s, v, w);
            y = QuickActions(s, v, y, w);
            y = HomeColumns(s, v, y, w);
            return HomeCrowd(s, y + S.Gap, w);
        }

        // ---- 히어로

        static float HeroLeftH => S.ChipH + S.HeroGap + S.HeroTitleH + Stat.LabelH + S.HeroGap + Stat.LabelH + D.HeroNumH + S.HeroGap + Button.H;

        static float StepsH => S.HeroGap + D.StepBarH + D.ActionGap + D.LineBody;

        float HomeHero(Season s, View v, float w)
        {
            var g = v.Next;
            float h = S.HeroPadY * 2f + HeroLeftH + (g != null ? StepsH : 0f);
            var r = new Rect(0f, 0f, w, h);
            HeroFrame(r);
            float x = S.HeroPadX, y = S.HeroPadY, leftW = w - S.HeroPadX * 2f - D.HeroSideW - S.HeroGap;
            if (g == null)
            {
                S.ChipAt(x, y + S.ChipH * 0.5f, "다음 경기", TabIcons[TabSchedule]);
                var champ = s.Champion(out var t) ? $"{t.Year}년 우승 {s.Team(t.Champion).Name}" : "정규 시즌이 끝났어요";
                S.HeroTitle(new Rect(x, y + S.ChipH + S.HeroGap, w - S.HeroPadX * 2f, S.HeroTitleHeight(champ)), "남은 경기가 없어요", null, champ + " · 설정에서 새 시즌을 시작할 수 있어요");
                return h + S.Gap;
            }
            bool home = g.Home == 0, today = g.Day == Today;
            int opp = Opponent(g), mine = MyGameNumber(s, g);
            string title = today ? home ? "오늘 홈 경기" : "오늘 원정 경기" : "다음 경기";
            float cw = S.ChipAt(x, y + S.ChipH * 0.5f, title, TabIcons[TabHome]);
            if (g.Series == 0) S.Tag(x + cw + S.ChipGap, y + S.ChipH * 0.5f, $"{mine} / {v.MyGames}");
            else S.Tag(x + cw + S.ChipGap, y + S.ChipH * 0.5f, RoundName(s, s.Post[g.Series - 1]));
            y += S.ChipH + S.HeroGap;

            // 두 팀: 색 막대 · 큰 이름 · 순위 · 승패
            float tx = HeroTeam(s, v, 0, x, y);
            float vw = Mathf.Ceil(Draw.TextWidth("vs", D.FontValue, true));
            Line(new Rect(tx, y, vw, S.HeroTitleH), "vs", Look.PillSub, true, D.FontValue);
            HeroTeam(s, v, opp, tx + vw + S.HeroStatGap, y);
            y += S.HeroTitleH + Stat.LabelH + S.HeroGap;

            // 플레이볼까지(오늘 · 시작 전) 또는 경기 날짜 · 시각
            int left = g.Minute - (int)Now.TimeOfDay.TotalMinutes;
            bool count = today && left > 0;
            string label = count ? "플레이볼까지" : today ? "시작 시각" : $"{g.Day.Month}월 {g.Day.Day}일 ({"일월화수목금토"[(int)g.Day.DayOfWeek]})";
            string big = count ? $"{left / 60}:{left % 60:00}" : Clock(g.Minute);
            Line(new Rect(x, y, leftW, Stat.LabelH), label, Look.PillSub, false, D.FontMin);
            float bw = Mathf.Ceil(Draw.TextWidth(big, D.HeroNumFont, true));
            if (Draw.Repaint) Draw.Text(new Rect(x, y + Stat.LabelH, bw + S.HeroSubGap, D.HeroNumH), big, D.HeroNumFont, Look.BarInk, true);
            string where = (home ? Park(s, 0) : Park(s, g.Home)) + " · " + Clock(g.Minute) + (g.Double ? " · 더블헤더" : g.Moved ? " · 예비일" : g.Inter ? " · 교류전" : "");
            Line(new Rect(x + bw + S.HeroStatGap * 0.5f, y + Stat.LabelH, leftW - bw - S.HeroStatGap * 0.5f, D.HeroNumH), where, S.SubInk, false, D.FontDesc);
            y += Stat.LabelH + D.HeroNumH + S.HeroGap;

            // 단추: 경기장으로 이동(홈구장이 있을 때) · 일정 탭
            float by = y + Button.H * 0.5f, bx = x;
            ushort stadium = Stadium.Home;
            if (stadium != 0) bx += Button.At(bx, by, null, "경기장으로 이동", null, Tone.Primary, null, true, () => Stadium.Focus(stadium)) + Button.Gap;
            Button.At(bx, by, null, "일정 탭에서 보기", null, stadium != 0 ? Tone.Neutral : Tone.Primary, null, true, () => Go(TabSchedule));

            // 오른쪽 흰 타일: 승률 조정(홈구장 홈 경기만) · 두 팀 최근 10경기
            var side = new Rect(w - S.HeroPadX - D.HeroSideW, S.HeroPadY, D.HeroSideW, HeroLeftH);
            if (Draw.Repaint)
            {
                Draw.Round(side, S.Plate, Tile.Radius);
                Draw.RoundOutline(side, S.PlateLine, Tile.Radius);
            }
            var inner = new Rect(side.x + Tile.PadX, side.y + D.CardRowPad, side.width - Tile.PadX * 2f, side.height - D.CardRowPad * 2f);
            bool adjusts = home && stadium != 0;
            float points = (s.Power(0) - s.Power(opp)) * League.PowerPoints;
            string value = adjusts ? (points >= 0f ? "+" : "") + points.ToString("0.0") + "점" : "조정 없음";
            string sub = adjusts ? "게임이 정한 승률에 더해요 · 최대 ±10점" : home ? "홈구장이 없어 모드가 결과를 정해요" : "원정 경기는 모드가 결과를 정해요";
            Stat.In(new Rect(inner.x, inner.y, inner.width, Stat.Height(true)), "홈 팀 승률 조정 · 팀 전력 차", value, sub);
            float ly = inner.y + Stat.Height(true) + S.HeadGap;
            Stat.Line(new Rect(inner.x, ly, inner.width, Stat.LineH), "최근 10경기 " + Short(s, 0), Last10(v, 0));
            Stat.Line(new Rect(inner.x, ly + Stat.LineH, inner.width, Stat.LineH), "최근 10경기 " + Short(s, opp), Last10(v, opp));
            ly += Stat.LineH * 2f + S.HeadGap;
            S.Hairline(new Rect(inner.x, ly, inner.width, 1f));
            Note(new Rect(inner.x, ly + D.ActionGap, inner.width, inner.yMax - ly - D.ActionGap), "경기 결과는 게임의 경기장 이벤트가 정하고, 모드는 승률만 팀 전력으로 조정해요");
            Steps(s, g, new Rect(x, S.HeroPadY + HeroLeftH + S.HeroGap, w - S.HeroPadX * 2f, StepsH - S.HeroGap));
            return h + S.Gap;
        }

        /// <summary>경기 진행 단계 띠(경기 준비 → 플레이볼 → 경기 종료 → 결과 기록): 지난 · 지금 단계 막대 초록, 오늘 경기가 아니면 모두 옅게.</summary>
        void Steps(Season s, Game g, Rect r)
        {
            int index = s.Games.IndexOf(g), cur = -1;
            var st = StateOf(s, g, index, out float t);
            string playing = Clock(g.Minute);
            if (g.Day == Today)
            {
                cur = st == Live.State.Before ? 0 : st == Live.State.Live ? 1 : st == Live.State.Over ? 2 : 3;
                if (st == Live.State.Live)
                {
                    var f = Live.At(Live.Of(s, g, index, Today), t);
                    playing = "진행 중 · " + Half(f.Inning, f.Bottom);
                }
            }
            int left = g.Minute - (int)Now.TimeOfDay.TotalMinutes, end = (g.Minute + Live.GameMinutes) % (Live.DayMinutes);
            string[] names = { "경기 준비", "플레이볼", "경기 종료", "결과 기록" };
            string[] when = { cur == 0 && left > 0 ? $"지금 · {left / 60}:{left % 60:00}" : g.Day == Today ? "오늘" : Md(g.Day), playing, "약 " + Clock((short)end), st == Live.State.Rain ? "우천 취소" : "날짜가 바뀔 때" };
            for (int i = 0; i < names.Length; i++)
            {
                var c = Cell(r, names.Length, i, D.ActionGap);
                if (Draw.Repaint) Draw.Round(new Rect(c.x, c.y, c.width, D.StepBarH), i <= cur ? Pal.Primary : Look.PastelFill, D.StepBarH * 0.5f);
                float ty = c.y + D.StepBarH + D.ActionGap, nw = Mathf.Ceil(Draw.TextWidth(names[i], S.ChipFont, true));
                Line(new Rect(c.x + D.Gap, ty, nw, D.LineBody), names[i], i == cur ? Pal.PrimaryInk : Look.BarOff, true, S.ChipFont);
                Line(new Rect(c.x + D.Gap + nw + D.ActionGap, ty, c.width - nw - D.ActionGap - D.Gap, D.LineBody), when[i], Look.PillSub, false, D.FontMin);
            }
        }

        // ---- 홈 경기 관중

        /// <summary>홈 경기 관중(시안 Main 아래 판): 왼쪽 타일 셋(누적 · 최근 홈 경기 · 지난 홈 경기 대비) · 오른쪽 월별 홈 경기 평균 막대(지난 달 회색 · 최근 달 초록 · 남은 달 빈 칸).</summary>
        float HomeCrowd(Season s, float y, float w)
        {
            var v = ViewOf(s);
            var homes = v.Homes;
            var done = v.HomeDone ??= homes.FindAll(g => g.Played && g.Box != null && League.SameUnit(g.Box)); // 지금 홈구장 단위와 같은 관중만(가상 구장 추정과 섞지 않음)
            int total = 0;
            foreach (var g in done) total += g.Box.Crowd;
            float tileH = TileH(true), left = tileH * 3f + D.ActionGap * 2f;
            float chart = D.LineBody + S.HeadGap + D.CrowdChartH + D.ActionGap + D.LineSub;
            float h = PlateH(Mathf.Max(left, chart));
            var b = Plate(new Rect(0f, y, w, h), "홈 경기 관중", IconStadium, $"{done.Count} / {homes.Count} 홈 경기");
            var col = new Rect(b.x, b.y, D.SideColW, tileH);
            Game last = done.Count > 0 ? done[done.Count - 1] : null, prev = done.Count > 1 ? done[done.Count - 2] : null;
            StatTile(col, "누적 관중", $"{total:N0}명", done.Count == 0 ? "아직 홈 경기가 없어요" : $"홈 경기 평균 {total / done.Count:N0}명", false);
            col.y += tileH + D.ActionGap;
            StatTile(col, "최근 홈 경기", last == null ? "-" : $"{last.Box.Crowd:N0}명", last == null ? "홈 경기가 끝나면 보여요" : $"{last.Day.Month}월 {last.Day.Day}일 vs {s.Team(last.Away).Name}", false);
            col.y += tileH + D.ActionGap;
            string diff = last == null || prev == null || prev.Box.Crowd == 0 ? "-" : (last.Box.Crowd >= prev.Box.Crowd ? "+" : "") + Mathf.RoundToInt((last.Box.Crowd - prev.Box.Crowd) * 100f / prev.Box.Crowd) + "%";
            StatTile(col, "지난 홈 경기 대비", diff, prev == null ? "홈 경기가 두 번 끝나면 보여요" : $"{prev.Day.Month}월 {prev.Day.Day}일 {prev.Box.Crowd:N0}명 대비", false);
            CrowdChart(s, homes, new Rect(b.x + D.SideColW + D.CardColGap, b.y, b.width - D.SideColW - D.CardColGap, chart));
            return y + h;
        }

        /// <summary>달마다 홈 경기 평균 관중 막대(정규 시즌 첫 달 ~ 끝 달, 높이 = 평균 / 자리).</summary>
        void CrowdChart(Season s, System.Collections.Generic.List<Game> homes, Rect r)
        {
            var first = new DateTime(s.Start.Year, s.Start.Month, 1);
            var lastDay = homes.Count > 0 ? homes[homes.Count - 1].Day : s.Start;
            int months = Mathf.Max(1, (lastDay.Year - first.Year) * 12 + lastDay.Month - first.Month + 1);
            var sum = new int[months];
            var n = new int[months];
            foreach (var g in homes)
            {
                if (!g.Played || g.Box == null || !League.SameUnit(g.Box)) continue;
                int m = (g.Day.Year - first.Year) * 12 + g.Day.Month - first.Month;
                if (m < 0 || m >= months) continue;
                sum[m] += g.Box.Crowd;
                n[m]++;
            }
            int latest = -1;
            for (int m = 0; m < months; m++)
                if (n[m] > 0) latest = m;
            Line(new Rect(r.x, r.y, r.width, D.LineBody), "월별 홈 경기 평균 관중", Look.BarInk, true, S.ChipFont);
            float lx = r.xMax;
            string[] legend = { "남은 달", "지난", "최근" };
            for (int i = 0; i < legend.Length; i++)
            {
                float tw = Mathf.Ceil(Draw.TextWidth(legend[i], D.FontMin));
                lx -= tw;
                Line(new Rect(lx, r.y, tw, D.LineBody), legend[i], Look.PillSub, false, D.FontMin);
                lx -= D.Icon + D.IconGap;
                var sw = new Rect(lx, Mathf.Round(r.y + (D.LineBody - D.Icon) * 0.5f), D.Icon, D.Icon);
                if (Draw.Repaint)
                {
                    if (i == 0) Draw.RoundOutline(sw, S.PlateLine, D.Gap);
                    else Draw.Round(sw, i == 1 ? Pal.Line2 : Pal.Primary, D.Gap);
                }
                lx -= D.CardColGap;
            }
            var area = new Rect(r.x, r.y + D.LineBody + S.HeadGap, r.width, D.CrowdChartH);
            int seats = Mathf.Max(1, League.Seats(new Game())); // 막대 기준 = 모은 관중과 같은 단위 자리
            float barTop = D.LineSub + D.Gap, barMax = area.height - barTop;
            for (int m = 0; m < months; m++)
            {
                var c = Cell(area, months, m, D.CardColGap);
                bool has = n[m] > 0;
                int avg = has ? sum[m] / n[m] : 0;
                float bh = has ? Mathf.Max(D.StepBarH, barMax * Mathf.Clamp01((float)avg / seats)) : D.StepBarH * 2f;
                var bar = new Rect(c.x, area.yMax - bh, c.width, bh);
                if (Draw.Repaint)
                {
                    if (has) Draw.Round(bar, m == latest ? Pal.Primary : Pal.Line2, D.Gap);
                    else Draw.RoundOutline(bar, S.PlateLine, D.Gap);
                }
                if (has) Line(new Rect(c.x, bar.y - barTop, c.width, D.LineSub), (avg / 1000f).ToString("0.0") + "k", m == latest ? Pal.PrimaryInk : Look.PillSub, m == latest, D.FontMin, TextAnchor.MiddleCenter);
                int month = (first.Month - 1 + m) % 12 + 1;
                Line(new Rect(c.x, area.yMax + D.ActionGap, c.width, D.LineSub), month + "월", has ? Look.BarOff : Look.PillSub, false, D.FontMin, TextAnchor.MiddleCenter);
            }
            if (Draw.Repaint) Draw.Fill(new Rect(area.x, area.yMax, area.width, 1f), S.PlateLine);
        }

        /// <summary>히어로 팀 하나: 색 막대 · 이름 30 · 아래 '2위 · 37승 27패'. 다음 x 를 돌려줌.</summary>
        static float HeroTeam(Season s, View v, int team, float x, float y)
        {
            float tx = Mark(x, y + (S.HeroTitleH + Stat.LabelH) * 0.5f, team, D.TeamHeroMarkW, D.TeamHeroMarkH);
            string name = s.Team(team).Name;
            float nw = Mathf.Ceil(Draw.TextWidth(name, S.HeroTitleFont, true));
            Line(new Rect(tx, y, nw + S.HeroSubGap, S.HeroTitleH), name, Look.BarInk, true, S.HeroTitleFont);
            var r = v.Records[team];
            Line(new Rect(tx, y + S.HeroTitleH, nw, Stat.LabelH), $"{v.Rank[team]}위 · {WinLoss(r.W, r.L, r.T)}", Look.PillSub, false, D.FontMin);
            return tx + nw + S.HeroStatGap;
        }

        static string Last10(View v, int team)
        {
            int w = 0, l = 0;
            foreach (var g in v.Records[team].Last10)
            {
                int res = League.Result(g, team);
                if (res > 0) w++;
                else if (res < 0) l++;
            }
            return WinLoss(w, l);
        }

        /// <summary>내 팀 정규 시즌 몇 번째 경기인지(1부터).</summary>
        static int MyGameNumber(Season s, Game g)
        {
            int n = 1;
            foreach (var o in s.Games)
                if (o != g && o.Series == 0 && Mine(o) && Earlier(o, g)) n++;
            return n;
        }

        // ---- 빠른 실행

        static readonly string[] ActionIcons = { IconStadium, TabIcons[TabSchedule], TabIcons[TabTeams], TabIcons[TabRecords], IconPulse };
        static readonly string[] ActionNames = { "홈구장", "일정 보기", "팀 기록", "시즌 기록", "상태 진단" };

        float QuickActions(Season s, View v, float y, float w)
        {
            float h = PlateH(D.ActionTileH);
            var body = Plate(new Rect(0f, y, w, h), "빠른 실행", IconBolt);
            int homeGames = 0;
            foreach (var g in s.Games)
                if (g.Home == 0 && g.Day.Year == Today.Year && g.Day.Month == Today.Month) homeGames++;
            int titles = s.Titles().Count;
            string[] subs =
            {
                Stadium.Name.Length > 0 ? Stadium.Name : "경기장 없음",
                $"{Today.Month}월 · 홈 {homeGames}경기",
                $"{Rules.TeamCount(s.Rule)}팀 · 순위 · 전력",
                titles > 0 ? $"우승 이력 {titles}시즌" : "포스트시즌 · 이력",
                "경기장 · 시즌",
            };
            for (int i = 0; i < ActionNames.Length; i++)
            {
                var c = Cell(body, ActionNames.Length, i, D.ActionGap);
                int k = i;
                if (Tile.In(c, false, true, null, () => ActionContent(c, ActionIcons[k], ActionNames[k], subs[k])))
                {
                    if (k == 0) Go(TabHome, Page.Venue);
                    else Go(k == 1 ? TabSchedule : k == 2 ? TabTeams : k == 3 ? TabRecords : TabSettings);
                }
            }
            return y + h + S.Gap;
        }

        static void ActionContent(Rect c, string icon, string name, string sub)
        {
            if (!Draw.Repaint) return;
            var ic = new Rect(c.x + D.CardRowPad, Mathf.Round(c.center.y - IconButton.Size * 0.5f), IconButton.Size, IconButton.Size);
            Draw.Round(ic, S.Plate, IconButton.Size * 0.5f);
            Draw.RoundOutline(ic, S.PlateLine, IconButton.Size * 0.5f);
            Draw.Icon(icon, new Rect(Mathf.Round(ic.center.x - IconButton.IconSize * 0.5f), Mathf.Round(ic.center.y - IconButton.IconSize * 0.5f), IconButton.IconSize, IconButton.IconSize), Look.BarOff);
            float tx = ic.xMax + D.CardRowPad, tw = c.xMax - D.CardRowPad - tx, top = Mathf.Round(c.center.y - (D.LineBody + D.LineSub) * 0.5f);
            Line(new Rect(tx, top, tw, D.LineBody), name, Look.BarInk, true);
            Line(new Rect(tx, top + D.LineBody, tw, D.LineSub), sub, Look.PillSub, false, D.FontMin);
        }

        // ---- 세 열: 순위 · 최근 경기 · 다음 경기

        float HomeColumns(Season s, View v, float y, float w)
        {
            const int recentN = 5, nextN = 3;
            int lg = Rules.LeagueOf(s.Rule, 0);
            var table = v.Tables[lg];
            var recent = Sorted(s, g => g.Played && Mine(g));
            if (recent.Count > recentN) recent.RemoveRange(0, recent.Count - recentN);
            recent.Reverse();
            var next = Sorted(s, g => !g.Played && Mine(g));
            if (next.Count > nextN) next.RemoveRange(nextN, next.Count - nextN);
            float progH = Stat.Height(true) + S.HeadGap + Progress.Bar + D.CardRowPad * 2f, recentH = S.RowH * Mathf.Max(1, recent.Count), nextH = S.RowH * Mathf.Max(1, next.Count);
            float body = Mathf.Max(D.TableRowH * table.Count, Mathf.Max(recentH + S.HeadGap + TileH(false), nextH + S.HeadGap + progH));
            float h = PlateH(body);
            var row = new Rect(0f, y, w, h);
            string played = $"{v.MyPlayed} / {v.MyGames}경기";

            var b = Plate(Cell(row, 3, 0, S.Gap), s.Rule == Rule.NPB ? Rules.LeagueName(s.Rule, lg) + " 순위" : "순위", TabIcons[TabRecords], played);
            StandingRows(s, table, b);

            b = Plate(Cell(row, 3, 1, S.Gap), "최근 경기", IconClock, s.Team(0).Name);
            if (recent.Count == 0) Line(new Rect(b.x, b.y, b.width, S.RowH), "아직 끝난 경기가 없어요", Look.PillSub);
            for (int i = 0; i < recent.Count; i++) RecentRow(s, recent[i], new Rect(b.x, b.y + S.RowH * i, b.width, S.RowH));
            var rec = v.Records[0];
            var t = new Rect(b.x, b.y + recentH + S.HeadGap, b.width, TileH(false));
            if (Draw.Repaint) Draw.Round(t, Look.Pill, Tile.Radius);
            var ti = new Rect(t.x + Tile.PadX, t.y, t.width - Tile.PadX * 2f, t.height);
            int diff = rec.Runs - rec.Against;
            Stat.In(Cell(ti, 2, 0, D.ActionGap), "최근 10경기", Last10(v, 0));
            Stat.In(Cell(ti, 2, 1, D.ActionGap), "득실 차", (diff > 0 ? "+" : "") + diff);

            b = Plate(Cell(row, 3, 2, S.Gap), "다음 경기", TabIcons[TabSchedule], next.Count > 0 && next[0].Series != 0 ? "포스트시즌" : "3연전");
            if (next.Count == 0) Line(new Rect(b.x, b.y, b.width, S.RowH), "남은 경기가 없어요", Look.PillSub);
            for (int i = 0; i < next.Count; i++) NextRow(s, next[i], new Rect(b.x, b.y + S.RowH * i, b.width, S.RowH));
            float ph = progH - D.CardRowPad * 2f;
            t = new Rect(b.x, b.y + nextH + S.HeadGap, b.width, progH);
            if (Draw.Repaint) Draw.Round(t, Look.Pill, Tile.Radius);
            ti = new Rect(t.x + Tile.PadX, t.y + D.CardRowPad, t.width - Tile.PadX * 2f, ph);
            float done = v.MyGames == 0 ? 0f : (float)v.MyPlayed / v.MyGames;
            DateTime end = default;
            foreach (var g in s.Games)
                if (g.Series == 0 && g.Day > end) end = g.Day;
            string post = s.Rule == Rule.KBO ? "포스트시즌 상위 5팀" : "포스트시즌 리그별 3팀";
            Stat.In(new Rect(ti.x, ti.y, ti.width, Stat.Height(true)), "시즌 진행 · " + Mathf.RoundToInt(done * 100f) + "%", played, $"정규 시즌 {end.Month}월 {end.Day}일까지 · {post}");
            Progress.In(new Rect(ti.x, ti.yMax - Progress.Bar, ti.width, Progress.Bar), done, -1f, Pal.Primary);
            return y + h;
        }

        /// <summary>순위 줄(시안 순위 판): 순번 · 색 막대 · 이름 · 승-패 · 게임 차 · 승률. 내 팀 줄은 옅은 초록.</summary>
        static void StandingRows(Season s, System.Collections.Generic.List<Standing> table, Rect b)
        {
            float pctW = Mathf.Ceil(Draw.TextWidth("1.000", D.FontBody, true)), wlW = Mathf.Ceil(Draw.TextWidth("100-100", D.FontMin)), gbW = Mathf.Ceil(Draw.TextWidth("00.0", D.FontMin));
            float numW = Mathf.Ceil(Draw.TextWidth("12", D.FontDesc, true));
            for (int i = 0; i < table.Count; i++)
            {
                var t = table[i];
                var row = new Rect(b.x, b.y + D.TableRowH * i, b.width, D.TableRowH);
                if (t.Team == 0) S.Mark(row);
                float x = row.x + S.RowPadL, right = row.xMax - S.RowPadL, cy = row.center.y;
                Line(new Rect(x, row.y, numW, row.height), (i + 1).ToString(), S.SubInk, true, D.FontDesc, TextAnchor.MiddleRight);
                x = Mark(x + numW + S.RowGap, cy, t.Team);
                Line(new Rect(right - pctW, row.y, pctW, row.height), Pct(t.Pct), Look.BarInk, true, D.FontBody, TextAnchor.MiddleRight);
                Line(new Rect(right - pctW - S.RowGap - gbW, row.y, gbW, row.height), Gb(t.GamesBehind), Look.PillSub, false, D.FontMin, TextAnchor.MiddleRight);
                Line(new Rect(right - pctW - gbW - wlW - S.RowGap * 2f, row.y, wlW, row.height), $"{t.W}-{t.L}", Look.PillSub, false, D.FontMin, TextAnchor.MiddleRight);
                Line(new Rect(x, row.y, right - pctW - gbW - wlW - S.RowGap * 3f - x, row.height), s.Team(t.Team).Name, Look.BarInk, true);
            }
        }

        /// <summary>최근 경기 줄: 날짜 · 승패 알약 · 상대(홈 · 원정 · 자리) · 점수(내 팀 먼저).</summary>
        static void RecentRow(Season s, Game g, Rect row)
        {
            float x = row.x + S.RowPadL, cy = row.center.y;
            float dw = Mathf.Ceil(Draw.TextWidth("12/31", D.FontMin));
            Line(new Rect(x, row.y, dw, row.height), Md(g.Day), Look.PillSub, false, D.FontMin);
            x += dw + S.RowGap;
            ResultBadge(new Rect(x, Mathf.Round(cy - Badge.H * 0.5f), S.RankD, Badge.H), League.Result(g, 0));
            x += S.RankD + S.RowGap;
            bool home = g.Home == 0;
            string score = home ? $"{g.HomeRuns} : {g.AwayRuns}" : $"{g.AwayRuns} : {g.HomeRuns}";
            float sw = Mathf.Ceil(Draw.TextWidth(score, D.FontValue, true));
            Line(new Rect(row.xMax - S.RowPadL - sw, row.y, sw, row.height), score, Look.BarInk, true, D.FontValue, TextAnchor.MiddleRight);
            TwoLines(new Rect(x, row.y, row.xMax - S.RowPadL - sw - S.RowGap - x, row.height), s.Team(Opponent(g)).Name, (home ? "홈 · " : "원정 · ") + Park(s, g.Home));
        }

        /// <summary>다음 경기 줄: 날짜 시각 · 색 막대 · 상대(자리) · 홈 · 원정 알약.</summary>
        static void NextRow(Season s, Game g, Rect row)
        {
            float x = row.x + S.RowPadL, cy = row.center.y;
            float dw = Mathf.Ceil(Draw.TextWidth("12/31 18:30", D.FontMin));
            Line(new Rect(x, row.y, dw, row.height), Md(g.Day) + " " + Clock(g.Minute), Look.PillSub, false, D.FontMin);
            x = Mark(x + dw + S.RowGap, cy, Opponent(g));
            float bw = HomeAway(row.xMax - S.RowPadL, cy, g.Home == 0);
            TwoLines(new Rect(x, row.y, row.xMax - S.RowPadL - bw - S.RowGap - x, row.height), s.Team(Opponent(g)).Name, Park(s, g.Home));
        }

        /// <summary>두 줄 글(굵은 이름 14 · 아래 보조 12)을 칸 세로 가운데로.</summary>
        static void TwoLines(Rect r, string top, string bottom)
        {
            float y = Mathf.Round(r.center.y - (D.LineBody + D.LineSub) * 0.5f);
            Line(new Rect(r.x, y, r.width, D.LineBody), top, Look.BarInk, true);
            Line(new Rect(r.x, y + D.LineBody, r.width, D.LineSub), bottom, Look.PillSub, false, D.FontMin);
        }
    }
}
