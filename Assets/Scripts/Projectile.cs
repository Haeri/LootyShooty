using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    public int damage = 40;
    public int maxPenetration = 1;
    public float deflectionAngle = 30.0f;
    public bool blank = false;
    public GameObject bulletHole;
    public GameObject shooter;

    private int penetrationCount;
    private Vector3 lastVelocity;
    private Rigidbody rb;
    private Collider projectileCollider;
    private readonly List<Collider> ignoredColliders = new();

    /// <summary>
    /// Marks the firing pawn and disables collision against all of its
    /// colliders so players can never run into their own bullets.
    /// </summary>
    public void SetShooter(GameObject shooterRoot)
    {
        shooter = shooterRoot;
        if (shooterRoot == null)
            return;

        foreach (Collider shooterCollider in shooterRoot.GetComponentsInChildren<Collider>(true))
        {
            Physics.IgnoreCollision(projectileCollider, shooterCollider, true);
            if (!ignoredColliders.Contains(shooterCollider))
                ignoredColliders.Add(shooterCollider);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        /* Safety net: IgnoreCollision pairs are silently cleared whenever a
         * collider is toggled (the CharacterController is, every reconcile).
         * Re-ignore and bail instead of damaging the shooter. */
        if (shooter != null && collision.transform.IsChildOf(shooter.transform))
        {
            Physics.IgnoreCollision(projectileCollider, collision.collider, true);
            if (!ignoredColliders.Contains(collision.collider))
                ignoredColliders.Add(collision.collider);
            return;
        }

        Damagable dmg = collision.gameObject.GetComponent<Damagable>();
        float multiplier = 1;
        if (dmg == null)
        {
            DamagePropagator dp = collision.gameObject.GetComponent<DamagePropagator>();
            if (dp != null)
            {
                dmg = dp.root;
                multiplier = dp.damage_multiplier;
            }
        }
        if (dmg != null)
        {
            // Do damage
            if (!blank && !dmg.IsDead())
            {
                int appliedDamage = Mathf.RoundToInt(damage * multiplier);
                // The shooter is the pawn, which carries no NetworkPlayer; it may
                // also already be destroyed by the time the bullet lands.
                string shooterName = shooter != null ? shooter.name : "unknown";
                Debug.Log($"Player({shooterName}) shot {dmg.name}. {dmg.health.Value}hp -> {dmg.health.Value - appliedDamage}hp." + (multiplier != 1 ? $" Multiplier({multiplier})" : ""));
                dmg.TakeDamage(appliedDamage);
                shooter?.GetComponent<FishController>()?.ServerNotifyHit();
            }

            // Display hit FX
            if (dmg.hitEffect != null)
            {
                ObjectPool.Instance.instanciate(
                    dmg.hitEffect,
                    collision.contacts[0].point,
                    Quaternion.LookRotation(collision.contacts[0].normal));
                //effect.transform.SetParent(collision.transform);
            }
        }


        Vector3 norm = collision.contacts[0].normal;
        Vector3 vel = -lastVelocity.normalized;

        // Penetrate
        if (dmg != null && dmg.isPenetrable && penetrationCount < maxPenetration)
        {
            //Debug.Log("Penetrate");
            Physics.IgnoreCollision(projectileCollider, collision.collider);
            if (!ignoredColliders.Contains(collision.collider))
                ignoredColliders.Add(collision.collider);
            rb.linearVelocity = rb.linearVelocity * 0.8f;
            penetrationCount++;
        }
        // Deflect
        else if (Vector3.Angle(vel, norm) > (90 - deflectionAngle))
        {
            //Debug.Log("Deflect");
            float strength = lastVelocity.magnitude;
            rb.linearVelocity = Vector3.Reflect(-vel, norm) * strength;

            Debug.DrawRay(collision.contacts[0].point, norm*0.5f, Color.cyan, 10);
            Debug.DrawRay(collision.contacts[0].point, vel * 0.5f, Color.green, 10);
            Debug.DrawRay(collision.contacts[0].point, rb.linearVelocity.normalized * 0.5f, Color.red, 10);
        }
        // Absorb
        else
        {
            //Debug.Log("Absorb");
            gameObject.SetActive(false);
            //ObjectPool.Instance.resetObject(gameObject);

            ObjectPool.Instance.instanciate(
                bulletHole,
                collision.contacts[0].point + norm * 0.01f,
                Quaternion.LookRotation(collision.contacts[0].normal));
            //hole.transform.SetParent(collision.transform);
        }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        projectileCollider = GetComponent<Collider>();
    }

    public void ResetForPool()
    {
        if (projectileCollider == null)
            projectileCollider = GetComponent<Collider>();

        foreach (Collider ignoredCollider in ignoredColliders)
        {
            if (ignoredCollider != null)
                Physics.IgnoreCollision(projectileCollider, ignoredCollider, false);
        }

        ignoredColliders.Clear();
        penetrationCount = 0;
        lastVelocity = Vector3.zero;
        blank = true;
        shooter = null;
    }

    void FixedUpdate()
    {
        lastVelocity = rb.linearVelocity;
    }
}
