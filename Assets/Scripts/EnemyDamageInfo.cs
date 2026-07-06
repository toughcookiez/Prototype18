using UnityEngine;

public readonly struct EnemyDamageInfo
{
    public readonly float DamageAmount;
    public readonly EnemyBodyPart BodyPart;
    public readonly Vector3 HitPoint;

    public EnemyDamageInfo(float damageAmount, EnemyBodyPart bodyPart, Vector3 hitPoint)
    {
        DamageAmount = damageAmount;
        BodyPart = bodyPart;
        HitPoint = hitPoint;
    }
}
