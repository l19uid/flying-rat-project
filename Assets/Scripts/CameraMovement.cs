using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    public float movementSpeed;
    public float sensitivity;
    public float zoomSpeed;

    private float fov;
    private Vector3 movement;
    private float xRotation = 0f;
    private float yRotation = 0f; 
    
    private Camera cam;
    
    void Start()
    {
        cam = GetComponent<Camera>();
        fov = cam.fieldOfView;
        Cursor.lockState = CursorLockMode.Locked;
    }
    
    void Update()
    {
        movement = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
        if(Input.GetKey(KeyCode.Space))
            movement.y = 1;
        if(Input.GetKey(KeyCode.LeftControl))
            movement.y = -1;
        
        if(Input.GetKey(KeyCode.LeftShift))
            transform.Translate(movement * (movementSpeed * 2 * Time.deltaTime));
        else
            transform.Translate(movement * (movementSpeed * Time.deltaTime));
        
        // Rotate the camera
        // Get mouse input
        float mouseX = Input.GetAxis("Mouse X") * sensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity * Time.deltaTime;

        // Calculate vertical rotation and clamp it
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        
        // Calculate horizontal rotation
        yRotation += mouseX;
        yRotation = yRotation % 360;

        // Apply rotation to the camera and player body
        transform.localRotation = Quaternion.Euler(xRotation, yRotation, 0f);
        transform.Rotate(Vector3.up * mouseX);
        
        fov -= Input.GetAxis("Mouse ScrollWheel") * zoomSpeed;
        fov = Mathf.Clamp(fov, 20, 100);
        cam.fieldOfView = fov;
    }

    public bool IsMoving()
    {
        return movement != Vector3.zero;
    }
}
