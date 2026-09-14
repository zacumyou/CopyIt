using System;
using CopyIt;
using Unity.Mathematics;

struct Terrain : IHeightSampler
{
    public float A, B, C, Bend;
    public int Samples;
    public float Height(float3 p) { Samples++; return A * p.x + B * p.z + C + Bend * p.x * p.x * p.x; }
}
struct InvalidTerrain : IHeightSampler { public float Height(float3 p) => float.NaN; }
static class Program
{
    static int checks;
    static void Check(bool pass, string name) { if (!pass) throw new Exception(name); checks++; }
    static float3 Up(quaternion q) => math.rotate(q, math.up());
    static void Main()
    {
        var random = new System.Random(47381);
        for (int i = 0; i < 2000; i++)
        {
            var t = new Terrain { A = (float)(random.NextDouble() * 4 - 2), B = (float)(random.NextDouble() * 4 - 2), C = 40 };
            var p = new float3((float)random.NextDouble() * 200 - 100, 50, (float)random.NextDouble() * 200 - 100);
            var yaw = quaternion.RotateY((float)random.NextDouble() * 6.2831853f);
            var size = new float2(2 + (float)random.NextDouble() * 40, 2 + (float)random.NextDouble() * 50);
            Check(SlopeMath.TryAlign(ref t, p, yaw, -size, size, out var result), "plane fit");
            Check(math.distance(Up(result), math.normalize(new float3(-t.A, 1, -t.B))) < 0.0002f, "plane normal");
            Check(math.distance(math.normalize(math.forward(result).xz), math.forward(yaw).xz) < 0.0001f, "yaw preserved");
            Check(math.abs(math.length(result.value) - 1) < 0.00001f, "unit quaternion");
            Check(t.Samples == 75, "bounded sampling");
            Check(SlopeMath.TryAlign(ref t, p, result, -size, size, out var repeated), "repeat fit");
            Check(math.distance(Up(result), Up(repeated)) < 0.0002f, "no accumulated tilt");
        }
        var flat = new Terrain { C = 97 };
        var heading = quaternion.RotateY(1.25f);
        Check(SlopeMath.TryAlign(ref flat, new float3(100,97,100), heading, new float2(-4,-10), new float2(4,10), out var level), "flat valid");
        Check(math.abs(math.dot(level.value, heading.value)) > 0.999999f, "flat unchanged");
        var curved = new Terrain { Bend = 0.005f };
        Check(SlopeMath.TryAlign(ref curved, new float3(0,0,0), quaternion.identity, new float2(-1,-1), new float2(1,1), out var small), "small footprint");
        Check(SlopeMath.TryAlign(ref curved, new float3(0,0,0), quaternion.identity, new float2(-8,-8), new float2(8,8), out var large), "large footprint");
        Check(math.distance(Up(small), Up(large)) > 0.1f, "size changes fitted slope");
        Check(SlopeMath.TryAlign(ref curved, new float3(0,0,0), quaternion.identity, new float2(-8,-1), new float2(8,1), out var along), "long x");
        Check(SlopeMath.TryAlign(ref curved, new float3(0,0,0), quaternion.RotateY(math.PI / 2), new float2(-8,-1), new float2(8,1), out var across), "long z");
        Check(math.distance(Up(along), Up(across)) > 0.1f, "orientation changes footprint");
        Check(SlopeMath.TryAlign(ref curved, new float3(0,0,0), quaternion.identity, new float2(4,-1), new float2(8,1), out var offset), "off-center mesh");
        Check(math.distance(Up(offset), Up(small)) > 0.1f, "pivot offset respected");
        var steep = new Terrain { A = 1000 };
        Check(SlopeMath.TryAlign(ref steep, new float3(0), quaternion.identity, new float2(-10), new float2(10), out var capped), "steep finite");
        Check(Up(capped).y > 0.087f, "85 degree limit");
        var invalid = new InvalidTerrain();
        Check(!SlopeMath.TryAlign(ref invalid, new float3(0), heading, new float2(-1), new float2(1), out _), "invalid terrain rejected");
        Check(!SlopeMath.TryAlign(ref flat, new float3(0), heading, new float2(0), new float2(0), out _), "empty bounds rejected");
        Check(!SlopeMath.TryAlign(ref flat, new float3(float.NaN), heading, new float2(-1), new float2(1), out _), "invalid position rejected");
        Check(!SlopeMath.TryAlign(ref flat, new float3(0), default, new float2(-1), new float2(1), out _), "invalid rotation rejected");
        Console.WriteLine($"PASS: {checks} checks; 2000 randomized planes; flat, curved, size, yaw, offset, repeat, and invalid-input cases.");
    }
}
