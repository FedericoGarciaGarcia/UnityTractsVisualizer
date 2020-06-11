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
using System.IO;
using UnityEngine.Networking;
using System.Text;

public class TubeGeneratorFromObj : TubeGenerator
{
	public string path; // Path or URL to file
	public bool threadedDownload;
	
	void Start()
	{
		// We override path with SceneController's path
		if(Global.objPath != null)
			path = Global.objPath;
		
		// If its URL, download data in a coroutine, then in
		if(path.StartsWith("https:") || path.StartsWith("http:")) {
			StartCoroutine(LoadFromURL(path));
		}
		// If its file, load data in a thread
		else {
			Thread thread = new Thread(()=>LoadFromFile());
			thread.Start();
		}
	}
	
	void LoadFromFile() {
		// Create OBJ reader
		try {
			ObjReader objReader = new ObjReader();
			
			Vector3 [][] polylines = objReader.GetPolylinesFromFilePath(path);
		
			// Make sure to lock to avoid multithreading problems
			lock(_enque) {
				// Run the generation of polylines in the Main Thread
				ExecuteOnMainThread.Enqueue(() => {  StartCoroutine(AfterLoading()); } );
				ExecuteOnMainThread.Enqueue(() => {  StartCoroutine(Generate(polylines)); } );
			};
		}
		catch(Exception e) {
			Debug.Log(e);
			OnError();
		}
	}
	
	IEnumerator LoadFromURL(string url) {
		UnityWebRequest www = UnityWebRequest.Get(url);
		yield return www.SendWebRequest();

		if(www.isNetworkError || www.isHttpError) {
			Debug.Log(www.error);
			OnError();
		}
		else {
			// Or retrieve results as binary data
			//byte[] byteArray = www.downloadHandler.data;
		
			// Get bytes from file
			byte[] byteArray = Encoding.UTF8.GetBytes(www.downloadHandler.text);
			
			// Create memory stream
			MemoryStream stream = new MemoryStream(byteArray);

			// Convert MemoryStream to StreamReader
			StreamReader reader = new StreamReader(stream);
			
			if(threadedDownload) {
				Thread thread = new Thread(()=>LoadFromStream(reader));
				thread.Start();
			}
			else {
				LoadFromStream(reader);
			}
		}
    }
	
	void LoadFromStream(StreamReader reader) {
		
		try {
			// Create OBJ reader
			ObjReader objReader = new ObjReader();
		
			Vector3 [][] polylines = objReader.GetPolylinesFromStreamReader(reader);
		
			// Make sure to lock to avoid multithreading problems
			lock(_enque) {
				// Run the generation of polylines in the Main Thread
				ExecuteOnMainThread.Enqueue(() => {  StartCoroutine(AfterLoading()); } );
				ExecuteOnMainThread.Enqueue(() => {  StartCoroutine(Generate(polylines)); } );
			};
		}
		catch(Exception e) {
			Debug.Log(e);
			OnError();
		}
	}
	
	// Do something after loading
	protected virtual IEnumerator AfterLoading() {
		yield return null;
	}
	
	// Do something if error
	protected virtual void OnError() {
	}
}