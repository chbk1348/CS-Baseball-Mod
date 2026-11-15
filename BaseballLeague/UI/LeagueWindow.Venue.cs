using System.Collections.Generic;
using BaseballLeague.Data;
using CSCommonUX;
using UnityEngine;
using D = CSCommonUX.CSCommonDesign;
using S = CSCommonUX.Section;

namespace BaseballLeague.UI
{
    /// <summary>
    /// 홈구장(시안 Stadium, 진입: 홈 › 빠른 실행 '홈구장'): 히어로(이름 · 종류 · 시즌 홈 경기 · 다음 홈 경기 · 카메라 이동 · 지금 입장 타일) · 도시의 경기장 · 시설 상태(건물 알림) · 이 구장 다음 경기.
    /// 입장 · 자리는 게임의 방문 칸을 셈(Stadium.Facts), 평균 · 최다 · 만원은 끝난 홈 경기 관중을 같은 단위끼리(실측이 있으면 실측만, 없으면 추정 — League.HomeCrowds), 예상은 League.Expected.
    /// 시안의 경기 날 주변 판(주변 차량 · 하차 · 상가 · 소음)은 게임에서 바로 읽히는 값이 없어 뺌 · 건물 정보 열기도 뺌.
    /// </summary>
    internal sealed partial class LeagueWindow
    {
        sealed class Facility
        {
            public string Name, Icon;
            public Notification.Problem1 Flags;
        }

        static readonly Facility[] Facilities =
        {
            new() { Name = "전기", Icon = IconBolt, Flags = Notification.Problem1.Electricity | Notification.Problem1.ElectricityNotConnected },
            new() { Name = "물 · 하수", Icon = "M12 3s6 6.5 6 11a6 6 0 0 1-12 0c0-4.5 6-11 6-11z", Flags = Notification.Problem1.Water | Notification.Problem1.WaterNotConnected | Notification.Problem1.Sewage },
            new() { Name = "쓰레기 수거", Icon = "M4 7h16M10 11v6M14 11v6M5 7l1 13h12l1-13M9 7V4h6v3", Flags = Notification.Problem1.Garbage },
            new() { Name = "도로 접근", Icon = "M5 17h14M5 12h14M8 7h8", Flags = Notification.Problem1.RoadNotConnected | Notification.Problem1.PathNotConnected },
            new() { Name = "화재", Icon = "M12 3c1 4 5 6 5 10a5 5 0 0 1-10 0c0-2 1-3 2-4 0 2 1 3 2 3 0-3-1-6 1-9z", Flags = Notification.Problem1.Fire },
        };

        float VenuePage(Season s, View v, float w)
        {
            float y = VenueHero(s, v, w);
            float half = (w - S.Gap) * 0.5f;
            float a = VenueList(s, new Rect(0f, y, half, 0f));
            float b = FacilityPlate(new Rect(half + S.Gap, y, half, 0f));
            return VenueNext(s, Mathf.Max(a, b) + S.Gap, w);
        }

        float VenueHero(Season s, View v, float w)
        {
            float tileH = TileH(true);
            float leftH = S.ChipH + S.HeroGap + HeroHeadH + S.HeroGap + tileH + S.HeroGap + Button.H;
            float h = S.HeroPadY * 2f + leftH;
            var r = new Rect(0f, 0f, w, h);
            HeroFrame(r);
            float x = S.HeroPadX, y = S.HeroPadY, leftW = w - S.HeroPadX * 2f - D.HeroSideW - S.HeroGap;
            ushort id = Stadium.Home;
            Stadium.Venue venue = null;
            foreach (var o in Stadium.Venues)
                if (o.Id == id) venue = o;
            float cw = S.ChipAt(x, y + S.ChipH * 0.5f, "홈구장", IconStadium);
            if (venue != null) S.Tag(x + cw + S.ChipGap, y + S.ChipH * 0.5f, venue.Campus ? "대학 경기장 건물 · Campus" : "축구 경기장 건물 · Match Day");
            y += S.ChipH + S.HeroGap;
            if (venue == null)
            {
                S.HeroTitle(new Rect(x, y, w - S.HeroPadX * 2f, HeroHeadH), "경기장이 없어요", null, "축구 경기장(Match Day)이나 대학 경기장(Campus)을 지으면 홈구장이 돼요. 그전엔 모든 경기를 모드가 정해요");
                return h + S.Gap;
            }
            var facts = Stadium.Now;
            float tx = Mark(x, y + S.HeroTitleH + S.HeroSubGap + S.HeroSubH * 0.5f, 0, D.TeamMarkW, D.LineSub, D.ActionGap);
            Line(new Rect(x, y, leftW, S.HeroTitleH), venue.Name, Look.BarInk, true, S.HeroTitleFont);
            Line(new Rect(tx, y + S.HeroTitleH + S.HeroSubGap, x + leftW - tx, S.HeroSubH), "홈 팀 " + s.Team(0).Name + (facts != null ? $" · 방문 자리 {facts.Seats:N0}" : ""), S.SubInk, false, S.HeroSubFont);
            y += HeroHeadH + S.HeroGap;

            int hw = 0, hl = 0, homeGames = 0, played = 0;
            foreach (var g in s.Games)
                if (g.Home == 0 && g.Series == 0)
                {
                    homeGames++;
                    if (!g.Played) continue;
                    played++;
                    int res = League.Result(g, 0);
                    if (res > 0) hw++;
                    else if (res < 0) hl++;
                }
            var c = v.Crowds ??= League.HomeCrowds(s); // 같은 단위끼리 · 실측이 있으면 실측만
            string kind = c.Real ? "실측" : "추정";
            var best = c.Best;
            var tiles = new Rect(x, y, leftW, tileH);
            StatTile(Cell(tiles, 4, 0, D.ActionGap), "시즌 홈 경기", $"{played} / {homeGames}", WinLoss(hw, hl), true);
            StatTile(Cell(tiles, 4, 1, D.ActionGap), "평균 관중", c.Games == 0 ? "-" : c.Avg.ToString("N0"), c.Games == 0 ? "홈 경기가 끝나면 보여요" : $"{kind} {c.Games}경기 · 자리의 {Mathf.RoundToInt(c.Avg * 100f / Mathf.Max(1, c.Seats))}%", true);
            StatTile(Cell(tiles, 4, 2, D.ActionGap), "최다 관중", best == null ? "-" : best.Box.Crowd.ToString("N0"), best == null ? "-" : $"{best.Day.Month}월 {best.Day.Day}일 vs {Short(s, best.Away)}", true);
            StatTile(Cell(tiles, 4, 3, D.ActionGap), "만원 경기", c.Full + "회", $"{kind} · {c.Seats:N0}명부터", true);
            y += tileH + S.HeroGap;
            Button.At(x, y + Button.H * 0.5f, null, "카메라 이동", null, Tone.Primary, null, true, () => Stadium.Focus(id));

            // 오른쪽 흰 타일: 오늘 입장(방문 칸에 와 있는 시민) / 오늘 홈 경기 예상 관중
            var side = new Rect(w - S.HeroPadX - D.HeroSideW, S.HeroPadY, D.HeroSideW, leftH);
            if (Draw.Repaint)
            {
                Draw.Round(side, S.Plate, Tile.Radius);
                Draw.RoundOutline(side, S.PlateLine, Tile.Radius);
            }
            var inner = new Rect(side.x + Tile.PadX, side.y + D.CardRowPad, side.width - Tile.PadX * 2f, side.height - D.CardRowPad * 2f);
            int inside = facts?.Inside ?? 0, seats = facts?.Seats ?? 0;
            var first = v.TodayHome.Count > 0 ? s.Games[v.TodayHome[0]] : null; // 오늘 첫 홈 경기
            int expect = first != null ? League.Expected(s, first, v.Rank) : 0;
            float t = expect > 0 ? Mathf.Clamp01((float)inside / expect) : seats == 0 ? 0f : Mathf.Clamp01((float)inside / seats);
            Stat.In(new Rect(inner.x, inner.y, inner.width, Stat.Height(true)), "오늘 입장", inside.ToString("N0") + "명", expect > 0 ? $"예상 {expect:N0}명" : $"방문 자리 {seats:N0}");
            float ly = inner.y + Stat.Height(true) + S.HeadGap;
            Progress.In(new Rect(inner.x, ly, inner.width, Progress.Bar), t, -1f, Pal.Primary);
            ly += Progress.Bar + S.HeadGap;
            Stat.Line(new Rect(inner.x, ly, inner.width, Stat.LineH), expect > 0 ? "예상 대비" : "자리 대비", Mathf.RoundToInt(t * 100f) + "%");
            ly += Stat.LineH + S.HeadGap;
            S.Hairline(new Rect(inner.x, ly, inner.width, 1f));
            string note = first == null ? "오늘은 홈 경기가 없어요. 경기장 건물에 와 있는 방문객을 1초마다 새로 읽어요"
                : v.Rank[first.Away] <= League.TopRanks ? $"{v.Rank[first.Away]}위 {Short(s, first.Away)}와의 경기라 평소보다 관중이 많을 것 같아요"
                : "경기장 건물에 와 있는 방문객을 1초마다 새로 읽어요";
            Note(new Rect(inner.x, ly + D.ActionGap, inner.width, inner.yMax - ly - D.ActionGap), note);
            return h + S.Gap;
        }

        float VenueList(Season s, Rect area)
        {
            var venues = Stadium.Venues;
            const string note = "경기장 건물이 있어야 홈 경기 결과를 게임이 정해요. 없으면 모드가 정해요";
            float nh = NoteH(note, area.width - S.Pad * 2f);
            float h = PlateH(S.RowH * Mathf.Max(1, venues.Length) + S.HeadGap + nh);
            var b = Plate(new Rect(area.x, area.y, area.width, h), "도시의 경기장", IconPin, venues.Length + "곳");
            if (venues.Length == 0) Line(new Rect(b.x, b.y, b.width, S.RowH), "도시에 경기장이 없어요", Look.PillSub);
            for (int i = 0; i < venues.Length; i++)
            {
                var o = venues[i];
                var row = new Rect(b.x, b.y + S.RowH * i, b.width, S.RowH);
                bool home = o.Id == Stadium.Home;
                float right = row.xMax - S.RowPadL;
                ushort id = o.Id;
                right -= Button.At(right - Button.Width(null, "카메라 이동", true), row.center.y, null, "카메라 이동", null, Tone.Neutral, null, true, () => Stadium.Focus(id), true, true) + S.RowGap;
                string state = home ? Short(s, 0) + " 홈" : "홈팀 없음";
                float bw = Badge.Pair(0f, 0f, state, Look.PastelFill, Look.PastelInk, false);
                Badge.Pair(right - bw, row.center.y, state, home ? Look.PastelFill : Look.Pill, home ? Look.PastelInk : Look.BarOff);
                right -= bw + S.RowGap;
                float x = row.x + S.RowPadL;
                if (Draw.Repaint) Draw.Round(new Rect(x, Mathf.Round(row.center.y - S.ChipH * 0.5f), D.TeamMarkW, S.ChipH), home ? TeamColor(0) : Look.PillLine, D.TeamMarkW * 0.5f);
                x += D.TeamMarkW + S.RowGap;
                TwoLines(new Rect(x, row.y, right - x, row.height), o.Name, o.Campus ? "대학 경기장 · Campus" : "축구 경기장 · Match Day");
            }
            Note(new Rect(b.x, b.yMax - nh, b.width, nh), note);
            return area.y + h;
        }

        float FacilityPlate(Rect area)
        {
            var facts = Stadium.Now;
            float h = PlateH(S.RowH * Facilities.Length);
            var b = Plate(new Rect(area.x, area.y, area.width, h), "시설 상태", IconPulse, "건물 알림 기준");
            for (int i = 0; i < Facilities.Length; i++)
            {
                var f = Facilities[i];
                var row = new Rect(b.x, b.y + S.RowH * i, b.width, S.RowH);
                bool bad = facts != null && (facts.Problems & f.Flags) != Notification.Problem1.None;
                float x = row.x + S.RowPadL, right = row.xMax - S.RowPadR;
                if (Draw.Repaint)
                {
                    var ic = new Rect(x, Mathf.Round(row.center.y - S.ChipH * 0.5f), S.ChipH, S.ChipH);
                    Draw.Round(ic, Look.Pill, S.ChipH * 0.5f);
                    Draw.Icon(f.Icon, new Rect(Mathf.Round(ic.center.x - S.ChipIcon * 0.5f), Mathf.Round(ic.center.y - S.ChipIcon * 0.5f), S.ChipIcon, S.ChipIcon), Look.BarOff);
                }
                x += S.ChipH + S.RowGap;
                string st = facts == null ? "-" : bad ? "주의" : "정상";
                Badge.In(new Rect(right - S.StateW, Mathf.Round(row.center.y - Badge.H * 0.5f), S.StateW, Badge.H), st, facts == null ? Pal.Sub : bad ? Pal.Warn : Pal.Primary);
                string val = facts == null ? "홈구장 없음" : bad ? "건물 알림이 떠 있어요" : "알림 없음";
                float vw = Mathf.Ceil(Draw.TextWidth(val, D.FontMin));
                Line(new Rect(right - S.StateW - S.RowGap - vw, row.y, vw, row.height), val, Look.PillSub, false, D.FontMin, TextAnchor.MiddleRight);
                Line(new Rect(x, row.y, right - S.StateW - vw - S.RowGap * 2f - x, row.height), f.Name, Look.BarInk, true);
            }
            return area.y + h;
        }

        float VenueNext(Season s, float y, float w)
        {
            const int count = 4;
            var next = ViewOf(s).NextHomes;
            int shown = Mathf.Min(count, next.Count);
            float h = PlateH(S.RowH * Mathf.Max(1, shown));
            var b = Plate(new Rect(0f, y, w, h), "이 구장 다음 경기", TabIcons[TabSchedule], $"남은 홈 경기 {next.Count}");
            if (shown == 0) Line(new Rect(b.x, b.y, b.width, S.RowH), "남은 홈 경기가 없어요", Look.PillSub);
            float dw = Mathf.Ceil(Draw.TextWidth("12/31 18:30", D.FontMin));
            for (int i = 0; i < shown; i++)
            {
                var g = next[i];
                var row = new Rect(b.x, b.y + S.RowH * i, b.width, S.RowH);
                float x = row.x + S.RowPadL;
                Line(new Rect(x, row.y, dw, row.height), Md(g.Day) + " " + Clock(g.Minute), Look.PillSub, false, D.FontMin);
                x = Mark(x + dw + S.RowGap, row.center.y, g.Away);
                string expect = "예상 " + League.Expected(s, g, ViewOf(s).Rank).ToString("N0");
                string tag = g.Day == Today ? "오늘 · " + expect : g.Series != 0 ? RoundName(s, s.Post[g.Series - 1]) + " · " + expect : expect;
                float tw = tag.Length == 0 ? 0f : Mathf.Ceil(Draw.TextWidth(tag, D.FontMin));
                if (tw > 0f) Line(new Rect(row.xMax - S.RowPadR - tw, row.y, tw, row.height), tag, Look.PillSub, false, D.FontMin, TextAnchor.MiddleRight);
                Line(new Rect(x, row.y, row.xMax - S.RowPadR - tw - S.RowGap - x, row.height), "vs " + s.Team(g.Away).Name, Look.BarInk, true);
            }
            return y + h;
        }
    }
}
