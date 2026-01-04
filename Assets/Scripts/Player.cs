using System;
using System.Collections.Generic;
using System.Numerics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

// Usually I would seperate this into multiple scripts but for simplicity im keeping it all here
// Just because this is a small project
public class Player : MonoBehaviour
{
    // Movement
    public float movementSpeed = 5f;
    public float jumpStrength = 5f;
    public float mouseSensitivity = 90f;
    private Rigidbody _rb;
    private Vector3 _input;
    private Vector3 _movement;
    private Vector3 _mouseInput;
    private Vector3 _mouseMovement;
    bool _isGrounded = true;
    public LayerMask groundLayer;
    
    // Camera
    public GameObject cameraObject;
    
    // Indicators
    public GameObject placeIndicator;
    public GameObject breakIndicator;
    public Material breakMaterial;
    public Sprite[] breakSprites;
    public float timeMining = 0f;
    
    // Blocks and Hotbar
    private int _selectedBlockIndex = 1; //1->GRASS,2->STONE,3->SNOW
    private int[] _blockAmounts = new []{0,0,0}; 
    public TextMeshProUGUI[] blockAmountText;
    public Image[] blockAmountBackground;
    
    private World.BlockType _lookingAtBlockType = World.BlockType.GRASS;
    private Vector3 _lookingAtBlockPos = Vector3.zero;
    
    public static Player Instance;
    
    private bool _started = false;
    
    private List<Vector3> _previousPositions = new List<Vector3>();

    private void Start()
    {
        Instance = this;
        Cursor.lockState = CursorLockMode.Locked;
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
    }

    private void Update()
    {
        // Initial spawn above ground
        if (!_started)
        {
            // Raycast under the player to see if there is ground and if yes, set the player above it
            RaycastHit hit;
            if (Physics.Raycast(transform.position, Vector3.down, out hit, Mathf.Infinity, groundLayer, QueryTriggerInteraction.Ignore))
            {
                _rb.useGravity = true;
                transform.position = hit.point + Vector3.up * 2f;
                _started = true;
            }
            return;
        }
        
        // Check if grounded
        _isGrounded = Physics.BoxCast(transform.position, Vector3.one * 0.3f, Vector3.down, Quaternion.identity, 1f, groundLayer);
        _input = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
        
        CameraManager();
        if (Input.GetKeyDown(KeyCode.Space) && _isGrounded)
            Jump();
        
        // Handle block interactions
        HWYLA();
        IndicatePlaceBlock();
        
        if (Input.GetKeyDown(KeyCode.Mouse1))
            PlaceBlock();
        
        if (Input.GetKey(KeyCode.Mouse0))
        {
            breakIndicator.SetActive(true);
            timeMining += Time.deltaTime;
            BlockBreakManager();
        }
        else
        {
            breakIndicator.SetActive(false);
            timeMining = 0f;  
        }

        HotbarManager();
    }

    private void FixedUpdate()
    {
        MovementManager();
    }

    public void MovementManager()
    {
        // camera-relative movement
        Vector3 forward = cameraObject.transform.forward;
        Vector3 right = cameraObject.transform.right;
        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();

        Vector3 movement = (forward * _input.z + right * _input.x);
        if (movement.sqrMagnitude > 0.01f)
            movement.Normalize();

        // target velocity
        Vector3 currentVel = _rb.linearVelocity;
        Vector3 targetVelocity = movement * movementSpeed;
        targetVelocity.y = currentVel.y;

        float accel = _isGrounded ? 12f : 4f;
        _rb.linearVelocity = Vector3.Lerp(currentVel, targetVelocity, accel * Time.deltaTime);
    }
    
    public void Jump()
    {
        _rb.AddForce(Vector3.up * jumpStrength, ForceMode.Impulse);
    }

    public void CameraManager()
    {
        // Simple mouse look
        _mouseInput = new Vector3(-Input.GetAxis("Mouse Y"), Input.GetAxis("Mouse X"), 0);
        _mouseMovement = _mouseInput * (mouseSensitivity * Time.deltaTime);

        cameraObject.transform.Rotate(_mouseMovement.x, 0, 0);
        float xRotation = cameraObject.transform.localEulerAngles.x;
        xRotation = (xRotation > 180) ? xRotation - 360 : xRotation;
        cameraObject.transform.localEulerAngles = new Vector3(xRotation, 0, 0);
        transform.Rotate(0, _mouseMovement.y, 0);
    }

    public void BlockBreakManager()
    {
        // If looking at air, reset mining time
        if (_lookingAtBlockType == World.BlockType.AIR)
        {
            timeMining = 0f;
            return;
        }   
        
        // Update break indicator
        RaycastHit hit = GetLookedAtBlock();
        if(hit.collider != null)
        {
            Vector3 placePosition = hit.point - hit.normal * 0.5f;
            placePosition = new Vector3(Mathf.Floor(placePosition.x), Mathf.Floor(placePosition.y), Mathf.Floor(placePosition.z));
            breakIndicator.transform.position = placePosition + new Vector3(0.5f, 0.5f, 0.5f);
            float timeToBreak = 0f;
            if(_lookingAtBlockType == World.BlockType.GRASS)
                timeToBreak = 1f;
            else if(_lookingAtBlockType == World.BlockType.STONE)
                timeToBreak = 2f;
            else if(_lookingAtBlockType == World.BlockType.SNOW)
                timeToBreak = 0.5f;
            breakMaterial.mainTexture = breakSprites[
                Mathf.Clamp((int)(timeMining / timeToBreak * breakSprites.Length), 0, breakSprites.Length - 1)].texture;
        }
        else
            breakIndicator.SetActive(false);
        
        // This should most likely be done in a like an dictionary of classes
        // of blocks with all sorts of properties but i want ot keep it simple
        if (timeMining >= World.Instance.GetBlockTimeToBreak(_lookingAtBlockType))
            BreakBlock();
    }

    public void IndicatePlaceBlock()
    {
        // Dont show indicator if no blocks to place
        if (_blockAmounts[_selectedBlockIndex - 1] <= 0)
        {
            placeIndicator.SetActive(false);
            return;
        }
        
        // Show place indicator at the block being looked at
        RaycastHit hit = GetLookedAtBlock();
        if(hit.collider != null)
        {
            Vector3 placePosition = hit.point + hit.normal * 0.5f;
            placePosition = new Vector3(Mathf.Floor(placePosition.x), Mathf.Floor(placePosition.y), Mathf.Floor(placePosition.z));
            placeIndicator.transform.position = placePosition + new Vector3(0.5f, 0.5f, 0.5f);
            placeIndicator.SetActive(true);
        }
        else
            placeIndicator.SetActive(false);
    }

    public void PlaceBlock()
    {
        RaycastHit hit = GetLookedAtBlock();
        if(hit.collider != null && _blockAmounts[_selectedBlockIndex - 1] > 0)
        {
            Vector3 placePosition = hit.point + hit.normal * 0.5f;
            placePosition = new Vector3(Mathf.Floor(placePosition.x), Mathf.Floor(placePosition.y), Mathf.Floor(placePosition.z));
            if (!IsPlayerInBlock(placePosition))
                return;
            World.Instance.PlaceBlockAt(placePosition, World.BlockType.AIR + (_selectedBlockIndex));
            _blockAmounts[_selectedBlockIndex - 1]--;
        }
    }
    
    public void BreakBlock()
    {
        RaycastHit hit = GetLookedAtBlock();
        if(hit.collider != null)
        {
            timeMining = 0f;
            Vector3 breakPosition = hit.point - hit.normal * 0.5f;
            breakPosition = new Vector3(Mathf.Floor(breakPosition.x), Mathf.Floor(breakPosition.y), Mathf.Floor(breakPosition.z));
            _blockAmounts[(int)_lookingAtBlockType-1]++;
            World.Instance.RemoveBlockAt(breakPosition);
        }
    }
    
    public void HWYLA() //Heres what youre looking at --- minecraft mod reference
    {
        RaycastHit hit = GetLookedAtBlock();
        if(hit.collider != null)
        {
            Vector3 position = hit.point - hit.normal * 0.5f;
            position = new Vector3(Mathf.Floor(position.x), Mathf.Floor(position.y), Mathf.Floor(position.z));
            _lookingAtBlockType = World.Instance.GetBlockAt(position);
            
            if(_lookingAtBlockPos != position) // reset mining time if looking at a new block
                timeMining = 0f;
            _lookingAtBlockPos = position;
        }
    }

    public void HotbarManager()
    {
        // Switch selected block with number keys
        if(Input.GetKeyDown(KeyCode.Alpha1))
            _selectedBlockIndex = 1; 
        if(Input.GetKeyDown(KeyCode.Alpha2))
            _selectedBlockIndex = 2; 
        if(Input.GetKeyDown(KeyCode.Alpha3))
            _selectedBlockIndex = 3; 
        
        // Update UI
        foreach (var bg in blockAmountBackground)
        {
            bg.color = new Color(1, 1, 1, .25f);
        }

        blockAmountBackground[_selectedBlockIndex - 1].color = new Color(1, 1, 1, 1f);
        
        for (int i = 0; i < _blockAmounts.Length; i++)
        {
            blockAmountText[i].text = _blockAmounts[i].ToString();
        }
    }
    
    // Raycast from camera to see what block player is looking at
    private RaycastHit GetLookedAtBlock()
    {
        RaycastHit hit = new RaycastHit();
        int layer = 1 << LayerMask.NameToLayer("Ground");
        Physics.Raycast(cameraObject.transform.position, cameraObject.transform.forward, out hit, 5f, layer, QueryTriggerInteraction.Ignore);
        return hit;
    }
    
    // Use boxcast to check if player is inside the block at blockPosition
    private bool IsPlayerInBlock(Vector3 blockPosition)
    {
        Vector3 boxCenter = transform.position;
        Vector3 boxHalfExtents = new Vector3(0.3f, 0.9f, 0.3f);
        Vector3 blockCenter = blockPosition + new Vector3(0.5f, 0.5f, 0.5f);
        Vector3 blockHalfExtents = new Vector3(0.5f, 0.5f, 0.5f);

        bool overlapX = Math.Abs(boxCenter.x - blockCenter.x) < (boxHalfExtents.x + blockHalfExtents.x);
        bool overlapY = Math.Abs(boxCenter.y - blockCenter.y) < (boxHalfExtents.y + blockHalfExtents.y);
        bool overlapZ = Math.Abs(boxCenter.z - blockCenter.z) < (boxHalfExtents.z + blockHalfExtents.z);

        return !(overlapX && overlapY && overlapZ);
    }
}
