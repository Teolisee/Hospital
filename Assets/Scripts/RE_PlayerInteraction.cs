using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

// -----------------------------------------------------------------------------
// SCRIPT: RE_PlayerInteraction
// METÁFORA: "El Radar y la Mano del Jugador"
// Detecta NPCs y objetos interactuables alrededor del jugador o al frente de la cámara,
// muestra avisos flotantes en pantalla y ejecuta la acción al presionar la tecla 'E'.
// -----------------------------------------------------------------------------
public class RE_PlayerInteraction : MonoBehaviour 
{
    [Header("Ajustes de Detección")]
    [Tooltip("Distancia máxima de interacción en metros.")]
    public float sphereRadius = 3.5f;

    [Tooltip("Capas a considerar para interactuar (dejar en Everything o Default).")]
    public LayerMask interactableMask = ~0; // Todo por defecto para evitar ignorar NPCs

    [Header("Referencias Visuales (Opcional)")]
    [Tooltip("Objeto o texto de UI en pantalla para mostrar el aviso 'Presiona E' (se auto-detecta si está vacío).")]
    [SerializeField] private GameObject promptUIObject;
    [SerializeField] private TextMeshProUGUI promptUIText;

    [Header("Ajustes de UI Flotante")]
    [Tooltip("Muestra un indicador en pantalla cuando hay un interactuable cerca.")]
    public bool mostrarIndicadorEnPantalla = true;

    private IInteractable currentInteractable;
    private GameObject currentInteractableGameObject;
    private GameObject lastPromptCanvas;
    private Transform cameraTransform;

    private void Start() 
    {
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        // Buscar texto de prompt en la escena si no se asignó en el Inspector
        DetectPromptUI();
    }

    private void DetectPromptUI()
    {
        if (promptUIObject == null)
        {
            // Intentamos buscar un objeto llamado "Prompt", "InteractPrompt" o "AvisoE"
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.hideFlags == HideFlags.None && 
                   (go.name.ToLower().Contains("prompt") || go.name.ToLower().Contains("interactprompt") || go.name.ToLower().Contains("avisoe")))
                {
                    promptUIObject = go;
                    promptUIText = go.GetComponent<TextMeshProUGUI>() ?? go.GetComponentInChildren<TextMeshProUGUI>();
                    break;
                }
            }
        }

        if (promptUIObject != null && promptUIText == null)
        {
            promptUIText = promptUIObject.GetComponent<TextMeshProUGUI>() ?? promptUIObject.GetComponentInChildren<TextMeshProUGUI>();
        }
    }

    private void Update()
    {
        // 1. Escanear el entorno buscando interactuables
        FindInteractable();

        // 2. Gestionar avisos visuales (Canvas flotante del NPC y/o HUD)
        UpdateVisualPrompts();

        // 3. Detectar si el jugador presiona la tecla E o botón de interacción
        if (WasInteractPressed())
        {
            if (currentInteractable != null)
            {
                Debug.Log($"[RE_PlayerInteraction] ✓ Interactuando con: {currentInteractable.GetInteractPrompt()}");
                currentInteractable.Interact();
            }
            else
            {
                Debug.Log("[RE_PlayerInteraction] Presionaste E pero no hay ningún interactuable a menos de " + sphereRadius + "m.");
            }
        }
    }

    /// <summary>
    /// Detecta entradas tanto del nuevo Input System como del Input tradicional de Unity.
    /// </summary>
    private bool WasInteractPressed()
    {
        // 1. Teclado en Input System moderno
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) return true;

        // 2. Gamepad en Input System moderno (Botón A / Cruz / X)
        if (Gamepad.current != null && (Gamepad.current.buttonSouth.wasPressedThisFrame || Gamepad.current.buttonWest.wasPressedThisFrame)) return true;

        // 3. Entrada clásica de Unity (Legacy Input Manager)
        try
        {
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetButtonDown("Submit"))
            {
                return true;
            }
        }
        catch { }

        return false;
    }

    /// <summary>
    /// Escanea objetos cercanos usando una esfera centrada en el torso del jugador y un raycast frontal.
    /// </summary>
    private void FindInteractable()
    {
        Vector3 playerCenter = transform.position + Vector3.up * 1.0f; // Centro del personaje (a la altura del pecho)

        IInteractable bestInteractable = null;
        GameObject bestGameObject = null;
        float bestDistance = Mathf.Infinity;

        // 1. Detección por esfera alrededor del jugador
        Collider[] colliders = Physics.OverlapSphere(playerCenter, sphereRadius, interactableMask);
        if (colliders == null || colliders.Length == 0)
        {
            colliders = Physics.OverlapSphere(playerCenter, sphereRadius);
        }

        if (colliders != null)
        {
            foreach (Collider col in colliders)
            {
                if (col.gameObject == gameObject) continue; // Ignorar al propio jugador

                IInteractable interactable = col.GetComponent<IInteractable>() ?? 
                                             col.GetComponentInParent<IInteractable>() ?? 
                                             col.GetComponentInChildren<IInteractable>();

                if (interactable == null && col.attachedRigidbody != null)
                {
                    interactable = col.attachedRigidbody.GetComponent<IInteractable>();
                }

                if (interactable != null)
                {
                    // Medir distancia al punto más cercano del collider o transform
                    Vector3 targetPos = col.bounds.center;
                    float dist = Vector3.Distance(playerCenter, targetPos);

                    // Si está dentro de la distancia máxima y es el más cercano
                    if (dist < bestDistance && dist <= sphereRadius)
                    {
                        bestDistance = dist;
                        bestInteractable = interactable;
                        bestGameObject = col.gameObject;
                    }
                }
            }
        }

        // 2. Fallback de seguridad: Si los colliders fallaron, buscar por proximidad a los scripts RE_NPCInteraction
        if (bestInteractable == null)
        {
            foreach (RE_NPCInteraction npc in FindObjectsByType<RE_NPCInteraction>(FindObjectsSortMode.None))
            {
                if (npc == null || !npc.enabled || !npc.gameObject.activeInHierarchy) continue;
                
                // Medir distancia a la posición del NPC o de sus hijos
                float dist = Vector3.Distance(playerCenter, npc.transform.position);
                
                // Si tiene modelo hijo, medir también la distancia al modelo
                foreach (Transform child in npc.transform)
                {
                    float childDist = Vector3.Distance(playerCenter, child.position);
                    if (childDist < dist) dist = childDist;
                }

                if (dist < bestDistance && dist <= sphereRadius)
                {
                    bestDistance = dist;
                    bestInteractable = npc;
                    bestGameObject = npc.gameObject;
                }
            }
        }

        // 3. Actualizar el interactuable actual
        if (bestInteractable != currentInteractable)
        {
            currentInteractable = bestInteractable;
            currentInteractableGameObject = bestGameObject;
        }
    }

    /// <summary>
    /// Activa/desactiva los avisos visuales (Canvas de visualización sobre el NPC o HUD de pantalla).
    /// </summary>
    private void UpdateVisualPrompts()
    {
        // Manejo de Canvas de visualización 3D en el NPC
        if (currentInteractableGameObject != null)
        {
            Transform rootTransform = currentInteractableGameObject.transform;
            if (rootTransform.GetComponent<RE_NPCInteraction>() == null && rootTransform.GetComponent<IInteractable>() == null)
            {
                RE_NPCInteraction parentNPC = rootTransform.GetComponentInParent<RE_NPCInteraction>();
                if (parentNPC != null) rootTransform = parentNPC.transform;
            }

            // Buscar si el NPC tiene un Canvas de visualización flotante
            Transform visualCanvasTransform = null;
            foreach (Transform child in rootTransform.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.ToLower().Contains("visualizacion") || child.name.ToLower().Contains("visualización"))
                {
                    visualCanvasTransform = child;
                    break;
                }
            }

            if (visualCanvasTransform != null)
            {
                if (lastPromptCanvas != null && lastPromptCanvas != visualCanvasTransform.gameObject)
                {
                    lastPromptCanvas.SetActive(false);
                }
                visualCanvasTransform.gameObject.SetActive(true);
                lastPromptCanvas = visualCanvasTransform.gameObject;
            }
        }
        else
        {
            if (lastPromptCanvas != null)
            {
                lastPromptCanvas.SetActive(false);
                lastPromptCanvas = null;
            }
        }

        // Manejo del texto de prompt en UI 2D (HUD)
        if (promptUIObject != null)
        {
            if (currentInteractable != null)
            {
                promptUIObject.SetActive(true);
                if (promptUIText != null)
                {
                    promptUIText.text = currentInteractable.GetInteractPrompt();
                }
            }
            else
            {
                promptUIObject.SetActive(false);
            }
        }
    }

    public IInteractable GetCurrentInteractable()
    {
        return currentInteractable;
    }

    // Dibujar aviso en pantalla mediante OnGUI si no hay HUD configurado
    private void OnGUI()
    {
        if (!mostrarIndicadorEnPantalla || promptUIObject != null) return;

        if (currentInteractable != null)
        {
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.fontSize = 18;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.white;
            style.alignment = TextAnchor.MiddleCenter;

            string mensaje = currentInteractable.GetInteractPrompt();
            if (string.IsNullOrEmpty(mensaje)) mensaje = "Presiona [E] para interactuar";

            float ancho = Mathf.Max(320, mensaje.Length * 12);
            float alto = 45;
            float x = (Screen.width - ancho) / 2f;
            float y = Screen.height - 110;

            GUI.Box(new Rect(x, y, ancho, alto), $"[E] {mensaje}", style);
        }
    }

    // Dibuja la esfera amarilla en el editor de Unity
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 playerCenter = transform.position + Vector3.up * 1.0f;
        Gizmos.DrawWireSphere(playerCenter, sphereRadius);
    }
}
