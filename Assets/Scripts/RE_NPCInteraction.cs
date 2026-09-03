using UnityEngine; // Librería estándar de Unity.
using UnityEngine.UI; // Herramienta para Botones (Botón 'Continuar').
using TMPro; // Textos bonitos y nítidos.

// -----------------------------------------------------------------------------
// SCRIPT: RE_NPCInteraction
// METÁFORA: "El Libreto de los Actores"
// Este script está metido adentro de todos los NPCs (Guardia, Civil, Enfermero, Recepcionista).
// Contiene lo que te van a decir, sabe cuándo callarse, y sabe cuándo es hora 
// de registrar el progreso y finalizar el nivel (La Recepcionista).
// -----------------------------------------------------------------------------
public class RE_NPCInteraction : MonoBehaviour, IInteractable 
{
    [Header("Configuración de NPC")]
    [Tooltip("El mensaje que aparecerá al acercarse al NPC.")]
    public string promptText = "Presiona E para hablar";

    [Tooltip("ID único para registrar esta conversación en el progreso. Si está vacío, se usa el nombre del GameObject.")]
    public string npcId;

    [Header("Contenido del Diálogo")]
    [Tooltip("El nombre del NPC que se mostrará en la interfaz.")]
    public string npcName;

    [Tooltip("El texto del diálogo que dirá el NPC.")]
    [TextArea(3, 10)]
    public string npcDialogue;

    [Header("Configuración de Flujo y Orden")]
    [Tooltip("Si está activo, obliga a hablar en orden estricto (Guardia -> Civil -> Enfermero). Por defecto está desactivado para libertad de exploración.")]
    public bool requiereOrdenSecuencial = false;

    [Header("Configuración de Facturación (Recepción/Fin de Nivel)")]
    [Tooltip("Si está activo, este NPC actuará como el punto de facturación que completa el nivel.")]
    public bool esNPCFacturacion = false;

    [Tooltip("Mensaje que muestra si intentas facturar pero te faltan tareas por completar.")]
    [TextArea(3, 5)] 
    public string mensajeTareasPendientes = "Aún no puedes facturar. Habla con los demás primero.";

    [Header("Referencias de UI")]
    [Tooltip("El objeto/Canvas que contiene los diálogos del NPC.")]
    public GameObject npcDialogosCanvas;

    [Tooltip("El botón para continuar/salir del diálogo.")]
    public Button botonContinuar;

    private bool puedeFacturar = false;

    private void Start()
    {
        // 1. Si no se especificó un ID manual, usamos el nombre del GameObject
        if (string.IsNullOrEmpty(npcId)) 
        {
            npcId = gameObject.name;
        }

        string lowerName = gameObject.name.ToLower();
        string lowerId = (npcId ?? "").ToLower();

        // 2. Detección automática de Recepcionista / Facturación por nombre
        if (lowerName.Contains("recepcionista") || 
            lowerName.Contains("facturacion") || 
            lowerId.Contains("recepcionista") ||
            lowerId.Contains("facturacion"))
        {
            esNPCFacturacion = true; 
        }

        // 3. Garantizar que este NPC tenga un Collider para que el jugador lo detecte
        Collider col = GetComponent<Collider>();
        if (col == null) col = GetComponentInChildren<Collider>();
        if (col == null)
        {
            CapsuleCollider newCap = gameObject.AddComponent<CapsuleCollider>();
            newCap.radius = 0.6f;
            newCap.height = 2.0f;
            newCap.center = new Vector3(0, 1.0f, 0);
        }

        // 4. Auto-detección del Canvas de Diálogo si no fue arrastrado en el Inspector
        if (npcDialogosCanvas == null)
        {
            if (lowerName.Contains("civil") || lowerId.Contains("civil") || lowerName.Contains("bot"))
            {
                npcDialogosCanvas = BuscarCanvasPorNombre("civil");
            }
            else if (lowerName.Contains("guardia") || lowerId.Contains("guardia"))
            {
                npcDialogosCanvas = BuscarCanvasPorNombre("guardia");
            }
            else if (lowerName.Contains("enfermer") || lowerId.Contains("enfermer"))
            {
                npcDialogosCanvas = BuscarCanvasPorNombre("enfermer");
            }
            else if (lowerName.Contains("recepcion") || lowerId.Contains("recepcion"))
            {
                npcDialogosCanvas = BuscarCanvasPorNombre("recepcion");
            }
        }

        // 5. Configuración del Canvas encontrado
        if (npcDialogosCanvas != null) 
        {
            npcDialogosCanvas.SetActive(false); 

            // Buscar botón Continuar dentro de su propio canvas
            if (botonContinuar == null || !botonContinuar.transform.IsChildOf(npcDialogosCanvas.transform))
            {
                Button foundBtn = npcDialogosCanvas.GetComponentInChildren<Button>(true); 
                if (foundBtn != null) botonContinuar = foundBtn; 
            }

            // Asignar nombre si está vacío
            if (string.IsNullOrEmpty(npcName))
            {
                npcName = GetExistingNameFromCanvas();
                if (string.IsNullOrEmpty(npcName))
                {
                    npcName = gameObject.name.Replace("Npc", "").Replace("NPC", "").Trim();
                }
            }

            // Asignar diálogo si está vacío
            if (string.IsNullOrEmpty(npcDialogue))
            {
                npcDialogue = GetExistingDialogueFromCanvas();
            }
        }

        // 6. Conectar evento del botón Continuar de forma segura
        if (botonContinuar != null)
        {
            botonContinuar.onClick.RemoveListener(CerrarDialogo);
            botonContinuar.onClick.AddListener(CerrarDialogo);
        }
    }

    private GameObject BuscarCanvasPorNombre(string keyword)
    {
        foreach (Canvas c in Resources.FindObjectsOfTypeAll<Canvas>())
        {
            if (c.hideFlags == HideFlags.None && c.gameObject.name.ToLower().Contains(keyword.ToLower()))
            {
                return c.gameObject;
            }
        }
        return null;
    }

    // Parte del contrato "IInteractable". Devolvemos el letrero flotante que el jugador leerá.
    public string GetInteractPrompt()
    {
        if (!string.IsNullOrEmpty(promptText))
        {
            bool isGuardiaObj = gameObject.name.ToLower().Contains("guardia") || (npcId ?? "").ToLower().Contains("guardia");
            bool promptHasGuardia = promptText.ToLower().Contains("guardia");

            if (isGuardiaObj || !promptHasGuardia)
            {
                return promptText;
            }
        }

        string nombreMostrar = !string.IsNullOrEmpty(npcName) ? npcName : gameObject.name.Replace("Npc", "").Replace("NPC", "").Trim();
        return $"Presiona E para hablar con {nombreMostrar}";
    }

    // Parte del contrato "IInteractable". Se acciona cuando el jugador aprieta la tecla E sobre nosotros.
    public void Interact()
    {
        AbrirDialogo(); 
    }

    // METÁFORA: "La Charla". Congela el movimiento y muestra el cuadro de diálogo.
    private void AbrirDialogo() 
    {
        if (npcDialogosCanvas != null) 
        {
            if (esNPCFacturacion) 
            {
                int tareasCompletadas = RE_GameProgress.Instance != null ? RE_GameProgress.Instance.progressData.completedTasks.Count : 0;
                
                if (RE_GameProgress.Instance != null && RE_GameProgress.Instance.IsTaskCompleted(npcId))
                {
                    tareasCompletadas--;
                }

                int tareasRequeridas = RE_GameProgress.Instance != null ? (RE_GameProgress.Instance.totalMainTasks - 1) : 3;
                if (tareasRequeridas < 1) tareasRequeridas = 1;

                if (tareasCompletadas >= tareasRequeridas)
                {
                    puedeFacturar = true;
                    UpdateDialogueTexts(npcName, npcDialogue);
                }
                else
                {
                    puedeFacturar = false;
                    string textoAviso = !string.IsNullOrEmpty(mensajeTareasPendientes) 
                        ? mensajeTareasPendientes 
                        : $"Aún no puedes facturar. Llevas {tareasCompletadas} de {tareasRequeridas} tareas completadas.";
                    UpdateDialogueTexts(npcName, textoAviso);
                }
            }
            else 
            {
                UpdateDialogueTexts(npcName, npcDialogue);
            }

            npcDialogosCanvas.SetActive(true);
            
            Cursor.lockState = CursorLockMode.None; 
            Cursor.visible = true; 

            if (RE_PlayerHealth.Instance != null) RE_PlayerHealth.Instance.SetPaused(true);

            RE_PlayerMovement pm = FindFirstObjectByType<RE_PlayerMovement>();
            if (pm != null) pm.enabled = false;
        }
        else
        {
            // Si por alguna razón no hay canvas, registramos el progreso para no atascar al jugador
            RegistrarProgresoNPC();
        }
    }

    // Se ejecuta al hacer clic en el botón Continuar de la UI.
    public void CerrarDialogo() 
    {
        if (npcDialogosCanvas != null && npcDialogosCanvas.activeSelf)
        {
            npcDialogosCanvas.SetActive(false); 
            
            Cursor.lockState = CursorLockMode.Locked; 
            Cursor.visible = false; 

            if (RE_PlayerHealth.Instance != null) RE_PlayerHealth.Instance.SetPaused(false);

            RE_PlayerMovement pm = FindFirstObjectByType<RE_PlayerMovement>();
            if (pm != null) pm.enabled = true;

            RegistrarProgresoNPC();
        }
    }

    private void RegistrarProgresoNPC()
    {
        if (esNPCFacturacion)
        {
            if (puedeFacturar)
            {
                if (RE_GameProgress.Instance != null)
                {
                    RE_GameProgress.Instance.CompleteTask(npcId);
                }
                if (RE_LevelComplete.Instance != null)
                {
                    RE_LevelComplete.Instance.TriggerLevelComplete();
                }
            }
            else
            {
                Debug.Log($"[RE_NPCInteraction] Recepción: Aún faltan tareas pendientes antes de facturar.");
            }
        }
        else
        {
            if (RE_GameProgress.Instance != null)
            {
                bool canComplete = true;

                if (requiereOrdenSecuencial)
                {
                    string lowerName = gameObject.name.ToLower();
                    string lowerId = (npcId ?? "").ToLower();

                    bool isCivil = lowerName.Contains("civil") || lowerId.Contains("civil");
                    bool isEnfermero = lowerName.Contains("enfermero") || lowerId.Contains("enfermero");

                    if (isCivil) canComplete = RE_GameProgress.Instance.IsGuardiaCompleted();
                    else if (isEnfermero) canComplete = RE_GameProgress.Instance.IsCivilCompleted();
                }

                if (canComplete)
                {
                    RE_GameProgress.Instance.CompleteTask(npcId);
                    Debug.Log($"[RE_NPCInteraction] Tarea completada con éxito para NPC: '{npcId}' ({npcName})");
                }
                else
                {
                    Debug.LogWarning($"[RE_NPCInteraction] Tarea no completada para '{npcId}' porque 'requiereOrdenSecuencial' está activo y no se cumplió el orden.");
                }
            }
        }
    }

    private void UpdateDialogueTexts(string title, string content) 
    {
        if (npcDialogosCanvas == null) return; 

        TextMeshProUGUI[] tmproTexts = npcDialogosCanvas.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var txt in tmproTexts)
        {
            string objName = txt.gameObject.name.ToLower();
            
            if (objName.Contains("nombre") || objName.Contains("title") || objName.Contains("name"))
            {
                if (!string.IsNullOrEmpty(title)) txt.text = title;
            }
            else if (objName.Contains("texto") || objName.Contains("dialog") || objName.Contains("cuerpo") || objName.Contains("content"))
            {
                if (!string.IsNullOrEmpty(content)) txt.text = content;
            }
        }

        Text[] legacyTexts = npcDialogosCanvas.GetComponentsInChildren<Text>(true);
        foreach (var txt in legacyTexts)
        {
            string objName = txt.gameObject.name.ToLower();
            if (objName.Contains("nombre") || objName.Contains("title") || objName.Contains("name"))
            {
                if (!string.IsNullOrEmpty(title)) txt.text = title;
            }
            else if (objName.Contains("texto") || objName.Contains("dialog") || objName.Contains("cuerpo") || objName.Contains("content"))
            {
                if (!string.IsNullOrEmpty(content)) txt.text = content;
            }
        }
    }

    private string GetExistingNameFromCanvas() 
    {
        if (npcDialogosCanvas == null) return null;
        foreach (var txt in npcDialogosCanvas.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (txt.gameObject.name.ToLower().Contains("nombre") || txt.gameObject.name.ToLower().Contains("title"))
            {
                if (!string.IsNullOrEmpty(txt.text)) return txt.text;
            }
        }
        return null;
    }

    private string GetExistingDialogueFromCanvas()
    {
        if (npcDialogosCanvas == null) return null;
        foreach (var txt in npcDialogosCanvas.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            string objName = txt.gameObject.name.ToLower();
            if (objName.Contains("nombre") || objName.Contains("title") || objName.Contains("name")) continue; 
            if (objName.Contains("texto") || objName.Contains("dialog") || objName.Contains("cuerpo") || objName.Contains("content"))
            {
                if (!string.IsNullOrEmpty(txt.text)) return txt.text;
            }
        }
        return null;
    }

    private void OnDestroy() 
    {
        if (botonContinuar != null)
        {
            botonContinuar.onClick.RemoveListener(CerrarDialogo);
        }
    }
}
