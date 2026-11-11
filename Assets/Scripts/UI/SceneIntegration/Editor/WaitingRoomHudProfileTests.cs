using System.Linq;
using CurtainCall.WaitingRoom;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration.EditorTools.Tests
{
    public sealed class WaitingRoomHudProfileTests
    {
        [Test] public void ReorderingDoesNotDuplicateOrLoseRequiredIntroSteps()
        {
            CollectionAssert.AreEqual(new[]{"N03","N01","N02","N04"},WaitingRoomHudProfile.Order(new[]{NightIntro.N03,NightIntro.N03,(NightIntro)99}));
        }
        [Test] public void RehearsalCatalogHasDefaultsAndRealFormatting()
        {
            var source=Resources.Load<TextAsset>("Settings/WaitingRoom/WaitingRoomTexts");
            var table=WaitingRoomText.Parse(source.text);
            foreach(var id in SceneIntegration.Editor.WaitingRoomHudProfileEditor.RehearsalKeys)
                Assert.That(table.ContainsKey(id),id);
            Assert.AreEqual("1/2",SceneIntegration.Editor.WaitingRoomHudProfileEditor.RehearsalSample("cannon-rehearsal.vote-body","{skip}/{required}"));
            Assert.AreEqual("E → Enter",RehearsalGuide.Format("{interact} → {action}","E","Enter"));
            Assert.AreEqual("W/S 이동",RehearsalGuide.Format("{forward} 이동","F","Space"));
            Assert.AreEqual("W/S · A/D → Enter · {required}",RehearsalGuide.Format("{move} → {jump} · {required}","E","Enter"));
        }
        [Test] public void CopyPreviewPairsFieldsAndOnlyFillsTokensBoundByThatCaller()
        {
            var keys=SceneIntegration.Editor.WaitingRoomHudProfileEditor.KeysFor("notice.item-broken-title");
            Assert.AreEqual("notice.item-broken",keys.body);
            Assert.AreEqual("은색 열쇠가 파괴되었습니다.",SceneIntegration.Editor.WaitingRoomHudProfileEditor.FormatSample(keys.body,"{item:이} 파괴되었습니다."));
            Assert.AreEqual("2/3",SceneIntegration.Editor.WaitingRoomHudProfileEditor.FormatSample("first-day.quest.gather.body","{inside}/{alive}"));
            Assert.AreEqual("{ready}",SceneIntegration.Editor.WaitingRoomHudProfileEditor.FormatSample("banner.recall-title","{ready}"));
        }
        [Test] public void DesignerStepIsQueuedInItsPhaseAndKeepsHistoryAfterRenaming()
        {
            var profile=Object.Instantiate(WaitingRoomHudProfile.Current);
            try
            {
                var added=new RoomAuthoredStep{id="extra:sample",after="G01",title="수화기",body="[{interact}] 사용",shot=RoomShot.Shop};
                profile.restExtra=new[]{added};
                var seq=profile.Sequence(false);CollectionAssert.AreEqual(new[]{"G01",added.id,"G11"},seq);
                CollectionAssert.DoesNotContain(profile.Sequence(true),added.id);
                var book=profile.BuildBook("");foreach(string id in seq)Assert.IsTrue(book.Enqueue(id));
                book.MarkDone("G01");Assert.IsTrue(book.TryNext(false,out var entry));
                Assert.AreEqual(added.id,entry.id);Assert.AreEqual("수화기",entry.title);book.MarkDone(added.id);
                added.title="상점 소개";var next=profile.BuildBook("");next.Load(book.Serialize());
                Assert.IsTrue(next.IsDone(added.id));Assert.AreEqual("상점 소개",next.Get(added.id).title);
                added.after="";Assert.AreEqual(added.id,profile.Sequence(false)[0]);
            }finally{Object.DestroyImmediate(profile);}
        }
        [Test] public void PerCueOverridesPreserveKeySubstitutionAndBlockedInputPolicy()
        {
            var settings=Object.Instantiate(WaitingRoomHudProfile.Current);
            try
            {
                var row=settings.Cue("G04");row.title="조사";row.body="[{interact}] 내려놓기";row.seconds=7;
                var book=settings.BuildBook("G01 | 기존 | 기존 안내");
                Assert.AreEqual("기존",book.Get("G01").title);
                var item=book.Get("G04");Assert.AreEqual("조사",item.title);Assert.AreEqual(7,item.seconds);Assert.IsTrue(item.whileBlocked);
                Assert.AreEqual("[E] 내려놓기",WaitingRoomTutorialBook.Format(item.body,"E","우클릭"));
                Assert.That(WaitingRoomTutorialBook.Defaults.All(x=>settings.Cue(x.id)!=null));
            }finally{Object.DestroyImmediate(settings);}
        }
    }
}
