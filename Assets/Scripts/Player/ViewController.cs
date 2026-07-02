using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ViewController : NetworkBehaviour
{
    /* Degrees per raw mouse-delta unit. Mouse deltas are already per-frame,
     * so they must NOT be scaled by Time.deltaTime - doing so makes aim
     * speed depend on framerate. */
    public Vector2 mouseSensitivity = new Vector2(0.2f, 0.2f);

    public Vector3 gunSway = new Vector3(0.5f, 0.4f, 0.7f);
    public float recoverSpeed = 5.0f;
    public float recoilRevoverSpeed = 3;
    public float variance = 0.4f;

    //private Transform playerTransform;
    [SerializeField] private GameObject _cameraObject;
    [SerializeField] private GameObject _cameraRoot;
    [SerializeField] private Transform _gunHolder;

    private float xRotation = 0.0f;
    private InputMaster inputMaster;
    private Vector2 lookInput;

    private Transform holderTransform;
    private Vector3 holderPosition;
    private Quaternion holderRotation;
    public float zoomFov = 60;
    private Camera cam;
    public float adsSpeed = 3;
    private float initialFOV;
    private DepthOfField dofEffect;
    private Volume volume;
    private Gun _gun;
    private Vector2 _recoil;
    private Vector2 _recoilReverse;

    public readonly SyncVar<bool> isAds = new(false);
    public readonly SyncVar<int> sightIndex = new(0);

    /// <summary>Current camera pitch in degrees; sent to the server with movement input.</summary>
    public float Pitch => xRotation;
    public float RecoilVariance => variance;

    private void Awake()
    {
        /* The sway/recover system must target the gun holder, never the
         * camera root - recovering the camera toward its rest rotation
         * fights the look input and vibrates the view. */
        holderTransform = _gunHolder != null ? _gunHolder : transform.GetChild(0);
        holderPosition = holderTransform.localPosition;
        holderRotation = holderTransform.localRotation;

        cam = _cameraObject.GetComponent<Camera>();
        initialFOV = cam.fieldOfView;

        volume = _cameraObject.GetComponent<Volume>();
        volume.profile.TryGet(out dofEffect);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (IsOwner)
        {
            inputMaster = new InputMaster();
            // Deltas accumulate here and are consumed (zeroed) each frame in Update.
            inputMaster.Player.Look.performed += ctx => lookInput += ctx.ReadValue<Vector2>();
            inputMaster.Player.MouseLock.performed += ctx => ToggleMouseLock();
            inputMaster.Enable();

            Cursor.lockState = CursorLockMode.Locked;
        }

        //playerTransform = transform.parent.GetComponent<Transform>();
    }

    public override void OnStopClient()
    {
        base.OnStopClient();

        // Ownership is gone by now, so OnDisable's IsOwner guard won't clean this up.
        if (inputMaster != null)
        {
            inputMaster.Disable();
            inputMaster.Dispose();
            inputMaster = null;
        }
    }

    void Update()
    {
        float mouseX = 0;
        float mouseY = 0;

        // Consume the accumulated look delta even when unlocked so it cannot
        // build up and yank the view when the cursor is re-locked.
        Vector2 look = lookInput;
        lookInput = Vector2.zero;

        if (IsOwner && Cursor.lockState == CursorLockMode.Locked)
        {
            mouseX = look.x * mouseSensitivity.x + _recoil.x;
            mouseY = look.y * mouseSensitivity.y + _recoil.y;

            _recoil = Vector2.zero;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90.0f, 90.0f);

            _cameraRoot.transform.localRotation = Quaternion.Euler(xRotation, 0, 0);
            /* Yaw is applied directly for instant response. The resulting
             * absolute angle is sent to the server with the movement input,
             * so the server stays authoritative over position while the
             * client owns its aim. */
            transform.Rotate(Vector3.up * mouseX);
        }

        if (_recoilReverse != Vector2.zero)
        {
            _recoil = _recoilReverse * recoilRevoverSpeed * Time.deltaTime;
            _recoilReverse -= _recoil;
        }


        if (isAds.Value)
        {
            if (IsOwner)
            {
                holderTransform.localRotation = Quaternion.Euler(new Vector3(mouseY * gunSway.x, -mouseX * gunSway.y, -mouseX * gunSway.z)) * holderTransform.localRotation;
            }

            float targetFOV = zoomFov;
            Vector3 pos = Vector3.forward;
            Quaternion rot = Quaternion.identity;

            if (_gun != null)
            {
                int len = _gun.sights.Count;
                int index = Math.Abs(sightIndex.Value % len);

                pos = _gun.sights[index].sightTransform.localPosition * -1;
                rot = Quaternion.Inverse(_gun.sights[index].sightTransform.localRotation);

                targetFOV = _gun.sights[index].magninfication;
            }

            if (holderTransform.localRotation != rot)
            {
                holderTransform.localRotation = Quaternion.Lerp(holderTransform.localRotation, rot, Time.deltaTime * recoverSpeed * 2);
            }
            if (holderTransform.localPosition != pos)
            {
                holderTransform.localPosition = Vector3.Lerp(holderTransform.localPosition, pos, Time.deltaTime * recoverSpeed * 2);
            }


            if (IsOwner && cam.fieldOfView != targetFOV)
            {
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * adsSpeed);
                dofEffect.focalLength.value = Mathf.Lerp(dofEffect.focalLength.value, 50, Time.deltaTime * adsSpeed);
            }
        }
        else
        {
            if (IsOwner)
            {
                holderTransform.localRotation = Quaternion.Euler(new Vector3(mouseY * gunSway.x, -mouseX * gunSway.y, -mouseX * gunSway.z)) * holderTransform.localRotation;
            }

            if (holderTransform.localRotation != holderRotation)
            {
                holderTransform.localRotation = Quaternion.Lerp(holderTransform.localRotation, holderRotation, Time.deltaTime * recoverSpeed);
            }
            if (holderTransform.localPosition != holderPosition)
            {
                holderTransform.localPosition = Vector3.Lerp(holderTransform.localPosition, holderPosition, Time.deltaTime * recoverSpeed);
            }

            if (IsOwner && cam.fieldOfView != initialFOV)
            {
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, initialFOV, Time.deltaTime * adsSpeed);
                dofEffect.focalLength.value = Mathf.Lerp(dofEffect.focalLength.value, 1, Time.deltaTime * adsSpeed);
            }
        }
    }

    public void setADS(bool ads)
    {
        setADSServerRPC(ads);
    }

    [ServerRpc]
    public void setADSServerRPC(bool ads)
    {
        isAds.Value = ads;
    }

    public void cycleSight(float input)
    {
        if (isAds.Value)
        {
            cycleSightServerRPC(input);
        }
    }

    [ServerRpc]
    public void cycleSightServerRPC(float input)
    {
        if (isAds.Value)
        {
            sightIndex.Value += Math.Sign(input);
        }
    }

    private void ToggleMouseLock()
    {
        Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
    }

    public void EquipGun(Gun gun)
    {
        _gun = gun;
    }

    public void AddRecoil(Vector2 direction, Vector2 deterministicJitter)
    {
        direction += deterministicJitter;
        _recoil += direction;
        _recoilReverse -= direction;
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
