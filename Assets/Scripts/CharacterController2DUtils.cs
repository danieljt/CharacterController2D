using System;

namespace RPCharacterController2D
{
	/// <summary>
	/// Bitmask telling where collisions on the character controller have taken place. The mask takes into account
	/// the local transform of the objects collider(Transform.up, transform.right) into account. Front is in the transform.right
	/// direction, Top is in the transform.up direction, Back is in the (-transform.right) direction and bottom is in
	/// the (-transform.up) direction. Collision flags are useful for giving the character certain behaviours like being
	/// grounded when Bottom is true or crushed if both front and back are true.
	/// </summary>
	[Flags]
	public enum CollisionFlags2D
	{
		None = 0,
		Front = 1,
		Bottom = 2,
		Back = 4,
		Top = 8
	}
}
