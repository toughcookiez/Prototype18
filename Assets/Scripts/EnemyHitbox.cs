using UnityEngine;

public class EnemyHitbox : MonoBehaviour
{
    [SerializeField]
    Enemy owner;

    public EnemyBodyPart bodyPart = EnemyBodyPart.Chest;

    public Enemy Owner => owner;

    public EnemyBodyPart BodyPart => bodyPart;

    void Awake()
    {
        EnsureOwnerReference();
    }

    void Reset()
    {
        EnsureOwnerReference();
    }

    void OnValidate()
    {
        EnsureOwnerReference();
    }

    void EnsureOwnerReference()
    {
        if (owner == null)
        {
            owner = GetComponentInParent<Enemy>();
        }
    }
}
