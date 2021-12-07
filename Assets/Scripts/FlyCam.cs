using UnityEngine;
using System.Collections;

public class FlyCam : MonoBehaviour
{

    /*
    Writen by Windexglow 11-13-10.  Use it, edit it, steal it I don't care.  
    Converted to C# 27-02-13 - no credit wanted.
    Simple flycam I made, since I couldn't find any others made public.  
    Made simple to use (drag and drop, done) for regular keyboard layout  
    
    Edited 10/05/2021 By Zachary Singleton to map space to +y and left shift to -y.
    Also set speed and sensitivity to be public for easy access in the Unity Editor.
    Left Control will now accelerate and Tab will now lock y position.
    */


    public float mainSpeed = 50.0f; //regular speed
    float ctrlAdd = 100.0f; //multiplied by how long ctrl is held.  Basically running
    float maxctrl = 1000.0f; //Maximum speed when holding ctrl
    public float sensitivity = 0.20f; //How sensitive it with mouse
    private Vector3 lastMouse = new Vector3(255, 255, 255); //kind of in the middle of the screen, rather than at the top (play)
    private float totalRun = 1.0f;

    void Update()
    {
        lastMouse = Input.mousePosition - lastMouse;
        lastMouse = new Vector3(-lastMouse.y * sensitivity, lastMouse.x * sensitivity, 0);
        lastMouse = new Vector3(transform.eulerAngles.x + lastMouse.x, transform.eulerAngles.y + lastMouse.y, 0);
        transform.eulerAngles = lastMouse;
        lastMouse = Input.mousePosition;
        //Mouse  camera angle done.  

        //Keyboard commands
        Vector3 p = GetBaseInput();
        if (p.sqrMagnitude > 0)
        { // only move while a direction key is pressed
            if (Input.GetKey(KeyCode.LeftControl))
            {
                totalRun += Time.deltaTime;
                p = p * totalRun * ctrlAdd;
                p.x = Mathf.Clamp(p.x, -maxctrl, maxctrl);
                p.y = Mathf.Clamp(p.y, -maxctrl, maxctrl);
                p.z = Mathf.Clamp(p.z, -maxctrl, maxctrl);
            }
            else
            {
                totalRun = Mathf.Clamp(totalRun * 0.5f, 1f, 1000f);
                p = p * mainSpeed;
            }

            p = p * Time.deltaTime;
            Vector3 newPosition = transform.position;
            if (Input.GetKey(KeyCode.Tab))
            { //If player wants to move on X and Z axis only
                transform.Translate(p);
                newPosition.x = transform.position.x;
                newPosition.z = transform.position.z;
                transform.position = newPosition;
            }
            else
            {
                transform.Translate(p);
            }
        }
    }

    private Vector3 GetBaseInput()
    { //returns the basic values, if it's 0 than it's not active.
        Vector3 p_Velocity = new Vector3();
        if (Input.GetKey(KeyCode.W))
        {
            p_Velocity += new Vector3(0, 0, 1);
        }
        if (Input.GetKey(KeyCode.S))
        {
            p_Velocity += new Vector3(0, 0, -1);
        }
        if (Input.GetKey(KeyCode.A))
        {
            p_Velocity += new Vector3(-1, 0, 0);
        }
        if (Input.GetKey(KeyCode.D))
        {
            p_Velocity += new Vector3(1, 0, 0);
        }
        if (Input.GetKey(KeyCode.Space))
        {
            p_Velocity += new Vector3(0, 1, 0);
        }
        if (Input.GetKey(KeyCode.LeftShift))
        {
            p_Velocity += new Vector3(0, -1, 0);
        }
        return p_Velocity;
    }
}