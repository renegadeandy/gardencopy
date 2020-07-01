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

    public float gravity = -9.81f;
    public Transform groundCheck;
    //radius of sphere we will use to check collisions
    public float groundDistance = 0.4f;
    //control which objects this sphere will check for
    public LayerMask groundMask;

    //are we grounded or not?
    bool isGrounded;

 
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

        //Apply our directional movement z is forward / backward and x is left and right (strafe)
       Vector3 move = transform.right * x + transform.forward * z;
        // something wrong in here, causes big circles to on x axis
       controller.Move(move * speed * Time.deltaTime);


        //left mouse click
        //  if (Input.GetMouseButtonUp(0))
        //  {
        //     Dig();
        // }

        //Gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        //physics calculation to determine how much velocity we need to jump a certain height
        if (Input.GetButtonDown("Jump"))
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            controller.Move(velocity * Time.deltaTime);
        }

      
    }

    Vector3 p0, p1, p2;
    Boolean drawGizmosReady = false;
    void Dig()
    {
        Ray ray = new Ray(myEyes.transform.position, myEyes.transform.forward);
        Debug.DrawRay(ray.origin, ray.direction *1000f, Color.cyan, 20f) ;
        RaycastHit hit;

        int layerMaskGround = 1 << LayerMask.NameToLayer("Ground");
         if(Physics.Raycast(ray, out hit,1000f, layerMaskGround))
        {

            //remove a 'spades' worth of voxels, if pointing at the terrain, and shove them somewhere random nearby.
            Debug.Log("Tag is:" + hit.collider.gameObject.tag + " and object name is:" + hit.collider.gameObject.name + "And layer is:" + hit.collider.gameObject.layer);


            MeshCollider meshCollider = hit.collider as MeshCollider;
            if (meshCollider == null || meshCollider.sharedMesh == null)
                return;

            
            Mesh mesh = meshCollider.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            drawGizmosReady = false;
          
            p0 = vertices[triangles[hit.triangleIndex * 3  + 0]];
            p1 = vertices[triangles[hit.triangleIndex * 3 + 1]];
            p2 = vertices[triangles[hit.triangleIndex * 3 + 2]];
            Transform hitTransform = hit.collider.transform;
           
            p0 = hitTransform.TransformPoint(p0);
            p1 = hitTransform.TransformPoint(p1);
            p2 = hitTransform.TransformPoint(p2);
            drawGizmosReady = true;
           // Debug.DrawLine(p0, p1,Color.red,1000f);
           // Debug.DrawLine(p1, p2, Color.red, 1000f);
           // Debug.DrawLine(p2, p0, Color.red, 1000f);


        }
        else{
            Debug.Log("Hit nothing");
            drawGizmosReady = false;
        }
    }


    void OnDrawGizmos()
    {
        // Draw a yellow sphere at the transform's position

        if (drawGizmosReady)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(p0, p1);
            Gizmos.DrawLine(p1, p2);
            Gizmos.DrawLine(p2, p0);
            Gizmos.DrawSphere(transform.position, 1);
        }
    }
}
