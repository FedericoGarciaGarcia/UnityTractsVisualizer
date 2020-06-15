///////////////////////////////////////////////////////////////////////////////
// Author: Federico Garcia Garcia
// License: GPL-3.0
// Created on: 11/06/2020 12:00
///////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public class TubeGeneratorFromObjMenu : TubeGeneratorFromObj
{
	public GameObject loading; // A GameObject that is disabled after the data is generated
	public Slider sliderResolution;
	public Slider sliderDecimation;
	public Slider sliderRadius;
	public Slider sliderDequeSize;
	public Slider sliderVoxelSize;
	public Button buttonUpdate;
	public Toggle toggleLod;
	public Text textLoading;
	public GameObject imageLoading;
	
	protected override IEnumerator AfterLoading() {
		if(loading != null)
		loading.SetActive(false);
		
		yield return null;
	}
	
	public void SetResolution() {
		resolution = (int)sliderResolution.value;
	}
	
	public void SetDecimation() {
		decimation = sliderDecimation.value;
	}
	
	public void SetRadius() {
		radius = sliderRadius.value;
		//UpdateRadius();
	}
	
	public void SetDequeSize() {
		dequeSize = (int)sliderDequeSize.value;
	}
	
	public void SetVoxelSize() {
		voxelCount = (int)sliderVoxelSize.value;
	}
	
	public void SetLod() {
		lod = toggleLod.isOn;
	}
	
	// Do something if error
	protected override void OnError() {
		ExecuteOnMainThread.Enqueue(() => {  StartCoroutine(ErrorMessage()); } );
	}
	
	IEnumerator ErrorMessage() {
		textLoading.text = "There was an error loading the .obj file";
		imageLoading.SetActive(false);
		yield return null;
	}
}