using System;
using CurtainCall.Network.PlayerSync;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.UI.Wayfinding
{
    /// <summary>A live endpoint. Default is the calling client's local player; destroyed objects never become positions.</summary>
    public readonly struct NavigationTarget
    {
        enum Kind { LocalPlayer, Position, Object, Player }
        readonly Kind kind;
        readonly Vector3 position;
        readonly Transform target;
        NavigationTarget(Kind kind, Vector3 position, Transform target) { this.kind=kind;this.position=position;this.target=target; }
        public static NavigationTarget LocalPlayer => default;
        public static NavigationTarget Position(Vector3 point) => new(Kind.Position,point,null);
        public static NavigationTarget Object(Transform target) => new(Kind.Object,default,target);
        public static NavigationTarget Object(GameObject target) => Object(target != null ? target.transform : null);
        public static NavigationTarget Player(NetworkPlayer player) => new(Kind.Player,default,player != null ? player.transform : null);
        public static implicit operator NavigationTarget(Vector3 point) => Position(point);
        public static implicit operator NavigationTarget(Transform target) => Object(target);
        public static implicit operator NavigationTarget(GameObject target) => Object(target);
        public static implicit operator NavigationTarget(NetworkPlayer target) => Player(target);
        /// <summary>Only explicit object/player endpoints are outlined; the implicit local start stays unobtrusive.</summary>
        internal bool UsesObjectOutline => kind is Kind.Object or Kind.Player;
        internal Transform OutlineTarget => UsesObjectOutline ? target : null;
        public bool TryGetPosition(out Vector3 point)
        {
            if(kind==Kind.Position) {point=position;return Finite(point);}
            var anchor=kind==Kind.LocalPlayer ? NetworkPlayer.Local?.transform : target;
            if(anchor==null || !anchor.gameObject.activeInHierarchy) {point=default;return false;}
            point=anchor.position;return Finite(point);
        }
        internal static bool Finite(Vector3 point) => float.IsFinite(point.x)&&float.IsFinite(point.y)&&float.IsFinite(point.z);
    }

    /// <summary>One independently owned guide. Dispose/Hide only removes this guide.</summary>
    public sealed class NavigationHandle : IDisposable
    {
        internal readonly string Id=Guid.NewGuid().ToString("N");
        internal NavigationService Service;
        internal NavigationTarget Start, Destination;
        internal bool Synchronized;
        internal Color Color;
        internal float Speed, Emphasis;
        internal int SceneHandle;
        internal string ScenePath;
        internal bool Disposed;
        bool visible;
        public event Action<bool> VisibilityChanged;
        public bool IsVisible => visible;
        public bool IsSynchronized => Synchronized;
        public bool IsDisposed => Disposed;
        public Vector3[] Path => Service != null ? Service.GetPath(this) : Array.Empty<Vector3>();

        public void Update(NavigationTarget destination, NavigationTarget start=default, bool synchronize=false,
            float animationSpeed=1, float emphasis=1, Color? color=null)
        {
            if(Disposed) throw new ObjectDisposedException(nameof(NavigationHandle));
            Service.Change(this,destination,start,synchronize,animationSpeed,emphasis,color??Color);
        }
        public void SetAppearance(Color color,float animationSpeed=1,float emphasis=1)
            => Update(Destination,Start,Synchronized,animationSpeed,emphasis,color);
        internal void SetVisible(bool value)
        {
            if(visible==value)return;visible=value;VisibilityChanged?.Invoke(value);
        }
        public void Hide()=>Dispose();
        public void Dispose()
        {
            if(Disposed)return;Disposed=true;
            if(Service!=null)Service.Remove(this);
            SetVisible(false);Service=null;
        }
    }

    public static class WorldNavigation
    {
        public static readonly Color DefaultColor=new(.12f,1,.86f,1);
        /// <summary>Local guides never send traffic. Shared guides wait for a session and use the caller's start on the host.</summary>
        public static NavigationHandle Show(NavigationTarget destination,NavigationTarget start=default,bool synchronize=false,
            float animationSpeed=1,float emphasis=1,Color? color=null)
        {
            if(!Application.isPlaying)throw new InvalidOperationException("WorldNavigation.Show requires Play Mode.");
            return NavigationService.EnsureExists().Show(destination,start,synchronize,animationSpeed,emphasis,color??DefaultColor);
        }
    }
}
