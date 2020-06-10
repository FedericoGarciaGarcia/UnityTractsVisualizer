using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Global : MonoBehaviour
{
	public static string objectURL;
	
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
}
