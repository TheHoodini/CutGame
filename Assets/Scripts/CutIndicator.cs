using System.Collections;
using UnityEngine;

public class CutIndicator : MonoBehaviour
{
    public void Rotate(float timeSeconds = 0.1f, float angle = -45)
    {
        StartCoroutine(RotateCoroutine(angle, timeSeconds));
    }

    private IEnumerator RotateCoroutine(float angle, float timeSeconds)
    {
        float elapsedTime = 0f;
        Quaternion initialRotation = transform.rotation;
        Quaternion targetRotation = initialRotation * Quaternion.Euler(0, 0, angle);
        while (elapsedTime < timeSeconds)
        {
            transform.rotation = Quaternion.Slerp(initialRotation, targetRotation, elapsedTime / timeSeconds);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        transform.rotation = targetRotation;
    }
}
