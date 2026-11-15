using System;
using System.Collections.Generic;

namespace BaseballLeague.Data
{
    /// <summary>
    /// 선수 이름 생성기(결정적: 같은 방식 · 시드면 늘 같은 순서). 성 × 이름 조합을 섞어 앞에서부터 뽑으므로 한 번 뽑은 목록 안에는 같은 이름이 없음.
    /// 한국형은 붙여 씀(김민준), 일본형은 한글 표기 · 띄어 씀(다나카 하루토). 실존 유명 선수와 같은 조합은 Banned 로 거름.
    /// </summary>
    public static class Names
    {
        static readonly string[] KoreanFamily = { "김", "이", "박", "최", "정", "강", "조", "윤", "장", "임", "한", "오", "서", "신", "권", "황", "안", "송", "류", "홍" };

        static readonly string[] KoreanGiven =
        {
            "민준", "서준", "도윤", "예준", "시우", "하준", "주원", "지호", "지후", "준우",
            "준서", "건우", "도현", "현우", "지훈", "우진", "선우", "서진", "민재", "현준",
            "연우", "유준", "정우", "승우", "승현", "시윤", "준혁", "은우", "현진", "정후",
        };

        static readonly string[] JapaneseFamily = { "사토", "스즈키", "다카하시", "다나카", "와타나베", "이토", "야마모토", "나카무라", "고바야시", "가토", "요시다", "야마다", "사사키", "야마구치", "마쓰모토", "이노우에", "기무라", "하야시", "시미즈", "오타니" };

        static readonly string[] JapaneseGiven =
        {
            "하루토", "소타", "유토", "하루키", "렌", "미나토", "다이키", "쇼헤이", "이치로", "마사히로",
            "요시노부", "겐타", "쇼타", "다쿠미", "유키", "고타", "료타", "가이토", "히로토", "하야토",
            "쓰바사", "유마", "게이타", "슌", "다이스케", "나오키", "가즈마", "리쿠", "소마", "아오이",
        };

        /// <summary>실존 유명 선수(한국 · 일본) — 이 조합은 만들지 않음.</summary>
        static readonly HashSet<string> Banned = new()
        {
            "이승엽", "류현진", "박찬호", "이대호", "추신수", "김광현", "양현종", "이정후", "김하성", "선동열", "이종범", "오승환", "강정호", "김현수", "최정",
            "오타니 쇼헤이", "스즈키 이치로", "다나카 마사히로", "야마모토 요시노부", "사사키 로키", "마쓰이 히데키", "다르빗슈 유", "노모 히데오", "오 사다하루", "나가시마 시게오", "무라카미 무네타카",
        };

        static string Join(Naming naming, string family, string given) => naming == Naming.Japanese ? family + " " + given : family + given;

        /// <summary>naming 방식으로 만들 수 있는 모든 이름(금지 목록 뺌 · 정해진 순서).</summary>
        static List<string> All(Naming naming)
        {
            bool jp = naming == Naming.Japanese;
            string[] family = jp ? JapaneseFamily : KoreanFamily, given = jp ? JapaneseGiven : KoreanGiven;
            var list = new List<string>(family.Length * given.Length);
            foreach (var f in family)
                foreach (var g in given)
                {
                    string name = Join(naming, f, g);
                    if (!Banned.Contains(name)) list.Add(name);
                }
            return list;
        }

        /// <summary>만들 수 있는 이름 수(한 시즌에 뽑을 수 있는 최대).</summary>
        public static int Capacity(Naming naming) => All(naming).Count;

        /// <summary>서로 다른 이름 count 개(시드로 섞음). count 가 Capacity 보다 크면 예외.</summary>
        public static List<string> Make(Naming naming, int seed, int count)
        {
            var all = All(naming);
            if (count < 0 || count > all.Count) throw new ArgumentOutOfRangeException(nameof(count), count, "이름은 최대 " + all.Count);
            var rng = new Random(seed);
            for (int i = all.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                string t = all[i];
                all[i] = all[j];
                all[j] = t;
            }
            all.RemoveRange(count, all.Count - count);
            return all;
        }

        /// <summary>개발용 자체 검사: 방식마다 고유 조합 100 이상 · 중복 없음 · 금지 이름 안 나옴 · 같은 시드 같은 목록.</summary>
        internal static void SelfCheck(Action<bool, string> check)
        {
            foreach (var naming in new[] { Naming.Korean, Naming.Japanese })
            {
                int cap = Capacity(naming);
                check(cap >= 100, $"이름({naming}): 조합 {cap} < 100");
                var names = Make(naming, 11, cap);
                check(new HashSet<string>(names).Count == names.Count, $"이름({naming}): 중복");
                check(!names.Exists(Banned.Contains), $"이름({naming}): 금지 이름이 나옴");
                var a = Make(naming, 5, 100);
                var b = Make(naming, 5, 100);
                bool same = true;
                for (int i = 0; i < a.Count; i++) same &= a[i] == b[i];
                check(same, $"이름({naming}): 같은 시드인데 목록이 다름");
            }
        }
    }
}
