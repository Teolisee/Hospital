using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Gestiona la transición suave entre niveles mediante un fundido (fade in/out) a negro
/// y carga los niveles en segundo plano para evitar tirones en el juego.
/// Se mantiene persistente entre escenas (DontDestroyOnLoad) para garantizar que el fundido termine.
/// </summary>
public class RE_LevelTransitionManager : MonoBehaviour
{
    public static RE_LevelTransitionManager Instance { get; private set; }

    [Header("Configuración de Transición")]
    [Tooltip("El CanvasGroup que contiene la imagen oscura para cubrir la pantalla.")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;

    [Tooltip("Duración en segundos del fundido de entrada y salida.")]
    [SerializeField] private float fadeDuration = 0.8f;

    private bool isTransitioning = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Desacoplar de cualquier padre y marcar como persistente entre escenas
        if (transform.parent != null)
        {
            transform.SetParent(null);
        }
        DontDestroyOnLoad(gameObject);

        // Asegurarnos de que el CanvasGroup esté configurado correctamente al inicio
        EnsureCanvasGroupExists();
        ResetFadeAlpha();
    }

    private void EnsureCanvasGroupExists()
    {
        if (fadeCanvasGroup == null)
        {
            fadeCanvasGroup = GetComponentInChildren<CanvasGroup>();
        }

        if (fadeCanvasGroup == null)
        {
            // Si no existe, crear un Canvas persistente con pantalla negra automáticamente
            GameObject canvasGo = new GameObject("TransitionCanvas");
            canvasGo.transform.SetParent(transform);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999; // Por encima de todo

            canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            GameObject imageGo = new GameObject("FadeImage");
            imageGo.transform.SetParent(canvasGo.transform, false);

            UnityEngine.UI.Image img = imageGo.AddComponent<UnityEngine.UI.Image>();
            img.color = Color.black;

            RectTransform rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            fadeCanvasGroup = canvasGo.AddComponent<CanvasGroup>();
        }
    }

    private void ResetFadeAlpha()
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// Inicia la transición suave a una nueva escena.
    /// Resuelve discrepancias de nombres y valida que la escena exista en Build Settings.
    /// </summary>
    public void TransitionToScene(string sceneName)
    {
        if (isTransitioning) return;

        string targetScene = ResolveValidSceneName(sceneName);
        StartCoroutine(TransitionCoroutine(targetScene));
    }

    /// <summary>
    /// Valida y normaliza nombres de escena frente a Build Settings.
    /// </summary>
    public static string ResolveValidSceneName(string requestedScene)
    {
        if (string.IsNullOrEmpty(requestedScene)) return "MainMenu";

        // Mapeos conocidos de nombres antiguos o alternativas en el proyecto
        if (requestedScene == "Hospital_Level2" || requestedScene == "HospitalLevel2") return "Nivel 2";
        if (requestedScene == "MenuPrincipal" || requestedScene == "Menu") return "MainMenu";
        if (requestedScene == "Hospital_Lobby" || requestedScene == "Lobby") return "Nivel 1";

        // Si la escena solicitada se puede cargar según Unity, la usamos directamente
        if (Application.CanStreamedLevelBeLoaded(requestedScene))
        {
            return requestedScene;
        }

        // Si no se encuentra, buscar por coincidencia sin espacios
        string trimmed = requestedScene.Replace(" ", "");
        if (Application.CanStreamedLevelBeLoaded(trimmed)) return trimmed;

        Debug.LogWarning($"[RE_LevelTransitionManager] Advertencia: La escena '{requestedScene}' no se encuentra en Build Settings.");
        return requestedScene;
    }

    private IEnumerator TransitionCoroutine(string sceneName)
    {
        isTransitioning = true;

        // Asegurar que el tiempo del juego esté activo para la transición
        Time.timeScale = 1f;

        EnsureCanvasGroupExists();

        // 1. Bloquear interacción del usuario
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = true;
        }

        // 2. Fundido a negro (Fade In) usando tiempo no escalado
        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            if (fadeCanvasGroup != null)
            {
                fadeCanvasGroup.alpha = Mathf.Clamp01(timer / fadeDuration);
            }
            yield return null;
        }
        if (fadeCanvasGroup != null) fadeCanvasGroup.alpha = 1f;

        // 3. Actualizar la escena en el progreso del juego
        if (RE_GameProgress.Instance != null && RE_GameProgress.Instance.progressData != null)
        {
            RE_GameProgress.Instance.progressData.currentSceneName = sceneName;
            RE_GameProgress.Instance.SaveProgress();
        }

        // 4. Iniciar la carga del nuevo mapa en segundo plano
        AsyncOperation asyncLoad = null;
        try
        {
            asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RE_LevelTransitionManager] Excepción al cargar '{sceneName}': {ex.Message}");
        }

        if (asyncLoad == null)
        {
            Debug.LogError($"[RE_LevelTransitionManager] Error: No se pudo iniciar la carga de '{sceneName}'. Abortando.");

            // Restaurar la pantalla
            timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.unscaledDeltaTime;
                if (fadeCanvasGroup != null)
                {
                    fadeCanvasGroup.alpha = Mathf.Clamp01(1f - (timer / fadeDuration));
                }
                yield return null;
            }
            ResetFadeAlpha();
            isTransitioning = false;
            yield break;
        }

        // Esperar a que la escena termine de cargar
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // Breve pausa para estabilizar la inicialización de la escena
        yield return new WaitForSecondsRealtime(0.1f);

        // 5. Fundido a transparente (Fade Out) para revelar el nuevo nivel
        timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            if (fadeCanvasGroup != null)
            {
                fadeCanvasGroup.alpha = Mathf.Clamp01(1f - (timer / fadeDuration));
            }
            yield return null;
        }
        ResetFadeAlpha();

        isTransitioning = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
