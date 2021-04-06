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
    [Tooltip("The maximum slope angle the controller can climb. Also determines what angle")]
    [SerializeField] protected float slopeLimit = 50f;
    [Tooltip("The number of position iterations in each move call. Higher values increase accuracy and are more expensive")]
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

    // DEBUG
    protected Vector2 gizmonormal;
    protected Vector2 gizmotangent;

  
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
        // The method will perform ground checks to change this value back on each call.
        isTouchingGround = false;

        // Split the movement into ground and vertical movement. 
        Vector2 upVector = Vector2.up;
        Vector2 groundDir = new Vector2(deltaPosition.x, 0);
        Vector2 verticMove = new Vector2(0, deltaPosition.y);

        // Apply movement
        HandleGroundMovement(groundDir, upVector, iterations);
        //HandleDownMovement(verticMove, upVector, iterations);
    }

    /// <summary>
    /// Movement along ground. The movement of the 2D character controller is divided into two movements.
    /// The movement along the ground and the vertical movement. The ground movement is the dominant
    /// movement method as it handles movement along slopes. 
    /// </summary>
    /// <param name="direction"></param>
    /// <param name="verticalDirection"></param>
    /// <param name="upDirection"></param>
    /// <param name="maxIterations"></param>
    protected void HandleGroundMovement(Vector2 direction, Vector2 upDirection, int maxIterations)
	{
        //---------------------------------------------------------------------------------------------------------
        // Store the current position of the rigidbody and calculate it's target position
        // These variables will be changed during the course of the method and the rigidbody
        // will be moved to the last currentposition registered
        //
        // NOTICE
        //---------------------------------------------------------------------------------------------------------
        // Check if this is needed. It might be best to have more variables in the method call instead of declaring
        // variables here. 
        //---------------------------------------------------------------------------------------------------------

        Vector2 currentPosition = rBody.position;
        Vector2 targetPosition = rBody.position + direction;
        bool isGrounded = false;

        int i = 0;
        while(i < maxIterations)
		{
            //---------------------------------------------------------------------------------------------------------
            // The first thing we do is set the start position to the characters position and calculate the direction
            // and distance to travel.
            //
            // NOTICE
            //---------------------------------------------------------------------------------------------------------
            // We calculate the distance directly before doing anything else. It would of course be better to skip
            // that calculation before necessary but the current build of the controller forces us to 
            // calculate it directly
            //---------------------------------------------------------------------------------------------------------
            Vector2 currentDirection = targetPosition - currentPosition;
            float distance = currentDirection.magnitude;


            // We first do a ground check to see if the character is grounded. This is to align our movement
            // with a slope if we are standing on one. It is also needed for later as we need to check if the
            // character needs to clamp to a slope if it has launched off one.
            // 
            // NOTICE
            //---------------------------------------------------------------------------------------------------------
            // The sensor can be used to check for walls and the ceiling as well. Adjust the direction accordingly.
            // The sensor check can fail if objects are already penetrating the character. Consider doing an
            // overlap check before applying movement or implementing code on the colliding objects to move the player.
            //---------------------------------------------------------------------------------------------------------

            // THE FOLLOWING CODE CREATES WIERD BEHAVIOUR.
            // The ground check is performed on each iteration meaning it sometimes cancelles out the target
            // position.
            //---------------------------------------------------------------------------------------------------------
            
            // THIS CODE OVERRITES THE MOVEMENTRESULT FROM THE LAST ITERATION. PLAYER GET'S STUCK
            // ON SLOPES DUE TO THIS!!!
            SensorResult firstGroundCheck = Sensor(rBody, upDirection, contactFilter, hitList, skinWidth, slopeLimit);

            isTouchingGround = firstGroundCheck.IsTouching;

            if (isTouchingGround)
			{
                Vector2 normalComponent = Vector2.Dot(currentDirection, firstGroundCheck.Normal) * firstGroundCheck.Normal;
                currentDirection -= normalComponent;
			}

            Debug.Log(i + "    " + currentDirection.normalized.ToString("F5"));
            gizmonormal = firstGroundCheck.Normal;
            gizmotangent = currentDirection;
            
            //---------------------------------------------------------------------------------------------------------

            // If no movement is added break out of the loop. This saves computational time, but read the statement
            // under.
            // 
            // NOTICE
            // --------------------------------------------------------------------------------------------------------
            // When dealing with dynamic and other moving kinematic objects we will need to revisit this part of
            // the script. Other moving objects can penetrate the colliders of the character meaning that the ground
            // check picks up the penetrating collider. This can have bad results
            //---------------------------------------------------------------------------------------------------------
            if (currentDirection.sqrMagnitude < Mathf.Epsilon*Mathf.Epsilon)
			{
                break;
			}


            // Movement and collision response. Here we get a new current position and a target position after
            // casting in the direction. The cast method calculates a new target position depending on
            // our choices for slope handling.
            //
            // NOTICE
            //---------------------------------------------------------------------------------------------------------
            // The cast method is currently doing a lot of things. It's casting, calculating angles and applying
            // collision responses. Consider dividing the tasks into more methods if it simplifies
            // the logic.
            //---------------------------------------------------------------------------------------------------------
            MovementResult movement = Cast(rBody, currentDirection, contactFilter, hitList, distance, skinWidth, upDirection, slopeLimit);
            currentPosition = movement.CurrentPosition;
            targetPosition = movement.TargetPosition;
            rBody.position = currentPosition;

            i++;
		}

        // Final position
        rBody.position = currentPosition;
	}


    /// <summary>
    /// Vertical movement. This method is a secondary movement used to correct the ground movement method.
    /// This class handles sliding down slopes
    /// </summary>
    /// <param name="direction"></param>
    /// <param name="upDirection"></param>
    /// <param name="maxIterations"></param>
    protected void HandleDownMovement(Vector2 direction, Vector2 upDirection, int maxIterations)
	{
        float bounciness = 0;
        float friction = 1.0f;

        Vector2 currentPosition = rBody.position;
        Vector2 targetPosition = rBody.position + direction;
        int i = 0;

        /*
        while (i < maxIterations)
		{
            Vector2 currentDirection = targetPosition - currentPosition;
            float distance = currentDirection.magnitude;

            

            i++;
		}
        */

        
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
    /// Cast all colliders on the current rigidbody in the direction and given distance. Filter out allowed contacts with
    /// the contact filter. The results are placed on in the results list. This method returns a MovementResult containing 
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
    protected MovementResult Cast(Rigidbody2D body, Vector2 direction, ContactFilter2D filter, List<RaycastHit2D> results, float distance, float skinDistance, Vector2 upDirection, float maxAngle)
    {
        // These are only current values. Try to implement the friction of the colliding
        // objects friction and bounciness
        float friction = 1;
        float bounciness = 0;

        Vector2 startPosition = body.position;
        Vector2 currentPosition = body.position;
        Vector2 targetPosition = currentPosition + direction.normalized * distance;
        Vector2 normal = Vector2.zero;
        float remainingDistance = 0;

        int hits = body.Cast(direction, filter, results, distance);

        if (hits <= 0)
        {
            currentPosition = targetPosition;
        }

        else
        {
            for (int i = 0; i < hits; i++)
            {
                float adjustedDistance = results[i].distance - skinDistance;
                distance = adjustedDistance < distance ? adjustedDistance : distance;
                currentPosition += direction.normalized * distance;

                if (Vector2.Dot(results[i].normal, upDirection) < 0)
                {
                    upDirection = -upDirection;
                }

                Vector2 reflected = Vector2.Reflect(direction, results[i].normal);
                normal = Vector2.Dot(reflected, results[i].normal) * results[i].normal;
                Vector2 tangent = reflected - normal;

                remainingDistance = (targetPosition - currentPosition).magnitude;
                float angle = Vector2.Angle(upDirection, results[i].normal);

                if (angle <= slopeLimit)
                {
                    targetPosition = currentPosition + normal.normalized * bounciness * remainingDistance + tangent.normalized * friction * remainingDistance;
                }

                else
                {
                    targetPosition = currentPosition;
                }
            }
        }
        return new MovementResult(startPosition, currentPosition, targetPosition, normal, distance, remainingDistance);
    }

    /// <summary>
    /// A sensor
    /// </summary>
    /// <param name="body"></param>
    /// <param name="direction"></param>
    /// <param name="filter"></param>
    /// <param name="results"></param>
    /// <param name="length"></param>
    /// <param name="slopeLimit"></param>
    /// <returns></returns>
    protected SensorResult Sensor(Rigidbody2D body, Vector2 upDirection, ContactFilter2D filter, List<RaycastHit2D> results, float length, float slopeLimit)
    {
        Vector2 normal = Vector2.zero;
        float angle = 0;
        bool isTouching = false;

		int hits = body.Cast(-upDirection, filter, results, length);
        for (int i = 0; i < hits; i++)
        {
            normal = results[i].normal;
            angle = Vector2.Angle(upDirection, normal);

            if (angle <= slopeLimit)
            {
                isTouching = true;
            }
        }

        return new SensorResult(normal, angle, isTouching);
    }

    /// <summary>
    /// Handle the case where the player must move down a slope. It is important that the players direction is aligned
    /// along the slope otherwise this method might fail. The Rigidbody in the method will be moved during the
    /// method call.
    /// </summary>
    /// <param name="startPosition"></param>
    /// <param name="currentPosition"></param>
    /// <param name="body"></param>
    /// <param name="direction"></param>
    /// <param name="filter"></param>
    /// <param name="results"></param>
    /// <param name="castDistance"></param>
    /// <param name="upDirection"></param>
    protected MovementResult SlopeClamp(MovementResult movement, Rigidbody2D body, Vector2 upDirection, ContactFilter2D filter, List<RaycastHit2D> results, float castDistance, float skinDistance)
	{
        // We initialize variables here so that we can change them during the method
        // because we aim at returning a new Movement result at the end of the 
        // method call. 
        //---------------------------------------------------------------------------------------------------------
        // NOTICE
        // This is required to get the correct lengths when doing the slope clamp. It is fully possible
        // that a collision after a slope is no longer valid when the slope clamp takes effect. we therefore need
        // all these values before we do any math
        //---------------------------------------------------------------------------------------------------------
        Vector2 startPosition = movement.StartPosition;
        Vector2 currentPosition = movement.CurrentPosition;
        Vector2 targetPosition = movement.TargetPosition;
        Vector2 normal = Vector2.zero;
        Vector2 slopePosition = currentPosition;
        float distance = movement.Distance;
        float remainingDistance = movement.RemainingDistance;
        float totalDistance = distance + remainingDistance;

        bool hitSlope = false;

        //---------------------------------------------------------------------------------------------------------
        // Main collision logic. We cast in the negative up direction and calculate the normal
        // vector of the slope.
        // TODO
        // -Fix the slope cast function so we cast by the right amount
        //---------------------------------------------------------------------------------------------------------
        int hits = body.Cast(-upDirection, filter, results, castDistance);
        for(int i=0; i<hits; i++)
		{
            hitSlope = true;
            float adjustedDistance = results[i].distance - skinDistance;
            castDistance = adjustedDistance < castDistance ? adjustedDistance : castDistance;
            slopePosition = currentPosition - upDirection.normalized * castDistance;
            normal = results[i].normal;
		}

        //---------------------------------------------------------------------------------------------------------
        // This part is excecuted only if we recieve a hit in the previous collision loop. Here, we compute
        // new values for the positions of the character. We use the law of sines and some geometric
        // considerations to solve.
        //---------------------------------------------------------------------------------------------------------
        if (hitSlope)
		{
            float theta1 = Vector2.Angle(movement.Normal, upDirection);
            float theta2 = Vector2.Angle(movement.Normal, normal);
            float theta3 = 180 - theta2 - theta1;

            remainingDistance = castDistance * Mathf.Sin(theta3) / Mathf.Sin(theta2);

            Vector2 oldDirection = currentPosition - startPosition;
            currentPosition -= oldDirection.normalized * remainingDistance;
            Vector2 newDirection = slopePosition - currentPosition;
            targetPosition = currentPosition + newDirection.normalized * remainingDistance;
            distance = totalDistance - remainingDistance;
        }
        
        return new MovementResult(startPosition, currentPosition, targetPosition, normal, distance, remainingDistance);
	}

    /// <summary>
    /// Special cast distance depending on the slope limit, velocity and other
    /// parameters.
    /// </summary>
    /// <param name="distance"></param>
    /// <returns></returns>
    protected float SlopeCastDistance(float distance)
	{
        return 16*distance;
	}

    /// <summary>
    /// Compute the collision response
    /// </summary>
    /// <returns></returns>
    protected CollisionResult CollisionResponse(Vector2 direction, RaycastHit2D hit, Vector2 upDirection)
	{
        Vector2 reflected = Vector2.Reflect(direction, hit.normal);
        Vector2 projection = Vector2.Dot(reflected, hit.normal) * hit.normal;
        Vector2 tangent = reflected - hit.normal;
        return new CollisionResult(Vector2.zero, Vector2.zero);
	}

    /// <summary>
    /// Grounded
    /// </summary>
    public bool IsGrounded
	{
		get { return isTouchingGround; }
	}

    /// <summary>
    /// Contains information about a collision result from the typical collide and slide algorithm
    /// When an object collides with an another it's velocity is changed to move parallell to
    /// the other colliders surface area. This struct contains
    /// - Normal of the other objects surface
    /// - A new target position from the collision
    /// </summary>
    protected struct CollisionResult
	{
        private readonly Vector2 normal;
        private readonly Vector2 targetPosition;

        public CollisionResult(Vector2 normal, Vector2 targetPosition)
		{
            this.normal = normal;
            this.targetPosition = targetPosition;
		}

        public Vector2 Normal
		{
			get { return normal; }
		}

        public Vector2 TargetPosition
		{
			get { return targetPosition; }
		}
	}

    /// <summary>
    /// Contains information about a movement result. When a character controller moves it gets affected by collisions. For stability it
    /// is useful to split up the movement into sub movements when collisions or slope clamps occur. A movement result returns the movement, 
    /// remaining distance to travel and the direction after a sub movement occurs
    /// 
    /// TODO
    /// NEED REMAINING OR INITIAL DISTANCE
    /// </summary>
    protected struct MovementResult
	{
        private readonly Vector2 startPosition;
        private readonly Vector2 currentPosition;
        private readonly Vector2 targetPosition;
        private readonly Vector2 normal;
        private readonly float distance;
        private readonly float remainingDistance;

        public MovementResult(Vector2 startPosition, Vector2 currentPosition, Vector2 targetPosition, Vector2 normal, float distance, float remainingDistance)
		{
            this.startPosition = startPosition;
            this.currentPosition = currentPosition;
            this.targetPosition = targetPosition;
            this.normal = normal;
            this.distance = distance;
            this.remainingDistance = remainingDistance;
		}

        public Vector2 StartPosition
		{
			get { return startPosition; }
		}

        public Vector2 CurrentPosition
		{
			get { return currentPosition; }
		}

        public Vector2 TargetPosition
		{
			get { return targetPosition; }
		}

        public Vector2 Normal
		{
			get { return normal; }
		}

        public float Distance
		{
			get { return distance; }
		}

        public float RemainingDistance
		{
			get { return remainingDistance; }
		}
	}

    /// <summary>
    /// Contains information about a sensor result. A sensor is used to check for nearby collisions like ground
    /// checks or wall checks. Contains a normal, a tangent and an angle. 
    /// </summary>
    protected struct SensorResult
	{
        private readonly Vector2 normal;
        private readonly float angle;
        private readonly bool isTouching;

        public SensorResult(Vector2 normal, float angle, bool isTouching)
		{
            this.normal = normal;
            this.angle = angle;
            this.isTouching = isTouching;
		}

        public Vector2 Normal
		{
			get { return normal; }
		}

        public float Angle
        {
            get { return angle; }
        }

        public bool IsTouching
        {
            get { return isTouching; }
        }
    }

	private void OnDrawGizmos()
	{
        Debug.DrawRay(transform.position, gizmonormal.normalized*2, Color.red);
        Debug.DrawRay(transform.position, gizmotangent.normalized*2, Color.blue);
        Gizmos.DrawWireCube(transform.position + skinWidth * Vector3.down, Vector2.one);
    }
}
