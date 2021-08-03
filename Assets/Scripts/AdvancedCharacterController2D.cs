using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The advanced character controller taking slopes, ground and ceilings into account. 
/// </summary>
public class AdvancedCharacterController2D : MonoBehaviour, ICharacterController2D
{
	[Tooltip("Skin to prevent the character from penetrating other colliders")]
	[SerializeField] protected float contactOffset;

	[Tooltip("Number of iterations we allow for each frame")]
	[SerializeField] protected int positionIterations;

	[Tooltip("The maximum slope the character can climb")]
	[SerializeField] protected float slopeLimit;

	[Tooltip("Should the character force slide down slopes steeper than the slopelimit?")]
	[SerializeField] protected bool forceSlide;

	[Tooltip("Should the Character collide and slide on ceilings the same way as on the ground?")]
	[SerializeField] protected bool slideOnCeilings;

	// Collision
	protected Rigidbody2D characterBody;
	protected ContactFilter2D contactFilter;
	protected List<RaycastHit2D> hitList;
	protected CollisionFlags collisionFlags;

	// Orientation
	protected Vector2 characterUpDirection;
	
	// Previous frame values
	protected bool characterIsGrounded;

	private void Awake()
	{
		characterBody = GetComponent<Rigidbody2D>();
		characterBody.isKinematic = true;
		contactFilter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
		contactFilter.useLayerMask = true;
		hitList = new List<RaycastHit2D>(16);
		collisionFlags = CollisionFlags.None;
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
		ResetCollisionFlags();

		//---------------------------------------------------------------------------------------------------------
		// We first get our up direction and calculate the dot product with respect to the deltaposition direction.
		// We also calculate the total distance to travel this frame.
		//---------------------------------------------------------------------------------------------------------
		Vector2 upDirection = characterUpDirection;

		//---------------------------------------------------------------------------------------------------------
		// Initial cast. Here we use the full movement vector before splitting into side/up movement. If no
		// collisions are found we can early exit the method.
		//---------------------------------------------------------------------------------------------------------
		if(!MoveInitial(characterBody, deltaPosition, contactFilter, hitList, contactOffset))
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
		MoveHorizontal(characterBody, sideVector, upDirection, contactFilter, hitList, contactOffset, positionIterations);
		MoveVertical(characterBody, vertVector, upDirection, contactFilter, hitList, contactOffset, positionIterations);
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
		CastResult hit = CollisionDetection(body, direction, filter, results, skinWidth);
		if(!hit.Collided)
		{
			body.position += direction.normalized * hit.Distance;
		}

		return hit.Collided;
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
	protected void MoveHorizontal(Rigidbody2D body, Vector2 direction, Vector2 upDirection, ContactFilter2D filter, List<RaycastHit2D> results, float skinWidth, int maxIterations)
	{
		//---------------------------------------------------------------------------------------------------------
		// Start up code before iteration
		//---------------------------------------------------------------------------------------------------------
		Vector2 currentPosition = body.position;
		Vector2 currentDirection = direction;
		Vector2 targetPosition = currentPosition + currentDirection;

		int i = 0;
		while(i < maxIterations)
		{
			//---------------------------------------------------------------------------------------------------------
			// Cast in the horizontal direction
			//---------------------------------------------------------------------------------------------------------
			float distance = currentDirection.magnitude;
			int hits = body.Cast(currentDirection, filter, results, distance + skinWidth);

			// Nothing hit, move straight to target position
			if(hits <= 0)
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
			currentPosition = hit.distance > skinWidth ? currentPosition + currentDirection.normalized * (hit.distance-skinWidth) : currentPosition; 

			//---------------------------------------------------------------------------------------------------------
			// There are a number of conditions that should be met for us to apply the collide and slide method
			// for the horizontal movement:
			// - The angle needs to be less than the slope limit.
			// - We need to check if the character hits a ceiling and if we allow ceiling collision responses.
			// - We need to check if the character hits ground and if we allow ground collision responses.
			//---------------------------------------------------------------------------------------------------------
			Vector2 verticalDirection = Vector2.Dot(hit.normal, upDirection) >= 0 ? upDirection : -upDirection;
			float angle = Vector2.Angle(verticalDirection, hit.normal); 
			bool applyCollisionRespone = angle <= slopeLimit && CanSlideOnSlope(hit.normal, upDirection, true, slideOnCeilings);
			targetPosition = applyCollisionRespone ? CollisionResponse(currentPosition, targetPosition, hit.normal, 0, 0) : currentPosition;

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
			if (currentDirection.sqrMagnitude < Mathf.Epsilon*Mathf.Epsilon)
			{
				break;
			}

			i++;
		}

		// Final position update
		body.position = currentPosition;
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
	protected void MoveVertical(Rigidbody2D body, Vector2 direction, Vector2 upDirection, ContactFilter2D filter, List<RaycastHit2D> results, float skinWidth, int maxIterations)
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

			// Nothing hit, move straight to target position
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
			// There are a number of conditions that should be met for us to apply the collide and slide method in the
			// vertical movement:
			// - The angle needs to be larger than the slope limit.
			// - We need to check if the character hits a ceiling and if we allow ceiling collision responses.
			// - We need to check if the character hits ground and if we allow ground collision responses.
			//---------------------------------------------------------------------------------------------------------
			Vector2 verticalDirection = Vector2.Dot(hit.normal, upDirection) >= 0 ? upDirection : -upDirection;
			float angle = Vector2.Angle(verticalDirection, hit.normal);
			bool applyCollisionRespone = angle > slopeLimit && CanSlideOnSlope(hit.normal, upDirection, forceSlide, slideOnCeilings);
			targetPosition = applyCollisionRespone ? CollisionResponse(currentPosition, targetPosition, hit.normal, 0, 0) : currentPosition;

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

		// Final position update
		body.position = currentPosition;
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="body"></param>
	/// <param name="direction"></param>
	/// <param name="filter"></param>
	/// <param name="results"></param>
	/// <param name="skinWidth"></param>
	/// <returns></returns>
	protected CastResult CollisionDetection(Rigidbody2D body, Vector2 direction, ContactFilter2D filter, List<RaycastHit2D> results, float skinWidth)
	{
		//---------------------------------------------------------------------------------------------------------
		// Define values to return if nothing's hit.
		//---------------------------------------------------------------------------------------------------------
		float distance = direction.magnitude;
		Rigidbody2D returnedBody = null;
		Vector2 normal = Vector2.zero;

		//---------------------------------------------------------------------------------------------------------
		// If we hit something, loop over all the hits and keep the one with the shortest distance. 
		//---------------------------------------------------------------------------------------------------------
		int hits = body.Cast(direction, filter, results, distance + skinWidth);
		for (int i = 0; i < hits; i++)
		{
			float adjustedDistance = results[i].distance - skinWidth;

			if (adjustedDistance < distance)
			{
				distance = adjustedDistance;
				returnedBody = results[i].rigidbody;
				normal = results[i].normal;
			}
		}

		return new CastResult(returnedBody, normal, distance, hits > 0);
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="currentPosition"></param>
	/// <param name="initialTarget"></param>
	/// <param name="hitNormal"></param>
	/// <param name="friction"></param>
	/// <param name="bounciness"></param>
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
	/// Is the slope valid for collide and slide? 
	/// </summary>
	/// <param name="collisionNormal">Collision normal in world space</param>
	/// <param name="upDirection">Defined up direction</param>
	/// <param name="allowSlideOnGround">Can we slide on ground slopes?</param>
	/// <param name="allowSlideOnCeiling">Can we slide on ceiling slopes?</param> 
	/// <returns>If we can slide on the slope or not.</returns>
	protected bool CanSlideOnSlope(Vector2 collisionNormal, Vector2 upDirection, bool allowSlideOnGround, bool allowSlideOnCeiling)
	{
		float normalDotUp = Vector2.Dot(upDirection, collisionNormal);
		return (normalDotUp >= 0 && allowSlideOnGround) || (normalDotUp < 0 && allowSlideOnCeiling);
	}

	/// <summary>
	/// Contains information from a character controller cast. Uses a subset of the values obtained in the RaycastHit2D
	/// structure that are useful for the controller.
	/// </summary>
	protected struct CastResult
	{
		public readonly Rigidbody2D Rigidbody2D { get; }
		public readonly Vector2 Normal { get; }
		public readonly float Distance { get; }
		public readonly bool Collided { get; }

		public CastResult(Rigidbody2D rigidbody, Vector2 normal, float distance, bool collided)
		{
			Rigidbody2D = rigidbody;
			Normal = normal;
			Distance = distance;
			Collided = collided;
		}
	}

	/// <summary>
	/// Bitmask telling where collisions on the character controller have taken place. The mask takes into account
	/// the local transform of the objects collider(Transform.up, transform.right) into account. Front is in the transform.right
	/// direction, Top is in the transform.up direction, Back is in the (-transform.right) direction and bottom is in
	/// the (-transform.up) direction. Collision flags are useful for giving the character certain behaviours like being
	/// grounded when Bottom is true or crushed is both front and back are true.
	/// </summary>
	[Flags]
	protected enum CollisionFlags
	{
		None = 0,
		Front = 1,
		Bottom = 2,
		Back = 4,
		Top = 8
	}

	/// <summary>
	/// If this controller is grounded or not.
	/// </summary>
	public bool IsGrounded
	{
		get { return characterIsGrounded; }
	}

	/// <summary>
	/// The up direction of the controller.
	/// </summary>
	public Vector2 UpDirection
	{
		get { return characterUpDirection; }
		set { characterUpDirection = value; }
	}

	/// <summary>
	/// Reset the collision flags of this gameObject to only None
	/// </summary>
	protected void ResetCollisionFlags()
	{
		collisionFlags &= ~CollisionFlags.Back;
		collisionFlags &= ~CollisionFlags.Top;
		collisionFlags &= ~CollisionFlags.Bottom;
		collisionFlags &= ~CollisionFlags.Front;
	}
}
