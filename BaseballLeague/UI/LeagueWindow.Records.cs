using System.Collections.Generic;
using BaseballLeague.Data;
using CSCommonUX;
using UnityEngine;
using D = CSCommonUX.CSCommonDesign;
using L = BaseballLeague.Data.Line;
using S = CSCommonUX.Section;

namespace BaseballLeague.UI
{
    /// <summary>
    /// 기록(시안 Records): 시즌 리더 5칸(타율 · 홈런 · 타점 · 평균자책 · 세이브, 내 팀 선수면 옅은 초록) · 승률 추이(상위 3팀 + 내 팀 · 8경기마다 · 끝난 경기에서 매번 셈 · 저장 안 함) ·
    /// 포스트시즌 경쟁(진출 자리 + 2팀) · 우승 이력(시즌 · 우승 · 정규 시즌 승패 · 준우승 · MVP · 내 팀).
    /// 시안의 부드러운 곡선은 꺾은선으로 그림(CSCommon 에 곡선 부품이 없음).
    /// </summary>
    internal sealed partial class LeagueWindow
    {
        float RecordsTab(Season s, View v, float w)
        {
            float y = Leaders(s, v, w);
            float lw = (w - S.Gap) * TrendShare;
            float a = Trend(s, v, new Rect(0f, y, lw, 0f));
            float b = Race(s, v, new Rect(lw + S.Gap, y, w - lw - S.Gap, 0f));
            return History(s, Mathf.Max(a, b) + S.Gap, w);
        }

        /// <summary>시안 두 판 폭 1.7 : 1 · 승률 추이 한 점 = 8경기 · 축 범위 .200 ~ .800 · 눈금 5줄.</summary>
        const float TrendShare = 1.7f / 2.7f, TrendLow = 0.2f, TrendHigh = 0.8f;
        const int TrendStep = 8, TrendGrid = 5, TrendTeams = 3;

        // ---- 시즌 리더

        sealed class Leader
        {
            public string Cat, Value, Name, Sub, Second;
            public int Team;
        }

        /// <summary>규정 타석 = 팀 경기 × 3.1 · 규정 이닝 = 팀 경기 × 1(아웃 3). shortcut: 시즌 초에도 같은 규정, 초반 규정을 따로 두게 되면 여기서.</summary>
        const float QualifyPa = 3.1f;

        List<Leader> leaders;
        View leadersFor, trendFor;
        readonly Dictionary<int, List<float>> trends = new();

        /// <summary>팀 승률 추이(판번호가 바뀔 때만 다시 셈).</summary>
        List<float> TrendCached(Season s, View v, int team)
        {
            if (trendFor != v)
            {
                trends.Clear();
                trendFor = v;
            }
            if (!trends.TryGetValue(team, out var pts)) trends[team] = pts = TrendOf(s, team);
            return pts;
        }

        List<Leader> LeadersOf(Season s, View v)
        {
            if (leadersFor == v && leaders != null) return leaders;
            var cand = new List<KeyValuePair<int, int>>[5];
            for (int k = 0; k < cand.Length; k++) cand[k] = new List<KeyValuePair<int, int>>();
            int n = Rules.TeamCount(s.Rule);
            for (int t = 0; t < n; t++)
            {
                var r = v.Records[t];
                int games = r.W + r.L + r.T;
                for (int i = 0; i < Roster.Size; i++)
                {
                    var l = s.StatOf(t, i);
                    var key = new KeyValuePair<int, int>(t, i);
                    if (i < Roster.Batters)
                    {
                        if (games > 0 && l[L.PA] >= games * QualifyPa) cand[0].Add(key);
                        if (l[L.HR] > 0) cand[1].Add(key);
                        if (l[L.RBI] > 0) cand[2].Add(key);
                    }
                    else
                    {
                        if (games > 0 && l[L.Outs] >= games * 3) cand[3].Add(key);
                        if (l[L.SV] > 0) cand[4].Add(key);
                    }
                }
            }
            string[] cats = { "타율", "홈런", "타점", "평균자책점", "세이브" };
            System.Func<L, float>[] score = { l => l.Avg, l => l[L.HR], l => l[L.RBI], l => -l.Era, l => l[L.SV] };
            System.Func<L, string>[] text = { l => Pct(l.Avg), l => l[L.HR].ToString(), l => l[L.RBI].ToString(), l => l.Era.ToString("0.00"), l => l[L.SV].ToString() };
            var list = new List<Leader>();
            for (int k = 0; k < cats.Length; k++)
            {
                var c = cand[k];
                int kk = k;
                c.Sort((x, y) => score[kk](s.StatOf(y.Key, y.Value)).CompareTo(score[kk](s.StatOf(x.Key, x.Value))));
                var ld = new Leader { Cat = cats[k], Team = -1 };
                if (c.Count > 0)
                {
                    var top = c[0];
                    var st = s.StatOf(top.Key, top.Value);
                    ld.Team = top.Key;
                    ld.Value = text[k](st);
                    ld.Name = s.RosterOf(top.Key).P[top.Value].Name;
                    ld.Sub = $"{s.Team(top.Key).Name} · {Roster.Position(top.Value)}";
                    ld.Second = c.Count > 1 ? $"2위 {s.RosterOf(c[1].Key).P[c[1].Value].Name} ({Short(s, c[1].Key)}) {text[k](s.StatOf(c[1].Key, c[1].Value))}" : "2위 없음";
                }
                list.Add(ld);
            }
            leadersFor = v;
            return leaders = list;
        }

        float Leaders(Season s, View v, float w)
        {
            var list = LeadersOf(s, v);
            float tileH = D.CardRowPad * 2f + D.LineSub + Stat.ValueH + D.LineBody + D.LineSub + D.ActionGap + 1f + D.LineSub;
            float h = PlateH(tileH);
            var b = Plate(new Rect(0f, 0f, w, h), "시즌 리더", TabIcons[TabRecords], $"{v.MyPlayed} / {v.MyGames}경기 기준");
            for (int k = 0; k < list.Count; k++)
            {
                var ld = list[k];
                var c = Cell(b, list.Count, k, D.ActionGap);
                bool mine = ld.Team == 0;
                if (Draw.Repaint)
                {
                    Draw.Round(c, mine ? Look.PastelPickFill : Look.Pill, Tile.Radius);
                    if (mine) Draw.RoundOutline(c, Look.PastelPickLine, Tile.Radius);
                }
                float x = c.x + Tile.PadX, iw = c.width - Tile.PadX * 2f, y = c.y + D.CardRowPad;
                Line(new Rect(x, y, iw, D.LineSub), ld.Cat, Look.PillSub, false, D.FontMin);
                y += D.LineSub;
                if (ld.Team < 0)
                {
                    Line(new Rect(x, y, iw, Stat.ValueH), "-", Look.BarInk, true, Stat.ValueFont);
                    Line(new Rect(x, y + Stat.ValueH, iw, D.LineBody), "규정을 채운 선수가 아직 없어요", Look.PillSub, false, D.FontMin);
                    continue;
                }
                Line(new Rect(x, y, iw, Stat.ValueH), ld.Value, Look.BarInk, true, Stat.ValueFont);
                y += Stat.ValueH;
                float tx = Mark(x, y + D.LineBody * 0.5f, ld.Team, D.TeamMarkW, D.TeamMarkSmallH, D.IconGap);
                Line(new Rect(tx, y, x + iw - tx, D.LineBody), ld.Name, Look.BarInk, true);
                y += D.LineBody;
                Line(new Rect(x, y, iw, D.LineSub), ld.Sub, Look.PillSub, false, D.FontMin);
                y += D.LineSub + D.ActionGap;
                if (Draw.Repaint) Draw.Fill(new Rect(x, y, iw, 1f), S.PlateLine);
                Line(new Rect(x, y + 1f, iw, D.LineSub), ld.Second, Look.PillSub, false, D.FontMin);
            }
            return h + S.Gap;
        }

        // ---- 승률 추이

        /// <summary>팀 team 의 8경기마다 누적 승률(무 제외).</summary>
        static List<float> TrendOf(Season s, int team)
        {
            var games = Sorted(s, g => g.Played && g.Series == 0 && (g.Home == team || g.Away == team));
            var pts = new List<float>();
            int w = 0, l = 0;
            for (int i = 0; i < games.Count; i++)
            {
                int r = League.Result(games[i], team);
                if (r > 0) w++;
                else if (r < 0) l++;
                if ((i + 1) % TrendStep == 0) pts.Add(w + l == 0 ? 0.5f : (float)w / (w + l));
            }
            return pts;
        }

        float Trend(Season s, View v, Rect area)
        {
            int lg = Rules.LeagueOf(s.Rule, 0);
            var table = v.Tables[lg];
            var teams = new List<int>();
            for (int i = 0; i < Mathf.Min(TrendTeams, table.Count); i++) teams.Add(table[i].Team);
            if (!teams.Contains(0)) teams.Add(0);
            float h = PlateH(D.LineBody + S.HeadGap + D.TrendH);
            var b = Plate(new Rect(area.x, area.y, area.width, h), teams.Count > TrendTeams ? "승률 추이 · 상위 3팀 · 내 팀" : "승률 추이 · 상위 3팀", IconChart, TrendStep + "경기마다");
            // 범례
            float lx = b.x + S.RowPadL;
            foreach (int t in teams)
            {
                lx = Mark(lx, b.y + D.LineBody * 0.5f, t, D.TeamMarkW, D.TeamMarkSmallH, D.ActionGap);
                string name = s.Team(t).Name;
                float nw = Mathf.Ceil(Draw.TextWidth(name, D.FontDesc, t == 0));
                Line(new Rect(lx, b.y, nw + D.Gap, D.LineBody), name, t == 0 ? Look.BarInk : Look.BarOff, t == 0, D.FontDesc);
                lx += nw + D.CardColGap;
            }
            var chart = new Rect(b.x, b.y + D.LineBody + S.HeadGap, b.width, D.TrendH - D.LineSub - D.ActionGap);
            var plot = new Rect(chart.x + D.TrendAxisW, chart.y + D.LineSub * 0.5f, chart.width - D.TrendAxisW - D.TrendAxisW * 0.5f, chart.height - D.LineSub);
            System.Func<float, float> Y = p => plot.yMax - (Mathf.Clamp(p, TrendLow, TrendHigh) - TrendLow) / (TrendHigh - TrendLow) * plot.height;
            for (int k = 0; k < TrendGrid; k++)
            {
                float p = TrendHigh - (TrendHigh - TrendLow) * k / (TrendGrid - 1), gy = Y(p);
                if (Draw.Repaint) Draw.Fill(new Rect(plot.x, Mathf.Round(gy), plot.width, 1f), S.PlateLine);
                Line(new Rect(chart.x, gy - D.LineSub * 0.5f, D.TrendAxisW - D.ActionGap, D.LineSub), Pct(p), Look.PillSub, false, D.FontMin, TextAnchor.MiddleRight);
            }
            var series = new List<List<float>>();
            int points = 0;
            foreach (int t in teams)
            {
                var pts = TrendCached(s, v, t);
                series.Add(pts);
                points = Mathf.Max(points, pts.Count);
            }
            if (points < 2)
            {
                Line(new Rect(plot.x, plot.y, plot.width, plot.height), $"{TrendStep * 2}경기가 끝나면 선이 그려져요", Look.PillSub, false, D.FontDesc, TextAnchor.MiddleCenter);
                return area.y + h;
            }
            System.Func<int, float> X = i => plot.x + plot.width * i / (points - 1);
            int every = Mathf.Max(1, Mathf.CeilToInt(points / (plot.width / (D.TrendAxisW * 1.5f))));
            for (int i = 0; i < points; i += every)
                Line(new Rect(X(i) - D.TrendAxisW * 0.5f, chart.yMax + D.ActionGap, D.TrendAxisW, D.LineSub), (i + 1) * TrendStep + "경기", Look.PillSub, false, D.FontMin, TextAnchor.MiddleCenter);
            // 내 팀 선을 맨 위로
            for (int pass = 0; pass < 2; pass++)
                for (int k = 0; k < teams.Count; k++)
                {
                    bool mine = teams[k] == 0;
                    if (mine != (pass == 1)) continue;
                    var pts = series[k];
                    if (pts.Count == 0) continue;
                    var line = new List<Vector2>();
                    for (int i = 0; i < pts.Count; i++) line.Add(new Vector2(X(i), Y(pts[i])));
                    var c = TeamColor(teams[k]);
                    Draw.Curve(line, mine ? D.TrendLineMine : D.TrendLine, c);
                    var end = line[line.Count - 1];
                    if (Draw.Repaint) Draw.Dot(end, D.TrendDot, c);
                    Line(new Rect(end.x + D.TrendDot + D.Gap, end.y - D.LineSub * 0.5f, D.TrendAxisW, D.LineSub), Pct(pts[pts.Count - 1]), mine ? Look.BarInk : Look.BarOff, mine, D.FontMin);
                }
            return area.y + h;
        }

        // ---- 포스트시즌 경쟁

        /// <summary>진출 자리 수(KBO 5 · NPB 리그별 3)와 그 자리 이름(1위부터).</summary>
        static string[] Spots(Rule rule) => rule == Rule.KBO ? new[] { "KS 직행", "PO", "준PO", "WC", "WC" } : new[] { "파이널", "퍼스트", "퍼스트" };

        float Race(Season s, View v, Rect area)
        {
            int lg = Rules.LeagueOf(s.Rule, 0);
            var table = v.Tables[lg];
            var spots = Spots(s.Rule);
            int rows = Mathf.Min(table.Count, spots.Length + 2);
            float tileH = TileH(true);
            float body = D.TableRowH * rows + S.HeadGap + tileH;
            float h = PlateH(Mathf.Max(body, D.LineBody + S.HeadGap + D.TrendH));
            string sub = s.Rule == Rule.KBO ? "상위 5팀" : Rules.LeagueName(s.Rule, lg) + " 상위 3팀";
            var b = Plate(new Rect(area.x, area.y, area.width, h), "포스트시즌 경쟁", IconFlag, sub);
            float numW = Mathf.Ceil(Draw.TextWidth("12", D.FontDesc, true)), wlW = Mathf.Ceil(Draw.TextWidth("100-100", D.FontMin)), gapW = Mathf.Ceil(Draw.TextWidth("KS 직행", D.FontMin, true));
            var cut = table.Count > spots.Length ? table[spots.Length - 1] : null;
            for (int i = 0; i < rows; i++)
            {
                var t = table[i];
                var row = new Rect(b.x, b.y + D.TableRowH * i, b.width, D.TableRowH);
                if (t.Team == 0) S.Mark(row);
                float x = row.x + S.RowPadL, right = row.xMax - S.RowPadR;
                Line(new Rect(x, row.y, numW, row.height), (i + 1).ToString(), S.SubInk, true, D.FontDesc, TextAnchor.MiddleRight);
                x = Mark(x + numW + S.RowGap, row.center.y, t.Team);
                // 진출 자리 안 = 자리 이름, 밖 = 마지막 진출 팀과 게임 차
                string gap = i < spots.Length ? spots[i] : Gb(((cut.W - t.W) + (t.L - cut.L)) * 0.5f);
                Line(new Rect(right - gapW, row.y, gapW, row.height), gap, i < spots.Length ? Look.PastelInk : Look.PillSub, i < spots.Length, D.FontMin, TextAnchor.MiddleRight);
                Line(new Rect(right - gapW - S.RowGap - wlW, row.y, wlW, row.height), $"{t.W}-{t.L}", Look.PillSub, false, D.FontMin, TextAnchor.MiddleRight);
                Line(new Rect(x, row.y, right - gapW - wlW - S.RowGap * 2f - x, row.height), s.Team(t.Team).Name, Look.BarInk, true);
            }

            // 아래 타일: 내 팀과 경계 팀의 게임 차 · 남은 경기
            var tile = new Rect(b.x, b.yMax - tileH, b.width, tileH);
            if (Draw.Repaint) Draw.Round(tile, Look.Pill, Tile.Radius);
            var ti = new Rect(tile.x + Tile.PadX, tile.y, tile.width - Tile.PadX * 2f, tile.height);
            int mine = v.Rank[0];
            var other = mine <= spots.Length ? (table.Count > spots.Length ? table[spots.Length] : null) : cut;
            string edge = other == null ? "-" : Gb(Mathf.Abs(((other.W - v.Records[0].W) + (v.Records[0].L - other.L)) * 0.5f)) + "경기";
            string edgeName = mine <= spots.Length ? $"{spots.Length + 1}위와 차이" : $"{spots.Length}위와 차이";
            string rule = s.Rule == Rule.KBO ? "1위 한국시리즈 직행 · 4 · 5위 와일드카드" : "1위 파이널 직행 · 2 · 3위 퍼스트";
            Stat.In(Cell(ti, 2, 0, D.ActionGap), edgeName, edge, rule);
            Stat.In(Cell(ti, 2, 1, D.ActionGap), "남은 경기", (v.MyGames - v.MyPlayed) + "경기", $"{s.Team(0).Name} {mine}위");
            return area.y + h;
        }

        // ---- 우승 이력

        float History(Season s, float y, float w)
        {
            var titles = s.Titles();
            const string note = "시즌이 끝나면 포스트시즌 결과와 MVP가 여기에 쌓이고, 우승 팀은 모드 알림 카드로 알려요";
            float nh = NoteH(note, w - S.Pad * 2f);
            float h = PlateH(S.TagH + S.RowH * Mathf.Max(1, titles.Count) + S.HeadGap + nh);
            var b = Plate(new Rect(0f, y, w, h), "우승 이력", TabIcons[TabRecords], titles.Count > 0 ? $"이 도시 리그 {titles.Count}시즌" : null);
            float yearW = Mathf.Ceil(Draw.TextWidth("2026년", D.FontBody, true)), wlW = Mathf.Ceil(Draw.TextWidth("100-100", D.FontDesc)), usW = Mathf.Ceil(Draw.TextWidth("한국시리즈 준우승", D.FontBody, true));
            float x0 = b.x + S.RowPadL, right = b.xMax - S.RowPadR, col = (right - x0 - yearW - wlW - usW - S.RowGap * 5f) / 3f;
            float c1 = x0 + yearW + S.RowGap, cw = c1 + col + S.RowGap, c2 = cw + wlW + S.RowGap, c3 = c2 + col + S.RowGap;
            Line(new Rect(x0, b.y, yearW, S.TagH), "시즌", S.SubInk, false, D.FontMin);
            Line(new Rect(c1, b.y, col, S.TagH), "우승", S.SubInk, false, D.FontMin);
            Line(new Rect(cw, b.y, wlW, S.TagH), "정규 시즌", S.SubInk, false, D.FontMin);
            Line(new Rect(c2, b.y, col, S.TagH), "준우승", S.SubInk, false, D.FontMin);
            Line(new Rect(c3, b.y, col, S.TagH), "MVP", S.SubInk, false, D.FontMin);
            Line(new Rect(right - usW, b.y, usW, S.TagH), s.Team(0).Name, S.SubInk, false, D.FontMin, TextAnchor.MiddleRight);
            float ry = b.y + S.TagH;
            if (titles.Count == 0) Line(new Rect(x0, ry, right - x0, S.RowH), "아직 우승 기록이 없어요", Look.PillSub);
            string final = Rules.RoundOf(s.Rule, Rules.RoundCount(s.Rule) - 1).Name;
            for (int i = 0; i < titles.Count; i++)
            {
                var t = titles[titles.Count - 1 - i]; // 최근 먼저
                var row = new Rect(b.x, ry + S.RowH * i, b.width, S.RowH);
                if (t.Champion == 0) S.Mark(row);
                Line(new Rect(x0, row.y, yearW, row.height), t.Year + "년", Look.BarInk, true);
                float tx = Mark(c1, row.center.y, t.Champion);
                Line(new Rect(tx, row.y, c1 + col - tx, row.height), s.Team(t.Champion).Name, Look.BarInk, true);
                Line(new Rect(cw, row.y, wlW, row.height), t.W + t.L == 0 ? "-" : $"{t.W}-{t.L}", Look.BarOff, false, D.FontDesc);
                Line(new Rect(c2, row.y, col, row.height), s.Team(t.Runner).Name, Look.BarOff);
                Line(new Rect(c3, row.y, col, row.height), t.Mvp.Length == 0 ? "-" : $"{t.Mvp} ({Short(s, t.MvpTeam)})", Look.BarOff, false, D.FontDesc);
                string us = t.Champion == 0 ? "우승" : t.Runner == 0 ? final + " 준우승" : "-";
                Line(new Rect(right - usW, row.y, usW, row.height), us, t.Champion == 0 || t.Runner == 0 ? Look.PastelInk : Look.BarOff, true, D.FontBody, TextAnchor.MiddleRight);
            }
            Note(new Rect(b.x, b.yMax - nh, b.width, nh), note);
            return y + h;
        }
    }
}
