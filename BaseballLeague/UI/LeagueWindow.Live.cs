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
    /// 경기 상세(시안 GameLive, 진입: 일정 › 오늘 리그 경기 · 진행 중인 경기만): 오늘 리그 경기 줄(진행 중만 눌러 바꿈) · 점수판 히어로(두 팀 · 점수 · 이닝 · 주자 · S/B/O) ·
    /// 타석 · 투수 줄 · 이닝 점수 · 이번 경기 기록 · 상대 전적 · 문자 중계. 시뮬은 Live 가 담아 둔 것을 진행 비율만큼 보여 줌(매 프레임 다시 돌리지 않음).
    /// 시안의 도루 줄은 시뮬에 도루가 없어 실책 줄로 바꿈.
    /// </summary>
    internal sealed partial class LeagueWindow
    {
        int liveIndex = -1;

        static readonly string[] Kinds = { "안타", "2루타", "3루타", "홈런", "볼넷", "삼진 아웃", "땅볼 아웃", "뜬공 아웃", "직선타 아웃", "병살타", "희생 뜬공", "실책으로 출루", "투수 교체" };

        const string IconFeed = "M4 6h16M4 12h16M4 18h10", IconVersus = "M7 7h10M7 12h10M7 17h6M4 4h16v16H4z";

        static string KindText(PlayKind k) => Kinds[(int)k];

        static string Half(int inning, bool bottom) => inning + (bottom ? "회말" : "회초");

        static string OutsText(int outs) => outs == 0 ? "무사" : outs >= 3 ? "3아웃" : outs + "사";

        static string BasesText(int b)
        {
            if (b == 7) return "만루";
            if (b == 0) return "주자 없음";
            var parts = new List<string>();
            for (int i = 0; i < 3; i++)
                if ((b & (1 << i)) != 0) parts.Add((i + 1).ToString());
            return string.Join(" · ", parts.ToArray()) + "루";
        }

        /// <summary>오늘 경기(날짜 · 시각 차례)와 그 경기 번호.</summary>
        List<int> TodayIndexes(Season s) => DayIndexes(s, Today);

        /// <summary>그날 경기(시각 차례)와 그 경기 번호.</summary>
        List<int> DayIndexes(Season s, System.DateTime day)
        {
            var list = new List<int>();
            for (int i = 0; i < s.Games.Count; i++)
                if (s.Games[i].Day == day) list.Add(i);
            list.Sort((a, b) => Earlier(s.Games[a], s.Games[b]) ? -1 : Earlier(s.Games[b], s.Games[a]) ? 1 : 0);
            return list;
        }

        /// <summary>경기 상세를 연 경기(없는 번호면 null → 일정으로). 지난 · 앞으로 경기도 열림.</summary>
        Game LiveGame(Season s) => liveIndex >= 0 && liveIndex < s.Games.Count ? s.Games[liveIndex] : null;

        void OpenLive(int index)
        {
            liveIndex = index;
            Go(TabSchedule, Page.Live);
        }

        string LiveTitle(Season s)
        {
            var g = s == null ? null : LiveGame(s);
            return g == null ? "경기 상세" : $"경기 상세 · {s.Team(g.Away).Name} vs {s.Team(g.Home).Name}";
        }

        string LiveSub(Season s)
        {
            var g = LiveGame(s);
            if (g == null) return "";
            string number = g.Series != 0 ? RoundName(s, s.Post[g.Series - 1]) : $"{GameNumber(s, g, g.Home)} / {Rules.RegularGames(s.Rule)}경기";
            int crowd = g.Box != null ? g.Box.Crowd : League.Expected(s, g, ViewOf(s).Rank);
            return $"{g.Day.Month}월 {g.Day.Day}일 · {Park(s, g.Home)} · {number} · 관중 {crowd:N0}";
        }

        string LiveStatus(Season s)
        {
            var g = LiveGame(s);
            if (g == null) return "";
            var st = StateOf(s, g, liveIndex, out float t);
            if (st != Live.State.Live) return st == Live.State.Before ? "경기 전" : st == Live.State.Rain ? "우천 취소" : "경기 끝";
            var f = Live.At(Live.Of(s, g, liveIndex, Today), t);
            return "경기 중 · " + Half(f.Inning, f.Bottom);
        }

        /// <summary>team 의 정규 시즌 몇 번째 경기인지(1부터).</summary>
        static int GameNumber(Season s, Game g, int team)
        {
            int n = 1;
            foreach (var o in s.Games)
                if (o != g && o.Series == 0 && (o.Home == team || o.Away == team) && Earlier(o, g)) n++;
            return n;
        }

        float LivePage(Season s, View v, float w)
        {
            var g = LiveGame(s);
            if (g == null)
            {
                Go(TabSchedule);
                return 0f;
            }
            var st = StateOf(s, g, liveIndex, out float t);
            var r = Live.Of(s, g, liveIndex, Today);
            if (st != Live.State.Live) t = st == Live.State.Before || st == Live.State.Rain ? 0f : 1f;
            var f = Live.At(r, t);
            float y = LiveStrip(s, g.Day, w);
            y = LiveHero(s, v, g, r, f, st, y, w);
            float col = (w - S.Gap * 2f) / 3f;
            float a = LiveCompare(s, g, r, f, new Rect(0f, y, col, 0f));
            float b = HeadToHead(s, g, new Rect(col + S.Gap, y, col, 0f));
            float c = Feed(s, g, r, f, st, new Rect((col + S.Gap) * 2f, y, col, 0f));
            y = Mathf.Max(a, Mathf.Max(b, c)) + S.Gap;
            const string note = "일정 탭의 오늘 경기 · 달력 칸에서 열려요. 위 줄에서 같은 날 다른 경기로 바꿀 수 있어요";
            float nh = NoteH(note, w - S.RowPadL * 2f);
            Note(new Rect(S.RowPadL, y, w - S.RowPadL * 2f, nh), note);
            return y + nh;
        }

        // ---- 그날 리그 경기 줄(눌러 바꿈)

        float LiveStrip(Season s, System.DateTime day, float w)
        {
            var today = DayIndexes(s, day);
            var row = new Rect(0f, 0f, w, D.LiveTileH);
            int n = Mathf.Max(1, today.Count);
            for (int k = 0; k < today.Count; k++)
            {
                int i = today[k];
                var g = s.Games[i];
                var c = Cell(row, n, k, D.ActionGap);
                var st = StateOf(s, g, i, out float t);
                bool live = st == Live.State.Live, on = i == liveIndex;
                if (Tile.In(c, on, live, null, () => StripContent(s, g, i, c, st, t, on, live)) && !on) OpenLive(i);
            }
            return D.LiveTileH + S.Gap;
        }

        void StripContent(Season s, Game g, int i, Rect c, Live.State st, float t, bool on, bool live)
        {
            if (!Draw.Repaint) return;
            string state, sa = "-", sh = "-";
            if (st == Live.State.Rain) state = "우천 취소";
            else if (st == Live.State.Before) state = Clock(g.Minute);
            else
            {
                var f = Live.At(Live.Of(s, g, i, Today), live ? t : 1f);
                state = live ? Half(f.Inning, f.Bottom) : "경기 끝";
                sa = f.Away.ToString();
                sh = f.Home.ToString();
            }
            Color ink = Tile.Ink(on, true), sub = Tile.Sub(on, true);
            if (!live) ink = sub = Look.BtnOffInk;
            float x = c.x + Tile.PadX, right = c.xMax - Tile.PadX, top = Mathf.Round(c.center.y - (Badge.CountH + D.ActionGap + D.LineBody * 2f) * 0.5f);
            float bw = Badge.Pair(0f, 0f, state, Look.WarnFill, Look.WarnInk, false);
            Badge.Pair(x, top + Badge.CountH * 0.5f, state, live ? Look.WarnFill : Look.Pill, live ? Look.WarnInk : Look.BtnOffInk);
            Line(new Rect(x + bw + D.Gap, top, right - x - bw - D.Gap, Badge.CountH), Park(s, g.Home), sub, false, D.FontMin, TextAnchor.MiddleRight);
            float ly = top + Badge.CountH + D.ActionGap;
            StripTeam(s, g.Away, sa, new Rect(x, ly, right - x, D.LineBody), ink);
            StripTeam(s, g.Home, sh, new Rect(x, ly + D.LineBody, right - x, D.LineBody), ink);
        }

        static void StripTeam(Season s, int team, string score, Rect r, Color ink)
        {
            float tx = Mark(r.x, r.center.y, team, D.TeamMarkW, D.TeamMarkSmallH, D.IconGap);
            float sw = Mathf.Ceil(Draw.TextWidth(score, S.ChipFont, true));
            Line(new Rect(r.xMax - sw, r.y, sw, r.height), score, ink, true, S.ChipFont, TextAnchor.MiddleRight);
            Line(new Rect(tx, r.y, r.xMax - sw - D.Gap - tx, r.height), s.Team(team).Name, ink, true, S.ChipFont);
        }

        // ---- 점수판 히어로

        float LiveHero(Season s, View v, Game g, Result r, Live.Frame f, Live.State st, float y, float w)
        {
            float lineH = D.LineHeadH + D.TableRowH * 2f;
            float h = S.HeroPadY * 2f + D.LiveSideH + S.HeroGap + D.LiveBarH + S.HeroGap + lineH;
            var box = new Rect(0f, y, w, h);
            HeroFrame(box);
            float x = S.HeroPadX, top = y + S.HeroPadY, inner = w - S.HeroPadX * 2f;
            var side = new Rect(x + inner - D.LiveSideW, top, D.LiveSideW, D.LiveSideH);
            var score = new Rect(x, top, inner - D.LiveSideW - S.HeroStatGap, D.LiveSideH);
            ScoreRow(s, v, g, r, f, st, score);
            Field(side, f, st == Live.State.Live);
            float by = top + D.LiveSideH + S.HeroGap;
            AtBat(s, g, r, f, st, new Rect(x, by, inner, D.LiveBarH));
            LineScore(s, g, r, f, st, new Rect(x, by + D.LiveBarH + S.HeroGap, inner, lineH));
            return y + h + S.Gap;
        }

        void ScoreRow(Season s, View v, Game g, Result r, Live.Frame f, Live.State st, Rect a)
        {
            float cy = a.center.y;
            // 가운데: 이닝 알약 · 아웃
            string inning = st == Live.State.Live ? Half(f.Inning, f.Bottom) : st == Live.State.Before ? Clock(g.Minute) : st == Live.State.Rain ? "우천 취소" : "경기 끝";
            float iw = Badge.Pair(0f, 0f, inning, S.Plate, Look.BarInk, false);
            float cx = a.center.x, ty = Mathf.Round(cy - (Badge.H + D.Gap + D.LineSub) * 0.5f);
            var pill = new Rect(Mathf.Round(cx - iw * 0.5f), ty, iw, Badge.H);
            if (Draw.Repaint)
            {
                Draw.RoundOutline(pill, S.PlateLine, Badge.H * 0.5f);
                Badge.Pair(pill.x, pill.center.y, inning, S.Plate, Look.BarInk);
            }
            if (st == Live.State.Live) Line(new Rect(pill.x - iw, pill.yMax + D.Gap, iw * 3f, D.LineSub), OutsText(f.Outs), Look.PillSub, false, D.FontMin, TextAnchor.MiddleCenter);
            // 점수(지는 쪽 회색)
            string sa = st == Live.State.Before || st == Live.State.Rain ? "-" : f.Away.ToString(), sh = st == Live.State.Before || st == Live.State.Rain ? "-" : f.Home.ToString();
            float nw = Mathf.Ceil(Mathf.Max(Draw.TextWidth(sa, D.HeroNumFont, true), Draw.TextWidth(sh, D.HeroNumFont, true)));
            float gap = S.HeroStatGap;
            var ra = new Rect(pill.x - gap - nw, Mathf.Round(cy - D.HeroNumH * 0.5f), nw, D.HeroNumH);
            var rh = new Rect(pill.xMax + gap, ra.y, nw, D.HeroNumH);
            Line(ra, sa, f.Away < f.Home ? Look.BarOff : Look.BarInk, true, D.HeroNumFont, TextAnchor.MiddleRight);
            Line(rh, sh, f.Home < f.Away ? Look.BarOff : Look.BarInk, true, D.HeroNumFont, TextAnchor.MiddleLeft);
            // 두 팀(원정 왼쪽 · 홈 오른쪽)
            HeroSide(s, v, g, r, f, st, 0, new Rect(a.x, a.y, ra.x - gap - a.x, a.height));
            HeroSide(s, v, g, r, f, st, 1, new Rect(rh.xMax + gap, a.y, a.xMax - rh.xMax - gap, a.height));
        }

        /// <summary>팀 쪽: 색 막대 · '원정 · 1위' · 이름 · 선발(투구 · 이닝 실점).</summary>
        void HeroSide(Season s, View v, Game g, Result r, Live.Frame f, Live.State st, int side, Rect a)
        {
            int team = side == 0 ? g.Away : g.Home;
            bool right = side == 1;
            float th = D.LineSub + D.LiveNameH + D.LineSub, top = Mathf.Round(a.center.y - th * 0.5f);
            float mx = right ? a.xMax - D.LiveMarkW : a.x;
            if (Draw.Repaint) Draw.Round(new Rect(mx, Mathf.Round(a.center.y - D.LiveMarkH * 0.5f), D.LiveMarkW, D.LiveMarkH), TeamColor(team), D.LiveMarkW * 0.5f);
            var text = right ? new Rect(a.x, top, a.width - D.LiveMarkW - S.RowGap, th) : new Rect(a.x + D.LiveMarkW + S.RowGap, top, a.width - D.LiveMarkW - S.RowGap, th);
            var anchor = right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            int sp = r.Box.SP[side];
            var p = s.RosterOf(team).P[sp];
            string pitch;
            bool battingBottom = side == 0; // 이 팀 투수는 상대가 칠 때 던짐
            Live.PitcherToday(r, f.Done, battingBottom, sp, out int outs, out int runs);
            if (st == Live.State.Before || st == Live.State.Rain) pitch = "선발 " + p.Name;
            else if (f.Pitcher == sp && f.Bottom == battingBottom && st == Live.State.Live) pitch = $"선발 {p.Name} · 투구 {f.Pitches}";
            else pitch = $"선발 {p.Name} · {outs / 3}.{outs % 3}이닝 {runs}실점";
            Line(new Rect(text.x, text.y, text.width, D.LineSub), (right ? "홈" : "원정") + $" · {v.Rank[team]}위", Look.PillSub, false, D.FontMin, anchor);
            Line(new Rect(text.x, text.y + D.LineSub, text.width, D.LiveNameH), s.Team(team).Name, Look.BarInk, true, D.LiveNameFont, anchor);
            Line(new Rect(text.x, text.y + D.LineSub + D.LiveNameH, text.width, D.LineSub), pitch, Look.PillSub, false, D.FontMin, anchor);
        }

        /// <summary>흰 칸: 주자 다이아몬드(채운 루 = 초록) · S/B/O(B 초록 3 · S 노랑 2 · O 빨강 2).</summary>
        static void Field(Rect r, Live.Frame f, bool live)
        {
            if (!Draw.Repaint) return;
            Draw.Round(r, S.Plate, Tile.Radius);
            Draw.RoundOutline(r, S.PlateLine, Tile.Radius);
            var dia = new Rect(r.x + D.FieldGap, Mathf.Round(r.center.y - D.DiamondD * 0.5f), D.DiamondD, D.DiamondD);
            int bases = live ? f.Bases : 0;
            float off = D.DiamondD * 0.5f - D.BaseD;
            var c = dia.center;
            Base(new Vector2(c.x + off, c.y), (bases & 1) != 0);
            Base(new Vector2(c.x, c.y - off), (bases & 2) != 0);
            Base(new Vector2(c.x - off, c.y), (bases & 4) != 0);
            // 홈 플레이트(오각형 선)
            float hb = D.BaseD * 0.5f, hy = c.y + off - hb * 0.5f;
            var plate = new List<Vector2> { new(c.x - hb, hy), new(c.x + hb, hy), new(c.x + hb, hy + hb), new(c.x, hy + hb * 2f), new(c.x - hb, hy + hb) };
            Draw.Polyline(plate, D.BaseLine, Pal.Line2, true);
            float x = dia.xMax + D.FieldGap, rows = D.CountRowH * 3f + D.CountRowGap * 2f, y = Mathf.Round(r.center.y - rows * 0.5f);
            Count(x, y, "B", live ? f.Balls : 0, 3, Pal.Primary);
            Count(x, y + D.CountRowH + D.CountRowGap, "S", live ? f.Strikes : 0, 2, Pal.BallStrike);
            Count(x, y + (D.CountRowH + D.CountRowGap) * 2f, "O", live ? f.Outs : 0, 2, Pal.BallOut);
        }

        static void Base(Vector2 c, bool on)
        {
            // 45° 돌린 정사각형: 대각선 방향 굵은 선(채움) + 네 꼭짓점 선(테두리)
            float h = D.BaseD * 0.5f, k = h * Mathf.Sqrt(0.5f), e = h * Mathf.Sqrt(2f);
            Draw.Line(new Vector2(c.x - k, c.y - k), new Vector2(c.x + k, c.y + k), D.BaseD, on ? Pal.Primary : S.Plate);
            var corners = new List<Vector2> { new(c.x, c.y - e), new(c.x + e, c.y), new(c.x, c.y + e), new(c.x - e, c.y) };
            Draw.Polyline(corners, D.BaseLine, on ? Pal.Primary : Pal.Line2, true);
        }

        static void Count(float x, float y, string k, int n, int max, Color color)
        {
            Draw.Text(new Rect(x, y, D.CountDot, D.CountRowH), k, D.FontDesc, Look.BarOff, true, TextAnchor.MiddleCenter);
            float dx = x + D.CountDot + D.ActionGap, cy = y + D.CountRowH * 0.5f, rad = D.CountDot * 0.5f;
            for (int i = 0; i < max; i++)
            {
                var c = new Vector2(dx + rad + (D.CountDot + D.ActionGap) * i, cy);
                if (i < Mathf.Min(n, max)) Draw.Dot(c, rad, color);
                else
                {
                    Draw.Dot(c, rad, S.Plate);
                    Draw.Ring(c, rad, D.CountRing, Look.PillLine);
                }
            }
        }

        /// <summary>타석 · 투수 줄(흰 칸): 타자(타순 · 자리 · 시즌 타율 · 오늘) | 투수(투구 · 시즌 평균자책 · 오늘) · 오른쪽 다음 타자.</summary>
        void AtBat(Season s, Game g, Result r, Live.Frame f, Live.State st, Rect a)
        {
            if (Draw.Repaint)
            {
                Draw.Round(a, S.Plate, Tile.Radius);
                Draw.RoundOutline(a, S.PlateLine, Tile.Radius);
            }
            float x = a.x + Tile.PadX, cy = a.center.y, right = a.xMax - Tile.PadX;
            if (st != Live.State.Live)
            {
                string text = st == Live.State.Before ? $"플레이볼 {Clock(g.Minute)} · 선발 {s.RosterOf(g.Away).P[r.Box.SP[0]].Name} vs {s.RosterOf(g.Home).P[r.Box.SP[1]].Name}" : st == Live.State.Rain ? "비로 취소돼 다른 날 다시 열려요" : FinalLine(s, g, r);
                Line(new Rect(x, a.y, right - x, a.height), text, Look.BarInk, true);
                return;
            }
            int bat = f.Bottom ? g.Home : g.Away, fld = f.Bottom ? g.Away : g.Home;
            var batter = s.RosterOf(bat).P[f.Batter];
            var pitcher = s.RosterOf(fld).P[f.Pitcher];
            Live.BatterToday(r, f.Done, f.Bottom, f.Batter, out int ab, out int h);
            Live.PitcherToday(r, f.Done, f.Bottom, f.Pitcher, out int outs, out int runs);
            var season = s.StatOf(bat, f.Batter);
            var ps = s.StatOf(fld, f.Pitcher);
            int nextSlot = (f.Batter + 1) % Roster.Lineup;
            string next = $"다음 타자 {s.RosterOf(bat).P[nextSlot].Name} · {s.StatOf(bat, nextSlot)[L.HR]}홈런";
            float nw = Mathf.Ceil(Draw.TextWidth(next, D.FontMin));
            Line(new Rect(right - nw, a.y, nw, a.height), next, Look.PillSub, false, D.FontMin, TextAnchor.MiddleRight);
            x += Badge.Pair(x, cy, "타석", Look.PastelFill, Look.PastelInk) + S.RowGap;
            x = Name(x, a, batter.Name);
            x = Sub(x, a, $"{f.Batter + 1}번 · {Roster.Position(f.Batter)} · 타율 {Pct(season.Avg)} · 오늘 {ab}타수 {h}안타");
            if (Draw.Repaint) Draw.Fill(new Rect(x, Mathf.Round(cy - D.DividerH * 0.5f), 1f, D.DividerH), S.PlateLine);
            x += S.RowGap;
            x += Badge.Pair(x, cy, "투수", Look.Pill, Look.BarOff) + S.RowGap;
            x = Name(x, a, pitcher.Name);
            Sub(x, a, $"투구 {f.Pitches} · 평균자책 {ps.Era:0.00} · 오늘 {outs / 3}.{outs % 3}이닝 {runs}실점", right - nw - S.RowGap);
        }

        static float Name(float x, Rect a, string name)
        {
            float w = Mathf.Ceil(Draw.TextWidth(name, D.FontValue, true));
            Line(new Rect(x, a.y, w + D.Gap, a.height), name, Look.BarInk, true, D.FontValue);
            return x + w + S.RowGap;
        }

        static float Sub(float x, Rect a, string text, float limit = float.MaxValue)
        {
            float w = Mathf.Min(Mathf.Ceil(Draw.TextWidth(text, D.FontMin)), limit - x);
            Line(new Rect(x, a.y, w, a.height), text, Look.PillSub, false, D.FontMin);
            return x + w + S.RowGap;
        }

        /// <summary>끝난 경기 한 줄: 승리 · 패전 · 세이브 투수.</summary>
        static string FinalLine(Season s, Game g, Result r)
        {
            var b = r.Box;
            if (r.Away == r.Home || b.Win < 0) return "경기가 끝났어요";
            int win = r.Home > r.Away ? g.Home : g.Away, lose = win == g.Home ? g.Away : g.Home;
            string t = $"승리 투수 {s.RosterOf(win).P[b.Win].Name} · 패전 투수 {s.RosterOf(lose).P[b.Lose].Name}";
            return b.Save >= 0 ? t + $" · 세이브 {s.RosterOf(win).P[b.Save].Name}" : t;
        }

        /// <summary>이닝 점수(흰 칸): 이닝 · 1 ~ 9(연장이면 더) · R H E B, 지금 이닝 칸 파스텔.</summary>
        void LineScore(Season s, Game g, Result r, Live.Frame f, Live.State st, Rect a)
        {
            if (Draw.Repaint)
            {
                Draw.Round(a, S.Plate, Tile.Radius);
                Draw.RoundOutline(a, S.PlateLine, Tile.Radius);
            }
            bool shown = st == Live.State.Live || st == Live.State.Over || st == Live.State.Done;
            int innings = Mathf.Max(Sim.Innings, f.Inn[0].Count);
            float x0 = a.x + S.RowPadR, right = a.xMax - S.RowPadR;
            float totals = D.LineTotW * 4f, cells = right - totals - S.RowGap - (x0 + D.LineNameW);
            float cw = cells / innings;
            var head = new Rect(a.x, a.y, a.width, D.LineHeadH);
            Line(new Rect(x0, head.y, D.LineNameW, head.height), "이닝", S.SubInk, false, D.FontMin);
            for (int i = 0; i < innings; i++)
            {
                bool now = st == Live.State.Live && i + 1 == f.Inning;
                Line(new Rect(x0 + D.LineNameW + cw * i, head.y, cw, head.height), (i + 1).ToString(), now ? Look.PastelInk : S.SubInk, now, D.FontMin, TextAnchor.MiddleCenter);
            }
            string[] cols = { "R", "H", "E", "B" };
            for (int k = 0; k < cols.Length; k++) Line(new Rect(right - totals + D.LineTotW * k, head.y, D.LineTotW, head.height), cols[k], S.SubInk, false, D.FontMin, TextAnchor.MiddleCenter);
            S.Hairline(new Rect(a.x, head.yMax, a.width, 1f));
            int[] hits = new int[2], errors = new int[2], walks = new int[2];
            for (int i = 0; i < f.Done; i++)
            {
                var p = r.Plays[i];
                int side = p.Bottom ? 1 : 0;
                if (p.Kind <= PlayKind.HomeRun) hits[side]++;
                else if (p.Kind == PlayKind.Walk) walks[side]++;
                else if (p.Kind == PlayKind.Error) errors[1 - side]++;
            }
            for (int side = 0; side < 2; side++)
            {
                int team = side == 0 ? g.Away : g.Home;
                var row = new Rect(a.x, head.yMax + D.TableRowH * side, a.width, D.TableRowH);
                if (side == 0) S.Hairline(new Rect(a.x, row.yMax, a.width, 1f));
                float tx = Mark(x0, row.center.y, team, D.TeamMarkW, D.TeamMarkH, D.ActionGap);
                Line(new Rect(tx, row.y, x0 + D.LineNameW - tx, row.height), s.Team(team).Name, Look.BarInk, true);
                var inn = f.Inn[side];
                for (int i = 0; i < innings; i++)
                {
                    bool has = shown && i < inn.Count;
                    var cell = new Rect(x0 + D.LineNameW + cw * i + D.LineCellPad, Mathf.Round(row.center.y - S.TagH * 0.5f), cw - D.LineCellPad * 2f, S.TagH);
                    bool now = st == Live.State.Live && i + 1 == f.Inning && has;
                    if (now && Draw.Repaint) Draw.Round(cell, Look.PastelFill, D.LineCellRadius);
                    Line(cell, has ? inn[i].ToString() : "", now ? Look.PastelInk : Look.BarInk, now, D.FontBody, TextAnchor.MiddleCenter);
                }
                string[] vals = { shown ? (side == 0 ? f.Away : f.Home).ToString() : "-", shown ? hits[side].ToString() : "-", shown ? errors[side].ToString() : "-", shown ? walks[side].ToString() : "-" };
                for (int k = 0; k < vals.Length; k++) Line(new Rect(right - totals + D.LineTotW * k, row.y, D.LineTotW, row.height), vals[k], Look.BarInk, k == 0, D.FontBody, TextAnchor.MiddleCenter);
            }
        }

        // ---- 아래 세 판

        /// <summary>이번 경기 기록: 두 팀 막대 비교(안타 · 홈런 · 볼넷 · 삼진 · 잔루 · 실책).</summary>
        float LiveCompare(Season s, Game g, Result r, Live.Frame f, Rect area)
        {
            string[] names = { "안타", "홈런", "볼넷", "삼진", "잔루", "실책" };
            var a = new int[names.Length];
            var b = new int[names.Length];
            for (int i = 0; i < f.Done; i++)
            {
                var p = r.Plays[i];
                var t = p.Bottom ? b : a;
                if (p.Kind <= PlayKind.HomeRun) t[0]++;
                if (p.Kind == PlayKind.HomeRun) t[1]++;
                if (p.Kind == PlayKind.Walk) t[2]++;
                if (p.Kind == PlayKind.Strikeout) t[3]++;
                if (p.Kind != PlayKind.Change && p.Outs == 3) t[4] += (p.Bases & 1) + (p.Bases >> 1 & 1) + (p.Bases >> 2 & 1);
                if (p.Kind == PlayKind.Error) (p.Bottom ? a : b)[5]++;
            }
            float h = PlateH(D.LineSub + D.CompareRowH * names.Length);
            var body = Plate(new Rect(area.x, area.y, area.width, h), "이번 경기 기록", IconChart);
            Line(new Rect(body.x, body.y, D.LineTotW, D.LineSub), s.Abbr(g.Away), S.SubInk, false, D.FontMin);
            Line(new Rect(body.xMax - D.LineTotW, body.y, D.LineTotW, D.LineSub), s.Abbr(g.Home), S.SubInk, false, D.FontMin, TextAnchor.MiddleRight);
            float barW = (body.width - D.LineTotW * 2f - D.CompareLabelW - S.RowGap * 2f) * 0.5f;
            for (int k = 0; k < names.Length; k++)
            {
                var row = new Rect(body.x, body.y + D.LineSub + D.CompareRowH * k, body.width, D.CompareRowH);
                int m = Mathf.Max(1, Mathf.Max(a[k], b[k]));
                Line(new Rect(row.x, row.y, D.LineTotW, row.height), a[k].ToString(), Look.BarInk, true, S.ChipFont);
                Line(new Rect(row.xMax - D.LineTotW, row.y, D.LineTotW, row.height), b[k].ToString(), Look.BarInk, true, S.ChipFont, TextAnchor.MiddleRight);
                float bx = row.x + D.LineTotW + S.RowGap * 0.5f, by = Mathf.Round(row.center.y - Progress.Bar * 0.5f);
                Bar(new Rect(bx, by, barW, Progress.Bar), (float)a[k] / m, true, TeamColor(g.Away));
                Line(new Rect(bx + barW, row.y, D.CompareLabelW + S.RowGap, row.height), names[k], Look.PillSub, false, D.FontMin, TextAnchor.MiddleCenter);
                Bar(new Rect(bx + barW + D.CompareLabelW + S.RowGap, by, barW, Progress.Bar), (float)b[k] / m, false, TeamColor(g.Home));
            }
            return area.y + h;
        }

        static void Bar(Rect r, float t, bool fromRight, Color c)
        {
            if (!Draw.Repaint) return;
            Draw.Round(r, S.PlateLine, r.height * 0.5f);
            float w = Mathf.Round(r.width * Mathf.Clamp01(t));
            if (w <= 0f) return;
            Draw.Round(new Rect(fromRight ? r.xMax - w : r.x, r.y, w, r.height), c, r.height * 0.5f);
        }

        /// <summary>상대 전적(홈 팀 기준): 시즌 · 이 구장 승패 타일 · 최근 맞대결 4경기.</summary>
        float HeadToHead(Season s, Game g, Rect area)
        {
            int me = g.Home, op = g.Away;
            int w = 0, l = 0, pw = 0, pl = 0, total = 0, played = 0;
            var last = new List<Game>();
            foreach (var o in Sorted(s, o => o.Series == 0 && (o.Home == me && o.Away == op || o.Home == op && o.Away == me)))
            {
                total++;
                if (!o.Played || o == g) continue;
                played++;
                int res = League.Result(o, me);
                if (res > 0) w++;
                else if (res < 0) l++;
                if (o.Home == g.Home)
                {
                    if (res > 0) pw++;
                    else if (res < 0) pl++;
                }
                last.Add(o);
            }
            last.Reverse();
            const int rows = 4;
            int shown = Mathf.Min(rows, last.Count);
            float tileH = TileH(true);
            float h = PlateH(tileH + D.ActionGap + D.TableRowH * Mathf.Max(1, shown));
            var b = Plate(new Rect(area.x, area.y, area.width, h), "상대 전적", IconVersus, $"{s.Start.Year} · {played} / {total}경기");
            var tiles = new Rect(b.x, b.y, b.width, tileH);
            StatTile(Cell(tiles, 2, 0, D.ActionGap), "시즌 상대 전적", WinLoss(w, l), Short(s, me) + " 기준", false);
            StatTile(Cell(tiles, 2, 1, D.ActionGap), Park(s, g.Home), WinLoss(pw, pl), pw > pl ? "홈에서 강해요" : pw < pl ? "홈에서 약해요" : "홈에서 팽팽해요", false);
            float y = b.y + tileH + D.ActionGap;
            if (shown == 0) Line(new Rect(b.x, y, b.width, D.TableRowH), "아직 맞대결이 없어요", Look.PillSub);
            float dw = Mathf.Ceil(Draw.TextWidth("12/31", D.FontMin));
            for (int i = 0; i < shown; i++)
            {
                var o = last[i];
                var row = new Rect(b.x, y + D.TableRowH * i, b.width, D.TableRowH);
                float x = row.x + S.RowPadL;
                Line(new Rect(x, row.y, dw, row.height), Md(o.Day), Look.PillSub, false, D.FontMin);
                x += dw + S.RowGap;
                ResultBadge(new Rect(x, Mathf.Round(row.center.y - Badge.H * 0.5f), S.RankD, Badge.H), League.Result(o, me));
                x += S.RankD + S.RowGap;
                string sc = o.Home == me ? $"{o.HomeRuns} : {o.AwayRuns}" : $"{o.AwayRuns} : {o.HomeRuns}";
                float sw = Mathf.Ceil(Draw.TextWidth(sc, D.FontBody, true));
                Line(new Rect(row.xMax - S.RowPadL - sw, row.y, sw, row.height), sc, Look.BarInk, true, D.FontBody, TextAnchor.MiddleRight);
                Line(new Rect(x, row.y, row.xMax - S.RowPadL - sw - S.RowGap - x, row.height), (o.Home == me ? "홈 · " : "원정 · ") + Park(s, o.Home), Look.BarInk, false, S.ChipFont);
            }
            return area.y + h;
        }

        /// <summary>문자 중계(최신순 6줄): 지금 타석(주황) · 끝난 타석 · 투수 교체.</summary>
        float Feed(Season s, Game g, Result r, Live.Frame f, Live.State st, Rect area)
        {
            const int rows = 6;
            var items = new List<string[]>(); // 이닝 · 글 · 보조 · 지금
            if (st == Live.State.Live)
            {
                int bat = f.Bottom ? g.Home : g.Away;
                items.Add(new[] { Half(f.Inning, f.Bottom), $"{s.RosterOf(bat).P[f.Batter].Name} 타석 · 볼 {f.Balls} 스트라이크 {f.Strikes}", $"{OutsText(f.Outs)} {BasesText(f.Bases)} · 투구 {f.Pitches}", "1" });
            }
            for (int i = f.Done - 1; i >= 0 && items.Count < rows; i--)
            {
                var p = r.Plays[i];
                int bat = p.Bottom ? g.Home : g.Away, fld = p.Bottom ? g.Away : g.Home;
                string text = p.Kind == PlayKind.Change ? $"투수 교체 · {s.RosterOf(fld).P[p.Pitcher].Name}" : $"{s.RosterOf(bat).P[p.Batter].Name} {KindText(p.Kind)}";
                string sub = p.Kind == PlayKind.Change ? $"{s.RosterOf(fld).P[p.Batter].Name} 내려감" : $"{OutsText(p.Outs)} {(p.Outs >= 3 ? "" : BasesText(p.Bases))}".Trim() + (p.Runs > 0 ? $" · {p.Runs}점" : "");
                items.Add(new[] { Half(p.Inning, p.Bottom), text, sub, "" });
            }
            if (st == Live.State.Before || st == Live.State.Rain) items.Clear();
            float h = PlateH(S.RowH * Mathf.Max(1, items.Count));
            var b = Plate(new Rect(area.x, area.y, area.width, h), "문자 중계", IconFeed, "최신순");
            if (items.Count == 0) Line(new Rect(b.x, b.y, b.width, S.RowH), st == Live.State.Rain ? "비로 취소된 경기예요" : "플레이볼 뒤 여기에 쌓여요", Look.PillSub);
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                bool now = it[3].Length > 0;
                var row = new Rect(b.x, b.y + S.RowH * i, b.width, S.RowH);
                if (i > 0) S.Hairline(new Rect(row.x, row.y, row.width, 1f));
                var badge = new Rect(row.x + S.RowPadL, Mathf.Round(row.center.y - Badge.H * 0.5f), D.FeedBadgeW, Badge.H);
                if (Draw.Repaint)
                {
                    Draw.Round(badge, now ? Look.WarnFill : Look.Pill, Badge.H * 0.5f);
                    Draw.TextPill(badge, it[0], D.FontMin, now ? Look.WarnInk : Look.BarOff, true, TextAnchor.MiddleCenter);
                }
                float x = badge.xMax + S.RowGap, top = Mathf.Round(row.center.y - (D.LineBody + D.LineSub) * 0.5f);
                Line(new Rect(x, top, row.xMax - x, D.LineBody), it[1], Look.BarInk, now, S.ChipFont);
                Line(new Rect(x, top + D.LineBody, row.xMax - x, D.LineSub), it[2], Look.PillSub, false, D.FontMin);
            }
            return area.y + h;
        }
    }
}
