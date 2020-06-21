using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using UnityEngine;

public class MouseLook : MonoBehaviour
{
    public float mouseSensitivity = 300f;
    public Camera myEyes;
    public Transform playerBody;

    private float xRotation = 0f;
    // Start is called before the first frame update = test update
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    // Update is called once per frame
    void Update()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        //Handles looking right and left on x axis
        playerBody.Rotate(Vector3.up * mouseX);
       
        //Handles looking up and down, clamps to 90 degrees up and down looking
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90, 90);
        transform.localRotation = Quaternion.Euler(xRotation, 0, 0);
        
    }
}
