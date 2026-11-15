using BaseballLeague.Data;
using CSCommonUX;
using UnityEngine;
using D = CSCommonUX.CSCommonDesign;
using S = CSCommonUX.Section;

namespace BaseballLeague.UI
{
    /// <summary>
    /// 설정 FAB(시안 Settings · 일반): 왼쪽 = 리그 규칙(KBO · NPB · 시즌 진행 중 잠김 · 규칙 요약) · 시즌(새 시즌 시작) · 경기(홈 경기 승률 조정), 오른쪽 = 알림 · 표시(경기 결과 · 시작 · 큰 소식 · 화면 위 점수판) · 상태 진단.
    /// 시안의 머리 줄 분류 탭(팀 · 세이브 · 정보), 경기 수 · 팀 수 · 포스트시즌 방식 · 일정 규칙 스위치(규칙에 묶여 고를 값이 없음), 원정 점수 · 경기장 없으면 원정만 스위치 · 승률 조정 폭,
    /// 창 열기 단축키(허브가 염), 기록 지우기는 뺌(팀 이름 방식은 홈팀 설정에).
    /// </summary>
    internal sealed partial class LeagueWindow
    {
        static readonly string[] RuleLabels = { "KBO", "NPB" }, RuleSubs = { "한국 프로야구 · 10팀", "일본 프로야구 · 12팀" };

        static readonly string[][] RuleRows =
        {
            new[] { "리그", "한 리그 10팀", "정규 시즌", "팀당 144경기 · 상대마다 16경기", "리그 사이 경기", "없음", "포스트시즌", "5팀 · 와일드카드 → 준PO → PO → 한국시리즈",
                "단기전", "와일드카드 4위 1승 안고 · 한국시리즈 4선승", "경기 시작", "평일 18:30 · 주말 17:00", "우천 취소", "예비일 · 더블헤더" },
            new[] { "리그", "센트럴 6팀 · 퍼시픽 6팀", "정규 시즌", "팀당 143경기 · 같은 리그 125 + 교류전 18", "리그 사이 경기", "교류전 · 5 ~ 6월", "포스트시즌", "리그별 3팀 · 클라이맥스 시리즈 → 일본시리즈",
                "단기전", "퍼스트 2선승 · 파이널 4선승(1위 1승) · 일본시리즈 4선승", "경기 시작", "평일 18:00 · 주말 14:00", "우천 취소", "예비일" },
        };

        float SettingsTab(Season s, View v, float w)
        {
            float half = (w - S.Gap) * 0.5f;
            float y = RulePlate(s, half);
            y = SeasonPlate(s, v, y + S.Gap, half);
            var c = s.Club;
            y = Switches(new Rect(0f, y + S.Gap, half, 0f), "경기", IconBall, new[]
            {
                new SwitchRow("홈 경기 승률 조정", "팀 전력으로 게임 경기 이벤트의 승률을 바꿔요", c.AdjustWin, x => x.AdjustWin = !x.AdjustWin),
            });
            float r = Switches(new Rect(half + S.Gap, 0f, half, 0f), "알림 · 표시", IconBell, new[]
            {
                new SwitchRow("경기 결과 알림", "내 팀 경기가 끝나면 오른쪽 위에 점수 카드를 띄워요", c.NotifyResult, x => x.NotifyResult = !x.NotifyResult),
                new SwitchRow("홈 경기 시작 알림", "플레이볼 30분 전", c.NotifyStart, x => x.NotifyStart = !x.NotifyStart),
                new SwitchRow("우승 · 포스트시즌 알림", "진출 확정 · 우승 · MVP", c.NotifyBig, x => x.NotifyBig = !x.NotifyBig),
                new SwitchRow("화면 위 점수판", "홈 경기 중에 작은 점수판을 띄워요", c.LiveBoard, x => x.LiveBoard = !x.LiveBoard),
            });
            r = Diagnosis(s, new Rect(half + S.Gap, r + S.Gap, half, 0f));
            return Mathf.Max(y, r);
        }

        const string IconBell = "M6 16V11a6 6 0 0 1 12 0v5l1.5 2h-15zM10 20a2 2 0 0 0 4 0";

        sealed class SwitchRow
        {
            public readonly string Name, Desc;
            public readonly bool On;
            public readonly System.Action<Club> Flip;

            public SwitchRow(string name, string desc, bool on, System.Action<Club> flip)
            {
                Name = name;
                Desc = desc;
                On = on;
                Flip = flip;
            }
        }

        /// <summary>스위치 판(시안 '경기' · '알림 · 표시'): 줄마다 이름 · 설명 · 오른쪽 스위치, 누르면 편집값을 통째로 바꿔 바로 저장(세이브).</summary>
        static float Switches(Rect area, string title, string icon, SwitchRow[] rows)
        {
            float h = PlateH(D.SettingRowH * rows.Length);
            var b = Plate(new Rect(area.x, area.y, area.width, h), title, icon, "세이브에 남아요");
            for (int i = 0; i < rows.Length; i++)
            {
                var t = rows[i];
                var row = new Rect(b.x, b.y + D.SettingRowH * i, b.width, D.SettingRowH);
                if (i > 0) S.Hairline(new Rect(row.x, row.y, row.width, 1f));
                var sw = new Rect(row.xMax - S.RowPadL - D.SwitchW, Mathf.Round(row.center.y - D.SwitchH * 0.5f), D.SwitchW, D.SwitchH);
                if (Switch.In(sw, t.On))
                {
                    var c = League.Current.Club.Clone();
                    t.Flip(c);
                    League.SetClub(c);
                }
                TwoLines(new Rect(row.x + S.RowPadL, row.y, sw.x - S.RowGap - row.x - S.RowPadL, row.height), t.Name, t.Desc);
            }
            return area.y + h;
        }

        float RulePlate(Season s, float w)
        {
            bool locked = s.InProgress;
            var rows = RuleRows[(int)s.Rule];
            int n = rows.Length / 2;
            string note = s.Rule == Rule.KBO ? "팀 이름은 홈팀 설정에서 한국형 · 일본형 중에 따로 골라요" : "내 도시 팀은 센트럴 리그에 들어가요. 팀 이름은 홈팀 설정에서 골라요";
            float segH = Segmented.SubH + Segmented.Pad * 2f, cardH = D.CardRowPad * 2f + D.LineBody + D.LineSub;
            float nh = NoteH(note, w - S.Pad * 2f);
            float h = PlateH(segH + S.Gap + cardH + S.Gap + D.TableRowH * n + S.Gap + nh);
            var b = Plate(new Rect(0f, 0f, w, h), "리그 규칙", TabIcons[TabRecords], locked ? "시즌 진행 중 · 잠김" : "바꾸면 새 시즌", true);
            float y = b.y;
            // TODO(CSCommon): Segmented 에 잠김(누를 수 없음 · 흐린 글) 상태가 없음 — 잠긴 동안 누르면 알림만 띄움
            Segmented.In(new Rect(b.x, y, b.width, segH), RuleLabels, (int)s.Rule, i =>
            {
                if (s.InProgress) Toast.Show("시즌이 진행 중이라 리그 규칙을 바꿀 수 없어요", ToastKind.Error);
                else
                {
                    League.NewSeason((Rule)i, s.Naming, Today);
                    Toast.Show(RuleLabels[i] + " 규칙으로 새 일정을 짰어요", ToastKind.Success);
                }
            }, null, RuleSubs);
            y += segH + S.Gap;

            // 잠김 안내 칸(잠기지 않아도 같은 높이로 안내 — 줄이 밀리지 않게)
            var card = new Rect(b.x, y, b.width, cardH);
            if (Draw.Repaint)
            {
                Draw.Round(card, S.Plate, Tile.Radius);
                Draw.RoundOutline(card, S.PlateLine, Tile.Radius);
                var ic = new Rect(card.x + D.CardRowPad, Mathf.Round(card.center.y - S.ChipH * 0.5f), S.ChipH, S.ChipH);
                Draw.Round(ic, Look.Pill, S.ChipH * 0.5f);
                Draw.Icon(locked ? IconLock : Icons.Check, new Rect(Mathf.Round(ic.center.x - D.Icon * 0.5f), Mathf.Round(ic.center.y - D.Icon * 0.5f), D.Icon, D.Icon), Look.BarOff);
            }
            float tx = card.x + D.CardRowPad + S.ChipH + S.RowGap;
            var text = new Rect(tx, card.y + D.CardRowPad, card.xMax - D.CardRowPad - tx, card.height - D.CardRowPad * 2f);
            TwoLinesSmall(text, locked ? $"{s.Start.Year} 시즌 진행 중이라 바꿀 수 없어요" : "지금 바꿀 수 있어요",
                locked ? "시즌이 끝나거나 새 시즌을 시작하면 바꿀 수 있어요" : "바꾸면 그 규칙으로 일정을 새로 짜요");
            y += cardH + S.Gap;

            float lw = 0f;
            for (int i = 0; i < n; i++) lw = Mathf.Max(lw, Mathf.Ceil(Draw.TextWidth(rows[i * 2], D.FontMin)));
            for (int i = 0; i < n; i++)
            {
                var row = new Rect(b.x, y + D.TableRowH * i, b.width, D.TableRowH);
                Line(new Rect(row.x + S.RowPadL, row.y, lw, row.height), rows[i * 2], S.SubInk, false, D.FontMin);
                float vx = row.x + S.RowPadL + lw + S.RowGap;
                Line(new Rect(vx, row.y, row.xMax - S.RowPadL - vx, row.height), rows[i * 2 + 1], Look.BarInk, false, D.FontDesc);
            }
            Note(new Rect(b.x, b.yMax - nh, b.width, nh), note);
            return h;
        }

        /// <summary>두 줄(굵은 13.5 · 아래 보조 12).</summary>
        static void TwoLinesSmall(Rect r, string top, string bottom)
        {
            Line(new Rect(r.x, r.y, r.width, D.LineBody), top, Look.BarInk, true, S.ChipFont);
            Line(new Rect(r.x, r.y + D.LineBody, r.width, D.LineSub), bottom, Look.PillSub, false, D.FontMin);
        }

        float SeasonPlate(Season s, View v, float y, float w)
        {
            bool locked = s.InProgress;
            string restart = locked ? "진행 중인 시즌을 지우고 새 시즌을 열어요. 우승 이력은 남아요" : "이번 시즌 우승은 이력에 남고 순위는 0부터 다시 시작해요";
            float nh = NoteH(restart, w - S.Pad * 2f);
            float h = PlateH(D.TableRowH * 2f + S.Gap + Button.H + S.HeadGap + nh);
            var b = Plate(new Rect(0f, y, w, h), "시즌", TabIcons[TabSchedule], locked ? "잠김" : null);
            string[] rows = { "개막", $"{s.Start.Year}년 {s.Start.Month}월 {s.Start.Day}일 ({WeekDays[(int)s.Start.DayOfWeek]})", "진행", $"내 팀 {v.MyPlayed} / {v.MyGames}경기 · 리그 {s.Played} / {s.Games.Count}경기" };
            float lw = Mathf.Ceil(Draw.TextWidth("개막", D.FontMin));
            for (int i = 0; i < 2; i++)
            {
                var row = new Rect(b.x, b.y + D.TableRowH * i, b.width, D.TableRowH);
                Line(new Rect(row.x + S.RowPadL, row.y, lw, row.height), rows[i * 2], S.SubInk, false, D.FontMin);
                float vx = row.x + S.RowPadL + lw + S.RowGap;
                Line(new Rect(vx, row.y, row.xMax - vx, row.height), rows[i * 2 + 1], Look.BarInk, false, D.FontDesc);
            }
            float by = b.y + D.TableRowH * 2f + S.Gap;
            Button.At(b.x, by + Button.H * 0.5f, null, "지금 새 시즌 시작", null, Tone.Danger, null, true, AskNewSeason);
            Note(new Rect(b.x, by + Button.H + S.HeadGap, b.width, nh), restart);
            return y + h;
        }

        /// <summary>상태 진단(게임에서 읽은 값): 경기장 · 홈구장 시설 · 다음 홈 경기 · 시즌 데이터.</summary>
        float Diagnosis(Season s, Rect area)
        {
            const int rowsN = 4;
            float h = PlateH(S.RowH * rowsN + S.Gap + Button.H);
            var b = Plate(new Rect(area.x, area.y, area.width, h), "상태 진단", IconPulse, "1초마다 확인");
            var venues = Stadium.Venues;
            var facts = Stadium.Now;
            var next = ViewOf(s).NextHomes;
            string problems = facts == null ? "" : Problems(facts.Problems);
            Diag(new Rect(b.x, b.y, b.width, S.RowH), "도시의 경기장", venues.Length == 0 ? "없음 · 모든 경기를 모드가 정해요" : $"{venues.Length}곳 · 홈구장 {Stadium.Name}", venues.Length > 0);
            Diag(new Rect(b.x, b.y + S.RowH, b.width, S.RowH), "홈구장 시설", facts == null ? "홈구장 없음" : problems.Length == 0 ? "건물 알림 없음" : problems, facts != null && problems.Length == 0);
            Diag(new Rect(b.x, b.y + S.RowH * 2f, b.width, S.RowH), "다음 홈 경기", next.Count == 0 ? "없음" : $"{next[0].Day.Month}월 {next[0].Day.Day}일 {Clock(next[0].Minute)} · {(Stadium.Home != 0 ? "경기장 이벤트" : "모드가 정함")}", next.Count > 0);
            Diag(new Rect(b.x, b.y + S.RowH * 3f, b.width, S.RowH), "시즌 데이터", SaveFormat.Unread != null ? "임시 시즌 · 못 읽은 세이브를 그대로 둬요" : $"{s.Rule} · 경기 {s.Games.Count} · 우승 이력 {s.History.Count}", SaveFormat.Unread == null);
            Button.At(b.x, b.yMax - Button.H * 0.5f, null, "다시 확인", null, Tone.Neutral, null, true, () =>
            {
                SimulationManager.instance.AddAction(Stadium.Refresh);
                Toast.Show("경기장을 다시 찾았어요", ToastKind.Info);
            });
            return area.y + h;
        }

        static void Diag(Rect row, string name, string value, bool ok)
        {
            float x = row.x + S.RowPadL, right = row.xMax - S.RowPadR;
            Badge.In(new Rect(right - S.StateW, Mathf.Round(row.center.y - Badge.H * 0.5f), S.StateW, Badge.H), ok ? "정상" : "확인", ok ? Pal.Primary : Pal.Warn);
            float nw = Mathf.Ceil(Draw.TextWidth("도시의 경기장", D.FontBody, true));
            Line(new Rect(x, row.y, nw, row.height), name, Look.BarInk, true);
            Line(new Rect(x + nw + S.RowGap, row.y, right - S.StateW - S.RowGap * 2f - nw - x, row.height), value, Look.PillSub, false, D.FontMin, TextAnchor.MiddleRight);
        }

        /// <summary>건물 알림 중 경기장에 닿는 것만 이름으로.</summary>
        static string Problems(Notification.Problem1 p)
        {
            var parts = new System.Collections.Generic.List<string>();
            foreach (var f in Facilities)
                if ((p & f.Flags) != Notification.Problem1.None) parts.Add(f.Name);
            return string.Join(" · ", parts.ToArray());
        }
    }
}
