// 모드 허브 연결 계약 v1. 원본: CS-YukiModBox/contract/ModHubEntry.cs — 고치면 원본도 같이.
// 허브(ModHub.dll)가 이 모드 어셈블리에서 이름 'ModHubEntry'(전역 이름공간)로 찾아 리플렉션으로 부름.
// 규칙: 정적 멤버 · 기본 형만(CSCommon 형식은 DLL 을 건너지 못함). 본 스레드에서만 불림. 던져도 허브가 잡음.
// 이 모드: 프로야구 리그(리그 창, 허브를 숨기는 도구 아님).
public static class ModHubEntry
{
    /// <summary>계약 판. 이름 · 뜻이 바뀌면 올림(덧붙이기만 하면 그대로).</summary>
    public const int ContractVersion = 1;

    /// <summary>바뀌지 않는 키(어셈블리 이름과 같게). 허브 설정의 순서 · 숨김이 이것으로 기억됨.</summary>
    public static string Id => "BaseballLeague";

    /// <summary>목록 이름(판 없이).</summary>
    public static string Name => "프로야구 리그";

    /// <summary>아이콘 SVG path(24 viewBox, 야구공 획).</summary>
    public static string Icon => "M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18z M7 4.5c2 2.5 2 12.5 0 15 M17 4.5c-2 2.5-2 12.5 0 15";

    /// <summary>강조색 이름: "Teal" · "F1Red" · "Sky"(CSCommonPalette 이름). 색 값을 쓰지 않음(CSC001). 선택(없으면 "Teal").</summary>
    public static string Accent => "Green"; // 야구 초록 팔레트(CSCommonPalette.Green)

    /// <summary>열 수 있음(도시를 불러와 본체가 붙음). false 면 허브 행이 '대기'.</summary>
    public static bool Ready => BaseballLeague.LeagueController.Instance != null;

    /// <summary>리그 창이 열려 있음.</summary>
    public static bool IsOpen => Ready && BaseballLeague.LeagueController.Instance.Window.Visible;

    /// <summary>열기 · 닫기.</summary>
    public static void Toggle() => BaseballLeague.LeagueController.Instance?.Window.Toggle();

    /// <summary>true 인 동안 허브 퀵 버튼 · 패널을 숨김(게임 UI 를 숨기는 전체 화면 도구). 선택(없으면 false).</summary>
    public static bool HideHub => false;

    /// <summary>선택: 모드 설정의 '모드 허브 연결' 스위치 값. false 면 허브 목록에서 빠지고 자기 퀵 버튼이 돌아옴.</summary>
    public static bool HubLinked => BaseballLeague.BaseballSetting.Current.HubLinked;

    /// <summary>허브가 씀: true 면 이 모드의 자기 퀵 버튼을 그리지도 막지도 않음. 허브가 꺼지면 false 로 되돌림.</summary>
    /// <remarks>기본 false(허브가 없으면 자기 퀵 버튼). 허브가 묶을 때 true 로 씀 · 그래도 HubLinked 가 false 면 자기 퀵 버튼을 그림(LeagueWindow.OwnLauncher).</remarks>
    public static bool HubOwnsLauncher = false;
}
