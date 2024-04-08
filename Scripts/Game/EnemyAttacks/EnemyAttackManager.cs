using System;
using System.Collections;
using System.Collections.Generic;
using TVEngine.Utility.Inspector;
using UnityEngine;

/// <summary>
/// Gets a list of all the attack type components and randomizes which one to use...
/// </summary>
public class EnemyAttackManager : MonoBehaviour
{
    public enum AgressiveWalkState
    {
        CalculateRandomDestinationPoint,
        DownTimeBefore,
        Move,
        DownTimeAfter,
        Count
    }

    public enum AggressiveWalkType
    { 
        BeforeAttack,
        AfterAttack,
        Count
    }

    [SerializeField]
    [Tooltip("Defines how fast character needs to rotate before attacking")]
    private float _rotateTowardsSpeed = 10.0f;

    [SerializeField]
    [Tooltip("Defines limit in degrees for the character snap to forward to target after rotating")]
    private float _rotateSnapThreshold = 10.0f;

    [SerializeField]
    private bool _hasFinalAttack = false;
    [Tooltip("Flag this if you do not want the final attack to be a valid randomized attack on normal routine")]
    [SerializeField]
    private bool _finalAttackNotValid = false;

    private List<BaseBasicAttackController> _attacksAvailable = new List<BaseBasicAttackController>();

    [ReadOnly]
    [SerializeField]
    private bool _attacking = false;
    [ReadOnly]
    [SerializeField]
    private int _previousAttack = int.MaxValue;
    [ReadOnly]
    [SerializeField]
    private int _currentAttack = int.MaxValue;
    [ReadOnly]
    [SerializeField]
    private List<int> _restrictedAbilities = new List<int>();

    private AgressiveWalkState _aggressiveMoveState = AgressiveWalkState.Count;
    private float _aggressiveMoveWaitTime = 0.0f;
    private float _aggressiveMoveAdditionalOffsetScale = 1.0f;
    private float _aggressiveMinNormalDistance = 0.0f;
    private float _aggressiveMaxNormalDistance = 0.0f;
    private AggressiveWalkType _aggressiveWalkType = AggressiveWalkType.Count;
    private bool _goingHome = false;
    private int _totalAttackWeights = int.MaxValue;
    protected virtual void Awake()
    {
        GetComponents(_attacksAvailable);
    }

    private void Start()
    {
        if (_hasFinalAttack && _finalAttackNotValid)
        {
            AddAbilityRestriction(_attacksAvailable.Count - 1);
        }

        RecalculateTotalWeight();
        //On the first roll ignore any move rules
        ResetAggressiveMoveVariables();
    }

    public void UpdateAnimationParams()
    {
        if (_currentAttack != int.MaxValue)
        {
            _attacksAvailable[_currentAttack]?.UpdateAnimationParams();
        }
    }

    /// <summary>
    /// Sets up the enemy to do a specific attack
    /// </summary>
    /// <returns>if was able to set a new attack up</returns>
    public bool RollNextAttack()
    {
        if (!_attacking)
        {
            _currentAttack = GetValidAttackIndex();

            if (_currentAttack < _attacksAvailable.Count)
            {
                if (_attacksAvailable[_currentAttack].GetIfHasBeforeAttackAggressiveWalk())
                {
                    _aggressiveWalkType = AggressiveWalkType.BeforeAttack;
                    _aggressiveMoveState = AgressiveWalkState.CalculateRandomDestinationPoint;
                    _aggressiveMoveWaitTime = _attacksAvailable[_currentAttack].GetTeleportDownTime();
                }
                // Note this needs to be set as it is used in the calculation for zig zag attack
                _aggressiveMoveAdditionalOffsetScale = _attacksAvailable[_currentAttack].GetAggressiveMoveAdditionalOffsetScale();
                _aggressiveMinNormalDistance = _attacksAvailable[_currentAttack].GetNormalizedMinDistance();
                _aggressiveMaxNormalDistance = _attacksAvailable[_currentAttack].GetNormalizedMaxDistance();
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Default attack call to randomize an attaick that is available in their assigned kit
    /// </summary>
    /// <param name="pTarget"></param>
    /// <param name="attackRange"></param>
    /// <param name="pTargetLayers"></param>
    public void Attack(Transform pTarget, float attackRange, LayerMask pTargetLayers)
    {
        if (!_attacking && _currentAttack != int.MaxValue && !IsAnyAttackOnCooldown())
        {
            _attacking = _attacksAvailable[_currentAttack].Attack(pTarget, attackRange, PreviousAttackFinished, pTargetLayers, _currentAttack);

            if (_attacking)
            {
                if (_attacksAvailable[_currentAttack].GetIfHasAfterAttackAggressiveWalk())
                {
                    _aggressiveWalkType = AggressiveWalkType.AfterAttack;
                    _aggressiveMoveState = AgressiveWalkState.CalculateRandomDestinationPoint;
                    _aggressiveMoveWaitTime = _attacksAvailable[_currentAttack].GetTeleportDownTime();
                }
                // Note this needs to be set as it is used in the calculation for zig zag attack
                _aggressiveMoveAdditionalOffsetScale = _attacksAvailable[_currentAttack].GetAggressiveMoveAdditionalOffsetScale();
                _aggressiveMinNormalDistance = _attacksAvailable[_currentAttack].GetNormalizedMinDistance();
                _aggressiveMaxNormalDistance = _attacksAvailable[_currentAttack].GetNormalizedMaxDistance();
            }
            else
            {
                RollNextAttack();
            }

        }
    }

    /// <summary>
    /// attack call but we force which attack to use instead of randomizing
    /// </summary>
    /// <param name="pTarget"></param>
    /// <param name="attackRange"></param>
    /// <param name="pTargetLayers"></param>
    /// <param name="pAttackIndex"></param>
    public void Attack(Transform pTarget, float attackRange, LayerMask pTargetLayers, int pAttackIndex)
    {

        if (!_attacking)
        {
            _currentAttack = pAttackIndex;
            if (pAttackIndex < 0 || pAttackIndex >= _attacksAvailable.Count)
            {
                _currentAttack = 0;
            }
            _attacking = _attacksAvailable[_currentAttack].Attack(pTarget, attackRange, PreviousAttackFinished, pTargetLayers, _currentAttack);
            if (_attacksAvailable[_currentAttack].GetIfHasAfterAttackAggressiveWalk())
            {
                _aggressiveWalkType = AggressiveWalkType.AfterAttack;
                _aggressiveMoveState = AgressiveWalkState.CalculateRandomDestinationPoint;
                _aggressiveMoveWaitTime = _attacksAvailable[_currentAttack].GetTeleportDownTime();
            }
            // Note this needs to be set as it is used in the calculation for zig zag attack
            _aggressiveMoveAdditionalOffsetScale = _attacksAvailable[_currentAttack].GetAggressiveMoveAdditionalOffsetScale();
            _aggressiveMinNormalDistance = _attacksAvailable[_currentAttack].GetNormalizedMinDistance();
            _aggressiveMaxNormalDistance = _attacksAvailable[_currentAttack].GetNormalizedMaxDistance();
        }
    }

    /// <summary>
    /// Note final attack should be always the last attack component
    /// </summary>
    /// <param name="pTarget"></param>
    /// <param name="attackRange"></param>
    /// <param name="pTargetLayers"></param>
    public bool FinalAttack(Transform pTarget, float attackRange, LayerMask pTargetLayers, Action pOnFinalAttackComplete)
    {
        if (_hasFinalAttack)
        {
            _attacksAvailable[_attacksAvailable.Count - 1].FinalAttack(pTarget, attackRange, PreviousAttackFinished, pTargetLayers, pOnFinalAttackComplete);
        }

        return _hasFinalAttack;
    }

    public void OnDeath()
    {
        for (int i = 0; i < _attacksAvailable.Count; i++)
        {
            _attacksAvailable[i].OnDeath();
        }

        StopAllCoroutines();
        ResetAggressiveMoveVariables();
    }

    public void ForceOnDeathCallOnAttack(int pAttackIndex)
    {
        if (pAttackIndex >= 0 && pAttackIndex < _attacksAvailable.Count)
        {
            _attacksAvailable[pAttackIndex].OnDeath();
        }
    }

    public void OnDamaged(string pDamageSourceTag)
    {
        if (_currentAttack != int.MaxValue)
        {
            _attacksAvailable[_currentAttack].OnDamaged(pDamageSourceTag);
        }
    }

    public void SetGoingHome(bool pGoingHome)
    {
        _goingHome = pGoingHome;
        if (pGoingHome)
        {
            StopAllCoroutines();
            ResetAggressiveMoveVariables();
        }
    }

    public void StartScriptedMove()
    {
        StopAllCoroutines();
        ResetAggressiveMoveVariables();
        if (_currentAttack != int.MaxValue)
        {
            _attacksAvailable[_currentAttack].OnStartScriptedMove();
        }
    }


    private void PreviousAttackFinished()
    {
        _attacking = false;
        _previousAttack = _currentAttack;
        if (_currentAttack < _attacksAvailable.Count && _currentAttack >= 0)
        {
            _attacksAvailable[_currentAttack].AnimTriggerEnd();
        }
        RollNextAttack();
    }

    private IEnumerator WaitBeforeAggressiveMove()
    {
        _aggressiveMoveState = AgressiveWalkState.DownTimeBefore;

        float elapsedTime = 0.0f;
        while (elapsedTime < _aggressiveMoveWaitTime)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        _aggressiveMoveState = AgressiveWalkState.Move;
    }

    private IEnumerator WaitAfterAggressiveMove()
    {
        _aggressiveMoveState = AgressiveWalkState.DownTimeAfter;
        float elapsedTime = 0.0f;
        while (elapsedTime < _aggressiveMoveWaitTime)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        ResetAggressiveMoveVariables();
    }

    private void ResetAggressiveMoveVariables()
    {
        _aggressiveMoveState = AgressiveWalkState.Count;
        _aggressiveWalkType = AggressiveWalkType.Count;
        _aggressiveMoveWaitTime = 0.0f;
        _aggressiveMoveAdditionalOffsetScale = 1.0f;
        _aggressiveMinNormalDistance = 0.0f;
        _aggressiveMaxNormalDistance = 0.0f;
    }

    private bool IsAnyAttackOnCooldown()
    {
        bool retValue = false;            
        
        for (int i = 0; i < _attacksAvailable.Count; i++)
        {
            if (_attacksAvailable[i].IsAttackOnCooldown())
            {
                retValue = true;
                break;
            }
        }

        return retValue;
    }

    public void ResetToStartingValues()
    {
        ResetAllVariables();
        ResetAllAttacks();
    }

    /// <summary>
    /// This is expcitly on used in the child class for the pet attack manager, now on enemies to reset them after death
    /// </summary>
    protected void ResetAllVariables()
    {
        _attacking = false;
        _previousAttack = int.MaxValue;
        if (_currentAttack < _attacksAvailable.Count && _currentAttack >= 0)
        {
            _attacksAvailable[_currentAttack].AnimTriggerEnd();
        }
        RollNextAttack();
        ResetAggressiveMoveVariables();
        _goingHome = false;
    }

    /// <summary>
    /// used to clean up coroutines running on attacks
    /// </summary>
    public void CleanUpCurrentAttack()
    {
        if (_currentAttack != int.MaxValue)
        {
            _attacksAvailable[_currentAttack].CleanUp();
        }
    }

    protected void ResetCurrentAttack()
    {
        if (_attacking || _currentAttack != int.MaxValue)
        {
            if (_attacksAvailable.Count > 0)
            {
                _attacksAvailable[_currentAttack].ResetAttackCooldownNow();
            }
        }
    }

    protected void ResetAllAttacks()
    {
        for (int i = 0; i < _attacksAvailable.Count; i++)
        {
            _attacksAvailable[i].ResetAttackCooldownNow();
        }
    }

    public bool IsAttacking()
    {
        return _attacking;
    }

    public bool NeedToMoveBeforeOrAfterAttack()
    {
        return _aggressiveMoveState == AgressiveWalkState.DownTimeBefore ||
            _aggressiveMoveState == AgressiveWalkState.DownTimeAfter ||
            _aggressiveMoveState == AgressiveWalkState.CalculateRandomDestinationPoint ||
            GetMoveNowState();
    }

    public bool GetMoveNowState()
    {
        return _aggressiveMoveState == AgressiveWalkState.Move;
    }

    public float GetCurrentAttackTurnRate()
    {
        if (_currentAttack != int.MaxValue)
        {
            return _attacksAvailable[_currentAttack].GetTurnRate();
        }
        return 0.0f;
    }

    public bool GetRotateTowardAttack()
    {
        if (_currentAttack != int.MaxValue)
        {
            return _attacksAvailable[_currentAttack].GetRotateTowardAttack();
        }
        return false;
    }

    public LayerMask GetAttackLayer(int pIndex)
    {
        if (pIndex != int.MaxValue)
        {
            return _attacksAvailable[pIndex].GetTargetLayer();
        }
        return 0;
    }

    public void ReleaseAggressiveMove()
    {
        ResetAggressiveMoveVariables();
    }

    public void OnCalculateAggressiveMoveComplete()
    {
        if (_aggressiveMoveState == AgressiveWalkState.CalculateRandomDestinationPoint)
        {
            _aggressiveMoveState = AgressiveWalkState.Move;
        }
    }

    public AggressiveWalkType GetAggressiveWalkType()
    {
        return _aggressiveWalkType;
    }

    public AgressiveWalkState GetAggressiveWalkState()
    {
        return _aggressiveMoveState;
    }

    public BaseBasicAttackController.MovementAfterAttack GetAggressiveMovementType()
    {
        if (GetAggressiveWalkType() == AggressiveWalkType.BeforeAttack)
        {
            return _attacksAvailable[_currentAttack].GetAggressiveBeforeAttackMovementType();
        }

        return _attacksAvailable[_previousAttack].GetAggressiveAfterAttackMovementType();
    }

    public bool IsCurrentAttackArmored()
    {
        if (_currentAttack != int.MaxValue)
        {
            return _attacksAvailable[_currentAttack].IsAttackArmored();
        }

        return false;
    }

    public float? GetCurrentAttackRangeOverride()
    {
        if (_currentAttack != int.MaxValue)
        {
            if (_attacksAvailable[_currentAttack].GetCurrentAttackRangeOverride() == BattleConstants.EnemyAttackRangeOverrideDefault)
            {
                return null;
            }
            else
            {
                return _attacksAvailable[_currentAttack].GetCurrentAttackRangeOverride();
            }
        }

        return null;
    }

    public bool IsCurrentAttackHaveRangeOverride()
    {
        if (_currentAttack != int.MaxValue)
        {
            return _attacksAvailable[_currentAttack].IsCurrentAttackHaveRangeOverride();
        }

        return false;
    }

    public void AggressiveMoveAttempted()
    {
        StartCoroutine(WaitAfterAggressiveMove());
    }

    public float GetRandomDistance()
    {
        return UnityEngine.Random.Range(GetAggressiveMoveMinDistance(), GetAggressiveMoveMaxDistance()) * _aggressiveMoveAdditionalOffsetScale;
    }

    public float GetAggressiveMoveAdditionalOffsetScale()
    {
        return _aggressiveMoveAdditionalOffsetScale;
    }

    public float GetAggressiveMoveMinDistance()
    {
        return _aggressiveMinNormalDistance;
    }

    public float GetAggressiveMoveMaxDistance()
    {
        return _aggressiveMaxNormalDistance;
    }

    public float GetRotateTowardsSpeed()
    {
        return _rotateTowardsSpeed;
    }

    public float GetRotateSnapThreshold()
    {
        return _rotateSnapThreshold;
    }

    public bool IsInAggressiveWalk()
    {
        return _aggressiveMoveState != AgressiveWalkState.Count;
    }

    #region Camera Visibility Callbacks
    public void InCameraView(bool pIsInView)
    {
        for (int i = 0; i < _attacksAvailable.Count; i++)
        {
            _attacksAvailable[i].enabled = pIsInView;
        }

        enabled = pIsInView;
    }
    #endregion

    #region Animation Callbacks
    /// <summary>
    /// Animation event callback when an damage source or projectile needs to be spawned
    /// </summary>
    public virtual void AnimTriggerStartAttack()
    {

        if (_currentAttack >= _attacksAvailable.Count)
        {
#if UNITY_EDITOR
            Debug.Log($"{gameObject.name}: _current Attack Index is: {_currentAttack} which is invalid (AnimTriggerStartAttack)");
#endif
        }
        else
        {
            _attacksAvailable[_currentAttack].AnimTriggerStart();
        }
    }

    public virtual void AnimTriggerEndAttack()
    {  
        if (_currentAttack >= _attacksAvailable.Count)
        {
#if UNITY_EDITOR
            Debug.Log($"{gameObject.name}: _current Attack Index is: {_currentAttack} which is invalid (AnimTriggerEnd), Might not be failing just callback being called");
#endif
            _currentAttack = int.MaxValue;
        }
        else
        {
            _attacksAvailable[_currentAttack].AnimTriggerEnd();
        }
    }
    #endregion

    #region Utility
    public void AddAbilityRestriction(int pRestrictIndex)
    {
        _restrictedAbilities.Add(pRestrictIndex);
        RecalculateTotalWeight();
    }

    public void ResetAbilityRestriction()
    {
        _restrictedAbilities.Clear();
        RecalculateTotalWeight();
    }
    public void RestrictAbilities(int[] pRestrictList)
    {
        _restrictedAbilities = new List<int>(pRestrictList);
        RecalculateTotalWeight();
    }

    private void RecalculateTotalWeight()
    {
        _totalAttackWeights = 0;

        List<int> validIndexes = new List<int>();
        for (int i = 0; i < _attacksAvailable.Count; i++)
        {
            bool restrictedAttack = false;
            for (int j = 0; j < _restrictedAbilities.Count; j++)
            {
                if (_restrictedAbilities[j] == i)
                {
                    restrictedAttack = true;
                    break;
                }
            }

            if (!restrictedAttack)
            {
                validIndexes.Add(i);
            }
        }

        for (int i = 0; i < validIndexes.Count; i++)
        {
            _totalAttackWeights += _attacksAvailable[validIndexes[i]].GetAttackRandomWeight();
        }

        RollNextAttack();
    }

    public int GetValidAttackIndex()
    {
        int retValue = int.MaxValue;
        int rolledAttackValue = 0;

        if (_attacksAvailable.Count > 0)
        {

            if (_restrictedAbilities.Count == 0)
            {
                rolledAttackValue = UnityEngine.Random.Range(1, _totalAttackWeights + 1);

                for (int i = 0; i < _attacksAvailable.Count; i++)
                {
                    if (rolledAttackValue > _attacksAvailable[i].GetAttackRandomWeight())
                    {
                        rolledAttackValue -= _attacksAvailable[i].GetAttackRandomWeight();
                    }
                    else
                    {
                        retValue = i;
                        break;
                    }
                }
            }
            else
            {
                List<int> validIndexes = new List<int>();
                for (int i = 0; i < _attacksAvailable.Count; i++)
                {
                    bool restrictedAttack = false;
                    for (int j = 0; j < _restrictedAbilities.Count; j++)
                    {
                        if (_restrictedAbilities[j] == i)
                        {
                            restrictedAttack = true;
                            break;
                        }
                    }

                    if (!restrictedAttack)
                    {
                        validIndexes.Add(i);
                    }
                }

                if (validIndexes.Count == 0)
                {
                    retValue = 0;
                }
                else
                {
                    rolledAttackValue = UnityEngine.Random.Range(1, _totalAttackWeights + 1);

                    for (int i = 0; i < validIndexes.Count; i++)
                    {
                        if (rolledAttackValue > _attacksAvailable[validIndexes[i]].GetAttackRandomWeight())
                        {
                            rolledAttackValue -= _attacksAvailable[validIndexes[i]].GetAttackRandomWeight();
                        }
                        else
                        {
                            retValue = validIndexes[i];
                            break;
                        }
                    }

                }
            }
        }
        return retValue;
    }
    #endregion

}
