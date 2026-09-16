using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/*
 * ResourceNode is a harvestable object like a tree or stone. Replicated health
 * counts down as players hit it with the right tool; at zero the server spawns
 * resource pickups and every client hides the depleted node.
 */

public class ResourceNode : Interactable
{
    [SerializeField] List<ObjectType> _toolTypeRequired = new();
    [SerializeField] NetworkObject _producedPrefab;
    [SerializeField] int _amountToSpawn = 3;
    [SerializeField] int _startingHealth = 1;
    [SerializeField] AudioClip _audioClip;

    readonly NetworkVariable<int> _health = new();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // TODO Slice 8.1: on the server, set health to _startingHealth. 
        // NOTE: make sure  NetworkObject.Despawn(!NetworkObject.InScenePlaced); is done
        // Next: Slice 8.2 CanInteract.

        if(!IsServer) return;
        _health.Value = _startingHealth;
        
        // TODO Slice 8.5: subscribe to health changes and apply the current health.
        // Check: both windows hide a depleted tree. A late joiner sees it hidden.
        
        _health.OnValueChanged += HandleHealthChanged;
        
        // Next: Slice 8.6 OnNetworkDespawn.
        
        // Apply correct state for late joiners
        bool isDepleted = _health.Value <= 0;
        foreach (var renderer in GetComponentsInChildren<Renderer>())
            renderer.enabled = !isDepleted;
        foreach (var collider in GetComponentsInChildren<Collider>())
            collider.enabled = !isDepleted;
    }

    public override void OnNetworkDespawn()
    {

        // TODO Slice 8.6: unsubscribe from replicated health changes.
        _health.OnValueChanged -= HandleHealthChanged;
        
        // </> end of Slice 8
        // Next: Slice 9.1 in World/Receptacle.cs.

        base.OnNetworkDespawn();
    }

    public override bool CanInteract(ObjectType heldType)
    {

        // TODO Slice 8.2:
        // 1. Require a living node.
        if(_health.Value <= 0) return false;
        
        // 2. Require an accepted tool.
        return _toolTypeRequired.Contains(heldType);
        
        // Check: hold the axe. The tree highlights. Empty-handed, it does not.

        // Next: Slice 8.3 Interact and HitFeedbackRpc.

        return false;
    }

    protected override void Interact(PlayerHeldItem heldItem)
    {
        if(!IsServer) return;
        
        // TODO Slice 8.3:
        // 1. Reduce health.
        _health.Value--; // By one I'm assuming

        // 2. Call HitFeedbackRpc.
        HitFeedbackRpc();
        
        // 3. Spawn _amountToSpawn copies of _producedPrefab with InstantiateAndSpawn.
        // 4. Place each with a small random XZ offset and random yaw.
        // Check: axe the tree. Wood appears. The mesh is still there until 8.4.

        // Next: Slice 8.4 HandleHealthChanged.

    }

    [Rpc(SendTo.ClientsAndHost)]
    void HitFeedbackRpc()
    {
        // TODO Slice 8.6: play the authored hit sound on each observer.
        
        
    }

    void HandleHealthChanged(int previousValue, int newValue)
    {

        // TODO Slice 8.4: make the visuals and physics match the health.
        
        bool isDepleted = newValue <= 0;
        
        // Disable or enable renderers and colliders based on health
        foreach (var renderer in GetComponentsInChildren<Renderer>())
            renderer.enabled = !isDepleted;
        foreach (var collider in GetComponentsInChildren<Collider>())
            collider.enabled = !isDepleted;

        // Next: Slice 8.5 in OnNetworkSpawn — subscribe and apply.

    }
}
