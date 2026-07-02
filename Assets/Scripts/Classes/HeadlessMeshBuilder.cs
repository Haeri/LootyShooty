using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a copy of a skinned mesh with every triangle removed that is
/// skinned to the head bone (or any of its children). Used for the local
/// player's first-person body so the head never intersects the camera,
/// without touching bones or colliders.
/// </summary>
public static class HeadlessMeshBuilder
{
    /// <param name="renderer">Renderer whose sharedMesh is copied. The mesh must be Read/Write enabled.</param>
    /// <param name="headBoneName">Exact name of the head bone in the renderer's bone array.</param>
    /// <param name="weightThreshold">
    /// A triangle is removed when any of its vertices carries more than this
    /// much combined weight on the head bones. Lower values remove more of
    /// the neck area; higher values leave more geometry near the head.
    /// </param>
    /// <returns>The headless mesh copy, or null when the head bone was not found.</returns>
    public static Mesh Build(SkinnedMeshRenderer renderer, string headBoneName, float weightThreshold)
    {
        Mesh source = renderer != null ? renderer.sharedMesh : null;
        if (source == null)
            return null;

        // Collect the head bone and everything parented under it (eyes, HeadTop_End, ...).
        Transform[] bones = renderer.bones;
        Transform headBone = null;
        foreach (Transform bone in bones)
        {
            if (bone != null && bone.name == headBoneName)
            {
                headBone = bone;
                break;
            }
        }

        if (headBone == null)
            return null;

        HashSet<int> headBoneIndices = new HashSet<int>();
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] != null && (bones[i] == headBone || bones[i].IsChildOf(headBone)))
                headBoneIndices.Add(i);
        }

        // Total head influence per vertex.
        BoneWeight[] weights = source.boneWeights;
        float[] headWeight = new float[weights.Length];
        for (int i = 0; i < weights.Length; i++)
        {
            BoneWeight w = weights[i];
            float total = 0f;
            if (headBoneIndices.Contains(w.boneIndex0)) total += w.weight0;
            if (headBoneIndices.Contains(w.boneIndex1)) total += w.weight1;
            if (headBoneIndices.Contains(w.boneIndex2)) total += w.weight2;
            if (headBoneIndices.Contains(w.boneIndex3)) total += w.weight3;
            headWeight[i] = total;
        }

        // Copy the mesh (keeps vertices, weights, bindposes and blendshapes
        // intact) and rebuild only the index buffers without head triangles.
        Mesh headless = Object.Instantiate(source);
        headless.name = source.name + " (headless)";

        List<int> kept = new List<int>();
        for (int sub = 0; sub < source.subMeshCount; sub++)
        {
            int[] tris = source.GetTriangles(sub);
            kept.Clear();
            for (int t = 0; t < tris.Length; t += 3)
            {
                if (headWeight[tris[t]] > weightThreshold ||
                    headWeight[tris[t + 1]] > weightThreshold ||
                    headWeight[tris[t + 2]] > weightThreshold)
                    continue;

                kept.Add(tris[t]);
                kept.Add(tris[t + 1]);
                kept.Add(tris[t + 2]);
            }

            headless.SetTriangles(kept, sub, calculateBounds: false);
        }

        return headless;
    }
}
