// KO: 데칼 영역의 지형을 표본 추출해 평면을 맞춥니다. 반복마다 원본 방향을 사용해 기울기 누적을 방지합니다.
// EN: Fits a plane to sampled terrain under the decal. Original heading prevents accumulated tilt.
using System;
using Unity.Mathematics;

namespace CopyIt
{
    public interface IHeightSampler
    {
        float Height(float3 position);
    }

    /// <summary>Deterministic least-squares terrain plane over the projected decal footprint.</summary>
    public static class SlopeMath
    {
        public static bool TryAlign<T>(ref T terrain, float3 position, quaternion input,
            float2 boundsMin, float2 boundsMax, out quaternion rotation) where T : struct, IHeightSampler
        {
            rotation = input;
            if (!math.all(math.isfinite(position)) || !math.all(math.isfinite(input.value)) ||
                math.lengthsq(input.value) < 0.5f || !math.all(math.isfinite(boundsMin)) ||
                !math.all(math.isfinite(boundsMax)) || math.any(boundsMax - boundsMin < 0.01f)) return false;

            float3 heading = math.forward(math.normalize(input));
            heading.y = 0;
            if (math.lengthsq(heading) < 1e-6f) return false;
            heading = math.normalize(heading);
            // Start from yaw every time; never accumulate pitch/roll across hover frames.
            quaternion fit = quaternion.LookRotationSafe(heading, math.up());
            for (int pass = 0; pass < 3; pass++)
            {
                double sx = 0, sz = 0, sy = 0, sxx = 0, szz = 0, sxz = 0, sxy = 0, szy = 0;
                const int count = 25;
                for (int row = 0; row < 5; row++)
                for (int col = 0; col < 5; col++)
                {
                    float2 local = math.lerp(boundsMin, boundsMax, new float2(col, row) * 0.25f);
                    float3 offset = math.rotate(fit, new float3(local.x, 0, local.y));
                    float height = terrain.Height(position + offset);
                    if (!math.isfinite(height)) return false;
                    double x = offset.x, z = offset.z, y = (double)height - position.y;
                    sx += x; sz += z; sy += y;
                    sxx += x * x; szz += z * z; sxz += x * z; sxy += x * y; szy += z * y;
                }
                double xx = sxx - sx * sx / count, zz = szz - sz * sz / count;
                double xz = sxz - sx * sz / count;
                double xy = sxy - sx * sy / count, zy = szy - sz * sy / count;
                double determinant = xx * zz - xz * xz;
                if (determinant <= 1e-12 || double.IsNaN(determinant)) return false;
                float2 gradient = new float2((float)((xy * zz - zy * xz) / determinant),
                    (float)((zy * xx - xy * xz) / determinant));
                if (!math.all(math.isfinite(gradient))) return false;
                // A terrain heightfield has no true vertical face. Cap at 85 degrees to avoid singular fits.
                float length = math.length(gradient);
                const float maxGradient = 11.4300523f;
                if (length > maxGradient) gradient *= maxGradient / length;
                float3 normal = math.normalize(new float3(-gradient.x, 1, -gradient.y));
                float3 tangent = new float3(heading.x, math.dot(gradient, heading.xz), heading.z);
                fit = quaternion.LookRotationSafe(math.normalize(tangent), normal);
            }
            rotation = fit;
            return math.all(math.isfinite(rotation.value));
        }
    }
}
