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
    private Shake _cameraShake;

    private float _holdActivationDelay = 0.25f;
    private bool _isHolding = false;
    private bool _isTap = false;
    private float _rotationTimer = 0f;
    private float _holdDelayTimer = 0f;
    private bool _holdConfirmed = false;

    private void Awake()
    {
        _cameraShake = Camera.main.GetComponent<Shake>();
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

        if (_isHolding && !GameMgr.Instance.IsPlaying) UIMgr.Instance.AddImageFill();

        if (!GameMgr.Instance.IsPlaying) return;
        _rotationTimer += Time.deltaTime;
        if (_rotationTimer >= _rotationInterval)
        {
            _cutIndicator.Rotate();
            _rotationTimer = 0f;
        }
    }

    public void OnReset(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            GameMgr.Instance.ResetScene();
        }
    }

    public void OnAction(InputAction.CallbackContext context)
    {
        //if (!GameMgr.Instance.IsPlaying) return;
        // TAP
        if (context.interaction is TapInteraction)
        {
            if (GameMgr.Instance.IsPlaying) { // isPlaying -------
                if (context.started)
                _isTap = true;

                if (context.performed && _isTap)
                {
                    TryCut();
                    _isTap = false;
                }
            }
            else // is not Playing -------
            {
                GameMgr.Instance.ResetScene();
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
                UIMgr.Instance.ResetImageFill();

            }
            _holdDelayTimer = 0f;
            _rotationTimer = 0f;
        }
    }

    private void TryCut()
    {
        if (!_objectSpawner.CanCut)
        {
            Debug.Log("Can't be cut");
            return;
        }

        Debug.Log("Tap");
        int indicatorAngle = GetRightAngle(_cutIndicator.transform.localEulerAngles.z);

        // Make cut
        bool isAngleEqual = indicatorAngle == (int)_objectSpawner.CurrentObject.CutAngle;
        _objectSpawner.CurrentObject.CutAtAngle(indicatorAngle, isAngleEqual);
        if (!isAngleEqual) _cameraShake.StartShake();
        if (isAngleEqual) GameMgr.Instance.AddScore();
        _objectSpawner.OnItemCut();
    }

    private int GetRightAngle(float angle)
    {
        angle %= 360;
        if (angle > 180) angle -= 360;

        int intAngle = Mathf.RoundToInt(angle);

        switch (intAngle)
        {
            case 0 or 180:
                intAngle = 0;
                break;
            case -45 or 135:
                intAngle = 45;
                break;
            case -90 or 90:
                intAngle = 90;
                break;
            case -135 or 45:
                intAngle = 135;
                break;
        }

        return intAngle;
    }
}