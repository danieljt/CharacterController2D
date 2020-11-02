using System.Collections;
using System.Collections.Generic;
using UnityEngine;

///<summary>
/// Simple Physics class for a custom physics object. Use this for a playercontroller or any other
/// object that is not stricktly a rigidbody although the class uses a kinematic rigidbody as a base.
/// Great for avoiding the blocky rigidbody implementation although requiring more work and not supporting 
/// rotations or other typical dynamic rb2d features.
///
/// !!! This class was taken from the unity online course platformer controller !!!
///<summary>
public class Physics2DObject : MonoBehaviour
{
    public float gravityScale = 1f;
    public float minGroundNormalY = .65f;

    // Important internal collision constants
    //
    protected const float minMoveDistance = 0.001f;
    protected const float shellRadius = 0.01f;

    // Collision detection variables and classes
    protected Rigidbody2D rbody;
    protected ContactFilter2D contactFilter;
    protected RaycastHit2D[] hitBuffer = new RaycastHit2D[16];
    protected List<RaycastHit2D> hitBufferList = new List<RaycastHit2D>(16);

    protected Vector2 velocity;
    protected Vector2 groundNormal;
    protected Vector2 targetVelocity;
    protected bool isGrounded;


    private void OnEnable()
    {
        rbody = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        contactFilter.useTriggers = false;
        contactFilter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
        contactFilter.useLayerMask = true;
    }

    void Update()
    {
        targetVelocity = Vector2.zero;
        ComputeVelocity();
    }

    protected virtual void ComputeVelocity()
    {

    }

    private void FixedUpdate()
    {
        // First we apply gravity and calculate a target velocity
        velocity += gravityScale*Physics2D.gravity*Time.fixedDeltaTime;
        velocity.x = targetVelocity.x;

        // We set isgrounded to false each frame before calculating it later
        isGrounded = false;

        // We then calculate the incremental velocity and get the ground normals
        Vector2 deltaPosition = velocity*Time.fixedDeltaTime;
        Vector2 moveAlongGround = new Vector2(groundNormal.y, -groundNormal.x);

        // We run our move scrip t in in the x direction first, and then the y direction 
        Vector2 move = moveAlongGround*deltaPosition.x;
        Movement(move, false);
        move = Vector2.up*deltaPosition.y;
        Movement(move, true);
    }

    private void Movement(Vector2 move, bool yMovement)
    {
        // First, get the magnitude of the move vector
        float distance = move.magnitude;

        // To avoid unecessary recast for very small movements, we check to see if the
        // movement is larger before we apply any physics
        if(distance > minMoveDistance)
        {

            // Cast the rigidbody colliders into the scene. This is the heart of the collision detection.
            // We add a shellradius so colliders don't get stuck to each other. Then we clear the 
            // hitbufferlist before adding all our contactpoints to it.
            int count = rbody.Cast(move, contactFilter, hitBuffer, distance + shellRadius);
            hitBufferList.Clear();


            for(int i=0; i<count; i++)
            {
                hitBufferList.Add(hitBuffer[i]);
            }

            for(int i=0; i<hitBufferList.Count; i++)
            {

                // We calculate the normal of each contactpoint in the list. We have added a minimum ground
                // normal value to account for round off errors. If the current y normal is larger than this then the
                // object is grounded.
                Vector2 currentNormal = hitBufferList[i].normal;
                if(currentNormal.y > minGroundNormalY)
                {
                    isGrounded = true;
                    if(yMovement)
                    {
                        groundNormal = currentNormal;
                        currentNormal.x = 0;
                    }
                }

                // We project the velocity to the ground to see if we are on a slope or not.
                // important for nice slope movement.
                float projection = Vector2.Dot(velocity, currentNormal);
                if(projection < 0)
                {
                    velocity = velocity - projection*currentNormal;
                }

                // Modify our distance according to the collision. 
                float modifiedDistance = hitBufferList[i].distance - shellRadius;
                distance = modifiedDistance < distance ? modifiedDistance : distance;
            }

        }

        // Apply our movement after all checks are done.
        rbody.position = rbody.position + move.normalized*distance;
    }
}
