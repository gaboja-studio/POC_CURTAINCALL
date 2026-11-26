using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.WaitingRoom.EditorTools
{
    /// <summary>
    /// 2026-11-26 Task-205(사용자): 관리실 단말기 암호 두 개를 각각 입력·저장한다.
    /// 칸 수 = 정답 글자 수, 모든 글자 입력, 칸을 다 채우면 그 칸만 확인(맞으면 줄 긋고 다음 칸), 닫아도 유지, 둘 다 맞으면 기존 형식으로 판정 요청.
    /// 판정 값은 시험용 값이며 씬의 실제 암호가 아니다.
    /// </summary>
    public sealed class TerminalEntryTests
    {
        const string Date = "19990101", Word = "ABC";
        static TerminalEntry New() => new(Date.Length, Word.Length, (i, v) => v == (i == 0 ? Date : Word));
        static void Type(TerminalEntry e, string s) { foreach (char c in s) e.Type(c); }

        [Test]
        public void FieldsMatchAnswerLengths()
        {
            var e = New();
            Assert.AreEqual(8, e.Length(0));
            Assert.AreEqual(3, e.Length(1), "암구호가 3글자면 3칸");
        }

        [Test]
        public void CorrectDate_LocksAndMovesToWord()
        {
            var e = New();
            Type(e, Date);
            Assert.IsTrue(e.IsSolved(0));
            Assert.AreEqual(1, e.Active);
            e.Select(0);
            Assert.AreEqual(TerminalCheck.Locked, e.Type('9'), "맞힌 칸은 다시 바뀌지 않는다");
        }

        [Test]
        public void WrongDate_ClearsOnlyThatField()
        {
            var e = New();
            e.Select(1); Type(e, "AB");
            e.Select(0);
            Assert.AreEqual(TerminalCheck.Wrong, TypeAll(e, "12345678"));
            Assert.AreEqual("", e.Value(0));
            Assert.AreEqual("AB", e.Value(1), "다른 칸 입력은 남는다");
        }

        static TerminalCheck TypeAll(TerminalEntry e, string s) { var r = TerminalCheck.Incomplete; foreach (char c in s) r = e.Type(c); return r; }

        [Test]
        public void BothFieldsAcceptLettersAndDigits()
        {
            var e = New();
            Type(e, "A1");
            Assert.AreEqual("A1", e.Value(0));
            e.Select(1); Type(e, "9Z");
            Assert.AreEqual("9Z", e.Value(1));
            Assert.AreEqual(TerminalCheck.Incomplete, e.Type('-'), "자판에 없는 글자는 무시");
        }

        [Test]
        public void SelectAndMove_SwitchFields()
        {
            var e = New();
            e.Move(+1); Assert.AreEqual(1, e.Active);
            e.Move(+1); Assert.AreEqual(1, e.Active, "아래 끝에서 멈춤");
            e.Move(-1); Assert.AreEqual(0, e.Active);
            e.Select(1); Assert.AreEqual(1, e.Active);
        }

        [Test]
        public void WordFirstThenDate_SolvesBothAndCombinesInOldFormat()
        {
            var e = New();
            e.Select(1); Type(e, "abc");
            Assert.IsTrue(e.IsSolved(1), "소문자도 받는다");
            Assert.AreEqual(0, e.Active);
            Type(e, Date);
            Assert.IsTrue(e.AllSolved);
            Assert.AreEqual(Date + "\nABC", e.Combined);
        }

        // 2026-11-26 사용자: 위아래로 입력란을 바꿀 때 줄이 한 칸씩 밀리지 않아야 한다. 힌트 문구는 띄우지 않는다.
        [Test]
        public void SwitchingFields_KeepsBothRowsAligned_AndShowsNoHint()
        {
            var e = New();
            var root = new GameObject("TerminalAlign") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var panel = root.AddComponent<ExplorationPanel>();
                panel.OpenTerminal("관리실", e, v => { }, () => { });
                var texts = root.GetComponentsInChildren<TMPro.TMP_Text>(true);
                var code = System.Array.Find(texts, t => t.name == "Code");
                var word = System.Array.Find(texts, t => t.name == "Word");
                var message = System.Array.Find(texts, t => t.name == "Message");
                Assert.AreEqual("", message.text, "힌트 문구 없음");
                float Bracket(TMPro.TMP_Text t)
                {
                    t.ForceMeshUpdate();
                    var info = t.textInfo;
                    for (int i = 0; i < info.characterCount; i++)
                        if (info.characterInfo[i].character == '[') return info.characterInfo[i].origin;
                    Assert.Fail(t.name + " 칸 시작 없음"); return 0;
                }
                float codeBefore = Bracket(code), wordBefore = Bracket(word);
                Assert.AreEqual(codeBefore, wordBefore, .5f, "두 줄의 칸 시작 위치가 같다");
                panel.SelectField(1);
                Assert.AreEqual(codeBefore, Bracket(code), .5f, "입력란을 바꿔도 위 줄이 밀리지 않는다");
                Assert.AreEqual(wordBefore, Bracket(word), .5f, "입력란을 바꿔도 아래 줄이 밀리지 않는다");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void PanelReopen_KeepsTypedValues_AndStrikesSolved()
        {
            var e = New();
            var root = new GameObject("TerminalEntryPanel") { hideFlags = HideFlags.HideAndDontSave };
            var again = new GameObject("TerminalEntryPanelReopen") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var panel = root.AddComponent<ExplorationPanel>();
                string sent = null;
                panel.OpenTerminal("관리실", e, v => sent = v, () => { });
                foreach (char c in Date) panel.PressKey(c.ToString());
                panel.PressKey("A");
                panel.Hide();
                // 닫고 다시 여는 것: 단말기가 보관한 같은 입력 상태로 새 화면을 연다.
                panel = again.AddComponent<ExplorationPanel>();
                panel.OpenTerminal("관리실", e, v => sent = v, () => { });
                Assert.IsTrue(e.IsSolved(0));
                Assert.AreEqual("A", e.Value(1), "닫았다 열어도 입력 유지");
                var rows = again.GetComponentsInChildren<TMPro.TMP_Text>(true);
                Assert.IsTrue(System.Array.Exists(rows, t => t.name == "Code" && t.text.Contains("<s>" + Date + "</s>")), "맞힌 날짜는 줄이 그어진다");
                Assert.IsTrue(System.Array.Exists(rows, t => t.name == "Word" && t.text.Contains("[A__]")), "암구호 칸은 정답 글자 수만큼");
                panel.PressKey("B"); panel.PressKey("C");
                Assert.AreEqual(Date + "\nABC", sent, "둘 다 맞으면 판정을 요청한다");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(again); }
        }
    }
}
