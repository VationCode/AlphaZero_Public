using System;
using System.Collections.Generic;
using alpha.ingame;
using alpha.ingame.input;
using alpha.ingame.player;
using UnityEngine;
using UnityEngine.Rendering;

public enum ECameraViewType
{
    Default = 0,
    Aim = 1,
    Scope = 2
}

/// <summary>카메라 리그의 위치, 회전, 줌 값을 View마다 보관한다.</summary>
[Serializable]
public struct CameraViewSettings
{
    public ECameraViewType Type;

    [Header("Settings")]
    public float Vertical;
    public float Horizontal;
    [Min(0f)] public float FollowSmoothTime;

    [Header("Rotation")]
    [Range(0f, 89f)] public float LimitAngleUp;
    [Range(0f, 89f)] public float LimitAngleDown;
    public float SensitivityX;
    public float SensitivityY;

    [Header("Zoom")]
    [Min(0f)] public float ZoomMin;
    [Min(0f)] public float ZoomMax;
    public float SensitivityZoom;
    [Min(0.01f)] public float ZoomSmoothSpeed;
    [Range(1f, 179f)] public float FieldOfView;

    // 기존 CameraModule 필드의 코드 기본값을 새 Default View에도 사용한다.
    public static CameraViewSettings CreateDefault() => new()
    {
        Type = ECameraViewType.Default,
        Vertical = 2.5f,
        Horizontal = 0f,
        FollowSmoothTime = 0.12f,
        LimitAngleUp = 60f,
        LimitAngleDown = 60f,
        SensitivityX = 15f,
        SensitivityY = 10f,
        ZoomMin = 1f,
        ZoomMax = 4.5f,
        SensitivityZoom = 1f,
        ZoomSmoothSpeed = 10f,
        FieldOfView = 60f
    };
}

public class CameraModule : MonoBehaviour
{
    [SerializeField] private Transform _rig;
    [SerializeField] private Transform _pivot;
    [SerializeField] private Transform _shoulder;
    [SerializeField] private Transform _zoom;
    [SerializeField] private Transform _shake;
    [SerializeField] private Transform _target;
    [SerializeField] private Camera _scopeCamera;

    [SerializeField] private CameraViewSettings[] _views = { CameraViewSettings.CreateDefault() };
    [SerializeField] private LayerMask _obstacleMask;
    [SerializeField, Min(0.01f)] private float _collisionRadius = 0.4f;
    [SerializeField, Min(0f)] private float _collisionPadding = 0.1f;

    public Camera Cam { get; private set; }
    public bool IsZoomed { get; private set; }
    public RenderTexture ScopeTexture => _scopeCamera != null ? _scopeCamera.targetTexture : null;
    public ECameraViewType ActiveViewType => _activeViewType;
    public event Action<ECameraViewType> OnViewChanged;

    private readonly Dictionary<ECameraViewType, float> _zoomDistances = new();
    private CameraViewSettings _activeView;
    private ECameraViewType _activeViewType;
    private readonly SmoothPositionFollower _positionFollower = new();
    private float _mouseX;
    private float _mouseY;
    private float _currentZoom;
    private MonoBehaviour _viewOwner;
    private PlayerRoot _playerCore;
    private GameObject _scopeHiddenModel;
    private Renderer[] _scopeHiddenRenderers = Array.Empty<Renderer>();
    private bool[] _scopeRendererStates = Array.Empty<bool>();
    private bool _scopeRenderersHidden;

    private void OnValidate()
    {
        if (_views == null || _views.Length == 0)
        {
            _views = new[] { CameraViewSettings.CreateDefault() };
            return;
        }

        // Inspector에서 새 Default 항목을 추가했을 때만 빈 설정을 채운다.
        for (int i = 0; i < _views.Length; i++)
        {
            CameraViewSettings view = _views[i];
            if (view.Type != ECameraViewType.Default || view.ZoomMax != 0f ||
                view.ZoomSmoothSpeed != 0f || view.FieldOfView != 0f) continue;
            _views[i] = CameraViewSettings.CreateDefault();
        }
    }

    private void Awake()
    {
        Cam = Camera.main;
        _playerCore = _target != null ? _target.GetComponentInParent<PlayerRoot>() : null;
        if (_scopeCamera != null) _scopeCamera.enabled = false;
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += HideLocalPlayerForScope;
        RenderPipelineManager.endCameraRendering += RestoreLocalPlayerAfterScope;
    }

    private void Start()
    {
        if (!TryGetView(ECameraViewType.Default, out _activeView))
        {
            Debug.LogWarning($"{nameof(CameraModule)}: Default View 설정이 없습니다.", this);
            enabled = false;
            return;
        }

        _activeViewType = ECameraViewType.Default;
        _currentZoom = Mathf.Max(_activeView.ZoomMin, _activeView.ZoomMax);
        _pivot.localPosition = new Vector3(0f, _activeView.Vertical, 0f);
        _shoulder.localPosition = new Vector3(_activeView.Horizontal, 0f, 0f);
        _zoom.localPosition = new Vector3(0f, 0f, -_currentZoom);
        if (Cam != null) Cam.fieldOfView = _activeView.FieldOfView;
        IsZoomed = false;
        OnViewChanged?.Invoke(_activeViewType);
    }

    // Scope는 메인 카메라의 시야를 유지한 채 별도 카메라로 렌즈 영상을 만든다.
    public bool SetView(MonoBehaviour owner, ECameraViewType type)
    {
        if (owner == null || Cam == null || !TryGetView(type, out CameraViewSettings view)) return false;
        if (type == ECameraViewType.Scope && ScopeTexture == null)
        {
            Debug.LogWarning($"{nameof(CameraModule)}: Scope Camera의 RenderTexture가 설정되지 않았습니다.", this);
            return false;
        }
        if (_activeViewType != type)
        {
            _zoomDistances[_activeViewType] = _currentZoom;
            _currentZoom = _zoomDistances.TryGetValue(type, out float savedZoom)
                ? savedZoom : Mathf.Max(view.ZoomMin, view.ZoomMax);
        }

        _viewOwner = owner;
        _activeView = view;
        _activeViewType = type;
        if (_scopeCamera != null) _scopeCamera.enabled = type == ECameraViewType.Scope;
        OnViewChanged?.Invoke(_activeViewType);
        return true;
    }

    public void ClearView(MonoBehaviour owner)
    {
        if (_viewOwner != owner || !TryGetView(ECameraViewType.Default, out CameraViewSettings view)) return;
        _zoomDistances[_activeViewType] = _currentZoom;
        _currentZoom = _zoomDistances.TryGetValue(ECameraViewType.Default, out float savedZoom)
            ? savedZoom : Mathf.Max(view.ZoomMin, view.ZoomMax);
        _viewOwner = null;
        _activeView = view;
        _activeViewType = ECameraViewType.Default;
        if (_scopeCamera != null) _scopeCamera.enabled = false;
        OnViewChanged?.Invoke(_activeViewType);
    }

    private bool TryGetView(ECameraViewType type, out CameraViewSettings view)
    {
        if (_views != null)
        {
            foreach (CameraViewSettings candidate in _views)
            {
                if (candidate.Type != type) continue;
                view = candidate;
                return true;
            }
        }

        view = default;
        return false;
    }

    private void FollowTarget()
    {
        if (_rig == null || _target == null) return;
        _rig.position = _positionFollower.Step(_rig.position, _target.position,
            _activeView.FollowSmoothTime);
    }

    private void Rotation(Vector2 lookInput)
    {
        _mouseX += lookInput.x * _activeView.SensitivityX * 0.01f;
        // 음수 피치는 위쪽, 양수 피치는 아래쪽을 향한다.
        _mouseY = Mathf.Clamp(_mouseY - lookInput.y * _activeView.SensitivityY * 0.01f,
            -_activeView.LimitAngleUp, _activeView.LimitAngleDown);
        _pivot.localRotation = Quaternion.Euler(_mouseY, _mouseX, 0f);
    }

    private void Zoom(float wheel, float blend)
    {
        if (wheel > 0f) IsZoomed = true;
        else if (wheel < 0f) IsZoomed = false;

        float min = Mathf.Min(_activeView.ZoomMin, _activeView.ZoomMax);
        float max = Mathf.Max(_activeView.ZoomMin, _activeView.ZoomMax);
        _currentZoom = Mathf.Clamp(_currentZoom - wheel * _activeView.SensitivityZoom, min, max);
        float next = Mathf.Lerp(-_zoom.localPosition.z, _currentZoom, blend);

        float castDistance = Mathf.Max(-_zoom.localPosition.z, _currentZoom);
        Vector3 destination = _shoulder.position - _shoulder.forward * castDistance;
        if (Physics.Linecast(_shoulder.position, destination, out RaycastHit hit, _obstacleMask,
                QueryTriggerInteraction.Ignore))
            next = Mathf.Min(next, Mathf.Max(0f, hit.distance - _collisionPadding));

        Vector3 local = _zoom.localPosition;
        local.z = -next;
        _zoom.localPosition = local;
    }

    private void UpdateView(float blend)
    {
        _pivot.localPosition = Vector3.Lerp(_pivot.localPosition,
            new Vector3(0f, _activeView.Vertical, 0f), blend);
        _shoulder.localPosition = Vector3.Lerp(_shoulder.localPosition,
            new Vector3(_activeView.Horizontal, 0f, 0f), blend);
        if (Cam == null) return;

        float fieldOfView = _activeView.FieldOfView;
        if (_activeViewType == ECameraViewType.Scope &&
            TryGetView(ECameraViewType.Default, out CameraViewSettings defaultView))
            fieldOfView = defaultView.FieldOfView;
        Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, fieldOfView, blend);
    }

    private void LateUpdate()
    {
        AlphaInput input = AlphaInput.Instance;
        if (input == null) return;

        float blend = 1f - Mathf.Exp(-Mathf.Max(0.01f, _activeView.ZoomSmoothSpeed) * Time.deltaTime);
        FollowTarget();
        Rotation(input.LookInput);
        Zoom(input.ZoomInput.y, blend);
        UpdateView(blend);
        if (_activeViewType == ECameraViewType.Scope && _scopeCamera != null)
        {
            _scopeCamera.fieldOfView = _activeView.FieldOfView;
        }
    }

    // 스코프 카메라에만 현재 모델과 장착 무기의 Renderer를 숨긴다.
    private void HideLocalPlayerForScope(ScriptableRenderContext context, Camera camera)
    {
        if (camera != _scopeCamera || _playerCore == null) return;
        GameObject model = _playerCore.CurrentModel;
        if (model == null) return;

        if (_scopeRenderersHidden) RestoreLocalRenderers();
        if (_scopeHiddenModel != model)
        {
            _scopeHiddenModel = model;
            _scopeHiddenRenderers = model.GetComponentsInChildren<Renderer>(true);
            _scopeRendererStates = new bool[_scopeHiddenRenderers.Length];
        }

        for (int i = 0; i < _scopeHiddenRenderers.Length; i++)
        {
            Renderer renderer = _scopeHiddenRenderers[i];
            if (renderer == null) continue;
            _scopeRendererStates[i] = renderer.forceRenderingOff;
            renderer.forceRenderingOff = true;
        }
        _scopeRenderersHidden = true;
    }

    private void RestoreLocalPlayerAfterScope(ScriptableRenderContext context, Camera camera)
    {
        if (camera == _scopeCamera) RestoreLocalRenderers();
    }

    private void RestoreLocalRenderers()
    {
        if (!_scopeRenderersHidden) return;
        for (int i = 0; i < _scopeHiddenRenderers.Length; i++)
        {
            Renderer renderer = _scopeHiddenRenderers[i];
            if (renderer != null) renderer.forceRenderingOff = _scopeRendererStates[i];
        }
        _scopeRenderersHidden = false;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= HideLocalPlayerForScope;
        RenderPipelineManager.endCameraRendering -= RestoreLocalPlayerAfterScope;
        RestoreLocalRenderers();
        if (_scopeCamera != null) _scopeCamera.enabled = false;
    }

}
