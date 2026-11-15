using System;
using System.Collections.Generic;
using BaseballLeague.Core;
using BaseballLeague.Data;
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
}
