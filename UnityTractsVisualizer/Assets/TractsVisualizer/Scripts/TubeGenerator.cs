﻿///////////////////////////////////////////////////////////////////////////////
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
	public bool lod;              // Whether to use LOD or not
	public int dequeSize = 10000; // How many generated tubes to be sent to the GPU every frame   
	public float decimation = 0;  // Decimation level, between 0 and 1. If set to 0, each polyline will have only two vertices (the endpoints)
	public float scale = 1;       // To resize the vertex data
    public float radius = 1;      // Thickness of the tube for LOD 0
	public int resolution = 3;    // Number of sides for each tube
	public int voxelCount = 100;  // Number of voxels per axis in space
	public Material material;     // Texture (can be null)
	public Color colorStart = Color.white; // Start color
	public Color colorEnd   = Color.white; // End color
		
	public int numberOfThreads = 1; // Number of threads used to generate tube
	
	private Vector3 [][] allpolylines; // Original polyline data
	 
	protected Vector3 [][] polylines; // To store polylines data
	protected float   [][] radii;     // To store radius data
	protected GameObject [] actors;   // Gameobjects that will have tubes attached
	protected Tube [] tubes;          // Tubes
	protected bool [] attached;       // If actors have already have their tube attached
	
	protected Vector3 [][] polylinesLod; // LOD polylines
	
	protected int ncpus; // How many CPU cores are available
	
	// For safe multithreading
	protected int nextPreprocess;
	protected int nextFindLod;
	protected int nextCreateLod;
	protected int nextLine;
	protected int finishedPreprocess;
	protected int finishedFindLOD;
	protected int finishedCreateLOD;
	protected readonly object _lock  = new object();
	protected readonly object _finishedPreprocess = new object();
	protected readonly object _finishedFindLOD = new object();
	protected readonly object _finishedCreateLOD = new object();
	protected readonly object _dictionary = new object();
	protected readonly object _enque = new object();

	// Dictionary of polylines in the same voxel to merge
	// Given a Start and End voxel position, save list of polyline IDs 
	private Dictionary<Tuple<Vector3Int, Vector3Int>, List<int>> dictionary;
	
	// List that contains the entries of the dictionary (the lists of polylines ids)
	private List<int> [] dictionaryList;
		
	// To dispatch coroutines
	public readonly Queue<Action> ExecuteOnMainThread = new Queue<Action>();
	
    protected IEnumerator Generate(Vector3 [][] allpolylines)
    {
		yield return null;
		
		// Use all original polylines
		this.allpolylines = new Vector3[allpolylines.Length][];
		
		// Deep copy
		for(int i=0; i<allpolylines.Length; i++) {
			this.allpolylines[i] = new Vector3[allpolylines[i].Length];
			
			for(int j=0; j<allpolylines[i].Length; j++) {
				Vector3 v = allpolylines[i][j];
				this.allpolylines[i][j] = new Vector3(v.x, v.y, v.z);
			}
		}
		
		polylines = new Vector3[allpolylines.Length][];
		
		Debug.Log(allpolylines.Length);
		
		// Radius
		radii = new float[allpolylines.Length][];
		
		// Create dictionary
		dictionary = new Dictionary<Tuple<Vector3Int, Vector3Int>, List<int>>();
		
		// Number of CPUS to use for tubing
		ncpus = SystemInfo.processorCount;
		
		// If user wants less threads, set it to that
		ncpus = numberOfThreads < ncpus ? numberOfThreads : ncpus;
		
		// Normalize data
		Normalize();
		
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
		
		// Attach an actor to the game object
		for(int i=0; i<actors.Length; i++) {
			actors[i] = new GameObject();
			actors[i].name = "Tube "+(i+1);
			actors[i].AddComponent<Actor>();
			actors[i].transform.parent = transform;
		}
		
		// To store tubes
		tubes = new Tube[polylines.Length];
		
		Process();
	}
	
	// Divide work into threads and start from the beginning
	public void Process() {
		// Init lock index
		nextPreprocess = 0;
		nextFindLod    = 0;
		nextCreateLod  = 0;
		nextLine       = 0;
		
		finishedPreprocess = 0;
		finishedFindLOD    = 0;
		finishedCreateLOD  = 0;
		
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
			ThreadFindLOD();
			ThreadCreateLOD();
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
				
				// Only decimate if necessary
				if(decimation > 0) {
					// List of decimated point
					List<Vector3> list = new List<Vector3>();
				
					// Add first point
					list.Add(allpolylines[x][0]);
						
					// Add the second point
					if(allpolylines[x].Length == 2) {
						list.Add(allpolylines[x][1]);
					}
					// Add more points
					else if(allpolylines[x].Length > 2) {
						
						// Origin vector
						Vector3 v1 = allpolylines[x][2]-allpolylines[x][1];
						
						// Index of the current and next point
						int ii=1;
						int jj=3;
						int last = 0;
						
						// Check all points
						while(jj<allpolylines[x].Length) {
							// Next vector
							Vector3 v2 = allpolylines[x][jj]-allpolylines[x][ii];
							
							// Angle of vectors
							float angle = Vector3.Angle(v1, v2);
							
							// If angle is less, do not add
							if(angle < decimation) {
							}
							// Angle is greater, add points
							else {
								// Add previous to next point
								list.Add(allpolylines[x][jj-1]);
								last = jj-1;
								
								ii = jj-1;
								
								v1 = allpolylines[x][jj]-allpolylines[x][ii];
							}
							
							// Advance
							jj++;
						}
						
						// Add last point if it was not added
						if(last != allpolylines[x].Length-1) {
							list.Add(allpolylines[x][allpolylines[x].Length-1]);
						}
					}
					
					// If after decimation there are less than 2 points, remake list
					if(list.Count < 2) {
						// List of decimated point
						list = new List<Vector3>();
					
						// Add first and last points
						list.Add(allpolylines[x][0]);
						list.Add(allpolylines[x][allpolylines[x].Length-1]);
					}
					
					// Make list to array
					polylines[x] = list.ToArray();
				}
				else {
					// Deep copy
					polylines[x] = new Vector3[allpolylines[x].Length];
					
					for(int j=0; j<allpolylines[x].Length; j++) {
						Vector3 v = allpolylines[x][j];
						polylines[x][j] = new Vector3(v.x, v.y, v.z);
					}
				}
				
				// Radius
				radii[x] = new float[polylines[x].Length];
				
				for(int j=0; j<radii[x].Length; j++) {
					radii[x][j] = radius;
				}
			}
		}
		
		// When done, clock out
		lock(_finishedPreprocess) {
			finishedPreprocess++;
			
			// Whoever is last, must call the next threads to create tubes
			dictionary.Clear();     // Clear dictionary
			
			if(finishedPreprocess == ncpus) {
				
				for(int i=0; i<ncpus; i++) {
					
					// If LOD
					if(lod) {
						Thread t = new Thread(()=>ThreadFindLOD());
						t.Start();
					}
					// Otherwise, make original polylines
					else  {
						Thread t = new Thread(()=>ThreadCreateTubes());
						t.Start();
					}
				}
			}
		}
	}
	
	// Find similar polylines by voxel matching
	public void ThreadFindLOD() {
		
		// Make polyline snap to closest voxel
		while(nextFindLod < polylines.Length) {

			int x;
			lock(_lock) {
				x = nextFindLod;
				nextFindLod++; // For the next thread
			}
			
			if(x < polylines.Length) {
				// Get closest voxel at start and end
				Vector3Int vs = GetClosestVoxel(polylines[x][0]);
				Vector3Int ve = GetClosestVoxel(polylines[x][polylines[x].Length-1]);
				
				// Create tuple
				Tuple<Vector3Int, Vector3Int> tuple = new Tuple<Vector3Int, Vector3Int>(vs, ve);
				
				lock(_dictionary) {
					// If new tuple, create add new list
					if(!dictionary.ContainsKey(tuple)) {
						dictionary.Add(tuple, new List<int>());
					}
					
					// Add id of polyline to list
					dictionary[tuple].Add(x);
				}
			}
		}
		
		// When done, clock out
		lock(_finishedFindLOD) {
			finishedFindLOD++;
			
			// Whoever is last, must call the next step
			if(finishedFindLOD == ncpus) {
					
				// Make dictionary to list
				dictionaryList = new List<int>[dictionary.Count];
				dictionary.Values.CopyTo(dictionaryList, 0);
				
				// Create array for LOD polylines
				polylinesLod = new Vector3[dictionary.Count][];
					
				for(int i=0; i<ncpus; i++) {
					// Start
					Thread t = new Thread(()=>ThreadCreateLOD());
					t.Start();
				}
			}
		}
	}
	
	// Merge polylines
	public void ThreadCreateLOD() {
		
		// Merge polylines
		while(nextCreateLod < dictionaryList.Length) {

			int x;
			lock(_lock) {
				x = nextCreateLod;
				nextCreateLod++; // For the next thread
			}
			
			if(x < dictionaryList.Length) {
				// Merge the polylines
				MergePolylines(dictionaryList[x], x);
			}
		}
		
		// When done, clock out
		lock(_finishedCreateLOD) {
			finishedCreateLOD++;
			
			// Whoever is last, must call the next step
			if(finishedCreateLOD == ncpus) {
				for(int i=0; i<ncpus; i++) {
					
					// Start
					Thread t = new Thread(()=>ThreadCreateTubes());
					t.Start();
				}
			}
		}
	}
	
	// Merge polylines
	private void MergePolylines(List<int> ids, int x) {
		// Create line with largest number of points
		int maxPoints = polylines[ids[0]].Length;
		
		for(int i=1; i<ids.Count; i++) {
			if(polylines[ids[i]].Length > maxPoints)
				maxPoints = polylines[ids[i]].Length;
		}
		
		Vector3[] polyline = new Vector3[maxPoints];
		
		// Divide the original polylines in an equidistant number of points
		List<Vector3 []> equidistantLines = new List<Vector3 []>();
		
		for(int i=0; i<ids.Count; i++) {
			equidistantLines.Add(GetEquidistantPolyline(ids[i], maxPoints));
		}
		
		// Merge the polylines
		for(int i=0; i<maxPoints; i++) {
			// Sum of every point
			for(int j=0; j<equidistantLines.Count; j++) {
				polyline[i] += equidistantLines[j][i];
			}
			
			// Average
			polyline[i] /= (float)equidistantLines.Count;
			polyline[i] = equidistantLines[0][i];
			
		}
		
		// Make the LOD polyline this one
		polylinesLod[x] = polyline;
		
		// Radius
		radii[x] = new float[maxPoints];
		
		for(int i=0; i<maxPoints; i++) {
			float radiusAverage = 0;
			
			for(int j=0; j<equidistantLines.Count; j++) {
				radiusAverage += Vector3.Distance(polyline[i], equidistantLines[j][i]);
			}
			radiusAverage /= (float)equidistantLines.Count;
			
			radii[x][i] = Mathf.Max(radius, radiusAverage);
		}
		
		// Make it soft
		SmoothPolylineLodRadius(x);
	}
	
	// Smooth LoD polyline with gauss
	float [] kernel = new float[7]{0.00598f, 0.060626f, 0.241843f, 0.383103f, 0.241843f, 0.060626f, 0.00598f};
	
	private void SmoothPolylineLodRadius(int id) {
		Debug.Log("Oh boy!");
		// New smooth radii
		float [] radiiSmooth = new float[radii[id].Length];
		
		// Soft
		for(int i=0; i<radii[id].Length; i++) {
			for(int j=0; j<kernel.Length; j++) {
				
				int p=i+j-kernel.Length/2;
				
				if(p < 0)
					p = 0;
				
				if(p >= radii[id].Length)
					p = radii[id].Length-1;
				
				radiiSmooth[i] += radii[id][p] * kernel[j];
			}
		}
		
		// Make it the new radii
		radii[id] = radiiSmooth;
	}
	
	// Given the id of a polyline, create a new one with a number of n equidistant points
	private Vector3[] GetEquidistantPolyline(int id, int n) {
		// Create empty line
		Vector3 [] polyline = new Vector3[n];
		
		// Step size
		float stepSize = (float)(polylines[id].Length-1)/(float)n;

		// Copy data
		float step = 0;
		for(int i=0; i<n-1; i++) {
			// The point is going to be the interpolation between this point and the next one
			step = stepSize*i;
			
			// Alfa and beta
			// Given point i and i+1, if alfa is 1 the new point is i;
			// if beta is 1, the new point is i+1
			float beta = step-(int)step;
			float alfa = 1.0f-beta;
			
			polyline[i] = polylines[id][(int)step]*alfa + polylines[id][(int)step+1]*beta;
		}
		
		// Put the last point
		polyline[n-1] = polylines[id][polylines[id].Length-1];
		
		// Return new polyline
		return polyline;
	}
	
	// Get the voxel position of point v
	private Vector3Int GetClosestVoxel(Vector3 v) {
		Vector3 vv = v*(float)voxelCount;
		
		return new Vector3Int((int)Mathf.Round(vv.x), (int)Mathf.Round(vv.y), (int)Mathf.Round(vv.z));
	}
	
	// Create tubes
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
    }
	
	// Coroutine to attach tube to actor
	IEnumerator AttachTubeToGameobject(int i) {
		yield return null;
		
		// If LOD is on, disable gameobjects past the size of LOD tubes
		if(lod && i >= polylinesLod.Length) {
			// Deativate
			actors[i].SetActive(false);
		}
		else {
			// Activate
			actors[i].SetActive(true);
			
			// Give tube data to gameobject's actor
			actors[i].GetComponent<Actor>().SetTube(tubes[i]);
			
			if(!attached[i]) {
				actors[i].transform.localPosition += transform.position;
				actors[i].transform.localEulerAngles += transform.eulerAngles;
			}
			
			// Give it color
			float lerp;
			if(lod)
				lerp = (float)i/(float)polylinesLod.Length;
			else
				lerp = (float)i/(float)polylines.Length;
		
			actors[i].GetComponent<Actor>().SetMaterial(material);
			actors[i].GetComponent<Actor>().SetColor(Color.Lerp(colorStart, colorEnd, lerp));
			
			attached[i] = true;
		}
	}
	
	// Create a tube
	void CreateTube(int i) {
		// Create empty tube
		tubes[i] = new Tube();
		
		// Generate data
		if(lod) {
			if(i < polylinesLod.Length)
				tubes[i].Create(polylinesLod[i], decimation, scale, radii[i], resolution);
		}
		else {
			tubes[i].Create(polylines[i], decimation, scale, radii[i], resolution);
		}
	}
}