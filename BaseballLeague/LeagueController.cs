using System;
using BaseballLeague.Core;
using BaseballLeague.Data;
using BaseballLeague.UI;
using ICities;
using UnityEngine;

namespace BaseballLeague
{
    /// <summary>게임 화면에 붙는 본체: Esc 닫기 · 시즌 진행(날짜가 바뀔 때 · 홈구장 다시 찾기는 시뮬레이션 스레드에) · 알림 카드 · 리그 창 그리기 · 입력 막기. 매 프레임 처리는 Safe 로 감쌈.</summary>
    internal sealed class LeagueController : MonoBehaviour
    {
        public static LeagueController Instance { get; private set; }

        public readonly LeagueWindow Window = new();

        Action esc, tick, gui, notify;
        DateTime stadiumDay, olympicDay;
        string olympicError;

        void Awake()
        {
            Instance = this;
            useGUILayout = false; // GUILayout · Layout 이벤트를 안 씀 — Layout 패스를 건너뛰어 OnGUI 를 줄임
            esc = EscClose;
            tick = Tick;
            gui = DrawGui;
            notify = Window.NotifyTick;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            InputBlocker.Release();
        }

        void Update()
        {
            Safe.Run("Esc 닫기", esc);
            Safe.Run("시즌 진행", tick);
            Safe.Run("알림 카드", notify);
            InputBlocker.Begin("BaseballLeague");
            if (!ColossalFramework.UI.UIView.HasModalInput())
            {
                if (LeagueWindow.OwnLauncher) InputBlocker.Add(Window.LauncherRect);
                if (Window.Visible) InputBlocker.Add(Window.Rect);
                foreach (var r in Window.CardRects) InputBlocker.Add(r); // 카드 자리만 막음(게임 화면 나머지는 그대로)
            }
            InputBlocker.End();
        }

        void Tick()
        {
            var now = SimulationManager.instance.m_currentGameTime;
            // 날짜가 바뀔 때, 홈구장 모습을 보여 주는 화면이 열려 있으면 1초마다(입장 · 시설 상태)
            float real = Time.realtimeSinceStartup;
            if (now.Date != stadiumDay || Window.WantsStadium && real - stadiumAt >= StadiumSeconds)
            {
                stadiumDay = now.Date;
                stadiumAt = real;
                SimulationManager.instance.AddAction(Stadium.Refresh);
            }
            League.HomeSeats = Stadium.Now?.Seats ?? 0; // 관중 추정 · 만원 기준(게임 방문 자리)
            if (now.Date != olympicDay)
            {
                // 올림픽 모드 대회 기간(하루 한 번 · 없으면 휴식 없음)
                olympicDay = now.Date;
                Olympic.Read(Olympic.Find(), out League.BreakFrom, out League.BreakTo);
                if (Olympic.Error != null && Olympic.Error != olympicError) Log.Warn("올림픽 모드 대회 기간을 못 읽어 휴식 없이 진행: " + (olympicError = Olympic.Error));
            }
            League.Tick(now);
        }

        const float StadiumSeconds = 1f;
        float stadiumAt;

        void OnGUI()
        {
            if (ColossalFramework.UI.UIView.HasModalInput()) return;
            Draw.Begin();
            Safe.Run("화면 그리기", gui);
        }

        void DrawGui()
        {
            // 알림 카드: 입력은 창보다 먼저(카드 위 클릭은 써 버림), 그림은 창 위
            bool paint = Event.current.type == EventType.Repaint;
            if (!paint) Window.NotifyGui(true);
            if (LeagueWindow.OwnLauncher) Window.LauncherGui();
            Window.OnGUI();
            if (paint) Window.NotifyGui(false);
            CSCommonUX.Select.Flush();
            CSCommonUX.Toast.Flush();
            Draw.EndInput();
        }

        int escFrame = -1;

        void EscClose()
        {
            if (escFrame == Time.frameCount || !Window.Visible || Window.Confirming || !Input.GetKeyDown(KeyCode.Escape)) return;
            Window.Close();
            escFrame = Time.frameCount;
        }

        /// <summary>게임 Esc(EscapePatch): 창이 열려 있으면 닫고(확인 상자는 창이 닫음) true — 같은 프레임에 이미 닫았어도 true(일시정지 메뉴를 열지 않음).</summary>
        internal bool TakeEscape()
        {
            if (escFrame == Time.frameCount) return true;
            if (!Window.Visible) return false;
            if (!Window.Confirming) Window.Close();
            escFrame = Time.frameCount;
            return true;
        }

        /// <summary>개발용 자체 검사(일정 6시즌을 끝까지 돌림 · League.Current 를 잠깐 바꿈): 실패 수를 돌려주고 실패마다 fail. 옵션 패널 '자체 검사'(도시 밖)에서만 부름.</summary>
        internal static int SelfCheck(Action<string> fail)
        {
            int bad = 0;
            Action<bool, string> check = (ok, what) =>
            {
                if (ok) return;
                bad++;
                fail(what);
            };
            Schedule.SelfCheck(check);
            Sim.SelfCheck(check);
            UI.Live.SelfCheck(check);
            League.SelfCheck(check);
            Names.SelfCheck(check);
            SaveFormat.SelfCheck(check);
            Log.Info("자체 검사 시뮬 평균: " + Sim.Stats + $" · 끝난 시즌 세이브 {SaveFormat.LastSize / 1024}KB");
            return bad;
        }
    }

    /// <summary>게임 Esc(GameKeyShortcuts.Escape)를 먼저 받음: 리그 창이 열려 있거나 이번 프레임에 Esc 로 닫았으면 일시정지 메뉴를 열지 않음. 게임 판에 메서드가 없으면 이 패치만 건너뜀.</summary>
    [HarmonyLib.HarmonyPatch]
    [CSCommonOptionalPatch]
    internal static class EscapePatch
    {
        static System.Reflection.MethodBase TargetMethod() => HarmonyLib.AccessTools.Method(typeof(GameKeyShortcuts), "Escape");

        static bool Prepare()
        {
            if (TargetMethod() != null) return true;
            Log.Warn("GameKeyShortcuts.Escape 를 찾지 못해 Esc 패치를 쉼(게임 판 확인)");
            return false;
        }

        static bool Prefix()
        {
            try
            {
                return !(LeagueController.Instance?.TakeEscape() ?? false);
            }
            catch (Exception e)
            {
                Safe.Report("Esc", e);
                return true;
            }
        }
    }

    /// <summary>도시를 불러오고 내릴 때 본체를 붙이고 뗌(CSCommon CSCommonLoading: 게임 모드에서만). 세이브에 시즌이 없으면 새 시즌.</summary>
    public sealed class Hooks : CSCommonLoading<OptionsPanel, BaseballPatcher>
    {
        GameObject host;

        protected override void LoadedActions(LoadMode mode)
        {
            host = new GameObject("BaseballLeague");
            host.AddComponent<LeagueController>();
            // 본체를 먼저 붙임: 새 시즌 만들기가 던져도 창은 열림(시즌 없음으로)
            Safe.Run("새 시즌", () =>
            {
                if (League.Current != null) return;
                var s = League.NewSeason(Rule.KBO, Naming.Korean, SimulationManager.instance.m_currentGameTime);
                Log.Info($"{(SaveFormat.Unread != null ? BaseballSave.UnreadText + " · 임시 " : "새 ")}시즌 · {s.Rule} · 개막 {s.Start:yyyy-MM-dd} · 경기 {s.Games.Count}");
            });
        }

        public override void OnLevelUnloading()
        {
            var h = host;
            host = null;
            Safe.Run("도시 내리기: 본체 없애기", () =>
            {
                if (h != null) UnityEngine.Object.Destroy(h);
            });
            League.Reset();
            Stadium.Home = 0;
            Stadium.Name = "";
            Stadium.Venues = new Stadium.Venue[0];
            Stadium.Now = null;
            BallparkAudio.Reset();
            base.OnLevelUnloading();
        }
    }
}
