using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TVEngine.Ai
{
    [ExecuteInEditMode]
    public class AiPatrolController : MonoBehaviour
    {
        private enum PathingType
        {
            PingPong,
            Loop
        }

        private enum PingPongDirection
        {
            Forward,
            Reverse
        }

        [Header("References")]
        [SerializeField]
        private Transform _pathGroup;

        [Header("Settings")]
        [SerializeField]
        private PathingType _pathingType = PathingType.PingPong;

        [SerializeField]
        private PingPongDirection _pingPongDirection = PingPongDirection.Forward;

#if UNITY_EDITOR
    [Header("Debug")]
    [SerializeField]
    private bool _drawGizmoPath = true;
#endif

        private List<AiPatrolNode> _wayPoints;

        private int _currentTargetWayPoint = 0;

        private Coroutine _waitCoroutine = null;


        private void Awake()
        {
            _wayPoints = new List<AiPatrolNode>();

            if (_pathGroup != null)
            {
                SetUpWaypoints(_pathGroup);
                enabled = true;
            }
            else
            {
                enabled = false;
            }
        }

        private void SetUpWaypoints(Transform pWaypointListRoot)
        {
            if (pWaypointListRoot == null)
            {
                return;
            }
            else
            {
                for (int i = 0; i < pWaypointListRoot.childCount; i++)
                {
                    _wayPoints.Add(pWaypointListRoot.GetChild(i).GetComponent<AiPatrolNode>());
                }

                switch (_pathingType)
                {
                    case PathingType.PingPong:
                        if (_pingPongDirection == PingPongDirection.Forward)
                        {
                            _currentTargetWayPoint = 1;
                        }
                        else
                        {
                            _currentTargetWayPoint = _wayPoints.Count - 1;
                        }
                        break;
                    case PathingType.Loop:
                        _currentTargetWayPoint = 1;
                        break;
                    default:
                        break;
                }
            }

        }

        private void HandleNextPingPongNode(Action callbackAfterWait, bool pUseInternalWaitRoutine)
        {
            if (_pingPongDirection == PingPongDirection.Forward)
            {
                if (pUseInternalWaitRoutine)
                {
                    if (_waitCoroutine != null)
                    {
                        StopCoroutine(_waitCoroutine);
                    }
                    _waitCoroutine = StartCoroutine(WaitCoroutine(_wayPoints[_currentTargetWayPoint], callbackAfterWait));
                }

                _currentTargetWayPoint++;
                if (_currentTargetWayPoint >= _wayPoints.Count)
                {
                    _currentTargetWayPoint = _wayPoints.Count - 2;
                    _pingPongDirection = PingPongDirection.Reverse;
                }
            }
            else
            {
                if (pUseInternalWaitRoutine)
                {
                    if (_waitCoroutine != null)
                    {
                        StopCoroutine(_waitCoroutine);
                    }
                    _waitCoroutine = StartCoroutine(WaitCoroutine(_wayPoints[_currentTargetWayPoint], callbackAfterWait));
                }

                _currentTargetWayPoint--;
                if (_currentTargetWayPoint < 0)
                {
                    _currentTargetWayPoint = 1;
                    _pingPongDirection = PingPongDirection.Forward;
                }
            }
        }

        private void HandleNextLoopNode(Action callbackAfterWait, bool pUseInternalWaitRoutine)
        {
            if (pUseInternalWaitRoutine)
            {
                if (_waitCoroutine != null)
                {
                    StopCoroutine(_waitCoroutine);
                }
                _waitCoroutine = StartCoroutine(WaitCoroutine(_wayPoints[_currentTargetWayPoint], callbackAfterWait));
            }
            _currentTargetWayPoint++;
            if (_currentTargetWayPoint >= _wayPoints.Count)
            {
                _currentTargetWayPoint = 0;
            }

        }


        private IEnumerator WaitCoroutine(AiPatrolNode waypointNode, Action callback)
        {
            yield return new WaitForSeconds(waypointNode.WaitTime);
            callback?.Invoke();
        }

        public bool AssignScriptedPath(Transform pWaypointListRoot)
        {
            bool retValue = false;

            if (pWaypointListRoot != null)
            {
                SetUpWaypoints(pWaypointListRoot);
                enabled = true;
            }

            if (_wayPoints.Count > 0)
            {
                retValue = true;
            }

            return retValue;
        }

        public int GetWaypointSystemLength()
        {
            return _wayPoints.Count;
        }

        public Vector3 GetClosestWaypointFromPoint(Vector3 position, bool overrideCurrentWaypointTarget = false)
        {
            return _wayPoints[GetIDOfClosestWaypoint(position, overrideCurrentWaypointTarget)].transform.position;
        }

        public Transform GetClosestWaypointTransformFromPoint(Vector3 position, bool overrideCurrentWaypointTarget = false)
        {
            return _wayPoints[GetIDOfClosestWaypoint(position, overrideCurrentWaypointTarget)].transform;
        }

        private int GetIDOfClosestWaypoint(Vector3 position, bool overrideCurrentWaypointTarget = false)
        {
            int idOfClosestWaypoint = 0;
            float lowestDistance = float.MaxValue;

            for (int i = 0; i < _wayPoints.Count; i++)
            {
                float distance = Vector3.Distance(position, _wayPoints[i].transform.position);

                if (lowestDistance > distance)
                {
                    lowestDistance = distance;
                    idOfClosestWaypoint = i;
                }

            }

            if (overrideCurrentWaypointTarget)
            {
                _currentTargetWayPoint = idOfClosestWaypoint;
            }

            return idOfClosestWaypoint;
        }

        public Vector3 GetCurrentTarget()
        {
            return GetCurrentTargetTransform().position;
        }

        public Transform GetCurrentTargetTransform()
        {
            return _wayPoints[_currentTargetWayPoint].transform;
        }

        public bool WaitAtThisWaypoint()
        {
            return _wayPoints[_currentTargetWayPoint].WaitTime > 0.0f;
        }

        public float GetWaitTimeForCurrentWaypoint()
        {
            return _wayPoints[_currentTargetWayPoint].WaitTime;
        }

        public void UpdateNextTarget(Action callback, bool pUseInternalWaitRoutine = true)
        {
            switch (_pathingType)
            {
                case PathingType.PingPong:
                    HandleNextPingPongNode(callback, pUseInternalWaitRoutine);

                    break;
                case PathingType.Loop:
                    HandleNextLoopNode(callback, pUseInternalWaitRoutine);
                    break;
                default:
                    break;
            }
        }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_drawGizmoPath)
        {
            for (int i = 0; i < _wayPoints.Count; i++)
            {
                UnityEditor.Handles.Label(_wayPoints[i].transform.position, "Waypoint " + i);
                Gizmos.DrawWireSphere(_wayPoints[i].transform.position, 0.1f);

                if (i != _wayPoints.Count - 1)
                {
                    Gizmos.DrawLine(_wayPoints[i].transform.position, _wayPoints[i + 1].transform.position);
                }

            }

        }
    }

#endif
    }
}
