using UnityEngine; // Librería base de Unity
using UnityEngine.SceneManagement; // Permite manejar la carga de niveles y escenas
using System.Collections; // Permite el uso de Corrutinas (funciones que se ejecutan a lo largo del tiempo)

/// <summary>
/// Gestiona la transición suave entre niveles mediante un fundido (fade in/out) a negro
/// y carga los niveles en segundo plano para evitar tirones en el juego.
/// </summary>
public class RE_LevelTransitionManager : MonoBehaviour // Clase principal para transiciones entre escenas
{
    // Instancia única (Singleton) para que cualquier script pueda llamarlo sin buscarlo
    public static RE_LevelTransitionManager Instance { get; private set; }

    [Header("Configuración de Transición")] // Categoría en el Inspector
    [Tooltip("El CanvasGroup que contiene la imagen oscura para cubrir la pantalla.")] // Guía de uso
    [SerializeField] private CanvasGroup fadeCanvasGroup; // Controla la transparencia de la pantalla negra

    [Tooltip("Duración en segundos del fundido de entrada y salida.")] // Guía de uso
    [SerializeField] private float fadeDuration = 0.8f; // Cuánto tarda en ponerse negra la pantalla

    private bool isTransitioning = false; // Bandera para evitar que se inicien dos transiciones al mismo tiempo

    private void Awake() // Se ejecuta inmediatamente cuando el objeto se crea
    {
        Instance = this;

        // Asegurarnos de que al iniciar la pantalla sea totalmente transparente
        if (fadeCanvasGroup != null) 
        {
            fadeCanvasGroup.alpha = 0f; // 0 de opacidad = transparente
            fadeCanvasGroup.blocksRaycasts = false; // Desactivar clics en la pantalla negra
        }
    }

    /// <summary>
    /// Inicia la transición a una nueva escena.
    /// </summary>
    public void TransitionToScene(string sceneName) // Función pública para ordenar un cambio de mapa
    {
        if (isTransitioning) return; // Si ya estamos cambiando de nivel, ignoramos la orden
        StartCoroutine(TransitionCoroutine(sceneName)); // Iniciamos el proceso que toma tiempo
    }

    /// <summary>
    /// Corrutina que realiza el fundido a negro, carga la escena y luego aclara la pantalla.
    /// </summary>
    private IEnumerator TransitionCoroutine(string sceneName) // Función especial que puede "pausarse" entre fotogramas
    {
        isTransitioning = true; // Marcamos que la transición ha comenzado

        // 1. Bloquear interacción del usuario (para que no toque botones mientras carga)
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = true;
        }

        // 2. Fundido a negro (Fade In)
        float timer = 0f; // Cronómetro local
        while (timer < fadeDuration) // Mientras no hayamos superado el tiempo de transición
        {
            timer += Time.unscaledDeltaTime; // Aumentamos el cronómetro incluso si el juego está en pausa
            if (fadeCanvasGroup != null)
            {
                // Interpolar (Lerp) de 0 a 1 suavemente según el progreso del cronómetro
                fadeCanvasGroup.alpha = Mathf.Lerp(0f, 1f, timer / fadeDuration); 
            }
            yield return null; // Esperamos al siguiente fotograma antes de repetir el ciclo
        }
        if (fadeCanvasGroup != null) fadeCanvasGroup.alpha = 1f; // Aseguramos que termine completamente negro

        // 3. Actualizar en el sistema de progreso en qué escena estamos a punto de entrar
        if (RE_GameProgress.Instance != null)
        {
            RE_GameProgress.Instance.progressData.currentSceneName = sceneName; // Guardamos el nombre del mapa
            RE_GameProgress.Instance.SaveProgress(); // Lo guardamos en el disco duro
        }

        // 4. Iniciar la carga del nuevo mapa en segundo plano (asíncrona)
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        
        if (asyncLoad == null) // Si ocurrió un error grave y no existe la escena...
        {
            Debug.LogError($"[RE_LevelTransitionManager] No se pudo cargar la escena '{sceneName}'. Verifica el Build Settings.");
            
            // Abortar y volver a hacer la pantalla transparente
            timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.unscaledDeltaTime;
                if (fadeCanvasGroup != null)
                {
                    fadeCanvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration); // Aclarar pantalla
                }
                yield return null;
            }
            if (fadeCanvasGroup != null)
            {
                fadeCanvasGroup.alpha = 0f;
                fadeCanvasGroup.blocksRaycasts = false; // Devolver el control al jugador
            }
            isTransitioning = false; // Marcamos que ya no estamos transicionando
            yield break; // Cortamos la ejecución de la función aquí
        }

        // Esperar pacientemente hasta que Unity termine de cargar toda la escena 3D nueva
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // Pequeña espera de seguridad de 0.1 segundos para evitar congelamientos en el primer fotograma
        yield return new WaitForSecondsRealtime(0.1f);

        // 5. Fundido a transparente (Fade Out) para revelar el nuevo nivel
        timer = 0f; // Reiniciamos cronómetro
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            if (fadeCanvasGroup != null)
            {
                fadeCanvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration); // Interpolar de negro a transparente
            }
            yield return null; // Esperar al siguiente fotograma
        }
        if (fadeCanvasGroup != null) fadeCanvasGroup.alpha = 0f; // Asegurar total transparencia

        // 6. Devolver el control de la interfaz al jugador
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = false;
        }

        isTransitioning = false; // Finalizamos el estado de transición y permitimos jugar
    }
}
