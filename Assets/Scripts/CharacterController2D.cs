using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]

///<summary>
/// The CharacterController2D is responsible for moving a kinematic rigidbody using custom unphysical
/// physics. It solves the simplest movement motion without a blocky and heavy dynamic rigidbody. 
/// 
///</summary>
public class CharacterController2D : MonoBehaviour
{
    [Tooltip("A small value that is added to the collision checking to prevent colliders getting stuck")]
    [SerializeField] protected float skinWidth = 0.01f;
    [SerializeField] protected float slopeLimit = 50f;
    [SerializeField] protected int iterations = 2;


    // Collision 
    protected Rigidbody2D rBody;
    protected Collider2D[] colliders = new Collider2D[16];
    protected ContactFilter2D contactFilter;
    protected List<Collider2D> colliderList = new List<Collider2D>(16);
    protected List<RaycastHit2D> hitList = new List<RaycastHit2D>(16);
    protected int attachedColliders;

    // Collision statuses
    protected bool isTouchingGround;
    protected bool isTouchingFrontWall;
    protected bool isTouchingBackWall;
    protected bool isTouchingCeiling;
  
    private void Awake()
    {
        // Set up rigidbody and colliders
        rBody = GetComponent<Rigidbody2D>();
        rBody.isKinematic = true;
        attachedColliders = rBody.GetAttachedColliders(colliders);

        // Set up our collision matrix
        contactFilter.useTriggers = false;
        contactFilter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
        contactFilter.useLayerMask = true;
    }

    public void Move(Vector2 deltaPosition)
    {
        // ALways set the ground movement to false before running the movement algorithm
        // The method will perform ground checks to change this value back.
        isTouchingGround = false;

        // Split the movement into ground and vertical movement
        Vector2 upVector = Vector2.up;
        Vector2 groundDir = new Vector2(deltaPosition.x, 0);
        Vector2 verticMove = new Vector2(0, deltaPosition.y);

        // Apply movement
        HandleGroundMovement(groundDir, verticMove, upVector,  iterations);
        HandleDownMovement(verticMove, upVector,  iterations);
    }

    // Movement along ground
    protected void HandleGroundMovement(Vector2 direction, Vector2 verticalDirection, Vector2 upDirection, int maxIterations)
	{
        float bounciness = 0;
        float friction = 1;

        Vector2 currentPosition = rBody.position;
        Vector2 targetPosition = rBody.position + direction;
        Vector2 currentUpDirection = upDirection;

        int i = 0;
        while(i < maxIterations)
		{
            // Ground check
            Sensor(-upDirection, skinWidth, upDirection);

            Vector2 currentDirection = targetPosition - currentPosition;
            if(currentDirection.sqrMagnitude < Mathf.Epsilon*Mathf.Epsilon)
			{
                break;
			}

            float distance = currentDirection.magnitude;

            int hits = rBody.Cast(currentDirection, contactFilter, hitList, distance);
            if (hits <= 0)
            {
                currentPosition = targetPosition;
                rBody.position = currentPosition;
            }

            else
            {
                // Do collision responses for all colliders on the character. Note that in many cases we only have
                // one collider. 
                for(int j=0; j<hits; j++)
				{
                    float adjustedDistance = hitList[j].distance - skinWidth;
                    distance = adjustedDistance < distance ? adjustedDistance : distance;
                    currentPosition += currentDirection.normalized * distance;

                    // Change the upDirection if the normalvector hit is a ceiling. 
                    if(Vector2.Dot(hitList[j].normal, upDirection) < 0)
					{
                        currentUpDirection = -upDirection;
					}

                    Vector2 reflected = Vector2.Reflect(currentDirection, hitList[j].normal).normalized;
                    Vector2 normalComponent = Vector2.Dot(reflected, hitList[j].normal) * hitList[j].normal;
                    Vector2 tangentComponent = reflected - normalComponent;

                    float remainingDistance = (targetPosition - currentPosition).magnitude;

                    // Handle the angle of collision
                    float angle = Vector2.Angle(currentUpDirection, hitList[j].normal);
                    if(angle <= slopeLimit)
					{
                        targetPosition = currentPosition + normalComponent.normalized * bounciness * remainingDistance + tangentComponent.normalized * friction * remainingDistance;
					}

                    else
					{
                        targetPosition = currentPosition;
					}

                    rBody.position = currentPosition;
                }
            }

            // Here, we check if we have run off a slope. We have run of a slope if we were grounded before movement
            // but not after the movement
            bool wasGrounded = isTouchingGround;
            Sensor(-upDirection, skinWidth, upDirection);
            if(wasGrounded && !isTouchingGround)
			{
                /*
                // If moving down a sloped platform moving upwards this migth fail due to the the player
                // getting vertical movement from the platform.
                if (verticalDirection.sqrMagnitude < Mathf.Epsilon * Mathf.Epsilon)
                {
                    // Clamp to slope
                    Debug.Log("Walked off slope");
                    hits = rBody.Cast(-upDirection, contactFilter, hitList, SlopeCastDistance(2f));
                    if(hits > 0)
					{
                        for(int j=0; j<hits; j++)
						{
                            if(Vector2.Angle(hitList[i].normal, upDirection) <= slopeLimit)
							{

							}
						}
					}
                }
                */
			}

            i++;
		}

        // Final position
        rBody.position = currentPosition;
	}

    // Movement downwards
    protected void HandleDownMovement(Vector2 direction, Vector2 upDirection, int maxIterations)
	{
        float bounciness = 0;
        float friction = 1.0f;

        Vector2 currentPosition = rBody.position;
        Vector2 targetPosition = rBody.position + direction;
        int i = 0;

        while(i < maxIterations)
		{
            // Direction and length
            Vector2 currentDirection = targetPosition - currentPosition;
            if(currentDirection.sqrMagnitude < Mathf.Epsilon*Mathf.Epsilon)
			{
                break;
			}

            float distance = currentDirection.magnitude;

            // Collision Detection
            int hits = rBody.Cast(currentDirection, contactFilter, hitList, distance);
            if(hits <= 0)
			{
                currentPosition = targetPosition;
                break;
			}

            else
			{
                for (int j = 0; j < hits; j++)
                {
                    // Movement
                    float modifiedDistance = hitList[j].distance - skinWidth;
                    distance = modifiedDistance < distance ? modifiedDistance : distance;
                    currentPosition += currentDirection.normalized * distance;

                    // Chose up or -up vector depending on the y orientation of the surface hit
                    if (Vector2.Dot(hitList[j].normal, upDirection) < 0)
                    {
                        upDirection = -upDirection;
                    }

                    // Collision resolution
                    Vector2 reflected = Vector2.Reflect(currentDirection, hitList[j].normal).normalized;
                    Vector2 normalComponent = Vector2.Dot(reflected, hitList[j].normal) * hitList[j].normal;
                    Vector2 tangentComponent = reflected - normalComponent;
                    float remainingDistance = (targetPosition - currentPosition).magnitude;

                    // Slope angle
                    float angle = Vector2.Angle(upDirection, hitList[j].normal);
                    if (angle > slopeLimit)
                    {
                        targetPosition = currentPosition + normalComponent.normalized * remainingDistance * bounciness + tangentComponent.normalized * remainingDistance * friction;
                    }
                    else
                    {
                        targetPosition = currentPosition;
                    }

                    rBody.position = currentPosition;
                }
            }

            i++;
		}

        rBody.position = currentPosition;
    }

    /// <summary>
    /// Sensor is used to check for touched geometry. Useful for ground checks etc
    /// </summary>
    /// <param name="direction"></param>
    /// <param name="distance"></param>
    /// <param name="upDirection"></param>
    protected void Sensor(Vector2 direction, float distance, Vector2 upDirection)
	{
        isTouchingGround = false;
        int hits = rBody.Cast(direction, contactFilter, hitList, distance);
        for(int i=0; i<hits; i++)
		{
            if(Vector2.Angle(hitList[i].normal, upDirection) <= slopeLimit)
			{
                isTouchingGround = true;
			}
		}
	}

    protected float SlopeCastDistance(float distance)
	{
        return distance;
	}

    public bool IsGrounded
	{
		get { return isTouchingGround; }
	}
}
