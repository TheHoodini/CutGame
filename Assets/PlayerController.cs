using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private CuttableObject _cuttableObject;
    private CharacterController _characterController;
    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
    }

    public void OnAction(InputAction.CallbackContext context)
    {
        if (context.interaction is TapInteraction &&
            context.performed)
        {
            Debug.Log("Tap");
                _cuttableObject.CutAtAngle(_cuttableObject.cutAngle * -1);
        }

        // HOLD
        if (context.interaction is HoldInteraction &&
            context.performed)
        {
            Debug.Log("Hold");
        }
    }
    
}
