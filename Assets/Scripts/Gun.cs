using System.Collections.Generic;
using UnityEngine;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Component.Transforming;

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
    [Tooltip("Maximum deterministic projectile deviation in degrees.")]
    public float spreadAngle;

    public float muzzleVelocity;
    public float fireRate = 1;
    public float reloadSpeed = 1;
    public int bullets = 30;
    public int bulletCapacity = 30;

    public GameObject bullet;
    public Transform muzzle;
    public Transform mag;
    public Transform handle;

    private AudioSource _audioSource;
    
    public List<Sight> sights = new List<Sight>();

    public Vector3 reloadThrowDirection;

    /// <summary>Unique runtime identity and deterministic random state for this gun.</summary>
    public int GunId => ObjectId;
    public uint WeaponSeed => weaponSeed.Value;
    public uint ShotsFired => shotsFired.Value;

    public readonly SyncVar<uint> weaponSeed = new(0u);
    public readonly SyncVar<uint> shotsFired = new(0u);

    private float lastFire;
    private bool timeToFire;
    private bool isReloading;
    private float reloadProgress = 0;
    private Vector3 magStore = new Vector3(-0.1f, -0.3f, -0.2f);
    private uint _predictedShotsFired;
    private readonly HashSet<uint> _predictedSequences = new();


    void Start()
    {
        _audioSource = GetComponent<AudioSource>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        weaponSeed.Value = DeterministicWeaponRandom.CreateWeaponSeed(unchecked((uint)ObjectId));
        shotsFired.Value = 0u;
        _predictedShotsFired = 0u;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        _predictedShotsFired = shotsFired.Value;
    }

    public override void OnOwnershipClient(NetworkConnection previousOwner)
    {
        base.OnOwnershipClient(previousOwner);
        _predictedShotsFired = shotsFired.Value;
        _predictedSequences.Clear();
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
                bullets = bulletCapacity;
            }
        }
    }

    public void SetEquiped(bool equiped)
    {
        if (IsServerInitialized)
        {
            GetComponent<Rigidbody>().isKinematic = equiped;
        }
        GetComponent<BoxCollider>().enabled = !equiped;
        GetComponent<NetworkTransform>().enabled = !equiped;
    }

    public void Shoot()
    {
        if (bullets <= 0)
        {
            Reload();
            return;
        }

        if (!timeToFire || isReloading)
            return;

        // A host performs the authoritative action directly. A remote owner
        // predicts with the same seed/sequence that the server will validate.
        if (IsServerInitialized)
        {
            TryShootServer(shotsFired.Value);
            return;
        }

        uint sequence = _predictedShotsFired++;
        uint seed = weaponSeed.Value;
        _predictedSequences.Add(sequence);

        ShootAction(false, seed, sequence);
        --bullets;
        timeToFire = false;
        ShootServerRpc(sequence);
    }

    private void ShootAction(bool isRealAction, uint seed, uint sequence)
    {
        //Debug.Log("Shoot " + (blank ? "fake" : "real") + " bullets");
        _audioSource.PlayOneShot(_audioSource.clip);

        // Activate pooled trails at the muzzle. Activating at world origin and
        // teleporting afterward draws a fake tracer line to (0,0,0).
        GameObject b = ObjectPool.Instance.instanciate(bullet, muzzle.position, muzzle.rotation);
        b.GetComponent<Projectile>().blank = !isRealAction;

        // Resolve the wielder by searching upward; hardcoded parent chains
        // break whenever the holder hierarchy changes.
        FishController wielder = GetComponentInParent<FishController>();
        // Also disables collision between the bullet and the wielder so
        // players cannot run into (and kill themselves with) their own shots.
        b.GetComponent<Projectile>().SetShooter(wielder != null ? wielder.gameObject : null);

        Vector2 spread = DeterministicWeaponRandom.InsideUnitCircle(
            seed, sequence, DeterministicWeaponRandom.SpreadStream) * spreadAngle;
        Vector3 shotDirection = muzzle.rotation * Quaternion.Euler(-spread.y, spread.x, 0f) * Vector3.forward;
        b.GetComponent<Rigidbody>().linearVelocity = shotDirection * muzzleVelocity;

        if (transform.parent != null)
        {
            Vector2 kickbackJitter = DeterministicWeaponRandom.InsideUnitCircle(
                seed, sequence, DeterministicWeaponRandom.KickbackStream) * kickbackRandomScalar;
            transform.parent.localPosition += kickback + new Vector3(kickbackJitter.x, kickbackJitter.y, 0f);
            transform.parent.localRotation = Quaternion.Euler(recoil) * transform.parent.localRotation;

            ViewController view = GetComponentInParent<ViewController>();
            if (view != null && view.IsOwner)
            {
                Vector2 recoilJitter = DeterministicWeaponRandom.InsideUnitCircle(
                    seed, sequence, DeterministicWeaponRandom.CameraRecoilStream) * view.RecoilVariance;
                view.AddRecoil(mouseRecoil, recoilJitter);
            }
        }
    }

    [ServerRpc]
    private void ShootServerRpc(uint requestedSequence)
    {
        TryShootServer(requestedSequence);
    }

    private void TryShootServer(uint requestedSequence)
    {
        if (bullets > 0)
        {
            if (timeToFire && !isReloading)
            {
                uint sequence = shotsFired.Value;
                uint seed = weaponSeed.Value;

                ShootAction(true, seed, sequence);

                --bullets;
                timeToFire = false;
                shotsFired.Value = sequence + 1u;

                ShootClientRpc(seed, sequence, bullets);
                ConfirmShotTargetRpc(Owner, requestedSequence, true, seed, sequence, sequence + 1u, bullets);
                return;
            }
        }
        else
        {
            ReloadServerRPC(0);
        }

        ConfirmShotTargetRpc(
            Owner,
            requestedSequence,
            false,
            weaponSeed.Value,
            shotsFired.Value,
            shotsFired.Value,
            bullets);
    }

    [ObserversRpc(ExcludeOwner = true)]
    private void ShootClientRpc(uint seed, uint sequence, int remainingBullets)
    {
        // The host already performed the action in the server world.
        if (IsServerInitialized)
            return;

        bullets = remainingBullets;
        _predictedShotsFired = sequence + 1u;
        ShootAction(false, seed, sequence);
    }

    [TargetRpc]
    private void ConfirmShotTargetRpc(
        NetworkConnection connection,
        uint requestedSequence,
        bool accepted,
        uint seed,
        uint authoritativeSequence,
        uint nextSequence,
        int remainingBullets)
    {
        if (IsServerInitialized)
            return;

        bool wasAcceptedPrediction = accepted &&
                                     requestedSequence == authoritativeSequence &&
                                     _predictedSequences.Remove(requestedSequence);

        if (wasAcceptedPrediction)
        {
            // Several automatic-fire requests may be in flight. Never roll the
            // local prediction counter or ammo back when acknowledging an older shot.
            if (IsSequenceNewer(nextSequence, _predictedShotsFired))
                _predictedShotsFired = nextSequence;
        }
        else
        {
            bullets = remainingBullets;
            _predictedShotsFired = nextSequence;
            _predictedSequences.Clear();
        }

        if (!wasAcceptedPrediction)
        {
            Debug.LogWarning(
                $"Gun {GunId} shot prediction resynced. Requested {requestedSequence}, " +
                $"server sequence {authoritativeSequence}, seed {seed}.");
        }
    }

    private static bool IsSequenceNewer(uint candidate, uint current) =>
        unchecked((int)(candidate - current)) > 0;

    public void Reload()
    {
        ReloadServerRPC(0);

        if (!IsServerInitialized)
        {
            ReloadAction(false);
        }
    }

    private void ReloadAction(bool isRealAction)
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

    [ServerRpc]
    private void ReloadServerRPC(ulong initialtor)
    {
        ReloadAction(true);
        ReloadClientRPC(initialtor);
    }

    [ObserversRpc(ExcludeOwner = true)]
    private void ReloadClientRPC(ulong initialtor)
    {
        //if (initialtor != NetworkManager.LocalClientId)
        {
            ReloadAction(false);
        }
    }
}
