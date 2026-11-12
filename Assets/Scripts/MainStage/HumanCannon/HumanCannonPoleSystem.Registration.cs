using System;
using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonPoleSystem
    {
        // 생성 순서/연습 단계 이력이 다른 클라이언트도 같은 배치의 봉을 지목한다.
        readonly Dictionary<string, int> registeredPaths = new(StringComparer.Ordinal);
        readonly Dictionary<int, int> wireToLocal = new();
        readonly Dictionary<int, int> localToWire = new();

        static string RegistrationPath(Transform part) => part.parent == null ? part.name
            : RegistrationPath(part.parent) + "/" + part.name;
        static int StableWireId(string path)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in path) hash = (hash ^ character) * 16777619;
                return (int)((hash & 0x3fffffff) | 0x40000000);
            }
        }
        int WireIndex(int local) => localToWire.TryGetValue(local, out int wire) ? wire : local;
        int LocalIndex(int wire) => (wire & 0x40000000) == 0 ? wire
            : wireToLocal.TryGetValue(wire, out int local) ? local : -1;

        int RegisterPlacement(HumanCannonPole pole)
        {
            string path = RegistrationPath(pole.transform);
            if (registeredPaths.TryGetValue(path, out int existing))
            {
                Deactivate(existing);
                poles[existing] = pole;
                var state = states[existing];
                state.State.Reset(); state.ResetRevision = state.State.Revision;
                state.Angle = state.Speed = 0f;
                state.ResetDrive();
                state.LastDeparture.Clear();
                for (int seat = 0; seat < state.Riders.Length; seat++) state.Riders[seat] = new Rider();
                return existing;
            }
            int wire = StableWireId(path);
            if (wireToLocal.ContainsKey(wire)) throw new InvalidOperationException("Duplicate common pole wire ID: " + path);
            int index = poles.Length;
            Array.Resize(ref poles, index + 1); poles[index] = pole;
            Array.Resize(ref states, index + 1); states[index] = NewRuntime(index);
            registeredPaths.Add(path, index); wireToLocal.Add(wire, index); localToWire.Add(index, wire);
            return index;
        }
    }
}
