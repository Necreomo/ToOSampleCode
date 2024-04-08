using Gamekit3D;
using System;
using System.Collections;
using TVEngine.Utility.Inspector;
using UnityEngine;

public class DodgeKinematicController : MonoBehaviour
{

    [SerializeField]
    private MeleeWeapon _dodgeDamageSource;

    [SerializeField]
    private bool _reverseMovement = false;

    [SerializeField]
    private bool _skipRigidbodyChanges = false;

    [SerializeField]
    private LayerMask _layerMask;


    [SerializeField]
    private AnimationClip _dashAnimation;

    [SerializeField]
    private AnimationClip _dashBlendTime;

    [Tooltip("Value for if for some reason the kinematic dodge controller gets stuck in its dodge state boot them out after a set amouint of time")]
    [SerializeField]
    [ReadOnly]
    private float _failSafeTime = 3.0f;

    [Header("Vertical Check")]
    [SerializeField]
    private Transform _startingRaycast;

    [SerializeField]
    private Transform _targetRaycast;

    [SerializeField]
    private Transform _targetRaycastShortRoll;

    [SerializeField]
    [Tooltip("The veritical limit that the raycast point is to the characters feet to allow movement")]
    private float _verticalLimit = 0.125f;

    [Header("Horizontal Wall Check")]
    [SerializeField]
    private Transform _horizontalOrigin;

    [SerializeField]
    private float _horiRadius = .25f;

    [Header("Slide Check")]
    [SerializeField]
    private float _slideColliderClosenessCheck = .1f;


#if UNITY_EDITOR
    [Header("Debug")]
    [SerializeField]
    private bool _drawRaycast = true;

    [SerializeField]
    private bool _outputDodgePosition = false;

#endif

    private Rigidbody _rigidbody;

    private Coroutine _dashRoutine;

    private bool _canStillDash = true;

    private float _movementDistance = 0.0f;
    private float _movementDistanceShortRoll = 0.0f;

    private void Awake()
    {
        _dodgeDamageSource?.SetOwner(gameObject, null);
    }
    public void Initialize(Rigidbody pRigidBody)
    {
        _rigidbody = pRigidBody;
        _movementDistance = _startingRaycast.localPosition.z - _targetRaycast.localPosition.z;
        _movementDistanceShortRoll = _startingRaycast.localPosition.z - _targetRaycastShortRoll.localPosition.z;

        _failSafeTime = _dashAnimation.length + _dashBlendTime.length;
    }

    public virtual void StartDodge(Action pTimedOutCallback, bool pIsShortRoll = false)
    {
        if (_dashRoutine != null)
        {
            StopCoroutine(_dashRoutine);
        }
        _dashRoutine = StartCoroutine(DashRoutine(pTimedOutCallback, pIsShortRoll));
    }

    public void StopDodge(bool pResetVelocity = true)
    {
        if (_dashRoutine != null)
        {
            StopCoroutine(_dashRoutine);
            _dashRoutine = null;
            if (!_skipRigidbodyChanges)
            {
                RigidBodySettings.ChangeRidgidMovement(_rigidbody, pUseKinematic: false);
            }
            _dodgeDamageSource?.EndAttack();
        }
        if (pResetVelocity)
        {
            _rigidbody.velocity = Vector3.zero;
        }
        _canStillDash = true;
    }

    private IEnumerator DashRoutine(Action pTimedOutCallback, bool pIsShortRoll)
    {
        float elapsedTime = 0.0f;

        Transform targetRaycast = pIsShortRoll ? _targetRaycastShortRoll : _targetRaycast;
        float targetMovement = pIsShortRoll ? _movementDistanceShortRoll : _movementDistance;

        _dodgeDamageSource?.BeginAttack(pThrowingAttack: false);

        if (!_skipRigidbodyChanges)
        {
            RigidBodySettings.ChangeRidgidMovement(_rigidbody, pUseKinematic: true);
        }

        GameController.Instance.CreateRefractionDamageSource(transform.position, transform.rotation);

        while (_canStillDash && elapsedTime < _failSafeTime && _rigidbody.isKinematic)
        {
            elapsedTime += Time.deltaTime;

            RaycastHit onhit;

#if UNITY_EDITOR
            if (_drawRaycast)
            {
                Debug.DrawLine(_startingRaycast.position, _startingRaycast.position + (targetRaycast.position - _startingRaycast.position).normalized * 2.0f, Color.yellow, 5.0f);
            }
#endif

            if (Physics.Raycast(_startingRaycast.position, targetRaycast.position - _startingRaycast.position, out onhit, 2.0f, _layerMask))
            {
                if (Mathf.Abs(_rigidbody.position.y - onhit.point.y) < _verticalLimit)
                {
                    if(!SlideCharacterAgainstWall(pIsShortRoll))
                    {
                        _rigidbody.MovePosition(onhit.point);
#if UNITY_EDITOR
                        if (_outputDodgePosition)
                        {
                            string position = transform.position.ToString("F10");
                            Debug.Log($"Current Position: {position}");
                        }
#endif
                    }
                }
                else
                {
                    SlideCharacterAgainstWall(pIsShortRoll);
                }
            }
            yield return new WaitForFixedUpdate();
        }

#if UNITY_EDITOR
        if (elapsedTime < _failSafeTime)
        {
            Debug.LogError($"Editor WARNING: Call to stop dodge movement has not been called for {_failSafeTime} seconds! Bailing out!");
        }
#endif
        pTimedOutCallback?.Invoke();
        StopDodge(pResetVelocity: true);
    }

    private bool SlideCharacterAgainstWall(bool pIsShortRoll)
    {
        Collider[] _listOfColliders = Physics.OverlapSphere(_horizontalOrigin.position, _slideColliderClosenessCheck, _layerMask);

        if (_listOfColliders.Length > 0)
        {
            Vector3 characterScanDirection = transform.forward;
            if (_reverseMovement)
            {
                characterScanDirection = -characterScanDirection;
            }

            RaycastHit inFrontObstacle;
            if (Physics.SphereCast(_horizontalOrigin.position - characterScanDirection,
                _horiRadius,
                characterScanDirection,
                out inFrontObstacle,
                1.5f,
                _layerMask))
            {
                Vector3 direction = inFrontObstacle.normal;
                direction = Quaternion.AngleAxis(90, Vector3.up) * direction;

                if (!_reverseMovement)
                {
                    if (Vector3.Angle(direction, characterScanDirection) <= 90.0f)
                    {
                        direction = -direction;
                    }
                }
                else
                {
                    if (Vector3.Angle(direction, characterScanDirection) > 90.0f)
                    {
                        direction = -direction;
                    }
                }

                direction *= (pIsShortRoll ? _movementDistanceShortRoll : _movementDistance);

                RaycastHit groundHit;
                Vector3 scanOrigin = _rigidbody.position + direction;
#if UNITY_EDITOR
                if (_drawRaycast)
                {
                    Debug.DrawLine(_startingRaycast.position,
                       _startingRaycast.position + ( scanOrigin - _startingRaycast.position).normalized * 2.0f, 
                        Color.magenta, 
                        5.0f);
                }
#endif
                //Angled check...
                if (Physics.Raycast(_startingRaycast.position, scanOrigin - _startingRaycast.position, out groundHit, 2.0f, _layerMask))
                {
                    //Height validation
                    if (Mathf.Abs(_rigidbody.position.y - groundHit.point.y) < _verticalLimit)
                    {
                        //Offset is needed so the next check wont constantly fail if the destination point is right on the collider
                        direction.y = groundHit.point.y - _rigidbody.position.y + .04f;

                        Collider[] collidersAtDestination = Physics.OverlapSphere(_rigidbody.position + direction, 0.0f, _layerMask);
                        if (collidersAtDestination.Length == 0)
                        {
                            _rigidbody.MovePosition(_rigidbody.position + direction);
                        }
                    }
                }
            }
        }
        return _listOfColliders.Length > 0;
    }
}
