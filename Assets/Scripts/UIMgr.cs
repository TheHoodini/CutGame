using UnityEngine;
using UnityEngine.UI;

public class UIMgr : MonoBehaviour
{
    [SerializeField] private Image _cutIcon;

    [Header("Cut Icons")]
    [SerializeField] private Sprite _iconVertical;
    [SerializeField] private Sprite _iconHorizontal;
    [SerializeField] private Sprite _iconDiagonal45;
    [SerializeField] private Sprite _iconDiagonal135;

    void Start()
    {
        HideCutIcon();
    }

    public void HideCutIcon()
    {
        _cutIcon.gameObject.SetActive(false);
    }

    public void ShowCutIcon(int cutDirection)
    {
        switch (cutDirection)
        {
            case 90:
                _cutIcon.sprite = _iconVertical;
                break;
            case 0:
                _cutIcon.sprite = _iconHorizontal;
                break;
            case 45:
                _cutIcon.sprite = _iconDiagonal45;
                break;
            case 135:
                _cutIcon.sprite = _iconDiagonal135;
                break;
            default:
                Debug.LogWarning($"UIMgr: Invalid cut direction {cutDirection}");
                break;
        }
        _cutIcon.gameObject.SetActive(true);
    }
}
