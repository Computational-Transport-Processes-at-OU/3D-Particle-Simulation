using System;
using System.Collections;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;

/*
Data handling; read system geometry and particle trajectory data from files.

GeometryData:
-------------

Assumes a list of (zero-based) integer x,y,z coordinates and the associated
intensity value at that location.

TrajectoryData:
---------------

Assumes each particle has an integer identifier, and a trajectory file is a
simple list of "[id: int] [x: float] [y: float] [z: float]" lines.
*/

//
// Simple data structure for the files output by the Matlab script.
//
public class GeometryData
{
    // i, j, k : integer lattice coordinates, store their maximum values.
    public int iMax = 0, jMax = 0, kMax = 0;

    // Values from file; stored as single flat (i.e. 1-D) array for simplicity.
    public List<float> intensity = new List<float>();
    public List<float> intensity2 = new List<float>();

    // Convert an i,j,k lattice coordinate into an index into the flat lists
    public int GetIndex( int i, int j, int k )
    {
        // assume i changes fastest.
        return i + (j * iMax) + (k * iMax * jMax);
    }

    // Retrieve intensity/intensity2 values gives lattice coordinates
    public float GetIntensityAt( int i, int j, int k )
    {
        var index = GetIndex(i,j,k);
        return intensity[index];
    }
    public float GetIntensity2At(int i, int j, int k)
    {
        var index = GetIndex(i, j, k);
        return intensity2[index];
    }

    // We expect:
    // 1st line like "VARIABLES = "X [lu]", "Y [lu]", "Z [lu]", "Intensity", "Intensity2""
    // 2nd line like "ZONE, i =   50, j =   100, k =   100, F=POINT, STRANDID=0" "
    // Subsequent lines have 4 numerical values, like "i j k intensity intensity2"
    // Remember: C# has ZERO-BASED indexing, so the first element of a list/array is
    // at index ZERO rather than index ONE!
    // Returns: null if everything worked, otherwise an error string describing the problem.
    
    public string LoadFromFile(string filePath)
    {
        var lines = System.IO.File.ReadAllLines(filePath, System.Text.Encoding.UTF8);

        // Determine maximum lattice indices for i, j, k.
        {
            var line = lines[1].Replace(',', ' ');
            var tokens = MiscUtil.Tokenize(line, ' ', '"');
            try
            {
                var iTok = tokens[2];
                var jTok = tokens[6];
                var kTok = tokens[4];

                iMax = int.Parse(iTok);
                jMax = int.Parse(jTok);
                kMax = int.Parse(kTok);
            }
            catch (System.Exception e)
            {
                return string.Format("Unable to extract max lattice values from '{0}' : {1}", line, e);
            }
        }

        // Ensure intensity/intensity2 lists are 1) of sufficient size, and 2) are zero'd out.
        {
            intensity.Clear();
            intensity2.Clear();
            for (int index = 0; index < (iMax * jMax * kMax); index++)
            {
                intensity.Add(0);
                intensity2.Add(0);
            }
        }

        // Load the volumetric lattice data
        for (int line_no = 2; line_no < lines.Length; line_no++)
        {
            var line = lines[line_no];
            var tokens = MiscUtil.Tokenize(line, ' ', '"');
            if (tokens.Count < 5)
            {
                return $"Error on line {line_no} : too few tokens ('{line}')";
            }

            try
            {
                // Convert from unit-based indices in the file to zero-based indices
                var i = int.Parse(tokens[0])-1;
                var j = int.Parse(tokens[2])-1;
                var k = int.Parse(tokens[1])-1;

                // Sanity check of lattice coordinates vs what we expect
                if( (i<0) || (i>=iMax) )
                {
                    return $"Error on line {line_no} : bad i value {i}, must be 0 <= i < {iMax}";
                }
                if ((j<0) || (j>=jMax))
                {
                    return $"Error on line {line_no} : bad j value {j}, must be 0 <= j < {jMax}";
                }
                if ((k<0) || (k>=kMax))
                {
                    return $"Error on line {line_no} : bad k value {k}, must be 0 <= k < {kMax}";
                }

                // Density values
                var val1 = float.Parse(tokens[3]);
                var val2 = float.Parse(tokens[4]);

                // Add density values to lists using the lattice coordinates
                var index = GetIndex(i, j, k);
                intensity[index] = val1;
                intensity2[index] = val2;
            }
            catch ( System.Exception e )
            {
                return $"Unable to extract data from line {line_no} : '{line}' : {e}";
            }
        }

        // If we get here, no problems were encountered - so return null!
        return null;
    }
}

// Simple data structure for trajectory files.
public class TrajectoryData
{
    // Map a particle id onto a list of x,y,z coords for that particle
    public Dictionary<int, List<float>> idToTrajectory = new Dictionary<int, List<float>>();

    // We expect lines in the format:
    // <id:integer> <x:float> <y:float> <z:float>
    // Returns: null if everything worked, otherwise an error string describing the problem.
    public string LoadFromFile(string filePath)
    {
        var lines = System.IO.File.ReadAllLines(filePath, System.Text.Encoding.UTF8);

        //
        // Load the particle trajectory data
        //
        for (int line_no = 0; line_no < lines.Length; line_no++)
        {
            var line = lines[line_no];
            var tokens = MiscUtil.Tokenize(line, ' ', '"');
            if (tokens.Count < 4)
            {
                return $"Error on line {line_no} : too few tokens ('{line}')";
            }

            try
            {
                var id = int.Parse(tokens[0]);
                var x  = float.Parse(tokens[1]);
                var y  = float.Parse(tokens[3]);
                var z  = float.Parse(tokens[2]);

                if (!idToTrajectory.ContainsKey(id))
                {
                    var newList = new List<float>();
                    idToTrajectory[id] = newList;
                }

                var l = idToTrajectory[id];
                l.Add(x);
                l.Add(y);
                l.Add(z);
            }
            catch (System.Exception e)
            {
                return $"Unable to extract data from line {line_no} : '{line}' : {e}";
            }
        }

        // If we get here, no problems were encountered - so return null!
        return null;
    }

    public List<float> GetTrajectoryFromParticleID( int id )
    {
        if (!idToTrajectory.ContainsKey(id)) return null;
        return idToTrajectory[id];
    }
}

// Simple data structure for fluid velocity files.
public class FluidVelocityData
{
    // x, y, z : integer lattice coordinates, store their maximum values.
    public int xMax = 201, yMax = 201, zMax = 201;

    // Values from file; stored as single flat (i.e. 1-D) array of lists for simplicity.
    public List<Vector3> velocity = new List<Vector3>();

    public List<float> magnitudes = new List<float>();

    // Convert an x,y,z lattice coordinate into an index into the flat lists
    public int GetIndex(int x, int y, int z)
    {
        // assume x changes fastest.
        return x + (y * xMax) + (z * xMax * yMax);
    }

    // Retrieve velocity values given lattice coordinates
    public Vector3 GetVelocityAt(int x, int y, int z)
    {
        var index = GetIndex(x, y, z);
        return new Vector3(velocity[index].x, velocity[index].y, velocity[index].z);
    }

    // We expect lines in the format:
    // <x:integer> <y:integer> <z:integer> <x_velocity:float> <y_velocity:float> <z_velocity:float> <velocity_magnitude:float>
    // Returns: null if everything worked, otherwise an error string describing the problem.
    public string LoadFromFile(string filePath)
    {
        var lines = System.IO.File.ReadAllLines(filePath, System.Text.Encoding.UTF8);

        // Ensure velocity list is zero'd out.
        velocity.Clear();
        for (int index = 0; index < (xMax * yMax * zMax); index++)
        {
            velocity.Add(new Vector3(0f, 0f, 0f));
        }

        // Load the fluid velocity data
        for (int line_no = 0; line_no < lines.Length; line_no++)
        {
            var line = lines[line_no];
            var tokens = MiscUtil.Tokenize(line, ' ', '"');
            if (tokens.Count < 7)
            {
                return $"Error on line {line_no} : too few tokens ('{line}')";
            }

            try
            {
                // Position values
                var x = int.Parse(tokens[0]) - 1;
                var y = int.Parse(tokens[2]) - 1;
                var z = int.Parse(tokens[1]) - 1;

                // Velocity + magnitude values
                float x_velocity = (float)Double.Parse(tokens[3], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture);
                float y_velocity = (float)Double.Parse(tokens[5], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture);
                float z_velocity = (float)Double.Parse(tokens[4], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture);
                float magnitude = (float)Double.Parse(tokens[6], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture);

                // Add velocity values to lists using the lattice coordinates
                var index = GetIndex(x, y, z);
                velocity[index] = new Vector3(x_velocity, y_velocity, z_velocity);
                magnitudes.Add(magnitude);
            }
            catch (System.Exception e)
            {
                return $"Unable to extract data from line {line_no} : '{line}' : {e}";
            }
        }

        // If we get here, no problems were encountered - so return null!
        return null;
    }
}
