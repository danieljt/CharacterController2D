using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This script tests:
/// Rigidbody2D.OverlapCollider(ContactFilter2D contactFilter, Collider2D[] results)
/// Collider2D.OverlapCollider(ContactFilter2D contactFilter, Collider2D[] results)
/// </summary>
public class Overlapper : MonoBehaviour
{
	protected Rigidbody2D rBody;
	protected ContactFilter2D contactFilter;
	protected List<Collider2D> colliders;
	protected List<Collider2D> overlappedColliders;

	private void Awake()
	{
		rBody = GetComponent<Rigidbody2D>();
		contactFilter.useTriggers = false;
		contactFilter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
		contactFilter.useLayerMask = true;

		colliders = new List<Collider2D>(16);
		overlappedColliders = new List<Collider2D>(16);

		rBody.GetAttachedColliders(colliders);
	}

	private void Start()
	{
		Debug.Log(rBody.position.ToString("F5"));
		int hits = rBody.OverlapCollider(contactFilter, overlappedColliders);

		for(int i=0; i<hits; i++)
		{
			for(int j=0; j<colliders.Count; j++)
			{
				ColliderDistance2D colliderDistance = overlappedColliders[i].Distance(colliders[j]);
				if(colliderDistance.isValid)
				{
					Debug.Log(overlappedColliders[i] + "    " + colliderDistance.normal);
					rBody.position -= colliderDistance.distance * colliderDistance.normal;
				}
			}
		}
		Debug.Log(rBody.position.ToString("F5"));
	}
}
