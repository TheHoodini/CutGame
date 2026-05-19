using System.Collections;
using UnityEngine;

public class Shake : MonoBehaviour
{
    [SerializeField] private bool start = false;
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private float magnitude = 0.1f;

    public void StartShake()
    {
        start = true;
    }

    private void Update()
    {
        if (start)
        {
            start = false;
            StartCoroutine(Shaking());
        }
    }

    IEnumerator Shaking()
    {
        Vector3 startposition = transform.position;

        float elapsed = 0.0f;
        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;
            transform.position = new Vector3(startposition.x + x, startposition.y + y, startposition.z);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = startposition;
    }
}
