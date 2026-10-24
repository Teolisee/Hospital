using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

/// <summary>
/// Gestiona la deteccion y ejecucion de interacciones del jugador (IInteractable).
/// Activa el Canvas 3D de visualizacion propio de cada NPC (sobre su cabeza).
/// No dibuja cuadros debug en OnGUI para no ensuciar la interfaz del juego.
/// </summary>
public class RE_PlayerInteraction : MonoBehaviour 
{
    [Header("Configuracion de Deteccion")]
    [Tooltip("Distancia maxima a la que el jugador puede interactuar con objetos.")]
    public float sphereRadius = 2.5f;

    [Tooltip("Capas fisicas donde buscar interactuables.")]
    public LayerMask interactableMask = ~0;

    [Tooltip("Angulo minimo de vision hacia el interactuable (0 = 90 grados, 0.25 = ~75 grados).")]
    public float minFacingDot = 0.2f;

    [Header("Interfaz de Usuario")]
    [Tooltip("Objeto Canvas o contenedor de la UI que avisa al jugador (opcional).")]
    public GameObject promptUIObject;

    [Tooltip("Componente TextMeshPro donde se muestra el mensaje de interaccion (opcional).")]
    public TextMeshProUGUI promptUIText;

    [Tooltip("Si es true, muestra un indicador en pantalla solo si hay un HUD asignado.")]
    public bool mostrarIndicadorEnPantalla = false;

    private IInteractable currentInteractable;
    private GameObject currentInteractableGameObject;
    private GameObject lastPromptCanvas;

    // Buffer fijo para evitar asignaciones de memoria (GC allocs) cada frame
    private readonly Collider[] hitBuffer = new Collider[16];

    private void Start() 
    {
        DetectPromptUI();
    }

    private void DetectPromptUI()
    {
        if (promptUIObject == null)
        {
            GameObject pObj = GameObject.Find("Prompt");
            if (pObj == null) pObj = GameObject.Find("InteractPrompt");
            if (pObj == null) pObj = GameObject.Find("AvisoE");
            if (pObj != null)
            {
                promptUIObject = pObj;
                promptUIText = pObj.GetComponent<TextMeshProUGUI>();
                if (promptUIText == null) promptUIText = pObj.GetComponentInChildren<TextMeshProUGUI>();
            }
        }

        if (promptUIObject != null && promptUIText == null)
        {
            promptUIText = promptUIObject.GetComponent<TextMeshProUGUI>();
            if (promptUIText == null) promptUIText = promptUIObject.GetComponentInChildren<TextMeshProUGUI>();
        }
    }

    private void Update()
    {
        // 0. Si el nivel ya fue completado (pantalla de Victoria), suspender interaccion y apagar prompts
        if (RE_LevelComplete.Instance != null && RE_LevelComplete.Instance.IsLevelCompleted())
        {
            OcultarAvisos();
            currentInteractable = null;
            currentInteractableGameObject = null;
            return;
        }

        // Si hay algun dialogo abierto, ocultar avisos y suspender interaccion
        if (RE_NPCInteraction.IsAnyDialogueOpen)
        {
            OcultarAvisos();
            return;
        }

        // 1. Escanear el entorno de forma no-alocativa y considerando orientacion
        FindInteractable();

        // 2. Actualizar avisos visuales (Canvas flotante 3D sobre el NPC)
        UpdateVisualPrompts();

        // 3. Procesar pulsacion de interaccion (tecla E)
        if (WasInteractPressed())
        {
            if (currentInteractable != null)
            {
                Debug.Log($"[RE_PlayerInteraction] Interactuando con: {currentInteractable.GetInteractPrompt()}");
                currentInteractable.Interact();
            }
        }
    }

    private void OcultarAvisos()
    {
        if (promptUIObject != null && promptUIObject.activeSelf) promptUIObject.SetActive(false);
        if (lastPromptCanvas != null && lastPromptCanvas.activeSelf)
        {
            lastPromptCanvas.SetActive(false);
            lastPromptCanvas = null;
        }
    }

    private bool WasInteractPressed()
    {
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) return true;
        if (Gamepad.current != null && (Gamepad.current.buttonSouth.wasPressedThisFrame || Gamepad.current.buttonWest.wasPressedThisFrame)) return true;

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

    private void FindInteractable()
    {
        Vector3 playerCenter = transform.position + Vector3.up * 1.0f;
        Vector3 playerForward = transform.forward;
        playerForward.y = 0;
        if (playerForward.sqrMagnitude > 0.001f) playerForward.Normalize();

        IInteractable bestInteractable = null;
        GameObject bestGameObject = null;
        float bestDistance = Mathf.Infinity;

        // 1. Deteccion no-alocativa mediante esfera de fisica
        int hitCount = Physics.OverlapSphereNonAlloc(playerCenter, sphereRadius, hitBuffer, interactableMask);

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = hitBuffer[i];
            if (col == null || col.gameObject == gameObject) continue;

            IInteractable interactable = col.GetComponent<IInteractable>();
            if (interactable == null || (interactable as Object) == null) interactable = col.GetComponentInParent<IInteractable>();
            if (interactable == null || (interactable as Object) == null) interactable = col.GetComponentInChildren<IInteractable>();

            if (interactable == null && col.attachedRigidbody != null)
            {
                interactable = col.attachedRigidbody.GetComponent<IInteractable>();
            }

            if (interactable != null)
            {
                Vector3 toTarget = col.bounds.center - playerCenter;
                float dist = toTarget.magnitude;
                if (dist <= sphereRadius)
                {
                    // Validar angulo de vision: el jugador debe estar mirando hacia el interactuable
                    Vector3 flatToTarget = toTarget;
                    flatToTarget.y = 0;
                    if (flatToTarget.sqrMagnitude > 0.01f)
                    {
                        float dot = Vector3.Dot(playerForward, flatToTarget.normalized);
                        if (dist > 1.2f && dot < minFacingDot)
                        {
                            continue; // Ignorar interactuables a la espalda o a los lados
                        }
                    }

                    if (dist < bestDistance)
                    {
                        bestDistance = dist;
                        bestInteractable = interactable;
                        bestGameObject = col.gameObject;
                    }
                }
            }
        }

        // 2. Busqueda de seguridad sobre lista estatica de NPCs
        if (bestInteractable == null)
        {
            for (int i = 0; i < RE_NPCInteraction.ActiveNPCs.Count; i++)
            {
                RE_NPCInteraction npc = RE_NPCInteraction.ActiveNPCs[i];
                if (npc == null || !npc.isActiveAndEnabled) continue;

                Vector3 toNpc = (npc.transform.position + Vector3.up * 1.0f) - playerCenter;
                float dist = toNpc.magnitude;
                if (dist <= sphereRadius)
                {
                    Vector3 flatToNpc = toNpc;
                    flatToNpc.y = 0;
                    if (flatToNpc.sqrMagnitude > 0.01f)
                    {
                        float dot = Vector3.Dot(playerForward, flatToNpc.normalized);
                        if (dist > 1.2f && dot < minFacingDot)
                        {
                            continue;
                        }
                    }

                    if (dist < bestDistance)
                    {
                        bestDistance = dist;
                        bestInteractable = npc;
                        bestGameObject = npc.gameObject;
                    }
                }
            }
        }

        if (bestInteractable != currentInteractable)
        {
            currentInteractable = bestInteractable;
            currentInteractableGameObject = bestGameObject;
        }
    }

    /// <summary>
    /// Activa el Canvas flotante 3D que cada NPC tiene colocado sobre su cabeza en la escena.
    /// </summary>
    private void UpdateVisualPrompts()
    {
        if (currentInteractableGameObject != null)
        {
            Transform rootTransform = currentInteractableGameObject.transform;
            if (rootTransform.GetComponent<RE_NPCInteraction>() == null && rootTransform.GetComponent<IInteractable>() == null)
            {
                RE_NPCInteraction parentNPC = rootTransform.GetComponentInParent<RE_NPCInteraction>();
                if (parentNPC != null) rootTransform = parentNPC.transform;
            }

            Transform visualCanvasTransform = null;
            foreach (Transform child in rootTransform.GetComponentsInChildren<Transform>(true))
            {
                string n = child.name.ToLower();
                if (n.Contains("visualizacion") || n.Contains("visualización"))
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

        // Si existe un HUD formal en Canvas 2D
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

    private void OnDisable()
    {
        OcultarAvisos();
        currentInteractable = null;
        currentInteractableGameObject = null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 playerCenter = transform.position + Vector3.up * 1.0f;
        Gizmos.DrawWireSphere(playerCenter, sphereRadius);
    }
}
