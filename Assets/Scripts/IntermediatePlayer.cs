using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using RPCharacterController2D;

public class IntermediatePlayer : MonoBehaviour
{
    public float speed;

    private ICharacterController2D controller;
    private PlayerInput input;
    private Vector2 inputVector;

    private void Awake()
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
        controller.Move(speed * Time.fixedDeltaTime * inputVector);
    }

    private void HandleInput(InputAction.CallbackContext context)
    {
        if (context.action.name == "Move")
        {
            inputVector = context.ReadValue<Vector2>();
        }
    }
}
