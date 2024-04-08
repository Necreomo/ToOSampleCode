using Gamekit3D;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BasicAoeController : BaseBasicAttackController
{
    [Serializable]
    private enum AoeAIType
    {
        Random,
        Smart
    }

    public class DamageSourceData
    {
        public AoEDecalController AoeDecal = null;
        public MeleeWeapon MeleeWeapon = null;
        public float ElapsedTime = 0.0f;
    }

    [Serializable]
    public class AdditionalSpawn
    {
        public GameObject Object = null;
        public int NumSpawnsPerAttack = 0;
        [Range(0, 100)]
        public int Probability = 0;

        public bool RollForAdditionalSpawn(int pCurrentSpawnCount)
        {
            if (!(Object == null || NumSpawnsPerAttack == 0 || Probability == 0))
            {
                if (pCurrentSpawnCount < NumSpawnsPerAttack)
                {
                    return UnityEngine.Random.Range(0, 101) < Probability;
                }
            }

            return false;
        }

        public bool HasObstructionNearSpawnPoint(Vector3 pSpawnPoint)
        {
            string[] blockingLayers = {
                            ProjectLayerName.Player,
                            ProjectLayerName.Prop,
                            ProjectLayerName.Enemy,
                            ProjectLayerName.AllCollateral,
                            ProjectLayerName.NPC,
                            ProjectLayerName.InteractivePlayerPet
                        };

            int blockingLayerMask = LayerMask.GetMask(blockingLayers);

            Collider[] test = Physics.OverlapBox(pSpawnPoint, halfExtents: GameConstants.SpawnEnemyClearance, Quaternion.identity, blockingLayerMask);

            return Physics.CheckBox(pSpawnPoint, halfExtents: GameConstants.SpawnEnemyClearance, Quaternion.identity, blockingLayerMask);
        }
    }

    [Header("AOE Controller Settings")]
    [SerializeField]
    protected GameObject _aoeDecalPrefab;

    [SerializeField]
    protected GameObject _aoeVisualPrefab;

    [SerializeField]
    private AoeAIType _AIType = AoeAIType.Random;

    [SerializeField]
    protected LayerMask _overrideTargetLayersFromController;

    [SerializeField]
    [Tooltip("Time Needed To Charge up an aoe attack")]
    protected float _aoeChargeTime = 3.0f;

    [SerializeField]
    [Tooltip("Size of the aoe region based on unity units")]
    protected float _aoeSize = 3.0f;

    [SerializeField]
    [Tooltip("Amount of time for each aoe to spawn, if set to 0 all will spawn at the same time")]
    private float _spawnDelay = 0.0f;

    [SerializeField]
    [Tooltip("Max Aoe Decals to create, note this value can be overwritten by chilldd classes")]
    protected int _maxAOEDecals = 1;

    [Header("Damage Source Params")]
    [SerializeField]
    private GameObject _damageSourcePrefab;

    [SerializeField]
    private float _damageSourceTimeToLive = 0.2f;

    [SerializeField]
    private MeleeWeapon.AttackData[] _damageSourceData;

    [Header("Additional Spawn")]
    
    [SerializeField]
    protected AdditionalSpawn _additionalSpawn;

    [SerializeField]
    private Transform _additionalSpawnRoot;

    [Header("Audio Playback")]

    [SerializeField]
    protected SfxDictionary.SfxIds _onLaunchAudioClip = SfxDictionary.SfxIds.None;

    [SerializeField]
    private SfxDictionary.SfxIds _onBreakStartAudioClip = SfxDictionary.SfxIds.None;


    [Header("Visual Playback")]
    [SerializeField]
    private VfxDictionary.VfxIds _onBreakVfx = VfxDictionary.VfxIds.None;

    protected Transform _attackTarget;
    protected DamageSourceData[] _damageSources;
    private bool _ownerDestroyed = false;

    //Cached attacked Data
    private float _cachedAttackRange;
    protected LayerMask _cachedTargetLayers;

    protected Coroutine _delayedAttackRoutine = null;

    protected int _numAdditionalObjectsSpawned;

    private List<GameObject> _spawnedObjects = new List<GameObject>();

    protected override void Start()
    {
        base.Start();
        _damageSources = new DamageSourceData[_maxAOEDecals];
        for (int i = 0; i < _damageSources.Length; i++)
        {
            _damageSources[i] = new DamageSourceData();
        }
    }

    private void OnEnable()
    {
        _ownerDestroyed = false;
    }

    private void OnDestroy()
    {
        _ownerDestroyed = true;
        if (!GameController.Instance.ApplicationEnding)
        {
            CleanUp();
        }
    }

    public override void OnDeath()
    {
        _ownerDestroyed = true;
        CleanUp();
    }

    public override void CleanUp()
    {
        base.CleanUp();
        StopAllCoroutines();
        CleanUpDecalsAndDamageSources();
        CleanUpAdditionalSpawns();
    }

    public override bool Attack(Transform pTarget, float pAttackRange, Action pAttackManagerCallback, LayerMask pTargetLayers, int pAttackIndex)
    {
        bool isAttackRegistered = false;

        if (_attackCooldownRoutine == null)
        {
            _attackTarget = pTarget;

            isAttackRegistered = base.Attack(pTarget, pAttackRange, pAttackManagerCallback, pTargetLayers, pAttackIndex);

            _cachedAttackRange = pAttackRange;
            _cachedTargetLayers = pTargetLayers;
        }
        return isAttackRegistered;
    }

    private IEnumerator StartDelayedAttacks(float pAttackRange, bool pValidOrigin)
    {
        int aoesCreated = 0;
        float elapsedTime = 0.0f;

        CreateAoeDecal(pAttackRange, aoesCreated, pValidOrigin);
        aoesCreated++;

        while (aoesCreated < _maxAOEDecals)
        {
            elapsedTime += Time.deltaTime;

            if (elapsedTime >= _spawnDelay)
            {
                elapsedTime = 0;
                CreateAoeDecal(pAttackRange, aoesCreated, pValidOrigin);
                aoesCreated++;
            }
            yield return null;
        }
        _delayedAttackRoutine = null;
    }

    private void CreateAoeDecal(float pRangeOffest, int pDecalIndex, bool pValidOrigin)
    {
        Vector3 attackPosition = Vector3.zero;

        switch (_AIType)
        {
            case AoeAIType.Random:
                attackPosition = new Vector3(UnityEngine.Random.Range(transform.position.x - pRangeOffest, transform.position.x + pRangeOffest),
                                            _attackTarget.position.y + 0.1f,
                                            UnityEngine.Random.Range(transform.position.z - pRangeOffest, transform.position.z + pRangeOffest));
                break;
            case AoeAIType.Smart:
                attackPosition = new Vector3(_attackTarget.position.x, _attackTarget.position.y + 0.1f, _attackTarget.position.z);
                break;
            default:
                break;
        }


        _damageSources[pDecalIndex].AoeDecal = Instantiate(_aoeDecalPrefab,
                    attackPosition,
                    Quaternion.identity).
                    GetComponent<AoEDecalController>();

        _damageSources[pDecalIndex].AoeDecal.SetUpDecal(this, _aoeChargeTime, _aoeSize, _aoeVisualPrefab, pDecalIndex, pValidOrigin, OnDecalComplete);
    }

    protected void OnDecalComplete(int pDecalIndex, Vector3 pDamageSourcePosition, bool pValidPosition)
    {
        _damageSources[pDecalIndex].MeleeWeapon = Instantiate(_damageSourcePrefab, pDamageSourcePosition, Quaternion.identity).GetComponent<MeleeWeapon>();
#if UNITY_EDITOR
        _damageSources[pDecalIndex].MeleeWeapon.gameObject.name = _damageSources[pDecalIndex].MeleeWeapon.gameObject.name + pDecalIndex;
#endif
        _damageSources[pDecalIndex].MeleeWeapon.targetLayers = _targetLayers;

        //Need to do a deep copy
        _damageSources[pDecalIndex].MeleeWeapon._attackData = new MeleeWeapon.AttackData[_damageSourceData.Length];
        for (int i = 0; i < _damageSourceData.Length; i++)
        {
            _damageSources[pDecalIndex].MeleeWeapon._attackData[i] = new MeleeWeapon.AttackData();
            _damageSources[pDecalIndex].MeleeWeapon._attackData[i].AttackLabel = _damageSourceData[i].AttackLabel;
            _damageSources[pDecalIndex].MeleeWeapon._attackData[i].Damage = _damageSourceData[i].Damage;
            _damageSources[pDecalIndex].MeleeWeapon._attackData[i].OnHitPushBackForce = _damageSourceData[i].OnHitPushBackForce;

            _damageSources[pDecalIndex].MeleeWeapon._attackData[i].AttackPoints = new MeleeWeapon.AttackPoint[_damageSourceData[i].AttackPoints.Length];
            for (int j = 0; j < _damageSourceData[i].AttackPoints.Length; j++)
            {
                _damageSources[pDecalIndex].MeleeWeapon._attackData[i].AttackPoints[j] = new MeleeWeapon.AttackPoint();
                _damageSources[pDecalIndex].MeleeWeapon._attackData[i].AttackPoints[j].diameter = _damageSourceData[i].AttackPoints[j].diameter;
#if UNITY_EDITOR
                if (_damageSources[pDecalIndex].MeleeWeapon._attackData[i].AttackPoints[j].diameter == 0)
                    Debug.LogError($"AttackPoint {j} of Attack Label: {_damageSources[pDecalIndex].MeleeWeapon._attackData[i].AttackLabel}) has a diameter set to 0");
#endif
                _damageSources[pDecalIndex].MeleeWeapon._attackData[i].AttackPoints[j].offset = _damageSourceData[i].AttackPoints[j].offset;
                _damageSources[pDecalIndex].MeleeWeapon._attackData[i].AttackPoints[j].attackRoot = _damageSourceData[i].AttackPoints[j].attackRoot;
            }
        }
        _damageSources[pDecalIndex].MeleeWeapon.SetOwner(_damageSources[pDecalIndex].MeleeWeapon.gameObject);
        _damageSources[pDecalIndex].MeleeWeapon.BeginAttack(pThrowingAttack: false);
        
        GameController.Instance.PlaySfxOneShot(_onBreakStartAudioClip);
        if (_onBreakVfx != VfxDictionary.VfxIds.Count && _onBreakVfx != VfxDictionary.VfxIds.None)
        {
            Vector3 randomForwardDirection = Quaternion.AngleAxis(UnityEngine.Random.Range(1.0f, 355.0f), Vector3.up) * transform.forward;
            GameController.Instance.CreateVfx(_onBreakVfx, _damageSources[pDecalIndex].MeleeWeapon.transform.position, randomForwardDirection);
        }

        if (pValidPosition)
        {
            CreateAdditionalSpawn(pDamageSourcePosition);
        }

        if (pDecalIndex == _maxAOEDecals - 1)
        {
            ResetSpawnlingCount();
        }
    }

    private void CleanUpDecalsAndDamageSources()
    {
        for (int i = 0; i < _damageSources.Length; i++)
        {
            if (_damageSources[i].AoeDecal != null)
            {
                Destroy(_damageSources[i].AoeDecal.gameObject);
                _damageSources[i].AoeDecal = null;
            }

            if (_damageSources[i].MeleeWeapon != null)
            {
                Destroy(_damageSources[i].MeleeWeapon.gameObject);
                _damageSources[i].MeleeWeapon = null;
            }
        }
    }

    public void CleanUpAdditionalSpawns()
    {
        Pushable pushableComponent = null;
        EnemyCharacterController enemyController = null;

        for (int i = 0; i < _spawnedObjects.Count; i++)
        {
            if (_spawnedObjects[i] == null)
            {
                continue;
            }

            pushableComponent = null;
            enemyController = null;

            pushableComponent = _spawnedObjects[i].GetComponent<Pushable>();

            if (pushableComponent != null)
            {
                Damageable.DamageMessage pDamageMessage = new Damageable.DamageMessage();
                pDamageMessage.damagerTag = ProjectTagName.PetWeaponPush;
                pDamageMessage.onHitPushBackForce = GameConstants.PushAttackVerifyThreshold + 1.0f;
                pushableComponent.ApplyDamage(pDamageMessage);
                continue;
            }

            enemyController = _spawnedObjects[i].GetComponent<EnemyCharacterController>();

            if (enemyController != null)
            {
                enemyController?.DeathOffMap();
                continue;
            }
        }

        _spawnedObjects.Clear();
    }

    protected void ResetSpawnlingCount()
    {
        _numAdditionalObjectsSpawned = 0;
    }

    /// <summary>
    /// We can probably optimzie this, i tired turning a coroutine on and off but there are scenarios were timing was causing issues
    /// </summary>
    private void Update()
    {
        if (_ownerDestroyed)
        {
            return;
        }

        for (int i = 0; i < _damageSources.Length; i++)
        {
            if (_damageSources[i].MeleeWeapon != null)
            {
                if (_damageSources[i].ElapsedTime > _damageSourceTimeToLive)
                {
                    _damageSources[i].MeleeWeapon.EndAttack();
                    Destroy(_damageSources[i].MeleeWeapon.gameObject);
                    _damageSources[i].MeleeWeapon = null;
                    _damageSources[i].ElapsedTime = 0.0f;
                }
                else
                {
                    _damageSources[i].ElapsedTime += Time.deltaTime;
                }
            }
        }
    }

    #region AnimationCallbacks
    public override void AnimTriggerStart()
    {
        base.AnimTriggerStart();

        if (_defeated.IsDead())
        {
            return;
        }

        GameController.Instance.PlaySfxOneShot(_onLaunchAudioClip);

        if (_spawnDelay == 0)
        {
            for (int i = 0; i < _maxAOEDecals; i++)
            {
                CreateAoeDecal(_cachedAttackRange, i, pValidOrigin: true);
            }
        }
        else
        {
            _delayedAttackRoutine = StartCoroutine(StartDelayedAttacks(_cachedAttackRange, pValidOrigin: true));
        }

        if (_overrideTargetLayersFromController == LayerMask.GetMask(ProjectLayerName.Nothing))
        {
            _targetLayers = _cachedTargetLayers;
        }
        else
        {
            _targetLayers = _overrideTargetLayersFromController;
        }
    }

    public override void AnimTriggerEnd()
    {
        // Do nothing as the cd starting start after the last damage source is created
        // base.AnimTriggerEndAttack();
    }
    #endregion

    public override void OnDamaged(string pDamageSourceTag)
    {
        base.OnDamaged(pDamageSourceTag);

        if (pDamageSourceTag == ProjectTagName.PlayerWeapon || pDamageSourceTag == ProjectTagName.PlayerWeaponBot)
        {
            if (_delayedAttackRoutine != null)
            {
                StopCoroutine(_delayedAttackRoutine);
                ResetSpawnlingCount();
                if (_attackCooldownRoutine == null)
                {
                    _attackCooldownRoutine = StartCoroutine(StartAttackCooldown());
                }
            }
        }
    }

    /// <summary>
    /// Called when a scripted walk needs to happen to 
    /// </summary>
    public override void OnStartScriptedMove()
    {
        base.OnStartScriptedMove();
        if (_delayedAttackRoutine != null)
        {
            StopCoroutine(_delayedAttackRoutine);
            ResetSpawnlingCount();
            if (_attackCooldownRoutine == null)
            {
                _attackCooldownRoutine = StartCoroutine(StartAttackCooldown());
            }
        }
    }

    protected void CreateAdditionalSpawn(Vector3 pDamageSourcePosition)
    {
        if (_additionalSpawn.RollForAdditionalSpawn(_numAdditionalObjectsSpawned) &&
            !_additionalSpawn.HasObstructionNearSpawnPoint(pDamageSourcePosition))
        {
            SpawnAdditional(pDamageSourcePosition);
        }
    }

    private void SpawnAdditional(Vector3 pDamageSourcePosition)
    {
        _numAdditionalObjectsSpawned++;
        _spawnedObjects.Add(Instantiate(_additionalSpawn.Object, pDamageSourcePosition, Quaternion.identity));
        _spawnedObjects[_spawnedObjects.Count - 1].transform.SetParent(_additionalSpawnRoot);
        EnemySpawner enemySpawner = _spawnedObjects[_spawnedObjects.Count - 1].GetComponent<EnemySpawner>();
        if (enemySpawner != null)
        {
            enemySpawner.AddOnSpawnCallback(AddAdditionalSpawnReference);
            enemySpawner.AddSpawnToParent(_additionalSpawnRoot);
        }
    }

    private void AddAdditionalSpawnReference(GameObject pSpawnedObject)
    {
        _spawnedObjects.Add(pSpawnedObject);

        EnemyCharacterController spawnedEnemy = pSpawnedObject.GetComponent<EnemyCharacterController>();
        if (pSpawnedObject != null)
        {
            spawnedEnemy.ChangeTarget(GameController.Instance.GetMainCharacterTransform());
        }
    }

}
