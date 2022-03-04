using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// This script tests:
/// Rigidbody2D.OverlapCollider(ContactFilter2D contactFilter, Collider2D[] results)
/// Rigidbody2D.Distance()
/// Collider2D.OverlapCollider(ContactFilter2D contactFilter, Collider2D[] results)
/// </summary>
public class OverlapRecovery : MonoBehaviour
{
	protected Rigidbody2D rBody;
	protected ContactFilter2D contactFilter;
	protected List<Collider2D> overlappedColliders;

	private void Awake()
	{
		rBody = GetComponent<Rigidbody2D>();
		contactFilter.useTriggers = false;
		contactFilter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
		contactFilter.useLayerMask = true;

		overlappedColliders = new List<Collider2D>(16);
	}

	private void Start()
	{
		ResolveOverlaps();
	}

	private void ResolveOverlaps()
	{
		// Get all overlapped colliders
		int hits = rBody.OverlapCollider(contactFilter, overlappedColliders);
		
		for(int i=0; i < hits; i++)
		{
			ColliderDistance2D overlap = rBody.Distance(overlappedColliders[i]);
			Debug.Log(overlap.normal);
			//Debug.Log(overlap.pointA);
			//Debug.Log(overlap.pointB);
		}
	}
}
