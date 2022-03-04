using UnityEngine;
using UnityEngine.InputSystem;

///<summary>
/// test class for charactercontroller2d
///</summary>
public class Player : MonoBehaviour
{
    public float speed;
    public float jumpSpeed;

    private ICharacterController2D controller;
    private PlayerInput input;
    private Vector2 inputVector;
    private float yVelocity;
    private float xVelocity;
    private bool jump;
    
    protected void Awake()
    {
        controller = GetComponent<ICharacterController2D>();
        input = GetComponent<PlayerInput>();
    }

    private void OnEnable()
    {
        input.onActionTriggered += HandleInput;
    }

    private void OnDisable()
    {
        input.onActionTriggered -= HandleInput;
    }

	private void FixedUpdate()
    {
        xVelocity = inputVector.x * speed;

        if(controller.IsGrounded)
		{
            yVelocity = 0;

            if (jump)
            {
                yVelocity = jumpSpeed;
            }
		}

        else
		{
            yVelocity += Physics2D.gravity.y * Time.fixedDeltaTime;
		}

        controller.Move(new Vector2(xVelocity, yVelocity) * Time.fixedDeltaTime);
    }

    private void HandleInput(InputAction.CallbackContext context)
    {
        if(context.action.name == "Move")
        {
            inputVector = context.ReadValue<Vector2>();
        }

        if(context.action.name == "Jump")
		{
            jump = context.ReadValue<float>() > 0;
		}
    }
}
