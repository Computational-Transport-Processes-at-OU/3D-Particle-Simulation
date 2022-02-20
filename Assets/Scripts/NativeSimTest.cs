using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NativeSimTest : MonoBehaviour
{
    internal static System.Random rand = new System.Random();

    public static int NUM_PARTICLES = 1000; // Specify how many particles to show 
    public double aggregationRate = 0.5;
    public int numberOfRestarts = 4;
    public string geometryFilePath = "3D_reconstruction_NEW.txt";
    public string velocityFilePath = "TECPLOT_CONVERGED_global_velocity_field.txt";
    public Material voxelMaterial = null;
    public PhysicMaterial geometryPhysic = null;

    GeometryData geometryData = new GeometryData();
    GameObject geometryGameObject = null;
    internal static FluidVelocityData velocityData = new FluidVelocityData();

    // These three variables store the particle speed quartile thresholds
    internal static float topThreshold = 0;
    internal static float midThreshold = 0;
    internal static float bottomThreshold = 0;


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
        var vertices = new List<CubeGenerator.Coordinate>();
        var indices = new List<int>();

        // Try to load the geometry data
        try
        {
            var errorMsg = geometryData.LoadFromFile(geometryFilePath);
            if (errorMsg != null)
            {
                Debug.LogError("Problem loading geometry data: " + errorMsg);
                return;
            }
            var str = $"System lattice dimensions: i={geometryData.iMax} j={geometryData.jMax} k={geometryData.kMax}";
            Debug.Log(str);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Problem loading geometry data: " + e);
        }

        // Try to load the velocity data
        try
        {
            var errorMsg = velocityData.LoadFromFile(velocityFilePath);
            if (errorMsg != null)
            {
                Debug.LogError("Problem loading fluid velocity data: " + errorMsg);
                return;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Problem loading fluid velocity data: " + e);
        }

        // Separate the velocity magnitudes into quarters to be used to define particle colors in ParticleHandler
        List<float> velocityScalars = velocityData.magnitudes;
        velocityScalars.Sort();
        bottomThreshold = velocityScalars[velocityScalars.Count / 4];
        midThreshold = velocityScalars[velocityScalars.Count / 2];
        topThreshold = velocityScalars[velocityScalars.Count / 2 + velocityScalars.Count / 4];

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
            new ParticleObject(i, aggregationRate, geometryPhysic, geometryData);
        }
    }

    private void FixedUpdate()
    {
        // Restart the simulation if all Particles are destroyed
        if (numberOfRestarts > 0)
        {
            if (GameObject.FindWithTag("Particle") == null)
            {
                Debug.Log("Respawning particles! " + numberOfRestarts + "respawns left!");
                for (int i = 0; i < NUM_PARTICLES; ++i)
                {
                    new ParticleObject(i, aggregationRate, geometryPhysic, geometryData);
                }
                --numberOfRestarts;
            }
        }
    }
}

