using Unity.Burst;
using Unity.Mathematics;

namespace PurrNet
{
    [BurstCompile(CompileSynchronously = true)]
    public readonly struct EntityBounds
    {
        public readonly float3 min;
        public readonly float3 max;

        public EntityBounds(float3 min, float3 max)
        {
            this.min = min;
            this.max = max;
        }

        public float3 center => (min + max) * 0.5f;
        public float3 size => max - min;
    }
}