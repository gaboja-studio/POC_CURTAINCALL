using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>Presentation-only transitions, driven by existing flight revision/segment and vertical speed.</summary>
    public sealed class CannonMotionPhase
    {
        public string Motion { get; private set; }
        bool flying;
        int station;
        uint launch, segment;
        float apexTime, landingTime, flightAge;
        bool apex;

        public void Reset() { flying = apex = false; apexTime = landingTime = flightAge = 0; Motion = null; }

        public string Step(bool inFlight, int source, uint revision, uint bounce, float verticalSpeed, float delta)
        {
            delta = Mathf.Clamp(delta, 0, .1f);
            if (inFlight)
            {
                if (!flying || station != source || launch != revision || segment != bounce)
                {
                    apex = false; apexTime = landingTime = flightAge = 0;
                    station = source; launch = revision; segment = bounce;
                }
                flying = true;
                // A remote segment notice can precede the first interpolated upward position.
                // Give that sample a short settling window instead of latching the previous descent.
                flightAge += delta;
                if (flightAge >= .12f && verticalSpeed <= 1.2f) apex = true;
                if (apex)
                {
                    apexTime += delta;
                    return Motion = apexTime < .42f ? "FlightApex" : "PanicFall";
                }
                return Motion = source >= 0 && bounce == 0 ? "CannonRise" : "TrampolineRise";
            }
            if (flying) { landingTime = .72f; flying = false; }
            if (landingTime > 0)
            {
                landingTime -= delta;
                return Motion = "FlightLand";
            }
            return Motion = null;
        }
    }
}
