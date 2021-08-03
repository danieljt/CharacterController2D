using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]

///<summary>
/// The CharacterController2D is responsible for moving a kinematic rigidbody using custom unphysical
/// physics. It solves the simplest movement motion without a blocky and heavy dynamic rigidbody. 
/// 
///</summary>
public class CharacterController2D : MonoBehaviour, ICharacterController2D
{
    [Tooltip("A small value that is added to the collision checking to prevent colliders getting stuck")]
    [SerializeField] protected float skinWidth = 0.01f;
    [Tooltip("The maximum slope angle the controller can climb. Also determines what angle")]
    [SerializeField] protected float slopeLimit = 50f;
    [Tooltip("The number of position iterations in each move call. Higher values increase accuracy and are more expensive")]
    [SerializeField] protected int iterations = 2;

    // Collision 
    protected Rigidbody2D rBody;
    protected Collider2D[] colliders = new Collider2D[16];
    protected ContactFilter2D contactFilter;
    protected List<RaycastHit2D> hitList = new List<RaycastHit2D>(16);

    // Collision statuses
    protected bool isTouchingGround;
    protected bool isTouchingFrontWall;
    protected bool isTouchingBackWall;
    protected bool isTouchingCeiling;
    Vector2 groundNormal;

    // DEBUG
    protected Vector2 gizmonormal;
    protected Vector2 gizmotangent;
 
    private void Awake()
    {
        // Set up rigidbody and colliders
        rBody = GetComponent<Rigidbody2D>();
        rBody.isKinematic = true;

        // Set up our collision matrix
        contactFilter.useTriggers = false;
        contactFilter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
        contactFilter.useLayerMask = true;
    }

    public void Move(Vector2 deltaPosition)
    {
        // Split the movement into ground and vertical movement. 
        Vector2 upVector = Vector2.up;
		Vector2 groundDir = new Vector2(deltaPosition.x, 0);
		Vector2 verticMove = new Vector2(0, deltaPosition.y);

        // Apply movement
        HandleGroundMovement(rBody, groundDir, upVector, deltaPosition.y > 0.05f, iterations);
        HandleDownMovement(rBody, verticMove, upVector, iterations);
    }

    /// <summary>
    /// Movement along ground. The movement of the 2D character controller is divided into two movements.
    /// The movement along the ground and the vertical movement. The ground movement is the dominant
    /// movement method as it handles movement along slopes. 
    /// </summary>
    /// <param name="direction"></param>
    /// <param name="previous"></param>
    /// <param name="upDirection"></param>
    /// <param name="maxIterations"></param>
    protected void HandleGroundMovement(Rigidbody2D body, Vector2 direction, Vector2 upDirection, bool upMovement, int maxIterations)
	{
        //---------------------------------------------------------------------------------------------------------
        // Define initial values. These values will change during the loop and contain the final
        // values at the end of the method
        //---------------------------------------------------------------------------------------------------------
        Vector2 currentPosition = body.position;
        Vector2 currentDirection = direction;
        bool isGrounded = isTouchingGround;
        Vector2 normal = groundNormal;

        //---------------------------------------------------------------------------------------------------------
        // This is the main loop. During iteration the position will be divided into submovements depending on
        // collisions. Note that the more iterations the slower this method becomes. Classic 
        // maxIterations <= 0 ===> No movement
        // maxIterations == 1 ===> No movement on slopes
        // maxIterations >= 2 ===> Movement on slopes
        //---------------------------------------------------------------------------------------------------------
        int i = 0;
        while(i < maxIterations)
		{
            //---------------------------------------------------------------------------------------------------------
            // Adjust direction to follow the ground, but only if there is no movement in the 
            // positive upwards direction
            //---------------------------------------------------------------------------------------------------------
            float distance = currentDirection.magnitude;
            if (!upMovement)
            {
                Vector2 reflected = Vector2.Reflect(currentDirection, normal);
                Vector2 projection = Vector2.Dot(reflected, normal) * normal;
                currentDirection = reflected - projection;
            }

            Vector2 targetPosition = currentPosition + currentDirection;

            //---------------------------------------------------------------------------------------------------------
            // For small movements break out of the loop. This is useful so we don't do any more calculations
            // than nescessary. This can cause bugs if the character is standing still and a moving object hits it.
            // The object will go straight through the character.
            //---------------------------------------------------------------------------------------------------------
            if (distance < Mathf.Epsilon)
			{
                break;
			}

            //---------------------------------------------------------------------------------------------------------
            // Cast in the movement direction and calculate the new target position. This is the core of the 
            // collide and slide algorithm
            //---------------------------------------------------------------------------------------------------------
            CastResult hit = Cast(body, currentDirection, contactFilter, hitList, distance, skinWidth);
            currentPosition += currentDirection.normalized * hit.Distance;

            //---------------------------------------------------------------------------------------------------------
            // Flip the up axis in case we hit a ceiling. We do this to avoid angles larger than 90 degrees
            //---------------------------------------------------------------------------------------------------------
            Vector2 adjustedAxis = upDirection;
            if(Vector2.Dot(upDirection, hit.Normal) < 0)
			{
                adjustedAxis = -upDirection;
			}

            //---------------------------------------------------------------------------------------------------------
            // See if we hit a valid surface to climb. This is decided by the slope limit
            //---------------------------------------------------------------------------------------------------------
            float angle = Vector2.Angle(adjustedAxis, hit.Normal);
            bool collided = false;

            if(angle <= slopeLimit)
			{
                targetPosition = CollisionResponse(currentPosition, targetPosition, hit.Normal, 0, 0);
                //normal = hit.Normal;
                collided = true;
			}

            else
			{
                targetPosition = currentPosition;
			}

            //---------------------------------------------------------------------------------------------------------
            // If no collision was found from the cast do a quick sensor check to see if we are actually grounded.
            // However if a collision was found we use the normal found from the collision. Using this we can also clamp 
            // down on slopes to a certain degree.
            //---------------------------------------------------------------------------------------------------------
            if(!collided && !upMovement)
            {
                CastResult ground = Cast(body, -upDirection, contactFilter, hitList, 5 * skinWidth, skinWidth);
                adjustedAxis = upDirection;
                if(Vector2.Dot(upDirection, ground.Normal) < 0)
				{
                    adjustedAxis = -upDirection;
				}

                angle = Vector2.Angle(adjustedAxis, ground.Normal);
                if(angle <= slopeLimit)
				{
                    normal = ground.Normal;
                    isGrounded = true;
				}
                else
				{
                    isGrounded = false;
                    normal = Vector2.zero;
				}

                //---------------------------------------------------------------------------------------------------------
                // Clamp down on slope if the sensor detected it and do the usual collision responses
                //---------------------------------------------------------------------------------------------------------
                if (isGrounded)
				{
                    currentPosition -= upDirection.normalized * ground.Distance;
                }
            }

            else
            {
                //normal = movement.Normal;
                normal = hit.Normal;
			}

            currentDirection = targetPosition - currentPosition;
            body.position = currentPosition;

            i++;
		}

        //---------------------------------------------------------------------------------------------------------
        // Final position and values
        //---------------------------------------------------------------------------------------------------------
        isTouchingGround = isGrounded;
        groundNormal = normal;
        body.position = currentPosition;
	}

    /// <summary>
    /// Vertical movement. This method is a secondary movement used to correct the ground movement method.
    /// This class handles sliding down slopes
    /// </summary>
    /// <param name="direction"></param>
    /// <param name="upDirection"></param>
    /// <param name="maxIterations"></param>
    protected void HandleDownMovement(Rigidbody2D body, Vector2 direction, Vector2 upDirection, int maxIterations)
	{
        //---------------------------------------------------------------------------------------------------------
        // Define initial values. These will change during the loop and hold the final values
        // at the end of the method.
        //---------------------------------------------------------------------------------------------------------
        Vector2 currentPosition = body.position;
        Vector2 currentDirection = direction;
        //bool isGrounded = isTouchingGround;

        //---------------------------------------------------------------------------------------------------------
        // Main loop
        //---------------------------------------------------------------------------------------------------------
        int i = 0;
        while (i < maxIterations)
		{
            float distance = currentDirection.magnitude;
            Vector2 targetPosition = currentPosition + currentDirection;

            //---------------------------------------------------------------------------------------------------------
            // End if the distance is small. Should probably redo this since no collision with 
            // dynamic objects will occur when the direction in the method call is zero.
            //---------------------------------------------------------------------------------------------------------
            if (distance < Mathf.Epsilon)
			{
                break;
			}

            //---------------------------------------------------------------------------------------------------------
            // Cast in the movement direction. The cast distance is the sum of the distance and skinwidth
            // to prevent the character getting stuck in the incoming collider. We update the
            // players position to this position.
            //---------------------------------------------------------------------------------------------------------
            CastResult hit = Cast(body, currentDirection, contactFilter, hitList, distance, skinWidth);
            currentPosition += currentDirection.normalized * hit.Distance;

            //---------------------------------------------------------------------------------------------------------
            // Get an axis and flip it so that we get the right angles when hitting ceilings. This is to assure we 
            // keep our angles below 90 degrees.
            //---------------------------------------------------------------------------------------------------------
            Vector2 adjustedAxis = upDirection;
            if (Vector2.Dot(upDirection, hit.Normal) < 0)
            {
                adjustedAxis = -upDirection;
            }

            //---------------------------------------------------------------------------------------------------------
            // Get the angle and check if it is larger than the slope limit. On the up/down motions
            // We only want to do collision responses when we are at angles over the slope limit
            //---------------------------------------------------------------------------------------------------------
            float angle = Vector2.Angle(adjustedAxis, hit.Normal);
            if (angle > slopeLimit)
            {
                targetPosition = CollisionResponse(currentPosition, targetPosition, hit.Normal, 0, 0);
                //isGrounded = false;
            }

            else
			{
                targetPosition = currentPosition;
                //isGrounded = true;
			}

            //---------------------------------------------------------------------------------------------------------
            // Set the new values before the next iteration
            //---------------------------------------------------------------------------------------------------------
            currentDirection = targetPosition - currentPosition;
            body.position = currentPosition;
            i++;
		}

        Debug.Log(isTouchingGround);
        //isTouchingGround = isGrounded;
        body.position = currentPosition;
    }

    /// <summary>
    /// Casts the rigidbody in the scene, collects all hits and returns the most significant hit. The most significant hit
    /// being the one with the largest opposing velocity to the characters movement. In the case of
    /// no collisions this method returns the default value of the raycastHit2D.
    /// </summary>
    /// <param name="body"></param>
    /// <param name="direction"></param>
    /// <param name="filter"></param>
    /// <param name="results"></param>
    /// <param name="distance"></param>
    /// <param name="skinDistance"></param>
    /// <param name="upDirection"></param>
    /// <param name="maxAngle"></param>
    /// <returns></returns>
    protected CastResult Cast(Rigidbody2D body, Vector2 direction, ContactFilter2D filter, List<RaycastHit2D> results, float distance, float skinDistance)
	{
        //---------------------------------------------------------------------------------------------------------
        // Initial default values. These will change during the iteration
        //---------------------------------------------------------------------------------------------------------
        Rigidbody2D rigidbody = null;
        Vector2 normal = Vector2.zero;
        int hits = body.Cast(direction, filter, results, distance + skinDistance);

        //---------------------------------------------------------------------------------------------------------
        // Find the most significant hit during the loop by checking the opposing velocities
        // of all hits. This is done by checking the dot products of the characters direction versus
        // the hit velocities.
        //---------------------------------------------------------------------------------------------------------
        for (int i=0; i<hits; i++)
		{
            if (rigidbody)
            {
                if (Vector2.Dot(direction, results[i].rigidbody.velocity) < Vector2.Dot(direction, rigidbody.velocity))
                {
                    rigidbody = results[i].rigidbody;
                    normal = results[i].normal;
                    float adjustedDistance = results[i].distance - skinDistance;
                    distance = adjustedDistance < distance ? adjustedDistance : distance;
                }
            }

            else
			{
                rigidbody = results[i].rigidbody;
                normal = results[i].normal;
                float adjustedDistance = results[i].distance - skinDistance;
                distance = adjustedDistance < distance ? adjustedDistance : distance;
            }
		}

        return new CastResult(rigidbody, normal, distance);
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
        // friction > 0 => velocity against 
        //---------------------------------------------------------------------------------------------------------
        Vector2 targetPosition = currentPosition + bounciness * remainingDistance * projection.normalized + (1 - friction) * remainingDistance * tangent.normalized;

        return targetPosition;
	}

    /// <summary>
    /// Grounded
    /// </summary>
    public bool IsGrounded
	{
		get { return isTouchingGround; }
	}

    /// <summary>
    /// A cast result
    /// </summary>
    protected struct CastResult
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

	private void OnDrawGizmos()
	{
        Debug.DrawRay(transform.position, gizmonormal.normalized*2, Color.red);
        Debug.DrawRay(transform.position, gizmotangent.normalized*2, Color.blue);
        Gizmos.DrawWireCube(transform.position + skinWidth * Vector3.down, Vector2.one);
    }
}
