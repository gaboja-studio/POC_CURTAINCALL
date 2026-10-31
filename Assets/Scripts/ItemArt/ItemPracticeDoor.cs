using CurtainCall.WaitingRoom;
using UnityEngine;
namespace CurtainCall.ItemArt
{
    public sealed class ItemPracticeDoor : MonoBehaviour,IShopTarget,IShopPresentation
    {
        public Transform Leaf;
        bool open;
        public Transform VisualRoot=>transform;
        public Vector3 PresentationPoint=>transform.position+Vector3.up*.9f;
        public string ActionLabel=>open?"문 닫기":"문 열기";
        public bool IsAvailable=>true;
        public float Reach(Vector3 feet){float d=Vector3.Distance(new Vector3(feet.x,0,feet.z),new Vector3(transform.position.x,0,transform.position.z));return d<1.8f?d:-1;}
        public void Interact(){open=!open;}
        void OnEnable()=>ShopReach.Add(this);
        void OnDisable()=>ShopReach.Remove(this);
        void Update(){ShopReach.Pump();if(Leaf!=null)Leaf.localRotation=Quaternion.Slerp(Leaf.localRotation,Quaternion.Euler(0,open?90:0,0),Time.deltaTime*4);}
    }
}
