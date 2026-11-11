using UnityEngine;
using UnityEngine.UI;

public class CrossHair : MonoBehaviour
{
    [SerializeField]
    private GameObject _base;
    [SerializeField]
    private GameObject _sniperScope;
    [SerializeField] private RawImage _scopeLens;

    // 스코프 렌즈에만 확대용 카메라의 RenderTexture를 표시한다.
    public bool SetScopeTexture(RenderTexture texture)
    {
        if (texture == null || _scopeLens == null || _scopeLens.material == null) return false;
        if (_scopeLens.texture != texture) _scopeLens.texture = texture;
        return true;
    }

    public void SetScopeMode(bool enabled)
    {
        if (_base != null) _base.SetActive(!enabled);
        if (_sniperScope != null) _sniperScope.SetActive(enabled);
    }

    private void Awake() => SetScopeMode(false);
}
