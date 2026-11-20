using System.Collections.Generic;
using BaseballLeague.Core;
using CSCommonUX;

namespace BaseballLeague.UI
{
    /// <summary>인게임 설정 패널(CSCommon): 모드 허브 연결 · 야구장 소리 · 개발용 자체 검사. 리그 규칙 · 시즌은 도시마다 달라 리그 창 설정 탭에서.</summary>
    public sealed class OptionsPanel : CSCommonOptionsPanel
    {
        protected override string LogoIcon => ModHubEntry.Icon;

        protected override void Build()
        {
            var main = AddPage("일반");
            var s = BaseballSetting.Current;
            main.Section("퀵 버튼", null, Pal.Primary).Toggle("모드 허브 연결", "켜면 모드 허브가 있을 때 이 모드가 허브 목록에 들어가요. 끄거나 허브가 없으면 화면 왼쪽 퀵 버튼으로 리그 창을 열어요.", () => s.HubLinked, v =>
            {
                s.HubLinked = v;
                BaseballSetting.Save();
            });
            main.Section("소리", null, Pal.Primary).Toggle("야구장 소리", "켜면 홈구장에서 리그 홈 경기가 열리는 동안 축구 소리 대신 관중 · 타격음 · 박수 · 응원 북 소리가 나요.", () => s.BallparkSound, v =>
            {
                s.BallparkSound = v;
                BaseballSetting.Save();
            });
            main.Note("리그 규칙 · 팀 이름 방식 · 새 시즌은 도시를 불러온 뒤 리그 창 설정에서 바꿔요.");
            // 검사가 League.Current 를 잠깐 바꾸므로 도시가 없을 때만(저장 · 시즌 진행과 겹치지 않게)
            main.Section("개발", null, Pal.Primary).Button("자체 검사", "일정 · 경기 시뮬레이션 · 진행 표시 · 순위 · 포스트시즌 · 명단 · 이름 · 세이브를 시즌 6개로 끝까지 돌려 봐요. 도시를 불러오기 전 메인 메뉴에서만 눌러요.", "검사하기", RunSelfCheck, CSCommonButtonKind.Outline, () => LeagueController.Instance == null);
        }

        static void RunSelfCheck()
        {
            var fails = new List<string>();
            int bad = 0;
            Safe.Run("자체 검사", () => bad = LeagueController.SelfCheck(fails.Add));
            foreach (var f in fails) Log.Warn("자체 검사 실패: " + f);
            Log.Info(bad == 0 ? "자체 검사 통과" : $"자체 검사 실패 {bad}건");
            CSCommonDialog.Notice("자체 검사", new[] { bad == 0 ? "모든 검사를 통과했어요." : $"{bad}건이 실패했어요. output_log.txt 의 [프로야구 리그]에도 남겼어요." }, fails.ToArray());
        }
    }
}
