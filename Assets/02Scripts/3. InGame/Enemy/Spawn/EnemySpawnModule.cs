using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace alpha.ingame.enemy
{
    /// <summary>씬에 지정한 사각 영역에서 바닥을 찾아 적을 한 번 생성한다.</summary>
    public sealed class EnemySpawnModule : MonoBehaviour
    {
        [Header("Spawn Area")]
        [SerializeField, Min(0.01f), InspectorName("Area Size X")]
        private float _areaSizeX = 20f;
        [SerializeField, Min(0.01f), InspectorName("Area Size Z")]
        private float _areaSizeZ = 20f;
        [SerializeField, Min(0.01f)] private float _rayDistance = 30f;
        [SerializeField] private LayerMask _groundLayers = 1 << 6;
        [SerializeField, Min(0f)] private float _spawnYOffset = 10f;
        [SerializeField, Min(0f)] private float _maxGroundHeightDifference = 0.5f;

        [Header("Enemies")]
        [SerializeField] private GameObject[] _enemyPrefabs = new GameObject[0];
        [SerializeField] private LayerMask _enemyLayers = 1 << 9;
        [SerializeField, Min(0)] private int _spawnCount;
        [SerializeField, Min(1)] private int _maxAttemptsPerEnemy = 20;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private bool _hasSpawned;

        public IReadOnlyList<GameObject> Spawned => _spawned;

        private void Start() => SpawnAll();

        public void SpawnAll()
        {
            if (_hasSpawned || _spawnCount <= 0) return;
            _hasSpawned = true;

            List<GameObject> validPrefabs = new List<GameObject>();
            if (_enemyPrefabs != null)
                foreach (GameObject prefab in _enemyPrefabs)
                    if (prefab != null) validPrefabs.Add(prefab.transform.root.gameObject);

            if (validPrefabs.Count == 0)
            {
                Debug.LogWarning($"{name}: 생성할 적 프리팹이 없습니다.", this);
                return;
            }

            if (_groundLayers.value == 0)
            {
                Debug.LogWarning($"{name}: Ground Layers가 비어 있어 지면을 찾을 수 없습니다.", this);
                return;
            }
            if (!HasActiveGroundCollider())
            {
                Debug.LogWarning($"{name}: Ground Layers에 속한 활성 지면 콜라이더를 찾지 못했습니다. 지면 오브젝트의 Layer를 확인하세요.", this);
                return;
            }

            EnemySpawnArea area = new EnemySpawnArea(transform.position, _areaSizeX, _areaSizeZ,
                _spawnYOffset, _rayDistance, _maxGroundHeightDifference, _groundLayers,
                _enemyLayers, _maxAttemptsPerEnemy);
            for (int i = 0; i < _spawnCount; i++)
            {
                GameObject prefab = validPrefabs[Random.Range(0, validPrefabs.Count)];
                if (!TrySpawnEnemy(prefab, area, out GameObject enemy, out string reason))
                {
                    Debug.LogWarning($"{name}: {i + 1}번째 적을 생성하지 못했습니다. {reason}", this);
                    continue;
                }

                // 생성 범위 값은 Core로 전달하고 순찰 기능은 직접 참조하지 않는다.
                enemy.GetComponent<EnemyRoot>()?.ConfigurePatrolArea(
                    transform.position, _areaSizeX, _areaSizeZ);
                _spawned.Add(enemy);
            }
        }

        private bool HasActiveGroundCollider()
        {
            // 비활성 맵이나 트리거는 레이캐스트 대상 지면으로 간주하지 않는다.
            foreach (Collider collider in FindObjectsByType<Collider>(FindObjectsSortMode.None))
                if (collider.enabled && !collider.isTrigger && collider.gameObject.activeInHierarchy &&
                    (_groundLayers.value & (1 << collider.gameObject.layer)) != 0)
                    return true;

            return false;
        }

        private bool TrySpawnEnemy(GameObject prefab, EnemySpawnArea area,
            out GameObject enemy, out string reason)
        {
            enemy = null;
            reason = "생성 영역에서 Ground Layers의 바닥을 레이캐스트로 찾지 못했습니다.";
            Collider bodyCollider = null;
            NavMeshAgent agent = null;

            // 오브젝트 위치를 기준으로 지역별 레이 시작 높이를 정한다.
            if (area.RayDistance <= 0f)
            {
                reason = "Spawn Y Offset이 0이라 바닥을 탐색할 수 없습니다.";
                return false;
            }

            for (int attempt = 0; attempt < area.MaxAttempts; attempt++)
            {
                Vector3 origin = area.RandomRayOrigin();
                Quaternion rotation = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up)
                    * prefab.transform.rotation;

                // 후보 위치와 Y축 회전을 적용한 뒤 실제 콜라이더 바운드를 측정한다.
                if (enemy == null)
                {
                    enemy = Instantiate(prefab, origin, rotation);
                    bodyCollider = EnemySpawnArea.FindBodyCollider(enemy);
                    agent = enemy.GetComponent<NavMeshAgent>();
                }
                else enemy.transform.SetPositionAndRotation(origin, rotation);

                Physics.SyncTransforms();
                if (bodyCollider == null)
                {
                    reason = $"{prefab.name} 인스턴스의 루트와 자식에서 본체 콜라이더를 찾지 못했습니다.";
                    break;
                }
                if (agent == null)
                {
                    reason = $"{prefab.name} 루트에 NavMeshAgent가 없습니다.";
                    break;
                }
                if (bodyCollider.bounds.size.sqrMagnitude <= 0f)
                {
                    reason = $"{prefab.name}의 본체 콜라이더 바운드가 비어 있습니다.";
                    break;
                }

                Bounds bodyBounds = bodyCollider.bounds;
                if (!area.IsInside(bodyBounds))
                {
                    reason = "콜라이더 전체가 생성 영역 안에 들어가지 않습니다.";
                    continue;
                }

                // 바닥 범위의 중앙과 가장자리가 모두 같은 높이의 지면을 지지해야 한다.
                if (!area.TryFindGround(bodyBounds, out float groundY, out bool centerHit))
                {
                    reason = centerHit ? "콜라이더 크기만큼 평탄한 바닥이 없습니다."
                        : "생성 영역에서 Ground Layers의 바닥을 레이캐스트로 찾지 못했습니다.";
                    continue;
                }

                Vector3 snappedPosition = enemy.transform.position;
                snappedPosition.y += groundY - bodyBounds.min.y;
                enemy.transform.position = snappedPosition;
                Physics.SyncTransforms();

                // 지면 위의 NavMesh에 정렬해 에이전트가 즉시 이동할 수 있게 한다.
                if (!NavMesh.SamplePosition(snappedPosition, out NavMeshHit navHit, 1f,
                        agent.areaMask) || !agent.Warp(navHit.position))
                {
                    reason = "생성 지점 근처에 NavMesh가 없습니다.";
                    continue;
                }
                Physics.SyncTransforms();
                if (!area.IsInside(bodyCollider.bounds))
                {
                    reason = "NavMesh 위 생성 위치가 영역 밖입니다.";
                    continue;
                }

                if (area.OverlapsEnemy(bodyCollider.bounds, enemy.transform))
                {
                    reason = "기존 적과 겹치지 않는 위치를 찾지 못했습니다.";
                    continue;
                }

                reason = null;
                return true;
            }

            if (enemy != null)
            {
                enemy.SetActive(false);
                Destroy(enemy);
                enemy = null;
            }
            return false;
        }

        private void OnValidate()
        {
            _areaSizeX = Mathf.Max(0.01f, _areaSizeX);
            _areaSizeZ = Mathf.Max(0.01f, _areaSizeZ);
            _rayDistance = Mathf.Max(0.01f, _rayDistance);
            _spawnYOffset = Mathf.Max(0f, _spawnYOffset);
            _maxGroundHeightDifference = Mathf.Max(0f, _maxGroundHeightDifference);
            _spawnCount = Mathf.Max(0, _spawnCount);
            _maxAttemptsPerEnemy = Mathf.Max(1, _maxAttemptsPerEnemy);
        }

        private void OnDrawGizmos()
        {
            Vector3 center = transform.position + Vector3.up * (_spawnYOffset * 0.5f);
            Vector3 size = new Vector3(Mathf.Max(0.01f, _areaSizeX),
                Mathf.Max(0.05f, Mathf.Abs(_spawnYOffset)), Mathf.Max(0.01f, _areaSizeZ));
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.55f);
            Gizmos.DrawWireCube(center, size);
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = transform.position + Vector3.up * (_spawnYOffset * 0.5f);
            Vector3 size = new Vector3(Mathf.Max(0.01f, _areaSizeX),
                Mathf.Max(0.05f, Mathf.Abs(_spawnYOffset)), Mathf.Max(0.01f, _areaSizeZ));
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.2f);
            Gizmos.DrawCube(center, size);
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireCube(center, size);
            Gizmos.DrawSphere(transform.position + Vector3.up * _spawnYOffset, 0.15f);
        }
    }
}
