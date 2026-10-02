using Unity.Netcode;
using UnityEngine;

public class AxeVfxController : NetworkBehaviour
{
    [Header("Trail")]
    [SerializeField] TrailRenderer _trail;

    [Header("Particles")]
    [SerializeField] ParticleSystem _ambientParticles;
    [SerializeField] ParticleSystem _impactParticles;
    [SerializeField] ParticleSystem _catchParticles;

    readonly NetworkVariable<bool> _isHeld = new();
    readonly NetworkVariable<bool> _isFlying = new();
    readonly NetworkVariable<bool> _isStuck = new();

    void Awake()
    {
        if (_trail == null)
            _trail = GetComponent<TrailRenderer>();

        StopAllEffects();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        _isHeld.OnValueChanged += OnStateChanged;
        _isFlying.OnValueChanged += OnStateChanged;
        _isStuck.OnValueChanged += OnStateChanged;

        RefreshEffects();
    }

    public override void OnNetworkDespawn()
    {
        _isHeld.OnValueChanged -= OnStateChanged;
        _isFlying.OnValueChanged -= OnStateChanged;
        _isStuck.OnValueChanged -= OnStateChanged;

        base.OnNetworkDespawn();
    }

    public void SetHeld(bool held)
    {
        if (!IsServer)
            return;

        _isHeld.Value = held;

        if (held)
            _isStuck.Value = false;
    }

    public void SetFlying(bool flying)
    {
        if (!IsServer)
            return;

        _isFlying.Value = flying;

        // Once the axe starts moving again, it is no longer stuck
        if (flying)
            _isStuck.Value = false;
    }

    public void SetStuck(bool stuck)
    {
        if (!IsServer)
            return;

        _isStuck.Value = stuck;
    }

    public void PlayImpactBurst()
    {
        if (!IsServer)
            return;

        PlayImpactBurstClientRpc();
    }

    public void PlayCatchBurst()
    {
        if (!IsServer)
            return;

        PlayCatchBurstClientRpc();
    }

    [ClientRpc]
    void PlayImpactBurstClientRpc()
    {
        if (_impactParticles != null)
            _impactParticles.Play(true);
    }

    [ClientRpc]
    void PlayCatchBurstClientRpc()
    {
        if (_catchParticles != null)
            _catchParticles.Play(true);
    }

    void OnStateChanged(bool previousValue, bool newValue)
    {
        RefreshEffects();
    }

    void RefreshEffects()
    {
        if (_trail != null)
        {
            // The trail only appears while the axe is flying or returning
            _trail.emitting = _isFlying.Value;

            if (!_isFlying.Value)
                _trail.Clear();
        }

        // Ambient particles remain active while held, flying, or stuck
        bool ambientShouldPlay =
            _isHeld.Value ||
            _isFlying.Value ||
            _isStuck.Value;

        if (_ambientParticles == null)
            return;

        if (ambientShouldPlay)
        {
            if (!_ambientParticles.isPlaying)
                _ambientParticles.Play(true);
        }
        else
        {
            _ambientParticles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }
    }

    void StopAllEffects()
    {
        if (_trail != null)
        {
            _trail.emitting = false;
            _trail.Clear();
        }

        if (_ambientParticles != null)
        {
            _ambientParticles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }

        if (_impactParticles != null)
        {
            _impactParticles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }

        if (_catchParticles != null)
        {
            _catchParticles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }
    }
}