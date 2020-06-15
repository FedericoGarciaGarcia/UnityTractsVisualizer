///////////////////////////////////////////////////////////////////////////////
// Author: Federico Garcia Garcia
// License: GPL-3.0
// Created on: 14/06/2020 14:08
///////////////////////////////////////////////////////////////////////////////

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotator : MonoBehaviour
{
	public Vector3 rotation = new Vector3(0, 10, 0);
	
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
		if (Input.GetKeyDown("right"))
        {
			transform.Rotate(rotation);
		}
		else if (Input.GetKeyDown("left"))
        {
			transform.Rotate(-rotation);
		}
    }
}
