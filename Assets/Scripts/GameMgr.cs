using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameMgr : MonoBehaviour
{
    public static GameMgr Instance;
    private ObjectSpawner _objectSpawner;

    [SerializeField] private float _gameDuration = 120f;
    private int _score = 0;
    public int Score => _score;
    private int _highscore;

    private bool isPlaying = false;
    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _objectSpawner = FindFirstObjectByType<ObjectSpawner>();
        //PlayerPrefs.SetInt("highscore", 2);
        _highscore = PlayerPrefs.GetInt("highscore", 0);
    }

    void Start()
    {
        StartCoroutine(StartGame());
    }

    private IEnumerator StartGame()
    {
        isPlaying = true;
        UIMgr.Instance.SetTimer(_gameDuration);
        yield return StartCoroutine(UIMgr.Instance.StartCountdown());
        _objectSpawner.SpawnObject();
        StartCoroutine(UIMgr.Instance.StartTimer(_gameDuration));
    }

    public void ResetScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void AddScore(int points = 1)
    {
        _score += points;
        UIMgr.Instance.UpdateScore(_score);
    }

    public void EndGame()
    {
        isPlaying = false;
        bool isHighscoreNew = _score > PlayerPrefs.GetInt("highscore", 0);
        if (isHighscoreNew)
        {
            PlayerPrefs.SetInt("highscore", _score);
            PlayerPrefs.Save();
            _highscore = _score;
        } else
        {
            _highscore = PlayerPrefs.GetInt("highscore", 0);
        }
        UIMgr.Instance.ShowResultsScreen(_score, _highscore, isHighscoreNew);
    }
}
