using System;
using System.Collections.Generic;
using UnityEngine;

/*
Loads system geometry and particle data, then generates the 3D meshes etc
and starts to "play" the trajectory.

This is complicated a little by the fact that Unity meshes by default only
support 2^16 (= ~65k) vertices in a mesh. We therefore may have the split
the "stacked cube" system geometry up into multiple meshes!
*/

public class Test : MonoBehaviour
{
    public string geometryFilePath = "3D_reconstruction_NEW.txt";
    public string trajectoryFilePath = "Particle_trajectories_THOUSAND.txt";
    public Material voxelMaterial = null, particleMaterial = null;
    public float frameDurationSeconds = 1f;
    float timestep = 0.3117167144E-03f;
    const int NUM_PARTICLES = 500; // Specify how many particles to show (1000 is the max currently)

    GeometryData geometryData = new GeometryData();
    TrajectoryData trajectoryData = new TrajectoryData();

    GameObject geometryGameObject = null;
    List<GameObject> particleGameObjects = new List<GameObject>(new GameObject[NUM_PARTICLES]);
    int[] particleIDs = new int[NUM_PARTICLES];

    int trajectoryFrame = 0;
    float lastTrajectoryFrameTransition = -1f;

    List<Vector3> oldPositions = new List<Vector3>(new Vector3[NUM_PARTICLES]);
    List<Vector3> positions = new List<Vector3>(new Vector3[NUM_PARTICLES]);
    double[] velocityScalars = new double[NUM_PARTICLES];
    List<Vector3> velocityVectors = new List<Vector3>(new Vector3[NUM_PARTICLES]);

    //
    // Create a cube for every occupied volume element ("voxel") in the data set.
    // This is the VERY SLOW, VERY INEFFICIENT approach!
    //
    /*void BuildCubesOld( GeometryData data )
    {
        // The current GameObject this script is attached to.
        var me = gameObject;

        // Cubes are stored under a "Cubes" GameObject, which created as a child of the current GameObject.
        var cubes = new GameObject("Cubes");
        cubes.transform.SetParent(me.transform);
        cubes.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

        //
        // Assumes unit cubes, with scaling applied via parent "Cubes" GameObject
        //
        for (var k = 0; k < data.kMax; k++)
        {
            for (var j = 0; j < data.jMax; j++)
            {
                for (var i = 0; i < data.iMax; i++)
                {
                    var val = data.GetIntensityAt(i, j, k);
                    if (val < 7500f) continue; // in this case, ignore values of 0 or 5000. 

                    var rx = -(0.5f * data.iMax) + (0.5f + i); // location of cube center: i->x, j->y, k->z
                    var ry = -(0.5f * data.iMax) + (0.5f + j);
                    var rz = -(0.5f * data.iMax) + (0.5f + k);

                    // Create new cube GameObject, and set as a child of the "Cubes" GameObject
                    var newCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    newCube.transform.SetParent(cubes.transform);
                    newCube.transform.localPosition = new Vector3(rx, ry, rz);

                    // Remove the BoxCollider component from the cube; we don't need these at the moment.
                    var boxCollider = newCube.GetComponent<BoxCollider>();
                    if (boxCollider) Destroy(boxCollider);

                }
            }
        }
    } */

    //
    // Create a cube for every occupied volume element ("voxel") in the data set.
    // This is the improved version, with two major assumptions:
    //
    // 1. We're not using a separate Unity GameObject for each cube; instead,
    //    we're going to merge lots of cube data into a smaller number of
    //    aggregate "meshes" of geometry data from the cubes.
    //
    // 2. On the assusmption that we're not interested in the interior of solid
    //    regions of the material (i.e., fluid only moves through the *empty*
    //    regions), the only parts of the little cubes that we actually care about
    //    seeing are those where a solid region turns into an empty region. This
    //    allows us to drastically reduce the amount of information in the system,
    //    as we can throw away all the cube faces that are present where a solid
    //    cube is next to another solid cube! 
    //
    void BuildCubesNew(GeometryData data, List<CubeGenerator.Coordinate> vertices, List<int> indices)
    {
        vertices.Clear();
        indices.Clear();

        var cutoff = 7500f; // intensity to consider voxel region to be "solid"

        //
        // Assumes unit cubes, with scaling applied via parent "Cubes" GameObject
        //
        for (int k = 0; k < data.kMax; k++)
        {
            for (int j = 0; j < data.jMax; j++)
            {
                for (int i = 0; i < data.iMax; i++)
                {
                    var val = data.GetIntensityAt(i, j, k);

                    if (val < cutoff) continue; // cube not occupied, ignore. 

                    // Location on the regular lattice of the current cube.
                    var location = new CubeGenerator.Coordinate { x=i, y=j, z=k };

                    //
                    // We're looking for a transition from an "occupied" cube to an "empty" cube
                    // to insert cube faces. The "current" cube at (i,j,k) is occupied, so look
                    // at its immediate neighbours on each axis to see if they are empty; if so,
                    // add a cube face before/after the current cube (as appropriate).
                    //

                    // -x/+x faces
                    {
                        bool prev_empty = true, next_empty = true;

                        if (i > 0) prev_empty = (data.GetIntensityAt(i - 1, j, k) < cutoff);
                        if (i < data.iMax - 1) next_empty = (data.GetIntensityAt(i + 1, j, k) < cutoff);

                        if (prev_empty) CubeGenerator.GenerateFace(0, 0, location, vertices, indices);
                        if (next_empty) CubeGenerator.GenerateFace(0, 1, location, vertices, indices);
                    }

                    // -y/+y faces
                    {
                        bool prev_empty = true, next_empty = true;

                        if (j > 0) prev_empty = (data.GetIntensityAt(i, j - 1, k) < cutoff);
                        if (j < data.jMax - 1) next_empty = (data.GetIntensityAt(i, j + 1, k) < cutoff);

                        if (prev_empty) CubeGenerator.GenerateFace(1, 0, location, vertices, indices);
                        if (next_empty) CubeGenerator.GenerateFace(1, 1, location, vertices, indices);
                    }

                    // -z/+z faces
                    {
                        bool prev_empty = true, next_empty = true;

                        if (k > 0) prev_empty = (data.GetIntensityAt(i, j, k - 1) < cutoff);
                        if (k < data.jMax - 1) next_empty = (data.GetIntensityAt(i, j, k + 1) < cutoff);

                        if (prev_empty) CubeGenerator.GenerateFace(2, 0, location, vertices, indices);
                        if (next_empty) CubeGenerator.GenerateFace(2, 1, location, vertices, indices);
                    }
                }
            }
        }
    }

    //
    // Unfortunately, Unity only supports meshes with less than ~65,000 vertices. To allow us to use
    // geometry meshes with more vertices, we can add the data as separate smaller meshes. This
    // function does exactly that; it builds one or more meshes from the specified vertex/index data
    // and adds the meshes to child GameObjects of the specified parent GameObject.
    //
    // Note: the ~65,000 vertex limit can be avoided in newer Unity versions, but let's try to keep
    // this as compatible as possible with earlier Unity versions.
    //
    void AddMeshToObject(GameObject parent, List<CubeGenerator.Coordinate> vertices, List<int> indices)
    {
        var maxVtx = 65000; // Unity only allows less than ~65,000 vertices per mesh!

        var remap = new Dictionary<int, int>();
        var vtx = new List<CubeGenerator.Coordinate>();
        var idx = new List<int>();

        int i, j, k;    // original vertex indices in global set
        int i_, j_, k_; // remapped vertex indices in local set

        for (var ti = 0; ti < indices.Count; ti += 3)
        {
            //
            // Is the current mesh data in danger of getting too big? Add it to
            // a child object of the specified parent object, and start a new mesh.
            //
            if (vtx.Count > maxVtx)
            {
                // Add current mesh data to parent object ...
                add(parent, vtx, idx);

                // ... then clear existing data to start a new mesh.
                vtx.Clear();
                idx.Clear();
                remap.Clear();
            }

            //
            // As we're potentially splitting a large data set into several smaller data sets,
            // the vertex indices used to describe triangles in the full data set are not
            // automatically valid for the smaller data sets; we need to convert "global"
            // vertx indices (i.e., indices into the full data set) into "local" vertex indices
            // (i.e., indices into the smaller data sets). We accomplish this by tracking which
            // "global" vertex indices we have previously seen when generating the current
            // "local" data set; if we come across a vertex index we have not used before
            // in the local data set, we store it in the "remap" dictionary along with the
            // appropriate local vertex index. We can now convert "global" vertex indices
            // into "local" vertex indices by using the "remap" dictionary.
            //

            // "Global" vertex indices for the triangle (i.e. indices into full data set)
            i = indices[ti + 0];
            j = indices[ti + 1];
            k = indices[ti + 2];

            // If we have not seen a global vertex index before in the current local data
            // set, add the global vertex to the local data set and store the global->local
            // conversion in the "remap" dictionary for future lookups.
            if (!remap.TryGetValue(i, out i_))
            {
                i_ = vtx.Count;
                remap[i] = i_;
                vtx.Add(vertices[i]);
            }

            if (!remap.TryGetValue(j, out j_))
            {
                j_ = vtx.Count;
                remap[j] = j_;
                vtx.Add(vertices[j]);
            }

            if (!remap.TryGetValue(k, out k_))
            {
                k_ = vtx.Count;
                remap[k] = k_;
                vtx.Add(vertices[k]);
            }

            // Add the LOCAL vertex indices for this triangle to the LOCAL mesh data!
            idx.Add(i_);
            idx.Add(j_);
            idx.Add(k_);
        }

        // If we have any partial mesh data left over, add it now.
        if (idx.Count > 0) add(parent, vtx, idx);
    }

    //
    // This internal function creates a new child GameObject of the specified parent GameObject,
    // and then attaches mesh data generated from the specified vertex/index lists.
    //
    void add(GameObject parent, List<CubeGenerator.Coordinate> vertices, List<int> indices)
    {
        var go = new GameObject("Mesh");
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>().material = voxelMaterial;
        go.transform.SetParent(parent.transform);

        var v = new Vector3[vertices.Count * 3];
        for (int i = 0; i < vertices.Count; i++)
        {
            v[i].x = vertices[i].x;
            v[i].y = vertices[i].y;
            v[i].z = vertices[i].z;
        }

        var mesh = new Mesh {
            vertices = v,
            triangles = indices.ToArray()
        };
        mesh.RecalculateNormals();

        go.GetComponent<MeshFilter>().mesh = mesh;
    }

    //
    // Use this for initialization
    //
    void Start ()
    {
        for (int i = 0; i < NUM_PARTICLES; ++i) {
            oldPositions.Add(new Vector3(0, 0, 0));
            positions.Add(new Vector3(0, 0, 0));
            velocityVectors.Add(new Vector3(0, 0, 0));
            particleIDs[i] = i;
        }

        var vertices = new List<CubeGenerator.Coordinate>();
        var indices = new List<int>();

        //
        // Try to load the geometry data
        //
        try
        {
            var errorMsg = geometryData.LoadFromFile(geometryFilePath);
            if ( errorMsg != null )
            {
                Debug.LogWarning("Problem loading geometry data: " + errorMsg);
                return;
            }
            var str = $"System lattice dimensions: i={geometryData.iMax} j={geometryData.jMax} k={geometryData.kMax}";
            Debug.LogWarning(str);
        }
        catch ( System.Exception e )
        {
            Debug.LogWarning( "Problem loading geometry data: " + e );
        }

        //
        // Generate cube data from the input file
        //
        BuildCubesNew(geometryData, vertices, indices);

        //
        // Add the mesh data as child GameObjects of a "Geometry" GameObject.
        //
        geometryGameObject = new GameObject("Geometry");
        geometryGameObject.transform.SetParent( gameObject.transform );
        AddMeshToObject(geometryGameObject, vertices, indices);

        //
        // Save the mesh data we calculated for inspection in e.g. MeshLab.
        // This can take a few seconds, so disable it if you don't care.
        //
        CubeGenerator.SaveWavefrontOBJ("cubes.obj", vertices, indices);

        //
        // Try to load the trajectory data
        //
        try
        {
            var errorMsg = trajectoryData.LoadFromFile(trajectoryFilePath);
            if (errorMsg != null)
            {
                Debug.LogWarning("Problem loading trajectory data: " + errorMsg);
                return;
            }
            var str = $"{trajectoryData.idToTrajectory.Keys.Count} particle trajectories loaded";
            Debug.LogWarning(str);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Problem loading trajectory data: " + e);
        }

        //
        // Debug! Print number of x,y,z coordinates read for each particle ID.
        //
        foreach (var key in trajectoryData.idToTrajectory.Keys)
        {
            var t = trajectoryData.idToTrajectory[key];
            var s = $"Particle id = {key} : {t.Count} entries, {t.Count/3} frames";
            Debug.LogWarning(s);
        }

        //
        // We're just using a single particle for now; add a simple sphere to
        // represent the particle as a child of the current GameObject. We'll
        // keep the default sphere size of 1.0 units for now.
        // Note: we hide the particle for now (via SetActive(false)), and
        // enable it in Update() if we find appropriate trajectory data.
        //
        //matBlock = new MaterialPropertyBlock();
        //matBlock.SetTexture("ParticleTexture", particleTexture);

        for (int i = 0; i < NUM_PARTICLES; ++i)
        {
            particleGameObjects[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            particleGameObjects[i].name = "Particle " + i;
            particleGameObjects[i].transform.SetParent(gameObject.transform);
            particleGameObjects[i].SetActive(false);
            particleGameObjects[i].GetComponent<Renderer>().material = particleMaterial;
        }
    }

    //
    // Animate the trajectory data; Update() is called by the Unity system 30+
    // times per second and so we implement the particle animation in Update().
    //
    // This is just for an example; in practice, we'd probably want a better
    // way to specify which particles to display at any given time.
    //
    private void Update()
    {
        var currentTime = Time.time;
        var dt = Time.deltaTime;
        for (int particleID = 0; particleID < NUM_PARTICLES; ++particleID)
        {
            var l = trajectoryData.GetTrajectoryFromParticleID(particleID + 1);
            if ((l == null) || (l.Count < 1)) return; // no such particle ID, or empty trajectory

            var nTrajectoryFrames = l.Count / 3;

            // Enable our particle object
            particleGameObjects[particleID].SetActive(true);

            // "p" will be set to the position of the particle. By default we set
            // to the coords found in the first trajectory frame. We'll overwrite
            // this later if needed.
            positions[particleID] = new Vector3(l[0], l[1], l[2]);

            // If we actually have more than one trajectory frame ...
            if (nTrajectoryFrames > 1)
            {
                // When was the last time we switched to the next trajectory frame, and how far through
                // the "current" trajectory frame are we? This fraction will be used to linearly interpolate
                // between successive positions in the trajectory to avoid sharp "jumps" from place to place.
                if (lastTrajectoryFrameTransition < 0f) lastTrajectoryFrameTransition = currentTime;
                var frameFraction = (currentTime - lastTrajectoryFrameTransition) / frameDurationSeconds;

                // Do we need to increment the trajectory frame?
                if (frameFraction >= 1.0f)
                {
                    trajectoryFrame++;
                    if (trajectoryFrame >= nTrajectoryFrames - 1) trajectoryFrame = 0;
                    lastTrajectoryFrameTransition = currentTime;
                    frameFraction = 0f;
                }

                // Particle position is linearly interpolated between the "current" trajectory frame
                // and the "next" trajectory frame.
                var i = trajectoryFrame * 3;
                var j = (trajectoryFrame + 1) * 3;
                var p1 = new Vector3(l[i + 0], l[i + 1], l[i + 2]); // "current" trajectory frame position
                var p2 = new Vector3(l[j + 0], l[j + 1], l[j + 2]); // "next" trajectory frame position
                positions[particleID] = Vector3.Lerp(p1, p2, frameFraction);

                // Calculate velocity vector & scalar
                var delta = positions[particleID] - oldPositions[particleID];
                velocityScalars[particleID] = Math.Sqrt(delta.x * delta.x + delta.y * delta.y + delta.z * delta.z) / dt;
                oldPositions[particleID] = positions[particleID];
            }
            // Move particle object to newly calculated position.
            particleGameObjects[particleID].transform.position = positions[particleID];
        }
        // This will sort the array of particle ids with respect to each particle's speed
        Array.Sort(velocityScalars);
        Array.Sort(velocityScalars, particleIDs);
        // Now we update the particles' colors based on their speeds.
        // This has to be a separate loop to break the array into quarters:
        // The colors will go red, yellow, green, blue, red being the slowest.
        for (int i = 0; i < NUM_PARTICLES / 4; ++i)
        {
            // Bottom 25%: red
            particleGameObjects[particleIDs[i]].GetComponent<Renderer>().material.color = Color.blue;
            // Second 25%: yellow
            particleGameObjects[particleIDs[i + NUM_PARTICLES / 4]].GetComponent<Renderer>().material.color = Color.green;
            // Third 25%: green
            particleGameObjects[particleIDs[i + NUM_PARTICLES / 2]].GetComponent<Renderer>().material.color = Color.yellow;
            // Last 25%: blue
            particleGameObjects[particleIDs[i + NUM_PARTICLES / 2 + NUM_PARTICLES / 4]].GetComponent<Renderer>().material.color = Color.red;
        }
    }
}
