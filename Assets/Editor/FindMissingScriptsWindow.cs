using UnityEngine;
using UnityEditor;

public class FindMissingScriptsWindow : EditorWindow
{
    [MenuItem("Tools/Buscar Scripts Faltantes")]
    public static void ShowWindow()
    {
        GetWindow<FindMissingScriptsWindow>("Scripts Faltantes");
    }

    private void OnGUI()
    {
        GUILayout.Label("Buscador de Componentes Faltantes (Missing Scripts)", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (GUILayout.Button("Buscar en la Escena Activa", GUILayout.Height(30)))
        {
            FindInActiveScene();
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Restablecer Progreso Guardado (PlayerPrefs)", GUILayout.Height(30)))
        {
            ResetSavedProgress();
        }
    }

    private static void FindInActiveScene()
    {
        Transform[] allTransforms = GameObject.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int missingCount = 0;
        int affectedObjectsCount = 0;

        foreach (Transform t in allTransforms)
        {
            GameObject go = t.gameObject;
            Component[] components = go.GetComponents<Component>();
            bool hasMissingInObject = false;

            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    missingCount++;
                    hasMissingInObject = true;
                    Debug.LogWarning($"[Missing Script] El GameObject '{go.name}' tiene un componente faltante (Índice {i}).", go);
                }
            }

            if (hasMissingInObject)
            {
                affectedObjectsCount++;
            }
        }

        Debug.Log($"[Búsqueda Finalizada] Se encontraron {missingCount} componentes faltantes en {affectedObjectsCount} GameObjects.");
    }

    [MenuItem("Tools/Restablecer Progreso Guardado")]
    public static void ResetSavedProgress()
    {
        RE_GameProgress gp = GameObject.FindAnyObjectByType<RE_GameProgress>();
        string keyToDelete = "HospitalGameProgress";

        if (gp != null)
        {
            SerializedObject so = new SerializedObject(gp);
            SerializedProperty prop = so.FindProperty("saveKey");
            if (prop != null && !string.IsNullOrEmpty(prop.stringValue))
            {
                keyToDelete = prop.stringValue;
            }
        }

        if (PlayerPrefs.HasKey(keyToDelete))
        {
            PlayerPrefs.DeleteKey(keyToDelete);
            Debug.Log($"[Editor] Se eliminó la clave de guardado activa: '{keyToDelete}'");
        }

        PlayerPrefs.DeleteKey("HospitalGameProgress");
        PlayerPrefs.DeleteKey("HospitalGam");
        PlayerPrefs.Save();

        Debug.Log("[Editor] ¡Restablecimiento completado! Todos los datos de progreso guardados han sido borrados de PlayerPrefs.");
    }
}
