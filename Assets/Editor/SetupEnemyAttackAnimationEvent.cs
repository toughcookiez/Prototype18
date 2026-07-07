using UnityEditor;
using UnityEngine;

public class SetupEnemyAttackAnimationEvent
{
    [MenuItem("Tools/Setup Enemy Attack Animation Event")]
    public static void SetupAttackAnimationEvent()
    {
        // Load the Attack animation clip
        AnimationClip attackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Attack.anim");

        if (attackClip == null)
        {
            Debug.LogError("Attack.anim not found at Assets/Animations/Attack.anim");
            return;
        }

        // Get existing events
        AnimationEvent[] existingEvents = AnimationUtility.GetAnimationEvents(attackClip);

        // Check if DealAttackDamage event already exists
        foreach (AnimationEvent evt in existingEvents)
        {
            if (evt.functionName == "DealAttackDamage")
            {
                Debug.Log("DealAttackDamage animation event already exists on Attack.anim");
                return;
            }
        }

        // Create new animation event
        AnimationEvent damageEvent = new AnimationEvent();
        damageEvent.functionName = "DealAttackDamage";
        
        // Set event to fire at 50% through the animation (mid-swing)
        float animationLength = attackClip.length;
        damageEvent.time = animationLength * 0.5f;
        
        // Combine with existing events
        AnimationEvent[] allEvents = new AnimationEvent[existingEvents.Length + 1];
        allEvents[0] = damageEvent;
        System.Array.Copy(existingEvents, 0, allEvents, 1, existingEvents.Length);
        
        // Add the event to the animation clip
        AnimationUtility.SetAnimationEvents(attackClip, allEvents);

        // Mark clip as dirty so changes are saved
        EditorUtility.SetDirty(attackClip);
        
        Debug.Log($"Added DealAttackDamage animation event to Attack.anim at {damageEvent.time:F2}s (50% of animation)");
    }
}
