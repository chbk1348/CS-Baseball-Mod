using System;
using System.Collections.Generic;
using BaseballLeague.Core;
using BaseballLeague.Data;
using ColossalFramework.UI;
using HarmonyLib;

namespace BaseballLeague
{
    /// <summary>
    /// 홈구장: 도시의 경기장(축구 경기장 · Campus 대학 경기장 — PlayerBuildingAI.m_supportEvents 에 Football · VarsitySportsMatch) 하나.
    /// 고르기 · 이름 읽기는 시뮬레이션 스레드(Refresh 를 SimulationManager.AddAction 으로), 세이브 · 화면은 읽기만.
    /// 게임이 PlayerBuildingAI.CheckEvents 로 경기 이벤트를 스스로 열므로, 홈구장에서 끝난 이벤트의 승패를 이벤트 시작일 · 다음 날 홈 경기 결과로 씀(League.HomeTarget).
    /// </summary>
    internal static class Stadium
    {
        const EventManager.EventType Sports = EventManager.EventType.Football | EventManager.EventType.VarsitySportsMatch;

        /// <summary>도시의 경기장 하나: 건물 번호 · 이름 · 대학 경기장(Campus)인지.</summary>
        internal sealed class Venue
        {
            public ushort Id;
            public string Name;
            public bool Campus;
        }

        /// <summary>홈구장 지금 모습(게임 값): 안에 있는 방문객 · 방문 자리 · 건물 알림(Notification.Problem1).</summary>
        internal sealed class Facts
        {
            public int Inside, Seats;
            public Notification.Problem1 Problems;
        }

        /// <summary>홈구장 건물 번호(0 = 없음 · 모든 경기를 모드가 정함). 세이브가 불러 넣고 Refresh 가 확인.</summary>
        public static volatile ushort Home;

        /// <summary>화면에 쓰는 홈구장 이름(없으면 빈 문자열).</summary>
        public static volatile string Name = "";

        /// <summary>도시의 경기장 목록(건물 번호 차례). 시뮬레이션 스레드가 통째로 갈아 끼우고 화면은 읽기만.</summary>
        public static volatile Venue[] Venues = new Venue[0];

        /// <summary>홈구장 모습(없으면 null). Refresh 가 통째로 갈아 끼움.</summary>
        public static volatile Facts Now;

        /// <summary>시뮬레이션 스레드: 경기장 목록 · 홈구장 모습을 새로 읽음. 지금 홈구장이 아직 경기장이면 그대로, 아니면 처음 찾은 경기장.</summary>
        public static void Refresh()
        {
            try
            {
                var bm = BuildingManager.instance;
                var list = new System.Collections.Generic.List<Venue>();
                for (int i = 1; i < bm.m_buildings.m_size; i++)
                    if (IsStadium(bm, (ushort)i))
                        list.Add(new Venue { Id = (ushort)i, Name = bm.GetBuildingName((ushort)i, InstanceID.Empty) ?? "", Campus = bm.m_buildings.m_buffer[i].Info.m_buildingAI is VarsitySportsArenaAI });
                ushort id = Home;
                if (!IsStadium(bm, id)) id = list.Count > 0 ? list[0].Id : (ushort)0;
                Venues = list.ToArray();
                Now = id == 0 ? null : Read(bm, id);
                Name = id == 0 ? "" : bm.GetBuildingName(id, InstanceID.Empty) ?? "";
                Home = id;
            }
            catch (Exception e)
            {
                Safe.Report("홈구장 찾기", e);
            }
        }

        /// <summary>본 스레드: 홈구장을 id 로 바꿈(시뮬레이션 스레드에서 경기장인지 확인 · 세이브에 남음).</summary>
        public static void Pick(ushort id) => SimulationManager.instance.AddAction(() =>
        {
            if (IsStadium(BuildingManager.instance, id)) Home = id;
            Refresh();
        });

        /// <summary>본 스레드: 카메라를 경기장 건물로.</summary>
        public static void Focus(ushort id)
        {
            if (id == 0 || id >= BuildingManager.instance.m_buildings.m_size) return;
            ToolsModifierControl.cameraController.SetTarget(new InstanceID { Building = id }, BuildingManager.instance.m_buildings.m_buffer[id].m_position, true);
        }

        /// <summary>방문 칸(CitizenUnit Visit · 칸마다 5명)을 훑어 자리 · 지금 안에 있는 방문객을 셈.</summary>
        static Facts Read(BuildingManager bm, ushort id)
        {
            ref var b = ref bm.m_buildings.m_buffer[id];
            var f = new Facts { Problems = b.m_problems.m_Problems1 };
            var cm = CitizenManager.instance;
            uint unit = b.m_citizenUnits;
            for (int guard = 0; unit != 0 && guard < cm.m_units.m_size; guard++)
            {
                ref var u = ref cm.m_units.m_buffer[unit];
                if ((u.m_flags & CitizenUnit.Flags.Visit) != 0)
                    for (int i = 0; i < 5; i++)
                    {
                        f.Seats++;
                        uint c = u.GetCitizen(i);
                        if (c != 0 && cm.m_citizens.m_buffer[c].m_visitBuilding == id && cm.m_citizens.m_buffer[c].CurrentLocation == Citizen.Location.Visit) f.Inside++;
                    }
                unit = u.m_nextUnit;
            }
            return f;
        }

        static bool IsStadium(BuildingManager bm, ushort id)
        {
            if (id == 0 || id >= bm.m_buildings.m_size) return false;
            ref var b = ref bm.m_buildings.m_buffer[id];
            return (b.m_flags & (Building.Flags.Created | Building.Flags.Deleted)) == Building.Flags.Created && b.Info?.m_buildingAI is PlayerBuildingAI ai && (ai.m_supportEvents & Sports) != 0;
        }

        /// <summary>홈구장 이벤트면 승률(0 ~ 100)을 결과를 넣을 그 홈 경기(League.HomeTarget) 상대와의 전력 차로 조정.</summary>
        internal static void AdjustWin(ref EventData data, ref float result)
        {
            try
            {
                if (Home != 0 && data.m_building == Home) result = League.HomeWinChance(result, data.StartTime);
            }
            catch (Exception e)
            {
                Safe.Report("홈 경기 승률 조정", e);
            }
        }
    }

    /// <summary>축구 경기장 경기 승률(Varsity 는 따로 — 거기서 base 를 부르면 두 번 조정되지 않게 건너뜀).</summary>
    [HarmonyPatch(typeof(SportMatchAI), "GetWinProbability")]
    internal static class WinChancePatch
    {
        static void Postfix(SportMatchAI __instance, ref EventData data, ref float __result)
        {
            if (!(__instance is VarsitySportsMatchAI)) Stadium.AdjustWin(ref data, ref __result);
        }
    }

    /// <summary>대학 경기장(Campus) 경기 승률.</summary>
    [HarmonyPatch(typeof(VarsitySportsMatchAI), "GetWinProbability")]
    internal static class VarsityWinChancePatch
    {
        static void Postfix(ref EventData data, ref float __result) => Stadium.AdjustWin(ref data, ref __result);
    }

    /// <summary>
    /// 경기 끝(시뮬레이션 스레드 · VarsitySportsMatchAI.EndEvent 도 base 로 여기를 지남): 홈구장 경기면 BeginEvent 가 정한 Success / Failure 를 대기열에.
    /// 취소(고장 · 문제 · 이전)는 EndEvent 없이 Cancel 로 끝나 결과가 없음 → 그 경기는 날짜가 지나면 모드가 정함.
    /// </summary>
    [HarmonyPatch(typeof(SportMatchAI), "EndEvent")]
    internal static class MatchEndPatch
    {
        /// <summary>끝나기 전 관중: EventAI.CountVisitors 의 와 있는 방문객 · 최대 방문객(게임 값 · 끝난 뒤엔 흩어질 수 있어 앞에서 셈).</summary>
        static void Prefix(ushort eventID, ref EventData data, out KeyValuePair<int, int> __state)
        {
            __state = default;
            try
            {
                if (Stadium.Home == 0 || data.m_building != Stadium.Home || data.Info?.m_eventAI == null) return;
                data.Info.m_eventAI.CountVisitors(eventID, ref data, out int arrived, out _, out int max);
                __state = new KeyValuePair<int, int>(arrived, max);
            }
            catch (Exception e)
            {
                Safe.Report("홈 경기 관중 세기", e);
            }
        }

        static void Postfix(ref EventData data, KeyValuePair<int, int> __state)
        {
            try
            {
                if (Stadium.Home == 0 || data.m_building != Stadium.Home) return;
                if ((data.m_flags & (EventData.Flags.Success | EventData.Flags.Failure)) == 0) return;
                League.QueueHomeResult(data.StartTime, (data.m_flags & EventData.Flags.Success) != 0, __state.Key, __state.Value);
            }
            catch (Exception e)
            {
                Safe.Report("홈 경기 결과 받기", e);
            }
        }
    }

    /// <summary>
    /// 경기장 정보 창(게임 FootballPanel · VarsitySportsArenaPanel.RefreshMatchInfo 가 SportMatchAI.GetTeamName · GetOpponentName · GetOpponentColor 로 매 갱신 채움) 뒤에
    /// 홈구장이면 글 · 색만 리그 이름 · 팀 색으로 덮음(본 스레드 · 게임 이벤트 값 · 세이브는 그대로). 리그 없음 · 올림픽 휴식 · 올림픽 모드가 붙인 경기 · 맞출 홈 경기 없음은 게임 그대로.
    /// </summary>
    internal static class MatchPanel
    {
        const EventData.Flags Ended = EventData.Flags.Completed | EventData.Flags.Cancelled | EventData.Flags.Expired;

        /// <summary>outline = 그 창의 UpdateOutlineColor(글 색에 맞춘 테두리), prefix = 게임이 팀 이름 칸 앞에 붙이는 글, label 색은 Campus 창만 바꿈(축구 창은 게임도 안 바꿈).</summary>
        /// <summary>칸 폭(게임 값 · 디컴파일): 'A vs B' 이름 칸 = 축구 220 · 대학 213(게임이 줄이는 폭), 팀 이름 줄 = 대학 407. 넘치면 글자를 줄이고 원래 크기의 MinScale 까지.</summary>
        internal const float FootballNameW = 220f, VarsityNameW = 213f, LabelW = 407f, MinScale = 0.6f;

        /// <summary>칸마다 게임 원래 글자 크기(처음 덮을 때 기억 · 손 뗄 때 되돌림).</summary>
        static readonly Dictionary<UILabel, float> baseScale = new();

        /// <summary>글이 max 를 넘으면 글자 크기를 칸에 맞게 줄임(말줄임 없음 · 하한 MinScale). 지금 폭 · 크기로 원래 크기 폭을 셈(autoSize 칸) · 바뀔 때만 씀.</summary>
        static void Fit(UILabel l, float max)
        {
            if (!baseScale.TryGetValue(l, out float b)) baseScale[l] = b = l.textScale;
            float natural = l.textScale > 0f ? l.width * b / l.textScale : l.width;
            float want = natural > max ? Math.Max(b * MinScale, b * max / natural) : b;
            if (Math.Abs(l.textScale - want) > 0.001f) l.textScale = want;
        }

        static void Restore(UILabel l)
        {
            if (l != null && baseScale.TryGetValue(l, out float b) && l.textScale != b) l.textScale = b;
        }

        internal static void Apply(object panel, System.Reflection.MethodInfo outline, ushort building, UILabel label, string prefix, bool colorLabel, float nameW, UILabel one, UILabel two, UIPanel past, UISprite a, UISprite b, UISprite c)
        {
            bool mineOn = false, twoOn = false;
            try
            {
                var s = League.Current;
                if (s == null || building == 0 || building != Stadium.Home || League.OnBreak(SimulationManager.instance.m_currentGameTime.Date)) return;
                var events = EventManager.instance.m_events.m_buffer;
                ushort ev = BuildingManager.instance.m_buildings.m_buffer[building].m_eventIndex;
                if (ev == 0 || !(events[ev].Info?.m_eventAI is SportMatchAI) || Olympic.IsAttached(ev)) return;
                var data = events[ev];
                string mine = s.Team(0).Name;
                var mineColor = Pal.BallTeams[s.ColorOf(0)];
                label.text = prefix + mine;
                one.text = mine;
                mineOn = true;
                Fit(label, LabelW);
                Fit(one, nameW);
                one.textColor = mineColor;
                if (colorLabel) label.textColor = mineColor;
                foreach (var sp in new[] { a, b, c }) if (sp != null) sp.color = mineColor;
                int away = League.EventOpponent(s, data.StartTime, (data.m_flags & Ended) != 0);
                if (away >= 0)
                {
                    two.text = s.Team(away).Name;
                    two.textColor = Pal.BallTeams[s.ColorOf(away)];
                    twoOn = true;
                    Fit(two, nameW);
                }
                outline?.Invoke(panel, new object[] { one });
                outline?.Invoke(panel, new object[] { two });
                if (colorLabel) outline?.Invoke(panel, new object[] { label });
                // 지난 경기 줄(게임과 같은 차례: 이 이벤트의 m_nextBuildingEvent 사슬, 지금 경기가 취소면 첫 줄이 지금 경기) 툴팁 = 상대
                bool cancelled = (data.m_flags & EventData.Flags.Cancelled) != 0;
                for (int i = 1; i <= 6; i++)
                {
                    ushort id = i == 1 && cancelled ? ev : data.m_nextBuildingEvent;
                    if (id == 0) break;
                    data = events[id];
                    var row = past?.Find<UISlicedSprite>("PastMatch " + i);
                    if (row == null || Olympic.IsAttached(id)) continue;
                    int o = League.EventOpponent(s, data.StartTime, true);
                    if (o >= 0) row.tooltip = s.Team(o).Name;
                }
            }
            catch (Exception e)
            {
                Safe.Report("경기장 정보 창 팀 이름", e);
            }
            finally
            {
                // 게임 이름으로 돌아가면 게임 글자 크기로
                if (!mineOn)
                {
                    Restore(label);
                    Restore(one);
                }
                if (!twoOn) Restore(two);
            }
        }
    }

    [HarmonyPatch(typeof(FootballPanel), "RefreshMatchInfo")]
    [CSCommonOptionalPatch]
    internal static class FootballPanelPatch
    {
        static readonly System.Reflection.MethodInfo Outline = AccessTools.Method(typeof(FootballPanel), "UpdateOutlineColor");

        static void Postfix(FootballPanel __instance, InstanceID ___m_InstanceID, UILabel ___m_labelTeamName, UILabel ___m_teamName1, UILabel ___m_teamName2, UIPanel ___m_panelPastMatches,
            UISprite ___m_teamColorIcon, UISprite ___m_teamColorIconNoButton, UISlicedSprite ___m_slicedSpriteTeamColor) =>
            MatchPanel.Apply(__instance, Outline, ___m_InstanceID.Building, ___m_labelTeamName, " ", false, MatchPanel.FootballNameW, ___m_teamName1, ___m_teamName2, ___m_panelPastMatches, ___m_teamColorIcon, ___m_teamColorIconNoButton, ___m_slicedSpriteTeamColor);
    }

    [HarmonyPatch(typeof(VarsitySportsArenaPanel), "RefreshMatchInfo")]
    [CSCommonOptionalPatch]
    internal static class VarsityPanelPatch
    {
        static readonly System.Reflection.MethodInfo Outline = AccessTools.Method(typeof(VarsitySportsArenaPanel), "UpdateOutlineColor");

        static void Postfix(VarsitySportsArenaPanel __instance, InstanceID ___m_InstanceID, UILabel ___m_labelTeamName, UILabel ___m_teamName1, UILabel ___m_teamName2, UIPanel ___m_panelPastMatches,
            UISprite ___m_homeTeamIcon, UISlicedSprite ___m_slicedSpriteTeamColor) =>
            MatchPanel.Apply(__instance, Outline, ___m_InstanceID.Building, ___m_labelTeamName, "", true, MatchPanel.VarsityNameW, ___m_teamName1, ___m_teamName2, ___m_panelPastMatches, ___m_homeTeamIcon, ___m_slicedSpriteTeamColor, null);
    }
}
