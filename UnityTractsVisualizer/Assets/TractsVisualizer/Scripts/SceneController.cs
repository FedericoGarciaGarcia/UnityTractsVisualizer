using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
	// Path of .obj file to load (or download)
	public static string objPath;
	
	// Do not destroy between scenes
	void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
    }
	
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
	
	// Load scenes
	public void LoadSceneObjURL() {
		SceneManager.LoadScene("Viewer", LoadSceneMode.Single);
	}
	
	// Load scenes
	public void LoadSceneObj1() {
		SceneManager.LoadScene("Viewer", LoadSceneMode.Single);
		objPath = "https://raw.githubusercontent.com/FedericoGarciaGarcia/UnityTractsVisualizer/development/UnityTractsVisualizer/Resources/corpuscallosum.obj";
	}
	
	// Load scenes
	public void LoadSceneObj2() {
		SceneManager.LoadScene("Viewer", LoadSceneMode.Single);
		objPath = "https://raw.githubusercontent.com/FedericoGarciaGarcia/UnityTractsVisualizer/development/UnityTractsVisualizer/Resources/fibres.obj";
	}
}
