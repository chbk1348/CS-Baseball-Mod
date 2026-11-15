using System;
using System.IO;

namespace BaseballLeague.Data
{
    /// <summary>
    /// 세이브 안 시즌 형식(BaseballSave 가 씀 · 게임 형식과 떨어져 있어 자체 검사를 도시 밖에서도 돌림):
    /// 형식 판 · 리그 규칙 · 이름 방식 · 시작일 · 시드 · 경기(날짜 · 시각 · 홈 · 원정 · 점수 · 교류전 · 우천 재편성 · 더블헤더(판 3) · 시리즈(판 3) · 박스 스코어 요약(판 4))
    /// · 홈구장 건물 번호(판 2) · 포스트시즌 시리즈 · 우승 이력(판 3, 정규 승패 · MVP 판 4) · 홈팀 편집값 · 스위치 · 시작 전력 · 명단 · 능력 · 정규 시즌 선수 기록 합(판 4).
    /// 판 4 이전은 없는 칸을 기본값으로 읽음(편집값 · 스위치 기본, 명단 없음 → 처음 쓸 때 새로 지음, 선수 기록 0, 박스 없음).
    /// </summary>
    public static class SaveFormat
    {
        public const int Version = 4;
        const int MaxText = 40, MaxInnings = 64;

        /// <summary>version 은 검사용(옛 판 데이터 만들기) · 저장은 늘 Version. Sync 안에서 부름.</summary>
        public static byte[] Write(Season s, ushort stadium, int version = Version)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(version);
            w.Write((byte)s.Rule);
            w.Write((byte)s.Naming);
            w.Write(s.Start.Ticks);
            w.Write(s.Seed);
            var games = s.Games.ToArray();
            w.Write(games.Length);
            foreach (var g in games)
            {
                w.Write((int)(g.Day - s.Start).TotalDays);
                w.Write(g.Minute);
                w.Write(g.Home);
                w.Write(g.Away);
                w.Write(g.HomeRuns);
                w.Write(g.AwayRuns);
                w.Write((byte)((g.Inter ? 1 : 0) | (g.Moved ? 2 : 0) | (version >= 3 && g.Double ? 4 : 0)));
                if (version >= 3) w.Write(g.Series);
                if (version >= 4) WriteBox(w, g.Played ? g.Box : null);
            }
            if (version >= 2) w.Write(stadium);
            if (version < 3) return Done(w, ms);
            var post = s.Post.ToArray();
            w.Write(post.Length);
            foreach (var x in post)
            {
                w.Write(x.Round);
                w.Write(x.League);
                w.Write(x.High);
                w.Write(x.Low);
            }
            var history = s.History.ToArray();
            w.Write(history.Length);
            foreach (var t in history)
            {
                w.Write(t.Year);
                w.Write(t.Champion);
                w.Write(t.Runner);
                if (version < 4) continue;
                w.Write(t.W);
                w.Write(t.L);
                w.Write(t.Mvp ?? "");
                w.Write(t.MvpTeam);
            }
            if (version < 4) return Done(w, ms);
            var c = s.Club;
            w.Write(c.Name);
            w.Write(c.Manager);
            w.Write((byte)c.Abbr.Length);
            foreach (var a in c.Abbr) w.Write(a ?? "");
            w.Write(c.Color);
            w.Write(c.Power);
            w.Write((byte)((c.AdjustWin ? 1 : 0) | (c.NotifyResult ? 2 : 0) | (c.NotifyStart ? 4 : 0) | (c.NotifyBig ? 8 : 0) | (c.LiveBoard ? 16 : 0)));
            w.Write(s.Power0);
            var rosters = s.Rosters;
            w.Write((byte)(rosters?.Length ?? 0));
            if (rosters != null)
                foreach (var r in rosters)
                {
                    w.Write(r.Manager);
                    foreach (var p in r.P)
                    {
                        w.Write(p.Name);
                        w.Write(p.A);
                        w.Write(p.B);
                        w.Write(p.C);
                    }
                }
            var stats = s.Stats;
            w.Write((byte)(stats?.Length ?? 0));
            if (stats != null)
                foreach (var team in stats)
                    foreach (var line in team)
                        foreach (int v in line.V)
                            w.Write((ushort)Math.Min(ushort.MaxValue, Math.Max(0, v)));
            return Done(w, ms);
        }

        static byte[] Done(BinaryWriter w, MemoryStream ms)
        {
            w.Flush();
            return ms.ToArray();
        }

        static void WriteBox(BinaryWriter w, Box b)
        {
            w.Write(b != null);
            if (b == null) return;
            w.Write(b.Attempt);
            w.Write(b.Flags);
            for (int side = 0; side < 2; side++)
            {
                w.Write(b.SP[side]);
                w.Write(b.H[side]);
                w.Write(b.E[side]);
                w.Write(b.B[side]);
                w.Write((byte)b.Inn[side].Length);
                w.Write(b.Inn[side]);
            }
            w.Write(b.Win);
            w.Write(b.Lose);
            w.Write(b.Save);
            w.Write(b.Crowd);
        }

        static Box ReadBox(BinaryReader r)
        {
            if (!r.ReadBoolean()) return null;
            var b = new Box { Attempt = r.ReadByte(), Flags = r.ReadByte() };
            for (int side = 0; side < 2; side++)
            {
                b.SP[side] = Slot(r.ReadByte());
                b.H[side] = r.ReadByte();
                b.E[side] = r.ReadByte();
                b.B[side] = r.ReadByte();
                int n = r.ReadByte();
                if (n == 0 || n > MaxInnings) throw new InvalidDataException("이닝 수 " + n);
                b.Inn[side] = r.ReadBytes(n);
            }
            b.Win = Pitcher(r.ReadSByte());
            b.Lose = Pitcher(r.ReadSByte());
            b.Save = Pitcher(r.ReadSByte());
            b.Crowd = r.ReadUInt16();
            return b;
        }

        static byte Slot(byte v) => v < Roster.Size ? v : throw new InvalidDataException("명단 자리 " + v);

        static sbyte Pitcher(sbyte v) => v == -1 || v >= Roster.FirstPitcher && v < Roster.Size ? v : throw new InvalidDataException("투수 자리 " + v);

        static string Text(BinaryReader r)
        {
            string t = r.ReadString();
            return t.Length <= MaxText ? t : throw new InvalidDataException("글 길이 " + t.Length);
        }

        /// <summary>
        /// 못 읽은 세이브 원본(없으면 null): 사용자가 설정 '새 시즌 시작'을 확인하기 전까지 저장 때 이것을 그대로 다시 씀(임시 시즌은 저장하지 않음).
        /// 불러올 때 Load 가 잡고 Discard 가 버림. 저장 스레드가 읽어 통째로 갈아 끼움.
        /// </summary>
        public static volatile byte[] Unread;

        /// <summary>불러온 데이터 → 시즌. 못 읽으면 원본을 Unread 에 잡고 null(error = 까닭).</summary>
        public static Season Load(byte[] data, out ushort stadium, out string error)
        {
            Unread = null;
            stadium = 0;
            error = null;
            if (data == null || data.Length == 0) return null;
            try
            {
                return Read(data, out stadium);
            }
            catch (Exception e)
            {
                Unread = data;
                error = e.Message;
                return null;
            }
        }

        /// <summary>저장할 데이터: 못 읽은 원본이 있으면 그것, 없으면 시즌 s(없으면 null). 시즌은 Sync 안에서 씀.</summary>
        public static byte[] Payload(Season s, ushort stadium)
        {
            var u = Unread;
            if (u != null) return u;
            if (s == null) return null;
            lock (League.Sync) return Write(s, stadium); // 본 스레드가 경기 · 기록을 고치는 도중의 반쯤 쓴 값을 쓰지 않게
        }

        /// <summary>사용자가 새 시즌을 시작함: 못 읽은 원본을 버리고 다음 저장부터 지금 시즌을 씀.</summary>
        public static void Discard() => Unread = null;

        public static Season Read(byte[] data, out ushort stadium)
        {
            using var r = new BinaryReader(new MemoryStream(data));
            int version = r.ReadInt32();
            if (version < 1 || version > Version) throw new InvalidDataException("모르는 형식 판 " + version);
            var rule = (Rule)r.ReadByte();
            var naming = (Naming)r.ReadByte();
            if (rule != Rule.KBO && rule != Rule.NPB) throw new InvalidDataException("모르는 리그 규칙 " + rule);
            if (naming != Naming.Korean && naming != Naming.Japanese) naming = Naming.Korean;
            var s = new Season { Rule = rule, Naming = naming, Start = new DateTime(r.ReadInt64()), Seed = r.ReadInt32() };
            int count = r.ReadInt32(), teams = Rules.TeamCount(rule);
            if (count <= 0 || count > 10000) throw new InvalidDataException("경기 수 " + count);
            for (int i = 0; i < count; i++)
            {
                var g = new Game { Day = s.Start.AddDays(r.ReadInt32()), Minute = r.ReadInt16(), Home = r.ReadByte(), Away = r.ReadByte() };
                g.HomeRuns = r.ReadSByte();
                g.AwayRuns = r.ReadSByte();
                byte flags = r.ReadByte();
                g.Inter = (flags & 1) != 0;
                g.Moved = (flags & 2) != 0;
                g.Double = (flags & 4) != 0;
                if (version >= 3) g.Series = r.ReadByte();
                if (version >= 4) g.Box = ReadBox(r);
                if (g.Home >= teams || g.Away >= teams || g.Home == g.Away) throw new InvalidDataException("팀 번호 " + g.Home + " · " + g.Away);
                s.Games.Add(g);
            }
            stadium = version >= 2 ? r.ReadUInt16() : (ushort)0;
            if (version < 3) return s;
            int posts = r.ReadInt32();
            if (posts < 0 || posts > 16) throw new InvalidDataException("시리즈 수 " + posts);
            for (int i = 0; i < posts; i++)
            {
                var x = new Series { Round = r.ReadByte(), League = r.ReadByte(), High = r.ReadByte(), Low = r.ReadByte() };
                if (x.Round >= Rules.RoundCount(rule) || x.League >= Rules.LeagueCount(rule) || x.High >= teams || x.Low >= teams || x.High == x.Low) throw new InvalidDataException("시리즈 " + i);
                s.Post.Add(x);
            }
            foreach (var g in s.Games)
                if (g.Series > posts) throw new InvalidDataException("경기 시리즈 번호 " + g.Series);
            int titles = r.ReadInt32();
            if (titles < 0 || titles > 10000) throw new InvalidDataException("우승 이력 수 " + titles);
            int all = Rules.TeamCount(Rule.NPB);
            for (int i = 0; i < titles; i++)
            {
                var t = new Title { Year = r.ReadInt32(), Champion = r.ReadByte(), Runner = r.ReadByte() };
                if (version >= 4)
                {
                    t.W = r.ReadInt16();
                    t.L = r.ReadInt16();
                    t.Mvp = Text(r);
                    t.MvpTeam = r.ReadByte();
                }
                if (t.Champion >= all || t.Runner >= all || t.MvpTeam >= all) throw new InvalidDataException("우승 이력 팀 번호");
                s.History.Add(t);
            }
            if (version < 4) return s;
            var c = new Club { Name = Text(r), Manager = Text(r) };
            int abbrs = r.ReadByte();
            if (abbrs > all) throw new InvalidDataException("줄임 이름 수 " + abbrs);
            for (int i = 0; i < abbrs; i++) c.Abbr[i] = Text(r);
            c.Color = r.ReadSByte();
            c.Power = r.ReadByte();
            byte sw = r.ReadByte();
            c.AdjustWin = (sw & 1) != 0;
            c.NotifyResult = (sw & 2) != 0;
            c.NotifyStart = (sw & 4) != 0;
            c.NotifyBig = (sw & 8) != 0;
            c.LiveBoard = (sw & 16) != 0;
            if (c.Color < -1 || c.Color >= all || c.Power > 2) throw new InvalidDataException("홈팀 편집값");
            s.Club = c;
            s.Power0 = r.ReadByte();
            if (s.Power0 > 2) throw new InvalidDataException("시작 전력 " + s.Power0);
            int rosters = r.ReadByte();
            if (rosters != 0 && rosters != teams) throw new InvalidDataException("명단 팀 수 " + rosters);
            if (rosters > 0)
            {
                s.Rosters = new Roster[rosters];
                for (int t = 0; t < rosters; t++)
                {
                    var ro = s.Rosters[t] = new Roster { Manager = Text(r) };
                    for (int i = 0; i < Roster.Size; i++) ro.P[i] = new Player { Name = Text(r), A = r.ReadByte(), B = r.ReadByte(), C = r.ReadByte() };
                }
            }
            int stats = r.ReadByte();
            if (stats != 0 && stats != teams) throw new InvalidDataException("기록 팀 수 " + stats);
            if (stats > 0)
            {
                s.Stats = Season.NewStats(stats);
                foreach (var team in s.Stats)
                    foreach (var line in team)
                        for (int k = 0; k < Line.Count; k++)
                            line.V[k] = r.ReadUInt16();
            }
            return s;
        }

        /// <summary>
        /// 개발용 자체 검사: 쓰고 다시 읽으면 같은 시즌 · 홈구장 · 포스트시즌 · 이력 · 박스 · 편집값 · 명단 · 기록,
        /// 판 1 · 2 · 3 은 없는 칸 기본값으로 읽힘, 깨진 데이터는 예외(→ 새 시즌), 한 시즌을 끝까지 한 세이브 크기 상한.
        /// </summary>
        public static void SelfCheck(Action<bool, string> check)
        {
            var start = new DateTime(2026, 3, 24);
            var s = new Season { Rule = Rule.NPB, Naming = Naming.Japanese, Start = start, Seed = 7, Games = Schedule.Make(Rule.NPB, start, 7) };
            s.Games[3].HomeRuns = 4;
            s.Games[3].AwayRuns = 2;
            s.Games[5].Moved = true;
            s.Games[5].Double = s.Games[6].Double = true;
            var res = Sim.Run(s, s.Games[8], 8, 2);
            res.Box.Crowd = 21345;
            res.Box.Flags |= Box.FlagReal;
            s.Games[8].Box = res.Box;
            s.Games[8].AwayRuns = (sbyte)res.Away;
            s.Games[8].HomeRuns = (sbyte)res.Home;
            s.Post.Add(new Series { Round = 1, League = 1, High = 6, Low = 8 });
            s.Games.Add(new Game { Day = start.AddDays(200), Minute = 1080, Home = 6, Away = 8, Series = 1, HomeRuns = 3, AwayRuns = 1 });
            s.History.Add(new Title { Year = 2025, Champion = 2, Runner = 9, W = 81, L = 60, Mvp = "사토 렌", MvpTeam = 4 });
            var club = s.Club.Clone();
            club.Name = "바다 펠리컨스";
            club.Manager = "감독 이름";
            club.Abbr[0] = "SEA";
            club.Abbr[5] = "MSX";
            club.Color = 10;
            club.Power = 2;
            club.LiveBoard = true;
            club.NotifyStart = false;
            s.Club = club;
            s.Power0 = 2;
            s.RosterOf(0);
            s.StatOf(3, 14).V[Line.K] = 77;
            byte[] data = Write(s, 1234);
            var back = Read(data, out ushort stadium);
            check(stadium == 1234, "세이브: 홈구장 번호가 다름");
            bool same = back.Rule == s.Rule && back.Naming == s.Naming && back.Start == s.Start && back.Seed == s.Seed && back.Games.Count == s.Games.Count;
            for (int i = 0; same && i < s.Games.Count; i++)
            {
                Game a = s.Games[i], b = back.Games[i];
                same = a.Day == b.Day && a.Minute == b.Minute && a.Home == b.Home && a.Away == b.Away && a.HomeRuns == b.HomeRuns && a.AwayRuns == b.AwayRuns && a.Inter == b.Inter && a.Moved == b.Moved && a.Double == b.Double && a.Series == b.Series && (a.Box == null) == (b.Box == null);
            }
            check(same, "세이브: 쓰고 읽은 시즌이 다름");
            var bb = back.Games[8].Box;
            check(bb != null && bb.Crowd == 21345 && bb.Attempt == 2 && bb.Flags == res.Box.Flags && bb.Runs(0) == res.Away && bb.Runs(1) == res.Home && bb.Win == res.Box.Win && bb.SP[1] == res.Box.SP[1] && bb.H[0] == res.Box.H[0], "세이브: 박스 스코어가 다름");
            check(back.Post.Count == 1 && back.Post[0].Round == 1 && back.Post[0].League == 1 && back.Post[0].High == 6 && back.Post[0].Low == 8, "세이브: 포스트시즌 시리즈가 다름");
            var h = back.History.Count == 1 ? back.History[0] : null;
            check(h != null && h.Year == 2025 && h.Champion == 2 && h.Runner == 9 && h.W == 81 && h.L == 60 && h.Mvp == "사토 렌" && h.MvpTeam == 4, "세이브: 우승 이력이 다름");
            var c = back.Club;
            check(c.Name == club.Name && c.Manager == club.Manager && c.Abbr[0] == "SEA" && c.Abbr[5] == "MSX" && c.Abbr[1] == "" && c.Color == 10 && c.Power == 2 && c.LiveBoard && !c.NotifyStart && c.AdjustWin && back.Power0 == 2, "세이브: 홈팀 편집값 · 스위치가 다름");
            check(back.Abbr(0) == "SEA" && back.Abbr(1) == Rules.Abbr(1) && back.Team(0).Name == club.Name, "세이브: 줄임 이름 · 팀 이름이 안 바뀜");
            check(back.Rosters != null && back.Rosters[11].P[24].Name == s.Rosters[11].P[24].Name && back.Rosters[3].P[2].B == s.Rosters[3].P[2].B && back.Rosters[0].Manager == s.Rosters[0].Manager, "세이브: 명단 · 능력이 다름");
            check(back.Stats != null && back.StatOf(3, 14)[Line.K] == 77 && back.StatOf(0, 0)[Line.PA] == 0, "세이브: 선수 기록이 다름");
            // 판 1 · 2 · 3: 옛 형식으로 쓴 정규 시즌만(시리즈 경기는 옛 판에 없음)
            s.Games.RemoveAt(s.Games.Count - 1);
            for (int v = 1; v <= 3; v++)
            {
                var old = Read(Write(s, 1234, v), out ushort oldStadium);
                check(oldStadium == (v == 1 ? 0 : 1234) && old.Games.Count == s.Games.Count && old.Games[3].HomeRuns == 4 && old.Games[5].Moved, $"세이브: 판 {v} 을 못 읽음");
                check((v == 3) == old.Games[5].Double && (v == 3) == (old.Post.Count == 1) && (v == 3) == (old.History.Count == 1), $"세이브: 판 {v} 의 없는 칸이 기본값 아님");
                check(old.Games[8].Box == null && old.Rosters == null && old.Stats == null && old.Club.Name == "" && old.Club.AdjustWin && old.Power0 == 1 && (v < 3 || old.History[0].Mvp == ""), $"세이브: 판 {v} 의 판 4 칸이 기본값 아님");
                check(old.RosterOf(0).P[0].Name.Length > 0 && old.Abbr(0) == Rules.Abbr(0), $"세이브: 판 {v} 에서 명단을 새로 못 지음");
            }
            bool threw = false;
            try
            {
                Read(new byte[] { 9, 0, 0, 0, 1 }, out _);
            }
            catch (Exception)
            {
                threw = true;
            }
            check(threw, "세이브: 모르는 판을 읽음");
            threw = false;
            try
            {
                var broken = Write(back, 1);
                Read(SubArray(broken, broken.Length - 7), out _);
            }
            catch (Exception)
            {
                threw = true;
            }
            check(threw, "세이브: 끝이 잘린 데이터를 읽음");
            // 못 읽은 세이브: 원본을 잡아 저장 때 그대로 다시 씀 → 새 시즌을 시작해야 지금 시즌을 씀
            var keep = Unread;
            var bad = new byte[] { 9, 0, 0, 0, 1 };
            check(Load(bad, out _, out string why) == null && Unread == bad && why != null, "세이브: 못 읽은 원본을 안 잡음");
            check(Payload(back, 1) == bad, "세이브: 못 읽었는데 원본 대신 임시 시즌을 씀");
            Discard();
            var fresh = Payload(back, 1);
            check(fresh != bad && Read(fresh, out _).Seed == back.Seed, "세이브: 새 시즌을 시작했는데 원본을 씀");
            check(Load(data, out ushort again, out _) != null && again == 1234 && Unread == null, "세이브: 읽은 세이브인데 원본을 잡음");
            check(Load(null, out _, out _) == null && Unread == null, "세이브: 빈 세이브를 못 읽은 원본으로 잡음");
            Unread = keep;
        }

        static byte[] SubArray(byte[] a, int n)
        {
            var b = new byte[n];
            Array.Copy(a, b, n);
            return b;
        }

        /// <summary>한 시즌을 끝까지 한 세이브 크기 상한(바이트).</summary>
        public const int MaxBytes = 96 * 1024;

        /// <summary>끝난 시즌 s 의 세이브 크기 검사(League.RunCheck 가 부름).</summary>
        public static void SizeCheck(Season s, string tag, Action<bool, string> check)
        {
            int n = Write(s, 1).Length;
            check(n < MaxBytes, $"{tag}세이브 {n / 1024}KB 가 상한 {MaxBytes / 1024}KB 를 넘음");
            var back = Read(Write(s, 1), out _);
            int played = 0, boxes = 0;
            foreach (var g in back.Games)
                if (g.Played)
                {
                    played++;
                    if (g.Box != null) boxes++;
                }
            check(played == s.Played && boxes == played, $"{tag}끝난 시즌 세이브의 박스가 빠짐 {boxes} / {played}");
            LastSize = n;
        }

        public static int LastSize;
    }
}
