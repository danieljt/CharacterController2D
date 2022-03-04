using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]

/// <summary>
/// A simple character controller that works in a rectangular worlds. Uses the collide and slide algorithm
/// and Casts. Does not handle slopes, step offsets or moving objects
/// </summary>
public class SimpleCharacterController2D : MonoBehaviour, ICharacterController2D
{
	[SerializeField] protected float contactOffset;

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

	public void Move(Vector2 deltaPosition)
	{
		Vector2 sideVector = new Vector2(deltaPosition.x, 0);
		Vector2 vertVector = new Vector2(0, deltaPosition.y);
		CastAndMove(characterBody, sideVector, contactFilter, hitList, contactOffset);
		CastAndMove(characterBody, vertVector, contactFilter, hitList, contactOffset);
	}

	protected void CastAndMove(Rigidbody2D body, Vector2 direction, ContactFilter2D filter, List<RaycastHit2D> results, float skinWidth)
	{
		float distance = direction.magnitude;

		int hits = body.Cast(direction, filter, results, distance + skinWidth);

		for (int i = 0; i < hits; i++)
		{
			float adjustedDistance = results[i].distance - skinWidth;
			distance = adjustedDistance < distance ? adjustedDistance : distance;
		}

		body.position += direction.normalized * distance;
	}

	public bool IsGrounded
	{
		get { return false; }
	}

	public CollisionFlags2D GetCollisionFlags => throw new System.NotImplementedException();
}
