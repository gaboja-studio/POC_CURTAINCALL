using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace CurtainCall.WaitingRoom.Tests
{
    public sealed class WaitingRoomTextTests
    {
        const string FilePath = "Assets/Resources/Settings/WaitingRoom/WaitingRoomTexts.txt";

        // 코드의 Get/Format("키", "기본 문구") 호출. 기본 문구는 하나의 일반 문자열이어야 한다.
        static readonly Regex Call = new(@"WaitingRoomText\.(?:Get|Format)\(\s*""([^""]+)""\s*,\s*""((?:[^""\\]|\\.)*)""", RegexOptions.Compiled);

        [TearDown] public void Restore() => WaitingRoomText.Clear();

        [Test] public void FileOverridesAndMissingKeyUsesFallback()
        {
            WaitingRoomText.Install("# 설명\na.b | 바꾼 {item:을} 줍기\\n둘째 줄\n빈키 |\n형식 틀림");
            Assert.AreEqual("바꾼 머리핀을 줍기\n둘째 줄", WaitingRoomText.Format("a.b", "기본", ("item", "머리핀")));
            Assert.AreEqual("기본 3", WaitingRoomText.Format("없음", "기본 {n}", ("n", 3)));
            Assert.AreEqual("기본", WaitingRoomText.Get("빈키", "기본"), "빈 문구는 무시");
            Assert.AreEqual("{모름} 그대로", WaitingRoomText.Format("x", "{모름} 그대로", ("n", 1)));
        }

        [Test] public void ParticlesFollowFinalConsonant()
        {
            Assert.AreEqual("을", WaitingRoomText.Particle("머리핀", "를"));
            Assert.AreEqual("를", WaitingRoomText.Particle("UV 등 상자", "을"));
            Assert.AreEqual("가", WaitingRoomText.Particle("배관함 문 하나 더 열 거야", "이"));
            Assert.AreEqual("이", WaitingRoomText.Particle("벽", "가"));
            Assert.AreEqual("로", WaitingRoomText.Particle("연습실", "으로"));
            Assert.AreEqual("으로", WaitingRoomText.Particle("소품방", "로"));
            Assert.AreEqual("로", WaitingRoomText.Particle("로비", "으로"));
            Assert.AreEqual("이", WaitingRoomText.Particle("3", "가"), "삼");
            Assert.AreEqual("가", WaitingRoomText.Particle("2", "이"), "이");
        }

        [Test] public void ShippedFileCoversEveryCodeKeyWithSameNames()
        {
            var file = WaitingRoomText.Parse(File.ReadAllText(FilePath));
            var problems = new System.Collections.Generic.List<string>();
            var seen = new System.Collections.Generic.Dictionary<string, string>();
            foreach (var path in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                if (path.Contains("/Tests/") || path.Contains("\\Tests\\")) continue;
                foreach (Match m in Call.Matches(File.ReadAllText(path)))
                {
                    string key = m.Groups[1].Value, fallback = Regex.Unescape(m.Groups[2].Value);
                    if (seen.TryGetValue(key, out var other) && other != fallback) problems.Add($"{key}: 기본 문구가 두 가지 ({Path.GetFileName(path)})");
                    seen[key] = fallback;
                    if (!file.TryGetValue(key, out var text)) { problems.Add($"{key}: 문구 파일에 없음 ({Path.GetFileName(path)})"); continue; }
                    var extra = WaitingRoomText.Names(text).Except(WaitingRoomText.Names(fallback)).ToList();
                    if (extra.Count > 0) problems.Add($"{key}: 코드가 채우지 않는 이름 {{{string.Join("}, {", extra)}}}");
                }
            }
            Assert.Greater(seen.Count, 0, "코드에서 키를 찾지 못함");
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test] public void ShippedFileHasNoUnusedKeys()
        {
            var file = WaitingRoomText.Parse(File.ReadAllText(FilePath));
            var used = Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories)
                .Where(p => !p.Contains("/Tests/") && !p.Contains("\\Tests\\"))
                .SelectMany(p => Call.Matches(File.ReadAllText(p)).Cast<Match>().Select(m => m.Groups[1].Value))
                .ToHashSet();
            // 아이템·탐색 대상 키는 id로 조립한다(item.<id>.name/worn/desc, target.<id>.name/desc/text/secret).
            // 추격 단계 알림은 단계 번호로 조립한다(pursuit.stage.<번호>, PursuitTexts.StageKey).
            var unused = file.Keys.Where(k => !used.Contains(k) && !k.StartsWith("item.") && !k.StartsWith("target.")
                && !Regex.IsMatch(k, @"^pursuit\.stage\.\d+$")).ToList();
            Assert.IsEmpty(unused, "쓰지 않는 키(오타?): " + string.Join(", ", unused));
        }

        [Test] public void TargetKeysMatchSceneTargets()
        {
            const string ScenePath = "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity";
            var file = WaitingRoomText.Parse(File.ReadAllText(FilePath));
            // 방은 공간 프리팹 인스턴스라 대상 id가 프리팹 파일과 씬의 오버라이드 값에 나뉘어 있다.
            const string SpacesPath = "Assets/Resources/Prefabs/Spaces/WaitingRoom";
            var sources = Directory.GetFiles(SpacesPath, "*.prefab").Prepend(ScenePath).Select(File.ReadAllText).ToList();
            var ids = sources.SelectMany(text => Regex.Matches(text, @"^  targetId: (.+)$", RegexOptions.Multiline).Cast<Match>()
                    .Concat(Regex.Matches(text, @"propertyPath: targetId\r?\n\s+value: (.+)$", RegexOptions.Multiline).Cast<Match>()))
                .Select(m => m.Groups[1].Value.Trim()).ToHashSet();
            var keys = file.Keys.Where(k => k.StartsWith("target.")).ToList();
            Assert.Greater(keys.Count, 0, "탐색 대상 이름 구역이 비었음");
            var wrong = keys.Where(k =>
            {
                int dot = k.LastIndexOf('.');
                string id = k.Substring("target.".Length, dot - "target.".Length), part = k.Substring(dot + 1);
                return !ids.Contains(id) || (part != "name" && part != "desc" && part != "text" && part != "hint" && part != "secret");
            }).ToList();
            Assert.IsEmpty(wrong, "씬에 없는 대상 id 또는 잘못된 끝말(name/desc/text/hint/secret): " + string.Join(", ", wrong));
        }
    }
}
