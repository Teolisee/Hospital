using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controla los menús de fin de juego (Derrota o Victoria).
/// Restaura el cursor y la escala de tiempo, reinicia el nivel correspondiente,
/// y auto-vincula los botones (Reiniciar, Salir, Menú) incluso si no se enlazaron manualmente.
/// </summary>
public class GameOverMenu : MonoBehaviour
{
    [Header("Configuración de Escenas")]
    [Tooltip("Nombre de la escena por defecto si no se detecta el nivel guardado.")]
    [SerializeField] private string levelSceneName = "Nivel 1";

    [Tooltip("Nombre de la escena del menú principal.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private void Start()
    {
        // Restaurar estado de juego y ratón
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        AutoBindButtons();
    }

    private void AutoBindButtons()
    {
        // Buscar botones tanto en hijos como en toda la escena de UI si no somos el Canvas
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var btn in buttons)
        {
            string n = btn.gameObject.name.ToLower();

            if (n.Contains("reiniciar") || n.Contains("reintentar") || n.Contains("restart") || n.Contains("retry") || n.Contains("jugar"))
            {
                btn.onClick.RemoveListener(RestartLevel);
                btn.onClick.AddListener(RestartLevel);
            }
            else if (n.Contains("menu") || n.Contains("inicio") || n.Contains("principal"))
            {
                btn.onClick.RemoveListener(LoadMainMenu);
                btn.onClick.AddListener(LoadMainMenu);
            }
            else if (n.Contains("salir") || n.Contains("quit") || n.Contains("exit"))
            {
                btn.onClick.RemoveListener(QuitGame);
                btn.onClick.AddListener(QuitGame);
            }
        }
    }

    /// <summary>
    /// Reinicia el nivel en el que se encontraba el jugador, o el nivel por defecto.
    /// </summary>
    public void RestartLevel()
    {
        Time.timeScale = 1f;

        string targetScene = levelSceneName;
        string activeScene = SceneManager.GetActiveScene().name;

        // Si estamos en la pantalla de Victoria, reiniciar vuelve a comenzar desde el Nivel 1
        if (activeScene.ToLower().Contains("victoria"))
        {
            targetScene = "Nivel 1";
            if (RE_GameProgress.Instance != null)
            {
                RE_GameProgress.Instance.ResetProgress();
            }
        }
        else if (RE_GameProgress.Instance != null && RE_GameProgress.Instance.progressData != null)
        {
            RE_GameProgress.Instance.ResetLevelProgressOnly();
            
            int maxHealth = RE_GameProgress.Instance.progressData.playerMaxHealth;
            if (maxHealth <= 0) maxHealth = 100;
            
            RE_GameProgress.Instance.progressData.RE_PlayerHealth = maxHealth;
            RE_GameProgress.Instance.SaveProgress();

            string savedScene = RE_GameProgress.Instance.progressData.currentSceneName;
            if (!string.IsNullOrEmpty(savedScene) && 
                !savedScene.ToLower().Contains("menu") && 
                !savedScene.ToLower().Contains("fin") &&
                !savedScene.ToLower().Contains("juego"))
            {
                targetScene = savedScene;
            }
        }

        if (string.IsNullOrEmpty(targetScene)) targetScene = "Nivel 1";

        if (RE_LevelTransitionManager.Instance != null)
        {
            RE_LevelTransitionManager.Instance.TransitionToScene(targetScene);
        }
        else
        {
            SceneManager.LoadScene(RE_LevelTransitionManager.ResolveValidSceneName(targetScene));
        }
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;

        string targetMenu = !string.IsNullOrEmpty(mainMenuSceneName) && mainMenuSceneName != "MenuPrincipal" 
            ? mainMenuSceneName 
            : "MainMenu";

        if (RE_LevelTransitionManager.Instance != null)
        {
            RE_LevelTransitionManager.Instance.TransitionToScene(targetMenu);
        }
        else
        {
            SceneManager.LoadScene(RE_LevelTransitionManager.ResolveValidSceneName(targetMenu));
        }
    }

    public void QuitGame()
    {
        Debug.Log("[GameOverMenu] Saliendo del juego...");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    /// <summary>
    /// Garantiza que en pantallas de fin de juego (Victoria o Fin del Juego) siempre
    /// exista un controlador para desbloquear el cursor y responder a los clics.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureMenuManagerOnEndScenes()
    {
        string sceneName = SceneManager.GetActiveScene().name.ToLower();
        if (sceneName.Contains("victoria") || sceneName.Contains("fin del juego"))
        {
            if (FindFirstObjectByType<GameOverMenu>() == null)
            {
                GameObject manager = new GameObject("GameOverManager_AutoHelper");
                manager.AddComponent<GameOverMenu>();
            }
        }
    }
}
