using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    /// <summary>
    /// Static, off-route evidence of a human industrial plant repurposed by an unknown operator.
    /// The caller supplies a solid shelf and keeps this entire envelope clear of traversal:
    /// x [-7, 7], y [0, 6], z [.8, 4.8], with the display facing local -Z.
    /// Only new transforms and mesh renderers are created; shared materials are never changed.
    /// </summary>
    public static class EscapeLabyrinthStory
    {
        public enum StoryStage { Intake, Processing, Output }

        const string RootName = "Human conversion evidence";

        public static void Build(Transform parent, Material concrete, Material steel, Material warning)
        {
            var root = Begin(parent, concrete, steel, warning);
            Gantry(root, concrete, steel, warning);
            Cage(root, new Vector3(-5.3f, 0, 2.3f), steel, warning, true);
            Cage(root, new Vector3(-3.5f, 0, 2.9f), steel, warning, false);
            Hook(root, -4.45f, 2.5f, steel);
            Process(root, 0, concrete, steel, warning);
            Crate(root, new Vector3(4.25f, 0, 2.15f), steel, warning, false);
            Crate(root, new Vector3(5.75f, 0, 3.5f), steel, warning, true);
        }

        public static void Build(Transform parent, Material concrete, Material steel, Material warning, StoryStage stage)
        {
            if (!Enum.IsDefined(typeof(StoryStage), stage)) throw new ArgumentOutOfRangeException(nameof(stage));
            var root = Begin(parent, concrete, steel, warning);
            Gantry(root, concrete, steel, warning);
            switch (stage)
            {
                case StoryStage.Intake:
                    for (int i = 0; i < 4; i++)
                    {
                        float x = -4.65f + i * 2.35f;
                        Cage(root, new Vector3(x, 0, 2.55f + (i % 2) * .65f), steel, warning, i != 1);
                        Hook(root, x, 2.5f, steel);
                    }
                    Restraint(root, new Vector3(5.05f, .3f, 2.5f), steel, warning);
                    break;
                case StoryStage.Processing:
                    Cage(root, new Vector3(-4.9f, 0, 2.4f), steel, warning, true);
                    Hook(root, -3.65f, 2.5f, steel);
                    Process(root, 0, concrete, steel, warning);
                    Crate(root, new Vector3(4.65f, 0, 2.6f), steel, warning, true);
                    break;
                case StoryStage.Output:
                    for (int i = 0; i < 5; i++)
                        Crate(root, new Vector3(-4.9f + i * 2.4f, 0, 2.2f + (i % 2) * 1.2f),
                            steel, warning, i % 2 == 0);
                    OutputDuct(root, concrete, steel, warning);
                    break;
            }
        }

        static Transform Begin(Transform parent, Material concrete, Material steel, Material warning)
        {
            if (!parent) throw new ArgumentNullException(nameof(parent));
            if (!concrete || !steel || !warning) throw new ArgumentException("All three shared materials are required.");
            if (Application.isPlaying) throw new InvalidOperationException("Build scenery only in edit mode.");
            if (parent.Find(RootName)) throw new InvalidOperationException("This parent already contains labyrinth story scenery.");
            return Node(parent, RootName, Vector3.zero);
        }

        static void Gantry(Transform root, Material concrete, Material steel, Material warning)
        {
            foreach (float x in new[] { -6.35f, 6.35f })
            {
                Box(root, "Original human concrete pier", new Vector3(x, 2.7f, 4.35f), new Vector3(.9f, 5.4f, .9f), concrete);
                Box(root, "Crude overhanging conversion bracket", new Vector3(x, 5.35f, 3.25f), new Vector3(.5f, .5f, 2.8f), steel);
                Box(root, "Oversized bolted pier collar", new Vector3(x, 4.65f, 4.3f), new Vector3(1.15f, .8f, 1), steel);
                foreach (float dx in new[] { -.37f, .37f })
                    foreach (float y in new[] { 4.43f, 4.87f })
                        Bolt(root, new Vector3(x + dx, y, 3.77f), .08f, warning);
                Beam(root, "Gantry diagonal support", new Vector3(x, 3.75f, 4.05f), new Vector3(x, 5.3f, 2.1f), .16f, steel);
                Box(root, "Pier base anchor", new Vector3(x, .15f, 4.3f), new Vector3(1.25f, .3f, 1), steel);
            }
            Box(root, "Overhead human transport rail", new Vector3(0, 5.6f, 2.5f), new Vector3(13.2f, .22f, .34f), steel);
            Box(root, "Rail top flange", new Vector3(0, 5.77f, 2.5f), new Vector3(13.2f, .12f, .58f), steel);
            Box(root, "Back pressure main", new Vector3(0, 4.65f, 4.3f), new Vector3(12.8f, .38f, .38f), steel);
            for (int i = 0; i < 3; i++)
            {
                float x = -4.7f + i * 4.7f;
                Box(root, "Pressure main fixed hanger", new Vector3(x, 5.13f, 3.4f), new Vector3(.12f, .72f, 2), steel);
                Box(root, "Mismatched pipe coupling", new Vector3(x, 4.65f, 4.3f), new Vector3(.28f, .65f, .65f), warning);
            }
        }

        static void Cage(Transform root, Vector3 position, Material steel, Material warning, bool withRestraint)
        {
            var cage = Node(root, "Human-sized locked transport cage", position);
            foreach (float x in new[] { -.58f, .58f })
                Box(cage, "Grounded transport skid", new Vector3(x, .12f, 0), new Vector3(.18f, .24f, 1.5f), steel);
            foreach (float y in new[] { .3f, 2.8f })
            {
                foreach (float z in new[] { -.61f, .61f })
                    Box(cage, "Cage perimeter frame", new Vector3(0, y, z), new Vector3(1.45f, .12f, .12f), steel);
                foreach (float x in new[] { -.66f, .66f })
                    Box(cage, "Cage perimeter frame", new Vector3(x, y, 0), new Vector3(.12f, .12f, 1.34f), steel);
            }
            foreach (float x in new[] { -.66f, -.33f, 0, .33f, .66f })
                foreach (float z in new[] { -.61f, .61f })
                    Box(cage, "Narrow vertical containment bars", new Vector3(x, 1.55f, z), new Vector3(.065f, 2.5f, .065f), steel);
            foreach (float x in new[] { -.66f, .66f })
                Box(cage, "Side containment bar", new Vector3(x, 1.55f, 0), new Vector3(.065f, 2.5f, .065f), steel);
            Box(cage, "Locked front latch", new Vector3(.44f, 1.55f, -.69f), new Vector3(.32f, .2f, .13f), warning);
            Box(cage, "Cage serial plate", new Vector3(-.43f, 2.57f, -.69f), new Vector3(.36f, .25f, .08f), warning);
            for (int i = 0; i < 3; i++)
                Box(cage, "Serial stamp mark", new Vector3(-.52f + i * .09f, 2.57f, -.738f), new Vector3(.025f, .13f, .025f), steel);
            if (withRestraint) Restraint(cage, new Vector3(0, .35f, .14f), steel, warning);
        }

        static void Restraint(Transform root, Vector3 position, Material steel, Material warning)
        {
            var rack = Node(root, "Empty adult-height restraint loading frame", position);
            Box(rack, "Frame support foot", new Vector3(0, .08f, 0), new Vector3(1.02f, .16f, .66f), steel);
            Box(rack, "Human back support", new Vector3(0, 1.1f, .14f), new Vector3(.15f, 2.2f, .15f), steel);
            Box(rack, "Shoulder restraint crossmember", new Vector3(0, 1.62f, .14f), new Vector3(.91f, .1f, .12f), steel);
            Box(rack, "Head restraint backing", new Vector3(0, 1.99f, .09f), new Vector3(.37f, .4f, .1f), steel);
            foreach (float x in new[] { -.2f, .2f })
            {
                Box(rack, "Ankle restraint stock", new Vector3(x, .22f, -.1f), new Vector3(.2f, .13f, .3f), warning);
                Box(rack, "Head clamp jaw", new Vector3(x, 1.99f, -.05f), new Vector3(.06f, .28f, .28f), warning);
            }
            foreach (float x in new[] { -.4f, .4f })
                Box(rack, "Wrist restraint stock", new Vector3(x, 1.62f, -.04f), new Vector3(.13f, .2f, .28f), warning);
            Box(rack, "Waist loading clamp", new Vector3(0, .94f, -.07f), new Vector3(.62f, .14f, .28f), steel);
        }

        static void Hook(Transform root, float x, float z, Material steel)
        {
            Box(root, "Captured rail trolley", new Vector3(x, 5.45f, z), new Vector3(.58f, .3f, .58f), steel);
            Beam(root, "Suspended conveyor shank", new Vector3(x, 5.35f, z), new Vector3(x, 3.9f, z), .065f, steel);
            Vector3[] points =
            {
                new Vector3(x, 3.92f, z), new Vector3(x, 3.56f, z),
                new Vector3(x + .13f, 3.4f, z), new Vector3(x + .33f, 3.4f, z),
                new Vector3(x + .46f, 3.55f, z), new Vector3(x + .46f, 3.77f, z)
            };
            for (int i = 1; i < points.Length; i++)
                Beam(root, "Empty human conveyance hook", points[i - 1], points[i], .075f, steel);
        }

        static void Process(Transform root, float x, Material concrete, Material steel, Material warning)
        {
            var machine = Node(root, "Shutter-obscured human processing station", new Vector3(x, 0, 0));
            Box(machine, "Grounded processing plinth", new Vector3(0, .25f, 3.1f), new Vector3(4.6f, .5f, 3.2f), concrete);
            foreach (float side in new[] { -1.95f, 1.95f })
            {
                Box(machine, "Press side enclosure", new Vector3(side, 2.2f, 3.2f), new Vector3(.38f, 3.4f, 2.7f), steel);
                Box(machine, "Press anchored foot", new Vector3(side, .55f, 2.05f), new Vector3(.9f, .15f, .9f), steel);
                Bolt(machine, new Vector3(side, .72f, 1.75f), .11f, warning);
            }
            Box(machine, "Rear process bulkhead", new Vector3(0, 2.25f, 4.47f), new Vector3(4.2f, 3.5f, .2f), steel);
            Box(machine, "Press upper casing", new Vector3(0, 3.85f, 3.15f), new Vector3(4.3f, .55f, 2.9f), steel);
            Restraint(machine, new Vector3(-.65f, .5f, 2.25f), steel, warning);
            Box(machine, "Low process receiving slot", new Vector3(.6f, .725f, 2.2f), new Vector3(1.35f, .45f, 1.25f), steel);
            // Uneven steel shutters hide the operation while retaining one narrow human-height sightline.
            for (int i = 0; i < 7; i++)
            {
                if (i == 2) continue;
                float height = i % 3 == 0 ? 2.85f : 3.1f;
                Box(machine, "Opaque process screen strip", new Vector3(-1.68f + i * .55f, 3.55f - height * .5f, 1.68f),
                    new Vector3(.51f, height, .12f), steel);
            }
            Box(machine, "Human loading aperture sill", new Vector3(0, .61f, 1.65f), new Vector3(4.2f, .16f, .2f), warning);
            Box(machine, "Unequal pressure head", new Vector3(.85f, 4.55f, 3.35f), new Vector3(2.9f, 1.35f, 2.5f), steel);
            Box(machine, "Retrofitted head collar", new Vector3(.85f, 4.55f, 2.05f), new Vector3(3.08f, 1.5f, .17f), warning);
            foreach (float dx in new[] { -.35f, .85f, 2.05f })
                foreach (float y in new[] { 4.05f, 5.05f })
                    Bolt(machine, new Vector3(dx, y, 1.94f), .095f, steel);
            Beam(machine, "Machine pressure inlet", new Vector3(.85f, 5.22f, 3.7f), new Vector3(.85f, 5.55f, 3.7f), .22f, steel);
            Beam(machine, "Pressure inlet elbow", new Vector3(.85f, 5.55f, 3.7f), new Vector3(.85f, 5.55f, 4.3f), .22f, steel);
            Beam(machine, "Pressure inlet return", new Vector3(.85f, 5.55f, 4.3f), new Vector3(.85f, 4.65f, 4.3f), .22f, steel);
            for (int i = 0; i < 4; i++)
                Box(machine, "Mismatched welded repair rib", new Vector3(-1.2f + i * .8f, 3.86f, 1.64f), new Vector3(.09f, .6f, .25f), warning);
        }

        static void Crate(Transform root, Vector3 position, Material steel, Material warning, bool tall)
        {
            var crate = Node(root, "Sealed unidentifiable manufactured output", position);
            float height = tall ? 2.65f : 1.8f;
            foreach (float x in new[] { -.48f, .48f })
                Box(crate, "Output pallet grounded skid", new Vector3(x, .125f, 0), new Vector3(.2f, .25f, 1.38f), steel);
            Box(crate, "Opaque output shipping shell", new Vector3(0, .25f + height * .5f, 0), new Vector3(1.3f, height, 1.3f), steel);
            foreach (float x in new[] { -.5f, .5f })
            {
                Box(crate, "Sealed output clamping strap", new Vector3(x, .25f + height * .5f, -.68f), new Vector3(.11f, height + .1f, .08f), warning);
                Box(crate, "Clamping strap over lid", new Vector3(x, .3f + height, 0), new Vector3(.11f, .1f, 1.42f), warning);
            }
            Box(crate, "Stamped nonhuman inspection plate", new Vector3(0, .9f, -.69f), new Vector3(.62f, .55f, .07f), warning);
            Box(crate, "Untranslatable stamp vertical", new Vector3(-.15f, .9f, -.74f), new Vector3(.055f, .37f, .025f), steel);
            Beam(crate, "Untranslatable stamp diagonal", new Vector3(-.15f, .75f, -.74f), new Vector3(.17f, 1.06f, -.74f), .033f, steel);
            Box(crate, "Untranslatable stamp crossbar", new Vector3(.06f, .85f, -.74f), new Vector3(.3f, .055f, .025f), steel);
            // A capped, asymmetrical fitting gives no recognizable purpose to the sealed contents.
            Box(crate, "Unknown product socket base", new Vector3(.17f, .33f + height, .12f), new Vector3(.65f, .16f, .6f), steel);
            var fitting = Primitive(crate, "Offset capped product fitting", PrimitiveType.Cylinder,
                new Vector3(.17f, .52f + height, .12f), new Vector3(.35f, .2f, .35f), warning);
            fitting.localRotation = Quaternion.Euler(0, 0, 18);
        }

        static void OutputDuct(Transform root, Material concrete, Material steel, Material warning)
        {
            Box(root, "Original low human utility pier", new Vector3(0, 1.8f, 4.2f), new Vector3(2.8f, 3.6f, .8f), concrete);
            Box(root, "Oversized sealed manufacturing duct", new Vector3(0, 3.85f, 3.65f), new Vector3(5.8f, 1.35f, 1.4f), steel);
            foreach (float x in new[] { -2.5f, 0, 2.5f })
            {
                Box(root, "External duct compression collar", new Vector3(x, 3.85f, 3.6f), new Vector3(.2f, 1.6f, 1.65f), warning);
                Box(root, "Duct support welded to human pier", new Vector3(x, 2.05f, 4.05f), new Vector3(.2f, 3.1f, .25f), steel);
                Box(root, "Duct support grounded foot", new Vector3(x, .25f, 4.05f), new Vector3(.6f, .5f, .6f), concrete);
            }
        }

        static Transform Node(Transform parent, string name, Vector3 position)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent, false);
            node.localPosition = position;
            node.gameObject.isStatic = true;
            return node;
        }

        static Transform Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
            => Primitive(parent, name, PrimitiveType.Cube, position, size, material);

        static Transform Primitive(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var node = GameObject.CreatePrimitive(type).transform;
            node.name = name;
            node.SetParent(parent, false);
            node.localPosition = position;
            node.localScale = scale;
            node.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.DestroyImmediate(node.GetComponent<Collider>());
            node.gameObject.isStatic = true;
            return node;
        }

        static void Beam(Transform parent, string name, Vector3 start, Vector3 end, float radius, Material material)
        {
            var beam = Primitive(parent, name, PrimitiveType.Cylinder, (start + end) * .5f,
                new Vector3(radius * 2, Vector3.Distance(start, end) * .5f, radius * 2), material);
            beam.localRotation = Quaternion.FromToRotation(Vector3.up, end - start);
        }

        static void Bolt(Transform parent, Vector3 position, float radius, Material material)
        {
            var bolt = Primitive(parent, "Visible crude anchor bolt", PrimitiveType.Cylinder, position,
                new Vector3(radius * 2, .045f, radius * 2), material);
            bolt.localRotation = Quaternion.Euler(90, 0, 0);
        }
    }
}
