using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UIMgr : MonoBehaviour
{
    // singleton
    public static UIMgr Instance;
    private ObjectSpawner _objectSpawner;

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
    [Header("Results UI")]
    [SerializeField] private TextMeshProUGUI _finalScore;
    [SerializeField] private TextMeshProUGUI _highscore;
    [SerializeField] private TextMeshProUGUI _newRecordLabel;
    [SerializeField] private Image _imageFill;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _objectSpawner = FindFirstObjectByType<ObjectSpawner>();
        _gameplayScreen.SetActive(true);
        _resultsScreen.SetActive(false);
    }


    void Start()
    {
        HideCutIcon();
    }

    public IEnumerator StartCountdown()
    {
        _gameplayScreen.SetActive(true);
        _resultsScreen.SetActive(false);

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

    public void SetTimer(float duration)
    {
        int minutes = Mathf.FloorToInt(duration / 60);
        float remSeconds = duration % 60;
        string formatted = string.Format("{0}:{1:00.00}", minutes, remSeconds);

        _timer.text = formatted;
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

            if (Mathf.RoundToInt(remaining) == (duration/2)) 
            { 
                _objectSpawner.GetFaster(0.9f, 0.5f);
                Debug.Log("faster 30%");
            }
            if (Mathf.RoundToInt(remaining) == (duration / 4))
            {
                _objectSpawner.GetFaster(0.7f, 0.4f);
                Debug.Log("faster 60%");
            }
            Debug.Log($"{Mathf.RoundToInt(remaining)}");
            yield return null;
        }
        _timer.text = "0:00.00";
        GameMgr.Instance.EndGame();
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

    public void ShowResultsScreen(int finalScore, int highscore, bool isHighscoreNew)
    {
        _gameplayScreen.SetActive(false);

        _finalScore.text = $"SCORE: {finalScore}";
        _highscore.text = $"HIGHSCORE: {highscore}";
        _newRecordLabel.gameObject.SetActive(isHighscoreNew);

        _resultsScreen.SetActive(true);
    }

    public void AddImageFill()
    {
        float fillAmount = 0.7f;
        _imageFill.fillAmount += fillAmount * Time.deltaTime;
        _imageFill.fillAmount = Mathf.Clamp01(_imageFill.fillAmount);

        if (_imageFill.fillAmount >= 1)
        {
            Application.Quit();

            // If running in the editor
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #endif
        }
    }

    public void ResetImageFill()
    {
        _imageFill.fillAmount = 0 ;
    }

    public void OnAction(InputAction.CallbackContext context)
    {
        if (GameMgr.Instance.IsPlaying) return;
        if (context.started) Debug.Log("uimgr input");
    }
}
