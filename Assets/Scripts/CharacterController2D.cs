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
    [Tooltip("The minimum ground normal. To prevent the controller registering walls as ground, we set this to a small value")]
    [SerializeField] protected float minimumGroundNormal = 0.5f;

    [Tooltip("The smallest movement distance required before collision checking is enabled. Prevents needless calculations when the character is still")]
    [SerializeField] protected float minimumMoveDistance = 0.01f;

    [Tooltip("A small value that is added to the collision checking to prevent colliders getting stuck")]
    [SerializeField] protected float skinWidth = 0.01f;

    [Tooltip("The largest slope the charactercontroller can move up or down")]
    [SerializeField] protected float slopeLimit = 45f;

    // Collision 
    protected Rigidbody2D rBody;
    protected ContactFilter2D contactFilter;
    protected RaycastHit2D[] hitArray = new RaycastHit2D[16];
    protected List<RaycastHit2D> hitList = new List<RaycastHit2D>(16);
    protected List<RaycastHit2D> hitListPrevious = new List<RaycastHit2D>(16);

    // Ground values
    protected Vector2 groundNormal;
    protected Vector2 slopeMoveVector;
    protected bool isGrounded;
    protected bool isColliding;
  
    private void Awake()
    {
        rBody = GetComponent<Rigidbody2D>();
        contactFilter.useTriggers = false;
        contactFilter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
        contactFilter.useLayerMask = true;
    }

    ///<summary>
    /// Moves the attached gameobject to the given position. This function uses position deltas,
    /// where the positions respond to collisions. This method does not use gravity or any other forces.
    /// Forces should be applied before calling this function. This metod responds to slopes.
    /// Only call this method once per fixed update frame.
    /// 
    /// 
    ///</summary>
    public void Move(Vector2 deltaPosition)
    {
        // We must cast the rigidbody in the distance of the deltaposition to get the
        // overlaps with the rest of the world
        float distance = deltaPosition.magnitude;
        int count = rBody.Cast(deltaPosition, contactFilter, hitArray, distance);

        hitList.Clear();
        for(int i=0; i<count; i++)
        {
            hitList.Add(hitArray[i]);
        }

        // This is where we compute the collision responses. This is the hard part. We must be able to handle 
        // the following situations
        // - No collisions
        // - sloped movement
        // - step movement


    }

    
    public void Predict(Vector2 deltaposition)
    {

    }

    public void Correct(Vector2 deltaPosition)
    {

    }
}
