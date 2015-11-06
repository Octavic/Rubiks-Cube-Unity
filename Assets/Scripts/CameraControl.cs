using UnityEngine;
using System.Collections;
using System;

public class CameraControl : MonoBehaviour {
	public float mouseSensitivity;
	public float degreeLimit;
	public GameObject target;

	private Vector3 focus;
	private bool IsRotating;

	// Use this for initialization
	void Start () 
	{
		this.focus = target.transform.position;
	}
	
	// Update is called once per frame
	void LateUpdate () 
	{
		var mouseX = Input.GetAxis("Mouse X");
		var mouseY = Input.GetAxis("Mouse Y");
		// If the right click mouse button is down, rotate
		if (Input.GetMouseButton(1))
		{
			IsRotating = true;
		}
		else
		{
			IsRotating = false;
		}

		if (IsRotating)
		{
			// Rotate the camera around the object based on the X and Y offset in mouse input
			var offset = this.transform.position - focus;

			// Rotate in the X axis
			transform.RotateAround(focus, target.transform.up, mouseSensitivity * mouseX);
			if (Math.Abs(offset.x) < degreeLimit && Math.Abs(offset.z) < degreeLimit && offset.y > 0 && mouseY < 0)
			{
				return;
			}
			if (Math.Abs(offset.x) < degreeLimit && Math.Abs(offset.z) < degreeLimit && offset.y < 0 && mouseY > 0)
			{
				return;
			}
			// Rotate in Y axis
			transform.RotateAround(focus, Vector3.Cross(offset, target.transform.up), -mouseSensitivity * mouseY);

			// Realign the camera so it's not tilted
			transform.Rotate(new Vector3(0, 0, -transform.eulerAngles.z));
		}
	}
}
