using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>Local arrival barriers keep an arrived owner on the platform without blocking incoming peers.</summary>
    public static class RehearsalArrivalWalls
    {
        public const float Thickness = .3f;
        public static GameObject Create(Transform parent, string name, Bounds floor)
        {
            const float thickness = Thickness, height = 4f;
            var root = new GameObject(name); root.transform.SetParent(parent, true);
            void Wall(string side, Vector3 point, Vector3 size)
            {
                var go = new GameObject(side, typeof(BoxCollider)); go.transform.SetParent(root.transform, true);
                go.transform.position = point + Vector3.up * (height * .5f - .5f);
                go.GetComponent<BoxCollider>().size = size;
            }
            float y = floor.max.y;
            Wall("Front", new Vector3(floor.center.x, y, floor.min.z), new Vector3(floor.size.x + thickness * 2, height, thickness));
            Wall("Back", new Vector3(floor.center.x, y, floor.max.z), new Vector3(floor.size.x + thickness * 2, height, thickness));
            Wall("Left", new Vector3(floor.min.x, y, floor.center.z), new Vector3(thickness, height, floor.size.z));
            Wall("Right", new Vector3(floor.max.x, y, floor.center.z), new Vector3(thickness, height, floor.size.z));
            root.SetActive(false);
            return root;
        }
    }
}
