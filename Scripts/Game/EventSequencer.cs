using System;
using System.Collections;
using System.Collections.Generic;
using UltEvents;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;

/// <summary>
/// Used to trigger a series of events that wait on completion of the previous event
/// </summary>
public class EventSequencer : MonoBehaviour
{
    
    [Serializable]
    private class TandemEventData
    {
        public int[] ConnectedEvents;
    }
    
    [Tooltip("Used to skip the character lockout when running through this trigger, mainly used for quest complete trigger")]
    [SerializeField]
    private bool _isQuestTrigger = false;

    [SerializeField]
    private bool _showletterbox = true;
    
    [Header("VERY SPECIFIC SETTINGS")]
    [Tooltip("Flag to not set aggro on enemeies after sequence complete, NOTE: THIS SHOULD ONLY BE SET IN VERY SPECIAL CASES LIKE END OF DEMO")]
    [SerializeField]
    private bool _skipAggroRelease = false;

    [Tooltip("Flag to not reveal player hud after sequence, NOTE: THIS SHOULD ONLY BE SET IN VERY SPECIAL CASES LIKE END OF DEMO")]
    [SerializeField]
    private bool _skipPlayerHudRelease = false;

    [Tooltip("Flag to not hide letterbox after sequence, NOTE: THIS SHOULD ONLY BE SET IN VERY SPECIAL CASES LIKE END OF DEMO")]
    [SerializeField]
    private bool _skipLetterboxRelease = false;

    [SerializeField]
    private UltEvent[] _sequenceOfEvents;

    [SerializeField]
    private TandemEventData[] _tandemEvents;

    [SerializeField]
    private UltEvent _onSequenceCompleteCallbacks;

    private Coroutine _sequencingRoutine = null;

    private IEnumerator[] _sequencer;   

    private int _initializeIterator = 0;

    private int _eventCriteria = 0;

    private Dictionary<int, int[]> _tandemEventLookup= new Dictionary<int, int[]>();

    /// <summary>
    /// if assigned means that this npc is related to a quest and should advance it...
    /// </summary>
    private string _questSpeaker = "NO_QUEST_SPEAKER";


    private void Start()
    {
        QuestWorldObject questWorldObject = GetComponent<QuestWorldObject>();
        if (questWorldObject != null)
        {
            _questSpeaker = questWorldObject._actorId;
        }
        InitalizeSequenceData();

        for (int i = 0; i < _tandemEvents.Length; i++)
        {
            _tandemEventLookup.Add(_tandemEvents[i].ConnectedEvents[0], _tandemEvents[i].ConnectedEvents);
        }
    }

    private void InitalizeSequenceData()
    {
        _sequencer = new IEnumerator[_sequenceOfEvents.Length];
        _initializeIterator = 0;
        for (int i = 0; i < _sequenceOfEvents.Length; i++)
        {
            _sequenceOfEvents[i].Invoke();
        }
    }

    public void StartSequence()
    {
        _sequencingRoutine = StartCoroutine(SequencingRoutine());
    }

    private IEnumerator SequencingRoutine()
    {
        if (!_isQuestTrigger)
        {
            GameController.Instance.StartEventSequence(_showletterbox);
        }

        for (int i = 0; i < _sequencer.Length; i++)
        {
            if (_tandemEventLookup.ContainsKey(i))
            {
                Coroutine[] tandemRoutines = new Coroutine[_tandemEventLookup[i].Length];
                int currentElement = 0;
                for (int j = 0; j < _tandemEventLookup[i].Length; j++)
                {
                    currentElement = _tandemEventLookup[i][j];
                    tandemRoutines[j] = StartCoroutine(_sequencer[currentElement]);
                }

                for (int j = 0; j < _tandemEventLookup[i].Length; j++)
                {
                    yield return tandemRoutines[j];
                }

                i = currentElement;
            }
            else
            {
                yield return _sequencer[i];
            }
        }

        _sequencingRoutine = null;
        if (!_isQuestTrigger)
        {
            GameController.Instance.EndEventSequence(_showletterbox, _skipAggroRelease, _skipPlayerHudRelease, _skipLetterboxRelease);
        }

        _onSequenceCompleteCallbacks?.Invoke();

        InitalizeSequenceData();
    }

    public bool IsSequenceActive()
    {
        return _sequencingRoutine != null;
    }

    #region Scripted Walk Event
    public void WalkForwardEvent(Transform pDestination, Transform pDestinationBot)
    {
        _sequencer[_initializeIterator] = WalkForwardRoutine(pDestination, pPlayerRun: false, pDestinationBot, pBotRun: false);
        _initializeIterator++;
    }

    public void WalkForwardEvent(Transform pDestination, bool pPlayerRun, Transform pDestinationBot, bool pBotRun)
    {
        _sequencer[_initializeIterator] = WalkForwardRoutine(pDestination, pPlayerRun, pDestinationBot, pBotRun);
        _initializeIterator++;
    }

    private IEnumerator WalkForwardRoutine(Transform pDestination, bool pPlayerRun, Transform pDestinationBot, bool pBotRun)
    {
        GameController.Instance.CharacterEventWalk(pDestination, pPlayerRun, pDestinationBot, pBotRun, EventCriteriaMet);
        while (_eventCriteria < 2)
        {
            yield return new WaitForFixedUpdate();
        }
        _eventCriteria = 0;
    }

    public void EnemyScriptedMove(EnemyCrabController pCrabBossController, Transform pDestination)
    {
        _sequencer[_initializeIterator] = EnemyScriptedMoveRoutine(pCrabBossController, pDestination);
        _initializeIterator++;
    }

    private IEnumerator EnemyScriptedMoveRoutine(EnemyCrabController pCrabBossController, Transform pDestination)
    {
        pCrabBossController.SetScriptedCinematicMoveDataEvent(pDestination);
        pCrabBossController.SetScriptedCinematicMoveCompleteCallback(EventCriteriaMet);

        while (_eventCriteria < 1)
        {
            yield return new WaitForFixedUpdate();
        }

        _eventCriteria = 0;
    }

    public void NPCScriptedMove(NpcCharacterController pNpcCharacterController, Transform pDestination)
    {
        _sequencer[_initializeIterator] = EnemyScriptedMoveRoutine(pNpcCharacterController, pDestination);
        _initializeIterator++;
    }

    private IEnumerator EnemyScriptedMoveRoutine(NpcCharacterController pNpcCharacterController, Transform pDestination)
    {
        pNpcCharacterController.SetScriptedCinematicMoveDataEvent(pDestination);
        pNpcCharacterController.SetScriptedCinematicMoveCompleteCallback(EventCriteriaMet);

        while (_eventCriteria < 1)
        {
            yield return new WaitForFixedUpdate();
        }

        _eventCriteria = 0;
    }

    public void WarpPlayerCharacter(Transform pDestination, Transform pDestinationBot)
    {
        _sequencer[_initializeIterator] = WarpObjectsRoutine(pDestination, pDestinationBot);
        _initializeIterator++;
    }

    private IEnumerator WarpObjectsRoutine(Transform pDestination, Transform pDestinationBot)
    {
        GameController.Instance.CharacterEventWarp(pDestination, pDestinationBot);
        yield return new WaitForFixedUpdate();
    }

    public void WarpObject(GameObject pObjectToMove, Transform pObjectDestination)
    {
        _sequencer[_initializeIterator] = WarpObjectsRoutine(pObjectToMove, pObjectDestination);
        _initializeIterator++;
    }

    private IEnumerator WarpObjectsRoutine(GameObject pObjectToMove, Transform pObjectDestination)
    {
        if (pObjectToMove != null && pObjectDestination != null)
        {
            NavMeshAgent navMeshAgent = pObjectToMove.GetComponent<NavMeshAgent>();

            if (navMeshAgent != null)
            {
                navMeshAgent.Warp(pObjectDestination.position);
            }
            
            pObjectToMove.transform.position = pObjectDestination.position;
            pObjectToMove.transform.rotation = pObjectDestination.rotation;
        }
        yield return new WaitForFixedUpdate();
    }

    #endregion
    #region Hud
    public void OpenDialog(DialogChain pDialogChain, bool pUseDialogCamera)
    {
        _sequencer[_initializeIterator] = DialogRoutine(pDialogChain, pUseDialogCamera);
        _initializeIterator++;
    }

    private IEnumerator DialogRoutine(DialogChain pDialogChain, bool pUseDialogCamera)
    {
        GameController.Instance.DialogEvent(pDialogChain, pUseDialogCamera, DialogEventCriteriaMet);
        
        while (_eventCriteria < 1)
        {
            yield return new WaitForFixedUpdate();
        }
        _eventCriteria = 0;
    }
    public void OpenTutorial(TutorialDictionary.TutorialID pTutorialId)
    {
        _sequencer[_initializeIterator] = TutorialRoutine(pTutorialId);
        _initializeIterator++;
    }

    private IEnumerator TutorialRoutine(TutorialDictionary.TutorialID pTutorialId)
    {
        GameController.Instance.TutorialEvent(pTutorialId, TutorialEventCriteriaMet);
        while (_eventCriteria < 1)
        {
            yield return new WaitForFixedUpdate();
        }
        _eventCriteria = 0;
    }

    public void ShowAreaTitleCard(string pMainTextId)
    {
        _sequencer[_initializeIterator] = ShowAreaTitleCardRoutine(pMainTextId);
        _initializeIterator++;
    }

    private IEnumerator ShowAreaTitleCardRoutine(string pMainTextId)
    {
        GameController.Instance.OpenAreaTitleCard(pMainTextId);
        yield return new WaitForFixedUpdate();
    }

    public void ShowBossHudElement(BaseCharacterController pBaseCharacterController)
    {
        _sequencer[_initializeIterator] = ShowBossHudElementRoutine(pBaseCharacterController);
        _initializeIterator++;
    }

    private IEnumerator ShowBossHudElementRoutine(BaseCharacterController pBaseCharacterController)
    {
        pBaseCharacterController.InitailizeBossHudElement();
        yield return new WaitForFixedUpdate();
    }

    public void UISetLetterBoxState(bool pState)
    {
        _sequencer[_initializeIterator] = UISetLetterBoxStateRoutine(pState);
        _initializeIterator++;
    }

    private IEnumerator UISetLetterBoxStateRoutine(bool pState)
    {
        GameController.Instance.UISetLetterBoxState(pState);
        yield return new WaitForFixedUpdate();
    }

    public void UIHudFade(bool pState)
    { 
        _sequencer[_initializeIterator] = UIHudFadeRoutine(pState);
        _initializeIterator++;
    }

    private IEnumerator UIHudFadeRoutine(bool pState)
    {
        GameController.Instance.UIHudFadeEvent(pState, EventCriteriaMet);
        while (_eventCriteria < 1)
        {
            yield return new WaitForFixedUpdate();
        }
        _eventCriteria = 0;
    }

    #endregion

    #region Camera
    public void ChangeVirtualCameraState(CameraController.VirtualCamerId pCameraId, bool pState)
    {
        _sequencer[_initializeIterator] = ChangeVirtualCameraRoutine(pCameraId, pState);
        _initializeIterator++;
    }

    private IEnumerator ChangeVirtualCameraRoutine(CameraController.VirtualCamerId pCameraId, bool pState)
    {
        GameController.Instance.CameraChangeEvent(pCameraId, pState);
        yield return new WaitForFixedUpdate();
    }

    public void CameraShake(float pStrength)
    {
        _sequencer[_initializeIterator] = CameraShakeRoutine(pStrength);
        _initializeIterator++;
    }

    private IEnumerator CameraShakeRoutine(float pStrength)
    {
        GameController.Instance.CameraShakeEvent(pStrength);
        yield return new WaitForFixedUpdate();
    }

    public void CameraCullDistance(string pLayerName, float pCullValue)
    {
        _sequencer[_initializeIterator] = CameraCullDistanceRoutine(pLayerName, pCullValue);
        _initializeIterator++;
    }

    private IEnumerator CameraCullDistanceRoutine(string pLayerName, float pCullValue)
    {
        GameController.Instance.ModifyCullDistance(pLayerName, pCullValue);
        yield return new WaitForFixedUpdate();
    }

    public void CameraCulDistance(int pLayerIndex, float pCullValue)
    {
        _sequencer[_initializeIterator] = CameraCullDistanceRoutine(pLayerIndex, pCullValue);
        _initializeIterator++;
    }

    private IEnumerator CameraCullDistanceRoutine(int pLayerIndex, float pCullValue)
    {
        GameController.Instance.ModifyCullDistance(pLayerIndex, pCullValue);
        yield return new WaitForFixedUpdate();
    }

    #endregion

    #region Vfx
    public void VfxPlayback(VfxDictionary.VfxIds pVfxId, Transform pVfxRoot, Vector3 pVfxOffset)
    {
        _sequencer[_initializeIterator] = VfxPlaybackRoutine(pVfxId, pVfxRoot, pVfxOffset);
        _initializeIterator++;
    }
    private IEnumerator VfxPlaybackRoutine(VfxDictionary.VfxIds pVfxId, Transform pVfxRoot, Vector3 pVfxOffset)
    {
        Vector3 pVfxFinalPosition = pVfxOffset;
        if (pVfxRoot != null)
        {
            pVfxFinalPosition += pVfxRoot.position;
        }

        GameController.Instance.CreateVfx(pVfxId, pVfxFinalPosition);
        yield return new WaitForFixedUpdate();
    }

    #endregion

    #region Audio
    public void SfxOneShot(SfxDictionary.SfxIds pSfxId)
    {
        _sequencer[_initializeIterator] = SfxOneShotRoutine(pSfxId);
        _initializeIterator++;
    }
    private IEnumerator SfxOneShotRoutine(SfxDictionary.SfxIds pSfxId)
    {
        GameController.Instance.PlaySfxOneShot(pSfxId);
        yield return new WaitForFixedUpdate();
    }

    public void Bgm(BgmDictionary.BgmIds bgmIds, bool pLoop)
    {
        _sequencer[_initializeIterator] = BgmRoutine(bgmIds, pLoop);
        _initializeIterator++;
    }
    private IEnumerator BgmRoutine(BgmDictionary.BgmIds bgmIds, bool pLoop)
    {
        GameController.Instance.PlayBgm(bgmIds, pLoop);
        yield return new WaitForFixedUpdate();
    }

    public void BgmCrossFade(BgmDictionary.BgmIds bgmIds, bool pLoop)
    {
        _sequencer[_initializeIterator] = BgmCrossfadeRoutine(bgmIds, pLoop);
        _initializeIterator++;
    }
    private IEnumerator BgmCrossfadeRoutine(BgmDictionary.BgmIds bgmIds, bool pLoop)
    {
        GameController.Instance.PlayCrossFadeBgm(bgmIds, pLoop);
        yield return new WaitForFixedUpdate();
    }
    public void BgmCrossFadeNothing()
    {
        _sequencer[_initializeIterator] = BgmCrossfadeRoutineNothing();
        _initializeIterator++;
    }
    private IEnumerator BgmCrossfadeRoutineNothing()
    {
        GameController.Instance.PlayCrossFadeBgmNoAudio();
        yield return new WaitForFixedUpdate();
    }

    public void BgmAllMixersChange(AudioMixerGroup pMixer)
    {
        _sequencer[_initializeIterator] = BgmAllMixersChangeRoutine(pMixer);
        _initializeIterator++;
    }

    private IEnumerator BgmAllMixersChangeRoutine(AudioMixerGroup pMixer)
    {
        GameController.Instance.SetBothBgmMixer(pMixer);
        yield return new WaitForFixedUpdate();
    }

    #endregion

    #region Quests
    public void AddActiveQuest(string pQuestId)
    {
        _sequencer[_initializeIterator] = AddActiveQuestRoutine(pQuestId);
        _initializeIterator++;
    }
    private IEnumerator AddActiveQuestRoutine(string pQuestId)
    {
        GameController.Instance.AddQuestToActiveList(pQuestId);
        yield return new WaitForFixedUpdate();
    }

    public void CompleteTriggerQuest()
    {
        _sequencer[_initializeIterator] = CompleteTriggerQuestRoutine();
        _initializeIterator++;
    }

    private IEnumerator CompleteTriggerQuestRoutine()
    {
        TriggerEventCriteriaMet();
        yield return new WaitForFixedUpdate();
    }

    #endregion

    #region Animation
    public void EnemyAnimationPlayback(BaseCharacterController pBaseCharacterController, string pAnimationStateName)
    {
        _sequencer[_initializeIterator] = EnemyAnimationRoutine(pBaseCharacterController, pAnimationStateName);
        _initializeIterator++;
    }

    private IEnumerator EnemyAnimationRoutine(BaseCharacterController pBaseCharacterController, string pAnimationStateName)
    {
        pBaseCharacterController.PlaybackSupplementAnimation(pAnimationStateName, EventCriteriaMet);
        while (_eventCriteria < 1)
        {
            yield return new WaitForFixedUpdate();
        }
        _eventCriteria = 0;
    }

    #endregion

    #region General
    public void ChangeGameObjectState(GameObject pGameObject, bool pState)
    {
        _sequencer[_initializeIterator] = ChangeGameObjectStateRoutine(pGameObject, pState);
        _initializeIterator++;
    }

    private IEnumerator ChangeGameObjectStateRoutine(GameObject pGameObject, bool pState)
    {
        pGameObject.SetActive(pState);
        yield return new WaitForFixedUpdate();
    }

    #endregion

    #region Criteria Met
    private void DialogEventCriteriaMet()
    {
        GameController.Instance.IsDialogRelatedToActiveQuest(_questSpeaker);
        EventCriteriaMet();
    }

    private void TutorialEventCriteriaMet()
    {
        GameController.Instance.IsTutorialRelatedToActiveQuest(_questSpeaker);
        EventCriteriaMet();
    }

    private void TriggerEventCriteriaMet()
    {
        GameController.Instance.IsTriggerRelatedToActiveQuest(_questSpeaker);
    }

    private void EventCriteriaMet()
    {
        _eventCriteria++;
    }
    #endregion
}
