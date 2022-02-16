using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleHandler : MonoBehaviour
{
    static bool destroyOutOfBounds = true;
    private bool destroyed = false;
    FluidVelocityData velocityData = NativeSimTest.velocityData;

    // Update is called once per frame
    void FixedUpdate()
    {
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
            rBody.velocity = velocityData.GetVelocityAt((int)Math.Floor(rBody.position.x), (int)Math.Floor(rBody.position.y), (int)Math.Floor(rBody.position.z));

            // Now we update the particle's color based on its speed.
            // This will be based on four speed thresholds
            // The colors will go red, yellow, green, blue, red being the slowest.

            // Bottom threshold: red
            if (rBody.velocity.sqrMagnitude <= 75f)
            {
                this.gameObject.GetComponent<Renderer>().material.color = Color.red;
            }
            // Second threshold: yellow
            if (rBody.velocity.sqrMagnitude > 75f && rBody.velocity.sqrMagnitude <= 115f)
            {
                this.gameObject.GetComponent<Renderer>().material.color = Color.yellow;
            }
            // Third threshold: green
            if (rBody.velocity.sqrMagnitude > 115f && rBody.velocity.sqrMagnitude <= 150f)
            {
                this.gameObject.GetComponent<Renderer>().material.color = Color.green;
            }
            // Last threshold: blue
            if (rBody.velocity.sqrMagnitude > 150f)
            {
                this.gameObject.GetComponent<Renderer>().material.color = Color.blue;
            }
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
