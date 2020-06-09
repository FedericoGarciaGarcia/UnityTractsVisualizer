///////////////////////////////////////////////////////////////////////////////
// Author: Federico Garcia Garcia
// License: GPL-3.0
// Created on: 04/06/2020 23:00
///////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TubeGeneratorWeb : MonoBehaviour
{
	public bool normalize;        // Normalize data between -1 and 1
	public int dequeSize = 10000; // How many generated tubes to be sent to the GPU every frame   
	public float decimation = 0;  // Decimation level, between 0 and 1. If set to 0, each polyline will have only two vertices (the endpoints)
	public float scale = 1;       // To resize the vertex data
    public float radius = 1;      // Thickness of the tube for LOD 0
	public int resolution = 3;    // Number of sides for each tube
	public Material material;     // Texture (can be null)
	public Color colorStart = Color.white; // Start color
	public Color colorEnd   = Color.white; // End color
		
	private Vector3 [][] polylines; // To store polylines data
	private float   [][] radii;     // To store radius data
	private GameObject [] actors;   // Gameobjects that will have tubes attached
	private Tube [] tubes;          // Tubes
	private bool [] attached;       // If actors have already have their tube attached
	
	// For safe enquing
	protected readonly object _enque = new object();

	// To dispatch coroutines
	public readonly Queue<Action> ExecuteOnMainThread = new Queue<Action>();
	
    protected IEnumerator Generate(Vector3 [][] allpolylines)
    {
		yield return null;
		
		// Use all original polylines
		polylines = allpolylines;
		
		// Radius
		radii = new float[polylines.Length][];
		
		// Set initial radius
		for(int i=0; i<radii.Length; i++) {
			
			radii[i] = new float [polylines[i].Length];
			
			for(int j=0; j<radii[i].Length; j++) {
				radii[i][j] = radius;
			}
		}
		
		// Normalize if necessary
		if(normalize) {
			Normalize();
		}
		
		// Generate tubes
		UpdateTubes();
	}
	
	
	// Normalize
	private void Normalize() {
		// Get min and max of each axis
		Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
		Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
		
		for(int i=0; i<polylines.Length; i++) {
			for(int j=0; j<polylines[i].Length; j++) {
				if(polylines[i][j].x < min.x) min.x = polylines[i][j].x;
				if(polylines[i][j].y < min.y) min.y = polylines[i][j].y;
				if(polylines[i][j].z < min.z) min.z = polylines[i][j].z;
				
				if(polylines[i][j].x > max.x) max.x = polylines[i][j].x;
				if(polylines[i][j].y > max.y) max.y = polylines[i][j].y;
				if(polylines[i][j].z > max.z) max.z = polylines[i][j].z;
			}
		}
		
		for(int i=0; i<polylines.Length; i++) {
			for(int j=0; j<polylines[i].Length; j++) {
				polylines[i][j] = Normalize(polylines[i][j], min, max);
			}
		}
	}
	
	private Vector3 Normalize(Vector3 v, Vector3 min, Vector3 max) {
		return new Vector3(Normalize(v.x, min.x, max.x), Normalize(v.y, min.y, max.y), Normalize(v.z, min.z, max.z));
	}
	
	private float Normalize(float x, float min, float max) {
		return (x-min)/(max-min);
	}
	
	// Only update tubes
	public void UpdateTubes() {
		// Create array of bools
		attached = new bool[polylines.Length];
		
		// Create game objects
		actors = new GameObject[polylines.Length];
		
		// Attach an actor to the game object
		for(int i=0; i<actors.Length; i++) {
			actors[i] = new GameObject();
			actors[i].name = "Tube "+(i+1);
			actors[i].AddComponent<Actor>();
			actors[i].transform.parent = transform;
		}
		
		// To store tubes
		tubes = new Tube[polylines.Length];
		
		// Decimate points
		int npoints = 0;
		int ndecimatedpoints = 0;
		
		for(int i=0; i<polylines.Length; i++) {
			// Total points
			npoints += polylines[i].Length;
			
			// Decimated point
			int npointsLine = (int)((1.0f - decimation) * (float)polylines[i].Length);
			
			if(npointsLine < 2)
				npointsLine = 2;
			ndecimatedpoints += npointsLine;
		}
		
		// Create tubes
		CreateTubes();
	}
	
	// Waiting to dispatch coroutines if there are any.
	// In this case, coroutines are meant to attach tube data to gameobjects
	// (send graphical data to GPU)
	public virtual void Update()
	{
		// dispatch stuff on main thread
		int deque = 0;
		while (deque < dequeSize && ExecuteOnMainThread.Count > 0)
		{
			deque++;
			lock(_enque) {
				ExecuteOnMainThread.Dequeue().Invoke();
			}
		}
	}
	
	// Create tube in a thread
	public void CreateTubes() {
		
		int nextLine = 0;
        while(nextLine < polylines.Length) {
			CreateTube(nextLine);
				
			// Make sure to lock to avoid multithreading problems
			lock(_enque) {
				ExecuteOnMainThread.Enqueue(() => {  StartCoroutine(AttachTubeToGameobject(nextLine)); } );
			}
			
			nextLine++;
		}
    }
	
	// Coroutine to attach tube to actor
	IEnumerator AttachTubeToGameobject(int i) {
		yield return null;
		
		// Give tube data to gameobject's actor
		actors[i].GetComponent<Actor>().SetTube(tubes[i]);
		
		// Give it color
		float lerp = (float)i/(float)polylines.Length;
		actors[i].GetComponent<Actor>().SetMaterial(material);
		actors[i].GetComponent<Actor>().SetColor(Color.Lerp(colorStart, colorEnd, lerp));
		
		if(!attached[i]) {
			actors[i].transform.localPosition += transform.position;
			actors[i].transform.localEulerAngles += transform.eulerAngles;
		}
		
		attached[i] = true;
	}
	
	// Create a tube
	void CreateTube(int i) {
		// Create empty tube
		tubes[i] = new Tube();
		
		// Generate data
		tubes[i].Create(polylines[i], decimation, scale, radii[i], resolution);
	}
	
	// Create a tube by merging
	void MergeTube(int i) {
		// Create empty tube
		tubes[i] = new Tube();
		
		// Generate data
		tubes[i].Create(polylines[i], decimation, scale, radii[i], resolution);
	}
}