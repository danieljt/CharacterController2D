using UnityEngine;
using UnityEngine.InputSystem;

public class AdvancedPlayer : MonoBehaviour
{
	public float walkSpeed;
	public float jumpSpeed;

	private ICharacterController2D controller;
	private PlayerInput player;
	private Vector2 input;
	private bool jump;

	// Used for movement
	private float xVelocity;
	private float yVelocity;

	private void Awake()
	{
		controller = GetComponent<ICharacterController2D>();
		player = GetComponent<PlayerInput>();
	}

	private void OnEnable()
	{
		player.onActionTriggered += HandleInput;
	}

	private void OnDisable()
	{
		player.onActionTriggered -= HandleInput;
	}

	private void FixedUpdate()
	{
		//Debug.Log(controller.)
		xVelocity = walkSpeed * input.x;
		
		if(controller.IsGrounded)
		{
			yVelocity = 0;

			if(jump)
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
			input = context.ReadValue<Vector2>();
		}

		if(context.action.name == "Jump")
		{
			jump = context.ReadValue<float>() > 0;
		}
	}
}
