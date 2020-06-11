///////////////////////////////////////////////////////////////////////////////
// Author: Federico Garcia Garcia
// License: GPL-3.0
// Created on: 04/06/2020 23:00
///////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class TubeGenerator : MonoBehaviour
{
	public bool normalize;        // Normalize data between 0 and 1
	public int dequeSize = 10000; // How many generated tubes to be sent to the GPU every frame   
	public float decimation = 0;  // Decimation level, between 0 and 1. If set to 0, each polyline will have only two vertices (the endpoints)
	public float scale = 1;       // To resize the vertex data
    public float radius = 1;      // Thickness of the tube for LOD 0
	public int resolution = 3;    // Number of sides for each tube
	public Material material;     // Texture (can be null)
	public Color colorStart = Color.white; // Start color
	public Color colorEnd   = Color.white; // End color
	public int lodVoxels = 100;  // Group start and end points in this number of voxels      
		
	public int numberOfThreads = 1; // Number of threads used to generate tube.
	
	protected Vector3 [][] polylines; // To store polylines data
	protected float   [][] radii;     // To store radius data
	protected GameObject [] actors;   // Gameobjects that will have tubes attached
	protected Tube [] tubes;          // Tubes
	protected bool [] attached;       // If actors have already have their tube attached
	
	protected Dictionary<Tuple<Vector3Int, Vector3Int>, List<int>> dictionaryLOD; // Key = voxel start, voxel end | Value = line IDs in those voxels
	protected Vector3 [][] polylinesLOD; // To store LOD polylines data
	protected float   [][] radiiLOD;     // To store LOD radius data
	protected GameObject [] actorsLOD;   // Gameobjects that will have LODtubes attached
	protected Tube [] tubesLOD;          // LOD Tubes
	
	protected int ncpus; // How many CPU cores are available
	
	// For safe multithreading
	protected int nextLine;
	protected int nextMerge;
	protected readonly object _lock  = new object();
	protected readonly object _enque = new object();

	// To dispatch coroutines
	public readonly Queue<Action> ExecuteOnMainThread = new Queue<Action>();
	
    protected IEnumerator Generate(Vector3 [][] allpolylines)
    {
		yield return null;
		
		// Dictionary for LOD
		dictionaryLOD = new Dictionary<Tuple<Vector3Int, Vector3Int>, List<int>>();
		
		// Use all original polylines
		polylines = allpolylines;
		
		Debug.Log(polylines.Length);
		
		// Number of CPUS to use for tubing
		ncpus = SystemInfo.processorCount;
		
		// If user wants less threads, set it to that
		ncpus = numberOfThreads < ncpus ? numberOfThreads : ncpus;
		
		// Radius
		radii = new float[polylines.Length][];
		
		// Set initial radius
		UpdateRadius();
		
		// Normalize if necessary
		if(normalize) {
			Normalize();
		}
		
		// Generate tubes
		UpdateTubes();
	}
	
	protected void UpdateRadius() {
		for(int i=0; i<radii.Length; i++) {
			
			radii[i] = new float [polylines[i].Length];
			
			for(int j=0; j<radii[i].Length; j++) {
				radii[i][j] = radius;
			}
		}
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
		actorsLOD = new GameObject[polylines.Length]; // LOD will have less than actors
		
		// Attach an actor to the game object
		for(int i=0; i<actors.Length; i++) {
			actors[i] = new GameObject();
			actors[i].name = "Tube "+(i+1);
			actors[i].AddComponent<Actor>();
			actors[i].transform.parent = transform;
		}
		
		// To store tubes
		tubes = new Tube[polylines.Length];
		tubesLOD = new Tube[polylines.Length];
		
		Process();
	}
	
	// Divide work into threads and start from the beginning
	public void Process() {
		// Init lock index
		nextLine = 0;
		nextMerge = 0;
		
		// Create tubes using specified number of threads
		if(ncpus > 0) {
			Thread [] threads = new Thread[ncpus];
			
			// Lines per thread
			int lpt = polylines.Length/ncpus;
			
			for(int i=0; i<ncpus; i++) {
				threads[i] = new Thread(()=>ThreadCreateTubes());
			}
			
			// Start threads
			for(int i=0; i<ncpus; i++) {
				threads[i].Start();
			}
		}
		// Do not use threads (for web)
		else {
			ThreadCreateTubes();
		}
	}
	
	// Waiting to dispatch coroutines if there are any.
	// In this case, coroutines are meant to attach tube data to gameobjects
	// (send graphical data to GPU)
	public virtual void Update()
	{
		// dispatch stuff on main thread
		Dispatch();
	}
	
	// Dispatch stuff on main thread
	protected void Dispatch() {
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
	public void ThreadCreateTubes() {
		
		// Merge polylines
		while(nextMerge < polylines.Length) {

			int x;
			lock(_lock) {
				x = nextMerge;
				nextMerge++; // For the next thread
				
				if(x < polylines.Length) {
					// Get voxel positions
					Vector3Int v1 = new Vector3Int((int)(polylines[x][0].x*lodVoxels),
												   (int)(polylines[x][0].y*lodVoxels),
												   (int)(polylines[x][0].z*lodVoxels));
												   
					Vector3Int v2 = new Vector3Int((int)(polylines[x][polylines[x].Length-1].x*lodVoxels),
												   (int)(polylines[x][polylines[x].Length-1].y*lodVoxels),
												   (int)(polylines[x][polylines[x].Length-1].z*lodVoxels));
					
					// Add to the dictionary
					Tuple<Vector3Int, Vector3Int> t = new Tuple<Vector3Int, Vector3Int>(v1, v2);
					
					// If it is a new value, create list first
					if (!dictionaryLOD.ContainsKey(t)) {
						dictionaryLOD.Add(t, new List<int>());
					}
					
					// Add to the list this polyline ID
					dictionaryLOD[new Tuple<Vector3Int, Vector3Int>(v1, v2)].Add(x);
				}
			}
		}
		
		// 
		
		// Create tubes
        while(nextLine < polylines.Length) {
		    // If we directly use i instead of x "AttachTubeToGameobject(i)" we have a concurrency problem;
		    // The thread modifies i so when the coroutine starts it may have strange i values
		    // By copying it to a local int that will be different each iteration, we avoid this problem
		   
			int x;
			lock(_lock) {
				x = nextLine;
				nextLine++; // For the next thread
			}
			
			if(x < polylines.Length) {
				// Run coroutine on the main thread that adds the tube data to the gameobject
				CreateTube(x);
				
				// Make sure to lock to avoid multithreading problems
				lock(_enque) {
					ExecuteOnMainThread.Enqueue(() => {  StartCoroutine(AttachTubeToGameobject(x)); } );
				}
			}
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