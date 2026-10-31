using CurtainCall.WaitingRoom;
using UnityEngine;

namespace CurtainCall.ItemArt
{
    // Initial pickup artwork. Successful inventory receipt hides it; dropped keys use FloorPropsView.
    [DefaultExecutionOrder(850)]
    public sealed class ExplorationKeyVisual : MonoBehaviour
    {
        ExplorationGate gate;
        GameObject visual;
        Renderer[] originals;
        bool[] enabledBefore;
        void Start()
        {
            gate=GetComponent<ExplorationGate>();
            originals=GetComponentsInChildren<MeshRenderer>(true);enabledBefore=new bool[originals.Length];
            for(int i=0;i<originals.Length;i++){enabledBefore[i]=originals[i].enabled;originals[i].enabled=false;}
            visual=ItemVisualAssets.Create("route-key");
            if(visual!=null)visual.name="Key artwork / "+gate.TargetId;
        }
        void LateUpdate()
        {
            if(visual==null||gate==null)return;
            visual.SetActive(gate.Visible&&!gate.Opened&&gameObject.activeInHierarchy);
            visual.transform.SetPositionAndRotation(gate.PresentationPoint+Vector3.up*.035f,transform.rotation);
        }
        void OnDisable(){if(visual!=null)visual.SetActive(false);}
        void OnDestroy()
        {
            if(visual!=null)Destroy(visual);
            if(originals!=null)for(int i=0;i<originals.Length;i++)if(originals[i]!=null)originals[i].enabled=enabledBefore[i];
        }
    }
}
