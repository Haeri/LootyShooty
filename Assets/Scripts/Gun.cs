using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI;
using MLAPI.Messaging;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(BoxCollider))]
public class Gun : NetworkBehaviour
{
    [System.Serializable]
    public struct Sight
    {
        public Transform sightTransform;
        public float magninfication;
    }

    public Vector3 recoil;
    public Vector3 kickback;
    public Vector2 mouseRecoil;

    public float kickbackRandomScalar;

    public float muzzleVelocity;
    public float fireRate = 1;
    public float reloadSpeed = 1;
    public int bullets = 30;

    public GameObject bullet;
    public Transform muzzle;
    public Transform mag;
    public Transform handle;

    private AudioSource audioSource;
    
    public List<Sight> sights = new List<Sight>();

    public Vector3 reloadThrowDirection;

    private float lastFire;
    private bool timeToFire;
    private bool isReloading;
    private float reloadProgress = 0;
    private Vector3 magStore = new Vector3(-0.1f, -0.3f, -0.2f);


    void Start()
    {
        audioSource = GetComponent<AudioSource>();

        if(IsClient && !IsServer)
        {
            // Remove Rigidbody, since this is a newtwork object and
            // the server is going to take care of physics simulation
            Destroy(GetComponent<Rigidbody>());
        }
    }

    void Update()
    {
        if (!timeToFire)
        {
            lastFire += Time.deltaTime;
            if (lastFire > (10.0f / fireRate))
            {
                timeToFire = true;
                lastFire = 0;
            }
        }

        if (isReloading)
        {
            Transform _mag = mag.GetChild(0);
            if (reloadProgress < 1.0f)
            {
                _mag.localPosition = Vector3.Lerp(magStore, Vector3.zero, reloadProgress);
                reloadProgress += Time.deltaTime * reloadSpeed;
            }
            else
            {
                isReloading = false;
                reloadProgress = 0;
                _mag.localPosition = Vector3.zero;
                if (transform.parent != null) 
                {
                    transform.parent.localPosition += new Vector3(0, 0.03f, 0);
                }
                bullets = 30;
            }
        }
    }

    public void shoot()
    {
        // Perform action on server
        ShootServerRpc(OwnerClientId);

        // Dont perform action on the host, as Host is also server
        if (!IsHost)
        {
            // Perform action locally
            if (bullets > 0)
            {
                if (timeToFire && !isReloading)
                {
                    shootAction(true);

                    --bullets;
                    timeToFire = false;
                }
            }
            else
            {
                Reload();
            }
        }
    }

    private void shootAction(bool blank)
    {
        //Debug.Log("Shoot " + (blank ? "fake" : "real") + " bullets");
        audioSource.PlayOneShot(audioSource.clip);

        GameObject b = ObjectPool.getInstance().instanciate(bullet);
        b.transform.position = muzzle.position;
        b.transform.rotation = muzzle.rotation;
        b.GetComponent<Projectile>().blank = blank;
        if (transform.parent != null)
        {
            b.GetComponent<Projectile>().shooter = transform.parent.parent.parent.gameObject;
        }
        b.GetComponent<Rigidbody>().velocity = transform.forward * muzzleVelocity;

        if (transform.parent != null)
        {
            transform.parent.localPosition += kickback;
            //transform.parent.localPosition += Util.vec3FromRandomAngle(kickback * 0.001f, kickbackRandomScalar);
            transform.parent.localRotation = Quaternion.Euler(recoil) * transform.parent.localRotation;

            transform.parent.parent.GetComponent<ViewController>().addRecoid(mouseRecoil);
        }
    }

    [ServerRpc]
    private void ShootServerRpc(ulong shooter)
    {
        if (bullets > 0)
        {
            if (timeToFire && !isReloading)
            {
                if (IsServer)
                {
                    shootAction(false);
                }
                ShootClientRpc(shooter);

                --bullets;
                timeToFire = false;
            }
        }
        else
        {
            Reload();
        }
    }

    [ClientRpc]
    public void ShootClientRpc(ulong shooter)
    {
        if (IsHost) return;

        // Replicate shooting on all clients except for the original one
        if (shooter != NetworkManager.LocalClientId)
        {   
            shootAction(true);
        }
    }

    public void Reload()
    {
        if (!isReloading)
        {
            isReloading = true;
            if (transform.parent != null) 
            {
                transform.parent.localRotation = Quaternion.Euler(new Vector3(0, 0, -90)) * transform.parent.localRotation;
            }
            Transform old = mag.GetChild(0);

            GameObject newOne = Instantiate(old.gameObject, old.position, old.rotation, mag);
            newOne.transform.Translate(magStore);

            old.GetComponent<Rigidbody>().isKinematic = false;
            old.GetComponent<Rigidbody>().AddForce(transform.right * -150);
            old.GetComponent<BoxCollider>().enabled = true;
            old.parent = null;
        }
    }

}
