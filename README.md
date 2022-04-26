# 3D Particle Simulation in Unity

This project is meant to be a presentation and visualization of some of the research done by Dr. Dimitrios Papavassiliou and his students for Computational Transport Processes.
This code will render a 3D porous material space, and create 3D particles that move in a simulated fluid flow through it.

## Credits
This project is the result of Honors Research done by Zachary Singleton in Fall 2021 and Spring 2022 at the University of Oklahoma. Much of the foundational code (such as reading and processing data files and rendering the 3D geometry) was created in 2019 by Dr. John Grime from the OU Library and student Devin Dill. 

## Functionality
The most essential component of this project is, of course, loading the 3D geometry. First, a data file with the geometry is loaded in. This is handled by `Assets/Scripts/Data.cs`. Then, several methods in `Assets/Scripts/NativeSim.cs` take this data and construct voxels at runtime to represent the porous material. This is done by determining all the coordinates where there is solid material, and then adding a cube face (adjacent cube faces are ignored to save computational load). Then, particles are loaded in. There are 2 scripts that handle particles separately:
* `PlaybackTest.cs` loads particle positions from a data file, and simply updates their positions as time passes, essentially acting as a playback for a simulation created in different software.
* `NativeSim.cs` uses Unity's physics engine to simulate particle behavior. A data file is read that defines x, y, and z velocities at every coordinate of the porous medium. This essentially simulates the behavior of a fluid flowing through the medium. Then, an adjustable number of particles are spawned. These particles probabilistically collide with each other based on an adjustable rate of aggregation. Based on a toggle, particles that exit the porous medium will either be destroyed, or reappear at the opposite end of the medium (since the medium is ___).  

## Running the Program
In order to run the program, one will have to install and setup the free [Unity Editor](https://unity.com/products/unity-platform) through the [Unity Hub](https://unity3d.com/get-unity/download). Then, after downloading this repository and unzipping it, one will just have to [import](https://docs.unity3d.com/2018.3/Documentation/Manual/GettingStartedOpeningProjects.html) the project into Unity Hub. NOTE that the data files must be separately downloaded from a [Google Drive](https://drive.google.com/drive/folders/1oe1ViM8rgkVKcAcduYa1ZJ79Geg1NmTt?usp=sharing). Then, simply open the project in the Unity Editor. Adjust any of the variables seen in the screenshot below. Note that only one of the scripts can be selected. The adjustable variables should be self-explanatory. The velocity scale adjusts how quickly the particles travel, and the number of restarts only applies when the particles are set to be destroyed out of bounds (set in `ParticleHandler.cs`). When all particles are destroyed, the simulation can be set to reset a certain number of times, with the option to vary the aggregation rate. To run, just click the play button at the top. The controls are:
* WASD keys to move
* Mouse to look
* Space to fly up
* Shift to fly down   
