using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Character controller that handles sloped surfaces
/// </summary>
public class IntermediateCharacterController2D : MonoBehaviour, ICharacterController2D
{
    [Tooltip("Skin to prevent the character from penetrating other colliders")]
	[SerializeField] protected float contactOffset;

    [Tooltip("Number of iterations we allow for each frame")]
    [SerializeField] protected int positionIterations;

	protected Rigidbody2D characterBody;
	protected ContactFilter2D contactFilter;
	protected List<RaycastHit2D> hitList;

	private void Awake()
	{
		characterBody = GetComponent<Rigidbody2D>();
		characterBody.isKinematic = true;
		contactFilter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
		contactFilter.useLayerMask = true;
		hitList = new List<RaycastHit2D>(16);
	}

    /// <summary>
    /// Move character while being contrained by collisions
    /// </summary>
    /// <param name="deltaPosition"></param>
	public void Move(Vector2 deltaPosition)
	{
        CastAndMove(characterBody, deltaPosition, contactFilter, hitList, contactOffset, positionIterations);
    }

    /// <summary>
    /// Handle movement along the ground axis.
    /// </summary>
    /// <param name="body"></param>
    /// <param name="direction"></param>
    /// <param name="filter"></param>
    /// <param name="results"></param>
    /// <param name="skinWidth"></param>
    protected void CastAndMove(Rigidbody2D body, Vector2 direction, ContactFilter2D filter, List<RaycastHit2D> results, float skinWidth, int maxIterations)
	{
        //---------------------------------------------------------------------------------------------------------
        // Add startup code here before the iteration.
        //---------------------------------------------------------------------------------------------------------
        Vector2 currentPosition = body.position;
        Vector2 currentDirection = direction;
        Vector2 targetPosition = currentPosition + currentDirection;

        int i = 0;
        while(i < maxIterations)
		{
            //---------------------------------------------------------------------------------------------------------
            // The collision detection stage consists of casting the body in the given direction and length and
            // recieving the result in a CastResult structure. The length takes into account the
            // contact Offset/skinwidth. We then update the characters current position.
            //---------------------------------------------------------------------------------------------------------
            CastResult hit = CollisionDetection(body, currentDirection, filter, results, skinWidth);
            currentPosition += currentDirection.normalized * hit.Distance;

            //---------------------------------------------------------------------------------------------------------
            // The collision response stage consists of calculating a new target position from the hit recieved from
            // the collision detection stage. From the hit we calculate a new direction 
            //---------------------------------------------------------------------------------------------------------
            targetPosition = CollisionResponse(currentPosition, targetPosition, hit.Normal, 0, 0);
            currentDirection = targetPosition - currentPosition;

            //---------------------------------------------------------------------------------------------------------
            // At the end of the iteration we move the character's body to the current position. In the next iteration,
            // we'll move the character towards the deflected position we got from this iteration
            //---------------------------------------------------------------------------------------------------------
            body.position = currentPosition;

            i++;
		}
        //---------------------------------------------------------------------------------------------------------
        // Before we exit the method we make sure the character is sent to the right position.
        //---------------------------------------------------------------------------------------------------------
        body.position = currentPosition;
    }

    /// <summary>
    /// Cast the rigidbodies colliders in the direction and return the closest hit in  a castResult
    /// struct. 
    /// </summary>
    /// <param name="body"></param>
    /// <param name="direction"></param>
    /// <param name="filter"></param>
    /// <param name="results"></param>
    /// <param name="skinWidth"></param>
    protected CastResult CollisionDetection(Rigidbody2D body, Vector2 direction, ContactFilter2D filter, List<RaycastHit2D> results, float skinWidth)
	{
        //---------------------------------------------------------------------------------------------------------
        // Define values to return if nothing's hit
        //---------------------------------------------------------------------------------------------------------
        float distance = direction.magnitude;
        Rigidbody2D returnedBody = null;
        Vector2 normal = Vector2.zero;

        //---------------------------------------------------------------------------------------------------------
        // If we hit something, we should loop over all the hits and keep the one with the shortest
        // distance.
        //---------------------------------------------------------------------------------------------------------
        int hits = body.Cast(direction, filter, results, distance + skinWidth);
        for(int i=0; i<hits; i++)
		{
            float adjustedDistance = results[i].distance - skinWidth;

            if(adjustedDistance < distance)
			{
                distance = adjustedDistance;
                returnedBody = results[i].rigidbody;
                normal = results[i].normal;
			}
		}

        return new CastResult(returnedBody, normal, distance);
	}

    /// <summary>
    /// Compute the collision response using friction and bounciness
    /// <paramref name="currentPosition"/> Position of collision
    /// <paramref name="initialTarget"/> The target position before the collision
    /// <paramref name="hitNormal"/> The normal vector of the touched surface
    /// <paramref name="friction"/> Friction of the collision
    /// <paramref name="bounciness"/> Bounciness of the collision
    /// </summary>
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

    protected readonly struct CastResult
	{
        public readonly Rigidbody2D Rigidbody2D { get; }
        public readonly Vector2 Normal { get; }
        public readonly float Distance { get; }

        public CastResult(Rigidbody2D rigidbody, Vector2 normal, float distance)
        {
            Rigidbody2D = rigidbody;
            Normal = normal;
            Distance = distance;
        }
    }

    public bool IsGrounded
	{
		get { return false; }
	}

	public CollisionFlags2D GetCollisionFlags => throw new System.NotImplementedException();
}
