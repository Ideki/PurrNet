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

        private readonly float _cellSizeInv;
        private readonly int _width;
        private readonly int2 _halfSize;

        public SpatialGrid2D(int width, int depth, float cellSize)
        {
            _width = width;
            _halfSize = new int2(width / 2, depth / 2);
            _cells = new NativeArray<NativeList<NativeNetworkID>>(width * depth, Allocator.Persistent);
            _cellSizeInv = 1f / cellSize;
        }

        [BurstCompile(CompileSynchronously = true)]
        static void Compare(in NativeNetworkID a, in NativeNetworkID b, out int comparison)
        {
            comparison = a.netId != b.netId
                ? a.netId.CompareTo(b.netId)
                : a.playerId.CompareTo(b.playerId);
        }

        [BurstCompile(CompileSynchronously = true)]
        static void BinarySearch(
            ref NativeList<NativeNetworkID> list,
            in NativeNetworkID value,
            out int index,
            out bool found
        )
        {
            int low = 0;
            int high = list.Length - 1;
            found = false;

            while (low <= high)
            {
                int mid = (low + high) >> 1;
                Compare(list[mid], value, out var cmp);

                int lessMask = cmp < 0 ? 1 : 0;
                int greaterMask = cmp > 0 ? 1 : 0;
                int equalMask = 1 - (lessMask | greaterMask);

                low = lessMask * (mid + 1) + (1 - lessMask) * low;
                high = greaterMask * (mid - 1) + (1 - greaterMask) * high;

                if (equalMask != 0)
                {
                    index = mid;
                    found = true;
                    return;
                }
            }

            index = low;
        }

        [BurstCompile(CompileSynchronously = true)]
        static void InsertSorted(ref NativeList<NativeNetworkID> list, in NativeNetworkID value)
        {
            BinarySearch(ref list, value, out var index, out var found);

            if (found)
                return;

            list.InsertRange(index, 1);
            list[index] = value;
        }

        [BurstCompile(CompileSynchronously = true)]
        static void RemoveEntry(ref NativeList<NativeNetworkID> list, in NativeNetworkID value)
        {
            BinarySearch(ref list, value, out var index, out var found);

            if (found)
            {
                list.RemoveAt(index);
                if (list.Length == 0)
                    list.Dispose();
            }
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

                    if (!list.IsCreated)
                        list = new NativeList<NativeNetworkID>(8, Allocator.Persistent);

                    InsertSorted(ref list, value);
                    grid._cells[cellIndex] = list;
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        public static void Remove(ref SpatialGrid2D grid, in EntityBounds bounds, in NativeNetworkID value)
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

                    if (!list.IsCreated)
                        continue;

                    RemoveEntry(ref list, value);
                    grid._cells[cellIndex] = list;
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        public static void Move(ref SpatialGrid2D grid, in EntityBounds oldBounds, in EntityBounds newBounds, in NativeNetworkID value)
        {
            var oldMin = math.int2(math.floor(oldBounds.min * grid._cellSizeInv).xz) + grid._halfSize;
            var oldMax = math.int2(math.floor(oldBounds.max * grid._cellSizeInv).xz) + grid._halfSize;
            var newMin = math.int2(math.floor(newBounds.min * grid._cellSizeInv).xz) + grid._halfSize;
            var newMax = math.int2(math.floor(newBounds.max * grid._cellSizeInv).xz) + grid._halfSize;

            oldMin = math.clamp(oldMin, int2.zero, grid._halfSize);
            oldMax = math.clamp(oldMax, int2.zero, grid._halfSize);
            newMin = math.clamp(newMin, int2.zero, grid._halfSize);
            newMax = math.clamp(newMax, int2.zero, grid._halfSize);

            bool areEqual = oldMin.x == newMin.x && oldMin.y == newMin.y && oldMax.x == newMax.x && oldMax.y == newMax.y;

            if (areEqual)
                return;

            // remove old
            for (var x = oldMin.x; x <= oldMax.x; x++)
            {
                for (var y = oldMin.y; y <= oldMax.y; y++)
                {
                    var cellIndex = x + y * grid._width;
                    var list = grid._cells[cellIndex];

                    if (!list.IsCreated)
                        continue;

                    bool isInsideNewBounds = x >= newMin.x && x <= newMax.x && y >= newMin.y && y <= newMax.y;

                    if (isInsideNewBounds)
                        continue;

                    RemoveEntry(ref list, value);
                    grid._cells[cellIndex] = list;
                }
            }

            // add new
            for (var x = newMin.x; x <= newMax.x; x++)
            {
                for (var y = newMin.y; y <= newMax.y; y++)
                {
                    var cellIndex = x + y * grid._width;
                    var list = grid._cells[cellIndex];

                    if (!list.IsCreated)
                        list = new NativeList<NativeNetworkID>(8, Allocator.Persistent);

                    bool isInsideOldBounds = x >= oldMin.x && x <= oldMax.x && y >= oldMin.y && y <= oldMax.y;

                    if (isInsideOldBounds)
                        continue;

                    InsertSorted(ref list, value);
                    grid._cells[cellIndex] = list;
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        public static void Query(ref SpatialGrid2D grid, in EntityBounds bounds, ref NativeList<NativeNetworkID> results)
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
                    if (!list.IsCreated)
                        continue;
                    results.AddRange(list.AsArray());
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        public static void Query(ref SpatialGrid2D grid, in float3 position, ref NativeList<NativeNetworkID> results)
        {
            var cellX = (int)math.floor(position.x * grid._cellSizeInv) + grid._halfSize.x;
            var cellY = (int)math.floor(position.z * grid._cellSizeInv) + grid._halfSize.y;

            var cellIndex = cellX + cellY * grid._width;
            var list = grid._cells[cellIndex];
            if (!list.IsCreated)
                return;

            results.AddRange(list.AsArray());
        }

        [BurstCompile(CompileSynchronously = true)]
        public static void Clear(ref SpatialGrid2D grid)
        {
            for (var i = 0; i < grid._cells.Length; i++)
            {
                if (grid._cells[i].IsCreated)
                    grid._cells[i].Dispose();
            }
        }

        [BurstDiscard]
        public void Dispose()
        {
            if (!_cells.IsCreated) return;

            int len = _cells.Length;
            for (var i = 0; i < len; i++)
            {
                if (_cells[i].IsCreated)
                    _cells[i].Dispose();
            }
            _cells.Dispose();
        }
    }
}
