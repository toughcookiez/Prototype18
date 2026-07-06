## Plan: Add Simple Enemy Movement, Attack, and Health

TL;DR - Create a new enemy script that chases the player, attacks when in range, and responds to raycast damage from the existing `WeaponHandler` by implementing `TakeDamage`.

**Steps**
1. Create `Assets/Scripts/Enemy.cs`.
   - Add public parameters: `health`, `attackRange`, `attackCooldown`, `damage`, and `playerTag`.
   - Get a reference to the `NavMeshAgent` component in `Awake`.
   - Use `Start` or `Awake` to find the player by tag if no target is assigned manually.
   - In `Update`, if the enemy is alive:
     - Calculate distance to player.
     - If outside `attackRange`, set `NavMeshAgent.SetDestination(player.position)` to chase.
     - If within `attackRange`, stop the agent (`NavMeshAgent.isStopped = true`) and, if cooldown expired, log a debug hit message and reset cooldown.
     - Resume the agent (`NavMeshAgent.isStopped = false`) when the player moves out of range again.
   - Implement `TakeDamage(float damageAmount)` to subtract health and destroy the enemy when health reaches zero.
   - Optionally add a `Die()` helper to handle enemy death.

2. Optionally create `Assets/Scripts/Health.cs` or `EnemyHealth.cs` if you prefer separate reusable health logic.
   - If created, make it implement `TakeDamage(float amount)` and death callbacks.
   - The `Enemy` script can then own or reuse that health component.

3. Configure the player and enemy in the Unity editor.
   - Tag the player GameObject as `Player`.
   - **Bake a NavMesh** on the scene geometry (Window > AI > Navigation > Bake).
   - Add a `NavMeshAgent` component to the enemy GameObject and tune `speed`, `stoppingDistance`, and `angularSpeed`.
   - Attach `Enemy.cs` to the enemy GameObject or prefab.
   - Set `health` to the desired number of shots it should take and `attackRange` to the desired melee distance.
   - Confirm `WeaponHandler` is attached to the player and `fpsCamera` is assigned.

4. Verify behavior.
   - Play the scene, confirm the enemy moves toward the player.
   - When the enemy reaches `attackRange`, confirm a debug log appears for each attack.
   - Shoot the enemy and verify it dies after the expected number of hits.

**Relevant files**
- `Assets/Scripts/WeaponHandler.cs` — already performs raycast damage via `SendMessage("TakeDamage", damage)`.
- `Assets/Scripts/Enemy.cs` — new behavior to add.

**Verification**
1. Run the scene and observe the enemy closing the gap to the player.
2. Watch the console for debug attack messages when the enemy reaches melee range.
3. Shoot the enemy and verify it dies after the configured health is depleted.

**Decisions**
- Use Unity's built-in `NavMeshAgent` for pathfinding so the enemy navigates around obstacles automatically.
- Use `SendMessage("TakeDamage")` compatibility with existing `WeaponHandler`.
- Use a `Player` tag to locate the player if no reference is assigned manually.

**Further Considerations**
1. If you want future AI behavior (patrol, flee, etc.), add `NavMeshAgent.SetDestination` calls for each state — NavMesh makes this straightforward.
2. If you want the enemy to deal actual player damage later, replace the debug log inside the attack branch with a call to player health logic.