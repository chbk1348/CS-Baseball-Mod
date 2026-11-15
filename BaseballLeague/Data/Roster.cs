using System;
using System.Collections.Generic;

namespace BaseballLeague.Data
{
    /// <summary>선수 한 명: 이름 · 능력 셋(0 ~ 255 → 0 ~ 1). 타자 = 정확 · 힘 · 선구, 투수 = 구위 · 제구 · 체력.</summary>
    public sealed class Player
    {
        public string Name;
        public byte A, B, C;

        public float Af => A / 255f;
        public float Bf => B / 255f;
        public float Cf => C / 255f;
    }

    /// <summary>
    /// 팀 명단: 타자 13(0 ~ 8 선발 타순 · 9 ~ 12 벤치) · 투수 12(13 ~ 17 선발 5 · 18 ~ 23 불펜 · 24 마무리) · 감독 1.
    /// 이름은 Names(시즌 안 중복 없음), 능력은 팀 전력 주변 작은 분산(시드 결정적).
    /// </summary>
    public sealed class Roster
    {
        public const int Batters = 13, Lineup = 9, Pitchers = 12, Starters = 5, Size = Batters + Pitchers;
        public const int FirstPitcher = Batters, FirstReliever = Batters + Starters, Closer = Size - 1;

        /// <summary>선발 타순 · 벤치 수비 자리(시안 타순 표).</summary>
        static readonly string[] Pos = { "CF", "SS", "RF", "1B", "3B", "LF", "C", "2B", "DH", "C", "IF", "IF", "OF" };

        public string Manager = "";
        public Player[] P = new Player[Size];

        public static string Position(int slot) => slot < Batters ? Pos[slot] : slot < FirstReliever ? "선발" : slot == Closer ? "마무리" : "불펜";

        /// <summary>능력 분산(팀 전력에서 ±).</summary>
        const double Spread = 0.07, PowerScale = 1.5;

        /// <summary>
        /// 리그 명단 전부(팀 n). 이름은 n × 26 개를 한 번에 뽑아 시즌 안 중복 없음. names 를 주면 그 이름을 그대로 씀(새 시즌에 명단 유지).
        /// </summary>
        public static Roster[] Make(Naming naming, int seed, int teams, Func<int, float> power, Roster[] names = null)
        {
            var list = names == null ? Names.Make(naming, seed, teams * (Size + 1)) : null;
            var rng = new Random(unchecked(seed * 131 + 17));
            var all = new Roster[teams];
            for (int t = 0; t < teams; t++)
            {
                var r = all[t] = new Roster { Manager = names?[t].Manager ?? list[t * (Size + 1) + Size] };
                float p = power(t);
                for (int i = 0; i < Size; i++)
                    r.P[i] = new Player { Name = names?[t].P[i].Name ?? list[t * (Size + 1) + i], A = Skill(rng, p), B = Skill(rng, p), C = Skill(rng, p) };
            }
            return all;
        }

        /// <summary>이름만 새 방식으로(능력 · 기록 그대로).</summary>
        public static Roster[] Rename(Roster[] old, Naming naming, int seed)
        {
            var list = Names.Make(naming, seed, old.Length * (Size + 1));
            var all = new Roster[old.Length];
            for (int t = 0; t < old.Length; t++)
            {
                var r = all[t] = new Roster { Manager = list[t * (Size + 1) + Size] };
                for (int i = 0; i < Size; i++)
                {
                    var o = old[t].P[i];
                    r.P[i] = new Player { Name = list[t * (Size + 1) + i], A = o.A, B = o.B, C = o.C };
                }
            }
            return all;
        }

        static byte Skill(Random rng, float power)
        {
            // 두 균등 난수 합 ≈ 종 모양
            double v = 0.5 + (power - 0.5) * PowerScale + (rng.NextDouble() + rng.NextDouble() - 1.0) * Spread * 2.0;
            return (byte)Math.Round(Math.Max(0.05, Math.Min(0.95, v)) * 255.0);
        }

        /// <summary>능력 평균(slot 범위): 팀 전력 4칸(타격 · 투수 · 수비)에 씀.</summary>
        public float Mean(int from, int to, Func<Player, float> pick)
        {
            float sum = 0f;
            for (int i = from; i < to; i++) sum += pick(P[i]);
            return sum / (to - from);
        }
    }

    /// <summary>선수 기록 한 줄(한 경기 또는 시즌 합): 칸은 번호로(저장 · 더하기를 한 번에).</summary>
    public sealed class Line
    {
        // 타자
        public const int G = 0, PA = 1, AB = 2, H = 3, D = 4, T = 5, HR = 6, RBI = 7, R = 8, BB = 9, SO = 10;
        // 투수(RA 는 자책점 대신 실점 — shortcut: 실책 · 승계 주자를 따로 셈하지 않음, 자책점이 필요해지면 Sim 에서 실책 주자를 표시)
        public const int PG = 11, GS = 12, Outs = 13, HA = 14, RA = 15, BBA = 16, K = 17, HRA = 18, W = 19, L = 20, SV = 21;
        public const int Count = 22;

        public readonly int[] V = new int[Count];

        public int this[int i] => V[i];

        public void Add(Line o)
        {
            for (int i = 0; i < Count; i++) V[i] += o.V[i];
        }

        public float Avg => V[AB] == 0 ? 0f : (float)V[H] / V[AB];
        public float Era => V[Outs] == 0 ? 0f : V[RA] * 27f / V[Outs];

        /// <summary>이닝 글('6.1' = 6이닝 1아웃).</summary>
        public string Innings => V[Outs] / 3 + "." + V[Outs] % 3;
    }

    /// <summary>
    /// 홈팀 편집값 · 경기 · 알림 스위치(도시마다 · 세이브). 화면이 고칠 땐 Clone 을 고쳐 통째로 갈아 끼움(저장 스레드가 반쯤 고친 값을 읽지 않게).
    /// Abbr = 팀마다 줄임 이름(빈 글 = Rules.Abbr), Color = 내 팀 색(Pal.BallTeams 번호 · -1 = 기본), Power = 시작 전력(0 약함 · 1 보통 · 2 강함, 새 시즌부터).
    /// </summary>
    public sealed class Club
    {
        public string Name = "", Manager = "";
        public string[] Abbr = new string[12];
        public sbyte Color = -1;
        public byte Power = 1;
        public bool AdjustWin = true, NotifyResult = true, NotifyStart = true, NotifyBig = true, LiveBoard;

        public Club Clone()
        {
            var c = (Club)MemberwiseClone();
            c.Abbr = (string[])Abbr.Clone();
            return c;
        }
    }
}
