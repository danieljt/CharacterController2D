using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{

	// Data
	public int number = 3;
	public float otherNumber = 4.4f;

	private Rigidbody2D body;


	private void Awake()
	{
		body = GetComponent<Rigidbody2D>();
		body.MovePosition(Vector2.zero);
		body.MovePosition(Vector2.one);
	}
}
