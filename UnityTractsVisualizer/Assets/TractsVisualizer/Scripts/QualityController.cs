using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class QualityController : MonoBehaviour
{
	public Dropdown dropdown;
	public GameObject camera;
	public GameObject cameraPostprocessing;
	
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
	}
	
	public void SetMedium() {
		QualitySettings.SetQualityLevel(2, true);
		SetPP(false);
	}
	
	public void SetHigh() {
		QualitySettings.SetQualityLevel(3, true);
		SetPP(false);
	}
	
	public void SetHighest() {
		QualitySettings.SetQualityLevel(5, true);
		SetPP(true);
	}
	
	public void SetPP(bool flag) {
		camera.SetActive(!flag);
		cameraPostprocessing.SetActive(flag);
	}
}
