using System;

namespace GPTZoomToCursor
{
    // An angle trajectory, never an allowed-angle bound. Native smoothed zoom drives
    // the trajectory so tilt reaches its endpoint in the same frame as zoom.
    internal sealed class TiltMotion
    {
        private bool initialized;
        private float startZoom, startPitch, expectedZoom, expectedPitch;

        internal void Reset() { initialized = false; }

        internal float Step(float before, float after, float minimum, float pitch)
        {
            if (!Finite(before) || !Finite(after) || !Finite(minimum) || !Finite(pitch)
                || minimum <= 0 || before < minimum || after < minimum)
            {
                Reset();
                return pitch;
            }
            // A manual tilt, camera relocation, or another camera mod takes priority.
            if (!initialized || Math.Abs(pitch - expectedPitch) > 0.001f
                || Math.Abs(before - expectedZoom) > Math.Max(0.001f, before * 0.00001f))
            {
                startZoom = before;
                startPitch = pitch;
                initialized = true;
            }
            if (after > startZoom)
            {
                // Beyond the original wide view, retain its angle and extend the
                // trajectory continuously. No imposed angle at any zoom distance.
                startZoom = after;
                startPitch = pitch;
            }
            float result = pitch;
            if (before != after && startZoom > minimum)
            {
                double progress = Math.Log(startZoom / after) / Math.Log(startZoom / minimum);
                progress = Math.Max(0, Math.Min(1, progress));
                double eased = progress * progress * (3 - 2 * progress);
                result = after <= minimum ? 90f : (float)(startPitch + (90 - startPitch) * eased);
            }
            expectedZoom = after;
            expectedPitch = result;
            return result;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
