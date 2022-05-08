using FishNet;
using FishNet.Object;
using FishNet.Object.Prediction;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class FishController : NetworkBehaviour
{
    #region Types.
    public struct MoveData
    {
        public float Horizontal;
        public float Vertical;
        public bool Sprint;
        public bool Jump;
        public float HorizontalMouse;
    }
    public struct ReconcileData
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public ReconcileData(Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            Position = position;
            Rotation = rotation;
            Velocity = velocity;
        }
    }

    private struct RagdollPart
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

    [Header("Interaction")]
    public float maxPickupDistance = 5;


    [Header("References")]
    [SerializeField] private GameObject _cameraObject;
    [SerializeField] private ViewController _viewController;
    [SerializeField] private GameObject _gunHolder;

    [SerializeField] private GameObject _graphics;

    [SerializeField] private Text _itemText;
    [SerializeField] private GameObject _itemTextPanel;




    #endregion

    #region Private.
    private CharacterController _characterController;
    private MoveData _clientMoveData;
    private InputMaster _inputMaster;
    private Vector2 _moveInput;
    private bool _sprintInput;
    private bool _jumpInput;

    private Gun _gun;


    private bool _isShooting;

    private Vector3 _velocity = new Vector3(0, 0, 0);
    private Vector3 _acceleration = new Vector3(0, 0, 0);

    private float _accelerationStrength = 0;
    private float _decelerationStrength = 0;
    private float _airAccelerationStrength = 0;
    private float _airDecelerationStrength = 0;

    private float _verticalVelocity = 0f;
    private int _jumpCount = 0;
    private uint _lastJumpTick = 0;

    private float _horizontalMouse = 0;

    private List<RagdollPart> _ragdoll_parts = new List<RagdollPart>();

    #endregion

    private void Awake()
    {
        _accelerationStrength = maxSpeed / accelerationTime;
        _decelerationStrength = -maxSpeed / decelerationTime;
        _airAccelerationStrength = maxSpeed / airAccelerationTime;
        _airDecelerationStrength = -maxSpeed / airDecelerationTime;

        InstanceFinder.TimeManager.OnTick += TimeManager_OnTick;
        InstanceFinder.TimeManager.OnUpdate += TimeManager_OnUpdate;
        _characterController = GetComponent<CharacterController>();

        foreach (Collider c in _graphics.GetComponentsInChildren<Collider>())
        {
            RagdollPart rp = new RagdollPart();
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
        _cameraObject.SetActive(IsOwner);

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
            _inputMaster.Player.Take.performed += ctx => pickupItem();
            _inputMaster.Enable();

            _graphics.SetActive(false);

            _itemTextPanel = UIManager.Instance.itemTextPanel;
            _itemText = _itemTextPanel.transform.GetChild(0).GetComponent<Text>();
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
            ReconcileData rd = new ReconcileData(transform.position, transform.rotation, _velocity);
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

        //if (horizontal == 0f && vertical == 0f && !_jumpInput && _horizontalMouse == 0f)
        //    return;

        md = new MoveData()
        {
            Horizontal = horizontal,
            Vertical = vertical,
            Sprint = _sprintInput,
            Jump = _jumpInput,
            HorizontalMouse = _horizontalMouse
        };

        _jumpInput = false;
    }

    public void Rotate(float horizontal)
    {
        _horizontalMouse = horizontal;
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
        Vector2 move = new Vector3(md.Horizontal, md.Vertical);


        //_velocity = _characterController.velocity;

        bool shouldJump = _lastJumpTick != InstanceFinder.TimeManager.LastPacketTick && md.Jump;

        // Vertical velocity
        if (shouldJump && (_characterController.isGrounded || _jumpCount < maxJumpCount))
        {
            // Apply initial jump force
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2.0f * Physics.gravity.y * mass);
            ++_jumpCount;
            _lastJumpTick = InstanceFinder.TimeManager.LastPacketTick;
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
        transform.Rotate(Vector3.up * md.HorizontalMouse);
    }

    [Reconcile]
    private void Reconciliation(ReconcileData rd, bool asServer)
    {
        transform.position = rd.Position;
        transform.rotation = rd.Rotation;
        //_characterController.velocity.Set(rd.Velocity);
        _velocity = rd.Velocity;
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



    private GameObject itemPickupCheck()
    {
        RaycastHit hit;
        //int oldMask = gameObject.layer;
        //gameObject.layer = 9;
        if (Physics.Raycast(_cameraObject.transform.position, _cameraObject.transform.forward, out hit, maxPickupDistance))//, gameObject.layer))
        {
            NetworkItem pi = hit.collider.GetComponent<NetworkItem>();
            if (pi != null)
            {
                Debug.DrawRay(_cameraObject.transform.position, _cameraObject.transform.forward * maxPickupDistance, Color.green);
                if (IsOwner)
                {
                    if (!_itemTextPanel.activeSelf)
                    {
                        _itemTextPanel.SetActive(true);
                    }
                    _itemText.text = pi.itemName + " [" + _inputMaster.Player.Take.GetBindingDisplayString() + "]";
                }

                //_characterController.enabled = true;
                //gameObject.layer = oldMask;
                return pi.gameObject;
            }
            else
            {
                Debug.DrawLine(_cameraObject.transform.position, hit.point, Color.red);
                Debug.DrawRay(hit.point, hit.normal, Color.magenta);
                //Debug.Log(hit.transform.gameObject.name);
            }
        }
        else
        {
            Debug.DrawRay(_cameraObject.transform.position, _cameraObject.transform.forward * maxPickupDistance, Color.blue);
        }


        if (IsOwner && _itemTextPanel.activeSelf)
        {
            _itemTextPanel.SetActive(false);
            _itemText.text = "";
        }

        //_characterController.enabled = true;
        //gameObject.layer = oldMask;
        return null;
    }

    private void equipItem(Gun newGun)
    {
        // Pick up new gun
        newGun.transform.parent = _gunHolder.transform;
        newGun.transform.localPosition = Vector3.zero;
        newGun.transform.localRotation = Quaternion.identity;
        if (IsServer)
        {
            newGun.GetComponent<Rigidbody>().isKinematic = true;
        }
        newGun.GetComponent<BoxCollider>().enabled = false;
        //newGun.GetComponent<NetworkTransform>().enabled = false;

        _gun = newGun;
        _viewController.EquipGun(_gun);

        //left_arm_target.localPosition = _gun.handle.localPosition;
        //left_arm_target.localRotation = _gun.handle.localRotation;
        //right_hand_ik.weight = 1;
        //left_hand_ik.weight = 1;
    }

    [ServerRpc]
    private void pickupItem()
    {
        Debug.Log(Owner);
        // Check if there is anything infront
        GameObject go = itemPickupCheck();
        if (go == null) return;

        // Is it a gun
        Gun newGun = go.GetComponent<Gun>();
        if (newGun != null)
        {
            // Chech if we already have a gun
            //if (_gun != null) DropItemServerRpc();

            NetworkObject nob = newGun.GetComponent<NetworkObject>();
            nob.GiveOwnership(base.Owner);

            //if (!IsHost)
            {
                //equipItem(newGun);
            }
            equipItemClientRpc(newGun.GetComponent<NetworkObject>().ObjectId);
        }
    }

    [ObserversRpc(IncludeOwner = true, BufferLast = true)]
    private void equipItemClientRpc(int ObjectId)
    {
        NetworkObject no = InstanceFinder.ServerManager.Objects.Spawned[ObjectId];
        Gun newGun = no.gameObject.GetComponent<Gun>();

        equipItem(newGun);
    }

    private void toggleRagdoll(bool toggle)
    {
        //_animator.enabled = !toggle;

        if (toggle)
        {
            foreach (RagdollPart rp in _ragdoll_parts)
            {
                rp.rigidbody.isKinematic = false;
            }
        }
        else
        {
            foreach (RagdollPart rp in _ragdoll_parts)
            {
                rp.rigidbody.isKinematic = true;
                rp.transform.localPosition = rp.initialPos;
                rp.transform.localRotation = rp.initialRot;
                rp.transform.localScale = rp.initialScale;
            }
        }
    }
}