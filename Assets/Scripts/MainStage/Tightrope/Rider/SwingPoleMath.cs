namespace CurtainCall.Tightrope
{
    /// <summary>외줄에만 필요한 구간 배치. 움직임·중력·공중 보정은 인간대포 시스템을 사용한다.</summary>
    public static class SwingPoleMath
    {
        public const float EntryOffset = .8f, ExitOffset = 1.2f, GripHeight = 1.3f;
        public static bool HasGap(float finish, float start, float length) =>
            float.IsFinite(finish) && float.IsFinite(start) && float.IsFinite(length)
            && start >= 10f && length >= 4f && length <= 12f && start + length + 10f < finish;
        public static bool Contains(float distance, float start, float length) => distance >= start && distance < start + length;
    }
}
