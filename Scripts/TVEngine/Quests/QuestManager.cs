using System.Collections.Generic;
using UnityEngine;

namespace TVEngine.Quest
{
    public class QuestManager : MonoBehaviour
    {
        private class QuestChainProgress
        {
            public QuestChain QuestChain;
            public int CurrentStep;

            public QuestChainProgress(QuestChain pQuestChain)
            {
                QuestChain = pQuestChain;
                CurrentStep = 0;
            }

            public QuestChainProgress(QuestChain pQuestChain, int pCurrentStep)
            {
                QuestChain = pQuestChain;
                CurrentStep = pCurrentStep;
            }
        }

        [SerializeField]
        private QuestLookup _questLookup;
        private Dictionary<string, QuestWorldObject> _questWorldObjects = new Dictionary<string, QuestWorldObject>();

        private List<QuestChainProgress> _activeQuests = new List<QuestChainProgress>();

        private List<List<QuestLookup.ActorState>> _finalizeQuestCompletionTasks = new List<List<QuestLookup.ActorState>>();
        private List<List<QuestLookup.ActorState>> _finalizeNextQuestStartTasks = new List<List<QuestLookup.ActorState>>();

        /// <summary>
        /// Quest chain ids that have been completed
        /// </summary>
        private List<string> _completedQuestChains = new List<string>();

#if UNITY_EDITOR
        [Header("Debug")]
        [SerializeField]
        private bool _showActiveQuestList = false;
        private float _onGuiYOffset = 100.0f;
#endif

        public void RegisterQuestWorldObject(QuestWorldObject pQuestWorldObject)
        {
            if (!_questWorldObjects.ContainsKey(pQuestWorldObject._actorId))
            {
                _questWorldObjects.Add(pQuestWorldObject._actorId, pQuestWorldObject);
            }
#if UNITY_EDITOR
            else
            {
                Debug.LogError("QuestWorldObject already added to Quest Manager, ensure all quest objects have unique id");
                Debug.LogError($"Attempting Register Quest Id: {pQuestWorldObject._actorId}, Registerer: {pQuestWorldObject.name}");
                Debug.LogError($"Quest Manager Duplicate: {_questWorldObjects[pQuestWorldObject._actorId].name}");
            }
#endif
        }

        public void UnRegisterCurrentSceneQuestWorldObjects()
        {
            _questWorldObjects.Clear();
        }

        /// <summary>
        /// Adding active quest through event system
        /// </summary>
        /// <param name="pQuestChainId"></param>
        public void AddActiveQuest(string pQuestChainId)
        {
            if (!QuestIsCompleteOrActive(pQuestChainId))
            {
                QuestChain quest = _questLookup.GetQuestChainWithUniqueId(pQuestChainId);

                if (quest == null)
                {
#if UNITY_EDITOR
                    Debug.LogError($"Quest: {pQuestChainId}, does not exist in quest database!");
#endif
                    return;
                }

                _activeQuests.Add(new QuestChainProgress(quest));

                int lastElement = _activeQuests.Count - 1;
                int lastElementInChain = _activeQuests[lastElement].QuestChain.QuestSteps[0].StartActorStateChange.Count;

                for (int i = 0; i < lastElementInChain; i++)
                {
                    QuestLookup.ActorState actorState = _activeQuests[lastElement].QuestChain.QuestSteps[0].StartActorStateChange[i];

                    if (_questWorldObjects.ContainsKey(actorState.Id))
                    {
                        _questWorldObjects[actorState.Id].ChangeInWorldState(actorState.State);
                    }
#if UNITY_EDITOR
                    else
                    {
                        Debug.LogError($"{actorState.Id} has been not registered make sure quest inworld component exists with this id");
                    }
#endif

                }
            }
        }

        public void IsDialogRelatedToActiveQuest(string pQuestSpeaker)
        {
            UpdateActiveQuests(pQuestSpeaker, QuestLookup.QuestStepType.Speak);
        }
        public void IsTutorialRelatedToActiveQuest(string pQuestSpeaker)
        {
            UpdateActiveQuests(pQuestSpeaker, QuestLookup.QuestStepType.Tutorial);
        }

        public void IsTriggerRelatedToActiveQuest(string pQuestTrigger)
        {
            UpdateActiveQuests(pQuestTrigger, QuestLookup.QuestStepType.Trigger);
        }

        private void UpdateActiveQuests(string pQuestSpeaker, QuestLookup.QuestStepType pQuestStepType)
        {
            if (pQuestSpeaker != string.Empty && _activeQuests.Count > 0)
            {
                int activeQuestCount = _activeQuests.Count;

                List<int> questChainsComplete = new List<int>();

                for (int i = 0; i < activeQuestCount; i++)
                {
                    QuestStep questStep = _activeQuests[i].QuestChain.QuestSteps[_activeQuests[i].CurrentStep];

                    if (questStep.Type == pQuestStepType)
                    {
                        if (questStep.InWorldActorId == pQuestSpeaker)
                        {
                            _activeQuests[i].CurrentStep++;
                            _finalizeQuestCompletionTasks.Add(new List<QuestLookup.ActorState>(questStep.CompleteActorStateChange));
                        }
                    }

                    if (_activeQuests[i].CurrentStep >= _activeQuests[i].QuestChain.QuestSteps.Count)
                    {
                        questChainsComplete.Add(i);
                        _completedQuestChains.Add(_activeQuests[i].QuestChain.QuestId);
                    }
                    else
                    {
                        questStep = _activeQuests[i].QuestChain.QuestSteps[_activeQuests[i].CurrentStep];
                        _finalizeNextQuestStartTasks.Add(new List<QuestLookup.ActorState>(questStep.StartActorStateChange));
                    }
                }

                for (int i = questChainsComplete.Count - 1; i >= 0; i--)
                {
                    _activeQuests.RemoveAt(questChainsComplete[i]);
                }
            }
        }

        /// <summary>
        /// Used to update quest system after an event system has finished if a dialog in the event triggers a quest event as triggering the quest complete during an event can cause some issues
        /// </summary>
        public void ValidateCompletedQuestsAfterEvent()
        {
            IterateQuestActorChangeEvent(ref _finalizeQuestCompletionTasks);
            IterateQuestActorChangeEvent(ref _finalizeNextQuestStartTasks);

        }

        public void IterateQuestActorChangeEvent(ref List<List<QuestLookup.ActorState>> pQuestActorChangeEvents)
        {
            int appendedActorChangeCount = pQuestActorChangeEvents.Count;
            for (int i = 0; i < appendedActorChangeCount; i++)
            {
                int completeActorStateChangeCount = pQuestActorChangeEvents[i].Count;
                for (int j = 0; j < completeActorStateChangeCount; j++)
                {
                    if (_questWorldObjects.ContainsKey(pQuestActorChangeEvents[i][j].Id))
                    {
                        _questWorldObjects[pQuestActorChangeEvents[i][j].Id].ChangeInWorldState(pQuestActorChangeEvents[i][j].State);
                    }
                }
                pQuestActorChangeEvents[i].Clear();
            }

            pQuestActorChangeEvents.Clear();
        }

        /// <summary>
        /// Runs through all the completed quests on the current loaded scene and updates the states of the QuestWorldObjects
        /// </summary>
        public void InitializeQuestsOnSceneLoad()
        {
            int completedQuestListCount = _completedQuestChains.Count;
            QuestChain questChain = null;
            int questChainStepCount = 0;
            int questStepEventCount = 0;

            // Iterate over all completed quests
            for (int i = 0; i < completedQuestListCount; i++)
            {
                questChain = _questLookup.GetQuestChainWithUniqueId(_completedQuestChains[i]);
                if (questChain != null)
                {
                    questChainStepCount = questChain.QuestSteps.Count;

                    if (questChainStepCount > 0)
                    {
                        for (int j = 0; j < questChainStepCount; j++)
                        {
                            List<QuestLookup.ActorState> startQuestStepEvents = questChain.QuestSteps[j].StartActorStateChange;
                            List<QuestLookup.ActorState> endQuestStepEvents = questChain.QuestSteps[j].CompleteActorStateChange;

                            questStepEventCount = startQuestStepEvents.Count;
                            for (int k = 0; k < questStepEventCount; k++)
                            {
                                if (_questWorldObjects.ContainsKey(startQuestStepEvents[k].Id))
                                {
                                    _questWorldObjects[startQuestStepEvents[k].Id].ChangeInWorldState(startQuestStepEvents[k].State);
                                }
                            }

                            questStepEventCount = endQuestStepEvents.Count;
                            for (int k = 0; k < questStepEventCount; k++)
                            {
                                if (_questWorldObjects.ContainsKey(endQuestStepEvents[k].Id))
                                {
                                    _questWorldObjects[endQuestStepEvents[k].Id].ChangeInWorldState(endQuestStepEvents[k].State);
                                }
                            }
                        }
                        questChainStepCount = 0;
                    }
                }
            }

            //Iterate over active quests
            int activeQuestsCount = _activeQuests.Count;
            for (int i = 0; i < activeQuestsCount; i++)
            {
                questChain = _questLookup.GetQuestChainWithUniqueId(_activeQuests[i].QuestChain.QuestId);
                if (questChain != null)
                {
                    questChainStepCount = questChain.QuestSteps.Count;

                    //If only on the first step only call the start events of the step
                    if (_activeQuests[i].CurrentStep == 0)
                    {
                        if (questChainStepCount > 0)
                        {
                            List<QuestLookup.ActorState> startQuestStepEvents = questChain.QuestSteps[0].StartActorStateChange;
                            questStepEventCount = startQuestStepEvents.Count;
                            for (int k = 0; k < questStepEventCount; k++)
                            {
                                if (_questWorldObjects.ContainsKey(startQuestStepEvents[k].Id))
                                {
                                    _questWorldObjects[startQuestStepEvents[k].Id].ChangeInWorldState(startQuestStepEvents[k].State);
                                }
                            }
                        }
                    }
                    //Iterate through all the completed steps and call both start/finish states, then finally call the current quest start states
                    else
                    {
                        if (questChainStepCount > 0)
                        {
                            for (int j = 0; j < _activeQuests[i].CurrentStep; j++)
                            {
                                List<QuestLookup.ActorState> startQuestStepEvents = questChain.QuestSteps[j].StartActorStateChange;
                                List<QuestLookup.ActorState> endQuestStepEvents = questChain.QuestSteps[j].CompleteActorStateChange;

                                questStepEventCount = startQuestStepEvents.Count;
                                for (int k = 0; k < questStepEventCount; k++)
                                {
                                    if (_questWorldObjects.ContainsKey(startQuestStepEvents[k].Id))
                                    {
                                        _questWorldObjects[startQuestStepEvents[k].Id].ChangeInWorldState(startQuestStepEvents[k].State);
                                    }
                                }

                                questStepEventCount = endQuestStepEvents.Count;
                                for (int k = 0; k < questStepEventCount; k++)
                                {
                                    if (_questWorldObjects.ContainsKey(endQuestStepEvents[k].Id))
                                    {
                                        _questWorldObjects[endQuestStepEvents[k].Id].ChangeInWorldState(endQuestStepEvents[k].State);
                                    }
                                }
                            }

                            List<QuestLookup.ActorState> finalStartQuestStepEvents = questChain.QuestSteps[_activeQuests[i].CurrentStep].StartActorStateChange;
                            questStepEventCount = finalStartQuestStepEvents.Count;
                            for (int k = 0; k < questStepEventCount; k++)
                            {
                                if (_questWorldObjects.ContainsKey(finalStartQuestStepEvents[k].Id))
                                {
                                    _questWorldObjects[finalStartQuestStepEvents[k].Id].ChangeInWorldState(finalStartQuestStepEvents[k].State);
                                }
                            }
                        }
                    }
                    questChainStepCount = 0;
                }
            }
        }

        /// <summary>
        /// Filles local data with complted quest list
        /// </summary>
        public void PopulateCompletedQuestsFromSaveFile()
        {

        }

        /// <summary>
        /// Used to override the completed quest list
        /// </summary>
        public void DebugAddQuestsToCompletedList(string[] pCompletedQuestIds)
        {
            _completedQuestChains.Clear();
            _completedQuestChains = new List<string>(pCompletedQuestIds);
        }

        /// <summary>
        /// Adds quest to active quest chains
        /// </summary>
        /// <param name="pQuestChainId"></param>
        /// <param name="pQuestChainStep"></param>
        public void DebugAddActiveQuest(string pQuestChainId, int pQuestChainStep)
        {

            if (!QuestIsCompleteOrActive(pQuestChainId))
            {
                QuestChain quest = _questLookup.GetQuestChainWithUniqueId(pQuestChainId);

                if (quest == null)
                {
#if UNITY_EDITOR
                    Debug.LogError($"Quest: {pQuestChainId}, does not exist in quest database!");
#endif
                    return;
                }

                _activeQuests.Add(new QuestChainProgress(quest, pQuestChainStep));
            }
        }

        private bool QuestIsCompleteOrActive(string pQuestChainId)
        {
            return IsQuestChainInCompletedQuestList(pQuestChainId) || IsQuestChainInActiveQuestList(pQuestChainId);
        }

        private bool IsQuestChainInCompletedQuestList(string pQuestChainId)
        {
            bool questAlreadyCompleted = false;
            int completedQuestChainCount = _completedQuestChains.Count;
            for (int i = 0; i < completedQuestChainCount; i++)
            {
                if (_completedQuestChains[i] == pQuestChainId)
                {
                    questAlreadyCompleted = true;
#if UNITY_EDITOR
                    Debug.LogError($"Quest: {pQuestChainId}, is already completed quest list");
#endif
                    break;
                }
            }

            return questAlreadyCompleted;
        }

        private bool IsQuestChainInActiveQuestList(string pQuestChainId)
        {
            bool questAlreadyInQuestList = false;

            for (int i = 0; i < _activeQuests.Count; i++)
            {
                if (_activeQuests[i].QuestChain.QuestId == pQuestChainId)
                {
                    questAlreadyInQuestList = true;
#if UNITY_EDITOR
                    Debug.LogError($"Quest: {pQuestChainId}, already in active quest list");
#endif
                    break;
                }
            }

            return questAlreadyInQuestList;
        }

#if UNITY_EDITOR
        private void OnGUI()
        {
            if (_showActiveQuestList)
            {
                float yOffset = _onGuiYOffset;

                GUI.Label(new Rect(10.0f, yOffset, 1000.0f, 20.0f), "Active Quests:");
                for (int i = 0; i < _activeQuests.Count; i++)
                {
                    yOffset += 20.0f;
                    GUI.Label(new Rect(10.0f, yOffset, 1000.0f, 20.0f), $"Quest Chain: {_activeQuests[i].QuestChain.QuestId}");
                    yOffset += 20.0f;
                    GUI.Label(new Rect(10.0f, yOffset, 1000.0f, 20.0f), $"Current Step: {_activeQuests[i].CurrentStep}) {_activeQuests[i].QuestChain.QuestSteps[_activeQuests[i].CurrentStep].name}");
                    yOffset += 20.0f;
                    GUI.Label(new Rect(10.0f, yOffset, 1000.0f, 20.0f), $"Target: {_activeQuests[i].QuestChain.QuestSteps[_activeQuests[i].CurrentStep].InWorldActorId}");
                    yOffset += 20.0f;
                    GUI.Label(new Rect(10.0f, yOffset, 1000.0f, 20.0f), $"Type: {_activeQuests[i].QuestChain.QuestSteps[_activeQuests[i].CurrentStep].Type}");

                }
                yOffset += 20.0f;
                GUI.Label(new Rect(10.0f, yOffset, 1000.0f, 20.0f), "Completed Quests:");
                for (int i = 0; i < _completedQuestChains.Count; i++)
                {
                    yOffset += 20.0f;
                    GUI.Label(new Rect(10.0f, yOffset, 1000.0f, 20.0f), $"{_completedQuestChains[i]}");
                }
            }
        }
#endif
    }
}
