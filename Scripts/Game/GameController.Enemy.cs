using System.Collections;
using TVEngine.Utility.Inspector;
using UnityEngine;

public partial class GameController
{
    [Header("Enemy Controller")]
    [Tooltip("How Frequently all the enemies update thier Tick call (60fps = 60 calls per second = 1/60 = .016)")]
    [SerializeField]
    private float _allEnemyTickRate = .016f;

    [SerializeField]
    private GameObject _wanderingSpirit;

    [SerializeField]
    private int _spawnRatePercentageOnEnemyDeath = 10;

    [SerializeField]
    private int _weightIncreaseOnNoSpawn = 10;

    [SerializeField]
    [ReadOnly]
    private int _missedRolls = 0;

    #region Delegate Functions
    /// <summary>
    /// Invoked to Tick() all enemies at specific intervals for optimzation
    /// </summary>
    public delegate void EnemyTick(float pElapsedTime);
    public EnemyTick OnEnemyTickDelegate;
    #endregion

    private EnemyCharacterController _possessedEnemy;

    public void SetPossessedEnemy(EnemyCharacterController pPossessedEnemy, float pSlowAmount)
    {
        if (pPossessedEnemy != null)
        {
            _possessedEnemy = pPossessedEnemy;
            _playerController.OnPossessionStart();
            _possessedEnemy.OnPossessionStart(pSlowAmount);
            OnObjectPossessedHudUpdate(pPossessedEnemy.GetAimIndicatorTransform());
        }
        else
        {
            if (_possessedEnemy != null)
            {
                _possessedEnemy.OnPossessionEnd();
                _possessedEnemy = null;
                OnObjectPossessedHudUpdate();
            }
            _playerController.OnPossessionEnd();
        }
    
    }


    private IEnumerator TickAllEnemiesRoutine()
    {
        float elapsedTime = 0.0f;
        while (true)
        {
            elapsedTime += Time.deltaTime;

            if (elapsedTime > _allEnemyTickRate)
            {
                elapsedTime = 0.0f;
                OnEnemyTickDelegate?.Invoke(_allEnemyTickRate);
            }
            yield return new WaitForFixedUpdate();
        }
    }

    public void OnBossDefeated()
    {
        _bossController?.OnBossDefeated();
    }

    public Transform OnEnemyDefeated(EnemyCharacterController pEnemyController, bool pDeathCausedOffOfMap)
    {
        OnEnemyDefeatedDelegate?.Invoke(pEnemyController);
        if (!pDeathCausedOffOfMap)
        {
            SpawnLoot(pEnemyController.GetLoot(), pEnemyController.transform);
            return SpawnWanderingSpirit(pEnemyController);
        }
        return null;
    }

    private Transform SpawnWanderingSpirit(EnemyCharacterController pEnemyController)
    {
        // Do we need a is boss special case?
        if (pEnemyController.SpawnSpirit())
        {
            int spawnWeightOverride = Mathf.Clamp(_spawnRatePercentageOnEnemyDeath + (_missedRolls * _weightIncreaseOnNoSpawn), 1, 100);

            if (Random.Range(1, 101) <= _spawnRatePercentageOnEnemyDeath)
            {
                BaseCharacterController baseCharacterController = Instantiate(_wanderingSpirit, pEnemyController.transform.position, Quaternion.identity).GetComponent<BaseCharacterController>();
                baseCharacterController?.SpawnAtRuntime();
                _missedRolls = 0;
                return baseCharacterController.transform;
            }
            else
            {
                _missedRolls++;
            }
        }

        return null;
    }

#if ENABLE_CHEAT
    public void ConsoleEnemyRevive()
    {
        OnWaypointFadeComplete();
    }
#endif
}
