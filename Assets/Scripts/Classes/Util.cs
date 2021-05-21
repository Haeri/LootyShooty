using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Util
{
    public static Vector3 randomVec3(Vector3 min, Vector3 max)
    {
        float x = Random.Range(min.x, max.x);
        float y = Random.Range(min.y, max.y);
        float z = Random.Range(min.z, max.z);

        return new Vector3(x, y, z);
    }

    public static Vector3 vec3FromRandomAngle(Vector3 centerVector, float angle)
    {
        Vector3 tempDir = centerVector;

        tempDir = tempDir.normalized;
        Vector3 crossDir = Vector3.Cross(tempDir, Vector3.up);
        crossDir = Quaternion.AngleAxis(Random.Range(0.0f, 360.0f), tempDir) * crossDir;
        crossDir = Random.Range(0.0f, angle) * crossDir;
        tempDir += crossDir;

        return tempDir;
    }
}
