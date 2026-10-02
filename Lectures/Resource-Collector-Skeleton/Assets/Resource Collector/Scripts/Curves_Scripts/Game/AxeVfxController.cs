using Unity.Netcode;
using UnityEngine;

public class AxeVfxController : NetworkBehaviour
{
    [SerializeField] TrailRenderer _trail;

    readonly NetworkVariable<bool> _isFlying = new();

    void Awake()
    {
        if (_trail == null)
            _trail = GetComponent<TrailRenderer>();

        SetTrail(false);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        _isFlying.OnValueChanged += HandleFlightStateChanged;
        HandleFlightStateChanged(false, _isFlying.Value);
    }

    public override void OnNetworkDespawn()
    {
        _isFlying.OnValueChanged -= HandleFlightStateChanged;
        base.OnNetworkDespawn();
    }

    public void SetFlying(bool flying)
    {
        if (!IsServer)
            return;

        _isFlying.Value = flying;
    }

    void HandleFlightStateChanged(bool previousValue, bool newValue)
    {
        SetTrail(newValue);
    }

    void SetTrail(bool enabled)
    {
        if (_trail == null)
            return;

        _trail.emitting = enabled;

        if (!enabled)
            _trail.Clear();
    }
}