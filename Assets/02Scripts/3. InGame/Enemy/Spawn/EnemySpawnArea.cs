using UnityEngine;

namespace alpha.ingame.enemy
{
    /// <summary>적 생성에 필요한 영역 및 지면 판정 값이다.</summary>
    public readonly struct EnemySpawnArea
    {
        private readonly Vector3 _center;
        private readonly float _sizeX;
        private readonly float _sizeZ;
        private readonly float _rayTopY;
        private readonly float _rayDistance;
        private readonly float _maxGroundHeightDifference;
        private readonly LayerMask _groundLayers;
        private readonly LayerMask _enemyLayers;

        public int MaxAttempts { get; }
        public float RayDistance => _rayDistance;

        public EnemySpawnArea(Vector3 center, float sizeX, float sizeZ, float spawnYOffset,
            float rayDistance, float maxGroundHeightDifference, LayerMask groundLayers,
            LayerMask enemyLayers, int maxAttempts)
        {
            _center = center;
            _sizeX = sizeX;
            _sizeZ = sizeZ;
            _rayTopY = center.y + spawnYOffset;
            _rayDistance = Mathf.Min(rayDistance, spawnYOffset);
            _maxGroundHeightDifference = maxGroundHeightDifference;
            _groundLayers = groundLayers;
            _enemyLayers = enemyLayers;
            MaxAttempts = maxAttempts;
        }

        public Vector3 RandomRayOrigin() => new Vector3(
            _center.x + Random.Range(-_sizeX * 0.5f, _sizeX * 0.5f),
            _rayTopY,
            _center.z + Random.Range(-_sizeZ * 0.5f, _sizeZ * 0.5f));

        public bool IsInside(Bounds bounds) =>
            bounds.min.x >= _center.x - _sizeX * 0.5f &&
            bounds.max.x <= _center.x + _sizeX * 0.5f &&
            bounds.min.z >= _center.z - _sizeZ * 0.5f &&
            bounds.max.z <= _center.z + _sizeZ * 0.5f;

        public bool TryFindGround(Bounds bounds, out float groundY, out bool centerHit)
        {
            Vector3 origin = new Vector3(bounds.center.x, _rayTopY, bounds.center.z);
            centerHit = Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                _rayDistance, _groundLayers, QueryTriggerInteraction.Ignore);
            groundY = centerHit ? hit.point.y : 0f;
            if (!centerHit) return false;

            // 콜라이더 바닥의 중앙과 가장자리 높이를 함께 검사한다.
            for (int x = 0; x < 3; x++)
                for (int z = 0; z < 3; z++)
                {
                    if (x == 1 && z == 1) continue;
                    origin.x = Mathf.Lerp(bounds.min.x, bounds.max.x, x * 0.5f);
                    origin.z = Mathf.Lerp(bounds.min.z, bounds.max.z, z * 0.5f);
                    if (!Physics.Raycast(origin, Vector3.down, out hit,
                            _rayDistance, _groundLayers, QueryTriggerInteraction.Ignore) ||
                        Mathf.Abs(hit.point.y - groundY) > _maxGroundHeightDifference)
                        return false;
                }

            return true;
        }

        public bool OverlapsEnemy(Bounds bounds, Transform candidateRoot)
        {
            Collider[] overlaps = Physics.OverlapBox(bounds.center, bounds.extents,
                Quaternion.identity, _enemyLayers, QueryTriggerInteraction.Ignore);
            foreach (Collider collider in overlaps)
                if (!collider.transform.IsChildOf(candidateRoot) && bounds.Intersects(collider.bounds))
                    return true;
            return false;
        }

        public static Collider FindBodyCollider(GameObject enemy)
        {
            // 본체가 있는 루트를 먼저 확인한다.
            foreach (Collider collider in enemy.GetComponents<Collider>())
                if (collider.enabled && !collider.isTrigger) return collider;

            foreach (Collider collider in enemy.GetComponentsInChildren<Collider>(true))
                if (collider.gameObject != enemy && collider.enabled && !collider.isTrigger)
                    return collider;
            return null;
        }
    }
}
