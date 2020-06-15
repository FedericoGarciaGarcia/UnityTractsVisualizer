///////////////////////////////////////////////////////////////////////////////
// Author: Federico Garcia Garcia
// License: GPL-3.0
// Created on: 08/06/2020 18:12
///////////////////////////////////////////////////////////////////////////////

using UnityEngine;

public class Screenshot : MonoBehaviour
{
	public int superSize = 1;
	
	private int i=0;
	
	void Update()
	{
		if (Input.GetKeyDown("space"))
        {
			ScreenCapture.CaptureScreenshot("C:\\Users\\FEDE\\Desktop\\screenshot_"+i+".png", superSize);
			Debug.Log(i+" screenshot taken");
			i++;
        }
	}
	
}