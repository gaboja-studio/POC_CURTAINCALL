using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public class HumanCannonPresentationTests
    {
        static readonly HumanCannonCourseSections.Boundaries Layout = new(18, 72, 90, 141.5f, 5);

        [TestCase(3f, 0f, HumanCannonCourseSections.Section.Departure)]
        [TestCase(30f, 12f, HumanCannonCourseSections.Section.First)]
        [TestCase(73f, 0f, HumanCannonCourseSections.Section.First)]
        [TestCase(73f, 6f, HumanCannonCourseSections.Section.Shelter)]
        [TestCase(105f, 6f, HumanCannonCourseSections.Section.Second)]
        [TestCase(150f, 6f, HumanCannonCourseSections.Section.Final)]
        public void GroundCarrierAndAirborneFlyer_UseTheirActualLevel(float z, float height, HumanCannonCourseSections.Section expected)
            => Assert.That(HumanCannonCourseSections.SectionAt(new Vector3(-9, height, z), Layout), Is.EqualTo(expected));

        [Test]
        public void BacktrackingAndRestart_ReturnToEarlierSectionWithoutStickyProgress()
        {
            Assert.That(HumanCannonCourseSections.SectionAt(new Vector3(0, 6, 160), Layout), Is.EqualTo(HumanCannonCourseSections.Section.Final));
            Assert.That(HumanCannonCourseSections.SectionAt(new Vector3(0, 6, 80), Layout), Is.EqualTo(HumanCannonCourseSections.Section.Shelter));
            Assert.That(HumanCannonCourseSections.SectionAt(new Vector3(0, 0, 3), Layout), Is.EqualTo(HumanCannonCourseSections.Section.Departure));
        }

        [Test]
        public void LiftBoundary_ChangesOnlyWhenFeetReachTheUpperLevel()
        {
            Assert.That(HumanCannonCourseSections.SectionAt(new Vector3(0, 4.99f, 73), Layout), Is.EqualTo(HumanCannonCourseSections.Section.First));
            Assert.That(HumanCannonCourseSections.SectionAt(new Vector3(0, 5, 73), Layout), Is.EqualTo(HumanCannonCourseSections.Section.Shelter));
        }

        [Test]
        public void SplitCubeCorner_ExtrudesAllFacesInTheSameDirectionWithoutChangingTheSourceNormals()
        {
            var corner = new Vector3(0.5f, 0.5f, 0.5f);
            var vertices = new[] { corner, corner, corner, -corner };
            var normals = new[] { Vector3.right, Vector3.up, Vector3.forward, Vector3.left };
            var result = HumanCannonOutline.SmoothNormals(vertices, normals);
            for (int i = 0; i < 3; i++)
                Assert.That(Vector3.Distance(result[i], Vector3.one.normalized), Is.LessThan(0.00001f));
            Assert.That(result[3], Is.EqualTo(Vector3.left));
            Assert.That(normals[0], Is.EqualTo(Vector3.right));
        }

        [Test]
        public void OpposingNormals_DoNotCollapseAThinDoubleSidedSurface()
        {
            var result = HumanCannonOutline.SmoothNormals(new[] { Vector3.zero, Vector3.zero }, new[] { Vector3.up, Vector3.down });
            Assert.That(result[0], Is.EqualTo(Vector3.up));
            Assert.That(result[1], Is.EqualTo(Vector3.down));
        }
    }
}
