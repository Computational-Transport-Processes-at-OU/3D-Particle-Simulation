using System.Collections.Generic;

/*
The 3D geometry of the porous material is generated using stacked cubes on a
regular lattice.

Each cube is essentially the same, so the CubeGenerator contains some static
routines to assist with cube generation; in particular, there are routines to
help with triangulation of the cube faces to create 3D meshes suitable for use
in Unity scenes (i.e., sets of vertices and sets of edges connecting three
vertices into a triangle).

Consider two cubes, with one stacked directly on top of the other. From outside
the structure, we cannot see the lower face of the top cube, or the top face of
the lower cube. We can therefore simplify the geometry in the scene by detecting
adjacent cubes, and removing the "connecting" faces. Therefore, CubeGenerator
methods allow the generation of specific faces of a cube as well as generating
a complete cube.
*/

public static class CubeGenerator
{
    // We assume cubes on a regular lattice, so use integer lattice coords.
    // It's useful to be able to add Coordinate items, so we also define an
    // addition operator.
    public struct Coordinate
    {
        public int x, y, z;

        public static Coordinate operator + (Coordinate a, Coordinate b)
        {
            return new Coordinate { x= a.x + b.x, y= a.y + b.y, z= a.z + b.z };
        }
    };

    // Relative vertex coords for a unit cube, local origin: 0,0,0
    static Coordinate[] verts = {
            // Lower vertices (plane on x,z at y = 0)
            new Coordinate {x=0, y=0, z=0},
            new Coordinate {x=0, y=0, z=1},
            new Coordinate {x=1, y=0, z=1},
            new Coordinate {x=1, y=0, z=0 },
            // Upper vertices (plane on x,z at y = 1)
            new Coordinate {x=0, y=1, z=0},
            new Coordinate {x=0, y=1, z=1},
            new Coordinate {x=1, y=1, z=1},
            new Coordinate {x=1, y=1, z=0},
        };

    // Indices into the verts[] array to form a "quad" (i.e. 4 vertices) for
    // each cube face. Vertices are listed in an order that ensures the
    // resultant face normals point "out" from centre of cube, and therefore
    // the faces are visible from "outside" the cube.
    static int[] faces = {
        // -x
        0, 1, 5, 4,
        // +x
        7, 6, 2, 3,
        // -y
        3, 2, 1, 0,
        // +y
        4, 5, 6, 7,
        // -z
        4, 7, 3, 0,
        // +z
        1, 2, 6, 5,
    };

    // We assume a cube face is defined by a set of 4 vertices, from which we
    // can define two triangles with vertex indices [0,1,2] & [0,2,3].

    // axisID:      x=0, y=1, z=2
    // directionID: 0="minus", 1="plus"
    // lattice_coord : location of the cube on the integer lattice
    
    // New vertex / vertex index (edge) data APPENDED to vertices[], indices[]
    public static void GenerateFace(int axisID, int directionID, Coordinate lattice_coord, List<Coordinate> vertices, List<int> indices)
    {
        int idx = ((axisID * 2) + directionID) * 4;

        var i = faces[idx + 0];
        var j = faces[idx + 1];
        var k = faces[idx + 2];
        var l = faces[idx + 3];

        int offset = vertices.Count; // start offset of vertices that describe the new face

        // Add the four vertices describing the new cube face to the vertex data
        vertices.Add(lattice_coord + verts[i]);
        vertices.Add(lattice_coord + verts[j]);
        vertices.Add(lattice_coord + verts[k]);
        vertices.Add(lattice_coord + verts[l]);

        // Add the new vertex indices that describe the first triangle
        indices.Add(offset + 0);
        indices.Add(offset + 1);
        indices.Add(offset + 2);

        // Add the new vertex indices that describe the second triangle
        indices.Add(offset + 0);
        indices.Add(offset + 2);
        indices.Add(offset + 3);
    }
    public static void GenerateFace(int axisID, int directionID, List<Coordinate> vertices, List<int> indices)
    {
        var zero = new Coordinate { x = 0, y = 0, z = 0 };
        GenerateFace(axisID, directionID, zero, vertices, indices);
    }

    public static void GetCube(Coordinate lattice_coord, List<Coordinate> vertices, List<int> indices)
    {
        int[] axes = { 0, 1, 2 };
        int[] dirs = { 0, 1 };

        foreach (var axisID in axes)
        {
            foreach (var dirID in dirs)
            {
                GenerateFace(axisID, dirID, lattice_coord, vertices, indices);
            }
        }
    }
    public static void GetCube(List<Coordinate> vertices, List<int> indices)
    {
        var zero = new Coordinate { x = 0, y = 0, z = 0 };
        GetCube(zero, vertices, indices);
    }

    // Attempt to collapse shared vertices into a single vertex entry. Can
    // reduce data size, but is also likely to cause problems with rendering
    // the light shading on triangles.
    // It's also a pretty slow implementation; I've left it here as a reminder
    // that such approaches are possible if needed.
    /*
    public static void Simplify(List<Coordinate> vertices, List<int> indices)
    {
        var vNew = new List<Coordinate>();
        var iNew = new List<int>();

        var d = new Dictionary<Coordinate, int>();

        UnityEngine.Debug.LogWarning($"{vertices.Count} {indices.Count}");

        for (int i_ = 0; i_ < indices.Count; i_++)
        {
            var v = vertices[indices[i_]];
            int i;

            if (!d.TryGetValue(v, out i))
            {
                i = vNew.Count;
                d[v] = i;
                vNew.Add(v);
            }

            iNew.Add(i);
        }

        UnityEngine.Debug.LogWarning($"{vNew.Count} {iNew.Count}");

        vertices.Clear();
        indices.Clear();

        foreach (var x in vNew) vertices.Add(x);
        foreach (var x in iNew) indices.Add(x);
    }
    */

    // Wavefront's ".obj" file format is simple and widely supported; it's
    // useful to save our mesh data as ".obj" for loading into other tools
    // such as MeshLab etc to check we're generating the correct data.
    // https://en.wikipedia.org/wiki/Wavefront_.obj_file

    public static void SaveWavefrontOBJ(string filePath, List<Coordinate> vertices, List<int> indices)
    {
        using (var f = new System.IO.StreamWriter(filePath))
        {
            // Write vertices
            foreach (var v in vertices)
            {
                f.WriteLine($"v {v.x} {v.y} {v.z}");
            }

            f.WriteLine("");

            // Write the UNIT BASED indices of vertices that represent faces.
            // Here we assume "faces" are triangles, so 3 indices per face.
            for (int idx = 0; idx < indices.Count; idx += 3)
            {
                var i = indices[idx + 0];
                var j = indices[idx + 1];
                var k = indices[idx + 2];
                f.WriteLine($"f {i+1} {j+1} {k+1}");
            }
        }
    }
}
