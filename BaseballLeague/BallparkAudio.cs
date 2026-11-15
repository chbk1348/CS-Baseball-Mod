using System;
using BaseballLeague.Core;
using BaseballLeague.Data;
using HarmonyLib;
using UnityEngine;

namespace BaseballLeague
{
    /// <summary>
    /// 야구장 소리: 홈구장에서 리그 홈 경기로 잡힌 경기 이벤트가 열린 동안(EventData.Flags.Active) 게임 축구 소리(SportMatchAI.GetAudioState)를 끄고,
    /// 코드로 만든 야구장 소리 한 줄(관중 웅성거림 · 타격음 뒤 함성 · 박수 · 응원 북)을 게임 건물 소리 묶음(BuildingManager.m_audioGroup)에 올림.
    /// 3D 위치 · 거리 감쇠 · 멀면 끔 · 효과음 · 전체 음량 · 소리 끄기는 게임 묶음이 맡음. 일시정지면 올리지 않아 게임이 줄여 끔. 본 스레드(AudioManager.LateUpdate)에서만.
    /// </summary>
    internal static class BallparkAudio
    {
        /// <summary>들리는 거리(m, 게임 SportMatchAI 200 보다 조금 넓게 · 건물 가운데 한 곳에서 나므로). 카메라가 높으면 게임처럼 최대 1.5배.</summary>
        const float MaxDistance = 250f;

        /// <summary>소리 크기(게임 AudioInfo.m_volume) · 켜지고 꺼질 때 바뀌는 시간(초, AudioInfo.m_fadeLength).</summary>
        const float Volume = 0.8f, FadeSeconds = 1f;

        static AudioInfo sound;
        static bool failed;
        static int checkedFrame = -1;
        static bool hosting;

        /// <summary>building 이 지금 리그 홈 경기를 열고 있나(설정 켬 · 홈구장 · 경기 이벤트 진행 중 · 올림픽 휴식 · 올림픽 경기 아님 · 맞출 홈 경기 있음). 프레임마다 한 번만 셈.</summary>
        internal static bool Hosting(ushort building)
        {
            if (building == 0 || building != Stadium.Home || !BaseballSetting.Current.BallparkSound) return false;
            if (Time.frameCount == checkedFrame) return hosting;
            checkedFrame = Time.frameCount;
            hosting = false;
            try
            {
                var s = League.Current;
                if (s == null) return false;
                ushort ev = BuildingManager.instance.m_buildings.m_buffer[building].m_eventIndex;
                if (ev == 0) return false;
                ref var e = ref EventManager.instance.m_events.m_buffer[ev];
                if ((e.m_flags & EventData.Flags.Active) == 0 || !(e.Info?.m_eventAI is SportMatchAI)) return false;
                if (League.OnBreak(SimulationManager.instance.m_currentGameTime.Date) || Olympic.IsAttached(ev)) return false;
                hosting = League.HomeTarget(s, e.StartTime) >= 0;
            }
            catch (Exception e)
            {
                Safe.Report("야구장 소리 확인", e);
            }
            return hosting;
        }

        /// <summary>건물 소리 묶음이 갱신되기 전(BuildingManager.PlayAudioImpl 앞): 홈구장이 리그 경기 중이고 카메라가 가까우면 야구장 소리를 올림.</summary>
        internal static void Play(AudioManager.ListenerInfo listener)
        {
            try
            {
                ushort id = Stadium.Home;
                if (failed || !Hosting(id) || SimulationManager.instance.SimulationPaused || LoadingManager.instance.m_currentlyLoading) return;
                var bm = BuildingManager.instance;
                ref var b = ref bm.m_buildings.m_buffer[id];
                var info = b.Info;
                if (info == null) return;
                // 게임 BuildingAI.PlayAudio 와 같은 자리 · 높이 보정
                var pos = Building.CalculateMeshPosition(info, b.m_position, b.m_angle, b.Length);
                pos.y += info.m_size.y;
                var d = listener.m_position - pos;
                float max = MaxDistance * (1f + Mathf.Clamp01(d.y / MaxDistance * 0.5f));
                if (d.sqrMagnitude >= max * max) return;
                if (sound == null) sound = Make();
                bm.m_audioGroup.AddPlayer(listener, id, sound, pos, Vector3.zero, max, 1f, 1f);
            }
            catch (Exception e)
            {
                failed = true;
                Safe.Report("야구장 소리 내기", e);
            }
        }

        /// <summary>도시를 내릴 때: 다음 도시에서 다시 셈(소리 한 줄은 만들어 둔 것을 다시 씀).</summary>
        internal static void Reset()
        {
            checkedFrame = -1;
            hosting = false;
            failed = false;
        }

        static AudioInfo Make()
        {
            var clip = BallparkSynth.Make();
            clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
            var info = ScriptableObject.CreateInstance<AudioInfo>();
            info.name = clip.name;
            info.hideFlags = HideFlags.DontUnloadUnusedAsset;
            info.m_clip = clip;
            info.m_volume = Volume;
            info.m_fadeLength = FadeSeconds;
            info.m_loop = true;
            info.m_is3D = true;
            return info;
        }
    }

    /// <summary>
    /// 야구장 소리 한 줄 만들기(AudioClip.Create · 외부 음원 없음): 32초 되풀이, 120 BPM 4박 16마디.
    /// 관중 웅성거림(저역 잡음 · 느린 출렁임) 계속, 10 · 23초에 타격음(딱) 뒤 함성(밝은 잡음) 올라갔다 가라앉음,
    /// 응원 마디(0 ~ 4 · 8 ~ 11 · 14 ~ 16마디)에 박수 · 응원 북 리듬(짝짝 짝짝짝 / 짝 짝 짝 짝 번갈아 · 곡 없음). 끝과 처음이 이어지게 거름 · 소리 조각을 둥글게 씀.
    /// </summary>
    internal static class BallparkSynth
    {
        const int Rate = 22050, Seed = 1982;
        const float Bpm = 120f;
        const int BeatsPerBar = 4, EighthsPerBar = 8, Bars = 16;
        const float BarSeconds = BeatsPerBar * 60f / Bpm, EighthSeconds = BarSeconds / EighthsPerBar, LoopSeconds = Bars * BarSeconds;

        /// <summary>응원 마디(시작 마디 · 끝 마디 바로 뒤).</summary>
        static readonly int[] CheerBars = { 0, 4, 8, 11, 14, 16 };

        /// <summary>마디 안 8분음표 자리: 짝짝 짝짝짝 · 짝 짝 짝 짝.</summary>
        static readonly int[] PatternA = { 0, 2, 4, 5, 6 }, PatternB = { 0, 2, 4, 6 };

        /// <summary>타격 시각(초).</summary>
        static readonly float[] Hits = { 10f, 23f };

        // 관중 웅성거림: 저역 거름(Hz) · 크기(RMS) · 느린 출렁임(되풀이 한 번에 3 · 7번) 깊이
        const float MurmurHz = 380f, MurmurLevel = 0.12f, Sway3 = 0.18f, Sway7 = 0.08f;
        // 함성: 띠 거름(Hz) · 평소 · 응원 마디 · 타격 뒤 크기, 타격 뒤 웅성거림도 키움
        const float CheerLowHz = 600f, CheerHighHz = 2600f, CheerLevel = 0.1f, CheerBed = 0.2f, CheerChant = 0.35f, CheerPeak = 2.4f, MurmurSwell = 0.8f;
        // 함성 모양(초): 타격 뒤 늦게 · 올라감 · 머묾 · 가라앉음
        const float SwellDelay = 0.25f, SwellRise = 0.8f, SwellHold = 1.5f, SwellDecay = 1.6f;
        // 응원 북: 처음 · 끝 높이(Hz) · 높이 내려가는 시간 · 울림 · 길이(초) · 크기 · 북 가죽 잡음
        const float DrumHighHz = 120f, DrumLowHz = 58f, DrumSweep = 0.04f, DrumDecay = 0.11f, DrumLength = 0.35f, DrumLevel = 0.45f, DrumSkin = 0.25f, DrumSkinDecay = 0.005f;
        // 박수: 사람 수 · 어긋남(초) · 울림 · 길이(초) · 크기 · 높은 거름(Hz)
        const int ClapVoices = 10;
        const float ClapJitter = 0.025f, ClapDecay = 0.011f, ClapLength = 0.06f, ClapLevel = 0.28f, ClapHz = 900f;
        // 타격음: 딱 소리(잡음) · 나무 울림 세 높이(Hz) · 울림(초) · 길이 · 크기
        const float CrackClickDecay = 0.0015f, CrackLength = 0.12f, CrackLevel = 0.8f;
        static readonly float[] CrackHz = { 1300f, 2300f, 3700f }, CrackDecay = { 0.03f, 0.018f, 0.01f }, CrackGain = { 0.5f, 0.35f, 0.2f };
        /// <summary>마지막 크기 맞춤(가장 큰 값).</summary>
        const float Peak = 0.9f;

        internal static AudioClip Make()
        {
            var x = Samples();
            var clip = AudioClip.Create("BaseballLeague.Ballpark", x.Length, 1, Rate, false);
            clip.SetData(x, 0);
            return clip;
        }

        /// <summary>되풀이 한 번의 소리(모노 · Rate Hz · 가장 큰 값 Peak).</summary>
        internal static float[] Samples()
        {
            int n = (int)(LoopSeconds * Rate);
            var r = new System.Random(Seed);
            var murmur = new float[n];
            var cheer = new float[n];
            for (int i = 0; i < n; i++)
            {
                murmur[i] = Noise(r);
                cheer[i] = Noise(r);
            }
            Lowpass(murmur, MurmurHz);
            Lowpass(murmur, MurmurHz);
            Scale(murmur, MurmurLevel);
            Lowpass(cheer, CheerHighHz);
            Highpass(cheer, CheerLowHz);
            Scale(cheer, CheerLevel);

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate, phase = 2f * Mathf.PI * t / LoopSeconds;
                float swell = 0f;
                foreach (float h in Hits) swell = Mathf.Max(swell, Swell(Wrap(t - h - SwellDelay)));
                float sway = 1f + Sway3 * Mathf.Sin(3f * phase) + Sway7 * Mathf.Sin(7f * phase + 1f);
                float crowd = (Cheering((int)(t / BarSeconds)) ? CheerChant : CheerBed) + CheerPeak * swell;
                murmur[i] = murmur[i] * sway * (1f + MurmurSwell * swell) + cheer[i] * crowd;
            }

            for (int bar = 0; bar < Bars; bar++)
            {
                if (!Cheering(bar)) continue;
                foreach (int e in bar % 2 == 0 ? PatternA : PatternB)
                {
                    float t = bar * BarSeconds + e * EighthSeconds;
                    Drum(murmur, t, r);
                    for (int v = 0; v < ClapVoices; v++) Clap(murmur, t + (float)r.NextDouble() * ClapJitter, r);
                }
            }
            foreach (float h in Hits) Crack(murmur, h, r);

            float peak = 0f;
            for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(murmur[i]));
            if (peak > 0f)
                for (int i = 0; i < n; i++) murmur[i] *= Peak / peak;
            return murmur;
        }

        static bool Cheering(int bar)
        {
            for (int i = 0; i < CheerBars.Length; i += 2)
                if (bar >= CheerBars[i] && bar < CheerBars[i + 1]) return true;
            return false;
        }

        static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

        static float Wrap(float t) => (t % LoopSeconds + LoopSeconds) % LoopSeconds;

        /// <summary>함성 모양(u = 시작 뒤 초): 올라감 · 머묾 · 가라앉음.</summary>
        static float Swell(float u)
        {
            if (u < SwellRise) return u / SwellRise;
            if (u < SwellRise + SwellHold) return 1f;
            return Mathf.Exp(-(u - SwellRise - SwellHold) / SwellDecay);
        }

        static float Alpha(float hz) => 1f - Mathf.Exp(-2f * Mathf.PI * hz / Rate);

        /// <summary>한 극 저역 거름. 두 번 돌려 끝 상태로 처음을 거름(되풀이 이음매 없음), 두 번째만 씀.</summary>
        static void Lowpass(float[] x, float hz)
        {
            float a = Alpha(hz), y = 0f;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < x.Length; i++)
                {
                    y += a * (x[i] - y);
                    if (pass == 1) x[i] = y;
                }
        }

        /// <summary>한 극 고역 거름(원래 - 저역), 되풀이 이음매 없이.</summary>
        static void Highpass(float[] x, float hz)
        {
            float a = Alpha(hz), y = 0f;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < x.Length; i++)
                {
                    y += a * (x[i] - y);
                    if (pass == 1) x[i] -= y;
                }
        }

        /// <summary>RMS 를 level 로.</summary>
        static void Scale(float[] x, float level)
        {
            double sum = 0;
            foreach (float v in x) sum += v * v;
            float rms = (float)Math.Sqrt(sum / x.Length);
            if (rms <= 0f) return;
            for (int i = 0; i < x.Length; i++) x[i] *= level / rms;
        }

        /// <summary>at 초부터 소리 조각을 더함(되풀이 끝을 넘으면 처음으로).</summary>
        static int Start(float[] x, float at) => (int)(Wrap(at) * Rate) % x.Length;

        static void Drum(float[] x, float at, System.Random r)
        {
            int s = Start(x, at), len = (int)(DrumLength * Rate);
            float phase = 0f;
            for (int k = 0; k < len; k++)
            {
                float t = (float)k / Rate;
                phase += 2f * Mathf.PI * (DrumLowHz + (DrumHighHz - DrumLowHz) * Mathf.Exp(-t / DrumSweep)) / Rate;
                x[(s + k) % x.Length] += DrumLevel * (Mathf.Sin(phase) * Mathf.Exp(-t / DrumDecay) + DrumSkin * Noise(r) * Mathf.Exp(-t / DrumSkinDecay));
            }
        }

        static void Clap(float[] x, float at, System.Random r)
        {
            int s = Start(x, at), len = (int)(ClapLength * Rate);
            float a = Alpha(ClapHz), low = 0f, gain = ClapLevel / Mathf.Sqrt(ClapVoices);
            for (int k = 0; k < len; k++)
            {
                float w = Noise(r) * Mathf.Exp(-(float)k / Rate / ClapDecay);
                low += a * (w - low);
                x[(s + k) % x.Length] += gain * (w - low);
            }
        }

        static void Crack(float[] x, float at, System.Random r)
        {
            int s = Start(x, at), len = (int)(CrackLength * Rate);
            for (int k = 0; k < len; k++)
            {
                float t = (float)k / Rate, v = Noise(r) * Mathf.Exp(-t / CrackClickDecay);
                for (int j = 0; j < CrackHz.Length; j++) v += CrackGain[j] * Mathf.Sin(2f * Mathf.PI * CrackHz[j] * t) * Mathf.Exp(-t / CrackDecay[j]);
                x[(s + k) % x.Length] += CrackLevel * v;
            }
        }
    }

    /// <summary>리그 홈 경기 중인 홈구장의 축구 소리 끄기(게임 DummyBuildingAI.PlayAudio 가 이벤트 GetAudioState 로 경기장 소리를 냄).</summary>
    [HarmonyPatch(typeof(SportMatchAI), "GetAudioState")]
    [CSCommonOptionalPatch]
    internal static class MatchAudioPatch
    {
        internal static bool Prefix(ref EventData data, ref bool __result)
        {
            if (!BallparkAudio.Hosting(data.m_building)) return true;
            __result = false;
            return false;
        }
    }

    /// <summary>대학 경기장(Campus) 경기는 base 를 부르지 않는 따로 된 GetAudioState(치어리더 소리 포함).</summary>
    [HarmonyPatch(typeof(VarsitySportsMatchAI), "GetAudioState")]
    [CSCommonOptionalPatch]
    internal static class VarsityAudioPatch
    {
        static bool Prefix(ref EventData data, ref bool __result) => MatchAudioPatch.Prefix(ref data, ref __result);
    }

    /// <summary>건물 소리 묶음 갱신(UpdatePlayers) 전에 야구장 소리를 올림.</summary>
    [HarmonyPatch(typeof(BuildingManager), "PlayAudioImpl")]
    [CSCommonOptionalPatch]
    internal static class BallparkPlayPatch
    {
        static void Prefix(AudioManager.ListenerInfo listenerInfo) => BallparkAudio.Play(listenerInfo);
    }
}
