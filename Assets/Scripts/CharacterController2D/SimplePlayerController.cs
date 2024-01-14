using UnityEngine;
using UnityEngine.InputSystem;

public class SimplePlayerController : MonoBehaviour
{
	[SerializeField] protected float speed;

    protected ICharacterController2D controller;
	protected PlayerInput input;

	// This vector contains the current input direction of the player
	protected Vector2 inputVector;

	// The input action in the input actions asset
	private InputAction moveAction;

	private void Awake()
	{
		controller = GetComponent<ICharacterController2D>();
		input = GetComponent<PlayerInput>();

		// Serch the input actions asset for an input action with the name "move".
		moveAction = input.actions.FindAction("move");
	}

	private void OnEnable()
	{
		// Register the OnMove function to the moveAction events
		moveAction.started += OnMove;
		moveAction.performed += OnMove;
		moveAction.canceled += OnMove;
	}

	private void OnDisable()
	{
		// Unregister the OnMove function from the moveAction events
		moveAction.started -= OnMove;
		moveAction.performed -= OnMove;
		moveAction.canceled -= OnMove;
	}

	protected void FixedUpdate()
    {
		// Moves the character
		controller.Move(speed * Time.fixedDeltaTime * inputVector);
    }

	/// <summary>
	/// This method takes in the data from the playerInput component when the player interacts
	/// with the chosen input.
	/// </summary>
	/// <param name="context"></param>
	protected void OnMove(InputAction.CallbackContext context)
	{
		inputVector = context.ReadValue<Vector2>();
	}
}
