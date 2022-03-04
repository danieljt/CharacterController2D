using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// A Charactercontroller2D is a component built on top of the Box2D physics system. This component allows a character
/// to respond to collisions with hand crafted physics. This component uses a kinematic rigidbody and shapecasting to perform 
/// collision checks and responses.
/// 
/// </summary>
public class CharacterController2D : MonoBehaviour, ICharacterController2D
{
	[Tooltip("Skin to prevent the character from penetrating other colliders")]
	[SerializeField] protected float contactOffset;

	[Tooltip("Number of iterations we allow for each frame")]
	[SerializeField] protected int positionIterations;

	[Tooltip("The maximum slope the character can climb")]
	[SerializeField] protected float slopeLimit;

	[Tooltip("Can the Character collide and slide on ground. If this is false the character will NOT collide and slide on ground")]
	[SerializeField] protected bool slideOnGround;

	[Tooltip("Can the Character collide and slide on ceilings the same way as on the ground?")]
	[SerializeField] protected bool slideOnCeilings;

	[Tooltip("Will the character force slide down/up slopes steeper than the slopelimit?")]
	[SerializeField] protected bool forceSlide;

	// Collision
	protected Transform characterTransform;
	protected Rigidbody2D characterBody;
	protected ContactFilter2D contactFilter;
	protected List<RaycastHit2D> hitList;
	protected CollisionFlags2D collisionFlags;

	// Orientation
	protected Vector2 characterUpDirection;

	// Previous frame values
	protected bool characterIsGrounded;

	private void Awake()
	{
		characterTransform = GetComponent<Transform>();
		characterBody = GetComponent<Rigidbody2D>();
		characterBody.isKinematic = true;
		contactFilter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
		contactFilter.useLayerMask = true;
		hitList = new List<RaycastHit2D>(16);
		collisionFlags = CollisionFlags2D.None;
		characterUpDirection = Vector2.up;
	}

	/// <summary>
	/// Move character while being contrained by collisions
	/// </summary>
	/// <param name="deltaPosition"></param>
	public void Move(Vector2 deltaPosition)
	{
		//---------------------------------------------------------------------------------------------------------
		// Reset the collision flags before invoking the movement method. If the user needs the flags from the
		// previous call they should be used before invoking this method. At this point the character will
		// move and recieve new flags.
		//---------------------------------------------------------------------------------------------------------
		collisionFlags = ResetCollisionFlags(collisionFlags);

		//---------------------------------------------------------------------------------------------------------
		// We first get our up direction and calculate the dot product with respect to the deltaposition direction.
		// We also calculate the total distance to travel this frame.
		//---------------------------------------------------------------------------------------------------------
		Vector2 upDirection = characterUpDirection;

		//---------------------------------------------------------------------------------------------------------
		// Initial cast. Here we use the full movement vector before splitting into side/up movement. If no
		// collisions are found we can early exit the method.
		//---------------------------------------------------------------------------------------------------------
		if (!MoveInitial(characterBody, deltaPosition, contactFilter, hitList, contactOffset))
		{
			return;
		}

		//---------------------------------------------------------------------------------------------------------
		// We decompose our movement into a normal and tangent vector with respect to the characters up direction.
		// The normal vector points in the direction of the up direction while the tangent Vector points
		// in the perpendicular direction to the normal with respect to the deltaposition. Remember that the
		// character up direction must be normalized and non zero.
		//---------------------------------------------------------------------------------------------------------		
		Vector2 vertVector = upDirection * Vector2.Dot(upDirection, deltaPosition);
		Vector2 sideVector = deltaPosition - vertVector;

		//---------------------------------------------------------------------------------------------------------
		// Run the horizontal and vertical move methods. Note that we do the horizontal movement first. This
		// is by design. Changing the order of these calls requires a full remake of the controller.
		//---------------------------------------------------------------------------------------------------------
		collisionFlags |= MoveHorizontal(characterTransform, characterBody, sideVector, upDirection, contactFilter, hitList,
			contactOffset, positionIterations, collisionFlags, slopeLimit, slideOnGround, slideOnCeilings, forceSlide);
		collisionFlags |= MoveVertical(characterTransform, characterBody, vertVector, upDirection, contactFilter, hitList,
			contactOffset, positionIterations, collisionFlags, slopeLimit, slideOnGround, slideOnCeilings, forceSlide);
	}

	/// <summary>
	/// This is an initial movement query using the whole movement vector without decomposing it. In many cases the movement
	/// does not hit anything during the move call and splitting the movement is not needed. This initial call also solves 
	/// the case of jumping while on a slope. We'll use this method later to also solve the cases where the character
	/// is overlapping with other colliders before the move method is called.
	/// </summary>
	/// <param name="body"></param>
	/// <param name="direction"></param>
	/// <param name="filter"></param>
	/// <param name="results"></param>
	/// <param name="skinWidth"></param>
	/// <returns></returns>
	protected bool MoveInitial(Rigidbody2D body, Vector2 direction, ContactFilter2D filter, List<RaycastHit2D> results, float skinWidth)
	{
		float distance = direction.magnitude;
		int hits = body.Cast(direction, filter, results, distance + skinWidth);
		if (hits <= 0)
		{
			body.position += direction.normalized * distance;
			return false;
		}

		return true;
	}

	/// <summary>
	/// Contains the primary logic for the horizontal casting and movement of the character controller. This is a dominant
	/// movement method, meaning the vertical movement is adjusted according to the horizontal movement. This method
	/// moves the character up/down slopes, checks the slope limit and does slope clamping. Also does ground
	/// checks in each iteration.
	/// </summary>
	/// <param name="body"></param>
	/// <param name="direction"></param>
	/// <param name="filter"></param>
	/// <param name="results"></param>
	/// <param name="skinWidth"></param>
	/// <param name="maxIterations"></param>
	protected CollisionFlags2D MoveHorizontal(Transform transform, Rigidbody2D body, Vector2 direction, Vector2 upDirection, ContactFilter2D filter,
		List<RaycastHit2D> results, float skinWidth, int maxIterations, CollisionFlags2D flags, float maxAngle, bool slideGround, bool slideCeilings, bool forceSlideCharacter)
	{
		//---------------------------------------------------------------------------------------------------------
		// Start up code before iteration
		//---------------------------------------------------------------------------------------------------------
		Vector2 currentPosition = body.position;
		Vector2 currentDirection = direction;
		Vector2 targetPosition = currentPosition + currentDirection;

		int i = 0;
		while (i < maxIterations)
		{
			//---------------------------------------------------------------------------------------------------------
			// Cast in the horizontal direction
			//---------------------------------------------------------------------------------------------------------
			float distance = currentDirection.magnitude;
			int hits = body.Cast(currentDirection, filter, results, distance + skinWidth);

			//---------------------------------------------------------------------------------------------------------
			// Nothing hit, move straight to target position
			//---------------------------------------------------------------------------------------------------------
			if (hits <= 0)
			{
				currentPosition = targetPosition;
				break;
			}

			//---------------------------------------------------------------------------------------------------------
			// Horizontal collision detection
			// We first sort the list of hits and get the closest one. Then we update our the character controllers
			// position if the movement is larger than the skin width.
			//---------------------------------------------------------------------------------------------------------
			results.Sort((x, y) => y.distance.CompareTo(x.distance));
			RaycastHit2D hit = results[0];
			currentPosition = hit.distance > skinWidth ? currentPosition + currentDirection.normalized * (hit.distance - skinWidth) : currentPosition;

			//---------------------------------------------------------------------------------------------------------
			// We decide if we hit ceiling or ground
			//---------------------------------------------------------------------------------------------------------
			float normalDotUp = Vector2.Dot(hit.normal, upDirection);
			Vector2 verticalDirection = normalDotUp >= 0 ? upDirection : -upDirection;
			float angle = Vector2.Angle(verticalDirection, hit.normal);

			//---------------------------------------------------------------------------------------------------------
			// Here we decide if we should apply collision responses on the given geometry during the horizontal
			// collision call. We first find out if the character hits the ground or ceiling. After this
			// we calculate if the character hit with it's back or front and set the collision flags
			// accordingly. In the end we apply collision responses
			//---------------------------------------------------------------------------------------------------------
			bool applyCollisionResponse = false;

			if (normalDotUp >= 0)
			{
				if (angle <= maxAngle && slideGround)
				{
					flags |= CollisionFlags2D.SlightPoly;
					applyCollisionResponse = true;
				}
			}

			else
			{
				if (angle <= maxAngle && slideCeilings)
				{
					flags |= CollisionFlags2D.SlightPoly;
					applyCollisionResponse = true;
				}
			}

			if (Vector2.Dot(transform.right, hit.normal) < 0)
			{
				flags |= CollisionFlags2D.Front;
			}

			else
			{
				flags |= CollisionFlags2D.Back;
			}

			targetPosition = applyCollisionResponse ? CollisionResponse(currentPosition, targetPosition, hit.normal, 0, 0) : currentPosition;

			//---------------------------------------------------------------------------------------------------------
			// Move body to current position and get ready for the next iteration. If this is the last iteration,
			// the values calculated here will not be used.
			//---------------------------------------------------------------------------------------------------------
			currentDirection = targetPosition - currentPosition;
			body.position = currentPosition;

			//---------------------------------------------------------------------------------------------------------
			// If we already reached our target we can exit the method. Saves us iterations in case we have many
			// position updates. If we didn't have this, the method would iterate to the end each time
			//---------------------------------------------------------------------------------------------------------
			if (currentDirection.sqrMagnitude < Mathf.Epsilon * Mathf.Epsilon)
			{
				break;
			}

			i++;
		}

		//---------------------------------------------------------------------------------------------------------
		// Final position update
		//---------------------------------------------------------------------------------------------------------
		body.position = currentPosition;
		return flags;
	}

	/// <summary>
	/// Contains casting and movement logic for the vertical movement of the character. This is a secondary movement meaning
	/// i only handles simple logic like moving down or up if not on the ground.
	/// </summary>
	/// <param name="body"></param>
	/// <param name="direction"></param>
	/// <param name="filter"></param>
	/// <param name="results"></param>
	/// <param name="skinWidth"></param>
	/// <param name="maxIterations"></param>
	protected CollisionFlags2D MoveVertical(Transform transform, Rigidbody2D body, Vector2 direction, Vector2 upDirection, ContactFilter2D filter,
		List<RaycastHit2D> results, float skinWidth, int maxIterations, CollisionFlags2D flags, float maxAngle, bool slideGround, bool slideCeilings, bool forceSlideCharacter)
	{
		//---------------------------------------------------------------------------------------------------------
		// Start up code before iteration
		//---------------------------------------------------------------------------------------------------------
		Vector2 currentPosition = body.position;
		Vector2 currentDirection = direction;
		Vector2 targetPosition = currentPosition + currentDirection;

		int i = 0;
		while (i < maxIterations)
		{
			//---------------------------------------------------------------------------------------------------------
			// Cast in the horizontal direction
			//---------------------------------------------------------------------------------------------------------
			float distance = currentDirection.magnitude;
			int hits = body.Cast(currentDirection, filter, results, distance + skinWidth);

			//---------------------------------------------------------------------------------------------------------
			// Nothing hit, move straight to target position
			//---------------------------------------------------------------------------------------------------------
			if (hits <= 0)
			{
				currentPosition = targetPosition;
				break;
			}

			//---------------------------------------------------------------------------------------------------------
			// Vertical collision detection
			// We first sort the list of hits and get the closest one. Then we update our the character controllers
			// position if the movement is larger than the skin width.
			//---------------------------------------------------------------------------------------------------------
			results.Sort((x, y) => y.distance.CompareTo(x.distance));
			RaycastHit2D hit = results[0];
			currentPosition = hit.distance > skinWidth ? currentPosition + currentDirection.normalized * (hit.distance - skinWidth) : currentPosition;

			//---------------------------------------------------------------------------------------------------------
			// We decide if we hit ceiling or ground
			//---------------------------------------------------------------------------------------------------------
			float normalDotUp = Vector2.Dot(hit.normal, upDirection);
			Vector2 verticalDirection = normalDotUp >= 0 ? upDirection : -upDirection;
			float angle = Vector2.Angle(verticalDirection, hit.normal);

			//---------------------------------------------------------------------------------------------------------
			// Here we decide if we should apply collision responses on the given geometry during the vertical
			// collision call. We first find out if the character hits the ground or ceiling, then we
			// calculate the collision flags accordingly. In the end we apply collision responses.
			//---------------------------------------------------------------------------------------------------------
			bool applyCollisionResponse = false;

			if (normalDotUp >= 0)
			{
				if (angle > maxAngle && slideGround && forceSlideCharacter)
				{
					flags |= CollisionFlags2D.SteepPoly;
					applyCollisionResponse = true;
				}

				else
				{
					flags |= CollisionFlags2D.Bottom;
				}
			}

			else
			{
				if (angle > maxAngle && slideCeilings)
				{
					applyCollisionResponse = true;
					flags |= CollisionFlags2D.Top;
				}
			}

			targetPosition = applyCollisionResponse ? CollisionResponse(currentPosition, targetPosition, hit.normal, 0, 0) : currentPosition;

			//---------------------------------------------------------------------------------------------------------
			// Calculate new target position and Move the body to current position
			//---------------------------------------------------------------------------------------------------------
			currentDirection = targetPosition - currentPosition;
			body.position = currentPosition;

			//---------------------------------------------------------------------------------------------------------
			// Exit the method if we reached our destination. This prevents the loop from iterating all the way
			// to maxIterations each time, saving computation time.
			//---------------------------------------------------------------------------------------------------------
			if (currentDirection.sqrMagnitude < Mathf.Epsilon * Mathf.Epsilon)
			{
				break;
			}

			i++;
		}

		//---------------------------------------------------------------------------------------------------------
		// Final position update
		//---------------------------------------------------------------------------------------------------------
		body.position = currentPosition;
		return flags;
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="currentPosition">The current position of the character</param>
	/// <param name="initialTarget">The initial position the character is moving to</param>
	/// <param name="hitNormal">The normal vector of the geometry hit by the character</param>
	/// <param name="friction">The friction between the character and the geometry hit</param>
	/// <param name="bounciness">The bounciness between the character and the geometry hit</param>
	/// <returns></returns>
	protected Vector2 CollisionResponse(Vector2 currentPosition, Vector2 initialTarget, Vector2 hitNormal, float friction, float bounciness)
	{
		//---------------------------------------------------------------------------------------------------------
		// We reflect the incoming vector around the hit normal, and from this 
		// we calculate the tangent and normal velocities with respect to the colliding surface.
		//---------------------------------------------------------------------------------------------------------
		Vector2 direction = initialTarget - currentPosition;
		float remainingDistance = direction.magnitude;
		Vector2 reflected = Vector2.Reflect(direction, hitNormal);
		Vector2 projection = Vector2.Dot(reflected, hitNormal) * hitNormal;
		Vector2 tangent = reflected - projection;

		//---------------------------------------------------------------------------------------------------------
		// Compute the new target position using a linear model for the friction and bounciness. Notice that for
		// the friction part that we have (1 - friction). This is to ensure that 0 friction corresponds to no
		// resistance and 1 to max resistance. This also means that:
		// friction < 0 => higher velocity
		// friction > 1 => reversed velocity 
		//---------------------------------------------------------------------------------------------------------
		Vector2 targetPosition = currentPosition + bounciness * remainingDistance * projection.normalized + (1 - friction) * remainingDistance * tangent.normalized;

		return targetPosition;
	}

	/// <summary>
	/// The collision flags last frame
	/// </summary>
	public CollisionFlags2D GetCollisionFlags
	{
		get { return collisionFlags; }
	}

	/// <summary>
	/// If this controller is grounded or not.
	/// </summary>
	public bool IsGrounded
	{
		get { return (collisionFlags & CollisionFlags2D.Bottom) != 0; }
	}

	/// <summary>
	/// Is the character stuck between two objects? If so it will be crushed. This method checks
	/// if both collisionflags.Top and collisionFlags.Bottom is true or if
	/// collisionflags.Front and collisionflags.Back are true.
	/// </summary>
	public bool IsCrushed
	{
		get { return (collisionFlags & CollisionFlags2D.Bottom & CollisionFlags2D.Top) != 0 || (collisionFlags & CollisionFlags2D.Top & CollisionFlags2D.Bottom) != 0; }
	}

	/// <summary>
	/// Reset the collision flags of this gameObject to only None
	/// </summary>
	protected CollisionFlags2D ResetCollisionFlags(CollisionFlags2D collisions)
	{
		collisions |= CollisionFlags2D.None;
		collisions &= ~CollisionFlags2D.Back;
		collisions &= ~CollisionFlags2D.Top;
		collisions &= ~CollisionFlags2D.Bottom;
		collisions &= ~CollisionFlags2D.Front;
		collisions &= ~CollisionFlags2D.SteepPoly;
		collisions &= ~CollisionFlags2D.SlightPoly;

		return collisions;
	}

	/// <summary>
	/// The up direction of the controller.
	/// </summary>
	public Vector2 UpDirection
	{
		get { return characterUpDirection; }
		set { characterUpDirection = value; }
	}

	protected void Flip()
	{
		//transform.Rotate(0, 180f, 0);
		//facingRight = !facingRight;
	}
}

/// <summary>
/// Collision flags are used to give reports of what the character controller hits and where it hits.
/// </summary>
[Flags]
public enum CollisionFlags2D
{
	None = 0,
	Front = 1,
	Bottom = 2,
	Back = 4,
	Top = 8,
	SteepPoly = 16,
	SlightPoly = 32
}

