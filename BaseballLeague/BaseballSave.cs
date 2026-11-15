using System;
using BaseballLeague.Core;
using BaseballLeague.Data;
using ICities;

namespace BaseballLeague
{
    /// <summary>
    /// 세이브 안 시즌(키 BaseballLeague): 형식은 Data.SaveFormat(판 4 · 옛 판 1 ~ 3 도 읽음).
    /// 없으면 시즌 없음 → 도시를 불러온 뒤 새 시즌. 못 읽으면(깨짐 · 모르는 판) 원본을 BaseballLeague.bak 에 한 번 남기고,
    /// 사용자가 설정 '새 시즌 시작'을 확인하기 전까지 임시 시즌으로 진행하며 저장 때 원본을 그대로 다시 씀(SaveFormat.Unread).
    /// </summary>
    public sealed class BaseballSave : SerializableDataExtensionBase
    {
        const string Key = "BaseballLeague", BackupKey = "BaseballLeague.bak";

        /// <summary>화면 · 로그 안내(못 읽은 원본을 들고 있는 동안).</summary>
        public const string UnreadText = "세이브를 읽지 못해 임시 시즌으로 진행 중";

        public override void OnLoadData()
        {
            League.Reset();
            Stadium.Home = 0;
            SaveFormat.Discard();
            byte[] data;
            try
            {
                data = serializableDataManager.LoadData(Key);
            }
            catch (Exception e)
            {
                Log.Warn($"시즌 데이터를 꺼내지 못해 새 시즌으로: {e.Message}");
                return;
            }
            var s = SaveFormat.Load(data, out ushort stadium, out string error);
            if (s != null)
            {
                League.Current = s;
                Stadium.Home = stadium;
                Log.Info($"시즌 불러옴 · {s.Rule} · 경기 {s.Played} / {s.Games.Count} · {data.Length / 1024}KB");
                return;
            }
            if (error == null) return; // 세이브에 시즌 없음
            Log.Warn($"{UnreadText}: {error} · 원본 {data.Length}바이트는 저장 때 그대로 다시 쓰고 {BackupKey} 에도 남겨요. 설정 '새 시즌 시작'을 확인하면 새 시즌으로 저장해요.");
            try
            {
                serializableDataManager.SaveData(BackupKey, data);
            }
            catch (Exception e)
            {
                Log.Error($"못 읽은 시즌 데이터 백업 실패: {e}");
            }
        }

        public override void OnSaveData()
        {
            try
            {
                var data = SaveFormat.Payload(League.Current, Stadium.Home); // 시즌은 한 번만 잡음(본 스레드가 새 시즌으로 갈아 끼워도 이 시즌을 끝까지 씀)
                if (data != null) serializableDataManager.SaveData(Key, data);
            }
            catch (Exception e)
            {
                Log.Error($"시즌 저장 실패: {e}");
            }
        }
    }
}
