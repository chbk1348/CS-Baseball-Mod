using System;
using System.Collections.Generic;

namespace BaseballLeague.Data
{
    /// <summary>타석 결과(교체 = 투수 교체 줄 · 타자 없음).</summary>
    public enum PlayKind : byte
    {
        Single,
        Double,
        Triple,
        HomeRun,
        Walk,
        Strikeout,
        Ground,
        Fly,
        LineOut,
        DoublePlay,
        SacFly,
        Error,
        Change,
    }

    /// <summary>타석 하나(문자 중계): 이닝 · 말 · 타자 · 투수(명단 자리) · 결과 · 끝난 뒤 아웃 · 주자(1 · 2 · 4 = 1 · 2 · 3루) · 이 타석 득점 · 점수 · 투수 투구 수 · 마지막 볼카운트.</summary>
    public struct Play
    {
        public byte Inning, Batter, Pitcher, Outs, Bases, Runs, Away, Home, Balls, Strikes;
        public bool Bottom;
        public PlayKind Kind;
        public short Pitches;
    }

    /// <summary>공 하나(진행 표시 단위): 어느 타석 · 던진 뒤 볼카운트 · 타석이 끝났는지.</summary>
    public struct Pitch
    {
        public short Play;
        public byte Balls, Strikes;
        public bool Done;
    }

    /// <summary>
    /// 박스 스코어 요약(세이브에 남는 것): 다시 돌릴 시드 차례 · 표시(점수 맞춤 · 만원 · 게임 관중) · 두 팀(0 원정 · 1 홈) 선발 · 이닝 점수 · 안타 · 실책 · 볼넷 · 승 · 패 · 세이브 투수(명단 자리 · -1 없음) · 관중.
    /// 문자 중계는 저장하지 않고 같은 시드로 다시 돌려 만듦(Sim.Replay).
    /// </summary>
    public sealed class Box
    {
        /// <summary>점수 맞춤 · 만원 · 게임 관중(실측) · 가상 구장 자리(League.ParkSeats) 기준 추정(판 4 의 이 표시 전 세이브는 없음 → 경기장 단위로 봄).</summary>
        public const byte FlagFixed = 1, FlagFull = 2, FlagReal = 4, FlagPark = 8;
        public byte Attempt, Flags;
        public readonly byte[] SP = new byte[2], H = new byte[2], E = new byte[2], B = new byte[2];
        public readonly byte[][] Inn = new byte[2][];
        public sbyte Win = -1, Lose = -1, Save = -1;
        public ushort Crowd;

        public int Runs(int side)
        {
            int n = 0;
            foreach (byte r in Inn[side]) n += r;
            return n;
        }

        public bool Fixed => (Flags & FlagFixed) != 0;
    }

    /// <summary>한 경기 시뮬레이션 결과: 박스 · 선수별 경기 기록(두 팀 · 명단 자리) · 잔루 · 결승 장면 · (원하면) 타석 · 공 목록.</summary>
    public sealed class Result
    {
        public Box Box;
        public readonly Line[][] Lines = new Line[2][];
        public readonly int[] Lob = new int[2];
        /// <summary>결승 득점이 난 타석: 타자 자리 · 이닝 · 결과(없으면 Batter -1).</summary>
        public int DeciderBatter = -1, DeciderInning;
        public PlayKind DeciderKind;
        public List<Play> Plays;
        public List<Pitch> Pitches;

        public int Away => Box.Runs(0);
        public int Home => Box.Runs(1);
    }

    /// <summary>
    /// 경기 진행(공 하나 · 타석 단위, 결정적: 같은 시즌 시드 · 경기 번호 · 날짜 · 시드 차례 · 선발이면 늘 같은 결과).
    /// 9이닝 · 정규 시즌 연장 상한(Rules.MaxInnings)까지 동점이면 무승부, 포스트시즌은 승부가 날 때까지. 9회말 홈 앞서면 말 없음 · 끝내기면 바로 끝.
    /// 투수: 선발은 팀 경기 차례로 5인 순환, 투구 수 · 실점이 많으면 불펜 차례로, 9회 이후 1 ~ 3점 앞서면 마무리.
    /// shortcut: 도루 · 몸에 맞는 공 · 대타 · 수비 교체 · 연투 피로 없음, 경기 내용을 더 세게 만들게 되면 Engine 안 타석 · 교체 규칙에.
    /// </summary>
    public static class Sim
    {
        public const int Innings = 9;

        /// <summary>홈 경기 이벤트 승패에 맞을 때까지 시드를 바꿔 다시 돌리는 상한.</summary>
        public const int MaxAttempts = 30;

        /// <summary>포스트시즌 연장 안전 상한(넘으면 홈 1점 · 점수 맞춤 표시). shortcut: 사실상 닿지 않음, 공식 연장 규정을 넣게 되면 여기서.</summary>
        const int PostGuard = 30;

        /// <summary>선발: 그 팀의 앞선 경기 수(날짜 · 시각 차례) % 5.</summary>
        public static int StarterOf(Season s, Game g, int team)
        {
            int n = 0;
            foreach (var o in s.Games)
                if (o != g && (o.Home == team || o.Away == team) && (o.Day < g.Day || o.Day == g.Day && o.Minute < g.Minute)) n++;
            return Roster.FirstPitcher + n % Roster.Starters;
        }

        public static Result Run(Season s, Game g, int index, int attempt, bool plays = false) =>
            new Engine(s, g, index, attempt, StarterOf(s, g, g.Away), StarterOf(s, g, g.Home), plays).Go();

        /// <summary>끝난 경기는 저장한 시드 차례 · 선발로, 안 한 경기는 첫 차례로 다시 돌림(문자 중계 · 진행 표시용). 점수를 맞춘 경기는 박스 점수와 다를 수 있음.</summary>
        public static Result Replay(Season s, Game g, int index)
        {
            var b = g.Box;
            if (b == null) return Run(s, g, index, 0, true);
            return new Engine(s, g, index, b.Attempt, b.SP[0], b.SP[1], true).Go();
        }

        static int Seed(Season s, Game g, int index, int attempt) => unchecked(s.Seed * 7349 + index * 104729 + (int)(g.Day.Ticks / TimeSpan.TicksPerDay) * 31 + attempt * 15485863);

        sealed class Engine
        {
            // 타석 확률(능력 0 ~ 1, 차이 1 당 기울기)
            const double Ball = 0.38, BallSlope = 0.12, Strike = 0.38, StrikeSlope = 0.12, Foul = 0.25;
            const double Homer = 0.032, HomerPower = 0.05, HomerStuff = 0.02, Hit = 0.31, HitSlope = 0.10, Two = 0.20, Three = 0.025, Err = 0.015;
            const double GroundShare = 0.45, FlyShare = 0.35, DoubleChance = 0.30, SacChance = 0.60, ScoreFromSecond = 0.60, ScoreFromFirst = 0.40, FirstToThird = 0.25, GroundAdvance = 0.50;
            /// <summary>홈 타자 정확 보정 · 선발 투구 수 한도(체력 0 → 85, 1 → 115) · 피로 기울기 · 불펜 한 번 투구 · 실점 한도.</summary>
            const double HomeEdge = 0.02, Fatigue = 0.004;
            const int StarterPitches = 85, StarterExtra = 30, StarterRuns = 6, ReliefPitches = 30, ReliefRuns = 3, WinOuts = 15, SaveLead = 3;

            readonly Random rng;
            readonly Roster[] team = new Roster[2];
            readonly Result res = new();
            readonly bool post, keep;
            readonly int maxRegular;
            readonly int[] score = new int[2], pitcher = new int[2], pitches = new int[2], stintOuts = new int[2], stintRuns = new int[2], next = new int[2], entryLead = new int[2], relief = { Roster.FirstReliever, Roster.FirstReliever };
            readonly bool[] closerUsed = new bool[2];
            readonly List<byte>[] inn = { new(), new() };
            readonly List<int>[] used = { new(), new() };
            readonly int[] bases = { -1, -1, -1 };
            int inning, bat, def, outs, runsHalf, leader = -1, winP = -1, loseP = -1;
            bool walkoff;

            public Engine(Season s, Game g, int index, int attempt, int awaySP, int homeSP, bool plays)
            {
                rng = new Random(Seed(s, g, index, attempt));
                team[0] = s.RosterOf(g.Away);
                team[1] = s.RosterOf(g.Home);
                post = g.Series != 0;
                maxRegular = Rules.MaxInnings(s.Rule);
                keep = plays;
                if (plays)
                {
                    res.Plays = new List<Play>();
                    res.Pitches = new List<Pitch>();
                }
                res.Box = new Box { Attempt = (byte)attempt };
                res.Box.SP[0] = (byte)awaySP;
                res.Box.SP[1] = (byte)homeSP;
                for (int side = 0; side < 2; side++)
                {
                    res.Lines[side] = new Line[Roster.Size];
                    for (int i = 0; i < Roster.Size; i++) res.Lines[side][i] = new Line();
                    for (int i = 0; i < Roster.Lineup; i++) res.Lines[side][i].V[Line.G] = 1;
                }
                Enter(0, awaySP);
                Enter(1, homeSP);
                res.Lines[0][awaySP].V[Line.GS] = res.Lines[1][homeSP].V[Line.GS] = 1;
            }

            public Result Go()
            {
                for (inning = 1; ; inning++)
                {
                    for (bat = 0; bat < 2; bat++)
                    {
                        def = 1 - bat;
                        if (bat == 1 && inning >= Innings && score[1] > score[0]) break; // 9회말 없음
                        Half();
                        if (walkoff) break;
                    }
                    if (walkoff || inning >= Innings && score[0] != score[1]) break;
                    if (!post && inning >= maxRegular) break; // 무승부
                    if (post && inning >= PostGuard)
                    {
                        score[1]++;
                        inn[1][inn[1].Count - 1]++;
                        res.Box.Flags |= Box.FlagFixed;
                        leader = 1;
                        break;
                    }
                }
                Finish();
                return res;
            }

            void Enter(int side, int slot)
            {
                pitcher[side] = slot;
                pitches[side] = stintOuts[side] = stintRuns[side] = 0;
                entryLead[side] = score[side] - score[1 - side];
                used[side].Add(slot);
                res.Lines[side][slot].V[Line.PG] = 1;
                if (slot == Roster.Closer) closerUsed[side] = true;
            }

            void Half()
            {
                outs = runsHalf = 0;
                bases[0] = bases[1] = bases[2] = -1;
                bool first = true;
                while (outs < 3 && !walkoff)
                {
                    Change(first);
                    first = false;
                    Appearance();
                }
                if (outs >= 3)
                    for (int i = 0; i < 3; i++)
                        if (bases[i] >= 0) res.Lob[bat]++;
                inn[bat].Add((byte)Math.Min(byte.MaxValue, runsHalf));
            }

            /// <summary>투수 교체(타석 전): 9회 이후 1 ~ 3점 앞선 이닝 시작 = 마무리, 지친 선발 · 한 이닝 던진 불펜 = 다음 불펜(다 썼으면 그대로).</summary>
            void Change(bool newInning)
            {
                int cur = pitcher[def], lead = score[def] - score[bat];
                var p = team[def].P[cur];
                int to = -1;
                if (newInning && inning >= Innings && lead >= 1 && lead <= SaveLead && !closerUsed[def]) to = Roster.Closer;
                else
                {
                    bool tired = cur < Roster.FirstReliever
                        ? pitches[def] >= StarterPitches + p.Cf * StarterExtra || stintRuns[def] >= StarterRuns
                        : newInning && stintOuts[def] >= 3 || pitches[def] >= ReliefPitches || stintRuns[def] >= ReliefRuns;
                    if (tired) to = relief[def] < Roster.Closer ? relief[def]++ : !closerUsed[def] ? Roster.Closer : -1;
                }
                if (to < 0 || to == cur) return;
                if (keep) res.Plays.Add(Snapshot(PlayKind.Change, cur, to, 0, 0, 0));
                Enter(def, to);
            }

            void Appearance()
            {
                int slot = next[bat];
                next[bat] = (slot + 1) % Roster.Lineup;
                var b = team[bat].P[slot];
                var p = team[def].P[pitcher[def]];
                double tire = Math.Max(0, pitches[def] - StarterPitches) * Fatigue;
                double con = b.Af + (bat == 1 ? HomeEdge : 0.0), pow = b.Bf, eye = b.Cf, stuff = p.Af - tire, ctrl = p.Bf - tire;
                double pBall = Ball + (eye - ctrl) * BallSlope, pStrike = Strike + (stuff - con) * StrikeSlope;
                int balls = 0, strikes = 0;
                PlayKind kind;
                while (true)
                {
                    pitches[def]++;
                    double r = rng.NextDouble();
                    if (r < pBall)
                    {
                        if (++balls == 4)
                        {
                            kind = PlayKind.Walk;
                            break;
                        }
                    }
                    else
                    {
                        r = rng.NextDouble();
                        if (r < pStrike)
                        {
                            if (++strikes == 3)
                            {
                                kind = PlayKind.Strikeout;
                                break;
                            }
                        }
                        else if (r < pStrike + Foul)
                        {
                            if (strikes < 2) strikes++;
                        }
                        else
                        {
                            kind = InPlay(con, pow, stuff);
                            break;
                        }
                    }
                    if (keep) res.Pitches.Add(new Pitch { Play = (short)res.Plays.Count, Balls = (byte)balls, Strikes = (byte)strikes });
                }
                if (keep) res.Pitches.Add(new Pitch { Play = (short)res.Plays.Count, Balls = (byte)balls, Strikes = (byte)strikes, Done = true });
                Resolve(slot, kind, balls, strikes);
            }

            PlayKind InPlay(double con, double pow, double stuff)
            {
                double r = rng.NextDouble();
                if (r < Homer + (pow - 0.5) * HomerPower - (stuff - 0.5) * HomerStuff) return PlayKind.HomeRun;
                if (rng.NextDouble() < Hit + (con - stuff) * HitSlope)
                {
                    r = rng.NextDouble();
                    return r < Three ? PlayKind.Triple : r < Three + Two ? PlayKind.Double : PlayKind.Single;
                }
                if (rng.NextDouble() < Err) return PlayKind.Error;
                r = rng.NextDouble();
                if (r < GroundShare) return bases[0] >= 0 && outs < 2 && rng.NextDouble() < DoubleChance ? PlayKind.DoublePlay : PlayKind.Ground;
                if (r < GroundShare + FlyShare) return bases[2] >= 0 && outs < 2 && rng.NextDouble() < SacChance ? PlayKind.SacFly : PlayKind.Fly;
                return PlayKind.LineOut;
            }

            /// <summary>타석 결과로 주자 · 아웃 · 점수 · 기록을 옮김.</summary>
            void Resolve(int slot, PlayKind kind, int balls, int strikes)
            {
                var bl = res.Lines[bat][slot].V;
                var pl = res.Lines[def][pitcher[def]].V;
                int before = score[bat];
                bl[Line.PA]++;
                switch (kind)
                {
                    case PlayKind.HomeRun:
                        for (int i = 2; i >= 0; i--) Home(i);
                        ScoreRunner(slot);
                        Hitting(bl, pl, Line.HR);
                        pl[Line.HRA]++;
                        break;
                    case PlayKind.Triple:
                        for (int i = 2; i >= 0; i--) Home(i);
                        bases[2] = slot;
                        Hitting(bl, pl, Line.T);
                        break;
                    case PlayKind.Double:
                        Home(2);
                        Home(1);
                        if (bases[0] >= 0)
                        {
                            if (rng.NextDouble() < ScoreFromFirst) Home(0);
                            else Move(0, 2);
                        }
                        bases[1] = slot;
                        Hitting(bl, pl, Line.D);
                        break;
                    case PlayKind.Single:
                        Single(slot);
                        Hitting(bl, pl, -1);
                        break;
                    case PlayKind.Error:
                        Home(2);
                        if (bases[1] >= 0) Move(1, 2);
                        if (bases[0] >= 0) Move(0, 1);
                        bases[0] = slot;
                        bl[Line.AB]++;
                        res.Box.E[def]++;
                        break;
                    case PlayKind.Walk:
                        if (bases[0] >= 0)
                        {
                            if (bases[1] >= 0)
                            {
                                if (bases[2] >= 0) Home(2);
                                Move(1, 2);
                            }
                            Move(0, 1);
                        }
                        bases[0] = slot;
                        bl[Line.BB]++;
                        pl[Line.BBA]++;
                        res.Box.B[bat]++;
                        break;
                    case PlayKind.Strikeout:
                        Out(bl, pl);
                        bl[Line.SO]++;
                        pl[Line.K]++;
                        break;
                    case PlayKind.DoublePlay:
                        bases[0] = -1;
                        Out(bl, pl);
                        Out(null, pl);
                        if (outs < 3) Advance(); // 2아웃 전까지 앞 주자 진루
                        break;
                    case PlayKind.Ground:
                        Out(bl, pl);
                        if (outs < 3 && rng.NextDouble() < GroundAdvance) Advance();
                        break;
                    case PlayKind.SacFly:
                        bl[Line.AB]--; // 희생 뜬공은 타수 아님(Out 이 더함)
                        Out(bl, pl);
                        if (outs < 3) Home(2);
                        break;
                    default: // 뜬공 · 직선타
                        Out(bl, pl);
                        break;
                }
                int runs = score[bat] - before;
                if (kind != PlayKind.DoublePlay && kind != PlayKind.Error) bl[Line.RBI] += runs;
                if (runs > 0) Lead(slot, kind);
                if (keep) res.Plays.Add(Snapshot(kind, slot, pitcher[def], runs, balls, strikes));
                if (bat == 1 && inning >= Innings && score[1] > score[0]) walkoff = true;
            }

            void Hitting(int[] bl, int[] pl, int extra)
            {
                bl[Line.AB]++;
                bl[Line.H]++;
                if (extra >= 0) bl[extra]++;
                pl[Line.HA]++;
                res.Box.H[bat]++;
            }

            void Single(int slot)
            {
                Home(2);
                if (bases[1] >= 0)
                {
                    if (rng.NextDouble() < ScoreFromSecond) Home(1);
                    else Move(1, 2);
                }
                if (bases[0] >= 0)
                {
                    if (bases[2] < 0 && rng.NextDouble() < FirstToThird) Move(0, 2);
                    else Move(0, 1);
                }
                bases[0] = slot;
            }

            /// <summary>땅볼 · 병살 뒤 앞 주자 한 칸씩(3루 주자는 득점).</summary>
            void Advance()
            {
                Home(2);
                if (bases[1] >= 0) Move(1, 2);
                if (bases[0] >= 0) Move(0, 1);
            }

            void Out(int[] bl, int[] pl)
            {
                outs++;
                stintOuts[def]++;
                pl[Line.Outs]++;
                if (bl != null) bl[Line.AB]++;
            }

            void Move(int from, int to)
            {
                bases[to] = bases[from];
                bases[from] = -1;
            }

            void Home(int b)
            {
                if (bases[b] < 0) return;
                ScoreRunner(bases[b]);
                bases[b] = -1;
            }

            void ScoreRunner(int slot)
            {
                score[bat]++;
                runsHalf++;
                stintRuns[def]++;
                res.Lines[bat][slot].V[Line.R]++;
                res.Lines[def][pitcher[def]].V[Line.RA]++;
            }

            /// <summary>앞선 팀이 바뀌면 승 · 패 후보 투수와 결승 장면을 새로 잡음.</summary>
            void Lead(int slot, PlayKind kind)
            {
                int lead = score[0] > score[1] ? 0 : score[1] > score[0] ? 1 : -1;
                if (lead == leader) return;
                leader = lead;
                if (lead < 0) return;
                winP = pitcher[lead];
                loseP = pitcher[1 - lead];
                res.DeciderBatter = slot;
                res.DeciderInning = inning;
                res.DeciderKind = kind;
            }

            Play Snapshot(PlayKind kind, int batter, int pitcherSlot, int runs, int balls, int strikes)
            {
                byte b = (byte)((bases[0] >= 0 ? 1 : 0) | (bases[1] >= 0 ? 2 : 0) | (bases[2] >= 0 ? 4 : 0));
                return new Play { Inning = (byte)inning, Bottom = bat == 1, Batter = (byte)batter, Pitcher = (byte)pitcherSlot, Kind = kind, Outs = (byte)outs, Bases = b, Runs = (byte)runs, Away = (byte)score[0], Home = (byte)score[1], Balls = (byte)balls, Strikes = (byte)strikes, Pitches = (short)pitches[def] };
            }

            /// <summary>
            /// 승 · 패 · 세이브: 마지막으로 앞선 순간의 두 팀 투수, 선발이 5이닝(15아웃) 못 채웠으면 그 팀 첫 불펜이 승.
            /// 세이브 = 이긴 팀 마지막 투수(승리 투수 아님)가 1 ~ 3점 앞설 때 올라와 끝냄. shortcut: 공식 기록원 재량 · 3이닝 세이브 규칙 없음.
            /// </summary>
            void Finish()
            {
                var box = res.Box;
                for (int side = 0; side < 2; side++) box.Inn[side] = inn[side].ToArray();
                if (score[0] == score[1] || leader < 0) return;
                int win = score[1] > score[0] ? 1 : 0, lose = 1 - win;
                if (winP < Roster.FirstReliever && res.Lines[win][winP][Line.Outs] < WinOuts && used[win].Count > 1) winP = used[win][1];
                box.Win = (sbyte)winP;
                box.Lose = (sbyte)loseP;
                res.Lines[win][winP].V[Line.W] = 1;
                res.Lines[lose][loseP].V[Line.L] = 1;
                int last = pitcher[win];
                if (last != winP && entryLead[win] >= 1 && entryLead[win] <= SaveLead)
                {
                    box.Save = (sbyte)last;
                    res.Lines[win][last].V[Line.SV] = 1;
                }
            }
        }

        /// <summary>
        /// 개발용 자체 검사: 결정성(같은 입력 같은 결과) · 한 이닝 3아웃(끝내기 · 9회말 없음 빼고) · 9이닝 이상 · 득점 = 이닝 점수 합 = 선수 득점 합 ·
        /// 안타 · 볼넷 합 · 승 · 패 한 명씩 · 무승부 규칙(정규 연장 상한 · 포스트시즌 없음) · 공 목록과 타석 이어짐 · 리그 평균 득점 범위.
        /// </summary>
        public static void SelfCheck(Action<bool, string> check)
        {
            var start = new DateTime(2026, 3, 24);
            var s = new Season { Rule = Rule.KBO, Start = start, Seed = 42, Games = Schedule.Make(Rule.KBO, start, 42) };
            var a = Run(s, s.Games[0], 0, 0, true);
            var b = Run(s, s.Games[0], 0, 0, true);
            bool same = a.Away == b.Away && a.Home == b.Home && a.Plays.Count == b.Plays.Count && a.Pitches.Count == b.Pitches.Count;
            for (int i = 0; same && i < a.Plays.Count; i++) same = a.Plays[i].Kind == b.Plays[i].Kind && a.Plays[i].Batter == b.Plays[i].Batter;
            check(same, "시뮬: 같은 경기를 다시 돌렸는데 결과가 다름");
            var quiet = Run(s, s.Games[0], 0, 0);
            check(quiet.Away == a.Away && quiet.Home == a.Home && quiet.Box.H[0] == a.Box.H[0], "시뮬: 중계를 안 남기면 결과가 다름");
            check(Run(s, s.Games[0], 0, 1).Box.Attempt == 1, "시뮬: 시드 차례가 박스에 안 남음");

            int games = 0, runs = 0, ties = 0, extra = 0, pa = 0, hits = 0, ab = 0, k = 0, bb = 0, hr = 0;
            bool outsOk = true, innOk = true, sumOk = true, decOk = true, tieOk = true, pitchOk = true;
            for (int i = 0; i < 400; i++)
            {
                var g = s.Games[i];
                if (i % 50 == 0) g.Series = 1; // 포스트시즌 경기 흉내(무승부 없음)
                var r = Run(s, g, i, 0, i % 10 == 0);
                g.Series = 0;
                var box = r.Box;
                int ar = r.Away, hrs = r.Home;
                games++;
                runs += ar + hrs;
                innOk &= box.Inn[0].Length >= Innings && box.Inn[1].Length >= box.Inn[0].Length - 1 && box.Inn[1].Length <= box.Inn[0].Length;
                if (ar == hrs)
                {
                    ties++;
                    tieOk &= box.Inn[0].Length == Rules.MaxInnings(s.Rule) && i % 50 != 0;
                    decOk &= box.Win < 0 && box.Lose < 0;
                }
                else decOk &= box.Win >= Roster.FirstPitcher && box.Lose >= Roster.FirstPitcher && box.Save != box.Win;
                if (box.Inn[0].Length > Innings) extra++;
                for (int side = 0; side < 2; side++)
                {
                    int pr = 0, ph = 0, pb = 0, po = 0, w = 0, l = 0;
                    foreach (var line in r.Lines[side])
                    {
                        pr += line[Line.R];
                        ph += line[Line.H];
                        pb += line[Line.BB];
                        w += line[Line.W];
                        l += line[Line.L];
                        pa += line[Line.PA];
                        hits += line[Line.H];
                        ab += line[Line.AB];
                        k += line[Line.SO];
                        bb += line[Line.BB];
                        hr += line[Line.HR];
                    }
                    foreach (var line in r.Lines[1 - side]) po += line[Line.Outs];
                    int halves = box.Inn[side].Length, need = halves * 3;
                    // 마지막 말이 끝내기면 3아웃 전에 끝남
                    bool lastShort = side == 1 && hrs > ar && box.Inn[1].Length == box.Inn[0].Length;
                    outsOk &= lastShort ? po < need && po >= need - 3 : po == need;
                    sumOk &= pr == box.Runs(side) && ph == box.H[side] && pb == box.B[side];
                    sumOk &= ar == hrs ? w + l == 0 : side == (hrs > ar ? 1 : 0) ? w == 1 && l == 0 : w == 0 && l == 1;
                }
                if (r.Plays != null)
                {
                    int last = -1;
                    foreach (var p in r.Pitches)
                    {
                        pitchOk &= p.Play >= last && p.Play < r.Plays.Count && r.Plays[p.Play].Kind != PlayKind.Change;
                        last = p.Play;
                    }
                    var end = r.Plays[r.Plays.Count - 1];
                    pitchOk &= end.Away == ar && end.Home == hrs;
                }
            }
            check(innOk, "시뮬: 9이닝 미만 · 홈 말 수 어긋남");
            check(outsOk, "시뮬: 한 이닝 3아웃이 아님");
            check(sumOk, "시뮬: 박스 점수 · 안타 · 볼넷과 선수 기록 합이 다름 · 승패 투수 수 어긋남");
            check(decOk, "시뮬: 승 · 패 · 세이브 투수가 어긋남");
            check(tieOk, "시뮬: 무승부가 연장 상한 전 · 포스트시즌에 나옴");
            check(pitchOk, "시뮬: 공 목록과 타석 · 마지막 점수가 어긋남");
            float perTeam = runs / (games * 2f), avg = ab == 0 ? 0f : (float)hits / ab;
            check(perTeam > 3.5f && perTeam < 6.5f, $"시뮬: 팀당 평균 득점 {perTeam:0.00} 이 3.5 ~ 6.5 밖");
            check(avg > 0.230f && avg < 0.300f, $"시뮬: 리그 타율 {avg:.000} 이 .230 ~ .300 밖");
            check(ties < games / 20, $"시뮬: 무승부 {ties} / {games} 너무 많음");
            Stats = $"팀당 {perTeam:0.00}점 · 타율 {avg:.000} · 삼진 {100f * k / pa:0.0}% · 볼넷 {100f * bb / pa:0.0}% · 홈런 {100f * hr / pa:0.0}% · 무 {ties} · 연장 {extra} / {games}";
        }

        /// <summary>마지막 자체 검사의 리그 평균(조정 확인용 로그).</summary>
        public static string Stats = "";
    }
}
