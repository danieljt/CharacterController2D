using UnityEngine;

 /// <summary>
/// Public interface for all 2D kinematic character controllers. Contains the core functionality
/// for charactercontrollers
/// </summary>
public interface ICharacterController2D
{
    public void Move(Vector2 deltaPosition);
    public bool IsGrounded { get; }
    public CollisionFlags2D GetCollisionFlags { get; }
}

