using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NativeSimTest : MonoBehaviour
{
    System.Random rand = new System.Random();

    public string geometryFilePath = "3D_reconstruction_NEW.txt";
    public string velocityFilePath = "TECPLOT_CONVERGED_global_velocity_field.txt";
    public bool destroyOutOfBounds = true;
    public Material voxelMaterial = null;
    public PhysicMaterial geometryPhysic = null;
    const int NUM_PARTICLES = 1000; // Specify how many particles to show (1000 is the max currently)

    GeometryData geometryData = new GeometryData();
    FluidVelocityData velocityData = new FluidVelocityData();

    GameObject geometryGameObject = null;
    List<GameObject> particleGameObjects = new List<GameObject>(new GameObject[NUM_PARTICLES]);
    int[] particleIDs = new int[NUM_PARTICLES];

    List<Vector3> oldPositions = new List<Vector3>();
    double[] velocityScalars = new double[NUM_PARTICLES];
    List<Vector3> velocityVectors = new List<Vector3>();

    /*
     * This function will randomly generate a Vector3 position on the x = -30 plane, 
     * as long as there are no solid cubes at x = 1.
     */
    Vector3 GetRandomPosition(GeometryData data)
    {
        float y = (float)(rand.NextDouble() * (199 - 1) + 1);
        float z = (float)(rand.NextDouble() * (199 - 1) + 1);
        // This is pretty ugly, I might refactor later, not much I can do to make it nicer (as far as I know)
        // Basically, this checks to make sure there is not a solid cube at (1, y, z), but it also checks +-1 in the y and z
        // Directions so the edge of the particle spheres don't hit the geometry
        while (true)
        {
            if (data.GetIntensityAt(1, (int)y++, (int)z++) >= 7500f || data.GetIntensityAt(1, (int)y++, (int)z) >= 7500f ||
                data.GetIntensityAt(1, (int)y, (int)z++) >= 7500f || data.GetIntensityAt(1, (int)y, (int)z) >= 7500f ||
                data.GetIntensityAt(1, (int)y--, (int)z--) >= 7500f || data.GetIntensityAt(1, (int)y--, (int)z) >= 7500f ||
                data.GetIntensityAt(1, (int)y, (int)z--) >= 7500f || data.GetIntensityAt(1, (int)y++, (int)z--) >= 7500f ||
                data.GetIntensityAt(1, (int)y--, (int)z++) >= 7500f)
            {
                // Regenerate the random numbers
                y = (float)(rand.NextDouble() * (199 - 1) + 1);
                z = (float)(rand.NextDouble() * (199 - 1) + 1);
            }
            else
            {
                break;
            }
        }
        return new Vector3(1, y, z);
    }

    /*
     Create a cube for every occupied volume element ("voxel") in the data set.
     This is the improved version, with two major assumptions:
    
     1. We're not using a separate Unity GameObject for each cube; instead,
        we're going to merge lots of cube data into a smaller number of
        aggregate "meshes" of geometry data from the cubes.
    
     2. On the assusmption that we're not interested in the interior of solid
        regions of the material (i.e., fluid only moves through the *empty*
        regions), the only parts of the little cubes that we actually care about
        seeing are those where a solid region turns into an empty region. This
        allows us to drastically reduce the amount of information in the system,
        as we can throw away all the cube faces that are present where a solid
        cube is next to another solid cube! 
    */
    void BuildCubesNew(GeometryData data, List<CubeGenerator.Coordinate> vertices, List<int> indices)
    {
        vertices.Clear();
        indices.Clear();

        var cutoff = 7500f; // intensity to consider voxel region to be "solid"

        // Assumes unit cubes, with scaling applied via parent "Cubes" GameObject
        for (int k = 0; k < data.kMax; k++)
        {
            for (int j = 0; j < data.jMax; j++)
            {
                for (int i = 0; i < data.iMax; i++)
                {
                    var val = data.GetIntensityAt(i, j, k);

                    if (val < cutoff) continue; // cube not occupied, ignore. 

                    // Location on the regular lattice of the current cube.
                    var location = new CubeGenerator.Coordinate { x = i, y = j, z = k };

                    /*
                     We're looking for a transition from an "occupied" cube to an "empty" cube
                     to insert cube faces. The "current" cube at (i,j,k) is occupied, so look
                     at its immediate neighbours on each axis to see if they are empty; if so,
                     add a cube face before/after the current cube (as appropriate).
                    */

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

    /*
     Unfortunately, Unity only supports meshes with less than ~65,000 vertices. To allow us to use
     geometry meshes with more vertices, we can add the data as separate smaller meshes. This
     function does exactly that; it builds one or more meshes from the specified vertex/index data
     and adds the meshes to child GameObjects of the specified parent GameObject.
    
     Note: the ~65,000 vertex limit can be avoided in newer Unity versions, but let's try to keep
     this as compatible as possible with earlier Unity versions.
    */
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
            // Is the current mesh data in danger of getting too big? Add it to
            // a child object of the specified parent object, and start a new mesh.
            if (vtx.Count > maxVtx)
            {
                // Add current mesh data to parent object ...
                add(parent, vtx, idx);

                // ... then clear existing data to start a new mesh.
                vtx.Clear();
                idx.Clear();
                remap.Clear();
            }

            /*
             As we're potentially splitting a large data set into several smaller data sets,
             the vertex indices used to describe triangles in the full data set are not
             automatically valid for the smaller data sets; we need to convert "global"
             vertx indices (i.e., indices into the full data set) into "local" vertex indices
             (i.e., indices into the smaller data sets). We accomplish this by tracking which
             "global" vertex indices we have previously seen when generating the current
             "local" data set; if we come across a vertex index we have not used before
             in the local data set, we store it in the "remap" dictionary along with the
             appropriate local vertex index. We can now convert "global" vertex indices
             into "local" vertex indices by using the "remap" dictionary.
            */

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

    // This internal function creates a new child GameObject of the specified parent GameObject,
    // and then attaches mesh data generated from the specified vertex/index lists.
    void add(GameObject parent, List<CubeGenerator.Coordinate> vertices, List<int> indices)
    {
        var go = new GameObject("Mesh");
        go.tag = "Geometry";
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>().material = voxelMaterial;
        MeshCollider mc = go.AddComponent<MeshCollider>();
        mc.sharedMaterial = geometryPhysic;
        /*Rigidbody rbody = go.AddComponent<Rigidbody>();
        rbody.useGravity = false;
        rbody.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationZ;
        rbody.isKinematic = true;*/

        go.transform.SetParent(parent.transform);

        var v = new Vector3[vertices.Count * 3];
        for (int i = 0; i < vertices.Count; i++)
        {
            v[i].x = vertices[i].x;
            v[i].y = vertices[i].y;
            v[i].z = vertices[i].z;
        }

        var mesh = new Mesh
        {
            vertices = v,
            triangles = indices.ToArray()
        };
        mesh.RecalculateNormals();

        go.GetComponent<MeshFilter>().mesh = mesh;
        go.GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    // Use this for initialization
    void Start()
    {
        for (int i = 0; i < NUM_PARTICLES; ++i)
        {
            oldPositions.Add(new Vector3(0, 1, 1));
            velocityVectors.Add(new Vector3(5, 5, 5));
            particleIDs[i] = i;
        }

        var vertices = new List<CubeGenerator.Coordinate>();
        var indices = new List<int>();

        // Try to load the geometry data
        try
        {
            var errorMsg = geometryData.LoadFromFile(geometryFilePath);
            if (errorMsg != null)
            {
                Debug.LogWarning("Problem loading geometry data: " + errorMsg);
                return;
            }
            var str = $"System lattice dimensions: i={geometryData.iMax} j={geometryData.jMax} k={geometryData.kMax}";
            Debug.LogWarning(str);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Problem loading geometry data: " + e);
        }

        // Try to load the velocity data
        try
        {
            var errorMsg = velocityData.LoadFromFile(velocityFilePath);
            if (errorMsg != null)
            {
                Debug.LogWarning("Problem loading fluid velocity data: " + errorMsg);
                return;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Problem loading fluid velocity data: " + e);
        }

        // Generate cube data from the input file
        BuildCubesNew(geometryData, vertices, indices);

        // Add the mesh data as child GameObjects of a "Geometry" GameObject.
        geometryGameObject = new GameObject("Geometry");
        geometryGameObject.transform.SetParent(gameObject.transform);
        AddMeshToObject(geometryGameObject, vertices, indices);

        // Save the mesh data we calculated for inspection in e.g. MeshLab.
        // This can take a few seconds, so disable it if you don't care.
        //CubeGenerator.SaveWavefrontOBJ("cubes.obj", vertices, indices);

        for (int i = 0; i < NUM_PARTICLES; ++i)
        {
            particleGameObjects[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            particleGameObjects[i].name = "Particle " + i;
            particleGameObjects[i].tag = "Particle";
            //particleGameObjects[i].transform.SetParent(gameObject.transform);
            particleGameObjects[i].AddComponent<ParticleHandling>();
            particleGameObjects[i].GetComponent<SphereCollider>().sharedMaterial = geometryPhysic;
            Rigidbody rbody = particleGameObjects[i].AddComponent<Rigidbody>();
            rbody.position = GetRandomPosition(geometryData);
            rbody.useGravity = false;
            // Random x velocity between 1.0 and 5.0
            rbody.velocity = new Vector3((float)(rand.NextDouble() * (5 - 1) + 1), 0f, 0f);
        }
    }

    private void Update()
    {
        // Make sure the particles don't go out of bounds
        for (int i = 0; i < NUM_PARTICLES; ++i)
        {
            Rigidbody rBody = particleGameObjects[i].GetComponent<Rigidbody>();
            // X
            if (rBody.position.x < 0)
            {
                if (destroyOutOfBounds) {
                    Destroy(rBody);
                }
                else {
                    rBody.position = new Vector3(0, rBody.position.y, rBody.position.z);
                }
            }
            if (rBody.position.x >= 200) {
                if (destroyOutOfBounds) {
                    Destroy(rBody);
                }
                else {
                    rBody.position = new Vector3(rBody.position.x - 200, rBody.position.y, rBody.position.z);
                }
            }
            // Y
            if (rBody.position.y < 0)
            {
                if (destroyOutOfBounds) {
                    Destroy(rBody);
                }
                else {
                    rBody.position = new Vector3(rBody.position.x, 0, rBody.position.z);
                }
            }
            if (rBody.position.y >= 200)
            {
                if (destroyOutOfBounds) {
                    Destroy(rBody);
                }
                else {
                    rBody.position = new Vector3(rBody.position.x, rBody.position.y - 200, rBody.position.z);
                }
            }
            // Z
            if (rBody.position.z < 0)
            {
                if (destroyOutOfBounds) {
                    Destroy(rBody);
                }
                else { 
                    rBody.position = new Vector3(rBody.position.x, rBody.position.y, 0);
                }
            }
            if (rBody.position.z >= 200)
            {
                if (destroyOutOfBounds) {
                    Destroy(rBody);
                }
                else {
                    rBody.position = new Vector3(rBody.position.x, rBody.position.y, rBody.position.z - 200);
                }
            }
        }
    }
    
    private void FixedUpdate()
    {
        // Set the particles' velocity based on their position
        for (int i = 0; i < NUM_PARTICLES; ++i)
        {
            Rigidbody rBody = particleGameObjects[i].GetComponent<Rigidbody>();
            rBody.velocity = velocityData.GetVelocityAt((int)rBody.position.x, (int)rBody.position.y, (int)rBody.position.z);
        }

        var dt = Time.deltaTime;
        for (int i = 0; i < NUM_PARTICLES; ++i)
        {
            // Calculate velocity vector & scalar
            velocityVectors[i] = particleGameObjects[i].GetComponent<Rigidbody>().velocity;
            velocityScalars[i] = Math.Sqrt(velocityVectors[i].x * velocityVectors[i].x + velocityVectors[i].y * velocityVectors[i].y + velocityVectors[i].z * velocityVectors[i].z) / dt;
        }
        // This will sort the array of particle ids with respect to each particle's speed (must sort the speeds first)
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

    /* Override to handle what happens immediately after collisions
     */
    /*
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "Particle" && gameObject.tag == "Particle")
        {
            // creates joint
            FixedJoint joint = gameObject.AddComponent<FixedJoint>();
            // sets joint position to point of contact
            joint.anchor = collision.contacts[0].point;
            // conects the joint to the other object
            joint.connectedBody = collision.contacts[0].otherCollider.transform.GetComponentInParent<Rigidbody>();
            // Stops objects from continuing to collide and creating more joints
            joint.enableCollision = false;
            Physics.IgnoreCollision(collision.collider, GetComponent<Collider>());
        }
    }*/
}

