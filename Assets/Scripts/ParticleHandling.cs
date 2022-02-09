using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleHandling : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
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
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        // Make sure the particles don't go out of bounds
        Rigidbody rBody = this.GetComponent<Rigidbody>();
        // X
        if (rBody.position.x < 0)
        {
            if (destroyOutOfBounds)
            {
                Destroy(rBody);
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
                Destroy(rBody);
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
                Destroy(rBody);
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
                Destroy(rBody);
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
                Destroy(rBody);
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
                Destroy(rBody);
            }
            else
            {
                rBody.position = new Vector3(rBody.position.x, rBody.position.y, 0);
            }
        }

        // Set the particle's velocity based on their position
        rBody.velocity = velocityData.GetVelocityAt((int)Math.Floor(rBody.position.x), (int)Math.Floor(rBody.position.y), (int)Math.Floor(rBody.position.z));

        // Now we update the particle's color based on its speed.
        // This will be based on four speed thresholds
        // The colors will go red, yellow, green, blue, red being the slowest.

        // Bottom threshold: red
        if (rBody.velocity.sqrMagnitude <= 5f)
        {
            this.GetComponent<Renderer>().material.color = Color.red;
        }
        // Second threshold: yellow
        if (rBody.velocity.sqrMagnitude > 5f && rBody.velocity.sqrMagnitude <= 10f)
        {
            this.GetComponent<Renderer>().material.color = Color.yellow;
        }
        // Third threshold: green
        if (rBody.velocity.sqrMagnitude > 10f && rBody.velocity.sqrMagnitude <= 15f)
        {
            this.GetComponent<Renderer>().material.color = Color.green;
        }
        // Third threshold: green
        if (rBody.velocity.sqrMagnitude > 15f && rBody.velocity.sqrMagnitude <= 20f)
        {
            this.GetComponent<Renderer>().material.color = Color.blue;
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
