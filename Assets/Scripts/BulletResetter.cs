using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletResetter : IPoolInstanceResetter
{
    public override void reset()
    {
        GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
        GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
        GetComponent<TrailRenderer>().Clear();
    }
}
