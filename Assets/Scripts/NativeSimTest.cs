using System;
using System.Linq;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NativeSimTest : MonoBehaviour
{
    internal static System.Random rand = new System.Random();

    // Public variables that can be set in the Editor
    public bool disableCollidingSpawns;
    public int numberOfParticles = 1000; // Specify how many particles to show 
    public float particleAngularDrag;
    public float particleDrag;
    public float velocityScale;
    public int numberOfRestarts;
    public double[] aggregationRates;
    public string geometryFilePath = "";
    public string velocityFilePath = "";
    public Material voxelMaterial = null;
    public PhysicMaterial geometryPhysic = null;
    public Material spaceMaterial = null;

    GeometryData geometryData = new GeometryData();
    GameObject geometryGameObject = null;
    GameObject spaceGameObject = null;
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
    void BuildCubesNew(GeometryData data, FluidVelocityData vdata, List<CubeGenerator.Coordinate> vertices, List<int> indices, bool colorOpenSpace)
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

                    if (val < cutoff && !colorOpenSpace) continue; // cube not occupied, ignore. 

                    // we want to color the open space, so ignore occupied cube or cube with positive x velocity.
                    if (colorOpenSpace)
                    {
                        if (val >= cutoff || vdata.GetVelocityAt(i, j, k).x >= 0f) continue;
                    }

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

                        // EXPLAIN
                        if (colorOpenSpace)
                        {
                            if (i > 0) prev_empty = (data.GetIntensityAt(i - 1, j, k) >= cutoff || vdata.GetVelocityAt(i, j, k).x >= 0f);
                            if (i < data.iMax - 1) next_empty = (data.GetIntensityAt(i + 1, j, k) >= cutoff || vdata.GetVelocityAt(i, j, k).x >= 0f);
                        }

                        else
                        {
                            if (i > 0) prev_empty = (data.GetIntensityAt(i - 1, j, k) < cutoff);
                            if (i < data.iMax - 1) next_empty = (data.GetIntensityAt(i + 1, j, k) < cutoff);
                        }

                        if (prev_empty) CubeGenerator.GenerateFace(0, 0, location, vertices, indices);
                        if (next_empty) CubeGenerator.GenerateFace(0, 1, location, vertices, indices);
                    }

                    // -y/+y faces
                    {
                        bool prev_empty = true, next_empty = true;

                        // EXPLAIN
                        if (colorOpenSpace)
                        {
                            if (j > 0) prev_empty = (data.GetIntensityAt(i, j - 1, k) >= cutoff || vdata.GetVelocityAt(i, j, k).x >= 0f);
                            if (j < data.jMax - 1) next_empty = (data.GetIntensityAt(i, j + 1, k) >= cutoff || vdata.GetVelocityAt(i, j, k).x >= 0f);
                        }

                        else
                        {
                            if (j > 0) prev_empty = (data.GetIntensityAt(i, j - 1, k) < cutoff);
                            if (j < data.jMax - 1) next_empty = (data.GetIntensityAt(i, j + 1, k) < cutoff);
                        }

                        if (prev_empty) CubeGenerator.GenerateFace(1, 0, location, vertices, indices);
                        if (next_empty) CubeGenerator.GenerateFace(1, 1, location, vertices, indices);
                    }

                    // -z/+z faces
                    {
                        bool prev_empty = true, next_empty = true;

                        // TODO: EXPLAIN
                        if (colorOpenSpace)
                        {
                            if (k > 0) prev_empty = (data.GetIntensityAt(i, j, k - 1) >= cutoff || vdata.GetVelocityAt(i, j, k).x >= 0f);
                            if (k < data.jMax - 1) next_empty = (data.GetIntensityAt(i, j, k + 1) >= cutoff || vdata.GetVelocityAt(i, j, k).x >= 0f);
                        }

                        else
                        {
                            if (k > 0) prev_empty = (data.GetIntensityAt(i, j, k - 1) < cutoff);
                            if (k < data.jMax - 1) next_empty = (data.GetIntensityAt(i, j, k + 1) < cutoff);
                        }

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
    void AddMeshToObject(GameObject parent, List<CubeGenerator.Coordinate> vertices, List<int> indices, bool colorOpenSpace)
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
                add(parent, vtx, idx, colorOpenSpace);

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
        if (idx.Count > 0) add(parent, vtx, idx, colorOpenSpace);
    }

    // This internal function creates a new child GameObject of the specified parent GameObject,
    // and then attaches mesh data generated from the specified vertex/index lists.
    void add(GameObject parent, List<CubeGenerator.Coordinate> vertices, List<int> indices, bool colorOpenSpace)
    {
        var go = new GameObject("Mesh");

        if (colorOpenSpace)
        {
            go.tag = "Space";
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>().material = spaceMaterial;
        }
        else
        {
            go.tag = "Geometry";
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>().material = voxelMaterial;
            MeshCollider mc = go.AddComponent<MeshCollider>();
            mc.sharedMaterial = geometryPhysic;
        }

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
        if (!colorOpenSpace) 
            go.GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    // Use this for initialization
    void Start()
    {
        if (numberOfRestarts != aggregationRates.Length)
        {
            throw new ArgumentException("Number of Restarts must be the same as the number of Aggregation Rates!!");
        }

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

        // Generate cube data from the input file
        BuildCubesNew(geometryData, velocityData, vertices, indices, false);

        // Add the mesh data as child GameObjects of a "Geometry" GameObject.
        geometryGameObject = new GameObject("Geometry");
        geometryGameObject.transform.SetParent(gameObject.transform);
        AddMeshToObject(geometryGameObject, vertices, indices, false);

        // Save the mesh data we calculated for inspection in e.g. MeshLab.
        // This can take a few seconds, so disable it if you don't care.
        //CubeGenerator.SaveWavefrontOBJ("cubes.obj", vertices, indices);

        // Generate cube data for any coordinates where the x velocity is negative
        vertices = new List<CubeGenerator.Coordinate>();
        indices = new List<int>();
        BuildCubesNew(geometryData, velocityData, vertices, indices, true);
        // Add the mesh data as child GameObjects of a "Geometry" GameObject.
        spaceGameObject = new GameObject("Space");
        spaceGameObject.transform.SetParent(gameObject.transform);
        AddMeshToObject(spaceGameObject, vertices, indices, true);


        // Separate the velocity magnitudes into quarters to be used to define particle colors in ParticleHandler
        List<float> velocityScalars = velocityData.magnitudes.Select(m => m / velocityScale).ToList();
        // Ignore zero magnitudes
        velocityScalars.RemoveAll(m => m == 0);
        velocityScalars.Sort();
        bottomThreshold = velocityScalars[velocityScalars.Count / 4];
        midThreshold = velocityScalars[velocityScalars.Count / 2];
        topThreshold = velocityScalars[velocityScalars.Count / 2 + velocityScalars.Count / 4];

        String startTime = DateTime.Now.ToString("t", CultureInfo.GetCultureInfo("es-ES")).Replace(":","");
        Debug.Log("Beginning simulation! The simulation will restart " + numberOfRestarts + " times. The initial aggregation rate is: " + aggregationRates[0] + ".");
        for (int i = 0; i < numberOfParticles; ++i)
        {
            new ParticleObject(i, aggregationRates[0], particleAngularDrag, particleDrag, velocityScale, geometryPhysic, geometryData, startTime, disableCollidingSpawns);
        }
    }

    private void FixedUpdate()
    {
        // Restart the simulation if all Particles are destroyed
        if (numberOfRestarts > 0)
        {
            if (GameObject.FindWithTag("Particle") == null)
            {
                --numberOfRestarts;
                double rate = aggregationRates[aggregationRates.Length - numberOfRestarts];
                String startTime = DateTime.Now.ToString("t", CultureInfo.GetCultureInfo("es-ES")).Replace(":", "");
                Debug.Log("Respawning particles! There are " + numberOfRestarts + " restarts left. The aggregation rate is now: " + rate + ".");
                for (int i = 0; i < numberOfParticles; ++i)
                {
                    new ParticleObject(i, rate, particleAngularDrag, particleDrag, velocityScale, geometryPhysic, geometryData, startTime, disableCollidingSpawns);
                }
            }
        }
    }
    
    // Helper function to get the number of particles that have not aggregated
    internal static int getNumNonAggregates()
    {
        GameObject[] particles = GameObject.FindGameObjectsWithTag("Particle");
        List<GameObject> destroyedParticles = new List<GameObject>();
        int numAggregates = 0;
        foreach (GameObject particle in particles)
        {
            if (particle.GetComponent<ParticleHandler>().aggregated)
            {
                ++numAggregates;
            }
            else
            {
                // Don't want to subtract destroyed particles from the non-aggregate count
                if (particle.GetComponent<ParticleHandler>().destroyed)
                {
                    destroyedParticles.Add(particle);
                }
            }
        }
        return (particles.Length + destroyedParticles.Count) - numAggregates;
    }
}

