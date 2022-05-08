using FishNet;
using FishNet.Object;
using FishNet.Object.Prediction;
using UnityEngine;
using System.Collections.Generic;

/*
* 
* See TransformPrediction.cs for more detailed notes.
* 
*/



public class FishController : NetworkBehaviour
{
    #region Types.
    public struct MoveData
    {
        public float Horizontal;
        public float Vertical;
        public bool Sprint;
        public bool Jump;
    }
    public struct ReconcileData
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public ReconcileData(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }
    }

    private struct ragdoll_part
    {
        public Collider collider;
        public Rigidbody rigidbody;
        public Transform transform;
        public Vector3 initialPos;
        public Vector3 initialScale;
        public Quaternion initialRot;
    }
    #endregion

    #region Serialized.
    [Header("Movement")]
    [SerializeField] private float maxSpeed = 5.0f;
    [SerializeField] private float maxShiftSpeed = 8;

    [SerializeField] private float accelerationTime = 0.1f;
    [SerializeField] private float decelerationTime = 0.1f;

    [SerializeField] private float airAccelerationTime = 0.4f;
    [SerializeField] private float airDecelerationTime = 3;
    [SerializeField] private float jumpHeight = 2.0f;
    [SerializeField] private float mass = 4.0f;
    [SerializeField] private int maxJumpCount = 2;

    [Header("References")]
    [SerializeField] private GameObject _cam;
    [SerializeField] private ViewController _viewController;
    [SerializeField] private GameObject _gunHolder;
    [SerializeField] private GameObject _bones_root;

    [SerializeField] private GameObject _graphics;

    

    #endregion

    #region Private.
    private CharacterController _characterController;
    private MoveData _clientMoveData;
    private InputMaster _inputMaster;
    private Vector2 _moveInput;
    private bool _sprintInput;
    private bool _jumpInput;


    private bool _isShooting;

    private Vector3 _velocity = new Vector3(0, 0, 0);
    private Vector3 _acceleration = new Vector3(0, 0, 0);
    private Vector3 _lastPosition = new Vector3(0, 0, 0);

    private float _accelerationStrength;
    private float _airAccelerationStrength;
    private float _decelerationStrength;
    private float _airDecelerationStrength;

    private float _verticalVelocity = 0f;
    private int _jumpCount = 0;

    private List<ragdoll_part> _ragdoll_parts = new List<ragdoll_part>();

    #endregion

    private void Awake()
    {
        InstanceFinder.TimeManager.OnTick += TimeManager_OnTick;
        InstanceFinder.TimeManager.OnUpdate += TimeManager_OnUpdate;
        _characterController = GetComponent<CharacterController>();

        foreach (Collider c in _bones_root.GetComponentsInChildren<Collider>())
        {
            ragdoll_part rp = new ragdoll_part();
            rp.collider = c;
            rp.rigidbody = c.GetComponent<Rigidbody>();
            rp.transform = c.transform;
            rp.initialPos = c.transform.localPosition;
            rp.initialRot = c.transform.localRotation;
            rp.initialScale = c.transform.localScale;
            _ragdoll_parts.Add(rp);
        }

        toggleRagdoll(false);

    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        _characterController.enabled = (base.IsServer || base.IsOwner);
        _cam.SetActive(IsOwner);

        if (IsOwner)
        {
            _inputMaster = new InputMaster();
            _inputMaster.Player.Move.performed += ctx => _moveInput = ctx.ReadValue<Vector2>();
            _inputMaster.Player.Sprint.started += ctx => _sprintInput = true;
            _inputMaster.Player.Sprint.canceled += ctx => _sprintInput = false;
            _inputMaster.Player.Jump.performed += ctx => _jumpInput = true;            
            //_inputMaster.Player.Reload.performed += ctx => Reload();
            _inputMaster.Player.Fire.started += ctx => _isShooting = true;
            _inputMaster.Player.Fire.canceled += ctx => _isShooting = false;
            //_inputMaster.Player.ADS.started += ctx => doAds(true);
            //_inputMaster.Player.ADS.canceled += ctx => doAds(false);
            //_inputMaster.Player.CycleSight.performed += ctx => cycleSight(ctx.ReadValue<float>());
            //_inputMaster.Player.Drop.performed += ctx => DropItem();
            //_inputMaster.Player.Take.performed += ctx => pickupItemServerRpc();
            _inputMaster.Enable();

            _graphics.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (InstanceFinder.TimeManager != null)
        {
            InstanceFinder.TimeManager.OnTick -= TimeManager_OnTick;
            InstanceFinder.TimeManager.OnUpdate -= TimeManager_OnUpdate;
        }
    }

    private void TimeManager_OnTick()
    {
        if (base.IsOwner)
        {
            Reconciliation(default, false);
            CheckInput(out MoveData md);
            Move(md, false);
        }
        if (base.IsServer)
        {
            Move(default, true);
            ReconcileData rd = new ReconcileData(transform.position, transform.rotation);
            Reconciliation(rd, true);
        }
    }


    private void TimeManager_OnUpdate()
    {
        if (base.IsOwner)
            MoveWithData(_clientMoveData, Time.deltaTime);
    }

    private void CheckInput(out MoveData md)
    {
        //Debug.Log(_moveInput);
        md = default;

        float horizontal = _moveInput.x;
        float vertical = _moveInput.y;

        if (horizontal == 0f && vertical == 0f && !_jumpInput)
            return;

        md = new MoveData()
        {
            Horizontal = horizontal,
            Vertical = vertical,
            Sprint = _sprintInput,
            Jump = _jumpInput
        };

        _jumpInput = false;
    }

    [Replicate]
    private void Move(MoveData md, bool asServer, bool replaying = false)
    {
        if (asServer || replaying)
            MoveWithData(md, (float)base.TimeManager.TickDelta);
        else if (!asServer)
            _clientMoveData = md;
    }

    private void MoveWithData(MoveData md, float delta)
    {
        //Vector3 move = new Vector3(md.Horizontal, Physics.gravity.y, md.Vertical);
        Vector2 move = new Vector3(md.Horizontal, md.Vertical);
        //_characterController.Move(move * _moveRate * delta);

        _accelerationStrength = maxSpeed / accelerationTime;
        _decelerationStrength = -maxSpeed / decelerationTime;
        _airAccelerationStrength = maxSpeed / airAccelerationTime;
        _airDecelerationStrength = -maxSpeed / airDecelerationTime;
        _velocity = _characterController.velocity;

        Debug.Log(_jumpCount);
        // Vertical velocity
        if (md.Jump && (_characterController.isGrounded || _jumpCount < maxJumpCount))
        {
            // Apply initial jump force
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2.0f * Physics.gravity.y * mass);
            ++_jumpCount;
        }
        else
        {
            if (_characterController.isGrounded)
            {
                // No jumping
                _verticalVelocity = -1.0f;
                _jumpCount = 0;
            }
            else
            {
                // In the air
                _verticalVelocity += Physics.gravity.y * mass * delta;
            }
        }

        // Horizontal velocity
        if (move.magnitude > 0)
        {
            // Movement input -> accelerate
            if (_characterController.isGrounded)
            {
                _acceleration = _accelerationStrength * (transform.forward * move.y + transform.right * move.x).normalized;
            }
            else
            {
                _acceleration = _airAccelerationStrength * (transform.forward * move.y + transform.right * move.x).normalized;
            }
        }
        else
        {
            // No input -> decelerate
            if (_characterController.isGrounded)
            {
                _acceleration = _decelerationStrength * _velocity.normalized;
            }
            else
            {
                _acceleration = _airDecelerationStrength * _velocity.normalized;
            }

            if (Vector3.Dot(_velocity, _velocity + _acceleration * delta) < 0)
            {
                // Character is standing still
                _acceleration = Vector3.zero;
                _velocity = Vector3.zero;
            }
        }

        _velocity += _acceleration * delta;

        float speedcap = md.Sprint ? maxShiftSpeed : maxSpeed;


        // Cap horizontal speed 
        if (new Vector2(_velocity.x, _velocity.z).magnitude > speedcap)
        {
            _velocity.y = 0;
            _velocity = _velocity.normalized * speedcap;
         
        }

        _velocity.y = _verticalVelocity;


        _characterController.Move(_velocity * delta);
    }

    [Reconcile]
    private void Reconciliation(ReconcileData rd, bool asServer)
    {
        transform.position = rd.Position;
        //transform.rotation = rd.Rotation;
    }



    private void OnEnable()
    {
        if (_inputMaster == null || !IsOwner) return;

        _inputMaster.Enable();
    }
    private void OnDisable()
    {
        if (!IsOwner) return;

        _inputMaster.Disable();
    }








    private void toggleRagdoll(bool toggle)
    {
        //_animator.enabled = !toggle;

        if (toggle)
        {
            foreach (ragdoll_part rp in _ragdoll_parts)
            {
                rp.rigidbody.isKinematic = false;
            }
        }
        else
        {
            foreach (ragdoll_part rp in _ragdoll_parts)
            {
                rp.rigidbody.isKinematic = true;
                rp.transform.localPosition = rp.initialPos;
                rp.transform.localRotation = rp.initialRot;
                rp.transform.localScale = rp.initialScale;
            }
        }
    }
}