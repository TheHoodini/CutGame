using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

//[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float _rotationInterval = 0.2f;

    private ObjectSpawner _objectSpawner;
    private CutIndicator _cutIndicator;
    private CharacterController _characterController;

    private float _holdActivationDelay = 0.25f;
    private bool _isHolding = false;
    private bool _isTap = false;
    private float _rotationTimer = 0f;
    private float _holdDelayTimer = 0f;
    private bool _holdConfirmed = false;

    private void Awake()
    {
        _objectSpawner = FindFirstObjectByType<ObjectSpawner>();
        _cutIndicator = FindFirstObjectByType<CutIndicator>();
        _characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (!_isHolding) return;

        if (!_holdConfirmed)
        {
            _holdDelayTimer += Time.deltaTime;
            if (_holdDelayTimer >= _holdActivationDelay)
            {
                _holdConfirmed = true;
                _rotationTimer = _rotationInterval;
            }
            return;
        }

        _rotationTimer += Time.deltaTime;
        if (_rotationTimer >= _rotationInterval)
        {
            _cutIndicator.Rotate();
            _rotationTimer = 0f;
        }
    }

    public void OnAction(InputAction.CallbackContext context)
    {
        // TAP
        if (context.interaction is TapInteraction)
        {
            if (context.started)
                _isTap = true;

            if (context.performed && _isTap)
            {
                TryCut();
                _isTap = false;
            }
        }

        // HOLD
        if (context.interaction is HoldInteraction && !_isTap)
        {
            if (context.started)
            {
                Debug.Log("Holding");
                _isHolding = true;
                _holdConfirmed = false;
            }
            else if (context.canceled)
            {
                Debug.Log("Hold Ended");
                _isHolding = false;
                _holdConfirmed = false;
            }
            _holdDelayTimer = 0f;
            _rotationTimer = 0f;
        }
    }

    private void TryCut()
    {
        if (_objectSpawner.IsRespawning || _objectSpawner.CurrentObject == null)
        {
            Debug.Log("Can't be cut");
            return;
        }

        Debug.Log("Tap");
        float angle = Math.Abs(_cutIndicator.transform.rotation.eulerAngles.z) * -1;
        _objectSpawner.CurrentObject.CutAtAngle(angle);
        _objectSpawner.OnItemCut();
    }
}