using UnityEngine;

namespace PurrNet
{
    public class SpatialGrid : MonoBehaviour
    {
        [SerializeField] private Vector2 _worldSize = new Vector2(1000, 1000);
        [SerializeField] private float _cellSize = 16;

        private SpatialGrid2D _grid;

        private void Awake()
        {
            int width = Mathf.CeilToInt(_worldSize.x / _cellSize);
            int depth = Mathf.CeilToInt(_worldSize.y / _cellSize);
            _grid = new SpatialGrid2D(width, depth, _cellSize);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireCube(transform.position, new Vector3(_worldSize.x, 0, _worldSize.y));
        }

        private void OnDestroy()
        {
            _grid.Dispose();
        }
    }
}
