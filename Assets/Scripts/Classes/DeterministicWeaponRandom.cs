using UnityEngine;

/// <summary>
/// Stateless weapon randomness with a stable integer hash. A sample depends only on the
/// weapon seed, accepted shot sequence, and stream id; it never touches
/// UnityEngine.Random's global state.
/// </summary>
public static class DeterministicWeaponRandom
{
    public const uint SpreadStream = 0xA511E9B3u;
    public const uint KickbackStream = 0x63D83595u;
    public const uint CameraRecoilStream = 0xC2B2AE35u;

    public static uint CreateWeaponSeed(uint networkObjectId)
    {
        uint seed = Hash(networkObjectId ^ 0x9E3779B9u);
        return seed != 0u ? seed : 1u;
    }

    public static Vector2 InsideUnitCircle(uint weaponSeed, uint shotSequence, uint stream)
    {
        float angle = Value01(weaponSeed, shotSequence, stream) * Mathf.PI * 2f;
        float radius = Mathf.Sqrt(Value01(weaponSeed, shotSequence, stream + 1u));
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    public static float Value01(uint weaponSeed, uint shotSequence, uint stream)
    {
        uint value = weaponSeed;
        value ^= Hash(unchecked(shotSequence + 0x9E3779B9u));
        value ^= Hash(stream);
        value = Hash(value);

        // Use the high 24 bits so every result is represented exactly by a float.
        return (value >> 8) * (1f / 16777216f);
    }

    private static uint Hash(uint value)
    {
        unchecked
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }
    }
}
