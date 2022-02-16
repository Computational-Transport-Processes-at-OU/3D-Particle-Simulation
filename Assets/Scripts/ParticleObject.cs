using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleObject
{
    System.Random rand = NativeSimTest.rand;

    /*
     * This function will randomly generate a Vector3 position on the x = -30 plane, 
     * as long as there are no solid cubes at x = 1.
     */
    private Vector3 GetRandomPosition(GeometryData data)
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

    // Constructor. Creates a new Sphere GameObject
    public ParticleObject(int index, PhysicMaterial geometryPhysic, GeometryData geometryData)
    {
        GameObject particle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        particle.name = "Particle " + index;
        particle.tag = "Particle";
        particle.GetComponent<SphereCollider>().sharedMaterial = geometryPhysic;
        Rigidbody rbody = particle.AddComponent<Rigidbody>();
        rbody.position = GetRandomPosition(geometryData);
        rbody.useGravity = false;
        // Random x velocity between 1.0 and 5.0
        rbody.velocity = new Vector3((float)(rand.NextDouble() * (5 - 1) + 1), 0f, 0f);
        particle.AddComponent<ParticleHandler>();
    }
}
