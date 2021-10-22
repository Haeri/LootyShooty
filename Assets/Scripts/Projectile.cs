using UnityEngine;
using Unity.Netcode;

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

    private void OnCollisionEnter(Collision collision)
    {
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
                damage = (int)(damage * multiplier);
                Debug.Log($"Player({shooter.GetComponent<NetworkPlayer>().playerName.Value}) shot {dmg.name}. {dmg.health.Value}hp -> {dmg.health.Value - damage}hp." + (multiplier != 1 ? $" Multiplier({multiplier})" : ""));
                bool died = dmg.TakeDamage(damage);
                shooter?.GetComponent<PlayerController>()?.enemyHitCallback(dmg.gameObject, damage, died);                
            }

            // Display hit FX
            if (dmg.hitEffect != null)
            {
                GameObject effect = ObjectPool.getInstance().instanciate(dmg.hitEffect);
                effect.transform.localPosition = collision.contacts[0].point;
                effect.transform.localRotation = Quaternion.LookRotation(collision.contacts[0].normal);
                //effect.transform.SetParent(collision.transform);
            }
        }


        Vector3 norm = collision.contacts[0].normal;
        Vector3 vel = -lastVelocity.normalized;

        // Penetrate
        if (dmg != null && dmg.isPenetrable && penetrationCount < maxPenetration)
        {
            //Debug.Log("Penetrate");
            Physics.IgnoreCollision(GetComponent<Collider>(), collision.collider);
            rb.velocity = rb.velocity * 0.8f;
            penetrationCount++;
        }
        // Deflect
        else if (Vector3.Angle(vel, norm) > (90 - deflectionAngle))
        {
            //Debug.Log("Deflect");
            float strength = lastVelocity.magnitude;
            rb.velocity = Vector3.Reflect(-vel, norm) * strength;

            Debug.DrawRay(collision.contacts[0].point, norm*0.5f, Color.cyan, 10);
            Debug.DrawRay(collision.contacts[0].point, vel * 0.5f, Color.green, 10);
            Debug.DrawRay(collision.contacts[0].point, rb.velocity.normalized * 0.5f, Color.red, 10);
        }
        // Absorb
        else
        {
            //Debug.Log("Absorb");
            gameObject.SetActive(false);
            //ObjectPool.getInstance().resetObject(gameObject);

            GameObject hole = ObjectPool.getInstance().instanciate(bulletHole);
            hole.transform.localPosition = (collision.contacts[0].point + norm * 0.01f);
            hole.transform.localRotation = Quaternion.LookRotation(collision.contacts[0].normal);
            //hole.transform.SetParent(collision.transform);
        }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        lastVelocity = rb.velocity;
    }
}