using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine.SceneManagement;

namespace TVEngine.Cheats
{
    public class TeleporterWindow : EditorWindow
    {
        private static CheatTeleporterSceneReferenceData _sceneMappingData;
        private static CheatTeleporterData _CTData;
        private SerializedObject _so;
        private static TeleporterWindow _teleporterWindow;

        private ReorderableList LIST_TeleporterData;

        private Vector2 _scrollPos;

        [MenuItem("Cheats/Teleporter")]
        static void ShowWindow()
        {
            _teleporterWindow = GetWindow<TeleporterWindow>("Teleporter Cheat");
        }
        

        private void OnEnable()
        {   
            LoadData(SceneManager.GetSceneAt(0).name);
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += OnSceneOpened;
            AsyncSceneLoaderController.OnAsyncLoadSceneUpdate += OnAsyncSceneLoaded;
        }

        private void OnDisable()
        {
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened -= OnSceneOpened;
            AsyncSceneLoaderController.OnAsyncLoadSceneUpdate -= OnAsyncSceneLoaded;
        }

        public void OnAsyncSceneLoaded()
        {
            LoadData(SceneManager.GetSceneAt(0).name);
        }

        private void OnSceneOpened(Scene pScene, UnityEditor.SceneManagement.OpenSceneMode pOpen)
        {
            if (!EditorApplication.isPlaying)
            {
                LoadData(pScene.name);
            }
        }

        private void LoadData(string pAssetName = "")
        {
            // if not overriden will fallback to this file
            string dataToLoad = "Cheats/Teleporter/02_AUT_TeleporterData.asset";
            if (pAssetName != "")
            {
                if (_sceneMappingData == null)
                {
                    _sceneMappingData = EditorGUIUtility.Load("Cheats/Teleporter/Debug_TeleportSceneReferences.asset") as CheatTeleporterSceneReferenceData;
                }

                for (int i = 0; i < _sceneMappingData.TelepoterSceneReferenceData.Count; i++)
                {
                    if (_sceneMappingData.TelepoterSceneReferenceData[i].SceneName == pAssetName)
                    {
                        dataToLoad = $"Cheats/Teleporter/{_sceneMappingData.TelepoterSceneReferenceData[i].TeleporterReference.name}.asset";
                        break;
                    }
                }

                UpdateWindowWithData(dataToLoad);
            }
            else
            {
                if (_sceneMappingData == null)
                {
                    _sceneMappingData = EditorGUIUtility.Load("Cheats/Teleporter/Debug_TeleportSceneReferences.asset") as CheatTeleporterSceneReferenceData;
                    UpdateWindowWithData(dataToLoad);
                }
            }


        }

        private void UpdateWindowWithData(string pDataToLoad)
        {
            _CTData = EditorGUIUtility.Load(pDataToLoad) as CheatTeleporterData;
            _so = new SerializedObject(_CTData);
            LIST_TeleporterData = new ReorderableList(_so, _so.FindProperty("TelepoterData"), true, true, true, true);
            HandleReordableList(LIST_TeleporterData, "TeleporterList");
        }

        private void OnGUI()
        {
            DrawWindow();
        }

        private void DrawWindow()
        {
            BeginWindows();
            EditorGUI.BeginChangeCheck();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos,
                alwaysShowHorizontal: true,
                alwaysShowVertical: true,
                GUILayout.Width(position.width),
                GUILayout.Height(position.height));

            _so.Update();
            LIST_TeleporterData.DoLayoutList();

            EditorGUILayout.EndScrollView();

            if (EditorGUI.EndChangeCheck())
            {

                EditorUtility.SetDirty(_CTData);

            }
            _so.ApplyModifiedProperties();

            EndWindows();

        }

        void HandleReordableList(ReorderableList list, string targetName)
        {
            list.drawHeaderCallback = (Rect rect) =>
            {
                Rect R_1 = new Rect(rect.x + 12, rect.y, (rect.width) / 3 + 25, EditorGUIUtility.singleLineHeight);
                EditorGUI.LabelField(R_1, "Spawn Point Description");

                Rect R_2 = new Rect(rect.x + (rect.width) / 3 + 65, rect.y, (rect.width) / 3, EditorGUIUtility.singleLineHeight);
                EditorGUI.LabelField(R_2, "Position");
            };

            list.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                var element = _CTData.TelepoterData[index];
                rect.y += 2;

                Rect R_1 = new Rect(rect.x, rect.y, (rect.width) / 3 + 55, EditorGUIUtility.singleLineHeight);
                Rect R_2 = new Rect(rect.x + (rect.width) / 3 + 61, rect.y, (rect.width) / 2, EditorGUIUtility.singleLineHeight);

                element.PointDescription = EditorGUI.TextArea(R_1, element.PointDescription);
                element.SpawnPoint = EditorGUI.ObjectField(R_2, element.SpawnPoint, typeof(SO_TeleportPoint), true) as SO_TeleportPoint;

            };

            list.onSelectCallback = (ReorderableList roList) =>
            {
                var element = _CTData.TelepoterData[list.index];

                if (EditorApplication.isPlaying && element.SpawnPoint != null)
                {
                    GameController.Instance?.CheatTeleportCharacter( element.SpawnPoint.TeleportPoint);
                }
            };
        }
    }
}