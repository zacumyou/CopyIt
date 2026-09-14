// KO: 게임 ECS와 분리된 높이 계산입니다. 동일한 코드를 회귀 테스트에서 실행합니다.
// EN: Height policy is independent of game ECS and is exercised directly by regression tests.
using System;

namespace CopyIt
{
    // Pure placement policies, shared with tests.
    public static class PlacementMath
    {
        internal static System.Tuple<float,float> FitTerrainCurve(float a,float h1,float h2,float d)
        {
            float r1=27*h1-8*a-d,r2=27*h2-a-8*d;
            return System.Tuple.Create((2*r1-r2)/18f,(2*r2-r1)/18f);
        }

        public static float Height(float sourceY, float minimumY, float terrainAtAnchor, bool absolute)
        {
            if (!Finite(sourceY) || !Finite(minimumY) || !Finite(terrainAtAnchor))
                throw new ArgumentException("Non-finite height");
            return absolute ? sourceY : terrainAtAnchor + (sourceY - minimumY);
        }
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static bool InBox(float x, float y, float ax, float ay, float bx, float by) =>
            x >= Math.Min(ax,bx) && x <= Math.Max(ax,bx) && y >= Math.Min(ay,by) && y <= Math.Max(ay,by);
    }
}
