using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonTriggerSound : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [SerializeField]
    private AudioSource _audioSource;

    [SerializeField]
    private AudioClip _enterClip;
    [SerializeField]
    private AudioClip _clickClip;

    public void OnPointerEnter(PointerEventData eventData)
    {
        _audioSource.PlayOneShot(_enterClip);
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        _audioSource.PlayOneShot(_clickClip);
    }
}
