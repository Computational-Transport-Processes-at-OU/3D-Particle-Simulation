using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleObject : MonoBehaviour
{
    public bool destroyOutOfBounds = true;
    System.Random rand = new System.Random();

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
        particle.transform.SetParent(gameObject.transform);
        particle.GetComponent<SphereCollider>().sharedMaterial = geometryPhysic;
        Rigidbody rbody = particle.AddComponent<Rigidbody>();
        rbody.position = GetRandomPosition(geometryData);
        rbody.useGravity = false;
        // Random x velocity between 1.0 and 5.0
        rbody.velocity = new Vector3((float)(rand.NextDouble() * (5 - 1) + 1), 0f, 0f);
    }

    // Start is called before the first frame update
    void Start()
    {
        for (int i = 0; i < NUM_PARTICLES; ++i)
        {
            oldPositions.Add(new Vector3(0, 1, 1));
            velocityVectors.Add(new Vector3(5, 5, 5));
            particleIDs[i] = i;
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        // Make sure the particles don't go out of bounds
        Rigidbody rBody = this.GetComponent<Rigidbody>();
        // X
        if (rBody.position.x < 0) {
            if (destroyOutOfBounds) {
                Destroy(rBody);
            }
            else {
                rBody.position = new Vector3(200, rBody.position.y, rBody.position.z);
            }
        }
        if (rBody.position.x >= 200) {
            if (destroyOutOfBounds) {
                Destroy(rBody);
            }
            else {
                rBody.position = new Vector3(0, rBody.position.y, rBody.position.z);
            }
        }
        // Y
        if (rBody.position.y < 0) {
            if (destroyOutOfBounds) {
                Destroy(rBody);
            }
            else {
                rBody.position = new Vector3(rBody.position.x, 200, rBody.position.z);
            }
        }
        if (rBody.position.y >= 200) {
            if (destroyOutOfBounds) {
                Destroy(rBody);
            }
            else {
                rBody.position = new Vector3(rBody.position.x, 0, rBody.position.z);
            }
        }
        // Z
        if (rBody.position.z < 0) {
            if (destroyOutOfBounds) {
                Destroy(rBody);
            }
            else {
                rBody.position = new Vector3(rBody.position.x, rBody.position.y, 200);
            }
        }
        if (rBody.position.z >= 200) {
            if (destroyOutOfBounds) {
                Destroy(rBody);
            }
            else {
                rBody.position = new Vector3(rBody.position.x, rBody.position.y, 0);
            }
        }

        // Set the particles' velocity based on their position
        Rigidbody rBody = this.GetComponent<Rigidbody>();
        rBody.velocity = velocityData.GetVelocityAt((int)Math.Floor(rBody.position.x), (int)Math.Floor(rBody.position.y), (int)Math.Floor(rBody.position.z));

        var dt = Time.deltaTime;
        // Calculate velocity vector & scalar
        velocityVectors[i] = particleGameObjects[i].GetComponent<Rigidbody>().velocity;
        velocityScalars[i] = Math.Sqrt(velocityVectors[i].x * velocityVectors[i].x + velocityVectors[i].y * velocityVectors[i].y + velocityVectors[i].z * velocityVectors[i].z) / dt;

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

    // Override to handle what happens immediately after collisions
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "Particle")
        {
            //Physics.IgnoreCollision(collision.collider, GetComponent<Collider>());
            // creates joint
            FixedJoint joint = gameObject.AddComponent<FixedJoint>();
            // sets joint position to point of contact
            joint.anchor = collision.contacts[0].point;
            // conects the joint to the other object
            joint.connectedBody = collision.contacts[0].otherCollider.transform.GetComponentInParent<Rigidbody>();
            // Stops objects from continuing to collide and creating more joints
            joint.enableCollision = false;
        }
    }
}
