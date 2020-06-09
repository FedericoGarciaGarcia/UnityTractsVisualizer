///////////////////////////////////////////////////////////////////////////////
// Author: Federico Garcia Garcia
// License: GPL-3.0
// Created on: 08/06/2020 18:12
///////////////////////////////////////////////////////////////////////////////

using UnityEngine;

public class Screenshot : MonoBehaviour
{
	public int superSize = 1;
	
	void Update()
	{
		if (Input.GetKeyDown("space"))
        {
			ScreenCapture.CaptureScreenshot("C:\\Users\\FEDE\\Desktop\\screenshot.png", superSize);
			Debug.Log("Screenshot taken");
        }
	}
	
}