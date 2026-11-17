using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed partial class ExitSawTests
    {
        const string CableType = "CurtainCall.EscapeRoute.EscapeCuttableCable";
        Component Strand(Vector3 center, bool required = true)
        {
            var root = Root("Projectile cable"); root.transform.position = center;
            var strand = root.AddComponent(T(CableType));
            Set(strand, "localPoints", new[] { Vector3.down, Vector3.up }); Set(strand, "curveSubdivisions", 1);
            Set(strand, "radius", .05f); Set(strand, "requiredForExit", required);
            root.SetActive(true); Call(strand, "Rebuild"); return strand;
        }
        void SetCables(params Component[] strands)
        {
            Array array = Array.CreateInstance(T(CableType), strands.Length);
            for (int i = 0; i < strands.Length; i++) array.SetValue(strands[i], i);
            Set(saw, "cables", array);
        }
        object Shot(uint id, uint epoch, Vector3 position, Vector3 direction, float age = 0)
        {
            var type = T(SawType).GetNestedType("Shot", BindingFlags.NonPublic);
            object shot = Activator.CreateInstance(type, true);
            Set(shot, "Id", id); Set(shot, "Epoch", epoch); Set(shot, "Position", position); Set(shot, "Direction", direction); Set(shot, "Age", age);
            return shot;
        }
        Array ShotArray(params object[] values)
        {
            var result = Array.CreateInstance(T(SawType).GetNestedType("Shot", BindingFlags.NonPublic), values.Length);
            for (int i = 0; i < values.Length; i++) result.SetValue(values[i], i);
            return result;
        }
        bool ProjectileSnapshot(uint version, uint owner, ulong actor, uint mask, Vector3[] points, Array entries,
            uint course = 0, int token = 0)
            => (bool)Call(saw, "ApplySnapshot", version, owner, actor, device.position, device.rotation, 0f, (byte)0,
                token, mask, points, entries, 0f, course);
        Vector3 ShotStart => Origin + Vector3.up * 1.45f;

        [Test]
        public void HeldLauncherFollowsThroughFinalBayRailButPlayerStillStopsAtIt()
        {
            Fixture(true);
            Set(saw, "fireWithoutCables", true);
            var player = Player(41);
            Own(41);
            Set(player, "IsOwner", true);
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            Set(mover, "Simulated", true);
            Isolate("CurtainCall.Cameras.CameraViewDirector");
            var load = device.gameObject.AddComponent<BoxCollider>();
            load.center = new Vector3(0, .55f, 0);
            load.size = new Vector3(.735f, 1.05f, .805f);
            Set(saw, "loadCollider", load);
            Physics.SyncTransforms();
            Assert.IsTrue((bool)Call(saw, "FollowCarrier", player, false));

            // Use the finale rail's authored length, height and depth.
            var rail = Root("Finale rail", true).AddComponent<BoxCollider>();
            rail.size = new Vector3(29.4f, .15f, .14f);
            rail.transform.position = Origin + new Vector3(0, .8f, 1.7f);
            player.transform.position += new Vector3(.35f, 0, .3f);
            Physics.SyncTransforms();
            Assert.IsTrue((bool)Call(saw, "FollowCarrier", player, false));
            Assert.That(device.position.x - Origin.x, Is.EqualTo(.35f).Within(.02f));
            Assert.That(player.transform.position.z - Origin.z, Is.EqualTo(.3f).Within(.02f));
            Physics.SyncTransforms();
            Assert.That(load.bounds.Intersects(rail.bounds), Is.True);

            var body = (CharacterController)Get(mover, "Body");
            body.Move(Vector3.forward * 3f);
            Assert.That(player.transform.position.z, Is.LessThan(rail.bounds.min.z));
            var feetBeforeFollow = player.transform.position;
            Physics.SyncTransforms();
            Assert.IsTrue((bool)Call(saw, "FollowCarrier", player, false));
            Assert.That(player.transform.position, Is.EqualTo(feetBeforeFollow));
            Assert.That(load.enabled, Is.True);
        }

        [Test]
        public void AClearMuzzleBeyondAThinWallCannotSpawnABladeThroughIt()
        {
            Fixture(true);
            var player = Player(41);
            Own(41);
            var strand = Strand(ShotStart + Vector3.forward * 4f);
            SetCables(strand);
            var wall = Root("Thin wall between bearer and muzzle", true).AddComponent<BoxCollider>();
            wall.size = new Vector3(4f, 4f, .04f);
            wall.transform.position = Origin + Vector3.forward * .9f;
            Physics.SyncTransforms();

            object[] sweep = { blade.position, Vector3.forward, 0f, 0f };
            Assert.IsFalse((bool)Call(saw, "ObstructionDistance", sweep), "The muzzle itself is beyond the wall and clear.");
            Assert.That(Call(saw, "TryFire", player), Is.False);
            Assert.That(Get(saw, "ActiveProjectileCount"), Is.EqualTo(0));
            Assert.That(Get(strand, "IsCut"), Is.False);

            wall.enabled = false;
            Physics.SyncTransforms();
            Assert.That(Call(saw, "TryFire", player), Is.True);
            Call(saw, "StepProjectiles", .2f, true);
            Assert.That(Get(strand, "IsCut"), Is.True);
        }

        [Test]
        public void CompletelyObstructedShotDoesNotReportFiringAndCanRetry()
        {
            Fixture(true);
            var player = Player(41);
            Own(41);
            SetCables(Strand(ShotStart + Vector3.forward * 4));
            var wall = Root("Blocked muzzle and fallback", true).AddComponent<BoxCollider>();
            wall.transform.position = blade.position;
            wall.size = new Vector3(2f, .5f, 3f);
            Physics.SyncTransforms();
            Assert.That(Call(saw, "TryFire", player), Is.False);
            Assert.That(Get(saw, "ActiveProjectileCount"), Is.EqualTo(0));
            Assert.That(Get(saw, "recoilTime"), Is.EqualTo(0f));
            wall.enabled = false;
            Physics.SyncTransforms();
            Assert.That(Call(saw, "TryFire", player), Is.True);
        }

        [Test]
        public void ManyLooseLimbCollidersDoNotAbsorbBladeOrHideARealWall()
        {
            Fixture(true);
            var strand = Strand(ShotStart + Vector3.forward * 4);
            SetCables(strand);
            for (int i = 0; i < 68; i++)
            {
                var limb = Root("Detached limb", true);
                limb.transform.position = ShotStart + Vector3.forward * (2f + i * .002f);
                limb.AddComponent(T("CurtainCall.Damage.DismemberedLimb"));
                limb.AddComponent<BoxCollider>().size = Vector3.one * .1f;
            }
            var wall = Root("Solid wall past the cable", true).AddComponent<BoxCollider>();
            wall.transform.position = ShotStart + Vector3.forward * 6f;
            wall.size = new Vector3(4, 4, .2f);
            var shots = (IList)Get(saw, "shots");
            Own(41);
            shots.Add(Shot(1, 1, ShotStart, Vector3.forward));
            Physics.SyncTransforms();
            Call(saw, "StepProjectiles", .5f, true);
            Assert.That(Get(strand, "IsCut"), Is.True);
            Assert.That(shots.Count, Is.Zero, "The real wall still stops the blade after it cuts.");
        }

        [Test]
        public void LauncherIgnoresCollidersFromTheActualPlayerCutPath()
        {
            Fixture();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Prefabs/Characters/Players/Models/Rabbit/Rabbit.prefab");
            Assert.IsNotNull(prefab);
            var modelRoot = UnityEngine.Object.Instantiate(prefab);
            roots.Add(modelRoot);
            modelRoot.transform.position = Origin;
            var model = modelRoot.GetComponent(T("CurtainCall.Player.HumanoidModel"));
            var animator = (Animator)Get(model, "Animator");
            animator.Rebind();
            animator.Update(0f);
            var piece = Call(model, "BakePiece", 0, null);
            var fragment = (GameObject)Get(piece, "Object");
            var mesh = (Mesh)Get(piece, "Mesh");
            try
            {
                fragment.SetActive(true);
                var colliders = fragment.GetComponentsInChildren<Collider>();
                Assert.That(colliders.Length, Is.EqualTo(2));
                foreach (var collider in colliders)
                    Assert.That(Call(saw, "ShotObstacle", collider), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fragment);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void RecoveryFindsSupportedSpaceWhenAnotherLauncherOccupiesTheAnchor()
        {
            Fixture(true);
            var load = device.gameObject.AddComponent<BoxCollider>();
            load.center = Vector3.up * .45f;
            load.size = Vector3.one * .6f;
            Set(saw, "loadCollider", load);
            var anchor = device.position;
            var blocker = Root("Another launcher on the anchor", true).AddComponent<BoxCollider>();
            blocker.size = load.size;
            blocker.transform.position = anchor + load.center;
            device.position += Vector3.forward * 2;
            Physics.SyncTransforms();
            Call(saw, "Recover");
            Physics.SyncTransforms();
            Assert.That(device.position.y, Is.EqualTo(anchor.y));
            Assert.That(Vector3.Distance(device.position, anchor), Is.InRange(.7f, 1.7f));
            Assert.That(load.bounds.Intersects(blocker.bounds), Is.False);
        }

        [Test]
        public void CosmeticPursuerChunksDoNotAbsorbShotsButWallsDo()
        {
            Fixture();
            var chunk = Root("Severed cosmetic chunk", true);
            var collider = chunk.AddComponent<BoxCollider>();
            Assert.That(Call(saw, "ShotObstacle", collider), Is.True);
            chunk.AddComponent(T("CurtainCall.EscapeRoute.EscapePursuerDebris"));
            Assert.That(Call(saw, "ShotObstacle", collider), Is.False);
        }

        [Test]
        public void HeldMachineDoesNotLimitLargeMovementStepsAndRestoresPairsOnDrop()
        {
            Fixture(true);var player=Player(41);var mover=player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            var body=(CharacterController)Get(mover,"Body");
            var load=device.gameObject.AddComponent<BoxCollider>();load.center=new Vector3(0,.78f,0);load.size=new Vector3(1.05f,1.5f,1.15f);
            Set(saw,"loadCollider",load);
            float Walk(bool carried)
            {
                Call(saw,"RefreshCarrierCollision",(object)null);
                body.enabled=false;player.transform.position=Origin;body.enabled=true;device.position=Origin+Vector3.forward*1.1f;
                if(carried)Call(saw,"RefreshCarrierCollision",player);
                Physics.SyncTransforms();
                for(int i=0;i<8;i++)
                {
                    body.Move(Vector3.forward*.3f+Vector3.down*.01f);
                    device.position=player.transform.position+Vector3.forward*1.1f;Physics.SyncTransforms();
                }
                return player.transform.position.z-Origin.z;
            }
            float oldDistance=Walk(false),heldDistance=Walk(true);
            Assert.That(oldDistance,Is.LessThan(1.8f),"The previous own-load collision must reproduce before the fix.");
            Assert.That(heldDistance,Is.EqualTo(2.4f).Within(.03f));
            Assert.That(Physics.GetIgnoreCollision(body,load),Is.True);
            var other=Player(42);other.transform.position=Origin+Vector3.right*4;
            var otherBody=(CharacterController)Get(other.GetComponent(T("CurtainCall.Player.PlayerMover")),"Body");
            Assert.That(Physics.GetIgnoreCollision(otherBody,load),Is.False,"Other players still collide with the carried machine.");
            var wall=Root("Blocking world wall",true).AddComponent<BoxCollider>();wall.transform.position=Origin+new Vector3(0,1,3.5f);wall.size=new Vector3(4,2,.2f);
            Physics.SyncTransforms();float before=player.transform.position.z;body.Move(Vector3.forward*2);
            Assert.That(player.transform.position.z-before,Is.LessThan(1),"World walls must still block the carrier.");
            body.enabled=false;body.enabled=true;Call(saw,"RefreshCarrierCollision",player);
            Assert.That(Physics.GetIgnoreCollision(body,load),Is.True,"Re-enabling the controller must renew the pair.");
            Call(saw,"RefreshCarrierCollision",(object)null);Assert.That(Physics.GetIgnoreCollision(body,load),Is.False);
        }

        [Test]
        public void CarrierChangeAndDisablePreservePreviouslyIgnoredPairs()
        {
            Fixture(true);var first=Player(41);var second=Player(42);second.transform.position=Origin+Vector3.right*4;
            var one=(CharacterController)Get(first.GetComponent(T("CurtainCall.Player.PlayerMover")),"Body");
            var two=(CharacterController)Get(second.GetComponent(T("CurtainCall.Player.PlayerMover")),"Body");
            var load=device.gameObject.AddComponent<BoxCollider>();Physics.IgnoreCollision(one,load,true);
            Call(saw,"RefreshCarrierCollision",first);Call(saw,"RefreshCarrierCollision",second);
            Assert.That(Physics.GetIgnoreCollision(one,load),Is.True,"An earlier owner's pre-existing rule is preserved.");
            Assert.That(Physics.GetIgnoreCollision(two,load),Is.True);
            Call(saw,"OnDisable");Assert.That(Physics.GetIgnoreCollision(two,load),Is.False);
            Assert.That(Physics.GetIgnoreCollision(one,load),Is.True);
        }

        [Test]
        public void LaunchClearsLegacyFireFlashAndKeepsMechanicalRecoil()
        {
            Fixture();var flash=Root("Legacy launch fire",true).AddComponent<ParticleSystem>();
            var main=flash.main;main.playOnAwake=false;flash.Play();Set(saw,"launchFlash",flash);
            Call(saw,"EmitLaunch");Assert.That(flash.isPlaying,Is.False);Assert.That(flash.particleCount,Is.Zero);
            Assert.That((float)Get(saw,"recoilTime"),Is.EqualTo(.15f));
        }

        [Test]
        public void HeldLauncherNeverFiresAutomaticallyAndFiresForEveryClickWithoutCooldown()
        {
            Fixture(true); var player = Player(41); Own(41);
            SetCables(Strand(ShotStart + Vector3.forward * 3)); Physics.SyncTransforms();
            Call(saw, "HostStep", 30f);
            Assert.That(Get(saw, "ActiveProjectileCount"), Is.EqualTo(0));
            Assert.That(Call(saw, "TryFire", player), Is.True);
            Assert.That(Get(saw, "ActiveProjectileCount"), Is.EqualTo(1));
            for(int click=0;click<23;click++) Assert.That(Call(saw, "TryFire", player), Is.True);
            Assert.That(Get(saw,"ActiveProjectileCount"),Is.EqualTo(24));
            Call(saw, "HostStep", 30f);
            Assert.That(Get(saw, "ActiveProjectileCount"), Is.EqualTo(0), "Waiting never queues another shot.");
            Assert.That(Call(saw, "TryFire", player), Is.True);
        }
        [Test]
        public void ManualFireAllowsJumpingAndRejectsABlockedPoseOutsideTheCarriersHand()
        {
            Fixture(true); var player = Player(41); Own(41);
            SetCables(Strand(ShotStart + Vector3.forward * 3)); Physics.SyncTransforms();
            var other = Player(42);
            Assert.That(Call(saw, "TryFire", other), Is.False);
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            Set(mover, "IsAirborne", true);
            player.transform.position = Origin + Vector3.up;
            device.position += Vector3.up;
            Physics.SyncTransforms();
            Assert.That(Call(saw, "TryFire", player), Is.True, "A normal jump must allow manual fire.");
            Set(saw, "shotWait", 0f);
            device.position -= Vector3.up;
            Set(saw, "carryBlocked", true);
            player.transform.position = Origin + Vector3.right;
            Physics.SyncTransforms();
            Assert.That(Call(saw, "TryFire", player), Is.False, "A blocked pose outside the carrier's hand cannot fire.");
            player.transform.position = Origin;
            Physics.SyncTransforms();
            Assert.That(Call(saw, "TryFire", player), Is.True, "The host can use the held pose after the owner has touched down.");
        }

        [TestCase(false, 0)] [TestCase(true, 0)] [TestCase(false, 3)] [TestCase(true, 3)]
        [Category("EscapeNovember16Recovery")]
        public void InjuredAndRemoteCarriersCanFireWhileAirborne(bool local, int lostCount)
        {
            Fixture(true); var player=Player(41); Own(41);
            SetCables(Strand(ShotStart+Vector3.forward*3));
            Set(player,"IsOwner",local);
            var mover=player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            Set(mover,"IsAirborne",true); Set(mover,"Simulated",local);
            var parts=Get(player,"lostParts");
            Set(parts,"Value",Enum.ToObject(T("CurtainCall.Player.BodyPart"),(1<<lostCount)-1));
            player.transform.position+=Vector3.up; device.position+=Vector3.up;
            Physics.SyncTransforms();
            Assert.That(Call(saw,"TryFire",player),Is.True);
            Assert.That(Call(saw,"TryFire",player),Is.True,"A second click in the same jump also fires.");
            Set(saw,"shotWait",0f);
            Set(Get(player,"state"),"Value",Enum.Parse(T("CurtainCall.Network.PlayerSync.PlayerState"),"Retired"));
            Assert.That(Call(saw,"TryFire",player),Is.False,"A dead carrier cannot fire.");
        }

        [Test, Category("EscapeNovember16Recovery")]
        public void BlockedHeldSawDoesNotUndoTheCarriersFallHeight()
        {
            var carrier=CarriedSawBeforeIntrusion(out var load);
            Set(carrier,"IsOwner",true);
            var mover=carrier.GetComponent(T("CurtainCall.Player.PlayerMover"));
            Set(mover,"Simulated",true);
            Isolate("CurtainCall.Cameras.CameraViewDirector");
            // The body falls below a ledge while the forward-held machine catches that ledge.
            foreach(var item in roots) if(item.name=="Actual support") item.SetActive(false);
            var edge=Root("Ledge blocking the carried saw",true).AddComponent<BoxCollider>();
            edge.transform.position=device.position+Vector3.down*.1f;
            edge.size=new Vector3(1.2f,.1f,1.2f);
            carrier.transform.position-=Vector3.up*.7f;
            float fallenY=carrier.transform.position.y;
            Physics.SyncTransforms();
            Assert.That(Call(saw,"FollowCarrier",carrier,false),Is.False);
            Assert.That(carrier.transform.position.y,Is.EqualTo(fallenY).Within(.02f),
                "Load collision must never lift the falling player back to lastSafeFeet.");
        }
        [Test]
        public void ProjectileCutsMultipleStrandsAlongPathButStopsBeforeWall()
        {
            Fixture(true); Own(41);
            var first = Strand(ShotStart + Vector3.forward * 2);
            var second = Strand(ShotStart + Vector3.forward * 3);
            var behind = Strand(ShotStart + Vector3.forward * 5);
            SetCables(first, second, behind);
            var wall = Root("Projectile obstruction", true).AddComponent<BoxCollider>();
            wall.transform.position = ShotStart + Vector3.forward * 4; wall.size = new Vector3(4, 4, .2f);
            ((IList)Get(saw, "shots")).Add(Shot(1, 1, ShotStart, Vector3.forward)); Physics.SyncTransforms();
            Call(saw, "StepProjectiles", .5f, true);
            Assert.That(Get(first, "IsCut"), Is.True); Assert.That(Get(second, "IsCut"), Is.True);
            Assert.That(Get(behind, "IsCut"), Is.False); Assert.That(Get(saw, "ActiveProjectileCount"), Is.EqualTo(0));
            Assert.That(Get(saw, "DoorOpen"), Is.False);
        }
        [Test]
        public void EdgeFirstProjectileCannotCutStrandBesideItsThinSide()
        {
            Fixture(true); Own(41);
            var centered = Strand(ShotStart + Vector3.forward * 3);
            var beside = Strand(ShotStart + Vector3.forward * 3 + Vector3.up * 1.4f);
            SetCables(centered, beside);
            ((IList)Get(saw, "shots")).Add(Shot(1, 1, ShotStart, Vector3.forward)); Physics.SyncTransforms();
            Call(saw, "StepProjectiles", .25f, true);
            Assert.That(Get(centered, "IsCut"), Is.True); Assert.That(Get(beside, "IsCut"), Is.False);
        }
        [Test]
        public void IndependentLaunchersShareCableCutsWithoutSharingTheirCarrier()
        {
            Fixture(true); Call(saw, "OnEnable"); Own(41);
            var strand = Strand(ShotStart + Vector3.forward * 3); SetCables(strand);
            var second = Root("Second independent saw").AddComponent(T(SawType));
            Set(second, "deviceId", 1); Set(second, "cables", Get(saw, "cables")); second.gameObject.SetActive(true); Call(second, "OnEnable");
            Call(saw, "CutAlong", ShotStart, ShotStart + Vector3.forward * 5, Vector3.up, null);
            Assert.That(Get(saw, "CutCableCount"), Is.EqualTo(1));
            Assert.That(Get(second, "CutCableCount"), Is.EqualTo(1));
            Assert.That(Get(second, "DoorOpen"), Is.True);
            Assert.That(Get(second, "Carrier"), Is.EqualTo(ulong.MaxValue));
            Assert.That(Get(saw, "Carrier"), Is.EqualTo(41UL));
            Assert.That(Get(saw, "RequestName"), Is.Not.EqualTo(Get(second, "RequestName")));
            Call(second, "OnDisable");
        }
        [Test]
        public void OldOwnerProjectileCannotCutAndManualDropClearsFlyingBlades()
        {
            Fixture(true); Player(41); Own(41);
            var strand = Strand(ShotStart + Vector3.forward * 3); SetCables(strand);
            var entries = (IList)Get(saw, "shots"); entries.Add(Shot(1, 0, ShotStart, Vector3.forward));
            Call(saw, "StepProjectiles", .4f, true);
            Assert.That(Get(strand, "IsCut"), Is.False); Assert.That(entries.Count, Is.Zero);
            entries.Add(Shot(2, 1, ShotStart, Vector3.forward));
            Call(saw, "HandleAction", 41UL, (byte)2, 1U, true);
            Assert.That(entries.Count, Is.Zero); Assert.That(Get(saw, "Carrier"), Is.EqualTo(ulong.MaxValue));
        }
        [Test]
        public void EveryRequiredWireMustBeCutButDecorativeStrandDoesNotLockExit()
        {
            Fixture(); var a = Strand(ShotStart + Vector3.forward * 2); var b = Strand(ShotStart + Vector3.forward * 4);
            var optional = Strand(ShotStart + Vector3.right * 4, false); SetCables(a, b, optional);
            var points = new Vector3[32]; points[0] = a.transform.position; points[1] = b.transform.position;
            Assert.That(ProjectileSnapshot(1, 0, ulong.MaxValue, 1, points, ShotArray()), Is.True);
            Assert.That(Get(saw, "DoorOpen"), Is.False); Assert.That(Get(saw, "RequiredCableCount"), Is.EqualTo(2));
            Assert.That(ProjectileSnapshot(2, 0, ulong.MaxValue, 3, points, ShotArray()), Is.True);
            Assert.That(Get(saw, "DoorOpen"), Is.True); Assert.That(Get(saw, "CutCableCount"), Is.EqualTo(2));
            Assert.That(Get(optional, "IsCut"), Is.False);
        }
        [Test]
        public void CutMaskPersistsAcrossDropAndRegrabAndOnlyNewCourseCanRestoreWires()
        {
            Fixture(); var strand = Strand(ShotStart + Vector3.forward * 3); SetCables(strand);
            var points = new Vector3[32]; points[0] = strand.transform.position;
            Assert.That(ProjectileSnapshot(5, 1, 41, 1, points, ShotArray()), Is.True);
            Assert.That(ProjectileSnapshot(6, 2, ulong.MaxValue, 0, points, ShotArray()), Is.False);
            Assert.That(ProjectileSnapshot(6, 2, ulong.MaxValue, 1, points, ShotArray()), Is.True);
            Assert.That(ProjectileSnapshot(7, 3, 42, 0, points, ShotArray()), Is.False);
            Assert.That(ProjectileSnapshot(7, 3, 42, 1, points, ShotArray()), Is.True);
            Assert.That(ProjectileSnapshot(8, 4, ulong.MaxValue, 0, points, ShotArray(), course: 1), Is.True);
            Assert.That(Get(strand, "IsCut"), Is.False);
            Assert.That(ProjectileSnapshot(9, 4, ulong.MaxValue, 1, points, ShotArray(), course: 0), Is.False);
        }
        [Test]
        public void LateJoinRestoresActualCutPointAndFlyingBladeWithoutLaunchReplay()
        {
            Fixture(); var strand = Strand(ShotStart + Vector3.forward * 3); SetCables(strand);
            var points = new Vector3[32]; points[0] = strand.transform.position + Vector3.up * .4f;
            Assert.That(ProjectileSnapshot(5, 1, 41, 1, points, ShotArray(Shot(1, 1, ShotStart, Vector3.forward, .2f))), Is.True);
            Assert.That(Vector3.Distance((Vector3)Get(strand, "CutPoint"), points[0]), Is.LessThan(.005f));
            Assert.That(Get(saw, "ActiveProjectileCount"), Is.EqualTo(1)); Assert.That(Get(saw, "recoilTime"), Is.EqualTo(0f));
            Assert.That(ProjectileSnapshot(6, 1, 41, 1, points, ShotArray(Shot(2, 1, ShotStart, Vector3.forward, .1f))), Is.True);
            Assert.That((float)Get(saw, "recoilTime"), Is.GreaterThan(0));
        }
        [Test]
        public void InvalidShotEpochDuplicateIdsAndUnconfiguredCutBitsAreAtomic()
        {
            Fixture(); var strand = Strand(ShotStart + Vector3.forward * 3); SetCables(strand);
            var points = new Vector3[32]; points[0] = strand.transform.position;
            Assert.That(ProjectileSnapshot(5, 1, 41, 0, points, ShotArray()), Is.True);
            Assert.That(ProjectileSnapshot(99, 1, 41, 0, points, ShotArray(Shot(1, 0, ShotStart, Vector3.forward))), Is.False);
            Assert.That(ProjectileSnapshot(99, 1, 41, 0, points, ShotArray(Shot(1, 1, ShotStart, Vector3.forward), Shot(1, 1, ShotStart, Vector3.forward))), Is.False);
            Assert.That(ProjectileSnapshot(99, 1, 41, 2, points, ShotArray()), Is.False);
            var tooMany = new object[65]; for (int i = 0; i < tooMany.Length; i++) tooMany[i] = Shot((uint)i + 1, 1, ShotStart, Vector3.forward);
            Assert.That(ProjectileSnapshot(99, 1, 41, 0, points, ShotArray(tooMany)), Is.False);
            Assert.That(Get(saw, "stateRevision"), Is.EqualTo(5U)); Assert.That(Get(strand, "IsCut"), Is.False);
            var volley = new object[24]; for (int i = 0; i < volley.Length; i++) volley[i] = Shot((uint)i + 1, 1, ShotStart, Vector3.forward);
            Assert.That(ProjectileSnapshot(6, 1, 41, 0, points, ShotArray(volley)), Is.True);
            Assert.That(Get(saw,"ActiveProjectileCount"),Is.EqualTo(24));
        }
        [Test]
        public void ResetBarrierRejectsStaleProjectilesAndMalformedReplyDoesNotReleaseIt()
        {
            Fixture(); var strand = Strand(ShotStart + Vector3.forward * 3); SetCables(strand);
            var points = new Vector3[32];
            ProjectileSnapshot(5, 1, 41, 0, points, ShotArray());
            int token = (int)Call(Get(saw, "resetBarrier"), "Begin"); Set(saw, "synchronized", false);
            Assert.That(ProjectileSnapshot(6, 1, 41, 0, points, ShotArray(Shot(1, 1, ShotStart, Vector3.forward))), Is.False);
            Assert.That(ProjectileSnapshot(6, 2, ulong.MaxValue, 0, points, ShotArray(Shot(1, 1, ShotStart, Vector3.forward)), course: 1, token: token), Is.False);
            Assert.That(Get(Get(saw, "resetBarrier"), "Waiting"), Is.True);
            Assert.That(ProjectileSnapshot(6, 2, ulong.MaxValue, 0, points, ShotArray(), course: 1, token: token), Is.True);
            Assert.That(Get(saw, "ActiveProjectileCount"), Is.EqualTo(0)); Assert.That(Get(saw, "recoilTime"), Is.EqualTo(0f));
        }
    }
}
