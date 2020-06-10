# UnityTractsVisualizer
Tract data interaction and visualization with LOD in Unity.

You can try the web app [here](https://htmlpreview.github.io/?https://github.com/FedericoGarciaGarcia/UnityTractsVisualizer/blob/development/Web/index.html).

![Obj](https://raw.githubusercontent.com/FedericoGarciaGarcia/UnityTubeExtrusion/master/Images/Corpus%20callosum%20AO.jpg)

## Features

* Tube extrusion.
* Texturing.
* Coloring.
    * By polyline file order.
    * By axis direction.
    * By similarity.
* Resolution.
* Decimation by angle.
* End capping.
* Multithreading.
* Realtime.
* Load data from URL.

## Installing

Download this repository and manually import files in Unity.

## How to use

### Data

Data must be in Wavefront OBJ format, and can only consist of lines.

There are several *.obj* data files in the repository's *Resources* folder as examples.

OBJ files do not need to be included in Unity's Asset or Resources folder. URLs can also be used.

### *TubeGenerator* properties

Before tube extrusion and LOD grouping, the following properties may be specified:

* *LOD Distance Threshold*: if lines have this much distance, they will no merge.
* *LOD*: how many levels of LOD should be generated.
* *Deque size:* how many tubes are to be sent to the GPU each frame after extrusion. If value is too high, there will be a performance hit. 50 by default.
* *Decimation Angle:* beween 0 and 180. Given three adyacent points P<sub>i</sub>, P<sub>i+1</sub> and P<sub>i+2</sub>, so that P<sub>j</sub> and P<sub>j+1</sub> are adjacent for all **j**.
* *Scale:* rescaling of the polylines. 1 by default (no rescaling).
* *Radius:* the tube 'thickness'. 1 by default.
* *Resolution:* number of sides in each tube. 3 by default.
* *Material:* texture and shader material. None by default.
* *Color Start:* the first color. White by default.
* *Color End:* the last color. Tubes will be interpolated between *Color Start* and *Color End*. White by default.
* *Threads:* how many threads to use for tube extrusion and LOD generation. It is not recommended to use all available CPU threads, as Unity uses one for the Main thread. 1 by default.

## Authors

* **Federico Garcia Garcia**

## Acknowledgments

Textures and materials taken from:
* [3D Textures](https://3dtextures.me/)
* [Texture Haven](https://texturehaven.com/textures/)