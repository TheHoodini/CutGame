using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class UIMgr : MonoBehaviour
{
    // singleton
    public static UIMgr Instance;

    [Header("Screens")]
    [SerializeField] private GameObject _gameplayScreen;
    [SerializeField] private GameObject _resultsScreen;
    [Header("Gameplay UI")]
    [SerializeField] private TextMeshProUGUI _startCount;
    [SerializeField] private TextMeshProUGUI _scoreText;
    [SerializeField] private TextMeshProUGUI _timer;
    [Space(10)]
    [SerializeField] private Image _cutIcon;
    [SerializeField] private Sprite _iconVertical;
    [SerializeField] private Sprite _iconHorizontal;
    [SerializeField] private Sprite _iconDiagonal45;
    [SerializeField] private Sprite _iconDiagonal135;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _gameplayScreen.SetActive(true);
        _resultsScreen.SetActive(false);
    }


    void Start()
    {
        HideCutIcon();
    }

    public IEnumerator StartCountdown()
    {
        _startCount.gameObject.SetActive(true);
        _startCount.text = "3";
        yield return new WaitForSeconds(1f);
        _startCount.text = "2";
        yield return new WaitForSeconds(1f);
        _startCount.text = "1";
        yield return new WaitForSeconds(1f);
        _startCount.text = "GO!";
        yield return new WaitForSeconds(1f);
        _startCount.gameObject.SetActive(false);
    }

    public IEnumerator StartTimer(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float remaining = Mathf.Max(0f, duration - elapsed);
            int totalCentiseconds = Mathf.CeilToInt(remaining * 100f);
            int minutes = totalCentiseconds / 6000;
            int seconds = (totalCentiseconds / 100) % 60;
            int centiseconds = totalCentiseconds % 100;
            _timer.text = $"{minutes}:{seconds:00}.{centiseconds:00}";
            elapsed += Time.deltaTime;
            yield return null;
        }
        _timer.text = "0:00.00";
    }

    public void UpdateScore(int score)
    {
        _scoreText.text = $"Score: {score}";
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

    public void ShowResultsScreen(int finalScore)
    {
        _gameplayScreen.SetActive(false);
        _resultsScreen.SetActive(true);
    }
}
