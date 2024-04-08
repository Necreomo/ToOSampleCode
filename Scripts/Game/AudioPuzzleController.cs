using System;
using System.Collections;
using System.Collections.Generic;
using TVEngine.Utility.Inspector;
using UltEvents;
using UnityEngine;

public class AudioPuzzleController : MonoBehaviour
{
    private enum State
    { 
        Idle,
        Playback,
        AwaitingInput,
        Evaluate,
        Completed,
    }

    [Serializable]
    private enum Evaluation
    { 
        EveryStep,
        FullSequence
    }

    [Serializable]
    private class ManualPuzzle
    {
        [SerializeField]
        public int[] Element;
    }

    [Header("Connected Refernces")]
    [SerializeField]
    private PuzzleAudioObject[] _puzzleAudioObject;

    [SerializeField]
    private Transform _cancelHudPivot;

    [SerializeField]
    private GameObject _interactRegion;

    [SerializeField]
    private GameObject _restrictBlockersRoot;

    [SerializeField]
    private SfxDictionary.SfxIds _sfxSuccess = SfxDictionary.SfxIds.None;

    [SerializeField]
    private SfxDictionary.SfxIds _sfxFail = SfxDictionary.SfxIds.None;

    [SerializeField]
    private float _evaluationHoldTime = 1.0f;

    [Header("Randomize Sequence Parameters")]
    [SerializeField]
    private bool _randomizeSequence = false;

    [SerializeField]
    private int[] _randomizeRoundSequenceLengths;

    
    [Header("Manual Sequence")]
    [SerializeField]
    private ManualPuzzle[] _manualPuzzle;
    

    [Header("Settings")]
    [SerializeField]
    private float _playbackIntervalSpeed = 1.5f;

    [SerializeField]
    private Evaluation _evaluationType = Evaluation.EveryStep;

    [Header("On Puzzle Complete")]
    [SerializeField]
    private UltEvent _onPuzzleComplete;

    [ReadOnly]
    [SerializeField]
    private State _currentState = State.Idle;

    private List<int>[] _finalSequence;

    private int _numRounds = 1;
    private int _currentRound = 0;
    private List<int> _currentSequence = new List<int>();

    private Coroutine _playbackRoutine = null;
    private Coroutine _evalutionHoldRoutine = null;
#if UNITY_EDITOR
    private bool _debugInfo = false;
#endif

    private void Start()
    {
        RegisterPuzzleObjects();
        InitializeSequence();
        _restrictBlockersRoot.SetActive(false);
        _interactRegion.SetActive(true);

        GameController.Instance.OnWaypointInteractedDelegate += OnWaypointInteract;
    }

    private void RegisterPuzzleObjects()
    {
        for (int i = 0; i < _puzzleAudioObject.Length; i++)
        {
            _puzzleAudioObject[i].RegisterInteractCallback(PuzzleObjectInteractCallback);
        }
    }

    private void InitializeSequence()
    {
        if (!_randomizeSequence)
        {

#if UNITY_EDITOR
            if (_manualPuzzle.Length == 0)
            {
                Debug.LogError("No Entries in manual sequence was created aborting initialization!, Did you intend to use randomization?");
                return;
            }
#endif

            _numRounds = _manualPuzzle.Length;
            _finalSequence = new List<int>[_numRounds];

            for (int i = 0; i < _manualPuzzle.Length; i++)
            {
                _finalSequence[i] = new List<int>();
                for (int j = 0; j < _manualPuzzle[i].Element.Length; j++)
                {
                    _finalSequence[i].Add(_manualPuzzle[i].Element[j]);
                }
            }

        }
        else
        {
            _numRounds = _randomizeRoundSequenceLengths.Length;
            _finalSequence = new List<int>[_numRounds];

            int randomNumber = 0;

            for (int i = 0; i < _numRounds; i++)
            {
                _finalSequence[i] = new List<int>();
                for (int j = 0; j < _randomizeRoundSequenceLengths[i]; j++)
                {
                    randomNumber = UnityEngine.Random.Range(0, _puzzleAudioObject.Length);

                    if(j >= 1)
                    {
                        if (randomNumber == _finalSequence[i][j - 1])
                        {
                            randomNumber++;
                            if (randomNumber >= _puzzleAudioObject.Length)
                            {
                                randomNumber = 0;
                            }
                        }
                    }
                    _finalSequence[i].Add(randomNumber);
                }
            }

        }
    }

    public void StartPuzzle()
    {
        if (_currentState == State.Idle)
        {
            GameController.Instance.OnInteractAudioPuzzle();
            _playbackRoutine = StartCoroutine(StartCurrentRoundPlayback());
            _interactRegion.SetActive(false);
            _restrictBlockersRoot.SetActive(true);
#if UNITY_EDITOR
            _debugInfo = true;  
#endif
        }
    }

    public void OnPuzzleFail()
    {
        GameController.Instance.PlaySfxOneShot(_sfxFail);
        ResetPuzzle();
        _restrictBlockersRoot.SetActive(false);
        _interactRegion.SetActive(true);
        GameController.Instance.OnAudioPuzzleExit();
#if UNITY_EDITOR
        _debugInfo = false;
#endif
    }

    public void OnPuzzleSuccess()
    {
        GameController.Instance.PlaySfxOneShot(_sfxSuccess);
        _currentState = State.Completed;
        _currentSequence.Clear();
        _onPuzzleComplete?.Invoke();
        _restrictBlockersRoot.SetActive(false);
        GameController.Instance.OnAudioPuzzleExit();
        GameController.Instance.OnWaypointInteractedDelegate -= OnWaypointInteract;
#if UNITY_EDITOR
        _debugInfo = false;
#endif
    }

    private IEnumerator StartCurrentRoundPlayback()
    {
        _currentState = State.Playback;
        yield return new WaitForFixedUpdate();
        GameController.Instance.StartEventSequence(pShowLetterbox: true, pSkipAggroLock: true);
        int currentSequenceIterator = 0;
        float elapsedIntervalTime = 0.0f;
        while (currentSequenceIterator < _finalSequence[_currentRound].Count)
        {
            elapsedIntervalTime += Time.deltaTime;
            if (elapsedIntervalTime > _playbackIntervalSpeed)
            {
                elapsedIntervalTime = 0.0f;
                _puzzleAudioObject[_finalSequence[_currentRound][currentSequenceIterator]].SimulateBellRing();
                currentSequenceIterator++;
            }
            yield return new WaitForFixedUpdate();
        }
        _currentState = State.AwaitingInput;
        GameController.Instance.EndEventSequence(pShowLetterbox: true, pSkipAggroRelease: true);
        
        //Note: this is a hacky fix because when a nevent sequencer releases it unlocsk the cahracters invuln sstate
        //This will reset it
        GameController.Instance.OnInteractAudioPuzzle();
        _playbackRoutine = null;
    }

    private void PuzzleObjectInteractCallback(int pInstanceId)
    {
        if (_currentState != State.AwaitingInput)
        {
            return;
        }

        int index = int.MaxValue;

        for (int i = 0; i < _puzzleAudioObject.Length; i++)
        {
            if (_puzzleAudioObject[i].GetInstanceID() == pInstanceId)
            {
                index = i;
                break;
            }
        }

#if UNITY_EDITOR
        if (index == int.MaxValue)
        {
            Debug.LogError("Interact Callback to controller passed invalid id ignoreing controller evaluation on callback");
            return;
        }
#endif

        _currentSequence.Add(index);

        if (_evaluationType == Evaluation.EveryStep)
        {
            EvaluateStep();
        }
        else if (_evaluationType == Evaluation.FullSequence)
        {
            if (_currentSequence.Count == _finalSequence[_currentRound].Count)
            {
                EvaluateFullSequence();
            }
        }
    }

    private void EvaluateStep()
    {
        _currentState = State.Evaluate;
        int currentIteration = _currentSequence.Count - 1;
        bool validPair = true;
        if (_currentSequence[currentIteration] != _finalSequence[_currentRound][currentIteration])
        {
            validPair = false;
        }

        if (validPair)
        {   
            // if last item in the sequence go to the next round if available
            if (_currentSequence.Count == _finalSequence[_currentRound].Count)
            {
                _evalutionHoldRoutine = StartCoroutine(EvaluationHoldRoutine(SuccessfulSequenceEvaluation));
                if (_currentState == State.Completed)
                {
                    return;
                }
            }
            _currentState = State.AwaitingInput;
        }
        else
        {
            _evalutionHoldRoutine = StartCoroutine(EvaluationHoldRoutine(OnPuzzleFail));
        }
    }

    private void EvaluateFullSequence()
    {
        _currentState = State.Evaluate;
        bool theSame = true;
        for (int i = 0; i < _currentSequence.Count; i++)
        {
            if (_currentSequence[i] != _finalSequence[_currentRound][i])
            {
                theSame = false;
                break;
            }
        }

        if (theSame)
        {
            _evalutionHoldRoutine = StartCoroutine(EvaluationHoldRoutine(SuccessfulSequenceEvaluation));
        }
        else
        {
            _evalutionHoldRoutine = StartCoroutine(EvaluationHoldRoutine(OnPuzzleFail));
        }
    }

    private void SuccessfulSequenceEvaluation()
    {
        _currentRound++;

        if (_currentRound >= _numRounds)
        {
            OnPuzzleSuccess();
#if UNITY_EDITOR
            _debugInfo = false; 
#endif
        }
        else
        {
            _currentSequence.Clear();
            _playbackRoutine = StartCoroutine(StartCurrentRoundPlayback());
        }
    }

    private void ResetPuzzle()
    {
        if (_playbackRoutine != null)
        {
            StopCoroutine(_playbackRoutine);
            _playbackRoutine = null;
        }
        if (_evalutionHoldRoutine != null)
        {
            StopCoroutine(_evalutionHoldRoutine);
            _evalutionHoldRoutine = null;
        }
        _currentState = State.Idle;
        _currentSequence.Clear();
        _currentRound = 0;
        InitializeSequence();
    }

    private IEnumerator EvaluationHoldRoutine(Action pAdvanceEvaluation)
    {
        float elapsedTime = 0.0f;
        while (elapsedTime < _evaluationHoldTime)
        {
            elapsedTime += Time.deltaTime;
            yield return new WaitForFixedUpdate();
        }

        pAdvanceEvaluation?.Invoke();
        _evalutionHoldRoutine = null;
    }

    /// <summary>
    /// Delegate subscriber for waypoint interaction to reset the system if it is in mid process and player has been teleported back to the waypoint
    /// </summary>
    private void OnWaypointInteract()
    {
        if (_currentState != State.Idle && _currentState != State.Completed)
        {
            ResetPuzzle();
            _restrictBlockersRoot.SetActive(false);
            _interactRegion.SetActive(true);
            GameController.Instance.OnAudioPuzzleExit();
#if UNITY_EDITOR
            _debugInfo = false;
#endif
        }
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        if(_debugInfo)
        {
            GUI.Label(new Rect(Screen.width / 2.0f, Screen.height / 2.0f, 500.0f, 20.0f), new GUIContent($"Current Round: {_currentRound}"));
            string expectedSequence;
            
            
            if(_currentRound < _numRounds)
            {
                expectedSequence =  string.Join( ",", _finalSequence[_currentRound]);
            }
            else
            {
                expectedSequence = "DONE!";
            }

            GUI.Label(new Rect(Screen.width / 2.0f , Screen.height / 2.0f + 20.0f, 500.0f, 20.0f), new GUIContent($"Current Sequence: {expectedSequence}"));
            string inputSequence = string.Join( ",", _currentSequence);
            GUI.Label(new Rect(Screen.width / 2.0f , Screen.height / 2.0f + 40.0f, 500.0f, 20.0f), new GUIContent($"Input Sequence: {inputSequence}"));
        }
    }
#endif

}
