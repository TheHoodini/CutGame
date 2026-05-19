using System.Collections;
using UnityEngine;

public class GameMgr : MonoBehaviour
{
    public static GameMgr Instance;
    private ObjectSpawner _objectSpawner;

    private int _score = 0;
    public int Score => _score;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _objectSpawner = FindFirstObjectByType<ObjectSpawner>();
    }

    private IEnumerator Start()
    {
        yield return StartCoroutine(UIMgr.Instance.StartCountdown());
        _objectSpawner.SpawnObject();
        StartCoroutine(UIMgr.Instance.StartTimer(120f));
    }

    public void ResetScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    public void AddScore(int points = 1)
    {
        _score += points;
        UIMgr.Instance.UpdateScore(_score);
    }
}
