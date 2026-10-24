using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Controla la interaccion, cuadros de dialogo y finalizacion de misiones con los NPCs.
/// Utiliza exclusivamente los Canvas nativos de la escena.
/// La victoria final del nivel solo puede ser activada por la Recepcionista tras hablar con los 3 NPCs previos.
/// </summary>
public class RE_NPCInteraction : MonoBehaviour, IInteractable 
{
    public static bool IsAnyDialogueOpen { get; private set; } = false;
    public static RE_NPCInteraction CurrentActiveNPC { get; private set; } = null;

    // Registro estatico de NPCs activos para busqueda rapida y sin recoleccion de basura
    public static readonly List<RE_NPCInteraction> ActiveNPCs = new List<RE_NPCInteraction>();

    [Header("Configuracion de NPC")]
    [Tooltip("El mensaje que aparecera al acercarse al NPC.")]
    public string promptText = "Presiona E para hablar";

    [Tooltip("ID unico para registrar esta conversacion en el progreso.")]
    public string npcId;

    [Header("Contenido del Dialogo")]
    [Tooltip("El nombre del NPC que se mostrara en la interfaz.")]
    public string npcName;

    [Tooltip("El texto del dialogo que dira el NPC.")]
    [TextArea(3, 10)]
    public string npcDialogue;

    [Header("Configuracion de Flujo y Orden")]
    [Tooltip("Si esta activo, obliga a hablar en orden estricto (Guardia -> Civil -> Enfermero).")]
    public bool requiereOrdenSecuencial = false;

    [Header("Configuracion de Facturacion (Recepcion/Fin de Nivel)")]
    [Tooltip("Si esta activo, este NPC actuara como el punto de facturacion que completa el nivel.")]
    public bool esNPCFacturacion = false;

    [Tooltip("Mensaje que muestra si intentas facturar pero te faltan tareas por completar.")]
    [TextArea(3, 5)] 
    public string mensajeTareasPendientes = "Aun no puedes facturar. Habla con los demas primero.";

    [Header("Referencias de UI")]
    [Tooltip("El objeto/Canvas que contiene los dialogos del NPC.")]
    public GameObject npcDialogosCanvas;

    [Tooltip("El boton para continuar/salir del dialogo.")]
    public Button botonContinuar;

    private bool puedeFacturar = false;
    private int frameDialogoAbierto = 0;

    private void OnEnable()
    {
        if (!ActiveNPCs.Contains(this))
        {
            ActiveNPCs.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveNPCs.Remove(this);
        if (CurrentActiveNPC == this)
        {
            CerrarDialogo();
        }
    }

    private void Awake()
    {
        EnsureAllCanvasesScaleOne();
    }

    private void Start() 
    {
        string lowerName = gameObject.name.ToLower();

        // Si es un bot o duplicado huerfano en la escena, desactivarlo inmediatamente
        if (lowerName.Contains("bot"))
        {
            gameObject.SetActive(false);
            enabled = false;
            return;
        }

        // Auto-normalizacion robusta de IDs, Nombres y Prompts para evitar inconsistencias de YAML
        if (lowerName.Contains("guardia"))
        {
            npcId = "Npc Guardia";
            if (string.IsNullOrEmpty(npcName) || npcName.ToLower().Contains("enfermer")) npcName = "Guardia";
            promptText = "Presiona E para hablar con el Guardia";
            esNPCFacturacion = false;
        }
        else if (lowerName.Contains("enfermer"))
        {
            npcId = "Npc Enfermero";
            if (string.IsNullOrEmpty(npcName) || npcName.ToLower().Contains("guardia")) npcName = "Enfermero";
            promptText = "Presiona E para hablar con el Enfermero";
            esNPCFacturacion = false;
        }
        else if (lowerName.Contains("remy") || lowerName.Contains("civil"))
        {
            npcId = "Npc Civil";
            if (string.IsNullOrEmpty(npcName)) npcName = "Civil";
            promptText = "Presiona E para hablar con el Civil";
            esNPCFacturacion = false;
        }
        else if (lowerName.Contains("recepcion") || lowerName.Contains("facturacion"))
        {
            npcId = "Npc Recepcionista";
            if (string.IsNullOrEmpty(npcName)) npcName = "Recepcionista";
            promptText = "Presiona E para hablar con la Recepcionista";
            esNPCFacturacion = true;
        }
        else
        {
            if (string.IsNullOrEmpty(npcId)) npcId = gameObject.name;
        }

        // Asegurar collider de interaccion
        Collider col = GetComponent<Collider>();
        if (col == null) col = GetComponentInChildren<Collider>();
        if (col == null)
        {
            CapsuleCollider newCap = gameObject.AddComponent<CapsuleCollider>();
            newCap.radius = 0.6f;
            newCap.height = 2.0f;
            newCap.center = new Vector3(0, 1.0f, 0);
        }

        // Auto-deteccion de canvas si no fue asignado
        if (npcDialogosCanvas == null)
        {
            DetectarCanvas();
        }

        if (npcDialogosCanvas != null) 
        {
            EnsureCanvasScale(npcDialogosCanvas);
            npcDialogosCanvas.SetActive(false); 

            if (botonContinuar == null || !botonContinuar.transform.IsChildOf(npcDialogosCanvas.transform))
            {
                Button foundBtn = npcDialogosCanvas.GetComponentInChildren<Button>(true); 
                if (foundBtn != null) botonContinuar = foundBtn; 
            }

            if (string.IsNullOrEmpty(npcName))
            {
                npcName = GetExistingNameFromCanvas();
                if (string.IsNullOrEmpty(npcName))
                {
                    npcName = gameObject.name.Replace("Npc", "").Replace("NPC", "").Trim();
                }
            }

            if (string.IsNullOrEmpty(npcDialogue))
            {
                npcDialogue = GetExistingDialogueFromCanvas();
            }
        }
    }

    private void DetectarCanvas()
    {
        string lowerName = gameObject.name.ToLower();
        string lowerId = (npcId ?? "").ToLower();

        if (lowerName.Contains("civil") || lowerId.Contains("civil") || lowerName.Contains("remy") || lowerId.Contains("remy"))
        {
            npcDialogosCanvas = BuscarCanvasEscena("civil");
        }
        else if (lowerName.Contains("guardia") || lowerId.Contains("guardia"))
        {
            npcDialogosCanvas = BuscarCanvasEscena("guardia");
        }
        else if (lowerName.Contains("enfermer") || lowerId.Contains("enfermer"))
        {
            npcDialogosCanvas = BuscarCanvasEscena("enfermer");
        }
        else if (lowerName.Contains("recepcion") || lowerId.Contains("recepcion") || esNPCFacturacion)
        {
            npcDialogosCanvas = BuscarCanvasEscena("recepcion");
        }
    }

    private void EnsureCanvasScale(GameObject canvasObj)
    {
        if (canvasObj == null) return;
        canvasObj.transform.localScale = Vector3.one;
        RectTransform rt = canvasObj.GetComponent<RectTransform>();
        if (rt != null) rt.localScale = Vector3.one;
    }

    private void EnsureAllCanvasesScaleOne()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas c in canvases)
        {
            if (c != null && c.name.ToLower().Contains("canvas de"))
            {
                c.transform.localScale = Vector3.one;
                RectTransform rt = c.GetComponent<RectTransform>();
                if (rt != null) rt.localScale = Vector3.one;
            }
        }
    }

    private GameObject BuscarCanvasEscena(string keyword)
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas c in canvases)
        {
            if (c.gameObject.name.ToLower().Contains(keyword.ToLower()))
            {
                EnsureCanvasScale(c.gameObject);
                return c.gameObject;
            }
        }
        return null;
    }

    private void Update()
    {
        // Si ESTE NPC tiene su dialogo abierto y el usuario presiona Espacio, Enter o Escape, cerrar dialogo
        if (CurrentActiveNPC == this && IsAnyDialogueOpen && npcDialogosCanvas != null && npcDialogosCanvas.activeSelf)
        {
            if (Time.frameCount > frameDialogoAbierto + 10)
            {
                bool cerrarInput = false;
                if (Keyboard.current != null)
                {
                    cerrarInput = Keyboard.current.spaceKey.wasPressedThisFrame || 
                                  Keyboard.current.enterKey.wasPressedThisFrame ||
                                  Keyboard.current.escapeKey.wasPressedThisFrame;
                }
                if (!cerrarInput)
                {
                    try
                    {
                        cerrarInput = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Escape);
                    }
                    catch { }
                }

                if (cerrarInput)
                {
                    CerrarDialogo();
                }
            }
        }
    }

    public string GetInteractPrompt()
    {
        return promptText;
    }

    public void Interact() 
    {
        if (requiereOrdenSecuencial && !CumpleOrdenSecuencial())
        {
            return;
        }

        AbrirDialogo(); 
    }

    private void AbrirDialogo() 
    {
        if (CurrentActiveNPC != null && CurrentActiveNPC != this)
        {
            CurrentActiveNPC.CerrarDialogo();
        }

        if (npcDialogosCanvas == null)
        {
            DetectarCanvas();
        }

        if (npcDialogosCanvas != null) 
        {
            EnsureCanvasScale(npcDialogosCanvas);

            string textoAMostrar = "";

            if (esNPCFacturacion) 
            {
                // La Recepcionista verifica explicitamente que se haya hablado con los 3 NPCs previos
                bool guardiaListo = RE_GameProgress.Instance != null && RE_GameProgress.Instance.IsGuardiaCompleted();
                bool civilListo = RE_GameProgress.Instance != null && RE_GameProgress.Instance.IsCivilCompleted();
                bool enfermeroListo = RE_GameProgress.Instance != null && RE_GameProgress.Instance.IsEnfermeroCompleted();

                if (guardiaListo && civilListo && enfermeroListo)
                {
                    puedeFacturar = true;
                    textoAMostrar = !string.IsNullOrEmpty(npcDialogue) 
                        ? npcDialogue 
                        : "¡Excelente trabajo! Has completado todas las revisiones con el Guardia, el Civil y el Enfermero. Tu cita ha sido facturada con exito.";
                }
                else 
                {
                    puedeFacturar = false;
                    List<string> pendientes = new List<string>();
                    if (!guardiaListo) pendientes.Add("el Guardia");
                    if (!civilListo) pendientes.Add("el Civil");
                    if (!enfermeroListo) pendientes.Add("el Enfermero");

                    string lista = string.Join(", ", pendientes);
                    textoAMostrar = !string.IsNullOrEmpty(mensajeTareasPendientes) 
                        ? mensajeTareasPendientes 
                        : $"Aun no puedes facturar tu cita. Primero debes hablar con {lista}.";
                }
            }
            else 
            {
                textoAMostrar = !string.IsNullOrEmpty(npcDialogue) ? npcDialogue : "Hola, gracias por asistir.";
            }

            UpdateDialogueTexts(npcName, textoAMostrar);

            CurrentActiveNPC = this;
            IsAnyDialogueOpen = true;
            frameDialogoAbierto = Time.frameCount;

            // Conectar el listener del boton exclusivamente para este NPC
            if (botonContinuar == null || !botonContinuar.transform.IsChildOf(npcDialogosCanvas.transform))
            {
                Button foundBtn = npcDialogosCanvas.GetComponentInChildren<Button>(true); 
                if (foundBtn != null) botonContinuar = foundBtn; 
            }

            if (botonContinuar != null)
            {
                botonContinuar.onClick.RemoveAllListeners();
                botonContinuar.onClick.AddListener(CerrarDialogo);
            }

            npcDialogosCanvas.SetActive(true);

            Cursor.lockState = CursorLockMode.None; 
            Cursor.visible = true; 

            if (RE_PlayerHealth.Instance != null) RE_PlayerHealth.Instance.SetPaused(true);

            RE_PlayerMovement pm = FindFirstObjectByType<RE_PlayerMovement>();
            if (pm != null) pm.enabled = false;
        }
    }

    public void CerrarDialogo() 
    {
        // Solo el NPC actualmente activo puede procesar su cierre
        if (CurrentActiveNPC != null && CurrentActiveNPC != this)
        {
            return;
        }

        CurrentActiveNPC = null;
        IsAnyDialogueOpen = false;

        if (botonContinuar != null)
        {
            botonContinuar.onClick.RemoveListener(CerrarDialogo);
        }

        if (npcDialogosCanvas != null)
        {
            npcDialogosCanvas.SetActive(false); 
        }

        Cursor.lockState = CursorLockMode.Locked; 
        Cursor.visible = false; 

        if (RE_PlayerHealth.Instance != null) RE_PlayerHealth.Instance.SetPaused(false);

        RE_PlayerMovement pm = FindFirstObjectByType<RE_PlayerMovement>();
        if (pm != null) pm.enabled = true;

        RegistrarProgresoNPC();
    }

    private void RegistrarProgresoNPC()
    {
        if (esNPCFacturacion)
        {
            // Solo si ya hablo con los 3 NPCs previos, la recepcionista completa el nivel
            bool guardiaListo = RE_GameProgress.Instance != null && RE_GameProgress.Instance.IsGuardiaCompleted();
            bool civilListo = RE_GameProgress.Instance != null && RE_GameProgress.Instance.IsCivilCompleted();
            bool enfermeroListo = RE_GameProgress.Instance != null && RE_GameProgress.Instance.IsEnfermeroCompleted();

            if (puedeFacturar && guardiaListo && civilListo && enfermeroListo)
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
                Debug.Log("[RE_NPCInteraction] Recepcionista: No se puede facturar hasta hablar con el Guardia, Civil y Enfermero.");
            }
        }
        else
        {
            // NPCs normales (Guardia, Civil, Enfermero): suman su progreso pero NUNCA activan victoria
            if (RE_GameProgress.Instance != null)
            {
                RE_GameProgress.Instance.CompleteTask(npcId);
            }
        }
    }

    private void UpdateDialogueTexts(string nombre, string mensaje)
    {
        if (npcDialogosCanvas == null) return;

        TextMeshProUGUI[] tmpTexts = npcDialogosCanvas.GetComponentsInChildren<TextMeshProUGUI>(true);
        Text[] legacyTexts = npcDialogosCanvas.GetComponentsInChildren<Text>(true);

        bool textoAsignado = false;

        foreach (TextMeshProUGUI tmp in tmpTexts)
        {
            if (tmp.transform.IsChildOf(botonContinuar != null ? botonContinuar.transform : transform)) continue;

            string n = tmp.gameObject.name.ToLower();
            if (n.Contains("name") || n.Contains("nombre") || n.Contains("titulo"))
            {
                if (!string.IsNullOrEmpty(nombre)) tmp.text = nombre;
            }
            else if (n.Contains("dialog") || n.Contains("texto") || n.Contains("mensaje") || n.Contains("content"))
            {
                tmp.text = mensaje;
                textoAsignado = true;
            }
        }

        foreach (Text leg in legacyTexts)
        {
            if (leg.transform.IsChildOf(botonContinuar != null ? botonContinuar.transform : transform)) continue;

            string n = leg.gameObject.name.ToLower();
            if (n.Contains("name") || n.Contains("nombre") || n.Contains("titulo"))
            {
                if (!string.IsNullOrEmpty(nombre)) leg.text = nombre;
            }
            else if (n.Contains("dialog") || n.Contains("texto") || n.Contains("mensaje") || n.Contains("content"))
            {
                leg.text = mensaje;
                textoAsignado = true;
            }
        }

        if (!textoAsignado)
        {
            foreach (TextMeshProUGUI tmp in tmpTexts)
            {
                if (botonContinuar != null && tmp.transform.IsChildOf(botonContinuar.transform)) continue;
                tmp.text = mensaje;
                break;
            }
        }
    }

    private string GetExistingNameFromCanvas()
    {
        if (npcDialogosCanvas == null) return "";
        TextMeshProUGUI[] tmpTexts = npcDialogosCanvas.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI t in tmpTexts)
        {
            string n = t.gameObject.name.ToLower();
            if ((n.Contains("name") || n.Contains("nombre")) && !string.IsNullOrEmpty(t.text))
            {
                return t.text;
            }
        }
        return "";
    }

    private string GetExistingDialogueFromCanvas()
    {
        if (npcDialogosCanvas == null) return "";
        TextMeshProUGUI[] tmpTexts = npcDialogosCanvas.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI t in tmpTexts)
        {
            if (botonContinuar != null && t.transform.IsChildOf(botonContinuar.transform)) continue;
            string n = t.gameObject.name.ToLower();
            if ((n.Contains("dialog") || n.Contains("texto") || n.Contains("mensaje")) && !string.IsNullOrEmpty(t.text))
            {
                return t.text;
            }
        }
        return "";
    }

    private bool CumpleOrdenSecuencial()
    {
        if (RE_GameProgress.Instance == null) return true;

        string idActual = (npcId ?? gameObject.name).ToLower();

        if (idActual.Contains("civil") || idActual.Contains("remy"))
        {
            if (!RE_GameProgress.Instance.IsGuardiaCompleted())
            {
                Debug.Log("[RE_NPCInteraction] Orden secuencial: Debes hablar primero con el Guardia.");
                return false;
            }
        }
        else if (idActual.Contains("enfermer"))
        {
            if (!RE_GameProgress.Instance.IsCivilCompleted())
            {
                Debug.Log("[RE_NPCInteraction] Orden secuencial: Debes hablar primero con el Civil.");
                return false;
            }
        }

        return true;
    }
}
