using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleController : MonoBehaviour
{
    public float maxSpeed = 10;
    public float accelerationTime = 1f;
    public float decelerationTime = 1f;
    
    private Vector3 _acceleration;
    private Vector3 _velocity;

    private float _accelerationStrength;
    private float _decelerationStrength;

    private InputMaster _inputMaster;
    private CharacterController _characterController;
    private Vector2 _moveInput;

    // Start is called before the first frame update
    void Awake()
    {
        _inputMaster = new InputMaster();
        _inputMaster.Player.Move.performed += ctx => _moveInput = ctx.ReadValue<Vector2>();

        _characterController = gameObject.GetComponent<CharacterController>();
    }

    // Update is called once per frame
    void Update()
    {
        _accelerationStrength = maxSpeed / accelerationTime;
        _decelerationStrength = -maxSpeed / decelerationTime;
        
        if(_moveInput.magnitude > 0)
        {
            _acceleration = _accelerationStrength * (transform.forward * _moveInput.y + transform.right * _moveInput.x).normalized;
        }
        else
        {
            _acceleration = _decelerationStrength * _velocity.normalized;

            if (Vector3.Dot(_velocity, _velocity + _acceleration * Time.deltaTime) < 0)
            {
                Debug.Log("STOP");
                _acceleration = Vector3.zero;
                _velocity = Vector3.zero;
            }
        }
        

        _velocity += _acceleration * Time.deltaTime;

        // Cap max speed
        if (_velocity.magnitude > maxSpeed)
        {
            _velocity = _velocity.normalized * maxSpeed;
        }

        _characterController.Move(_velocity * Time.deltaTime);
    }


    private void OnEnable() => _inputMaster.Enable();
    private void OnDisable() => _inputMaster.Disable();
}
