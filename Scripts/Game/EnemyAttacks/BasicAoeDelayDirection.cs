using System.Collections;
using UnityEngine;

public class BasicAoeDelayDirection : BasicAoeController
{
    private const int DAMAGESOURCE_PER_EXPAND_COUNT = 1;
    private const float OFF_THE_GROUND_OFFSET = 0.1f;
    private const float RAYCAST_ORIGIN_OFFSET_Y = 5.0f;
    private const float RAYCAST_SCAN_LENGTH = 10.0f;

    [SerializeField]
    private Transform[] _baseAoeSpawnPosition;

    [Header("Tracking Expand Settings")]

    [SerializeField]
    private int _numExpansions = 2;

    [SerializeField]
    private float _delayBetweenExpansion = 0.5f;

    [SerializeField]
    private float _distanceBetweenPreviousExpand = 5.0f;

    [SerializeField]
    private LayerMask _groundLayer;

    private Vector3[] _rootOrigin;


    protected override void Start()
    {
        base.Start();
        _maxAOEDecals = _baseAoeSpawnPosition.Length * ((_numExpansions + 1) * DAMAGESOURCE_PER_EXPAND_COUNT);
        _damageSources = new DamageSourceData[_maxAOEDecals];
        for (int i = 0; i < _damageSources.Length; i++)
        {
            _damageSources[i] = new DamageSourceData();
        }
        _rootOrigin = new Vector3[_baseAoeSpawnPosition.Length];
    }

    protected override void UpdateCooldownLength()
    {
        base.UpdateCooldownLength();

        float attackLength = _delayBetweenExpansion * _numExpansions;

        _timeBetweenAttacks += attackLength;
    }

    public override void AnimTriggerStart()
    {
        if (_defeated.IsDead())
        {
            return;
        }

        GameController.Instance.PlaySfxOneShot(_onLaunchAudioClip);

        _delayedAttackRoutine = StartCoroutine(ExpandingRoutine());

        if (_overrideTargetLayersFromController == LayerMask.GetMask(ProjectLayerName.Nothing))
        {
            _targetLayers = _cachedTargetLayers;
        }
        else
        {
            _targetLayers = _overrideTargetLayersFromController;
        }
    }

    private IEnumerator ExpandingRoutine()
    {
        int decalCount = 0;
        int expandsCount = 1;
        float elapsedTime = 0.0f;

        for (int i = 0; i < _baseAoeSpawnPosition.Length; i++)
        {
            CreateAoeDecal(decalCount, pRootIndex: i);
            decalCount++;
        }

        while (decalCount < _maxAOEDecals)
        {
            elapsedTime += Time.deltaTime;

            if (elapsedTime >= _delayBetweenExpansion)
            {
                elapsedTime = 0.0f;
                for (int i = 0; i < _baseAoeSpawnPosition.Length; i++)
                {
                    CreateExpandRingDecal(decalCount, expandsCount, pRootIndex: i);
                    decalCount += DAMAGESOURCE_PER_EXPAND_COUNT;
                }
                
                expandsCount++;
            }
            yield return null;
        }

        _delayedAttackRoutine = null;
    }

    private void CreateAoeDecal(int pDecalCount, int pRootIndex)
    {
        Vector3 attackPosition = Vector3.zero;
        attackPosition = new Vector3(_baseAoeSpawnPosition[pRootIndex].position.x, 
            _baseAoeSpawnPosition[pRootIndex].position.y + OFF_THE_GROUND_OFFSET, 
            _baseAoeSpawnPosition[pRootIndex].position.z);
        _rootOrigin[pRootIndex] = attackPosition;
        CreateDecal(pDecalCount, attackPosition, pValidOrigin: true);
    }

    private void CreateExpandRingDecal(int pDecalCount, int pExpandsCount, int pRootIndex)
    {
        Vector3 attackPosition = Vector3.zero;
        Vector3 raycastOrigin = Vector3.zero;
        for (int i = 0; i < DAMAGESOURCE_PER_EXPAND_COUNT; i++)
        {
            attackPosition = new Vector3(_rootOrigin[pRootIndex].x, _rootOrigin[pRootIndex].y, _rootOrigin[pRootIndex].z);
            attackPosition += _baseAoeSpawnPosition[pRootIndex].forward * (pExpandsCount * _distanceBetweenPreviousExpand);

            raycastOrigin = attackPosition;
            raycastOrigin.y += RAYCAST_ORIGIN_OFFSET_Y;

            //If the expected position of the target is off the map move it to the map origin away from emverything
            RaycastHit onHit;
            bool validOrigin = true;
            if (!Physics.Raycast(raycastOrigin, Vector3.down, out onHit, RAYCAST_SCAN_LENGTH, _groundLayer))
            {
                attackPosition = Vector3.zero;
                validOrigin = false;
            }

            CreateDecal(pDecalCount + i, attackPosition, validOrigin);
        }
    }

    private void CreateDecal(int pDamageSourceIndex, Vector3 pOrigin, bool pValidOrigin)
    {
        _damageSources[pDamageSourceIndex].AoeDecal = Instantiate(_aoeDecalPrefab,
            pOrigin,
            Quaternion.identity).
            GetComponent<AoEDecalController>();

        _damageSources[pDamageSourceIndex].AoeDecal.SetUpDecal(this, _aoeChargeTime, _aoeSize, _aoeVisualPrefab, pDamageSourceIndex, pValidOrigin, OnDecalComplete);
    }
}
