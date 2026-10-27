using Unity.Mathematics;

namespace GPTZoomToCursor
{
    // Pure geometry, shared with the standalone numerical tests.
    internal static class ZoomMath
    {
        internal static bool ShouldAnchor(bool enabled, bool zoomOutFromCursor, float before, float after)
        {
            return enabled && math.isfinite(before) && math.isfinite(after) && before > 0f && after > 0f
                && (after < before - 0.0001f || (zoomOutFromCursor && after > before + 0.0001f));
        }

        internal static bool TryPlaneCorrection(float3 origin, float3 direction, float3 anchor, out float3 correction)
        {
            correction = float3.zero;
            if (!math.all(math.isfinite(origin)) || !math.all(math.isfinite(direction)) || !math.all(math.isfinite(anchor))
                || direction.y >= -0.025f) return false;
            float distance = (anchor.y - origin.y) / direction.y;
            if (!math.isfinite(distance) || distance <= 0f || distance > 50000f) return false;
            correction = anchor - (origin + direction * distance);
            correction.y = 0f;
            return math.all(math.isfinite(correction));
        }
    }
}
