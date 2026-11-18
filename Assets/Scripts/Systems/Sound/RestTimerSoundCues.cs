using CurtainCall.UI.RestTimerV28;

namespace CurtainCall.Sound
{
    /// <summary>
    /// 휴식·정비 타이머 표시 상태를 따라 낼 소리를 고른다(순수 로직, 화면 갱신마다 Step).
    /// - Normal → Warning(30초 이하)으로 바뀌는 순간: <see cref="SoundSituations.RestTimerWarning"/>.
    /// - Critical(10초 이하) 동안 남은 초가 한 칸 줄 때마다: <see cref="SoundSituations.RestTimerFinalTick"/>.
    /// 경고 문턱은 타이머 표시(<see cref="RestTimerPresentation.Evaluate"/>)와 같다. 처음 보인 프레임·멈춤 중에는 소리 내지 않아
    /// 중간 입장이나 다시 붙은 HUD가 경고를 다시 울리지 않는다.
    /// </summary>
    public sealed class RestTimerSoundCues
    {
        bool hasPrevious;
        RestTimerVisualState previousState;
        int previousSecond;

        public void Reset() => hasPrevious = false;

        /// <returns>재생할 상황 키. 없으면 null.</returns>
        public string Step(RestTimerSnapshot snapshot)
        {
            var state = RestTimerPresentation.Evaluate(snapshot);
            if (!snapshot.visible || state is RestTimerVisualState.Unavailable or RestTimerVisualState.Unlimited)
            {
                hasPrevious = false;
                return null;
            }
            int second = RestTimerPresentation.IsFinite(snapshot.remainingSeconds)
                ? (int)System.Math.Ceiling(System.Math.Max(0d, snapshot.remainingSeconds)) : int.MaxValue;
            string cue = null;
            if (hasPrevious && state != RestTimerVisualState.Paused)
            {
                if (state == RestTimerVisualState.Warning && previousState == RestTimerVisualState.Normal)
                    cue = SoundSituations.RestTimerWarning;
                else if (state == RestTimerVisualState.Critical && second > 0 && second < previousSecond)
                    cue = SoundSituations.RestTimerFinalTick;
            }
            hasPrevious = true;
            if (state != RestTimerVisualState.Paused) previousState = state;
            previousSecond = second;
            return cue;
        }
    }
}
