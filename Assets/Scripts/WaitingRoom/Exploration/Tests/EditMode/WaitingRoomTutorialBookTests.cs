using System.Linq;
using NUnit.Framework;

namespace CurtainCall.WaitingRoom.Tests
{
    public sealed class WaitingRoomTutorialBookTests
    {
        [Test] public void DefaultsHaveUniqueIdsAndShortBodies()
        {
            var ids = WaitingRoomTutorialBook.Defaults.Select(e => e.id).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct().Count());
            // 패널 760×230·본문 21pt에 4줄 안쪽.
            foreach (var entry in WaitingRoomTutorialBook.Defaults)
                Assert.LessOrEqual(WaitingRoomTutorialBook.Format(entry.body, "F", "우클릭").Length, 100, entry.id);
        }

        [Test] public void QueueShowsInOrderOnceAndSkipsSeen()
        {
            var book = new WaitingRoomTutorialBook();
            Assert.IsTrue(book.Enqueue("G01"));
            Assert.IsTrue(book.Enqueue("G03"));
            Assert.IsFalse(book.Enqueue("G01"), "줄에 이미 있음");
            Assert.IsFalse(book.Enqueue("없는-ID"));
            Assert.IsTrue(book.TryNext(false, out var first));
            Assert.AreEqual("G01", first.id);
            book.MarkDone(first.id);
            Assert.IsFalse(book.Enqueue("G01"), "이미 본 안내");
            Assert.IsTrue(book.TryNext(false, out var second));
            Assert.AreEqual("G03", second.id);
        }

        [Test] public void BlockedInputOnlyLetsBlockedEntriesThrough()
        {
            var book = new WaitingRoomTutorialBook();
            book.Enqueue("G07"); book.Enqueue("G04");
            Assert.IsTrue(book.TryNext(true, out var entry));
            Assert.AreEqual("G04", entry.id, "조사 화면 안내는 입력이 막혀도 띄운다");
            book.MarkDone("G04");
            Assert.IsFalse(book.TryNext(true, out _));
            Assert.IsTrue(book.TryNext(false, out entry));
            Assert.AreEqual("G07", entry.id);
        }

        [Test] public void SaveRoundTripDropsUnknownIdsAndResetClears()
        {
            var book = new WaitingRoomTutorialBook();
            book.Load("G03,옛-ID,N04");
            Assert.IsTrue(book.IsDone("G03") && book.IsDone("N04"));
            Assert.AreEqual("G03,N04", book.Serialize());
            CollectionAssert.AreEqual(new[] { "G03", "N04" }, book.Seen().Select(e => e.id).ToArray());
            book.ResetAll();
            Assert.AreEqual("", book.Serialize());
            Assert.IsTrue(book.Enqueue("G03"));
        }

        [Test] public void TextFileLinesOverrideAndBadLinesAreSkipped()
        {
            var parsed = WaitingRoomTutorialBook.ParseTexts("# 설명\r\nG01 | 새 제목 | 새 본문 | 파이프 포함\r\n\r\nG02 | 제목만\r\nZ9 | 모름 | 모름\r\nG03 |  | 빈 제목\r\n");
            Assert.AreEqual(1, parsed.Count);
            Assert.AreEqual("새 본문 | 파이프 포함", parsed[0].body, "본문 안의 |는 그대로");
            var book = new WaitingRoomTutorialBook(parsed);
            Assert.AreEqual("새 제목", book.Get("G01").title);
            Assert.AreEqual("살펴보기", book.Get("G03").title, "틀린 줄은 기본 문구");
        }

        [Test] public void ShippedTextFileCoversEveryId()
        {
            string text = System.IO.File.ReadAllText("Assets/Resources/Settings/WaitingRoom/WaitingRoomTutorialTexts.txt");
            var ids = WaitingRoomTutorialBook.ParseTexts(text).Select(e => e.id).ToList();
            CollectionAssert.AreEquivalent(WaitingRoomTutorialBook.Defaults.Select(e => e.id).ToList(), ids);
        }

        [Test] public void OverridesReplaceOnlyKnownIdsAndKeysAreFilled()
        {
            var book = new WaitingRoomTutorialBook(new[] { new TutorialEntry("G12", "바꾼 제목", "[{interact}] [{uv}]"), new TutorialEntry("X", "x", "x") });
            Assert.AreEqual("바꾼 제목", book.Get("G12").title);
            Assert.IsNull(book.Get("X"));
            Assert.AreEqual("[E] [우클릭]", WaitingRoomTutorialBook.Format(book.Get("G12").body, "E", "우클릭"));
        }

        [Test] public void TextFileFourthColumnSetsSecondsOnlyWhenNumber()
        {
            var list = WaitingRoomTutorialBook.ParseTexts("G01 | 제목 | 본문 | 12\nG03 | 제목 | 본문 | 99\nG05 | 제목 | A | B\nG09 | 제목 | 본문");
            Assert.AreEqual(12f, list.Single(e => e.id == "G01").seconds);
            Assert.AreEqual("본문", list.Single(e => e.id == "G01").body);
            Assert.AreEqual(60f, list.Single(e => e.id == "G03").seconds, "60초까지");
            Assert.AreEqual("A | B", list.Single(e => e.id == "G05").body, "숫자가 아니면 본문");
            Assert.AreEqual(0f, list.Single(e => e.id == "G09").seconds, "없으면 패널 기본");
        }

        [Test] public void PanelSettingsParseClampAndSkipBadLines()
        {
            var s = WaitingRoomTutorialPanelSettings.Parse("# 설명\n위치 X = 50 # 왼쪽\n너비 = 900\n표시시간 = abc\n본문크기 = 20\n제목크기 = 0\n모르는값 = 3\n간격=1.5");
            Assert.AreEqual(50f, s.X);
            Assert.AreEqual(300f, s.Y, "없으면 기본");
            Assert.AreEqual(480f, s.Width, "툴팁 너비 한도");
            Assert.AreEqual(8f, s.Seconds, "숫자가 아니면 기본");
            Assert.AreEqual(20f, s.BodySize);
            Assert.AreEqual(0f, s.TitleSize, "0이면 패널 기본");
            Assert.AreEqual(1.5f, s.Gap);
        }

        [Test] public void ShippedPanelSettingsFileReadsDefaults()
        {
            var s = WaitingRoomTutorialPanelSettings.Parse(System.IO.File.ReadAllText("Assets/Resources/Settings/WaitingRoom/WaitingRoomTutorialPanel.txt"));
            Assert.AreEqual(36f, s.X); Assert.AreEqual(300f, s.Y); Assert.AreEqual(440f, s.Width);
            Assert.AreEqual(8f, s.Seconds); Assert.AreEqual(.6f, s.Gap, 1e-4f);
        }
    }
}
