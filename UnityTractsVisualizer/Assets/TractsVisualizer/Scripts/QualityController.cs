using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class QualityController : MonoBehaviour
{
	public Dropdown dropdown;
	public GameObject camera;
	public GameObject cameraPostprocessing;
	
	private int screenWidth, screenHeight;
	
	void Start() {
		screenWidth  = Screen.width;
		screenHeight = Screen.height;
	}
	
	public void ChangeQuality() {
		switch(dropdown.value) {
			case 0: SetLow();     break;
			case 1: SetMedium();  break;
			case 2: SetHigh();    break;
			case 3: SetHighest(); break;
			default: SetLow();    break;
		}
	}
	
	public void SetLow() {
		QualitySettings.SetQualityLevel(0, true);
		SetPP(false);
		
        Screen.SetResolution(screenWidth/2, screenHeight/2, true);
	}
	
	public void SetMedium() {
		QualitySettings.SetQualityLevel(2, true);
		SetPP(false);
		
        Screen.SetResolution(screenWidth, screenHeight, true);
	}
	
	public void SetHigh() {
		QualitySettings.SetQualityLevel(3, true);
		SetPP(false);
		
        Screen.SetResolution(screenWidth, screenHeight, true);
	}
	
	public void SetHighest() {
		QualitySettings.SetQualityLevel(5, true);
		SetPP(true);
		
        Screen.SetResolution(screenWidth, screenHeight, true);
	}
	
	public void SetPP(bool flag) {
		camera.SetActive(!flag);
		cameraPostprocessing.SetActive(flag);
	}
}
