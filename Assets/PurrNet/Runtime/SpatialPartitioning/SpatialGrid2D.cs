using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace PurrNet
{
    [BurstCompile(CompileSynchronously = true)]
    public struct SpatialGrid2D : IDisposable
    {
        private NativeArray<NativeList<NativeNetworkID>> _cells;

        private readonly float _cellSize;
        private readonly float _cellSizeInv;

        private readonly int _width;
        private readonly int _depth;
        private readonly int2 _halfSize;

        public SpatialGrid2D(int width, int depth, float cellSize)
        {
            _width = width;
            _depth = depth;
            _halfSize = new int2(width / 2, depth / 2);
            _cells = new NativeArray<NativeList<NativeNetworkID>>(width * depth, Allocator.Persistent);
            _cellSize = cellSize;
            _cellSizeInv = 1f / cellSize;
        }

        [BurstCompile(CompileSynchronously = true)]
        public static void Compare(in NativeNetworkID a, in NativeNetworkID b, out int comparison)
        {
            if (a.playerId > b.playerId)
            {
                comparison = 1;
            }
            else if (a.playerId < b.playerId)
            {
                comparison = -1;
            }
            else
            {
                if (a.netId > b.netId)
                {
                    comparison = 1;
                }
                else if (a.netId < b.netId)
                {
                    comparison = -1;
                }
                else
                {
                    comparison = 0;
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        public static void BinarySearch(ref NativeList<NativeNetworkID> list, in NativeNetworkID value, out int index)
        {
            int low = 0;
            int high = list.Length - 1;

            while (low <= high)
            {
                int mid = (low + high) / 2;

                Compare(list[mid], value, out var firstComp);

                switch (firstComp)
                {
                    case < 0:
                        low = mid + 1;
                        break;
                    case > 0:
                        high = mid - 1;
                        break;
                    default:
                        index = mid;
                        return;
                }
            }

            index = low;
        }

        [BurstCompile(CompileSynchronously = true)]
        public static void InsertSorted(ref NativeList<NativeNetworkID> list, in NativeNetworkID value)
        {

        }

        [BurstCompile(CompileSynchronously = true)]
        public static void Add(ref SpatialGrid2D grid, in EntityBounds bounds, in NativeNetworkID value)
        {
            var minCellIndex = math.int2(math.floor(bounds.min * grid._cellSizeInv).xz) + grid._halfSize;
            var maxCellIndex = math.int2(math.floor(bounds.max * grid._cellSizeInv).xz) + grid._halfSize;

            minCellIndex = math.clamp(minCellIndex, int2.zero, grid._halfSize);
            maxCellIndex = math.clamp(maxCellIndex, int2.zero, grid._halfSize);

            for (var x = minCellIndex.x; x <= maxCellIndex.x; x++)
            {
                for (var y = minCellIndex.y; y <= maxCellIndex.y; y++)
                {
                    var cellIndex = x + y * grid._width;
                    var list = grid._cells[cellIndex];
                    InsertSorted(ref list, value);
                    grid._cells[cellIndex] = list;
                }
            }
        }

        [BurstDiscard]
        public void Dispose()
        {
            if (!_cells.IsCreated) return;

            int len = _cells.Length;
            for (var i = 0; i < len; i++)
                _cells[i].Dispose();
            _cells.Dispose();
        }
    }
}
