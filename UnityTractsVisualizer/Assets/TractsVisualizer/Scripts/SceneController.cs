using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
	public Text url;
	
	// Load scenes
	public void LoadSceneObjURL() {
		SceneManager.LoadScene("Viewer", LoadSceneMode.Single);
		Global.objPath = url.text;
	}
	
	// Load scenes
	public void LoadSceneObj1() {
		SceneManager.LoadScene("Viewer", LoadSceneMode.Single);
		Global.objPath = "https://raw.githubusercontent.com/FedericoGarciaGarcia/UnityTractsVisualizer/development/UnityTractsVisualizer/Resources/corpuscallosum.obj";
	}
	
	// Load scenes
	public void LoadSceneObj2() {
		SceneManager.LoadScene("Viewer", LoadSceneMode.Single);
		Global.objPath = "https://raw.githubusercontent.com/FedericoGarciaGarcia/UnityTractsVisualizer/development/UnityTractsVisualizer/Resources/fibres.obj";
	}
	
	public void LoadSceneMenu() {
		SceneManager.LoadScene("Main", LoadSceneMode.Single);
	}
}
