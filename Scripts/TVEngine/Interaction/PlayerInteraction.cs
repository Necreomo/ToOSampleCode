using System;
using System.Collections;
using TVEngine.Maths;
using UnityEngine;

namespace TVEngine.Interaction
{
    public class PlayerInteraction : MonoBehaviour
    {
        public class ClosestInteractionData
        {
            public int Index;
            public Transform Transform;

            public ClosestInteractionData(int pIndex, Transform pTransform)
            {
                Index = pIndex;
                Transform = pTransform;
            }
        }

        [Tooltip("Interact sphere cast radius")]
        [SerializeField]
        private float _interactScanDiameter = 2.0f;

        [SerializeField]
        private LayerMask _scanLayerMask;

        [SerializeField]
        private float _interactThreshold = 1.0f;

        [SerializeField]
        private float _interactFacingThreshold = 45.0f;

        [SerializeField]
        private float _interactScanRate = 0.5f;

#if UNITY_EDITOR
        [Header("Debug")]
        [SerializeField]
        private bool _showDebugVisual = false;

        [SerializeField]
        private bool _ignoreInteractScan = false;
#endif

        /// <summary>
        /// Note this is the root transform not the transform of the collider that is hit!
        /// </summary>
        private Transform _interactableObjectTrans;
        private Vector3? _scanHitPoint = null;
        private float _hitPointColliderRadius = 0.0f;

        private float _interactScanRadius;

        private bool _stopScan = false;

        private bool _inInteractThreshold = false;
        InteractInputData _interactInputData = null;

        // Used to stop the scan if interacting with an object
        private bool _isInteracting = false;

        private void Start()
        {
            _interactScanRadius = _interactScanDiameter * 0.5f;
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (!_ignoreInteractScan)
            {
#endif
                StartCoroutine(ScanForInteractables());
#if UNITY_EDITOR
            }
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            if (!_ignoreInteractScan)
            {
#endif
                StopAllCoroutines();
                ResetVars();
#if UNITY_EDITOR
            }
#endif
        }

        private void ResetVars()
        {
            _interactableObjectTrans = null;
            _scanHitPoint = null;
            _hitPointColliderRadius = 0.0f;
            _inInteractThreshold = false;
            _interactInputData = null;
            GameController.Instance.OnInteractVisualChanged(pCanInteract: false);
            GameController.Instance.OnInteractTargetChanged();
        }

        public bool Interact(Action pBeforeInteractCallback = null)
        {
            if (_inInteractThreshold)
            {
                if (_interactInputData != null)
                {
                    pBeforeInteractCallback?.Invoke();
                    _interactInputData.Interact();
                }
#if UNITY_EDITOR
                else
                {
                    Debug.Log($"Object does not have InteractObject Component!: {_interactableObjectTrans.name}");
                }
#endif
                return true;
            }

            return false;
        }
        private IEnumerator ScanForInteractables()
        {
            while (!_stopScan)
            {
                if (!_isInteracting)
                {
                    RaycastHit[] hitInfo = UnityEngine.Physics.SphereCastAll(transform.position, _interactScanRadius, transform.forward, 0.1f, _scanLayerMask);
                    float colliderRadius = 0.0f;
                    if (hitInfo.Length == 1)
                    {
                        if (hitInfo[0].transform != _interactableObjectTrans)
                        {
                            if (CheckInteractionValidInteractionTag(hitInfo[0].collider.transform))
                            {
                                if (hitInfo[0].collider.GetType() == typeof(SphereCollider))
                                {
                                    SphereCollider sphereInteractCollider = (SphereCollider)hitInfo[0].collider;
                                    colliderRadius = sphereInteractCollider.radius;
                                }
                                else if (hitInfo[0].collider.GetType() == typeof(CapsuleCollider))
                                {
                                    CapsuleCollider capsuleInteractCollider = (CapsuleCollider)hitInfo[0].collider;
                                    colliderRadius = capsuleInteractCollider.radius;
                                }

                                //Vector 0 check here means that the sweeptest was within the collider already so an invalid point gets sent
                                //will use the center of the trigger point instead
                                if (hitInfo[0].point != Vector3.zero)
                                {
                                    UpdateHudElement(hitInfo[0].collider.transform, hitInfo[0].point, colliderRadius);
                                }
                                else
                                {
                                    UpdateHudElement(hitInfo[0].collider.transform, hitInfo[0].collider.transform.position, colliderRadius);
                                }
                            }
                        }
                    }
                    else if (hitInfo.Length > 1)
                    {
                        string[] exclusionTag = new string[1];

                        if (transform.CompareTag(ProjectTagName.Player))
                        {
                            exclusionTag[0] = ProjectTagName.Pet;
                        }
                        else if (transform.CompareTag(ProjectTagName.Pet))
                        {
                            exclusionTag[0] = ProjectTagName.Player;
                        }

                        ClosestInteractionData closestInteractionObject = TVMaths.GetClosestFromOrigin(transform.position, hitInfo, exclusionTag);

                        if (closestInteractionObject != null &&
                            closestInteractionObject.Transform != _interactableObjectTrans &&
                            closestInteractionObject.Index != int.MaxValue)
                        {

                            if (hitInfo[closestInteractionObject.Index].collider.GetType() == typeof(SphereCollider))
                            {
                                SphereCollider sphereInteractCollider = (SphereCollider)hitInfo[closestInteractionObject.Index].collider;
                                colliderRadius = sphereInteractCollider.radius;
                            }
                            else if ((hitInfo[closestInteractionObject.Index].collider.GetType() == typeof(CapsuleCollider)))
                            {
                                CapsuleCollider capsuleInteractCollider = (CapsuleCollider)hitInfo[closestInteractionObject.Index].collider;
                                colliderRadius = capsuleInteractCollider.radius;
                            }

                            //Vector 0 check here means that the sweeptest was within the collider already so an invalid point gets sent
                            //will use the center of the trigger point instead
                            if (hitInfo[closestInteractionObject.Index].point != Vector3.zero)
                            {
                                UpdateHudElement(closestInteractionObject.Transform, hitInfo[closestInteractionObject.Index].point, colliderRadius);
                            }
                            else
                            {
                                UpdateHudElement(closestInteractionObject.Transform, hitInfo[closestInteractionObject.Index].collider.transform.position, colliderRadius);
                            }
                        }
                    }
                    else
                    {
                        UpdateHudElement(pClosestTransform: null, pHitPoint: null, colliderRadius);
                    }

                    SetInInteractThreshold();
                }
                yield return new WaitForSecondsRealtime(_interactScanRate);
            }
        }

        private void UpdateHudElement(Transform pClosestTransform, Vector3? pHitPoint, float pColliderRadius)
        {
            _interactableObjectTrans = pClosestTransform;
            _scanHitPoint = pHitPoint;
            _hitPointColliderRadius = pColliderRadius;

            if (pClosestTransform != null)
            {
                _interactInputData = _interactableObjectTrans.GetComponent<InteractInputData>();
                GameController.Instance.OnInteractTargetChanged(_interactInputData.GetUiWorldSpacePivot());
            }
            else if(_interactInputData != null)
            {
                _interactInputData = null;
                GameController.Instance.OnInteractTargetChanged(_interactableObjectTrans);
            }
        }

        private void SetInInteractThreshold()
        {
            bool previousInteractThreshold = _inInteractThreshold;

            if (_scanHitPoint.HasValue)
            {
                _inInteractThreshold = (Vector3.Distance(transform.position, _scanHitPoint.Value) - _hitPointColliderRadius) < _interactThreshold &&
                        Vector3.Angle(transform.forward, _scanHitPoint.Value - transform.position) < _interactFacingThreshold;

                if (_inInteractThreshold != previousInteractThreshold)
                {
                    GameController.Instance.OnInteractVisualChanged(_inInteractThreshold);
                }
            }
        }

        public void SetScanningState(bool pState)
        {
            if (!pState)
            {
                ResetVars();
            }
            _isInteracting = !pState;
        }

        private bool CheckInteractionValidInteractionTag(Transform pInteractionTrigger)
        {
            return pInteractionTrigger.transform.CompareTag(ProjectTagName.Untagged) ||
                pInteractionTrigger.transform.CompareTag(ProjectTagName.Player) && transform.CompareTag(ProjectTagName.Player) ||
                pInteractionTrigger.transform.CompareTag(ProjectTagName.Pet) && transform.CompareTag(ProjectTagName.Pet);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_showDebugVisual)
            {
                Gizmos.DrawWireSphere(transform.position, _interactScanRadius);
                GizmoUtility.DrawString($"Interact Scanner", transform.position, Color.white, Vector2.zero, pTextSize: 8);

                if (_interactableObjectTrans != null)
                {
                    Gizmos.DrawWireSphere(_interactableObjectTrans.position, 1.0f);
                    Vector3 stringOffset = _interactableObjectTrans.position;
                    stringOffset.y += 1.0f;
                    GizmoUtility.DrawString($"Interact Object: {_interactableObjectTrans.name}", stringOffset, Color.magenta, Vector2.zero, pTextSize: 8);
                }
            }
        }
#endif
    }
}
