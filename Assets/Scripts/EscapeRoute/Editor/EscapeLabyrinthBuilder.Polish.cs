using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        const string LabyrinthArt="Assets/Scenes/Levels/EscapeLabyrinthArt";
        static void BuildLabyrinthPolish()
        {
            RemoveLabyrinthFloorRoutePaint();
            RemoveLabyrinthInteriorConveyorRail();
            if(!AssetDatabase.IsValidFolder(LabyrinthArt))AssetDatabase.CreateFolder("Assets/Scenes/Levels","EscapeLabyrinthArt");
            // The original running slab has deliberately chipped edges. New side aisles must
            // join a continuous edge: copy this scene's slab mesh, never edit the shared source.
            var deck=labyrinthFrame.GetComponentsInChildren<MeshFilter>().Single(m=>m.name=="복귀 고정 후 마지막 질주면");
            var mesh=Object.Instantiate(deck.sharedMesh);mesh.name="LabyrinthLastRunningSlab";
            var vertices=mesh.vertices;
            for(int i=0;i<vertices.Length;i++)
            {
                var p=labyrinthFrame.InverseTransformPoint(deck.transform.TransformPoint(vertices[i]));
                p.z=p.z<78?71:85;vertices[i]=deck.transform.InverseTransformPoint(LW(p));
            }
            mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            const string meshPath=LabyrinthArt+"/last-running-slab.asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(saved){EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}
            else{saved=mesh;AssetDatabase.CreateAsset(saved,meshPath);}
            deck.sharedMesh=saved;deck.GetComponent<MeshCollider>().sharedMesh=saved;AssetDatabase.SaveAssetIfDirty(saved);

            var workshop=new Bounds(new(381,3.6f,78),new(30,8.8f,34));
            LCutNamedBox("거친 외벽 남측 방벽",workshop);
            LCutNamedBox("거친 외벽 북측 방벽",workshop);
            foreach(var t in labyrinthFrame.GetComponentsInChildren<Transform>().Where(t=>t.name=="이탈한 무거운 콘크리트 조각"||t.name=="잘게 깨진 바닥 가장자리").ToArray())
            {
                var p=labyrinthFrame.InverseTransformPoint(t.position);
                if(p.x>=336&&p.x<=396&&p.z>=61&&p.z<=95&&t.GetComponentsInChildren<MonoBehaviour>().Length==0)Object.DestroyImmediate(t.gameObject);
            }
            // The cut wall used to leave a misleading parallel ledge beside the new cable.
            // Remove the old remnant, retaining the deliberately isolated landing piers.
            LCutNamedBox("Concrete remainder",new Bounds(new(202,-49.9f,55.5f),new(81,100.2f,13)));
        }

        static void RemoveLabyrinthFloorRoutePaint()
        {
            // The copied scene includes baked static meshes for its old floor arrows.
            // Remove only thin, horizontal paint visuals; do not touch the shared meshes,
            // physical floors, yellow pole bars, railings or machine markings.
            foreach (var renderer in labyrinthFrame.GetComponentsInChildren<MeshRenderer>(true).ToArray())
            {
                var materials = renderer.sharedMaterials;
                if (renderer.bounds.size.y > .03f || materials.Length == 0
                    || materials.Any(m => !m || (m.name != "factory-safety-ochre" && m.name != "working-faded-ochre"))) continue;
                if (renderer.transform.childCount != 0 || renderer.GetComponents<Component>().Any(c =>
                    c is not Transform && c is not MeshRenderer && c is not MeshFilter))
                    throw new System.InvalidOperationException("Floor paint has unexpected gameplay or child components: " + renderer.name);
                Object.DestroyImmediate(renderer.gameObject);
            }
        }

        static void RemoveLabyrinthInteriorConveyorRail()
        {
            // This edge bordered the void in the source scene. The new inspection
            // apron now meets it at the same floor height, so its old guard became
            // an unintended fence across the shared cannon approach.
            foreach(var t in labyrinthFrame.GetComponentsInChildren<Transform>(true)
                .Where(t=>t.name.StartsWith("Launch side guard")).ToArray())
            {
                var p=labyrinthFrame.InverseTransformPoint(t.position);
                if(Mathf.Abs(p.x-70.92f)>.15f||p.y>-20||p.z<0||p.z>16)continue;
                if(t.childCount!=0||t.GetComponents<MonoBehaviour>().Length!=0)
                    throw new System.InvalidOperationException("Unexpected conveyor guard components: "+t.name);
                Object.DestroyImmediate(t.gameObject);
            }
        }
    }
}
