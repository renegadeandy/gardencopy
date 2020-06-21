using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using UnityEngine;
using UnityEngineInternal;
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
        velocity += Vector3.up * gravity * Time.deltaTime;
        //gravity still feels slow.
       //Debug.Log("Velocity is:" + velocity.ToString() + "Gravity deduction is:"+ gravity * Time.deltaTime);
        controller.Move(velocity * Time.deltaTime);
    }


    void Dig()
    {
        Ray ray = new Ray(myEyes.transform.position, myEyes.transform.forward);
        Debug.DrawRay(ray.origin, ray.direction * 50f, Color.cyan, 2000f) ;
        RaycastHit hit;


        // if(Physics.Raycast(ray, out hit,1000f, LayerMask.NameToLayer("Ground"))) -- this dos not work - debug line shows ray is hitting layer 8 == ground but with the mask, nothing is hit?
        if (Physics.Raycast(ray,out hit, 50f))
        {

            //remove a 'spades' worth of voxels, if pointing at the terrain, and shove them somewhere random nearby.
            Debug.Log("Tag is:" + hit.collider.gameObject.tag + " and object name is:" + hit.collider.gameObject.name + "And layer is:" + hit.collider.gameObject.layer);


            MeshCollider meshCollider = hit.collider as MeshCollider;
            if (meshCollider == null || meshCollider.sharedMesh == null)
                return;

            
            Mesh mesh = meshCollider.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Vector3 p0 = vertices[triangles[hit.triangleIndex * 3 + 0]];
            Vector3 p1 = vertices[triangles[hit.triangleIndex * 3 + 1]];
            Vector3 p2 = vertices[triangles[hit.triangleIndex * 3 + 2]];
            Transform hitTransform = hit.collider.transform;
           
            p0 = hitTransform.TransformPoint(p0);
            p1 = hitTransform.TransformPoint(p1);
            p2 = hitTransform.TransformPoint(p2);
            Debug.DrawLine(p0, p1,Color.red,1000f);
            Debug.DrawLine(p1, p2, Color.red, 1000f);
            Debug.DrawLine(p2, p0, Color.red, 1000f);


        }
        else{
            Debug.Log("Hit nothing");
        }
    }
}
