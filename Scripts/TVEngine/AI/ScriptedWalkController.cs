using System;
using System.Collections;
using TVEngine.SO;
using UnityEngine;
using UnityEngine.AI;

namespace TVEngine.Ai
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class ScriptedWalkController : MonoBehaviour
    {
        [SerializeField]
        private SO_ScriptedMoveData _scriptedMoveData;

        private NavMeshAgent _agent;
        private Rigidbody _rigidbody;

        private Transform _destination;
        private Action _onScriptedWalkComplete = null;

        private Coroutine _scriptedWalkRoutine = null;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            _agent.updatePosition = false;
        }

        public void ScriptedWalk(Transform pDestination, Action pCallback)
        {
            _agent.enabled = true;
            _destination = pDestination;
            _onScriptedWalkComplete = pCallback;
            _agent.SetDestination(_destination.position);
            _scriptedWalkRoutine = StartCoroutine(HasReachedDestination());
        }

        private IEnumerator HasReachedDestination()
        {

            while (!HasReachedScriptedCinematicDestination())
            {
                yield return new WaitForFixedUpdate();
            }

            while (!EndRotationComplete())
            {
                yield return new WaitForEndOfFrame();
            }
            _agent.isStopped = true;
            _agent.enabled = false;
            _destination = null;
            _onScriptedWalkComplete?.Invoke();
            _scriptedWalkRoutine = null;
        }

        public bool HasReachedScriptedCinematicDestination()
        {
            if (!_agent.pathPending)
            {
                if (_agent.remainingDistance <= _agent.stoppingDistance)
                {
                    if (_agent.hasPath || _agent.velocity.sqrMagnitude == 0.0f)
                    {
                        _rigidbody.position = _destination.position;
                        return true;
                    }
                }
                else
                {
                    Vector3 velocity = Vector3.zero;
                    transform.position = Vector3.SmoothDamp(transform.position, _agent.nextPosition, ref velocity, _scriptedMoveData.BlendMoveRate);
                }
            }
            return false;
        }

        public bool EndRotationComplete()
        {
            if (Vector3.Angle(transform.forward, _destination.forward) < 0.1f)
            {
                transform.rotation = _destination.rotation;
                return true;
            }
            else
            {
                Vector3 desiredForward = Vector3.RotateTowards(transform.forward, _destination.forward, AIConstants.ScriptedWalkEndRotationSpeed * Time.deltaTime, maxMagnitudeDelta: 0.1f);
                transform.rotation = Quaternion.LookRotation(desiredForward);
            }

            return false;
        }

        public float UpdateBlendAndSpeed(bool pRun)
        {
            float percentile = pRun ? _scriptedMoveData.RunMoveSpeedPercentile : _scriptedMoveData.WalkMoveSpeedPercentile;

            float interpolatedValue = _scriptedMoveData.InterpolationModifier.Evaluate(percentile);
            _agent.speed = Mathf.Lerp(_scriptedMoveData.MinMoveSpeed, _scriptedMoveData.MaxMoveSpeed, interpolatedValue);
            return Mathf.Lerp(0.0f, AnimationConstants.BotMaxBlendAmount, interpolatedValue);
        }

        public void WarpAgent(Vector3 pDestination)
        {
            _agent.Warp(pDestination);
        }
    }
}
