using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class TitleScreen : MonoBehaviour
{
    [SerializeField] private string _mainGameScene;
    [SerializeField] private TextMeshProUGUI _startText;
    private Vector3 _textStartScale;

    // Update is called once per frame
    void Start()
    {
        _textStartScale = _startText.transform.localScale;
    }

    void Update()
    {
        float scale = 1 + Mathf.Sin(Time.time * 2f) * 0.1f;
        _startText.transform.localScale = _textStartScale * scale;
    }

    public void OnStartGame(InputAction.CallbackContext context)
    {
        if (context.started) SceneManager.LoadScene(_mainGameScene);
    }
}
