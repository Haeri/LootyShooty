using Unity.Netcode;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

public class ViewController : NetworkBehaviour
{
    public Vector2 mouseSensitivity = new Vector2(30.0f, 30.0f);

    public Vector3 gunSway = new Vector3(0.5f, 0.4f, 0.7f);
    public float recoverSpeed = 5.0f;
    public float recoilRevoverSpeed = 3;
    public float variance = 0.4f;

    private Transform playerTransform;

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

    public NetworkVariable<bool> isAds = new NetworkVariable<bool>(/*new NetworkVariableSettings { WritePermission = NetworkVariablePermission.OwnerOnly },*/ false);
    public NetworkVariable<int> sightIndex = new NetworkVariable<int>(/*new NetworkVariableSettings { WritePermission = NetworkVariablePermission.OwnerOnly }, */ 0);

    private void Awake()
    {
        holderTransform = transform.GetChild(0);
        holderPosition = holderTransform.localPosition;
        holderRotation = holderTransform.localRotation;

        cam = GetComponent<Camera>();
        initialFOV = cam.fieldOfView;

        volume = GetComponent<Volume>();
        volume.profile.TryGet(out dofEffect);
    }

    void Start()
    {
        if (IsLocalPlayer)
        {
            inputMaster = new InputMaster();
            inputMaster.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
            inputMaster.Player.MouseLock.performed += ctx => ToggleMouseLock();
            inputMaster.Enable();

            Cursor.lockState = CursorLockMode.Locked;
        }

        playerTransform = transform.parent.GetComponent<Transform>();
    }

    void Update()
    {
        float mouseX = 0;
        float mouseY = 0;

       
        if (IsLocalPlayer && Cursor.lockState == CursorLockMode.Locked)
        {
            mouseX = lookInput.x * mouseSensitivity.x * Time.deltaTime + _recoil.x;
            mouseY = lookInput.y * mouseSensitivity.y * Time.deltaTime + _recoil.y;

            _recoil = Vector2.zero;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90.0f, 90.0f);

            transform.localRotation = Quaternion.Euler(xRotation, 0, 0);
            playerTransform.Rotate(Vector3.up * mouseX);
        }

        if (_recoilReverse != Vector2.zero)
        {
            _recoil = _recoilReverse * recoilRevoverSpeed * Time.deltaTime;
            _recoilReverse -= _recoil;
        }


        if (isAds.Value)
        {
            if (IsLocalPlayer)
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


            if (IsLocalPlayer && cam.fieldOfView != targetFOV)
            {
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * adsSpeed);
                dofEffect.focalLength.value = Mathf.Lerp(dofEffect.focalLength.value, 50, Time.deltaTime * adsSpeed);
            }
        }
        else
        {
            if (IsLocalPlayer)
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

            if (IsLocalPlayer && cam.fieldOfView != initialFOV)
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

    public void AddRecoid(Vector2 direction)
    {
        Vector2 rand = Random.insideUnitCircle.normalized * variance;
        direction += rand;
        _recoil += direction;
        _recoilReverse -= direction;
    }

    private void OnEnable()
    {
        if (!IsLocalPlayer) return;

        inputMaster.Enable();
    }
    private void OnDisable()
    {
        if (!IsLocalPlayer) return;

        inputMaster.Disable();
    }


   
}