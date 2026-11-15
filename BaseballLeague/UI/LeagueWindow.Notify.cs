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
    /// 알림 카드(시안 Notify · Chirper 대신, 창이 닫혀 있어도 게임 화면 위에 뜸): 오른쪽 위에 쌓이는 경기 전 · 경기 결과 카드, 가운데 한 번 큰 소식(포스트시즌 진출 · 우승 · MVP),
    /// 오른쪽 아래 LIVE 점수판(홈 경기 중 · 설정에서 켜기). 설정 '알림 · 표시' 스위치(Club)로 끄고 켬.
    /// 카드 자리만 게임 입력을 막음(InputBlocker — 컨트롤러가 지난 프레임 자리를 올림). 카드 부품이 CSCommon 에 없어 Panel · Badge · Button · Tile 을 시안 배치로 묶음.
    /// </summary>
    internal sealed partial class LeagueWindow
    {
        enum CardKind
        {
            Start,
            Result,
            Post,
            Champion,
            Mvp,
        }

        sealed class Card
        {
            public CardKind Kind;
            public Game Game;
            public int Index;
            public float At, Life;
            public string Line;
        }

        /// <summary>경기 전 카드는 8초 뒤 접힘 · 오른쪽 위 카드는 둘까지(나머지는 '지난 알림 더 보기') · 모아 두는 카드는 최근 다섯 장(오래된 것은 사라짐) · 경기 전 알림은 플레이볼 30분 전.</summary>
        const float StartLife = 8f;
        const int StackShown = 2, StackMax = 5, StartLead = 30;

        readonly List<Card> stack = new();
        readonly Queue<Card> bigs = new();
        Card big;
        bool showAll;

        /// <summary>지난 프레임에 그린 카드 자리(컨트롤러 Update 가 InputBlocker 에 올림).</summary>
        public readonly List<Rect> CardRects = new();
        readonly List<Rect> drawn = new();

        Season seen;
        int seenVersion = -1;
        readonly HashSet<Game> known = new();
        bool regularDone, finished;
        Game started;

        static bool RegularDone(Season s)
        {
            foreach (var g in s.Games)
                if (g.Series == 0 && !g.Played) return false;
            return true;
        }

        /// <summary>본 스레드 매 프레임: 시즌이 바뀌면 지금 상태를 기억만, 판번호가 바뀌면 내 팀 새 결과 · 정규 시즌 끝 · 우승을 카드로, 홈 경기 30분 전이면 경기 전 카드.</summary>
        public void NotifyTick()
        {
            var s = League.Current;
            float now = Time.realtimeSinceStartup;
            stack.RemoveAll(c => c.Life > 0f && now - c.At > c.Life);
            if (s == null) return;
            var club = s.Club;
            if (s != seen)
            {
                seen = s;
                seenVersion = League.Version;
                known.Clear();
                foreach (var g in s.Games)
                    if (g.Played && Mine(g)) known.Add(g);
                regularDone = RegularDone(s);
                finished = s.Finished;
                return;
            }
            if (League.Version != seenVersion)
            {
                seenVersion = League.Version;
                Card last = null;
                for (int i = 0; i < s.Games.Count; i++)
                {
                    var g = s.Games[i];
                    if (!g.Played || !Mine(g) || known.Contains(g)) continue;
                    known.Add(g);
                    if (last == null || Earlier(last.Game, g)) last = new Card { Kind = CardKind.Result, Game = g, Index = i };
                }
                if (last != null && club.NotifyResult) Push(s, last);
                bool rd = RegularDone(s), fin = s.Finished;
                if (rd && !regularDone && club.NotifyBig && ViewOf(s).Rank[0] <= Spots(s.Rule).Length) bigs.Enqueue(new Card { Kind = CardKind.Post });
                if (fin && !finished && club.NotifyBig)
                {
                    bigs.Enqueue(new Card { Kind = CardKind.Champion });
                    if (League.Mvp(s, out _, out _)) bigs.Enqueue(new Card { Kind = CardKind.Mvp });
                }
                regularDone = rd;
                finished = fin;
            }
            if (club.NotifyStart)
            {
                var g = ViewOf(s).Next;
                int left = g == null ? -1 : g.Minute - (int)Now.TimeOfDay.TotalMinutes;
                if (g != null && g.Home == 0 && g.Day == Today && !League.OnBreak(Today) && left > 0 && left <= StartLead && started != g)
                {
                    started = g;
                    Stack(new Card { Kind = CardKind.Start, Game = g, At = now, Life = StartLife });
                }
            }
            if (big == null && bigs.Count > 0) big = bigs.Dequeue();
        }

        void Push(Season s, Card c)
        {
            c.At = Time.realtimeSinceStartup;
            // 승리 투수 · 결승 장면(같은 시드로 한 번 다시 돌림)
            var b = c.Game.Box;
            var parts = new List<string>();
            if (b != null && b.Win >= 0)
            {
                int win = c.Game.HomeRuns > c.Game.AwayRuns ? c.Game.Home : c.Game.Away;
                parts.Add($"승리 투수 {s.RosterOf(win).P[b.Win].Name} ({s.StatOf(win, b.Win)[L.W]}승)");
                var r = Sim.Replay(s, c.Game, c.Index);
                if (r.DeciderBatter >= 0 && !b.Fixed) parts.Add($"결승타 {s.RosterOf(win).P[r.DeciderBatter].Name} {r.DeciderInning}회 {KindText(r.DeciderKind)}");
            }
            else if (c.Game.HomeRuns == c.Game.AwayRuns) parts.Add($"{(b != null ? b.Inn[0].Length : Rules.MaxInnings(s.Rule))}회까지 승부가 안 나 무승부예요");
            c.Line = string.Join(" · ", parts.ToArray());
            Stack(c);
        }

        /// <summary>카드를 맨 위에 쌓음(StackMax 를 넘으면 가장 오래된 것부터 버림).</summary>
        void Stack(Card c)
        {
            stack.Insert(0, c);
            if (stack.Count > StackMax) stack.RemoveRange(StackMax, stack.Count - StackMax);
        }

        /// <summary>OnGUI: input = 마우스 · 키 이벤트(창보다 먼저 받고 카드 위 클릭은 써 버림), 아니면 그림(창 위).</summary>
        public void NotifyGui(bool input)
        {
            var s = League.Current;
            drawn.Clear();
            if (s != null)
            {
                StackCards(s);
                if (big != null) BigCard(s);
                if (s.Club.LiveBoard) LiveBoard(s);
            }
            if (input)
            {
                var e = Event.current;
                if (e.isMouse || e.type == EventType.ScrollWheel)
                    foreach (var r in drawn)
                        if (r.Contains(Draw.Mouse) && e.type != EventType.Used)
                        {
                            e.Use();
                            break;
                        }
            }
            else
            {
                CardRects.Clear();
                CardRects.AddRange(drawn);
            }
        }

        // ---- 오른쪽 위 카드

        float CardH(Card c) => c.Kind == CardKind.Start
            ? D.FloatPad * 2f + Badge.H + D.InnerGap + D.LineBody + D.LineSub + D.InnerGap + Button.HCompact
            : D.FloatPad * 2f + Badge.H + D.InnerGap + D.NotifyScoreH + D.InnerGap + D.LineBody + D.InnerGap + Button.HCompact;

        void StackCards(Season s)
        {
            float x = Draw.Width - D.NotifyEdge - D.NotifyW, y = D.NotifyEdge;
            // 펼쳐도 화면 아래(LIVE 점수판 자리 위)를 넘지 않을 만큼만, '접기' 단추 자리를 남김
            float bottom = Draw.Height - D.NotifyEdge - D.LiveBarH - D.InnerGap - Button.HCompact - D.InnerGap;
            int limit = Mathf.Min(showAll ? stack.Count : StackShown, stack.Count), shown = 0;
            for (float fit = y; shown < limit && (shown == 0 || fit + CardH(stack[shown]) <= bottom); shown++) fit += CardH(stack[shown]) + D.InnerGap;
            for (int i = 0; i < shown; i++)
            {
                var c = stack[i];
                var r = new Rect(x, y, D.NotifyW, CardH(c));
                drawn.Add(r);
                Look.Elevated(r, Tile.Radius, S.Plate);
                if (Draw.Repaint) Draw.RoundOutline(r, Look.PillLine, Tile.Radius);
                if (c.Kind == CardKind.Start) StartCard(s, c, r);
                else ResultCard(s, c, r);
                y = r.yMax + D.InnerGap;
            }
            int more = stack.Count - shown;
            if (more > 0 || showAll && stack.Count > StackShown)
            {
                string label = showAll ? "지난 알림 접기" : $"지난 알림 {more}개 더 보기";
                float bw = Button.Width(null, label, true);
                var br = new Rect(x + D.NotifyW - bw, y, bw, Button.HCompact);
                drawn.Add(br);
                Button.At(br.x, br.center.y, null, label, null, Tone.Neutral, null, true, () => showAll = !showAll, true, true);
            }
        }

        /// <summary>카드 머리: 종류 알약 · 시각 · 닫기. 몸 시작 y 를 돌려줌.</summary>
        float CardHead(Rect r, string kind, bool pastel, string when, System.Action close)
        {
            float x = r.x + D.CardPad, y = r.y + D.FloatPad, cy = y + Badge.H * 0.5f;
            float kw = Badge.Pair(x, cy, kind, pastel ? Look.PastelFill : Look.Pill, pastel ? Look.PastelInk : Look.BarOff);
            Line(new Rect(x + kw + D.ActionGap, y, r.width, Badge.H), when, Look.PillSub, false, D.FontMin);
            var cr = new Rect(r.xMax - D.CardPad - D.NotifyCloseD + D.Gap, Mathf.Round(cy - D.NotifyCloseD * 0.5f), D.NotifyCloseD, D.NotifyCloseD);
            if (Button.Square(cr, Icons.Close, "닫기")) close();
            return y + Badge.H + D.InnerGap;
        }

        void ResultCard(Season s, Card c, Rect r)
        {
            var g = c.Game;
            float y = CardHead(r, "경기 결과", true, $"{g.Day.Month}월 {g.Day.Day}일", () => stack.Remove(c));
            float x = r.x + D.CardPad, w = r.width - D.CardPad * 2f;
            var box = new Rect(x, y, w, D.NotifyScoreH);
            if (Draw.Repaint) Draw.Round(box, Look.Pill, D.CalendarRadius);
            bool home = g.Home == 0;
            int opp = Opponent(g), mine = home ? g.HomeRuns : g.AwayRuns, theirs = home ? g.AwayRuns : g.HomeRuns;
            var v = ViewOf(s);
            string score = $"{mine} : {theirs}";
            float sw = Mathf.Ceil(Draw.TextWidth(score, S.CountFont, true));
            Line(new Rect(box.center.x - sw * 0.5f, box.y, sw, box.height), score, Look.BarInk, true, S.CountFont, TextAnchor.MiddleCenter);
            float side = (box.width - sw) * 0.5f - S.RowGap - D.CardPad;
            ScoreSide(s, v, 0, home ? "홈" : "원정", new Rect(box.x + D.CardPad, box.y, side, box.height), false, Look.BarInk);
            ScoreSide(s, v, opp, home ? "원정" : "홈", new Rect(box.xMax - D.CardPad - side, box.y, side, box.height), true, mine < theirs ? Look.BarInk : Look.BarOff);
            y += D.NotifyScoreH + D.InnerGap;
            Line(new Rect(x, y, w, D.LineBody), c.Line, Look.BarOff, false, D.FontDesc);
            y += D.LineBody + D.InnerGap;
            float by = y + Button.HCompact * 0.5f;
            float bw = Button.At(x, by, null, "경기 기록 보기", null, Tone.Primary, null, true, () => Show(TabSchedule), true, true);
            Button.At(x + bw + D.ActionGap, by, null, "순위 보기", null, Tone.Neutral, null, true, () => Show(TabHome), true, true);
        }

        static void ScoreSide(Season s, View v, int team, string where, Rect r, bool right, Color ink)
        {
            float mx = right ? r.xMax - D.TeamMarkW : r.x;
            if (Draw.Repaint) Draw.Round(new Rect(mx, Mathf.Round(r.center.y - S.ChipH * 0.5f + D.Gap), D.TeamMarkW, S.ChipH - D.Gap * 2f), TeamColor(team), D.TeamMarkW * 0.5f);
            var t = right ? new Rect(r.x, r.y, r.width - D.TeamMarkW - D.ActionGap, r.height) : new Rect(r.x + D.TeamMarkW + D.ActionGap, r.y, r.width - D.TeamMarkW - D.ActionGap, r.height);
            var anchor = right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            float top = Mathf.Round(r.center.y - (D.LineBody + D.LineSub) * 0.5f);
            var rec = v.Records[team];
            Line(new Rect(t.x, top, t.width, D.LineBody), s.Team(team).Name, ink, true, D.FontValue, anchor);
            Line(new Rect(t.x, top + D.LineBody, t.width, D.LineSub), $"{where} · {WinLoss(rec.W, rec.L)}", Look.PillSub, false, D.FontMin, anchor);
        }

        void StartCard(Season s, Card c, Rect r)
        {
            var g = c.Game;
            float y = CardHead(r, "경기 전", false, Clock((short)Mathf.Max(0, g.Minute - StartLead)), () => stack.Remove(c));
            float x = r.x + D.CardPad, w = r.width - D.CardPad * 2f;
            int left = Mathf.Max(0, g.Minute - (int)Now.TimeOfDay.TotalMinutes);
            var v = ViewOf(s);
            Line(new Rect(x, y, w, D.LineBody), $"{left}분 뒤 플레이볼", Look.BarInk, true, D.FontValue);
            string gb = v.Rank[0] == 1 ? "지금 1위" : $"1위와 {Gb(v.Behind[0])}경기";
            Line(new Rect(x, y + D.LineBody, w, D.LineSub), $"vs {s.Team(g.Away).Name} · {Park(s, 0)} {Clock(g.Minute)} · {gb}", Look.PillSub, false, D.FontMin);
            y += D.LineBody + D.LineSub + D.InnerGap;
            float by = y + Button.HCompact * 0.5f, bx = x;
            ushort id = Stadium.Home;
            if (id != 0) bx += Button.At(bx, by, null, "경기장으로 이동", null, Tone.Primary, null, true, () => Stadium.Focus(id), true, true) + D.ActionGap;
            Button.At(bx, by, null, "나중에", null, id != 0 ? Tone.Neutral : Tone.Primary, null, true, () => stack.Remove(c), true, true);
        }

        // ---- 가운데 큰 소식

        void BigCard(Season s)
        {
            var v = ViewOf(s);
            string kind, title, desc, action;
            int team = 0;
            string[] labels, values;
            int action0;
            if (big.Kind == CardKind.Post)
            {
                var spots = Spots(s.Rule);
                int rank = v.Rank[0];
                string stage = s.Rule == Rule.KBO ? (rank == 1 ? "한국시리즈" : rank == 2 ? "플레이오프" : rank == 3 ? "준플레이오프" : "와일드카드 결정전") : rank == 1 ? "클라이맥스 시리즈 파이널" : "클라이맥스 시리즈 퍼스트";
                kind = "큰 소식 · 포스트시즌";
                title = $"{s.Team(0).Name} 포스트시즌 진출";
                labels = new[] { "최종 순위", "정규 시즌", "다음 단계" };
                values = new[] { rank + "위", WinLoss(v.Records[0].W, v.Records[0].L), spots[rank - 1] };
                desc = $"{stage}부터 시작해요. 대진과 날짜는 일정 탭 포스트시즌 판에서 볼 수 있어요";
                action = "포스트시즌 일정 보기";
                action0 = TabSchedule;
            }
            else if (big.Kind == CardKind.Champion)
            {
                s.Champion(out var t);
                team = t.Champion;
                int last = s.Post.Count - 1;
                kind = "큰 소식 · 우승";
                title = $"{s.Team(team).Name} {t.Year} 우승";
                labels = new[] { "정규 시즌", Rules.RoundOf(s.Rule, Rules.RoundCount(s.Rule) - 1).Name, "준우승" };
                values = new[] { WinLoss(v.Records[team].W, v.Records[team].L), $"{s.Wins(last, team)} : {s.Wins(last, t.Runner)}", s.Team(t.Runner).Name };
                desc = team == 0 ? "내 도시 팀이 우승했어요. 우승 이력에 남고, 다음 시즌에도 이어 가요" : "우승 이력에 남았어요. 설정에서 새 시즌을 시작할 수 있어요";
                action = "기록 보기";
                action0 = TabRecords;
            }
            else
            {
                League.Mvp(s, out team, out int slot);
                var l = s.StatOf(team, slot);
                bool pitcher = slot >= Roster.FirstPitcher;
                kind = "큰 소식 · MVP";
                title = $"{s.RosterOf(team).P[slot].Name} 시즌 MVP";
                labels = pitcher ? new[] { "평균자책점", "승", "탈삼진" } : new[] { "타율", "홈런", "타점" };
                values = pitcher ? new[] { l.Era.ToString("0.00"), l[L.W].ToString(), l[L.K].ToString() } : new[] { Pct(l.Avg), l[L.HR].ToString(), l[L.RBI].ToString() };
                desc = $"{s.Team(team).Name} · {Roster.Position(slot)}. 정규 시즌 기록으로 뽑았어요";
                action = "기록 보기";
                action0 = TabRecords;
            }
            float inner = D.NotifyBigW - S.HeroPadX * 2f, tileH = TileH(false);
            float dh = Draw.TextHeight(desc, D.FontDesc, inner);
            float h = D.NotifyBand + S.HeroPadY * 2f + D.NotifyIconD + S.Gap + tileH + S.Gap + dh + S.Gap + Button.H;
            var r = new Rect(Mathf.Round((Draw.Width - D.NotifyBigW) * 0.5f), Mathf.Round((Draw.Height - h) * 0.5f), D.NotifyBigW, h);
            drawn.Add(r);
            Look.Elevated(r, S.HeroRadius, S.Plate);
            if (Draw.Repaint)
            {
                // 위 색 띠: 둥근 위 모서리 칸을 그리고 띠 아래를 판 면으로 덮음
                Draw.Round(new Rect(r.x, r.y, r.width, S.HeroRadius * 2f), TeamColor(team), S.HeroRadius);
                Draw.Fill(new Rect(r.x, r.y + D.NotifyBand, r.width, S.HeroRadius * 2f - D.NotifyBand), S.Plate);
                Draw.RoundOutline(r, Look.PillLine, S.HeroRadius);
            }
            float x = r.x + S.HeroPadX, y = r.y + D.NotifyBand + S.HeroPadY;
            var ic = new Rect(x, y, D.NotifyIconD, D.NotifyIconD);
            if (Draw.Repaint)
            {
                Draw.Round(ic, Look.PastelFill, D.NotifyIconD * 0.5f);
                Draw.Icon(TabIcons[TabRecords], new Rect(Mathf.Round(ic.center.x - IconButton.IconSize * 0.5f), Mathf.Round(ic.center.y - IconButton.IconSize * 0.5f), IconButton.IconSize, IconButton.IconSize), Look.PastelInk);
            }
            float tx = ic.xMax + S.RowGap, top = Mathf.Round(ic.center.y - (Badge.H + D.LiveNameH) * 0.5f);
            Badge.Pair(tx, top + Badge.H * 0.5f, kind, Look.PastelFill, Look.PastelInk);
            Line(new Rect(tx, top + Badge.H, r.xMax - S.HeroPadX - tx, D.LiveNameH), title, Look.BarInk, true, D.LiveNameFont);
            y += D.NotifyIconD + S.Gap;
            var tiles = new Rect(x, y, inner, tileH);
            for (int i = 0; i < labels.Length; i++) StatTile(Cell(tiles, labels.Length, i, D.ActionGap), labels[i], values[i], null, false);
            y += tileH + S.Gap;
            Draw.Text(new Rect(x, y, inner, dh), desc, D.FontDesc, Look.BarOff, false, TextAnchor.UpperLeft, true);
            y += dh + S.Gap;
            float by = y + Button.H * 0.5f;
            int go = action0;
            float bw = Button.At(x, by, null, action, null, Tone.Primary, null, true, () =>
            {
                Show(go);
                big = null;
            });
            Button.At(x + bw + Button.Gap, by, null, "닫기", null, Tone.Neutral, null, true, () => big = null);
        }

        // ---- 오른쪽 아래 LIVE 점수판(홈 경기 중)

        void LiveBoard(Season s)
        {
            int index = -1;
            foreach (int i in ViewOf(s).TodayHome) // 오늘 홈 경기만(View 에 담아 둠)
                if (StateOf(s, s.Games[i], i, out _) == Live.State.Live) index = i;
            if (index < 0) return;
            var g = s.Games[index];
            StateOf(s, g, index, out float t);
            var f = Live.At(Live.Of(s, g, index, Today), t);
            string badge = "LIVE · " + Half(f.Inning, f.Bottom), score = $"{f.Home} : {f.Away}", outs = OutsText(f.Outs);
            string ha = s.Abbr(g.Home), aa = s.Abbr(g.Away);
            float bw = Badge.Pair(0f, 0f, badge, Look.WarnFill, Look.WarnInk, false);
            float hw = Mathf.Ceil(Draw.TextWidth(ha, D.FontBody, true)), aw = Mathf.Ceil(Draw.TextWidth(aa, D.FontBody, true)), sw = Mathf.Ceil(Draw.TextWidth(score, S.HeroUnitFont, true)), ow = Mathf.Ceil(Draw.TextWidth(outs, D.FontMin));
            float gap = D.FloatPad, mark = D.TeamMarkW + D.ActionGap;
            float w = D.CardPad + bw + gap + mark + hw + gap + sw + gap + aw + mark + gap + D.MiniDiamondW + D.ActionGap + ow + D.CardPad + D.Gap;
            var r = new Rect(Draw.Width - D.NotifyEdge - w, Draw.Height - D.NotifyEdge - D.LiveBarH, w, D.LiveBarH);
            drawn.Add(r);
            Look.Elevated(r, r.height * 0.5f, S.Plate);
            if (Draw.Repaint) Draw.RoundOutline(r, Look.PillLine, r.height * 0.5f);
            float x = r.x + D.CardPad, cy = r.center.y;
            x += Badge.Pair(x, cy, badge, Look.WarnFill, Look.WarnInk) + gap;
            x = Mark(x, cy, g.Home, D.TeamMarkW, D.TeamMarkH, D.ActionGap);
            Line(new Rect(x, r.y, hw + D.Gap, r.height), ha, Look.BarInk, true);
            x += hw + gap;
            Line(new Rect(x, r.y, sw + D.Gap, r.height), score, Look.BarInk, true, S.HeroUnitFont);
            x += sw + gap;
            Line(new Rect(x, r.y, aw + D.Gap, r.height), aa, Look.BarOff, true);
            x += aw + D.ActionGap;
            x = Mark(x, cy, g.Away, D.TeamMarkW, D.TeamMarkH, gap);
            // 작은 다이아몬드(1 · 2 · 3루)
            if (Draw.Repaint)
            {
                float off = D.MiniDiamondW * 0.5f - D.MiniBaseD * 0.5f;
                var c = new Vector2(x + D.MiniDiamondW * 0.5f, cy);
                MiniBase(new Vector2(c.x + off, c.y + D.MiniBaseD * 0.25f), (f.Bases & 1) != 0);
                MiniBase(new Vector2(c.x, c.y - off + D.MiniBaseD * 0.25f), (f.Bases & 2) != 0);
                MiniBase(new Vector2(c.x - off, c.y + D.MiniBaseD * 0.25f), (f.Bases & 4) != 0);
            }
            x += D.MiniDiamondW + D.ActionGap;
            Line(new Rect(x, r.y, ow + D.Gap, r.height), outs, Look.PillSub, false, D.FontMin);
        }

        static void MiniBase(Vector2 c, bool on)
        {
            float h = D.MiniBaseD * 0.5f, k = h * Mathf.Sqrt(0.5f), e = h * Mathf.Sqrt(2f);
            Draw.Line(new Vector2(c.x - k, c.y - k), new Vector2(c.x + k, c.y + k), D.MiniBaseD, on ? Pal.Primary : S.Plate);
            Draw.Polyline(new List<Vector2> { new(c.x, c.y - e), new(c.x + e, c.y), new(c.x, c.y + e), new(c.x - e, c.y) }, D.CountRing, on ? Pal.Primary : Pal.Line2, true);
        }
    }
}
