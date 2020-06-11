using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class SliderValueToText : MonoBehaviour {
  public Slider sliderUI;
  private Text textSliderValue;

  void Start (){
    textSliderValue = GetComponent<Text>();
    ShowSliderValue();
  }

  public void ShowSliderValue () {
	  
    string sliderMessage;
	
	if(sliderUI.wholeNumbers)
		sliderMessage = ""+sliderUI.value;
	else
		sliderMessage = sliderUI.value.ToString("F2");
	
	if(textSliderValue != null)
    textSliderValue.text = sliderMessage;
  }
}