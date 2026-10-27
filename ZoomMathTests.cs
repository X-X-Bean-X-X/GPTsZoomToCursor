using System;
using Unity.Mathematics;
namespace GPTZoomToCursor
{
    internal static class ZoomMathTests
    {
        static int checks;
        static void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
        static void Main()
        {
            Check(ZoomMath.ShouldAnchor(true, false, 1000, 900), "Default anchors zoom in");
            Check(!ZoomMath.ShouldAnchor(true, false, 900, 1000), "Default preserves standard zoom out");
            Check(ZoomMath.ShouldAnchor(true, true, 900, 1000), "Optional cursor zoom out");
            Check(!ZoomMath.ShouldAnchor(false, true, 1000, 900), "Disabled blocks zoom in");
            Check(!ZoomMath.ShouldAnchor(false, true, 900, 1000), "Disabled blocks zoom out");
            Check(!ZoomMath.ShouldAnchor(true, true, 10, 10), "Zoom limit causes no pan");
            Check(!ZoomMath.ShouldAnchor(true, true, float.NaN, 100), "Reject NaN");
            Check(!ZoomMath.ShouldAnchor(true, true, 100, float.PositiveInfinity), "Reject infinity");
            Check(!ZoomMath.TryPlaneCorrection(new float3(0,100,0), new float3(1,0,0), float3.zero, out _), "Reject horizon");
            Check(!ZoomMath.TryPlaneCorrection(new float3(0,100,0), new float3(0,1,0), float3.zero, out _), "Reject sky");
            foreach (float pitch in new[]{25f,45f,65f,85f})
            foreach (float yaw in new[]{0f,90f,180f,270f})
            foreach (float x in new[]{-0.5f,0f,0.5f})
            foreach (float y in new[]{-0.2f,0f,0.2f})
            foreach (float ratio in new[]{0.8f,1.2f})
            {
                quaternion rotation = quaternion.EulerXYZ(math.radians(new float3(pitch,yaw,0)));
                float3 direction=math.normalize(math.mul(rotation,new float3(x,y,1)));
                float3 origin=math.mul(rotation,new float3(0,0,-1000));
                float3 anchor=origin+direction*(-origin.y/direction.y);
                float3 predicted=origin*ratio;
                Check(ZoomMath.TryPlaneCorrection(predicted,direction,anchor,out float3 correction),"Valid oblique projection");
                float3 actual=predicted+correction+direction*(-predicted.y/direction.y);
                Check(math.distance(actual,anchor)<0.003f,"Anchor preserved across pitch/yaw/cursor/direction");
            }
            // Real requirements: endpoint timing, monotonicity, reversibility,
            // independence from wheel segmentation, and manual input authority.
            foreach (float initial in new[]{-30f,0f,20f,60f,89f})
            {
                var motion = new TiltMotion();
                float z=10000, pitch=initial;
                for (int i=1;i<=100;i++)
                {
                    float next=i==100?10f:(float)(10000*Math.Pow(.001,i/100.0));
                    float p=motion.Step(z,next,10,pitch);
                    Check(p>=pitch && p<=90,"Tilt moves monotonically toward overhead");
                    Check(i==100 || p<90,"Overhead is not reached before zoom endpoint");
                    z=next; pitch=p;
                }
                Check(pitch==90,"Exact overhead on the same step as minimum zoom");
                Check(motion.Step(10,10,10,35)==35,"Manual tilt at full zoom is not clamped");
            }
            var reversible = new TiltMotion();
            float middle=reversible.Step(1000,100,10,20);
            Check(Math.Abs(middle-55)<.0001,"Even midpoint in logarithmic zoom distance");
            Check(Math.Abs(reversible.Step(100,1000,10,middle)-20)<.0001,"Zoom out retraces original angle");
            var segmented=new TiltMotion();
            float a=segmented.Step(1000,400,10,20);
            a=segmented.Step(400,100,10,a);
            Check(Math.Abs(a-middle)<.0001,"Wheel segmentation does not alter tilt trajectory");
            Check(segmented.Step(100,100,10,15)==15,"Manual tilt while idle stays untouched");
            Check(segmented.Step(100,10,10,15)==90,"Next zoom uses manually selected angle and reaches endpoint");
            var stationary=new TiltMotion();
            Check(stationary.Step(1000,1000,10,5)==5,"Enabling option without zoom never snaps angle");
            Check(stationary.Step(1000,2000,10,5)==5,"Zooming beyond start keeps chosen wide angle");
            stationary.Reset();
            Check(stationary.Step(2000,2000,10,-15)==-15,"Reset preserves angle");
            Check(stationary.Step(float.NaN,100,10,20)==20,"Invalid zoom leaves angle untouched");
            Console.WriteLine(checks+" geometry, direction and tilt checks passed.");
        }
    }
}
