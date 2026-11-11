using UnityEngine;

namespace alpha.ingame.item
{
    /// <summary>무기별 Aim/Scope View를 전환하고 Scope일 때 렌즈 UI를 표시한다.</summary>
    public sealed class SecondaryViewAction : WeaponSecondaryAction
    {
        [SerializeField] private ECameraViewType _viewType = ECameraViewType.Aim;

        private CameraModule _cameraModule;
        private CrossHair _crossHair;
        private bool _isActive;
        public bool IsActive => _isActive;

        public override void Begin()
        {
            if (_isActive) return;
            if (_cameraModule == null) _cameraModule = FindFirstObjectByType<CameraModule>();
            if (_cameraModule == null || !_cameraModule.SetView(this, _viewType)) return;
            _isActive = true;

            if (_viewType != ECameraViewType.Scope) return;

            GameObject crossHairObject = UIManager.Instance?.CrossHair;
            _crossHair = crossHairObject != null ? crossHairObject.GetComponent<CrossHair>() : null;
            if (_crossHair == null || !_crossHair.SetScopeTexture(_cameraModule.ScopeTexture))
            {
                Debug.LogWarning("SecondaryViewAction: 스코프 렌즈 UI 또는 RenderTexture가 설정되지 않았습니다.", this);
                End();
                return;
            }

            _crossHair.SetScopeMode(true);
        }

        public override void End()
        {
            _crossHair?.SetScopeMode(false);
            _crossHair = null;
            if (_isActive) _cameraModule?.ClearView(this);
            _isActive = false;
        }
    }
}
