using System.Collections.Generic;
namespace CopyIt
{
    // KO: 순서는 무관하지만 새 연결, 누락 및 중복은 허용하지 않습니다.
    // EN: Order may change; new, missing or duplicate connections must not pass.
    internal static class NetworkReferencePolicy
    {
        // Squared metres: tolerate native floating-point regeneration, never NaN/infinity.
        internal static bool WithinPositionTolerance(float squaredDistance)=>squaredDistance>=0f&&squaredDistance<=0.0001f;
        internal static bool SameConnections<T>(IList<T> expected,IList<T> actual)
        {
            if(expected.Count!=actual.Count)return false;
            var remaining=new List<T>(expected);
            foreach(var value in actual)if(!remaining.Remove(value))return false;
            return remaining.Count==0;
        }
    }
}
