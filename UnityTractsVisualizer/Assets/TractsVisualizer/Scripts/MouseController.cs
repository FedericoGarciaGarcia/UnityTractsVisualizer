///////////////////////////////////////////////////////////////////////////////
// Author: Federico Garcia Garcia
// License: GPL-3.0
// Created on: 10/06/2020 23:00
///////////////////////////////////////////////////////////////////////////////

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class MouseController : MonoBehaviour
{
	public Transform pivot; // Transform to rotate with mouse
	public Camera camera;   // Transform to rotate with mouse
	public Camera cameraPostProcessing;   // Transform to rotate with mouse
	public float fovMin;    // Min field of view
	public float fovMax;    // Max field of view
	public float rotationSpeed = 1.0f;  // Rotation speed
	public float zoomSpeed = 1.0f;      // Zomming speed

	private bool hold;
	
	// Android
	private Vector2 touchOrigin1;
	private Vector2 touchOrigin2;
	private float distOrigin;

    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
		
		#if UNITY_ANDROID
			// One finger. Move
			if(Input.touches.Length == 1)
			{
				Touch touch = Input.touches[0];
				int pointerID = touch.fingerId;
				if (!EventSystem.current.IsPointerOverGameObject(pointerID)) {
					if (touch.phase == TouchPhase.Began)
					{
						touchOrigin1 = touch.position;
						hold = true;
					}
					
					if (touch.phase == TouchPhase.Ended) {
						hold = false;
					}
				}
				
				if(hold) {
					Vector2 v = touch.position-touchOrigin1;
					pivot.Rotate(new Vector3(-v.y, v.x, 0) * rotationSpeed);
					
					// Remember previous movement
					touchOrigin1 = touch.position;
				}

			}
			
			// Two fingers. Zoom
			if(Input.touches.Length == 2)
			{
				// Get touch presses
				Touch touch1 = Input.touches[0];
				Touch touch2 = Input.touches[1];
				
				// Touch ids
				int pointerID1 = touch1.fingerId;
				int pointerID2 = touch2.fingerId;
				
				// As long as not touching the canvas
				if (!EventSystem.current.IsPointerOverGameObject(pointerID1) && !EventSystem.current.IsPointerOverGameObject(pointerID2)) {
					// When the second finger begins, get the origin of presses and distance
					// between touches
					if (touch2.phase == TouchPhase.Began)
					{
						touchOrigin1 = touch1.position;
						touchOrigin2 = touch2.position;
						distOrigin   = Vector2.Distance(touchOrigin1, touchOrigin2);
					}
				}
				
				// Get current distance
				float dist = Vector2.Distance(touch1.position, touch2.position);
				
				// FOV is proportional
				float proportion = (dist/distOrigin)-1.0f;
				
				// Update cameras FOVs
				camera.fieldOfView -= proportion * zoomSpeed;
				cameraPostProcessing.fieldOfView = camera.fieldOfView;
				
				// For next touch
				touchOrigin1 = touch1.position;
				touchOrigin2 = touch2.position;
				distOrigin   = Vector2.Distance(touchOrigin1, touchOrigin2);
			}
		#else
			// If mouse is pressed, rotate
			if (Input.GetMouseButtonDown(0)) {
				// As long as the canvas is not being clicked over
				if (!EventSystem.current.IsPointerOverGameObject()) {
					hold = true;
				}
			}
			
			if (!Input.GetMouseButton(0)) {
				hold = false;
			}
			
			if(hold) {
				// Rotate
				pivot.Rotate(new Vector3(-Input.GetAxis("Mouse Y"), Input.GetAxis("Mouse X"), 0) * rotationSpeed);
			}
		#endif
		
		// Camera zoom
		camera.fieldOfView -= Input.mouseScrollDelta.y * zoomSpeed;
		
		if(camera.fieldOfView < fovMin)
			camera.fieldOfView = fovMin;
		
		if(camera.fieldOfView > fovMax)
			camera.fieldOfView = fovMax;
		
		cameraPostProcessing.fieldOfView = camera.fieldOfView;
    }
}
