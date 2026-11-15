using System.IO;
using System.Xml.Serialization;
using ColossalFramework.IO;

namespace BaseballLeague
{
    /// <summary>
    /// 게임 전체 설정(세이브와 따로, %LOCALAPPDATA%\Colossal Order\Cities_Skylines\BaseballLeague.xml): 모드 허브 연결 · 자기 퀵 버튼 자리 · 업데이트 내역 판.
    /// 리그 규칙 · 이름 방식 · 시즌은 도시마다 달라 세이브(BaseballSave)에.
    /// </summary>
    [XmlRoot("BaseballLeague")]
    public sealed class BaseballSetting : CSCommonSettingsXml
    {
        const string FileName = "BaseballLeague.xml";

        public static BaseballSetting Current { get; private set; } = new BaseballSetting();

        static string FilePath => Path.Combine(DataLocation.localApplicationData, FileName);

        /// <summary>모드 허브 목록에 넣고 퀵 버튼을 허브에 맡김.</summary>
        public bool HubLinked = true;

        /// <summary>자기 퀵 버튼 자리(논리 좌표, 음수면 기본 자리 — 옛 설정 파일도 기본). 허브가 없거나 연결을 껐을 때만 보임.</summary>
        public float LauncherX = -1f, LauncherY = -1f;

        public static void Load() => Current = Load<BaseballSetting>(FilePath);

        public static void Save() => Save(FilePath, Current);
    }
}
