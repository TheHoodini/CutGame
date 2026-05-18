using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectSpawner : MonoBehaviour
{
    [SerializeField] private List<GameObject> _cuttableObjPrefabs;
    [Header("Spawn Settings")]
    [SerializeField] private float _respawnDelay = 2f;
    [SerializeField] private float _spawnYOffset = 5f;
    [SerializeField] private float _slideDownDuration = 0.8f;

    private CuttableObject _currentObject;
    private bool _isRespawning = false;
    private bool _isSliding = false;

    public CuttableObject CurrentObject => _currentObject;
    public bool IsRespawning => _isRespawning;
    public bool IsSliding => _isSliding;
    public bool CanCut => !_isRespawning && !_isSliding && _currentObject != null;

    private Vector3 SpawnPosition => transform.position + Vector3.up * _spawnYOffset;
    private Vector3 DestinationPosition => transform.position;

    private void Start()
    {
        SpawnObject();
    }

    public void SpawnObject()
    {
        if (_cuttableObjPrefabs == null || _cuttableObjPrefabs.Count == 0)
        {
            Debug.LogWarning("ObjectSpawner: No prefabs");
            return;
        }

        int index = Random.Range(0, _cuttableObjPrefabs.Count);
        GameObject prefab = _cuttableObjPrefabs[index];

        GameObject spawnedObj = Instantiate(prefab, SpawnPosition, Quaternion.identity);
        _currentObject = spawnedObj.GetComponent<CuttableObject>();

        if (_currentObject == null)
        {
            Debug.LogError("ObjectSpawner: Spawned object does not have CuttableObject component");
            Destroy(spawnedObj);
            return;
        }

        StartCoroutine(SlideDown(_currentObject.gameObject));
    }

    public void OnItemCut()
    {
        _currentObject = null;

        if (_respawnDelay > 0)
            StartCoroutine(DelayAndRespawn());
        else
            SpawnObject();
    }

    private IEnumerator SlideDown(GameObject obj)
    {
        _isSliding = true;

        float elapsed = 0f;
        Vector3 start = SpawnPosition;
        Vector3 end = DestinationPosition;

        while (elapsed < _slideDownDuration)
        {
            if (obj == null) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _slideDownDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            obj.transform.position = Vector3.Lerp(start, end, smoothT);

            yield return null;
        }

        if (obj != null)
            obj.transform.position = end;

        _isSliding = false;
    }

    private IEnumerator DelayAndRespawn()
    {
        _isRespawning = true;
        yield return new WaitForSeconds(_respawnDelay);
        _isRespawning = false;
        SpawnObject();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Vector3 destination = transform.position;
        Vector3 spawnPoint = transform.position + Vector3.up * _spawnYOffset;

        // Destination point (green)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(destination, 0.3f);
        UnityEditor.Handles.color = Color.green;
        UnityEditor.Handles.Label(destination + Vector3.right * 0.4f, "Destination");

        // Spawn point (red)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(spawnPoint, 0.3f);
        UnityEditor.Handles.color = Color.red;
        UnityEditor.Handles.Label(spawnPoint + Vector3.right * 0.4f, "Spawn");
    }
#endif
}