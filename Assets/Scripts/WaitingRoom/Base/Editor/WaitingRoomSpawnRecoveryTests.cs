using System.Collections.Generic;
using System.Reflection;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.EditorTools
{
    [Category("MorningRevivalPlacement")]
    public sealed class WaitingRoomSpawnRecoveryTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        readonly List<GameObject> objects = new();
        CampaignRun previousRun, run;
        NetworkPlayer previousLocal, player;
        WaitingRoomSpawn spawn;
        object previousPoints;
        object previousRehearsal, previousCannon, practice;
        NetworkPlayer previousPlacedPlayer;
        int previousPlacedScene;
        Transform destination;
        static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        GameObject Make(string name) { var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave }; objects.Add(go); return go; }

        [SetUp]
        public void SetUp()
        {
            previousRun = CampaignRun.Instance; previousLocal = NetworkPlayer.Local;
            previousPlacedPlayer = WaitingRoomSpawn.LastPlacedPlayer; previousPlacedScene = WaitingRoomSpawn.LastPlacedScene;
            previousPoints = typeof(PlayerSpawnPoints).GetField("current", Static).GetValue(null);
            previousRehearsal = typeof(WaitingRoomRehearsal).GetField("instance", Static).GetValue(null);
            previousCannon = typeof(WaitingRoomCannonRehearsal).GetField("instance", Static).GetValue(null);
            typeof(WaitingRoomRehearsal).GetField("instance", Static).SetValue(null, null);
            typeof(WaitingRoomCannonRehearsal).GetField("instance", Static).SetValue(null, null);
            run = Make("Morning placement campaign").AddComponent<CampaignRun>();
            typeof(CampaignRun).GetProperty("Instance").SetValue(null, run);
            var go = Make("Morning placement player");
            var mover = go.AddComponent<PlayerMover>(); Call(mover, "Awake");
            player = go.AddComponent<NetworkPlayer>(); Set(player, "mover", mover);
            typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(player, true);
            typeof(NetworkBehaviour).GetProperty("IsServer").SetValue(player, true);
            ((NetworkVariable<int>)Get(player, "slot")).Value = 0;
            ((NetworkVariable<BodyPart>)Get(player, "lostParts")).Value = PlayerCondition.OneArmParts;
            typeof(NetworkPlayer).GetProperty("Local").SetValue(null, player);
            destination = Make("Existing lobby slot").transform; destination.position = new Vector3(22, 3, 11);
            var points = Make("Existing lobby points").AddComponent<PlayerSpawnPoints>(); Set(points, "points", new[] { destination });
            typeof(PlayerSpawnPoints).GetField("current", Static).SetValue(null, points);
            spawn = Make("Morning placement").AddComponent<WaitingRoomSpawn>();
            go.transform.position = new Vector3(70, -12, 3);
        }

        [TearDown]
        public void TearDown()
        {
            typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(player, false);
            for (int i = objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(objects[i]);
            objects.Clear();
            typeof(CampaignRun).GetProperty("Instance").SetValue(null, previousRun);
            typeof(NetworkPlayer).GetProperty("Local").SetValue(null, previousLocal);
            typeof(PlayerSpawnPoints).GetField("current", Static).SetValue(null, previousPoints);
            typeof(WaitingRoomRehearsal).GetField("instance", Static).SetValue(null, previousRehearsal);
            typeof(WaitingRoomCannonRehearsal).GetField("instance", Static).SetValue(null, previousCannon);
            practice = null;
            typeof(WaitingRoomSpawn).GetProperty("LastPlacedPlayer").SetValue(null, previousPlacedPlayer);
            typeof(WaitingRoomSpawn).GetProperty("LastPlacedScene").SetValue(null, previousPlacedScene);
        }

        void Step(CampaignPhase phase, int day, int generation = 1) => Set(run, "state", new CampaignSnapshot
        { Phase = phase, Rest = RestKind.Morning, Day = day, Generation = generation });

        [TestCase(true), TestCase(false)]
        public void MorningRevivalUsesExistingLobbySlotRegardlessOfSnapshotOrder(bool revivalFirst)
        {
            Step(revivalFirst ? CampaignPhase.Dawn : CampaignPhase.RestTime, revivalFirst ? 1 : 2);
            ((NetworkVariable<PlayerState>)Get(player, "state")).Value = revivalFirst ? PlayerState.Fallen : PlayerState.Normal;
            Call(spawn, "Revived", player);
            if (revivalFirst)
            {
                Assert.AreNotEqual(destination.position, player.transform.position, "Wait for morning and the Normal state.");
                Step(CampaignPhase.RestTime, 2);
                Call(spawn, "TryPlaceMorningRevival");
                Assert.AreNotEqual(destination.position, player.transform.position);
                ((NetworkVariable<PlayerState>)Get(player, "state")).Value = PlayerState.Normal;
                Call(spawn, "TryPlaceMorningRevival");
            }
            Assert.AreEqual(destination.position, player.transform.position);
            Assert.AreEqual(PlayerCondition.OneArmParts, player.LostParts, "Placement does not restore limbs.");
            player.transform.position += Vector3.right;
            Call(spawn, "TryPlaceMorningRevival");
            Assert.AreEqual(destination.position + Vector3.right, player.transform.position, "One revival places only once.");
        }

        [Test]
        public void PendingMorningPlacementCannotLeakIntoAnotherGeneration()
        {
            Step(CampaignPhase.Dawn, 1);
            Call(spawn, "Revived", player);
            Step(CampaignPhase.RestTime, 2, 2);
            Call(spawn, "TryPlaceMorningRevival");
            Assert.AreNotEqual(destination.position, player.transform.position);
            Assert.IsNull(Get(spawn, "morningRevival"));
        }

        void SetPractice(bool cannon, bool active)
        {
            if (practice == null)
            {
                if (cannon)
                {
                    practice = Make("Cannon practice owns recovery").AddComponent<WaitingRoomCannonRehearsal>();
                    Call(practice, "AwakeNetwork");
                    typeof(WaitingRoomCannonRehearsal).GetField("instance", Static).SetValue(null, practice);
                }
                else
                {
                    practice = Make("Tightrope practice owns recovery").AddComponent<WaitingRoomRehearsal>();
                    typeof(WaitingRoomRehearsal).GetField("instance", Static).SetValue(null, practice);
                }
            }
            if (cannon) Get(practice, "shared").GetType().GetField("active").SetValue(Get(practice, "shared"), active);
            else Set(practice, "active", active);
        }

        [TestCase(true), TestCase(false)]
        public void PracticeCheckpointRecoveryCannotBeMovedToMorningLobby(bool cannon)
        {
            Step(CampaignPhase.RestTime, 2);
            SetPractice(cannon, true);
            Call(spawn, "Revived", player);
            Assert.IsNull(Get(spawn, "morningRevival"), "Practice owns its revival/checkpoint placement.");
            Assert.AreNotEqual(destination.position, player.transform.position);
            SetPractice(cannon, false);
            Step(CampaignPhase.Dawn, 1); Call(spawn, "Revived", player);
            Assert.IsNotNull(Get(spawn, "morningRevival"));
            SetPractice(cannon, true);
            Step(CampaignPhase.RestTime, 2); Call(spawn, "TryPlaceMorningRevival");
            Assert.IsNull(Get(spawn, "morningRevival"), "An already queued morning move cannot override a later practice start.");
            Assert.AreNotEqual(destination.position, player.transform.position);
        }
    }
}
