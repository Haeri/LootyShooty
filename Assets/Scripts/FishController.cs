using FishNet;
using FishNet.Object;
using FishNet.Object.Prediction;
using UnityEngine;


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
    #endregion

    #region Serialized.
    [SerializeField]
    private float _moveRate = 5f;
    [SerializeField]
    private GameObject cam;
    #endregion

    #region Private.
    private CharacterController _characterController;
    private MoveData _clientMoveData;
    private InputMaster inputMaster;
    private Vector2 _moveInput;
    #endregion

    private void Awake()
    {
        InstanceFinder.TimeManager.OnTick += TimeManager_OnTick;
        InstanceFinder.TimeManager.OnUpdate += TimeManager_OnUpdate;
        _characterController = GetComponent<CharacterController>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        _characterController.enabled = (base.IsServer || base.IsOwner);
        cam.SetActive(IsOwner);

        if (IsOwner)
        {
            inputMaster = new InputMaster();
            inputMaster.Player.Move.performed += ctx => _moveInput = ctx.ReadValue<Vector2>();
            inputMaster.Enable();
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

        if (horizontal == 0f && vertical == 0f)
            return;

        md = new MoveData()
        {
            Horizontal = horizontal,
            Vertical = vertical
        };
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
        Vector3 move = new Vector3(md.Horizontal, Physics.gravity.y, md.Vertical);
        _characterController.Move(move * _moveRate * delta);
    }

    [Reconcile]
    private void Reconciliation(ReconcileData rd, bool asServer)
    {
        transform.position = rd.Position;
        //transform.rotation = rd.Rotation;
    }



    private void OnEnable()
    {
        if (inputMaster == null || !IsOwner) return;

        inputMaster.Enable();
    }
    private void OnDisable()
    {
        if (!IsOwner) return;

        inputMaster.Disable();
    }


}