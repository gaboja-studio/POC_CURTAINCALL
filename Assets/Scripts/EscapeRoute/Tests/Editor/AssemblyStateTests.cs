using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class AssemblyStateTests
    {
        object state;
        Type type;
        [SetUp] public void Setup() { type=Find("EscapeAssemblyState"); state=Activator.CreateInstance(type); }
        static Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("CurtainCall.EscapeRoute."+name)).First(t=>t!=null);
        object Get(string name)=>type.GetProperty(name).GetValue(state);
        void Tick(int people,int eligible,int required=4)=>type.GetMethod("Tick").Invoke(state,new object[]{people,eligible,required});
        bool Depart()=>(bool)type.GetMethod("Depart").Invoke(state,null);
        string Status=>Get("Status").ToString();

        [Test] public void PartialDeparturesKeepGatheringWithoutADeadline()
        {
            Tick(1,1); Tick(3,3); Tick(1,1);
            for(int i=0;i<10000;i++) Tick(1,1);
            Assert.That(Status,Is.EqualTo("Gathering"));
            Assert.That(type.GetProperty("Deadline"),Is.Null);
            Tick(4,4); Assert.That(Depart(),Is.True);
        }
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void AllCurrentPlayersCanDepart(int count)
        { Tick(count,count,count); Assert.That(Depart(),Is.True); }
        [Test] public void JoiningRaisesTheRequiredCountBeforeDeparture()
        { Tick(1,1,2); Tick(2,2,3); Assert.That(Depart(),Is.False); Tick(3,3,3); Assert.That(Depart(),Is.True); }
        [Test] public void DisconnectingLowersTheRequiredCountBeforeDeparture()
        { Tick(2,2,3); Assert.That(Depart(),Is.False); Tick(2,2,2); Assert.That(Depart(),Is.True); }
        [Test] public void ZeroConnectedPlayersCannotDepart()
        { Tick(0,0,0); Assert.That(Depart(),Is.False); }
        [Test] public void EmptyRoomClearsTheCount()
        { Tick(3,3); Tick(0,0); Assert.That(Status,Is.EqualTo("Empty")); Assert.That(Get("Eligible"),Is.EqualTo(0)); }
        [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void FewerThanFourCannotDepart(int count)
        { Tick(count,count); Assert.That(Depart(),Is.False); }
        [Test] public void FourCanDepartImmediately()
        { Tick(4,4); Assert.That(Depart(),Is.True); Assert.That(Status,Is.EqualTo("Departed")); }
        [Test] public void ABodyInTheRoomDoesNotCountAsAnEligibleParticipant()
        { Tick(4,3); Assert.That(Depart(),Is.False); }
        [Test] public void DepartureIsLatchedAndCannotBeRepeatedOrResetByLeaving()
        { Tick(4,4); Assert.That(Depart(),Is.True); Tick(0,0); Assert.That(Depart(),Is.False); Assert.That(Status,Is.EqualTo("Departed")); }
        [Test] public void RelocatedFallRegionStillUsesTheAuthoredFacilityCoordinates()
        {
            var frame=new GameObject("Facility frame"); frame.SetActive(false);
            var node=new GameObject("Fall bounds"); node.transform.SetParent(frame.transform,false);
            try
            {
                frame.transform.SetPositionAndRotation(new Vector3(-40,-16,108.8f),Quaternion.Euler(0,180,0));
                var fall=node.AddComponent(Find("EscapeFallVoid"));
                fall.GetType().GetField("coordinateSpace",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(fall,frame.transform);
                bool Contains(Vector3 p)=>(bool)fall.GetType().GetMethod("Contains").Invoke(fall,new object[]{p});
                Assert.That(Contains(frame.transform.TransformPoint(new Vector3(67,-31,-17))),Is.True);
                Assert.That(Contains(frame.transform.TransformPoint(new Vector3(67,-18,-17))),Is.False);
                Assert.That(Contains(new Vector3(-17.6f,-10,93.35f)),Is.False,"Waiting-room assembly must never become an A fall zone.");
            } finally { Object.DestroyImmediate(frame); }
        }
        [Test] public void CampaignActivationNeverReenablesTheStandaloneCameraRoot()
        {
            var root=new GameObject("Campaign content"); root.SetActive(false);
            var body=new GameObject("Geometry"); body.transform.SetParent(root.transform);
            var camera=new GameObject("Standalone camera"); camera.transform.SetParent(root.transform); camera.SetActive(false);
            try
            {
                var component=root.AddComponent(Find("EscapeCampaignContent")); var t=component.GetType();
                void Field(string name,object value)=>t.GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(component,value);
                Field("content",body); Field("activeRoots",new[]{body,camera}); Field("campaignDisabledObjects",new[]{camera});
                t.GetMethod("SetActive",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(component,new object[]{true});
                Assert.That(body.activeSelf,Is.True); Assert.That(camera.activeSelf,Is.False);
            } finally { Object.DestroyImmediate(root); }
        }
        [Test] public void SavedFacilityPlacementMovesRootsAndWorldLinesOnlyOnce()
        {
            var frame=new GameObject("Saved frame"); frame.SetActive(false);
            var geometry=new GameObject("Authored geometry"); geometry.SetActive(false);
            geometry.transform.position=new Vector3(12,3,8);
            var line=geometry.AddComponent<LineRenderer>(); line.useWorldSpace=true; line.positionCount=2;
            line.SetPositions(new[]{new Vector3(12,3,8),new Vector3(15,3,8)});
            try
            {
                var component=frame.AddComponent(Find("EscapeCampaignContent")); var t=component.GetType();
                void Field(string name,object value)=>t.GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(component,value);
                Field("sceneRoots",new[]{geometry}); Field("campaignPosition",new Vector3(-100,-16,118.3f)); Field("campaignEuler",new Vector3(0,180,0));
                var place=t.GetMethod("PlaceCampaign"); place.Invoke(component,null);
                Assert.That(geometry.transform.parent,Is.EqualTo(frame.transform));
                var expected=frame.transform.TransformPoint(new Vector3(12,3,8));
                Assert.That(Vector3.Distance(geometry.transform.position,expected),Is.LessThan(.001f));
                Assert.That(Vector3.Distance(line.GetPosition(0),expected),Is.LessThan(.001f));
                var position=geometry.transform.position;var point=line.GetPosition(1);
                place.Invoke(component,null);
                Assert.That(geometry.transform.position,Is.EqualTo(position));
                Assert.That(line.GetPosition(1),Is.EqualTo(point));
            } finally { Object.DestroyImmediate(geometry); Object.DestroyImmediate(frame); }
        }
        [Test] public void RetirementRemovesOnlyThatPlayersItemsAndAdvancesTheRevisionOnce()
        {
            Type Game(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("CurtainCall.WaitingRoom."+name)).First(t=>t!=null);
            var bookType=Game("ItemCarryBook");var itemType=Game("PropItem");var book=Activator.CreateInstance(bookType,new object[]{20});
            object Item(int id)=>Activator.CreateInstance(itemType,new object[]{id,"rescue-gloves","Gloves",3});
            bookType.GetMethod("Take").Invoke(book,new object[]{1ul,2,Item(1)});bookType.GetMethod("Take").Invoke(book,new object[]{2ul,2,Item(2)});
            long revision=(long)bookType.GetProperty("Revision").GetValue(book);
            Assert.That(bookType.GetMethod("Retire").Invoke(book,new object[]{1ul}),Is.True);
            Assert.That(bookType.GetMethod("Count").Invoke(book,new object[]{1ul}),Is.EqualTo(0));
            Assert.That(bookType.GetMethod("Count").Invoke(book,new object[]{2ul}),Is.EqualTo(1));
            Assert.That(bookType.GetProperty("Revision").GetValue(book),Is.EqualTo(revision+1));
            Assert.That(bookType.GetMethod("Retire").Invoke(book,new object[]{1ul}),Is.False);
            Assert.That(bookType.GetProperty("Revision").GetValue(book),Is.EqualTo(revision+1));
        }
    }
}
