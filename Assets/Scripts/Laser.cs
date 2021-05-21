using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class Laser : MonoBehaviour
{
    public float maxDistance = 2000;

    private LineRenderer lineRenderer;
    private Transform pointer;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        pointer = transform.GetChild(1);
    }

    private void LateUpdate()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, maxDistance))
        {
            setPointer(new Vector3(0.0f, 0.0f, hit.distance));
        }
        else
        {
            setPointer(new Vector3(0.0f, 0.0f, maxDistance));
        }
    }

    private void setPointer(Vector3 position)
    {
        lineRenderer.SetPosition(1, position);
        pointer.localPosition = position;
    }
}
