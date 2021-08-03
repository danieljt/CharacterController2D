using System.Collections;
using System.Collections.Generic;
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
		xVelocity = walkSpeed * input.x;
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
