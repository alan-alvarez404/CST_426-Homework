using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/*
 * PlayerController is the owner's local input loop: movement, target
 * selection, and the client-to-server interaction request. The server
 * still owns every world mutation.
 */

public class PlayerController : NetworkBehaviour
{
    // Animator Components
    static readonly int Speed = Animator.StringToHash("Speed");
    static readonly int ThrowHash = Animator.StringToHash("Throw");
    
    [Header("Components")]
    [SerializeField] CharacterController _characterController;
    [SerializeField] Animator _animator;
    [SerializeField] PlayerHeldItem _heldItem;

    [Header("Detection")]
    [SerializeField] float _detectionRadius = 3f;
    [SerializeField] float _detectionAngle = 60f;
    [SerializeField] LayerMask _pickupLayer;

    [Header("Movement")]
    [SerializeField] float _movementSpeed = 4f;
    [SerializeField] float _rotationSpeed = 200f;
    
    // For the line that shows up when holding the axe
    [Header("Aiming")]
    [SerializeField] LineRenderer _aimLine;
    [SerializeField] Transform _aimOrigin;
    //[SerializeField] float _aimDistance = 10f;
    //[SerializeField] private LayerMask _aimLayer = ~0;
    
    // Needed for throwing the axe
    [Header("Axe")] 
    public ThrownAxe axe;
    public float throwImpulse = 25f;
    public float returnDuration = 1f;
    public float bowAmount = 0.5f;
    
    // For syncing the axe across different players
    [SerializeField] NetworkObject _thrownAxePrefab;
    [SerializeField] Transform _axeHand;
    ThrownAxe _activeThrownAxe;
    readonly NetworkVariable<bool> _axeIsAway = new();

    enum AxeState { Held, Throwing, Away, Returning }
    
    Interactable _closestTarget;
    Vector2 _smoothedInput;
    AxeState _axeState = AxeState.Held;
    
    // For debug
    bool _lastAimHit;
    bool _hasAimHitState;

    void Awake()
    {
        if (_aimLine == null)
            _aimLine = GetComponent<LineRenderer>();

        if (_aimLine != null)
        {
            _aimLine.useWorldSpace = true;
            _aimLine.positionCount = 0;
        }

        int upperBodyLayer = _animator.GetLayerIndex("Upper Body");

        if (upperBodyLayer >= 0)
            _animator.SetLayerWeight(upperBodyLayer, 1f);
        Debug.Log($"Upper Body layer weight: " + $"{_animator.GetLayerWeight(upperBodyLayer)}");
    }
    
    void Update()
    {
        ApplyAxeVisual();

        if (!IsOwner) return;

        // TODO Slice 2.2: read this owner's movement in Update. Done?
        Vector2 movementInput = ReadMovementInput();

        // TODO Slice 2.5: smooth _smoothedInput toward the raw input so the walk cycle does not pop.
        _smoothedInput = Vector2.MoveTowards(_smoothedInput, movementInput, Time.deltaTime * 10f);
        
        // TODO Slice 2.3: rotate and move forward/back.
        transform.Rotate(Vector3.up, _smoothedInput.x * _rotationSpeed * Time.deltaTime);

        Vector3 motion = _characterController.transform.forward * _smoothedInput.y * _movementSpeed * Time.deltaTime;
        _characterController.Move(motion);
        
        // TODO Slice 2.4: set the "Speed" animator float so walk speed matches input.
        _animator.SetFloat("Speed", _characterController.velocity.magnitude);

        // Make sure the animator receives an update for the axe being held
        bool axeIsHeldOrThrowing = _heldItem.ObjectType == ObjectType.Axe && (_axeState == AxeState.Held || _axeState == AxeState.Throwing);
        _animator.SetBool("IsAxeHeld", axeIsHeldOrThrowing);
        
        UpdateInteractionTarget();
        UpdateAxeInput(); // For throwing the axe
        UpdateAimVisual(); // For the targeting aim line
        
        // TODO Slice 6.2: detect a target and request interaction on E or left-click.
        // Check: Play Mode, Host, highlight the axe, press E.
        // The Interact clip plays. The axe stays on the ground.
        if (Keyboard.current.eKey.wasPressedThisFrame || Mouse.current.leftButton.wasPressedThisFrame)
        {
            HandleInteractionPressed();
        }
        
    }


    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        _axeIsAway.OnValueChanged += HandleAxeAwayChanged;
        HandleAxeAwayChanged(false, _axeIsAway.Value);

        // TODO Slice 2.6: make the main camera follow only its local player. </> end of Slice 2
        if (IsOwner)
            Camera.main.GetComponent<FollowCamera>().Target = transform;
        else if (_aimLine != null)
            _aimLine.positionCount = 0;
    }

    public override void OnNetworkDespawn()
    {
        _axeIsAway.OnValueChanged -= HandleAxeAwayChanged;

        if (IsOwner)
        {
            // TODO Slice 5.2: turn off the current target's Highlightable,
            // then clear _closestTarget.
            ClearSelection();
        }

        base.OnNetworkDespawn();
    }

    
    // =================================================================================================================
    // These are all the functions related to axe throwing
    
    void UpdateAxeInput()
    {
        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            bool isAxeHeld = _animator.GetBool("IsAxeHeld");

            Debug.Log(
                $"T pressed | Owner: {IsOwner} | " +
                $"Item: {_heldItem.ObjectType} | " +
                $"Axe State: {_axeState} | " +
                $"IsAxeHeld: {isAxeHeld}"
            );
        }

        if (_axeState == AxeState.Held &&
            _heldItem.ObjectType == ObjectType.Axe &&
            Keyboard.current.tKey.wasPressedThisFrame)
        {
            _animator.SetTrigger(ThrowHash);
            _axeState = AxeState.Throwing;
            Debug.Log("Throw trigger sent");
        }

        if (_axeState == AxeState.Away &&
            Mouse.current.rightButton.wasPressedThisFrame)
        {
            RequestReturnAxeServerRpc();
        }
    }

    public void LaunchAxe()
    {
        if (!IsOwner) return;

        RequestLaunchAxeServerRpc();
    }

    IEnumerator ReturnAxeServer()
    {
        _axeState = AxeState.Returning;

        ThrownAxe returningAxe = _activeThrownAxe;
        if (returningAxe == null)
            yield break;

        AxeVfxController returningAxeVfx = returningAxe.GetComponent<AxeVfxController>();
        
        returningAxe.rigidbody.isKinematic = true;
        returningAxe.axeCollider.enabled = false;
        
        // Axe is flying back to player, trail and flying particles stay active
        returningAxeVfx?.SetFlying(true);
        
        // TODO Slice 8.3 (recall hook): start visual spin for the return.
        // Next: the Slice 8.3 catch hook in ThrownAxe.AttachToHand.

        Vector3 start = returningAxe.transform.position;
        // TODO Slice 5.3: advance recall progress from 0 to 1 over returnDuration,
        // one step per frame, replacing the one-frame wait below.
        // The catch runs only after progress reaches 1.
        // Check: a stuck or mid-flight axe waits returnDuration, then snaps to the hand.
        // Next: Slice 5.4 below.

        // TODO Slice 5.4: each frame, place the axe on the GetReturnControlPoints curve
        // at the recall progress. The basic curve can stay fixed for the whole recall.
        // Check: standing still, recall follows the preview's bow at two bowAmount values.
        // Two full throw-and-recall cycles work, and so does recall mid-flight.
        // Next: optional Slice 5.5 below, or open Demo, Slice 6.1 in
        // Bezier/QuadraticBezierMath.cs. </> end of Slice 5

        // TODO Slice 5.5 (optional): keep the start fixed; let the handle and end
        // follow the moving hand.
        // Check: turn during recall. The axe still lands in the animated grip.
        // Next: open Demo, Slice 6.1 in Bezier/QuadraticBezierMath.cs.
        float elapsed = 0f;
        do
        {
            float t = elapsed / returnDuration;
            Vector3 p0 = start;
            Vector3 p2 = _axeHand.position;
            Vector3 p1 = (p0 + p2) * 0.5f + transform.right * bowAmount;
            
            returningAxe.transform.position = QuadraticBezierMath.SamplePointBernstein(p0, p1, p2, t);
            returningAxe.transform.Rotate(Vector3.forward, returningAxe.spinSpeed * Time.deltaTime, Space.Self);
            
            yield return null;
            elapsed += Time.deltaTime;
            
        }
        while (elapsed < returnDuration);

        // Axe has reached player's hand
        returningAxeVfx?.SetFlying(false);

        // Particle burst when axe is caught
        returningAxeVfx?.PlayCatchBurst();

        // Particle burst display briefly before despawning thrown axe
        // yield return new WaitForSeconds(0.15f);
        
        NetworkObject thrownObject = returningAxe.GetComponent<NetworkObject>();
        if (thrownObject != null && thrownObject.IsSpawned)
            thrownObject.Despawn(true);

        _activeThrownAxe = null;
        _axeIsAway.Value = false;
        _axeState = AxeState.Held;
        
    }

    void ApplyAxeVisual()
    {
        if (axe == null || _heldItem == null)
            return;

        if (_heldItem.ObjectType == ObjectType.Axe)
            axe.gameObject.SetActive(!_axeIsAway.Value);
    }

    void HandleAxeAwayChanged(bool previousValue, bool newValue)
    {
        _axeState = newValue ? AxeState.Away : AxeState.Held;
        ApplyAxeVisual();
    }

    (Vector3 p0, Vector3 p1, Vector3 p2) GetReturnControlPoints(Vector3 start)
    {
        Vector3 end = axe.CatchPosition;
        // TODO Slice 5.1: bow the return curve sideways to the axe-to-hand direction.
        // bowAmount controls how far.
        // Next: Slice 5.2 in DrawReturnPath, where you can see the bow.
        Vector3 direction = end - start;
        Vector3 sideways = Vector3.Cross(Vector3.up, direction);
        if (sideways.sqrMagnitude > 0.000001f)
        {
            sideways.Normalize();
        }
        
        Vector3 middle = (start + end) * 0.5f;
        return (start, middle, end);
    }
    
    // =================================================================================================================
    
    
    // =================================================================================================================
    // These are all the functions related to drawing the aiming line when wielding the axe
    
    // Switch statement with different states of the axe in the world (for drawing the aiming line visual)
    void UpdateAimVisual()
    {
        if (_aimLine == null) return;

        switch (_axeState)
        {
            case AxeState.Held:
                DrawHeldAimLine();
                break;

            case AxeState.Away:
                DrawReturnAimLine();
                break;

            default:
                _aimLine.positionCount = 0;
                break;
        }
    }
    
    // Drawing the aim line when axe is held
    void DrawHeldAimLine()
    {
        if (_heldItem.ObjectType != ObjectType.Axe ||
            _aimOrigin == null)
        {
            _aimLine.positionCount = 0;
            ReportAimHit(false);
            return;
        }

        Vector3 origin = _aimOrigin.position;
        Vector3 direction = transform.forward;

        bool hitObject = Physics.Raycast(
            origin,
            direction,
            out RaycastHit hit
        );

        ReportAimHit(hitObject);

        if (!hitObject)
        {
            _aimLine.positionCount = 0;
            return;
        }

        _aimLine.positionCount = 2;
        _aimLine.SetPosition(0, origin);
        _aimLine.SetPosition(1, hit.point);
    }
    
    // The path that's drawn when the axe returns
    void DrawReturnAimLine()
    {
        if (axe == null || !axe.gameObject.activeInHierarchy)
        {
            _aimLine.positionCount = 0;
            return;
        }

        Vector3 start = axe.transform.position;
        Vector3 end = axe.CatchPosition;
        Vector3 middle =
            (start + end) * 0.5f +
            transform.right * bowAmount;

        const int samples = 10;
        _aimLine.positionCount = samples;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / (samples - 1);

            Vector3 point =
                QuadraticBezierMath.SamplePointBernstein(
                    start,
                    middle,
                    end,
                    t
                );

            _aimLine.SetPosition(i, point);
        }
    }
    
    // =================================================================================================================
    
    // Just print whenever the line hits
    void ReportAimHit(bool hitObject)
    {
        if (_hasAimHitState && _lastAimHit == hitObject) return;

        _lastAimHit = hitObject;
        _hasAimHitState = true;
        Debug.Log($"Line hit object: {hitObject}");
    }
    
    void HandleInteractionPressed()
    {
        if (!IsOwner) return;

        // TODO Slice 6.1: if there is no target, return. Otherwise fire the
        // Animator's "Interact" trigger and send the target's NetworkObjectId
        // to the server.

        if (_closestTarget == null) return;
        
        _animator.SetTrigger("Interact");
        
        RequestInteractRpc(_closestTarget.NetworkObjectId);
        
    }

    static Vector2 ReadMovementInput()
    {
        // TODO Slice 2.1: return WASD input as a two-dimensional vector.
        Vector2 movementInput = Vector2.zero;
        movementInput.x += Keyboard.current.aKey.isPressed ? -1f : 0f;
        movementInput.x += Keyboard.current.dKey.isPressed ? 1f : 0f;
        movementInput.y += Keyboard.current.wKey.isPressed ? 1f : 0f;
        movementInput.y += Keyboard.current.sKey.isPressed ? -1f : 0f;

        
        return movementInput;
    }

    void UpdateInteractionTarget()
    {
        // TODO Slice 5.1: find the closest valid Interactable in front of the player.
        // When the target changes, clear the old highlight and select the new one.
        
        Interactable interactable = FindClosestValidInteractable();

        if (interactable == _closestTarget) return;
        
        ClearSelection();

        if (interactable != null)
        {
            _closestTarget = interactable;
            _closestTarget.GetComponent<Highlightable>().SetHighlighted(true);
        }

        // 1. Detect nearby objects with Physics.OverlapSphere, using
        //    _detectionRadius and _pickupLayer.
        // 2. Check each hit and keep the closest Interactable within _detectionAngle.
        //    Ignore hits without an Interactable or whose
        //    CanInteract(_heldItem.ObjectType) returns false.
        // 3. If the closest candidate is still _closestTarget, nothing changed; return.
        // 4. Otherwise, remove the old highlight, store the new candidate, and
        //    highlight it (if there is one).
    }

    Interactable FindClosestValidInteractable()
    {
        Collider[] candidates = Physics.OverlapSphere(transform.position, _detectionRadius, _pickupLayer);
        Interactable closestInteractable = null;
        float closestDistanceSqr = float.MaxValue;

        foreach (Collider c in candidates)
        {
            if(!c.TryGetComponent(out Interactable interactable)) continue;
            if(!interactable.CanInteract(_heldItem.ObjectType)) continue;

            Vector3 directionToInteractable = interactable.transform.position - transform.position;

            float angle = Vector3.Angle(transform.forward, directionToInteractable.normalized);
            if (angle > _detectionAngle) continue;

            float distanceSqr = directionToInteractable.sqrMagnitude;
            if (distanceSqr < closestDistanceSqr)
            {
                closestInteractable = interactable;
                closestDistanceSqr = distanceSqr;
            }
        }
        
        
        return closestInteractable;
    }

    void ClearSelection()
    {
        if (_closestTarget != null)
        {
            _closestTarget.GetComponent<Highlightable>().SetHighlighted(false);
        }
        
        _closestTarget = null;
        
    }
    
    // When throwing the newly spawned Axe object when thrown
    [Rpc(SendTo.Server)]
    public void RequestLaunchAxeServerRpc()
    {
        if (_heldItem.ObjectType != ObjectType.Axe)
            return;

        if (_activeThrownAxe != null)
            return;

        NetworkObject spawnedObject =
            Instantiate(
                _thrownAxePrefab,
                _axeHand.position,
                _axeHand.rotation
            );

        spawnedObject.Spawn();

        _activeThrownAxe = spawnedObject.GetComponent<ThrownAxe>();
        
        AxeVfxController thrownAxeVfx = spawnedObject.GetComponent<AxeVfxController>();

        // Axe is leaving player's hand, stop held particle state
        thrownAxeVfx?.SetHeld(false);
        
        // Axe leaves player's hand, enable trail and flying particles
        thrownAxeVfx?.SetFlying(true);

        Vector3 direction = transform.forward;
        direction.y = 0f;
        direction.Normalize();

        _activeThrownAxe.Launch(
            direction,
            throwImpulse,
            _characterController,
            _axeHand
        );
        
        _axeState = AxeState.Away;
        _axeIsAway.Value = true;
    }

    [Rpc(SendTo.Server)]
    void RequestReturnAxeServerRpc()
    {
        if (!_axeIsAway.Value || _activeThrownAxe == null)
            return;

        StartCoroutine(ReturnAxeServer());
    }

    [Rpc(SendTo.Server)]
    void RequestInteractRpc(ulong networkObjectId)
    {
        if (_axeIsAway.Value)
            return;

        Debug.Log($"Requesting Interact on server for {networkObjectId}");
        
        // TODO Slice 6.3: look up networkObjectId in SpawnedObjects. If that
        // object is gone, return. It may have despawned after you selected it.
        // If it has an Interactable, call ServerInteract(_heldItem).
        Dictionary<ulong, NetworkObject> spawnedObjectMap = NetworkManager.SpawnManager.SpawnedObjects;
        if (!spawnedObjectMap.TryGetValue(networkObjectId, out NetworkObject spawnedObject))
        {
            Debug.LogError($"Couldn't find spawned object: {networkObjectId}");
            return;
        }

        if (!spawnedObject.TryGetComponent(out Interactable interactable))
        {
            Debug.LogError("Object doesn't have interactable");
            return;
        }

        if (interactable.CanInteract(_heldItem.ObjectType))
        {
            interactable.ServerInteract(_heldItem);
        }
        
        // Check: E still only plays Interact. Console stays clean. The pickup
        // (e.g. axe) does not move yet.

        // Next: Slice 6.4 in World/Interactable.cs — ServerInteract.
    }
}
