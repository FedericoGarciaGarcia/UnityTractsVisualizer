///////////////////////////////////////////////////////////////////////////////
// Author: Federico Garcia Garcia
// License: GPL-3.0
// Created on: 10/06/2020 23:00
///////////////////////////////////////////////////////////////////////////////

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MouseController : MonoBehaviour
{
	public Transform pivot; // Transform to rotate with mouse
	public Camera camera;   // Transform to rotate with mouse
	public float fovMin;    // Min field of view
	public float fovMax;    // Max field of view
	public float rotationSpeed = 1.0f;  // Rotation speed
	public float zoomSpeed = 1.0f;      // Zomming speed

    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
		// If mouse is pressed, rotate
        if (Input.GetMouseButton(0)) {
			// Rotate
			pivot.Rotate(new Vector3(-Input.GetAxis("Mouse Y"), Input.GetAxis("Mouse X"), 0) * rotationSpeed);
		}
		
		// Camera zoom
		camera.fieldOfView -= Input.mouseScrollDelta.y * zoomSpeed;
		
		if(camera.fieldOfView < fovMin)
			camera.fieldOfView = fovMin;
		
		
		if(camera.fieldOfView > fovMax)
			camera.fieldOfView = fovMax;
    }
}
