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
	
	private Vector3 [][] allpolylines; // Original polyline data
	
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
	protected int nextPreprocess;
	protected int nextLine;
	protected int finishedPreprocess;
	protected readonly object _dictionary = new object();
	protected readonly object _lock  = new object();
	protected readonly object _finishedPreprocess = new object();
	protected readonly object _enque = new object();

	// To dispatch coroutines
	public readonly Queue<Action> ExecuteOnMainThread = new Queue<Action>();
	
    protected IEnumerator Generate(Vector3 [][] allpolylines)
    {
		yield return null;
		
		// Dictionary for LOD
		dictionaryLOD = new Dictionary<Tuple<Vector3Int, Vector3Int>, List<int>>();
		
		// Use all original polylines
		this.allpolylines = allpolylines;
		polylines = new Vector3[allpolylines.Length][];
		
		Debug.Log(allpolylines.Length);
		
		// Radius
		radii = new float[allpolylines.Length][];
		
		// Number of CPUS to use for tubing
		ncpus = SystemInfo.processorCount;
		
		// If user wants less threads, set it to that
		ncpus = numberOfThreads < ncpus ? numberOfThreads : ncpus;
		
		// Normalize if necessary (original polylines)
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
		
		for(int i=0; i<allpolylines.Length; i++) {
			for(int j=0; j<allpolylines[i].Length; j++) {
				if(allpolylines[i][j].x < min.x) min.x = allpolylines[i][j].x;
				if(allpolylines[i][j].y < min.y) min.y = allpolylines[i][j].y;
				if(allpolylines[i][j].z < min.z) min.z = allpolylines[i][j].z;
				
				if(allpolylines[i][j].x > max.x) max.x = allpolylines[i][j].x;
				if(allpolylines[i][j].y > max.y) max.y = allpolylines[i][j].y;
				if(allpolylines[i][j].z > max.z) max.z = allpolylines[i][j].z;
			}
		}
		
		for(int i=0; i<allpolylines.Length; i++) {
			for(int j=0; j<allpolylines[i].Length; j++) {
				allpolylines[i][j] = Normalize(allpolylines[i][j], min, max);
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
		// Clear dictionary
		dictionaryLOD.Clear();
		
		// Init lock index
		nextPreprocess = 0;
		nextLine = 0;
		
		finishedPreprocess = 0;
		
		// Create tubes using specified number of threads
		if(ncpus > 0) {
			
			// Start threads
			for(int i=0; i<ncpus; i++) {
				Thread t = new Thread(()=>ThreadPreprocess());
				t.Start();
			}
		}
		// Do not use threads (for web)
		else {
			ThreadPreprocess();
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

	// Preprocess poylines in a thread: decimate, put radius, and add to dictionary for merging
	public void ThreadPreprocess() {
		
		// Decimate polylines
		while(nextPreprocess < allpolylines.Length) {

			int x;
			lock(_lock) {
				x = nextPreprocess;
				nextPreprocess++; // For the next thread
			}
			
			if(x < allpolylines.Length) {
				
				// Estimate number of polylines and vertices
				int npoints = (int)((1.0f-decimation) * (float)allpolylines[x].Length);
				
				// If there are not even 2 points, the decimation was too much
				if(npoints < 2) {
					decimation = 1.0f;
					npoints = allpolylines[x].Length;
				}
				
				// New polyline
				polylines[x] = new Vector3[npoints];
				
				// Convert to one single polyline decimating
				float skip = (float)allpolylines[x].Length/(float)npoints;
				
				int [] skipIndices = new int[npoints];
				
				float currentSkip = 0;
				for(int i=0; i<npoints; i++) {
					skipIndices[i] = (int)currentSkip;
					currentSkip += skip;
				}
				
				skipIndices[skipIndices.Length-1] = allpolylines[x].Length-1; // Make sure last vertex is the real last vertex
				
				// Set with skip indices
				for(int i=0; i<npoints; i++) {
					polylines[x][i] = allpolylines[x][skipIndices[i]];
				}
				 
				// Radius
				radii[x] = new float[polylines[x].Length];
				
				for(int j=0; j<radii[x].Length; j++) {
					radii[x][j] = radius;
				}
				
				// Get voxel positions
				Vector3Int v1 = new Vector3Int((int)(polylines[x][0].x*lodVoxels),
											   (int)(polylines[x][0].y*lodVoxels),
											   (int)(polylines[x][0].z*lodVoxels));
											   
				Vector3Int v2 = new Vector3Int((int)(polylines[x][polylines[x].Length-1].x*lodVoxels),
											   (int)(polylines[x][polylines[x].Length-1].y*lodVoxels),
											   (int)(polylines[x][polylines[x].Length-1].z*lodVoxels));
				
				// Add to the dictionary
				lock(_dictionary) {
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
		
		Debug.Log("Decimated!");
		
		// When done, clock out
		lock(_finishedPreprocess) {
			finishedPreprocess++;
			
			// Whoever is last, must call the next threads to create tubes
			if(finishedPreprocess == ncpus) {
				for(int i=0; i<ncpus; i++) {
					Thread t = new Thread(()=>ThreadCreateTubes());
					t.Start();
				}
		
				Debug.Log("It is me!");
			}
		}
	}
		
	public void ThreadCreateTubes() {
		
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
		
		Debug.Log("Done!");
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