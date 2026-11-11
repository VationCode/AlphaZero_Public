using UnityEngine;

/// <summary>아레테별 게이지의 이미지 묶음을 표시하거나 숨긴다.</summary>
public abstract class AreteMobilityGauge : MonoBehaviour
{
    [SerializeField] private GameObject _group;

    protected bool IsGroupVisible => _group != null && _group.activeSelf;

    protected virtual void Awake()
    {
        if (_group == null)
            _group = transform.Find("Group")?.gameObject;
    }

    protected void SetGroupVisible(bool visible)
    {
        if (_group != null) _group.SetActive(visible);
    }
}
