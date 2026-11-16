using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        sealed class FinishSolid
        {
            public Bounds bounds;
            public MeshRenderer renderer;
            public Material material;
            public int layer;
            public StaticEditorFlags flags;
            public readonly List<GameObject> sources = new();
        }

        // Repeated doorway cuts leave overlapping cuboids and coplanar faces. Consolidate
        // only exact rectangular unions, preserving the existing occupied space and holes.
        public static object FinishLabyrinthSolidJoints()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != LabyrinthScenePath)
                throw new InvalidOperationException("Stopped Prototype required");
            var frame = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Single(t => t.name == "EscapeCampaignFrame");
            Physics.SyncTransforms();
            var solids = new List<FinishSolid>();
            var referenced = new HashSet<Object>();
            foreach (var behavior in frame.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!behavior) continue;
                var iterator = new SerializedObject(behavior).GetIterator();
                while (iterator.Next(true))
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue)
                        referenced.Add(iterator.objectReferenceValue);
            }
            foreach (var r in frame.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || r.transform.childCount != 0
                    || !(r.name == "Concrete remainder" || r.name == "Concrete remainder fitted return")
                    || r.sharedMaterials.Length != 1 || !r.sharedMaterial) continue;
                var components = r.GetComponents<Component>();
                if (components.Any(c => !(c is Transform || c is MeshRenderer || c is MeshFilter || c is BoxCollider || c is MeshCollider))
                    || referenced.Contains(r.gameObject) || components.Any(c => referenced.Contains(c))) continue;
                var mesh = r.GetComponent<MeshFilter>()?.sharedMesh;
                if (!mesh || mesh.triangles.Length != 36) continue;
                var bounds = new Bounds(frame.InverseTransformPoint(r.bounds.center), r.bounds.size);
                bool aligned = mesh.vertices.All(v =>
                {
                    var p = frame.InverseTransformPoint(r.transform.TransformPoint(v));
                    return Enumerable.Range(0,3).All(a => Mathf.Min(Mathf.Abs(p[a]-bounds.min[a]),Mathf.Abs(p[a]-bounds.max[a])) < .0005f);
                });
                var colliders = r.GetComponents<Collider>();
                if (!aligned || colliders.Length == 0 || colliders.Any(c => !c.enabled || c.isTrigger
                    || Vector3.Distance(c.bounds.center,r.bounds.center)>.0005f || Vector3.Distance(c.bounds.size,r.bounds.size)>.0005f)) continue;
                var solid = new FinishSolid { bounds=bounds,renderer=r,material=r.sharedMaterial,layer=r.gameObject.layer,flags=GameObjectUtility.GetStaticEditorFlags(r.gameObject) };
                solid.sources.Add(r.gameObject); solids.Add(solid);
            }
            int original = solids.Count;
            bool changed;
            do
            {
                changed = false;
                for (int i=0;i<solids.Count;i++)
                for (int j=i+1;j<solids.Count;j++)
                {
                    var a=solids[i]; var b=solids[j];
                    if (a.material!=b.material || a.layer!=b.layer || a.flags!=b.flags) continue;
                    if (!FinishRectangularUnion(a.bounds,b.bounds,out var union)) continue;
                    a.bounds=union; a.sources.AddRange(b.sources); solids.RemoveAt(j--); changed=true;
                }
            } while(changed);
            Undo.IncrementCurrentGroup(); int undo=Undo.GetCurrentGroup();
            int removed=0,created=0;
            try
            {
                foreach(var s in solids.Where(s=>s.sources.Count>1))
                {
                    var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name="Concrete remainder";
                    Undo.RegisterCreatedObjectUndo(go,"Fit continuous concrete joint");
                    go.transform.SetParent(s.renderer.transform.parent,false);
                    go.transform.SetPositionAndRotation(frame.TransformPoint(s.bounds.center),frame.rotation);
                    var scale=go.transform.parent.lossyScale;
                    go.transform.localScale=new(s.bounds.size.x/Mathf.Abs(scale.x),s.bounds.size.y/Mathf.Abs(scale.y),s.bounds.size.z/Mathf.Abs(scale.z));
                    go.GetComponent<MeshRenderer>().sharedMaterial=s.material;
                    go.layer=s.layer;GameObjectUtility.SetStaticEditorFlags(go,s.flags);
                    foreach(var source in s.sources){Undo.DestroyObjectImmediate(source);removed++;}created++;
                }
                Physics.SyncTransforms();
                if(removed>0)EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(undo);
                return new { candidates=original,removed,created,retained=solids.Count,maximumBoundaryTolerance=.00015f };
            }
            catch{Undo.RevertAllDownToGroup(undo);throw;}
        }

        static bool FinishRectangularUnion(Bounds a,Bounds b,out Bounds union)
        {
            const float epsilon=.00015f; union=a;
            bool Contains(Bounds x,Bounds y)=>Enumerable.Range(0,3).All(d=>x.min[d]<=y.min[d]+epsilon&&x.max[d]>=y.max[d]-epsilon);
            if(Contains(a,b))return true;
            if(Contains(b,a)){union=b;return true;}
            for(int axis=0;axis<3;axis++)
            {
                int p=(axis+1)%3,q=(axis+2)%3;
                if(Mathf.Abs(a.min[p]-b.min[p])>epsilon||Mathf.Abs(a.max[p]-b.max[p])>epsilon
                    ||Mathf.Abs(a.min[q]-b.min[q])>epsilon||Mathf.Abs(a.max[q]-b.max[q])>epsilon
                    ||a.min[axis]>b.max[axis]+epsilon||b.min[axis]>a.max[axis]+epsilon)continue;
                union.SetMinMax(Vector3.Min(a.min,b.min),Vector3.Max(a.max,b.max));return true;
            }
            return false;
        }
    }
}
