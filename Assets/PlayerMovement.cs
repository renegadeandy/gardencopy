using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

public class PlayerMovement : MonoBehaviour
{
    public CharacterController controller;
    public Camera myEyes;
    public float speed = 12f;
    private Vector3 velocity;

    public float jumpHeight = 1f;

    public float gravity = -29.4f;
    public Transform groundCheck;
    //radius of sphere we will use to check collisions
    public float groundDistance = 0.4f;
    //control which objects this sphere will check for
    public LayerMask groundMask;

    //are we grounded or not?
    bool isGrounded;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if(isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        //left mouse click
        if (Input.GetMouseButtonUp(0))
        {
            Dig();
        }

        //Apply our directional movement z is forward / backward and x is left and right (strafe)
        Vector3 move = (transform.right * x) + (transform.forward * z);
        controller.Move(move * speed * Time.deltaTime);

        //physics calculation to determine how much velocity we need to jump a certain height
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        //Apply our gravity
        velocity.y += gravity * Time.deltaTime;
        //gravity still feels slow.
       //Debug.Log("Velocity is:" + velocity.ToString() + "Gravity deduction is:"+ gravity * Time.deltaTime);
        controller.Move(velocity * Time.deltaTime);
    }


    void Dig()
    {
        Ray ray = myEyes.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0f));
        RaycastHit hit;

        if(Physics.Raycast(ray,out hit, 50f))
        {
            //remove a 'spades' worth of voxels, if pointing at the terrain, and shove them somewhere random nearby.
            Debug.Log(hit.point);
        }
    }
}
