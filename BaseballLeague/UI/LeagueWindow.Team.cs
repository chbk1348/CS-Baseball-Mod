using BaseballLeague.Data;
using CSCommonUX;
using UnityEngine;
using D = CSCommonUX.CSCommonDesign;
using L = BaseballLeague.Data.Line;
using S = CSCommonUX.Section;

namespace BaseballLeague.UI
{
    /// <summary>
    /// 팀(시안 Team): 왼쪽 팀 목록(순위순 · 눌러 고름) · 오른쪽 고른 팀 카드(감독 · 성적 · 홈 / 원정 · 득점 · 실점 · 연속 · 최근 10경기, 내 팀이면 '홈팀 설정') · 팀 전력 4칸 · 타순 · 투수.
    /// 홈팀 설정(시안 HomeTeam, 진입: 내 팀 카드 '홈팀 설정'): 팀 이름 방식 · 팀 이름 · 줄임 이름 · 감독 · 팀 색 · 홈구장 · 시작 전력 · 저장 / 되돌리기 · 미리보기(점수판) · 원정 팀 줄임 이름 고치기.
    /// </summary>
    internal sealed partial class LeagueWindow
    {
        int pickedTeam;

        float TeamTab(Season s, View v, float w)
        {
            int n = Rules.TeamCount(s.Rule);
            if (pickedTeam >= n) pickedTeam = 0;
            float leftH = TeamList(s, v, D.SideColW);
            float x = D.SideColW + S.Gap, rw = w - x;
            float y = TeamCard(s, v, pickedTeam, new Rect(x, 0f, rw, 0f));
            y = TeamPower(s, v, pickedTeam, new Rect(x, y, rw, 0f));
            y = Lineups(s, pickedTeam, new Rect(x, y + S.Gap, rw, 0f));
            return Mathf.Max(leftH, y);
        }

        float TeamList(Season s, View v, float w)
        {
            int n = Rules.TeamCount(s.Rule);
            const string note = "내 도시 팀은 홈구장이 있는 팀이에요. 팀 이름 방식은 홈팀 설정에서 바꿔요";
            float nh = NoteH(note, w - S.Pad * 2f);
            float h = PlateH(D.TableRowH * n + S.HeadGap + nh);
            var b = Plate(new Rect(0f, 0f, w, h), n + "팀", TabIcons[TabTeams], v.Tables.Length > 1 ? "리그별 순위순" : "순위순");
            float numW = Mathf.Ceil(Draw.TextWidth("12", D.FontDesc, true)), wlW = Mathf.Ceil(Draw.TextWidth("100-100", D.FontMin));
            int k = 0;
            foreach (var table in v.Tables)
                foreach (var t in table)
                {
                    var row = new Rect(b.x, b.y + D.TableRowH * k++, b.width, D.TableRowH);
                    bool on = t.Team == pickedTeam;
                    if (Draw.Repaint && on)
                    {
                        Draw.Round(row, Look.PastelPickFill, row.height * 0.5f);
                        Draw.RoundOutline(row, Look.PastelPickLine, row.height * 0.5f);
                    }
                    float x = row.x + S.RowPadL;
                    Line(new Rect(x, row.y, numW, row.height), v.Rank[t.Team].ToString(), S.SubInk, true, D.FontDesc, TextAnchor.MiddleRight);
                    x = Mark(x + numW + S.RowGap, row.center.y, t.Team);
                    Line(new Rect(row.xMax - D.CardRowPad - wlW, row.y, wlW, row.height), $"{t.W}-{t.L}", Look.PillSub, false, D.FontMin, TextAnchor.MiddleRight);
                    Line(new Rect(x, row.y, row.xMax - D.CardRowPad - wlW - S.RowGap - x, row.height), s.Team(t.Team).Name, Look.BarInk, true);
                    if (Draw.Button(row)) pickedTeam = t.Team;
                }
            Note(new Rect(b.x, b.yMax - nh, b.width, nh), note);
            return h;
        }

        /// <summary>팀 카드(히어로): 색 막대 · 이름 · 설명 · 순위 알약 · '홈팀 설정'(내 팀) · 다섯 타일 · 최근 10경기 알약.</summary>
        float TeamCard(Season s, View v, int team, Rect area)
        {
            var r = v.Records[team];
            float tileH = TileH(true);
            float h = S.HeroPadY * 2f + HeroHeadH + S.HeroGap + tileH + S.HeroGap + Badge.H;
            var c = new Rect(area.x, area.y, area.width, h);
            HeroFrame(c);
            float x = c.x + S.HeroPadX, y = c.y + S.HeroPadY, inner = c.width - S.HeroPadX * 2f, headH = HeroHeadH, cy = y + headH * 0.5f;
            float tx = Mark(x, cy, team, D.TeamHeroMarkW, D.TeamHeroMarkH);
            float right = x + inner;
            if (team == 0) right -= Button.At(right - Button.Width(TabIcons[TabSettings], "홈팀 설정"), cy, TabIcons[TabSettings], "홈팀 설정", null, Tone.Neutral, null, true, () => Go(TabTeams, Page.HomeTeam)) + S.RowGap;
            int lg = Rules.LeagueOf(s.Rule, team);
            string rank = v.Rank[team] == 1 ? "1위" : $"{v.Rank[team]}위 · 1위와 {Gb(v.Behind[team])}경기";
            float rw = S.Tag(0f, 0f, rank, null, null, null, false);
            S.Tag(right - rw, cy, rank, Look.PastelFill, Look.PastelInk);
            right -= rw + S.RowGap;
            string desc = (team == 0 ? "내 도시 팀 · 홈 " + Park(s, 0) : "원정 팀 · 연고지 " + s.Team(team).City) + " · 감독 " + s.Manager(team);
            if (s.Rule == Rule.NPB) desc = Rules.LeagueName(s.Rule, lg) + " · " + desc;
            S.HeroTitle(new Rect(tx, y, right - tx, headH), s.Team(team).Name, null, desc);
            y += headH + S.HeroGap;

            var tiles = new Rect(x, y, inner, tileH);
            int games = r.W + r.L + r.T;
            string streak = r.Streak > 0 ? r.Streak + "연승" : r.Streak < 0 ? -r.Streak + "연패" : "-";
            StatTile(Cell(tiles, 5, 0, D.ActionGap), "성적", WinLoss(r.W, r.L), r.T > 0 ? $"{r.T}무 · 승률 {Pct(r.W + r.L == 0 ? 0f : (float)r.W / (r.W + r.L))}" : "승률 " + Pct(r.W + r.L == 0 ? 0f : (float)r.W / (r.W + r.L)), true);
            StatTile(Cell(tiles, 5, 1, D.ActionGap), "홈", WinLoss(r.HomeW, r.HomeL), "원정 " + WinLoss(r.AwayW, r.AwayL), true);
            StatTile(Cell(tiles, 5, 2, D.ActionGap), "득점", r.Runs.ToString(), "경기당 " + (games == 0 ? 0f : (float)r.Runs / games).ToString("0.0"), true);
            StatTile(Cell(tiles, 5, 3, D.ActionGap), "실점", r.Against.ToString(), "경기당 " + (games == 0 ? 0f : (float)r.Against / games).ToString("0.0"), true);
            StatTile(Cell(tiles, 5, 4, D.ActionGap), "연속", streak, "최근 10경기 " + Last10(v, team).Split(' ')[0], true);
            y += tileH + S.HeroGap;

            // 최근 10경기 알약(오래된 것 → 최근) · 오른쪽 기간
            float lw = Mathf.Ceil(Draw.TextWidth("최근 10경기", D.FontMin));
            float by = y + Badge.H * 0.5f;
            Line(new Rect(x, y, lw, Badge.H), "최근 10경기", Look.PillSub, false, D.FontMin);
            float bx = x + lw + S.RowGap;
            if (r.Last10.Count == 0) Line(new Rect(bx, y, inner - lw, Badge.H), "아직 끝난 경기가 없어요", Look.PillSub, false, D.FontMin);
            foreach (var g in r.Last10)
            {
                ResultBadge(new Rect(bx, Mathf.Round(by - Badge.H * 0.5f), S.RankD + D.Gap, Badge.H), League.Result(g, team));
                bx += S.RankD + D.Gap + D.IconGap;
            }
            if (r.Last10.Count > 0)
            {
                string span = Md(r.Last10[0].Day) + " – " + Md(r.Last10[r.Last10.Count - 1].Day);
                float sw = Mathf.Ceil(Draw.TextWidth(span, D.FontMin));
                Line(new Rect(x + inner - sw, y, sw, Badge.H), span, Look.PillSub, false, D.FontMin, TextAnchor.MiddleRight);
            }
            return area.y + h + S.Gap;
        }

        static readonly string[] PartNames = { "타격", "투수", "수비", "홈 이점" };

        /// <summary>
        /// 팀 전력 4칸(0 ~ 100): 타격 = 선발 타자 능력 평균 · 투수 = 투수 구위 · 제구 평균 · 수비 = 100 − 경기당 실책 × 100 · 홈 이점 = 50 + (홈 승률 − 원정 승률) × 100.
        /// shortcut: 수비 능력 칸이 없어 실책 수로만 셈, 수비 능력을 두게 되면 Roster 에 칸을 더함.
        /// </summary>
        static float[] Parts(Season s, int team, TeamRecord r, int errors)
        {
            var ro = s.RosterOf(team);
            int games = r.W + r.L + r.T;
            float home = r.HomeW + r.HomeL == 0 ? 0f : (float)r.HomeW / (r.HomeW + r.HomeL), away = r.AwayW + r.AwayL == 0 ? 0f : (float)r.AwayW / (r.AwayW + r.AwayL);
            return new[]
            {
                ro.Mean(0, Roster.Lineup, p => (p.Af + p.Bf + p.Cf) / 3f) * 100f,
                ro.Mean(Roster.FirstPitcher, Roster.Size, p => (p.Af + p.Bf) * 0.5f) * 100f,
                Mathf.Clamp(100f - (games == 0 ? 0f : (float)errors / games) * 100f, 0f, 100f),
                Mathf.Clamp(50f + (home - away) * 100f, 0f, 100f),
            };
        }

        /// <summary>팀 전력 4칸: 값 · 막대 · 리그 순위(홈 이점은 홈 − 원정 승률 차).</summary>
        float TeamPower(Season s, View v, int team, Rect area)
        {
            float tile = D.LineBody + D.ActionGap + Progress.Bar + D.ActionGap + D.LineSub + D.CardRowPad * 2f;
            float h = PlateH(tile);
            var b = Plate(new Rect(area.x, area.y, area.width, h), "팀 전력", IconChart, "홈 경기 승률 조정에 써요");
            var mine = v.Parts[team];
            for (int k = 0; k < PartNames.Length; k++)
            {
                var c = Cell(b, PartNames.Length, k, D.ActionGap);
                if (Draw.Repaint) Draw.Round(c, Look.Pill, Tile.Radius);
                var inner = new Rect(c.x + Tile.PadX, c.y + D.CardRowPad, c.width - Tile.PadX * 2f, c.height - D.CardRowPad * 2f);
                int rank = 1;
                foreach (var o in v.Parts)
                    if (o[k] > mine[k]) rank++;
                string value = Mathf.RoundToInt(mine[k]).ToString();
                float nw = Mathf.Ceil(Draw.TextWidth(PartNames[k], D.FontMin));
                Line(new Rect(inner.x, inner.y, nw, D.LineBody), PartNames[k], Look.PillSub, false, D.FontMin);
                Line(new Rect(inner.x + nw + D.ActionGap, inner.y, inner.width - nw - D.ActionGap, D.LineBody), value, Look.BarInk, true, D.FontValue);
                Progress.In(new Rect(inner.x, inner.y + D.LineBody + D.ActionGap, inner.width, Progress.Bar), mine[k] / 100f, -1f, Pal.Primary);
                var r = v.Records[team];
                float diff = (r.HomeW + r.HomeL == 0 ? 0f : (float)r.HomeW / (r.HomeW + r.HomeL)) - (r.AwayW + r.AwayL == 0 ? 0f : (float)r.AwayW / (r.AwayW + r.AwayL));
                string sub = k == 3 ? $"홈 승률 {(diff >= 0f ? "+" : "")}{Mathf.RoundToInt(diff * 100f)}" : $"리그 {rank}위";
                Line(new Rect(inner.x, inner.yMax - D.LineSub, inner.width, D.LineSub), sub, Look.PillSub, false, D.FontMin);
            }
            return area.y + h;
        }

        /// <summary>타순(선발 9명 · 시즌 타율 · 홈런 · 타점) · 투수(선발 5 · 불펜 3 · 마무리 · 승패 · 평균자책, 다음 경기 선발 표시) 두 판 + 아래 안내.</summary>
        float Lineups(Season s, int team, Rect area)
        {
            const int pitchers = Roster.Lineup;
            float bodyH = D.LineHeadH + D.TableRowH * Roster.Lineup;
            float h = PlateH(bodyH);
            float lw = (area.width - S.Gap) * LineupShare;
            var next = NextGameOf(s, team);
            int starter = next == null ? -1 : Sim.StarterOf(s, next, team);
            var ro = s.RosterOf(team);

            // 타순
            var b = Plate(new Rect(area.x, area.y, lw, h), "타순", TabIcons[TabTeams], "오늘 선발");
            string[] heads = { "순", "이름", "위치", "타율", "홈런", "타점" };
            float[] cols = { Badge.H, 0f, D.LineTotW, D.StatColW, D.LineTotW, D.LineTotW };
            TableHead(b, heads, cols);
            for (int i = 0; i < Roster.Lineup; i++)
            {
                var row = new Rect(b.x, b.y + D.LineHeadH + D.TableRowH * i, b.width, D.TableRowH);
                var st = s.StatOf(team, i);
                bool hot = st[L.AB] > 0 && st.Avg >= HotAvg;
                TableRow(row, cols, new[] { (i + 1).ToString(), ro.P[i].Name, Roster.Position(i), st[L.AB] == 0 ? "-" : Pct(st.Avg), st[L.HR].ToString(), st[L.RBI].ToString() }, hot ? 3 : -1, -1);
            }

            // 투수: 선발 5 · 불펜 앞 3 · 마무리
            string sp = starter >= 0 ? "오늘 선발 " + ro.P[starter].Name : "남은 경기 없음";
            b = Plate(new Rect(area.x + lw + S.Gap, area.y, area.width - lw - S.Gap, h), "투수", TabIcons[TabTeams], sp);
            string[] pheads = { "이름", "역할", "승패", "ERA" };
            float[] pcols = { 0f, D.StatColW, D.StatColW, D.StatColW };
            TableHead(b, pheads, pcols);
            int[] slots = new int[pitchers];
            for (int i = 0; i < Roster.Starters; i++) slots[i] = Roster.FirstPitcher + i;
            for (int i = Roster.Starters; i < pitchers - 1; i++) slots[i] = Roster.FirstReliever + i - Roster.Starters;
            slots[pitchers - 1] = Roster.Closer;
            for (int i = 0; i < pitchers; i++)
            {
                int slot = slots[i];
                var row = new Rect(b.x, b.y + D.LineHeadH + D.TableRowH * i, b.width, D.TableRowH);
                var st = s.StatOf(team, slot);
                TableRow(row, pcols, new[] { ro.P[slot].Name, Roster.Position(slot), $"{st[L.W]}-{st[L.L]}" + (slot == Roster.Closer && st[L.SV] > 0 ? $" {st[L.SV]}S" : ""), st[L.Outs] == 0 ? "-" : st.Era.ToString("0.00") }, -1, slot == starter ? 0 : -1);
            }
            const string note = "선수 기록은 경기 시뮬레이션에서 타석마다 쌓여요. 실제 선수 이름은 쓰지 않아요";
            float nh = NoteH(note, area.width - S.RowPadL * 2f);
            Note(new Rect(area.x + S.RowPadL, area.y + h + S.Gap, area.width - S.RowPadL * 2f, nh), note);
            return area.y + h + S.Gap + nh;
        }

        /// <summary>시안 타순 표: 타율 .300 넘으면 굵게 · 두 표 폭 1.35 : 1.</summary>
        const float HotAvg = 0.3f, LineupShare = 1.35f / 2.35f;

        static Game NextGameOf(Season s, int team)
        {
            Game best = null;
            foreach (var g in s.Games)
                if (!g.Played && (g.Home == team || g.Away == team) && g.Day >= Today && (best == null || Earlier(g, best))) best = g;
            return best;
        }

        /// <summary>표 머리(12 보조): cols 의 0 은 남은 폭을 채우는 칸(왼쪽 맞춤), 나머지는 고정 폭 · 가운데.</summary>
        static void TableHead(Rect b, string[] heads, float[] cols)
        {
            var row = new Rect(b.x, b.y, b.width, D.LineHeadH);
            Cols(row, cols, (i, c) => Line(c, heads[i], S.SubInk, false, D.FontMin, cols[i] == 0f ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter));
            S.Hairline(new Rect(row.x, row.yMax - 1f, row.width, 1f));
        }

        /// <summary>표 줄: bold = 굵게 할 칸(-1 없음), tag = 그 칸 글 뒤에 '선발' 알약(-1 없음).</summary>
        static void TableRow(Rect row, float[] cols, string[] vals, int bold, int tag)
        {
            Cols(row, cols, (i, c) =>
            {
                bool flex = cols[i] == 0f;
                if (i == tag)
                {
                    float nw = Mathf.Ceil(Draw.TextWidth(vals[i], D.FontBody, true));
                    Line(new Rect(c.x, c.y, nw + D.Gap, c.height), vals[i], Look.BarInk, true);
                    Badge.Pair(c.x + nw + D.ActionGap, c.center.y, "선발", Look.PastelFill, Look.PastelInk);
                    return;
                }
                Line(c, vals[i], flex ? Look.BarInk : i == bold ? Look.BarInk : Look.BarOff, flex || i == bold, flex ? D.FontBody : D.FontDesc, flex ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
            });
        }

        static void Cols(Rect row, float[] cols, System.Action<int, Rect> cell)
        {
            float fixedW = 0f;
            foreach (float c in cols) fixedW += c;
            float flex = row.width - S.RowPadL * 2f - fixedW - S.RowGap * (cols.Length - 1), x = row.x + S.RowPadL;
            for (int i = 0; i < cols.Length; i++)
            {
                float cw = cols[i] == 0f ? flex : cols[i];
                cell(i, new Rect(x, row.y, cw, row.height));
                x += cw + S.RowGap;
            }
        }

        // ---- 홈팀 설정(상세)

        static readonly string[] NamingLabels = { "한국형", "일본형" }, PowerLabels = { "약함", "보통", "강함" };
        static readonly string[] ColorNames = { "하늘", "빨강", "노랑", "보라", "청록", "주황", "검정", "초록", "갈색", "회색", "남색", "자주" };

        /// <summary>고치는 중인 홈팀 편집값(저장 전) · 그 바탕 · 줄임 이름을 고치는 원정 팀(-1 없음) · 그 글.</summary>
        Club draft, draftOf;
        int editAway = -1;
        string awayText = "";

        Club Draft(Season s)
        {
            if (draft == null || draftOf != s.Club)
            {
                draft = s.Club.Clone();
                draftOf = s.Club;
            }
            return draft;
        }

        const int AbbrMax = 3, AbbrMin = 2, NameMax = 16;

        /// <summary>줄임 이름: 영문 대문자만 AbbrMax 자까지(빈 글 = 기본).</summary>
        static string CleanAbbr(string t)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in (t ?? "").ToUpperInvariant())
                if (c >= 'A' && c <= 'Z' && sb.Length < AbbrMax) sb.Append(c);
            return sb.ToString();
        }

        static bool AbbrOk(string t) => t.Length == 0 || t.Length >= AbbrMin;

        float HomeTeamPage(Season s, View v, float w)
        {
            float half = (w - S.Gap) * 0.5f;
            float left = HomeTeamForm(s, new Rect(0f, 0f, half, 0f));
            float y = HomeTeamPreview(s, new Rect(half + S.Gap, 0f, half, 0f));
            y = AwayTeams(s, new Rect(half + S.Gap, y, half, 0f));
            return Mathf.Max(left, y);
        }

        static float Block(float ctl) => Stat.LabelH + D.Gap + ctl + D.Gap + Textfield.HelpH;

        float HomeTeamForm(Season s, Rect area)
        {
            var c = Draft(s);
            float segH = Segmented.SubH + Segmented.Pad * 2f, fieldH = Button.H, gap = S.HeroGap, powerH = Segmented.H + Segmented.Pad * 2f;
            float h = PlateH(Block(segH) + Block(fieldH) * 4f + Block(powerH) + gap * 6f + Button.H);
            var b = Plate(new Rect(area.x, area.y, area.width, h), "홈팀 설정", TabIcons[TabHome], "내 도시 팀");
            float y = b.y;

            // 팀 이름 방식(바로 바뀜)
            string[] subs = { Rules.TeamOf(Naming.Korean, 0).Name + " · 한국 도시", Rules.TeamOf(Naming.Japanese, 0).Name + " · 일본 도시" };
            y = Field(b, y, "팀 이름 방식", segH, "리그 팀 이름 · 연고지 · 선수 이름이 함께 바뀌어요. 경기 결과 · 기록은 그대로예요", false, r => Segmented.In(r, NamingLabels, (int)s.Naming, i => League.SetNaming((Naming)i), null, subs)) + gap;

            // 팀 이름
            string dflt = Rules.TeamOf(s.Naming, 0).Name;
            bool nameBad = c.Name.Trim().Length > NameMax;
            y = Field(b, y, "팀 이름", fieldH, nameBad ? $"{NameMax}자까지 써요" : "경기 · 순위 · 알림 카드에 이 이름이 나와요. 비워 두면 " + dflt, nameBad,
                r => c.Name = Textfield.In(r, c.Name, NameMax + 1, dflt, null, nameBad, true, "baseball.name")) + gap;

            // 줄임 이름 · 감독
            float cw = (b.width - S.RowGap) * 0.5f;
            bool abbrBad = !AbbrOk(c.Abbr[0] ?? "");
            Field(new Rect(b.x, b.y, cw, b.height), y, "줄임 이름", fieldH, abbrBad ? $"영문 {AbbrMin} ~ {AbbrMax}자로 써요" : "영문 3자 · 점수판에 써요", abbrBad,
                r => c.Abbr[0] = CleanAbbr(Textfield.In(r, c.Abbr[0] ?? "", AbbrMax, Rules.Abbr(0), null, abbrBad, true, "baseball.abbr")));
            y = Field(new Rect(b.x + cw + S.RowGap, b.y, cw, b.height), y, "감독", fieldH, "비워 두면 자동으로 정해요", false,
                r => c.Manager = Textfield.In(r, c.Manager, NameMax, s.RosterOf(0).Manager, null, false, true, "baseball.manager")) + gap;

            // 팀 색(리그 다른 팀이 쓰는 색은 흐리게 · 못 고름)
            int n = Rules.TeamCount(s.Rule), cur = c.Color >= 0 ? c.Color : 0;
            y = Field(b, y, "팀 색", fieldH, "다른 팀과 같은 색은 고를 수 없어요", false, r =>
            {
                float x = r.x + Swatch.Ring * 2f;
                for (int i = 0; i < Pal.BallTeams.Length; i++)
                {
                    bool used = i > 0 && i < n;
                    var sw = new Rect(x, Mathf.Round(r.center.y - D.SwatchD * 0.5f), D.SwatchD, D.SwatchD);
                    float op = Draw.Opacity;
                    if (used) Draw.Opacity *= D.MarkDim;
                    int k = i;
                    if (Swatch.In(sw, Pal.BallTeams[i], i == cur, used ? ColorNames[i] + " · 다른 팀 색" : ColorNames[i]) && !used) c.Color = (sbyte)(k == 0 ? -1 : k);
                    Draw.Opacity = op;
                    x += D.SwatchD + D.InnerGap;
                }
            }) + gap;

            // 홈구장(바로 바뀜)
            var venues = Stadium.Venues;
            var items = new string[venues.Length];
            int vc = -1;
            for (int i = 0; i < venues.Length; i++)
            {
                items[i] = venues[i].Name + (venues[i].Campus ? " · 대학 경기장" : " · 축구 경기장");
                if (venues[i].Id == Stadium.Home) vc = i;
            }
            string text = venues.Length == 0 ? "도시에 경기장이 없어요" : vc >= 0 ? items[vc] : "고르기";
            string help = venues.Length == 0 ? "축구 경기장(Match Day)이나 대학 경기장(Campus)을 지으면 골라요" : $"도시의 경기장 {venues.Length}곳 중에서 골라요. 바꾸면 바로 저장돼요";
            y = Field(b, y, "홈구장", fieldH, help, false, r => Select.In(r, text, null, items, null, vc, i =>
            {
                Stadium.Pick(venues[i].Id);
                Toast.Show(venues[i].Name + "을 홈구장으로 정했어요", ToastKind.Success);
            }, venues.Length > 0)) + gap;

            // 시작 전력(새 시즌부터)
            y = Field(b, y, "시작 전력", powerH, $"새 시즌을 시작할 때만 바뀌어요. 지금 시즌은 {PowerLabels[s.Power0]}", false, r => Segmented.In(r, PowerLabels, c.Power, i => c.Power = (byte)i)) + gap;

            // 저장 · 되돌리기
            bool changed = c.Name != s.Club.Name || c.Manager != s.Club.Manager || (c.Abbr[0] ?? "") != (s.Club.Abbr[0] ?? "") || c.Color != s.Club.Color || c.Power != s.Club.Power;
            float by = y + Button.H * 0.5f;
            float bw = Button.At(b.x, by, null, "저장", null, Tone.Primary, null, changed && !nameBad && !abbrBad, () =>
            {
                var saved = c.Clone();
                saved.Name = saved.Name.Trim();
                saved.Manager = saved.Manager.Trim();
                League.SetClub(saved);
                draft = saved.Clone();
                draftOf = saved;
                Toast.Show("홈팀 설정을 저장했어요", ToastKind.Success);
            });
            Button.At(b.x + bw + Button.Gap, by, null, "되돌리기", null, Tone.Neutral, null, changed, () => draft = null);
            return area.y + h;
        }

        /// <summary>이름(위) · 칸(ctl 높이) · 도움말(아래 · 잘못이면 빨강). 다음 y 를 돌려줌.</summary>
        static float Field(Rect b, float y, string label, float ctl, string help, bool error, System.Action<Rect> control)
        {
            Line(new Rect(b.x, y, b.width, Stat.LabelH), label, S.SubInk, false, D.FontMin);
            y += Stat.LabelH + D.Gap;
            control(new Rect(b.x, y, b.width, ctl));
            y += ctl + D.Gap;
            Line(new Rect(b.x, y, b.width, Textfield.HelpH), help, error ? Pal.Red : Look.PillSub, false, D.FontMin);
            return y + Textfield.HelpH;
        }

        /// <summary>미리보기(히어로): 색 막대 · 이름 · 홈 · 감독 · 줄임 이름 알약, 아래 점수판(최근 내 경기 · 없으면 다음 경기).</summary>
        float HomeTeamPreview(Season s, Rect area)
        {
            var c = Draft(s);
            float headH = HeroHeadH;
            float h = PlateH(headH + S.HeroGap + D.LiveBarH);
            var b = Plate(new Rect(area.x, area.y, area.width, h), "미리보기", IconEye, null, true);
            string name = c.Name.Length > 0 ? c.Name : Rules.TeamOf(s.Naming, 0).Name, abbr = c.Abbr[0]?.Length > 0 ? c.Abbr[0] : Rules.Abbr(0);
            var color = Pal.BallTeams[c.Color >= 0 ? c.Color : 0];
            float cy = b.y + headH * 0.5f;
            if (Draw.Repaint) Draw.Round(new Rect(b.x, Mathf.Round(cy - D.TeamHeroMarkH * 0.5f), D.TeamHeroMarkW, D.TeamHeroMarkH), color, D.TeamHeroMarkW * 0.5f);
            float tx = b.x + D.TeamHeroMarkW + S.RowGap;
            float aw = Badge.Pair(0f, 0f, abbr, Look.PastelFill, Look.PastelInk, false);
            Badge.Pair(b.xMax - aw, cy, abbr, Look.PastelFill, Look.PastelInk);
            S.HeroTitle(new Rect(tx, b.y, b.xMax - aw - S.RowGap - tx, headH), name, null, $"홈 {Park(s, 0)} · 감독 {(c.Manager.Length > 0 ? c.Manager : s.RosterOf(0).Manager)}");

            // 점수판
            var row = new Rect(b.x, b.y + headH + S.HeroGap, b.width, D.LiveBarH);
            if (Draw.Repaint)
            {
                Draw.Round(row, S.Plate, Tile.Radius);
                Draw.RoundOutline(row, S.PlateLine, Tile.Radius);
            }
            var recent = Sorted(s, g => g.Played && Mine(g));
            var g0 = recent.Count > 0 ? recent[recent.Count - 1] : NextGame(s, Today);
            float x = row.x + Tile.PadX, rcy = row.center.y;
            float lw = Mathf.Ceil(Draw.TextWidth("점수판", D.FontMin));
            Line(new Rect(x, row.y, lw, row.height), "점수판", Look.PillSub, false, D.FontMin);
            x += lw + S.Gap;
            if (g0 == null) return area.y + h + S.Gap;
            int opp = Opponent(g0);
            bool home = g0.Home == 0;
            string score = g0.Played ? (home ? $"{g0.HomeRuns} : {g0.AwayRuns}" : $"{g0.AwayRuns} : {g0.HomeRuns}") : "- : -";
            if (Draw.Repaint) Draw.Round(new Rect(x, Mathf.Round(rcy - D.TeamMarkH * 0.5f), D.TeamMarkW, D.TeamMarkH), color, D.TeamMarkW * 0.5f);
            x += D.TeamMarkW + D.ActionGap;
            float nw = Mathf.Ceil(Draw.TextWidth(abbr, D.FontBody, true));
            Line(new Rect(x, row.y, nw + D.Gap, row.height), abbr, Look.BarInk, true);
            x += nw + S.RowGap;
            float sw = Mathf.Ceil(Draw.TextWidth(score, D.FontValue, true));
            Line(new Rect(x, row.y, sw + D.Gap, row.height), score, Look.BarInk, true, D.FontValue);
            x += sw + S.RowGap;
            string oa = s.Abbr(opp);
            float ow = Mathf.Ceil(Draw.TextWidth(oa, D.FontBody, true));
            Line(new Rect(x, row.y, ow + D.Gap, row.height), oa, Look.BarOff, true);
            x += ow + D.ActionGap;
            Mark(x, rcy, opp);
            string state = g0.Played ? (g0.Box != null && g0.Box.Inn[0].Length > Sim.Innings ? g0.Box.Inn[0].Length + "회 끝" : "경기 끝") : Md(g0.Day) + " " + Clock(g0.Minute);
            float stw = Mathf.Ceil(Draw.TextWidth(state, D.FontMin));
            Line(new Rect(row.xMax - Tile.PadX - stw, row.y, stw, row.height), state, Look.PillSub, false, D.FontMin, TextAnchor.MiddleRight);
            return area.y + h + S.Gap;
        }

        /// <summary>원정 팀: 색 막대 · 이름 · 연고지 · 줄임 이름 · '고치기'(누르면 그 줄이 글 칸 + 저장 · 바로 저장).</summary>
        float AwayTeams(Season s, Rect area)
        {
            int n = Rules.TeamCount(s.Rule);
            const string note = "원정 팀은 도시 밖 가상 팀이라 경기장이 없어도 돼요. 줄임 이름은 점수판 · 알림 카드에 써요";
            float nh = NoteH(note, area.width - S.Pad * 2f);
            float h = PlateH(S.RowH * (n - 1) + S.HeadGap + nh);
            var b = Plate(new Rect(area.x, area.y, area.width, h), "원정 팀", TabIcons[TabTeams], $"{n - 1}팀 · 경기장 없음");
            float fieldW = D.StatColW + D.LineTotW;
            for (int t = 1; t < n; t++)
            {
                var row = new Rect(b.x, b.y + S.RowH * (t - 1), b.width, S.RowH);
                float x = Mark(row.x + S.RowPadL, row.center.y, t), right = row.xMax - S.RowPadL;
                int team = t;
                if (editAway == t)
                {
                    bool ok = AbbrOk(awayText);
                    right -= Button.At(right - Button.Width(null, "저장", true), row.center.y, null, "저장", null, Tone.Primary, null, ok, () =>
                    {
                        var c = s.Club.Clone();
                        c.Abbr[team] = awayText;
                        League.SetClub(c);
                        editAway = -1;
                        Toast.Show($"{s.Team(team).Name} 줄임 이름을 바꿨어요", ToastKind.Success);
                    }, true, true) + S.RowGap;
                    var field = new Rect(right - fieldW, Mathf.Round(row.center.y - Button.HCompact * 0.5f), fieldW, Button.HCompact);
                    awayText = CleanAbbr(Textfield.In(field, awayText, AbbrMax, Rules.Abbr(t), null, !ok, true, "baseball.away"));
                    right -= fieldW + S.RowGap;
                }
                else
                {
                    right -= Button.At(right - Button.Width(null, "고치기", true), row.center.y, null, "고치기", null, Tone.Neutral, null, true, () =>
                    {
                        editAway = team;
                        awayText = s.Club.Abbr[team] ?? "";
                    }, true, true) + S.RowGap;
                    string abbr = s.Abbr(t);
                    float aw = Mathf.Ceil(Draw.TextWidth(abbr, D.FontDesc, true));
                    Line(new Rect(right - aw, row.y, aw, row.height), abbr, Look.BarOff, true, D.FontDesc, TextAnchor.MiddleRight);
                    right -= aw + S.RowGap;
                }
                string lg = s.Rule == Rule.NPB ? Rules.LeagueName(s.Rule, Rules.LeagueOf(s.Rule, t)) + " · " : "";
                TwoLines(new Rect(x, row.y, right - x, row.height), s.Team(t).Name, lg + "연고지 " + s.Team(t).City);
            }
            Note(new Rect(b.x, b.yMax - nh, b.width, nh), note);
            return area.y + h;
        }
    }
}
