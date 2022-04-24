using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;
using FishNet.Object;
using UnityEngine.Animations.Rigging;

public class OldPlayerController : NetworkBehaviour
{
    public float animmultiplier;
    [Header("Movement")]
    public float maxSpeed = 5.0f;
    public float maxShiftSpeed = 8;

    public float accelerationTime = 0.2f;
    public float decelerationTime = 0.1f;

    public float airAccelerationTime = 0.4f;
    public float airDecelerationTime = 3;
    public float jumpHeight = 2.0f;
    public float mass = 4.0f;
    public float gravity = -9.81f;

    [Header("Interaction")]
    public float maxPickupDistance = 5;

    [Header("References")]
    [SerializeField] private Animator _animator;
    [SerializeField] private ViewController _viewController;
    [SerializeField] private GameObject _gunHolder;
    [SerializeField] private GameObject _cameraObject;
    [SerializeField] private GameObject _headBone;
    [SerializeField] private GameObject _bones_root;
    public TwoBoneIKConstraint left_hand_ik;
    public TwoBoneIKConstraint right_hand_ik;
    public Transform left_arm_target;
    public AudioClip hitSound;

    private struct ragdoll_part
    {
        public Collider collider;
        public Rigidbody rigidbody;
        public Transform transform;
        public Vector3 initialPos;
        public Vector3 initialScale;
        public Quaternion initialRot;
    }
    private List<ragdoll_part> _ragdoll_parts = new List<ragdoll_part>();
    
    private Gun _gun;
    private CharacterController _characterController;
    private Damagable _playerHealth;

    private Text _itemText;
    private GameObject _itemTextPanel;

    private InputMaster inputMaster;
    private Vector2 _moveInput;
    private bool _isSprint;
    private bool _shouldJump;
    private bool _isShooting;

    private Vector3 _velocity = new Vector3(0, 0, 0);
    private Vector3 _acceleration = new Vector3(0, 0, 0);
    private Vector3 _lastPosition = new Vector3(0, 0, 0);

    private float _accelerationStrength;
    private float _airAccelerationStrength;
    private float _decelerationStrength;
    private float _airDecelerationStrength;

    private int _jumpCount = 0;
    private float _verticalVelocity = 0f;

#if false

    void Awake()
    {
        _characterController = gameObject.GetComponent<CharacterController>();
        _playerHealth = GetComponent<Damagable>();

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
        

        DebugGUI.SetGraphProperties("speed", "Speed", 0, 20, 1, new Color(0, 1, 1), true);
        DebugGUI.SetGraphProperties("state", "State", 0, 3, 1, new Color(1, 0, 0), true);
        DebugGUI.SetGraphProperties("vertical", "Vertical", 0, 20, 1, new Color(0, 0, 1), true);
        DebugGUI.SetGraphProperties("speedcap", "Cap", 0, 20, 1, new Color(1, 0, 1), true);
    }

    

    void Start()
    {
        if (IsServer)
        {
            _playerHealth.OnDamage += OnDamage;
            _playerHealth.OnDeath += OnDeath;
        }        

        if (!IsOwner)
        {
            _cameraObject.GetComponent<AudioListener>().enabled = false;
            _cameraObject.GetComponent<Camera>().enabled = false;
            _cameraObject.GetComponent<Volume>().enabled = false;
            return;
        }

        //NetworkManager.Singleton.OnClientDisconnectCallback += onDisconnect;

        _itemTextPanel = UIManager.Instance.itemTextPanel;
        _itemText = _itemTextPanel.transform.GetChild(0).GetComponent<Text>();

        inputMaster = new InputMaster();
        inputMaster.Player.Move.performed += ctx => _moveInput = ctx.ReadValue<Vector2>();
        inputMaster.Player.Sprint.started += ctx => _isSprint = true;
        inputMaster.Player.Sprint.canceled += ctx => _isSprint = false;
        inputMaster.Player.Jump.performed += ctx => Jump();
        inputMaster.Player.Reload.performed += ctx => Reload();
        inputMaster.Player.Fire.started += ctx => _isShooting = true;
        inputMaster.Player.Fire.canceled += ctx => _isShooting = false;
        inputMaster.Player.ADS.started += ctx => doAds(true);
        inputMaster.Player.ADS.canceled += ctx => doAds(false);
        inputMaster.Player.CycleSight.performed += ctx => cycleSight(ctx.ReadValue<float>());
        inputMaster.Player.Drop.performed += ctx => DropItem();
        inputMaster.Player.Take.performed += ctx => pickupItemServerRpc();
        inputMaster.Enable();

        _cameraObject.SetActive(true);
        _headBone.transform.localScale = Vector3.zero;

        right_hand_ik.weight = 0;
        left_hand_ik.weight = 0;
    }

    void Update()
    {
        if (!IsOwner) return;

        _accelerationStrength = maxSpeed / accelerationTime;
        _decelerationStrength = -maxSpeed / decelerationTime;
        _airAccelerationStrength = maxSpeed / airAccelerationTime;
        _airDecelerationStrength = -maxSpeed / airDecelerationTime;
        _velocity = _characterController.velocity;

        //text.text = "Speed: " + Mathf.Round(new Vector2(_velocity.x, _velocity.z).magnitude);


        if (_isShooting && _gun != null)
        {
            _gun.Shoot();
        }


        // Vertical velocity
        if (_shouldJump)
        {
            // Apply initial jump force
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2.0f * gravity * mass);
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
                _verticalVelocity += gravity * mass * Time.deltaTime;
            }
        }



        // Horizontal velocity
        if (_moveInput.magnitude > 0)
        {
            // Movement input -> accelerate
            if (_characterController.isGrounded)
            {
                _acceleration = _accelerationStrength * (transform.forward * _moveInput.y + transform.right * _moveInput.x).normalized;
            }
            else
            {
                _acceleration = _airAccelerationStrength * (transform.forward * _moveInput.y + transform.right * _moveInput.x).normalized;
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

            if (Vector3.Dot(_velocity, _velocity + _acceleration * Time.deltaTime) < 0)
            {
                // Character is standing still
                _acceleration = Vector3.zero;
                _velocity = Vector3.zero;
            }
        }

        _velocity += _acceleration * Time.deltaTime;

        float speedcap = _isSprint ? maxShiftSpeed : maxSpeed;


        // Cap horizontal speed 
        DebugGUI.Graph("speedcap", speedcap);
        if (new Vector2(_velocity.x, _velocity.z).magnitude > speedcap)
        {
            _velocity.y = 0;
            _velocity = _velocity.normalized * speedcap;
            DebugGUI.Graph("state", 3);
        }
        else
        {
            DebugGUI.Graph("state", 0);
        }

        _velocity.y = _verticalVelocity;

        DebugGUI.Graph("speed", new Vector2(_velocity.x, _velocity.z).magnitude);
        DebugGUI.Graph("vertical", _velocity.y);


        MoveServerRPC(_velocity * Time.deltaTime);
        

        if (_shouldJump) _shouldJump = false;

        itemPickupCheck();
        performAnimation();


        DebugGUI.Graph("velx", _animator.GetFloat("VelocityX"));
        DebugGUI.Graph("vely", _animator.GetFloat("VelocityZ"));
    }

    [ServerRpc]
    private void MoveServerRPC(Vector3 newpos)
    {
        _characterController.Move(newpos);
    }

    private void FixedUpdate()
    {
        //performAnimation();
    }

    private bool networkMove(ulong clientId, Vector3 oldPos, Vector3 newPos)
    {
        return true;
    }
    private void performAnimation()
    {

        //GetComponent<NetworkTransform>().IsMoveValidDelegate += networkMove;
        //float dividor = isOwner ? Time.deltaTime : 20;
        /*
        if(OwnerClientId == 0)
        {
            Debug.Log(OwnerClientId + " do lateupdate " + (_lastPosition == transform.position));
            DebugGUI.Graph("velx", (localVel.x / maxShiftSpeed) * 2);
            DebugGUI.Graph("vely", (localVel.z / maxShiftSpeed) * 2);
        }
        */

        if (IsOwner)
        {
            Vector3 localVel = (transform.position - _lastPosition) / Time.deltaTime;
            localVel = transform.InverseTransformDirection(localVel);
            localVel = (localVel / maxShiftSpeed) * 2 * animmultiplier;
            //_animator.SetFloat("VelocityX", (_animator.GetFloat("VelocityX") * 0.9f + localVel.x *0.1f));
            //_animator.SetFloat("VelocityZ", (_animator.GetFloat("VelocityZ") * 0.9f + localVel.z * 0.1f));
            _animator.SetFloat("VelocityX", localVel.x);
            _animator.SetFloat("VelocityZ", localVel.z);
            _lastPosition = transform.position;
        }
    }

    private void Jump()
    {

        if (_characterController.isGrounded || _jumpCount < 2)
        {
            _shouldJump = true;
        }
    }

    [ClientRpc]
    public void onDeathClientRpc()
    {
        if (isOwner)
        {
            _viewController.enabled = false;
            enabled = false;
        }

        toggleRagdoll(true);
    }

    [ClientRpc]
    public void onSpawnClientRpc()
    {
        if (IsOwner)
        {
            _characterController.enabled = false;
            transform.position = Vector3.zero;
            enabled = true;
            _characterController.enabled = true;
            _viewController.enabled = true;
        }
        toggleRagdoll(false);
    }

    private void toggleRagdoll(bool toggle)
    {
        _animator.enabled = !toggle;

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
                    _itemText.text = pi.itemName + " [" + inputMaster.Player.Take.GetBindingDisplayString() + "]";
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

    [ServerRpc]
    private void pickupItemServerRpc()
    {
        // Check if there is anything infront
        GameObject go = itemPickupCheck();
        if (go == null) return;

        // Is it a gun
        Gun newGun = go.GetComponent<Gun>();
        if (newGun != null)
        {
            // Chech if we already have a gun
            if (_gun != null) DropItemServerRpc();

            newGun.GetComponent<NetworkObject>().ChangeOwnership(OwnerClientId);

            if (!IsHost)
            {
                equipItem(newGun);
            }
            equipItemClientRpc(newGun.GetComponent<NetworkObject>().NetworkObjectId);
        }
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
        newGun.GetComponent<NetworkTransform>().enabled = false;

        _gun = newGun;
        _viewController.EquipGun(_gun);

        left_arm_target.localPosition = _gun.handle.localPosition;
        left_arm_target.localRotation = _gun.handle.localRotation;
        right_hand_ik.weight = 1;
        left_hand_ik.weight = 1;
    }

    [ClientRpc]
    private void equipItemClientRpc(ulong nid)
    {
        NetworkObject no = NetworkManager.Singleton.SpawnManager.SpawnedObjects[nid];
        Gun newGun = no.gameObject.GetComponent<Gun>();

        equipItem(newGun);
    }

    private void Reload()
    {
        if (_gun != null)
        {
            _gun.Reload();
        }
    }


    public void DropItem()
    {
        if (_gun != null)
        {
            DropItemServerRpc();
        }
    }

    private void DropItemAction()
    {
        _gun.transform.parent = null;
        if (IsServer)
        {
            _gun.GetComponent<Rigidbody>().isKinematic = false;
            _gun.GetComponent<Rigidbody>().AddForce(transform.forward * 150);
        }
        _gun.GetComponent<BoxCollider>().enabled = true;
        _gun.GetComponent<NetworkTransform>().enabled = true;

        _gun = null;
        _viewController.EquipGun(null);

        right_hand_ik.weight = 0;
        left_hand_ik.weight = 0;
    }

    [ServerRpc]
    private void DropItemServerRpc()
    {
        if (_gun != null)
        {
            _gun.GetComponent<NetworkObject>().RemoveOwnership();
            DropItemAction();
            dropItemClientRpc();
        }
    }

    [ClientRpc]
    private void dropItemClientRpc()
    {
        if (!IsHost)
        {
            DropItemAction();
        }
    }


    
    private void doAds(bool ads)
    {
        _viewController.setADS(ads);
    }
    private void cycleSight(float index)
    {
        _viewController.cycleSight(index);
    }

    private void OnDeath()
    {
        DropItemServerRpc();

        onDeathClientRpc();

        StartCoroutine(respawn());
    }


    IEnumerator respawn()
    {

        yield return new WaitForSeconds(10);

        _playerHealth.health.Value = _playerHealth.maxHealth;
        // transform.position = Vector3.zero;
        //_characterController.Move(Vector3.up * 10);
        onSpawnClientRpc();
    }

    private void OnDamage(int amount)
    {
        takeDamageClientRpc(amount, new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { OwnerClientId }
            }
        });
    }

    [ClientRpc]
    public void takeDamageClientRpc(int damage, ClientRpcParams clientRpcParams = default)
    {
        flashDamage();
    }

    public void enemyHitCallback(GameObject other, int damage, bool died)
    {
        enemyHitClientRpc(other.GetComponent<NetworkObject>().NetworkObjectId, damage, died, new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { OwnerClientId }
            }
        });
    }

    [ClientRpc]
    public void enemyHitClientRpc(ulong target, int damage, bool died, ClientRpcParams clientRpcParams = default)
    {
        GameObject other = NetworkManager.Singleton.SpawnManager.SpawnedObjects[target].gameObject;

        //Debug.Log("I hit " + other.name + " damage: " + damage + (died ? " He dead!" : ""));
        flashHit();
    }

    void flashHit()
    {
        GetComponent<AudioSource>().PlayOneShot(hitSound);
        UIManager.Instance.hitmarker.GetComponent<UIFader>().ResetFade();
    }
    void flashDamage()
    {
        UIManager.Instance.damagemarker.GetComponent<UIFader>().ResetFade();
    }

    /*
    private void onDisconnect(ulong clientId)
    {
        if(clientId == OwnerClientId)
        {
            DropItemServerRpc();
            Debug.Log($"Player ({OwnerClientId}) Disconnected from the Game");
        }
    }
    */
    private void OnEnable()
    {
        if (!isOwner) return;

        inputMaster.Enable();
    }
    private void OnDisable()
    {
        if (!isOwner) return;

        inputMaster.Disable();
    }
    /*
    
    void OnDestroy()
    {
        Debug.Log("OnDestroy Call");
        if (IsServer)
        {
            if (_gun != null)
            {
                _gun.GetComponent<NetworkObject>().RemoveOwnership();
                DropItemAction();
                dropItemClientRpc();
            }
        }
    }
    */
#endif
}
