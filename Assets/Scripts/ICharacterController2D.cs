using UnityEngine;

namespace RPCharacterController2D
{
    /// <summary>
    /// Public interface for all 2D kinematic character controllers
    /// </summary>
    public interface ICharacterController2D
    {
        public void Move(Vector2 deltaPosition);
        public bool IsGrounded { get; }
        public CollisionFlags2D GetCollisionFlags { get; }
    }
}
