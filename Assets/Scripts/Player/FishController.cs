using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class FishController : NetworkBehaviour
{
    #region Types.
    public struct MoveData : IReplicateData
    {
        public float Horizontal;
        public float Vertical;
        public bool Sprint;
        public bool Jump;
        // Absolute view angles. The owner controls aim locally; the server
        // applies these so simulation and shooting direction match the client.
        public float Yaw;
        public float Pitch;

        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }
    public struct ReconcileData : IReconcileData
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float VerticalVelocity;
        public int JumpCount;
        private uint _tick;

        public ReconcileData(Vector3 position, Vector3 velocity, float verticalVelocity, int jumpCount)
        {
            Position = position;
            Velocity = velocity;
            VerticalVelocity = verticalVelocity;
            JumpCount = jumpCount;
            _tick = 0;
        }

        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
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

    [Header("Life")]
    [SerializeField] private float respawnDelay = 10f;

    [Header("Animation")]
    [SerializeField] private float animMultiplier = 1f;

    [Header("Feedback")]
    [SerializeField] private AudioClip hitConfirmSound;

    [Header("First person")]
    [SerializeField] private string headBoneName = "mixamorig:Head";
    [Tooltip("Triangles whose vertices carry more head-bone weight than this are hidden for the local player. Lower removes more of the neck.")]
    [SerializeField, Range(0f, 1f)] private float headWeightThreshold = 0.4f;

    [Header("References")]
    [SerializeField] private GameObject _cameraObject;
    [SerializeField] private GameObject _cameraRoot;    
    [SerializeField] private GameObject _gunHolder;
    [SerializeField] private GameObject _graphics;
    [SerializeField] private TwoBoneIKConstraint _leftHandIk;
    [SerializeField] private TwoBoneIKConstraint _rightHandIk;
    [SerializeField] private Transform _leftHandTarget;
    #endregion

    #region Private.
    // References
    private CharacterController _characterController;
    private InputMaster _inputMaster;
    private ViewController _viewController;
    private Damagable _damagable;
    private Animator _animator;
    private AudioSource _feedbackAudioSource;

    private struct HeadlessSwap
    {
        public SkinnedMeshRenderer Renderer;
        public Mesh Original;
        public Mesh Headless;
    }
    private readonly List<HeadlessSwap> _headlessSwaps = new List<HeadlessSwap>();
    private readonly List<Renderer> _headlessFallbacks = new List<Renderer>();


    private Text _itemText;
    private GameObject _itemTextPanel;


    private Vector2 _moveInput;
    private bool _sprintInput;
    private bool _jumpInput;

    private Gun _gun;


    private bool _isShooting;
    private Vector3 _spawnPosition;

    private Vector3 _velocity = new Vector3(0, 0, 0);
    private Vector3 _acceleration = new Vector3(0, 0, 0);

    private float _accelerationStrength = 0;
    private float _decelerationStrength = 0;
    private float _airAccelerationStrength = 0;
    private float _airDecelerationStrength = 0;

    private float _verticalVelocity = 0f;
    private int _jumpCount = 0;
    private bool _isRagdollActive;

    private List<RagdollPart> _ragdoll_parts = new List<RagdollPart>();

    #endregion

    private void Awake()
    {
        _viewController = GetComponent<ViewController>();
        _characterController = GetComponent<CharacterController>();
        _damagable = GetComponent<Damagable>();
        _animator = GetComponentInChildren<Animator>();
        _feedbackAudioSource = GetComponent<AudioSource>();

        _accelerationStrength = maxSpeed / accelerationTime;
        _decelerationStrength = -maxSpeed / decelerationTime;
        _airAccelerationStrength = maxSpeed / airAccelerationTime;
        _airDecelerationStrength = -maxSpeed / airDecelerationTime;

        if (_graphics == null)
        {
            Debug.LogError($"{name} has no graphics root assigned; ragdoll setup was skipped.", this);
        }
        else
        {
            foreach (Rigidbody body in _graphics.GetComponentsInChildren<Rigidbody>(true))
            {
                Collider ragdollCollider = body.GetComponent<Collider>();
                if (ragdollCollider == null)
                {
                    Debug.LogWarning($"Skipping ragdoll body '{body.name}' because it has no collider.", body);
                    continue;
                }

                RagdollPart rp = new RagdollPart();
                rp.collider = ragdollCollider;
                rp.rigidbody = body;
                rp.transform = body.transform;
                rp.initialPos = body.transform.localPosition;
                rp.initialRot = body.transform.localRotation;
                rp.initialScale = body.transform.localScale;
                _ragdoll_parts.Add(rp);
            }
        }

        SetRagdollActive(false);
        SetWeaponIk(null);
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        base.TimeManager.OnTick += TimeManager_OnTick;
        base.TimeManager.OnUpdate += TimeManager_OnUpdate;
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();

        if (base.TimeManager != null)
        {
            base.TimeManager.OnTick -= TimeManager_OnTick;
            base.TimeManager.OnUpdate -= TimeManager_OnUpdate;
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        _spawnPosition = transform.position;

        if (_damagable != null)
        {
            _damagable.OnDamage += Server_OnDamage;
            _damagable.OnDeath += Server_OnDeath;
        }
    }

    public override void OnStopServer()
    {
        base.OnStopServer();

        if (_damagable != null)
        {
            _damagable.OnDamage -= Server_OnDamage;
            _damagable.OnDeath -= Server_OnDeath;
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (IsOwner)
        {
            LevelRefs.Instance.LevelCamera.SetActive(false);
            _cameraObject.SetActive(true);

            _inputMaster = new InputMaster();
            _inputMaster.Player.Move.performed += ctx => _moveInput = ctx.ReadValue<Vector2>();
            _inputMaster.Player.Move.canceled += ctx => _moveInput = Vector2.zero;
            _inputMaster.Player.Sprint.started += ctx => _sprintInput = true;
            _inputMaster.Player.Sprint.canceled += ctx => _sprintInput = false;
            _inputMaster.Player.Jump.performed += ctx => _jumpInput = true;
            _inputMaster.Player.Reload.performed += ctx => Reload();
            _inputMaster.Player.Fire.started += ctx => _isShooting = true;
            _inputMaster.Player.Fire.canceled += ctx => _isShooting = false;
            _inputMaster.Player.ADS.started += ctx => _viewController.setADS(true);
            _inputMaster.Player.ADS.canceled += ctx => _viewController.setADS(false);
            _inputMaster.Player.CycleSight.performed += ctx => _viewController.cycleSight(ctx.ReadValue<float>());
            _inputMaster.Player.Drop.performed += ctx => DropItemServerRpc();
            _inputMaster.Player.Take.performed += ctx => PckupItemServerRPC();
            _inputMaster.Enable();

            SetFirstPersonBody(true);

            _itemTextPanel = UIManager.Instance.itemTextPanel;
            _itemText = _itemTextPanel.transform.GetChild(0).GetComponent<Text>();
        }
    }

    public override void OnStopClient()
    {
        base.OnStopClient();

        /* Ownership is already cleared during despawn, so the IsOwner guards
         * in OnDisable never run - tear the input down explicitly or a stale
         * InputMaster keeps firing RPCs from a dead pawn. */
        if (_inputMaster != null)
        {
            _inputMaster.Disable();
            _inputMaster.Dispose();
            _inputMaster = null;
        }

        ReleaseHeadlessSwaps();
    }

    private void TimeManager_OnTick()
    {
        // Runs on everyone: the owner builds real input, the server consumes the
        // owner's queued inputs, and observers consume forwarded states.
        Move(BuildMoveData());
        CreateReconcile();
    }


    private void TimeManager_OnUpdate()
    {
        bool isDead = _damagable != null && _damagable.IsDead();

        if (base.IsOwner && !isDead)
        {
            itemPickupCheck();

            if (_isShooting && _gun != null)
            {
                _gun.Shoot();
            }
        }

        UpdateAnimator();
    }

    private void Reload()
    {
        if (_gun != null)
        {
            _gun.Reload();
        }
    }

    /// <summary>Feeds the simulated velocity into the walk animation. Runs on every instance.</summary>
    private void UpdateAnimator()
    {
        if (_animator == null || !_animator.isActiveAndEnabled)
            return;

        Vector3 localVel = transform.InverseTransformDirection(_velocity);
        localVel = (localVel / maxShiftSpeed) * 2f * animMultiplier;
        _animator.SetFloat("VelocityX", localVel.x);
        _animator.SetFloat("VelocityZ", localVel.z);
    }

    private MoveData BuildMoveData()
    {
        if (!base.IsOwner)
            return default;

        MoveData md = new MoveData()
        {
            Horizontal = _moveInput.x,
            Vertical = _moveInput.y,
            Sprint = _sprintInput,
            Jump = _jumpInput,
            Yaw = transform.eulerAngles.y,
            Pitch = _viewController.Pitch
        };

        _jumpInput = false;

        return md;
    }

    [Replicate]
    private void Move(
        MoveData md,
        ReplicateState state = ReplicateState.Invalid,
        Channel channel = Channel.Unreliable)
    {
        /* The owner already rotates itself in real time via ViewController.
         * Everyone else (server, observers) applies the owner's view angles,
         * but only from inputs the owner actually created - default data
         * (e.g. dropped packets) would snap the view to zero. */
        if (!base.IsOwner && state.ContainsCreated())
        {
            transform.rotation = Quaternion.Euler(0f, md.Yaw, 0f);
            _cameraRoot.transform.localRotation = Quaternion.Euler(md.Pitch, 0f, 0f);
        }

        MoveWithData(md, (float)base.TimeManager.TickDelta);
    }

    private void MoveWithData(MoveData md, float delta)
    {
        // No movement while dead; the ragdoll takes over until respawn.
        if (_damagable != null && _damagable.IsDead())
            return;

        Vector2 move = new Vector2(md.Horizontal, md.Vertical);

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
        _velocity.y = 0f;

        float speedcap = md.Sprint ? maxShiftSpeed : maxSpeed;

        // Cap horizontal speed
        if (_velocity.magnitude > speedcap)
        {
            _velocity = _velocity.normalized * speedcap;
        }

        Vector3 frameVelocity = _velocity;
        frameVelocity.y = _verticalVelocity;

        /* Measure how far the controller really moved so collisions
         * (walls, slopes) feed back into the simulated velocity. This keeps
         * the client's replayed prediction consistent with the server. */
        Vector3 positionBefore = transform.position;
        _characterController.Move(frameVelocity * delta);
        Vector3 actualVelocity = (transform.position - positionBefore) / delta;
        _velocity.x = actualVelocity.x;
        _velocity.z = actualVelocity.z;
    }

    [Reconcile]
    private void Reconciliation(ReconcileData rd, Channel channel = Channel.Unreliable)
    {
        _velocity = rd.Velocity;
        _verticalVelocity = rd.VerticalVelocity;
        _jumpCount = rd.JumpCount;

        /* The CharacterController must be disabled while teleporting,
         * otherwise its internal physics position lags one simulate behind
         * the transform. */
        _characterController.enabled = false;
        transform.position = rd.Position;
        _characterController.enabled = !_isRagdollActive;
    }

    /// <summary>Builds the authoritative movement snapshot required by FishNet prediction.</summary>
    public override void CreateReconcile()
    {
        ReconcileData data = new ReconcileData(
            transform.position,
            _velocity,
            _verticalVelocity,
            _jumpCount);
        Reconciliation(data);
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


    private void DropItemAction()
    {
        _gun.transform.parent = null;
        _gun.SetEquiped(false);
        _gun.GetComponent<Rigidbody>().AddForce(transform.forward * 150);

        _gun = null;
        _viewController.EquipGun(null);
        SetWeaponIk(null);
    }

    [ServerRpc]
    private void DropItemServerRpc()
    {
        ServerDropItem();
    }

    private void ServerDropItem()
    {
        if (_gun != null)
        {
            _gun.GetComponent<NetworkObject>().RemoveOwnership();
            if (IsServerOnlyInitialized)
            {
                DropItemAction();
            }
            DropItemClientRpc();
        }
    }

    [ObserversRpc(BufferLast = true)]
    private void DropItemClientRpc()
    {
        DropItemAction();
    }



    private void EquipItemAction(Gun newGun)
    {
        if (IsServerInitialized)
        {
            Debug.Log("Server Equip Gun");
        }
        else {
            Debug.Log("Equip Gun");
        }

        _gun = newGun;

        // Pick up new gun
        _gun.transform.parent = _gunHolder.transform;
        _gun.transform.localPosition = Vector3.zero;
        _gun.transform.localRotation = Quaternion.identity;

        _gun.SetEquiped(true);

        _viewController.EquipGun(_gun);
        SetWeaponIk(_gun);
    }

    [ServerRpc]
    private void PckupItemServerRPC()
    {
        // Check if there is anything in front
        GameObject go = itemPickupCheck();
        if (go == null) return;

        // Is it a gun
        Gun newGun = go.GetComponent<Gun>();
        if (newGun != null)
        {
            // Chech if we already have a gun
            if (_gun != null) DropItemServerRpc();

            NetworkObject nob = newGun.GetComponent<NetworkObject>();
            nob.GiveOwnership(base.Owner);

            if (IsServerOnlyInitialized)
            {
                Debug.Log("Equip Item Call on Server");
                EquipItemAction(newGun);
            }
            EquipItemClientRpc(newGun.GetComponent<NetworkObject>().ObjectId);
        }
    }

    [ObserversRpc(BufferLast = true)]
    private void EquipItemClientRpc(int ObjectId)
    {
        NetworkObject no = InstanceFinder.ClientManager.Objects.Spawned[ObjectId];
        Gun newGun = no.gameObject.GetComponent<Gun>();

        EquipItemAction(newGun);
    }

    #region Death & respawn.
    private void Server_OnDamage(int amount)
    {
        // Show the damage flash on the victim's screen.
        if (base.Owner.IsValid)
            TargetDamageFlash(base.Owner);
    }

    private void Server_OnDeath()
    {
        ServerDropItem();
        SetRagdollActive(true);
        SetDeadObserversRpc(true);
        StartCoroutine(Server_RespawnAfterDelay());
    }

    private IEnumerator Server_RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);

        // The controller is already disabled while dead. Teleport the root,
        // then rebuild the animated pose before clients resume movement.
        transform.position = _spawnPosition;
        _velocity = Vector3.zero;
        _verticalVelocity = 0f;
        _jumpCount = 0;

        _damagable.ResetHealth();
        SetRagdollActive(false);
        SetDeadObserversRpc(false);
    }

    [ObserversRpc(BufferLast = true)]
    private void SetDeadObserversRpc(bool dead)
    {
        // The server has already applied this state. On a host the client and
        // server share the same object, so avoid resetting the ragdoll twice.
        if (!IsServerInitialized)
            SetRagdollActive(dead);

        if (IsOwner)
        {
            _viewController.enabled = !dead;
            if (dead)
                _isShooting = false;

            // Show the full body (head included) while the death ragdoll is
            // visible; go headless again on respawn.
            SetFirstPersonBody(!dead);
        }
    }

    [TargetRpc]
    private void TargetDamageFlash(NetworkConnection conn)
    {
        if (UIManager.Instance != null && UIManager.Instance.damagemarker != null)
            UIManager.Instance.damagemarker.GetComponent<UIFader>().ResetFade();
    }

    /// <summary>Called by a server-authoritative projectile after this pawn lands a hit.</summary>
    public void ServerNotifyHit()
    {
        if (!IsServerInitialized || !base.Owner.IsValid)
            return;

        TargetHitFeedback(base.Owner);
    }

    [TargetRpc]
    private void TargetHitFeedback(NetworkConnection conn)
    {
        if (_feedbackAudioSource != null && hitConfirmSound != null)
            _feedbackAudioSource.PlayOneShot(hitConfirmSound);

        if (UIManager.Instance != null && UIManager.Instance.hitmarker != null)
            UIManager.Instance.hitmarker.GetComponent<UIFader>().ResetFade();
    }
    #endregion

    #region First-person visuals and weapon IK.
    /// <summary>
    /// Toggles the local player's first-person body: headless meshes while
    /// alive and controlling the camera, the full meshes (head included)
    /// otherwise - e.g. while the death ragdoll is showing. Bones and
    /// colliders are untouched, so hit detection and the ragdoll stay intact.
    /// </summary>
    private void SetFirstPersonBody(bool enabled)
    {
        if (enabled && _headlessSwaps.Count == 0 && _headlessFallbacks.Count == 0)
            BuildHeadlessSwaps();

        foreach (HeadlessSwap swap in _headlessSwaps)
        {
            if (swap.Renderer != null)
                swap.Renderer.sharedMesh = enabled ? swap.Headless : swap.Original;
        }

        // Renderers without a resolvable head bone are simply hidden instead.
        foreach (Renderer fallback in _headlessFallbacks)
        {
            if (fallback != null)
                fallback.forceRenderingOff = enabled;
        }
    }

    private void BuildHeadlessSwaps()
    {
        if (_graphics == null)
            return;

        foreach (SkinnedMeshRenderer smr in _graphics.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh headless = HeadlessMeshBuilder.Build(smr, headBoneName, headWeightThreshold);
            if (headless == null)
            {
                Debug.LogWarning($"Could not build headless mesh for '{smr.name}' (head bone '{headBoneName}' not found?); hiding it instead.", smr);
                _headlessFallbacks.Add(smr);
                continue;
            }

            _headlessSwaps.Add(new HeadlessSwap { Renderer = smr, Original = smr.sharedMesh, Headless = headless });
        }
    }

    private void ReleaseHeadlessSwaps()
    {
        SetFirstPersonBody(false);

        foreach (HeadlessSwap swap in _headlessSwaps)
        {
            if (swap.Headless != null)
                Destroy(swap.Headless);
        }

        _headlessSwaps.Clear();
        _headlessFallbacks.Clear();
    }

    private void SetWeaponIk(Gun gun)
    {
        bool equipped = gun != null && gun.handle != null;

        if (equipped && _leftHandTarget != null)
            _leftHandTarget.SetPositionAndRotation(gun.handle.position, gun.handle.rotation);

        if (_leftHandIk != null)
            _leftHandIk.weight = equipped ? 1f : 0f;
        if (_rightHandIk != null)
            _rightHandIk.weight = equipped ? 1f : 0f;
    }
    #endregion

    private void SetRagdollActive(bool active)
    {
        _isRagdollActive = active;

        // The animator must release the bones while ragdolling.
        if (_animator != null)
            _animator.enabled = !active;

        if (_characterController != null)
            _characterController.enabled = !active;

        if (active)
        {
            foreach (RagdollPart rp in _ragdoll_parts)
            {
                rp.collider.enabled = true;
                rp.rigidbody.isKinematic = false;
                rp.rigidbody.linearVelocity = _velocity + Vector3.up * _verticalVelocity;
            }
        }
        else
        {
            foreach (RagdollPart rp in _ragdoll_parts)
            {
                rp.rigidbody.isKinematic = true;
                rp.rigidbody.linearVelocity = Vector3.zero;
                rp.rigidbody.angularVelocity = Vector3.zero;
                rp.transform.localPosition = rp.initialPos;
                rp.transform.localRotation = rp.initialRot;
                rp.transform.localScale = rp.initialScale;
            }
        }
    }
}
