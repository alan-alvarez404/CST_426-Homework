using UnityEngine;

/*
 * ThrownAxe keeps the axe's held pose so it can be attached to the hand
 * after a throw. Launch physics and collision response belong here;
 * PlayerController decides when to throw and recall it.
 */

public class ThrownAxe : MonoBehaviour
{
    public Rigidbody rigidbody;
    public Collider axeCollider;
    
    public float spinSpeed = 1000f;
    
    [SerializeField] AxeVfxController _effects;

    // bool stuck; - This was unused, gonna leave it here just in case.
    Transform _hand;
    Vector3 _heldLocalPosition;
    Quaternion _heldLocalRotation;

    // Ensure the axe starts as kinematic and its collider disabled
    void Awake()
    {
        if (_effects == null)
            _effects = GetComponent<AxeVfxController>();
        
        if (rigidbody == null)
            rigidbody = GetComponent<Rigidbody>();

        if (axeCollider == null)
            axeCollider = GetComponent<Collider>();

        rigidbody.isKinematic = true;
        axeCollider.enabled = false;
    }
    
    // Called in the coroutine bit
    public void SetFlying(bool flying)
    {
        _effects?.SetFlying(flying);
    }
    
    public void SetHeld(bool held)
    {
        _effects?.SetHeld(held);
    }
    
    // TODO Slice 8.1: give Assets/Curves/Prefabs/Axe.prefab a visual child that can rotate
    // on its own, separate from the physics root. Keep its look and collision the same.
    // Check: the held axe looks the same, and throw and catch still work.
    // Next: Slice 8.2 below.

    // TODO Slice 8.2: spin the visual child end over end, based on time.
    // Pick an axis and speed that suit the mesh. Leave the root's rotation to physics.
    // Next: Slice 8.3 at the hooks below and in PlayerController.ReturnAxe.

    public Vector3 CatchPosition => _hand.TransformPoint(_heldLocalPosition);

    public void Launch(Vector3 direction, float impulse, CharacterController thrower, Transform hand) 
    {
        _hand = hand;
        _heldLocalPosition = hand.InverseTransformPoint(transform.position);
        _heldLocalRotation = Quaternion.Inverse(hand.rotation) * transform.rotation;
        
        // TODO Slice 4.1: hand the detached axe to physics. Right now it hangs in the air.
        // 1. Let physics move it and let it collide with the world.
        // 2. Never let it collide with the thrower.
        // 3. Push it along direction with impulse.
        // Check: Launch_DetachesAndEnablesPhysicsWhileIgnoringThrower passes.
        // A throw flies and sticks on first contact.
        // Next: Slice 4.2 in OnCollisionEnter.
        
        Physics.IgnoreCollision(axeCollider, thrower, true);
        transform.SetParent(null);
        transform.right = direction;

        rigidbody.isKinematic = false;
        axeCollider.enabled = true;

        rigidbody.AddForce(direction * impulse, ForceMode.Impulse);
        rigidbody.AddTorque(transform.forward * (-spinSpeed * Mathf.Deg2Rad), ForceMode.VelocityChange);

        // Particle effects turn on when flying through air
        _effects?.SetFlying(true);
        _effects?.SetStuck(false); // Ambient particles are off when midair

        // TODO Slice 8.3 (launch hook): start visual spin.
        // Pair it with the contact hook below.
        // Check: a throw spins, and the spin stops once the axe sticks.
    }

    public void AttachToHand()
    {
        transform.SetParent(_hand);
        transform.SetLocalPositionAndRotation(_heldLocalPosition, _heldLocalRotation);
        rigidbody.isKinematic = true;
        axeCollider.enabled = false;
        
        // Stop trail effect
        _effects?.SetFlying(false);

        // TODO Slice 8.3 (catch hook): stop the spin and restore the held look.
        // Check: throw and recall both spin. Two full cycles end with the original held look.
        // Next: polish, networking, and your showcase video. </> end of Slice 8
    }

    void OnCollisionEnter(Collision collision)
    {
        // TODO Slice 4.2: make the stick below apply only to your designated target.
        // Right now every first contact sticks, even the floor.
        // 1. Decide how to recognize the target.
        // 2. Give every other contact a visibly different response.
        // Check: a target hit stays stuck until recall. A floor hit does not stick.
        // Recall (still a snap) and catch work after both.
        // The Launch test and the four starting-green tests still pass.
        // Next: Slice 5.1 in PlayerController.GetReturnControlPoints. </> end of Slice 4
        // if (collision.collider.GetComponentInParent<PlayerController>() == null) return;
        
        rigidbody.isKinematic = true;
        
        // Particle effects when stuck into an object
        _effects?.SetFlying(false);
        _effects?.SetStuck(true);
        _effects?.PlayImpactBurst();
        
        // TODO Slice 8.3 (contact hook): stop visual spin while stuck.
    }
}
