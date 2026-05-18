using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectSpawner : MonoBehaviour
{
    [SerializeField] private List<GameObject> _cuttableObjPrefabs;
    [SerializeField] private float _respawnDelay = 2f;

    private CuttableObject _currentObject;
    private bool _isRespawning = false;

    public CuttableObject CurrentObject => _currentObject;
    public bool IsRespawning => _isRespawning;

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

        GameObject spawnedObj = Instantiate(prefab, transform.position, Quaternion.identity);

        _currentObject = spawnedObj.GetComponent<CuttableObject>();

        if (_currentObject == null)
        {
            Debug.LogError("ObjectSpawner: Spawned object does not have CuttableObject component");
            Destroy(spawnedObj);
            return;
        }
    }

    public void OnItemCut()
    {
        _currentObject = null;
        if (_respawnDelay > 0)
        {
            StartCoroutine(DelayAndRespawn());
        }
        else
        {
            SpawnObject();
        }

    }

    private IEnumerator DelayAndRespawn()
    {
        _isRespawning = true;
        yield return new WaitForSeconds(_respawnDelay);
        SpawnObject();
        _isRespawning = false;
    }
}
