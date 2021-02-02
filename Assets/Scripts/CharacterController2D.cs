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
    protected ContactFilter2D contactFilter;
    protected List<Collider2D> colliderList = new List<Collider2D>(16);
    protected List<RaycastHit2D> hitList = new List<RaycastHit2D>(16);
    protected Bounds bounds;

  
    private void Awake()
    {
        // Set up rigidbody
        rBody = GetComponent<Rigidbody2D>();
        rBody.isKinematic = true;

        // Set up our collision matrix
        contactFilter.useTriggers = false;
        contactFilter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
        contactFilter.useLayerMask = true;
    }

    public void Move(Vector2 deltaPosition)
    {
        // TODO
        // WHEN SLIDING AGAINST A WALL THE SPEED IN THE OTHER AXIS IS SLOWED. MUST FIND OUT WHY
        Vector2 upVector = Vector2.up;
        Vector2 groundDir = new Vector2(deltaPosition.x, 0);
        Vector2 verticMove = new Vector2(0, deltaPosition.y);

        HandleGroundMovement(groundDir, upVector, iterations);
        HandleDownMovement(verticMove, upVector, iterations);

    }

    // Movement along ground
    private void HandleGroundMovement(Vector2 direction, Vector2 upDirection, int maxIterations)
	{
        float bounciness = 0;
        float friction = 1;

        Vector2 currentPosition = rBody.position;
        Vector2 targetPosition = rBody.position + direction;
        int i = 0;
        while(i < maxIterations)
		{
            Vector2 currentDirection = targetPosition - currentPosition;
            if(currentDirection.sqrMagnitude < Mathf.Epsilon*Mathf.Epsilon)
			{
                break;
			}

            float distance = currentDirection.magnitude;

            // Detection
            int hits = rBody.Cast(currentDirection, contactFilter, hitList, distance);
            if(hits <= 0)
			{
                currentPosition = targetPosition;
                break;
			}

            // Find the closest hit
            RaycastHit2D hit = FindClosestRaycastHit2D(hitList);
            
            // Movement
            float modifiedDistance = hit.distance - skinWidth;
            distance = modifiedDistance < distance ? modifiedDistance : distance;
            currentPosition += currentDirection.normalized*distance;

            // Direction vector for angle calculations
            if (Vector2.Dot(hit.normal, upDirection) < 0)
			{
                upDirection = -upDirection;
			}

            // Collision Resolution
            Vector2 reflected = Vector2.Reflect(currentDirection, hit.normal).normalized;
            Vector2 normalComponent = Vector2.Dot(reflected, hit.normal) * hit.normal;
            Vector2 tangentComponent = reflected - normalComponent;

            float amplitude = (targetPosition - currentPosition).magnitude;

            // Handle angle
            float angle = Vector2.Angle(upDirection, hit.normal);
            if (angle <= slopeLimit)
            {
                targetPosition = currentPosition + normalComponent.normalized * bounciness * amplitude + tangentComponent.normalized * friction * amplitude;
            }

            else
			{
                targetPosition = currentPosition;
			}

            rBody.position = currentPosition;
            i++;
		}

        // Final position
        rBody.position = currentPosition;
	}

    // Movement downwards
    private void HandleDownMovement(Vector2 direction, Vector2 upDirection, int maxIterations)
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

            RaycastHit2D hit = FindClosestRaycastHit2D(hitList);

            // Movement
            float modifiedDistance = hit.distance - skinWidth;
            distance = modifiedDistance < distance ? modifiedDistance : distance;
            currentPosition += currentDirection.normalized*distance;

            // Chose up or -up vector depending on the y orientation of the surface hit
            if(Vector2.Dot(hit.normal, upDirection) < 0)
			{
                upDirection = -upDirection;
			}

            // Collision resolution
            Vector2 reflected = Vector2.Reflect(currentDirection, hit.normal).normalized;
            Vector2 normalComponent = Vector2.Dot(reflected, hit.normal) * hit.normal;
            Vector2 tangentComponent = reflected - normalComponent;
            float remainingDistance = (targetPosition - currentPosition).magnitude;

            // Slope angle
            float angle = Vector2.Angle(upDirection, hit.normal);
            if(angle > slopeLimit)
			{
                targetPosition = currentPosition + normalComponent.normalized * remainingDistance * bounciness + tangentComponent.normalized * remainingDistance * friction;
			}
            else
			{
                targetPosition = currentPosition;
			}

            //rBody.MovePosition(currentPosition);
            rBody.position = currentPosition;
            i++;
		}
        //rBody.MovePosition(currentPosition);
        rBody.position = currentPosition;
    }

    // Movement upwards
    private void HandleUpMovement(ref Vector2 targetPosition, int maxIterations)
	{

	}

    // Find closest hit from cast
    private RaycastHit2D FindClosestRaycastHit2D(List<RaycastHit2D> hits)
	{
        RaycastHit2D result = default;
        if(hits != null)
        {
            int size = hits.Count;

            if (size <= 0)
            {
                return default;
            }

            else if (size == 1)
            {
                return hits[0];
            }

            else
            {
                result = hits[0];
                for (int i = 1; i < hits.Count; i++)
                {
                    if(hits[i].distance < result.distance)
				    {
                        result = hits[i];
					}
                }
                return result;
            }
        }

        else
		{
            return result;
		}
	}
}
