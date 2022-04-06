using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleHandler : MonoBehaviour
{
    System.Random rand = NativeSimTest.rand;

    static bool destroyOutOfBounds = true;
    private bool destroyed = false;
    FluidVelocityData velocityData = NativeSimTest.velocityData;
    // These four variables store the particle speed quartile thresholds
    float topThreshold = Mathf.Pow(NativeSimTest.topThreshold, 2);
    float midThreshold = Mathf.Pow(NativeSimTest.midThreshold, 2);
    float bottomThreshold = Mathf.Pow(NativeSimTest.bottomThreshold, 2);
    internal Vector3 initialPos;
    internal double aggregationRate;
    internal float velocityScale;
    internal long survivalTime;
    internal bool aggregated = false;
    internal String startTime;

    // Helper function to write aggregation times to a csv file
    void writeToFile(string particle1, string particle2, float p1SurvivalTime, float p2SurvivalTime, float p1SurvivalDist, float p2SurvivalDist)
    {
        String filename = "aggregations_" + startTime + ".csv";
        String text = particle1 + "," + (p1SurvivalTime * 0.02) + "," + p1SurvivalDist + "," + particle2 + "," + (p2SurvivalTime * 0.02) + "," + p2SurvivalDist;
        using (StreamWriter w = File.AppendText(filename))
        {
            w.WriteLine(text);
        }
    }

    // FixedUpdate is called every 0.02 seconds by default
    void FixedUpdate()
    {
        // Increment survival time as long as a particle is not aggregated
        if (aggregated == false)
        {
            ++survivalTime;
        }

        // Make sure the particles don't go out of bounds
        Rigidbody rBody = this.gameObject.GetComponent<Rigidbody>();
        // X
        if (rBody.position.x < 0)
        {
            if (destroyOutOfBounds)
            {
                Destroy(this.gameObject);
                destroyed = true;
            }
            else
            {
                rBody.position = new Vector3(200, rBody.position.y, rBody.position.z);
            }
        }
        if (rBody.position.x >= 200)
        {
            if (destroyOutOfBounds)
            {
                Destroy(this.gameObject);
                destroyed = true;
            }
            else
            {
                rBody.position = new Vector3(0, rBody.position.y, rBody.position.z);
            }
        }
        // Y
        if (rBody.position.y < 0)
        {
            if (destroyOutOfBounds)
            {
                Destroy(this.gameObject);
                destroyed = true;
            }
            else
            {
                rBody.position = new Vector3(rBody.position.x, 200, rBody.position.z);
            }
        }
        if (rBody.position.y >= 200)
        {
            if (destroyOutOfBounds)
            {
                Destroy(this.gameObject);
                destroyed = true;
            }
            else
            {
                rBody.position = new Vector3(rBody.position.x, 0, rBody.position.z);
            }
        }
        // Z
        if (rBody.position.z < 0)
        {
            if (destroyOutOfBounds)
            {
                Destroy(this.gameObject);
                destroyed = true;
            }
            else
            {
                rBody.position = new Vector3(rBody.position.x, rBody.position.y, 200);
            }
        }
        if (rBody.position.z >= 200)
        {
            if (destroyOutOfBounds)
            {
                Destroy(this.gameObject);
                destroyed = true;
            }
            else
            {
                rBody.position = new Vector3(rBody.position.x, rBody.position.y, 0);
            }
        }

        // Only update velocity and color if the Particle has not just been destroyed
        if (!destroyed)
        {
            // Set the particle's velocity based on their position
            rBody.velocity = velocityData.GetVelocityAt((int)Math.Floor(rBody.position.x), (int)Math.Floor(rBody.position.y), (int)Math.Floor(rBody.position.z)) / 0.005f;

            // Now we update the particle's color based on its speed.
            // This will be based on four speed thresholds
            // The colors will go red, yellow, green, blue, red being the slowest.

            // Bottom threshold: red
            if (rBody.velocity.sqrMagnitude < bottomThreshold)
            {
                this.gameObject.GetComponent<Renderer>().material.color = Color.red;
            }
            // Second threshold: yellow
            if (rBody.velocity.sqrMagnitude >= bottomThreshold && rBody.velocity.sqrMagnitude < midThreshold)
            {
                this.gameObject.GetComponent<Renderer>().material.color = Color.yellow;
            }
            // Third threshold: green
            if (rBody.velocity.sqrMagnitude >= midThreshold && rBody.velocity.sqrMagnitude < topThreshold)
            {
                this.gameObject.GetComponent<Renderer>().material.color = Color.green;
            }
            // Last threshold: blue
            if (rBody.velocity.sqrMagnitude >= topThreshold)
            {
                this.gameObject.GetComponent<Renderer>().material.color = Color.blue;
            }
        }
    }

    // Override to handle what happens immediately after collisions
    private void OnCollisionEnter(Collision collision)
    {
        // Uncomment to completely ignore Particle collision
        //Physics.IgnoreCollision(collision.collider, GetComponent<Collider>());

        // Particles will stick together based on their aggregation rate, used as a probability of joining.
        if (collision.gameObject.tag == "Particle") 
        {
            if (rand.NextDouble() < aggregationRate)
            {
                FixedJoint joint = gameObject.AddComponent<FixedJoint>();
                // Sets joint position to point of contact
                joint.anchor = collision.contacts[0].point;
                // Conects the joint to the other Particle
                joint.connectedBody = collision.contacts[0].otherCollider.transform.GetComponentInParent<Rigidbody>();
                // Stops Particles from continuing to collide and creating more joints
                joint.enableCollision = false;

                //Debug.Log(this.gameObject.name + " and " + collision.gameObject.name + " have collided and joined!");
                int nonAggregates = NativeSimTest.getNumNonAggregates();
                Debug.Log("There are currently " + nonAggregates + " particles that are not aggregated!");
                if (this.aggregated == false)
                {
                    float dist1 = Vector3.Distance(initialPos, collision.gameObject.GetComponent<Rigidbody>().position);
                    float dist2 = Vector3.Distance(initialPos, this.gameObject.GetComponent<Rigidbody>().position);
                    //Debug.Log(this.gameObject.name + " survival time was: " + (survivalTime * 0.02) + " seconds. Survival distance was: " + dist1 + ".");
                    writeToFile(this.gameObject.name, collision.gameObject.name, this.survivalTime, collision.gameObject.GetComponent<ParticleHandler>().survivalTime, dist1, dist2);
                }
                if (collision.gameObject.GetComponent<ParticleHandler>().aggregated == false)
                {
                    float dist = Vector3.Distance(initialPos, this.gameObject.GetComponent<Rigidbody>().position);
                    //Debug.Log(collision.gameObject.name + " survival time was: " + (collision.gameObject.GetComponent<ParticleHandler>().survivalTime * 0.02) + " seconds. Survival distance was: " + dist + ".");
                }
                
                this.aggregated = true;
                collision.gameObject.GetComponent<ParticleHandler>().aggregated = true;
            }
        }
    }
}
