using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

///<summary>
/// test class for charactercontroller2d
///</summary>
public class Player : MonoBehaviour
{
    public float speed;
    private CharacterController2D controller;
    private PlayerInput input;
    private Vector2 move;
    
    private void Awake()
    {
        controller = GetComponent<CharacterController2D>();
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
        controller.Move(move*Time.fixedDeltaTime);
    }

    private void HandleInput(InputAction.CallbackContext context)
    {
        if(context.action.name == "Move")
        {
            Vector2 inputVector = context.ReadValue<Vector2>();
            Move(inputVector);
        }
    }

    private void Move(Vector2 inputVector)
    {
        move = inputVector*speed;
    }
}
