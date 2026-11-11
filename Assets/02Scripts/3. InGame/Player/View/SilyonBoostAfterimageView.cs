using UnityEngine;
using UnityEngine.Rendering;

namespace alpha.ingame.player
{
    /// <summary>부스터 이동 중 현재 자세를 복제해 월드에 발광 잔상을 남긴다.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(150)]
    public sealed class SilyonBoostAfterimageView : MonoBehaviour
    {
        private sealed class AfterimageFrame
        {
            public readonly GameObject Root;
            public readonly Mesh[] Meshes;
            public readonly MeshRenderer[] Renderers;
            public readonly MaterialPropertyBlock Properties = new();
            public float SpawnedAt;

            public AfterimageFrame(int index, SkinnedMeshRenderer[] sources, Material material)
            {
                Root = new GameObject($"SilyonAfterimage_{index}");
                Meshes = new Mesh[sources.Length];
                Renderers = new MeshRenderer[sources.Length];

                for (int i = 0; i < sources.Length; i++)
                {
                    GameObject part = new($"Part_{i}");
                    part.transform.SetParent(Root.transform, false);
                    Mesh mesh = new() { name = $"SilyonAfterimageMesh_{index}_{i}" };
                    mesh.MarkDynamic();
                    part.AddComponent<MeshFilter>().sharedMesh = mesh;

                    MeshRenderer renderer = part.AddComponent<MeshRenderer>();
                    int submeshCount = Mathf.Max(1,
                        sources[i].sharedMesh != null ? sources[i].sharedMesh.subMeshCount : 1);
                    Material[] materials = new Material[submeshCount];
                    for (int j = 0; j < materials.Length; j++) materials[j] = material;
                    renderer.sharedMaterials = materials;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.enabled = false;

                    Meshes[i] = mesh;
                    Renderers[i] = renderer;
                }

                Root.SetActive(false);
            }
        }

        private static readonly int FadeId = Shader.PropertyToID("_Fade");

        [SerializeField] private Material _material;
        [SerializeField, Min(0.01f)] private float _spawnInterval = 0.08f;
        [SerializeField, Min(0.01f)] private float _lifetime = 0.3f;
        [SerializeField, Min(0f)] private float _minimumDistance = 0.25f;

        private PlayerRoot _player;
        private LocomotionModule _locomotion;
        private SkinnedMeshRenderer[] _sources;
        private AfterimageFrame[] _frames;
        private Vector3 _lastSpawnPosition;
        private float _nextSpawnAt;
        private int _nextFrameIndex;
        private bool _isBoosting;

        private void Awake()
        {
            _player = GetComponentInParent<PlayerRoot>(true);
            _sources = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        }

        private void OnEnable()
        {
            _locomotion = _player?.LocomotionModule;
            if (_locomotion == null) return;
            _locomotion.OnBoostStateChanged += HandleBoostStateChanged;
            HandleBoostStateChanged(_locomotion.IsBoosting);
        }

        private void OnDisable()
        {
            if (_locomotion != null)
                _locomotion.OnBoostStateChanged -= HandleBoostStateChanged;
            _locomotion = null;
            _isBoosting = false;
            if (_frames == null) return;
            foreach (AfterimageFrame frame in _frames)
                if (frame.Root != null) frame.Root.SetActive(false);
        }

        private void HandleBoostStateChanged(bool isBoosting)
        {
            _isBoosting = isBoosting;
            if (!isBoosting || _player == null) return;
            _lastSpawnPosition = _player.transform.position;
            _nextSpawnAt = Time.time;
        }

        private void LateUpdate()
        {
            FadeFrames();
            if (!CanCapture()) return;

            Vector3 position = _player.transform.position;
            if ((position - _lastSpawnPosition).sqrMagnitude <
                _minimumDistance * _minimumDistance) return;

            EnsurePool();
            CaptureFrame();
            _lastSpawnPosition = position;
            _nextSpawnAt = Time.time + _spawnInterval;
        }

        private bool CanCapture()
        {
            return _isBoosting && _player != null && _material != null &&
                   _locomotion != null &&
                   (_locomotion.MoveType == EMoveType.Boost ||
                    _locomotion.MoveType == EMoveType.BoostCombat) &&
                   _locomotion.MoveDirection.sqrMagnitude > 0.0001f &&
                   _sources != null && _sources.Length > 0 &&
                   Time.time >= _nextSpawnAt;
        }

        private void EnsurePool()
        {
            if (_frames != null) return;
            int count = Mathf.Clamp(Mathf.CeilToInt(_lifetime / _spawnInterval) + 1, 2, 64);
            _frames = new AfterimageFrame[count];
            for (int i = 0; i < count; i++)
                _frames[i] = new AfterimageFrame(i, _sources, _material);
        }

        private void CaptureFrame()
        {
            AfterimageFrame frame = _frames[_nextFrameIndex];
            _nextFrameIndex = (_nextFrameIndex + 1) % _frames.Length;
            frame.Root.SetActive(false);
            bool hasVisibleMesh = false;

            for (int i = 0; i < _sources.Length; i++)
            {
                SkinnedMeshRenderer source = _sources[i];
                MeshRenderer renderer = frame.Renderers[i];
                renderer.enabled = source != null && source.enabled &&
                                   source.gameObject.activeInHierarchy &&
                                   source.sharedMesh != null;
                if (!renderer.enabled) continue;

                // BakeMesh로 현재 애니메이션 자세를 고정하고 렌더러를 월드 위치에 둔다.
                source.BakeMesh(frame.Meshes[i], false);
                renderer.transform.SetPositionAndRotation(
                    source.transform.position, source.transform.rotation);
                renderer.transform.localScale = source.transform.lossyScale;
                renderer.gameObject.layer = source.gameObject.layer;
                hasVisibleMesh = true;
            }

            if (!hasVisibleMesh) return;
            frame.SpawnedAt = Time.time;
            SetFrameFade(frame, 1f);
            frame.Root.SetActive(true);
        }

        private void FadeFrames()
        {
            if (_frames == null) return;
            foreach (AfterimageFrame frame in _frames)
            {
                if (frame.Root == null || !frame.Root.activeSelf) continue;
                float fade = 1f - (Time.time - frame.SpawnedAt) / _lifetime;
                if (fade <= 0f)
                {
                    frame.Root.SetActive(false);
                    continue;
                }
                SetFrameFade(frame, fade);
            }
        }

        private static void SetFrameFade(AfterimageFrame frame, float fade)
        {
            frame.Properties.SetFloat(FadeId, fade);
            foreach (MeshRenderer renderer in frame.Renderers)
                if (renderer.enabled) renderer.SetPropertyBlock(frame.Properties);
        }

        private void OnDestroy()
        {
            if (_frames == null) return;
            foreach (AfterimageFrame frame in _frames)
            {
                foreach (Mesh mesh in frame.Meshes)
                    if (mesh != null) Destroy(mesh);
                if (frame.Root != null) Destroy(frame.Root);
            }
        }

        private void OnValidate()
        {
            _spawnInterval = Mathf.Max(0.01f, _spawnInterval);
            _lifetime = Mathf.Max(0.01f, _lifetime);
            _minimumDistance = Mathf.Max(0f, _minimumDistance);
        }
    }
}
