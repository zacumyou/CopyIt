using Unity.Mathematics;
namespace CopyIt {
internal static class ReferenceMath {
    internal static bool IsFlat(float3 size)=>math.min(size.x,size.z)>.1f&&size.y<=math.min(size.x,size.z)*.3f;
    internal static bool Hit(float3 origin,float3 direction,float3 min,float3 max,out float distance){
        float near=0,far=float.MaxValue;distance=0;
        if(!math.all(math.isfinite(origin))||!math.all(math.isfinite(direction))||math.lengthsq(direction)<.000001f)return false;
        for(int axis=0;axis<3;axis++){
            if(math.abs(direction[axis])<.000001f){if(origin[axis]<min[axis]||origin[axis]>max[axis])return false;continue;}
            float a=(min[axis]-origin[axis])/direction[axis],b=(max[axis]-origin[axis])/direction[axis];
            near=math.max(near,math.min(a,b));far=math.min(far,math.max(a,b));if(near>far)return false;
        }
        distance=near;return true;
    }
}
}
