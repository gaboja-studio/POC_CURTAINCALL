using System;
using UnityEngine;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public sealed class EscapeStaticBatchRecord : ScriptableObject
    {
        public string[] sources = Array.Empty<string>();
        public string[] meshes = Array.Empty<string>();
    }
}
