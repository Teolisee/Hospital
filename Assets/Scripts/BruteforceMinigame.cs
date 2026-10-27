using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Minijuego estilo "Bruteforce": columnas de letras que giran a diferentes velocidades.
/// El jugador debe bloquear cada columna (de izquierda a derecha) cuando la letra central
/// coincida con la letra correspondiente de la palabra objetivo.
/// Controles: ESPACIO / ENTER, o clic en el botón de pantalla. ESC para cancelar.
/// </summary>
public class BruteforceMinigame : MonoBehaviour
{
    public static BruteforceMinigame Instance { get; private set; }

    [Serializable]
    public class Column
    {
        [Tooltip("5 textos de arriba a abajo. El del medio (índice 2) es la fila resaltada.")]
        public TMP_Text[] rows;
        [HideInInspector] public char[] strip;
        [HideInInspector] public int index;
        [HideInInspector] public float interval, timer;
        [HideInInspector] public bool locked;
    }

    [Header("UI")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private Column[] columns;
    [SerializeField] private Color normalColor = new Color(0.7f, 0.75f, 0.85f, 0.6f);
    [SerializeField] private Color lockedColor = new Color(0.1f, 1f, 0.35f, 1f);
    [SerializeField] private Color activeColor = new Color(0f, 0.9f, 1f, 1f);

    [Header("Reglas")]
    [SerializeField] private float timeLimit = 60f;
    [SerializeField] private float wrongPenalty = 3f;
    [SerializeField] private Vector2 stepInterval = new Vector2(0.08f, 0.2f); // velocidad de giro
    [SerializeField] private string[] words = { "HOSPITAL", "PACIENTE", "URGENCIA", "MEDICINA", "DOCTORES", "SISTEMAS", "FARMACIA", "SEGURIDAD", "ACCESOS" };

    [Header("Eventos")]
    public UnityEvent onOpened;
    public UnityEvent onClosed;
    public UnityEvent onSuccess;
    public UnityEvent onFail;

    public bool IsRunning => running;

    private string target;
    private int current;
    private float timeLeft;
    private bool running;
    private Action<bool> callback;
    private bool uiProceduralGenerada = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    public void Open(Action<bool> onFinished)
    {
        if (running) return;
        callback = onFinished;

        EnsureUI();

        // Determinar número de columnas
        int numCols = (columns != null && columns.Length > 0) ? columns.Length : 8;

        // Palabra objetivo adaptada al número de columnas
        var valid = new List<string>();
        foreach (var w in words)
        {
            if (w.Length == numCols) valid.Add(w.ToUpper());
        }

        target = valid.Count > 0 
            ? valid[UnityEngine.Random.Range(0, valid.Count)]
            : "HOSPITAL".PadRight(numCols, 'X').Substring(0, numCols);

        if (targetText != null)
        {
            targetText.text = $"PALABRA CLAVE: <b><color=#00e5ff>[ {FormatearEspaciado(target)} ]</color></b>";
        }

        timeLeft = timeLimit;
        current = 0;

        if (columns != null)
        {
            foreach (var c in columns)
            {
                if (c == null) continue;
                c.strip = Shuffled("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
                c.index = UnityEngine.Random.Range(0, c.strip.Length);
                c.interval = UnityEngine.Random.Range(stepInterval.x, stepInterval.y);
                c.timer = 0f;
                c.locked = false;
            }
        }

        if (panel != null)
        {
            panel.SetActive(true);
        }

        running = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        onOpened?.Invoke();
        Refresh();
    }

    private void Update()
    {
        if (!running) return;

        timeLeft -= Time.unscaledDeltaTime;
        if (timerText != null)
        {
            int mins = Mathf.Max(0, (int)(timeLeft / 60));
            int secs = Mathf.Max(0, (int)(timeLeft % 60));
            int millis = Mathf.Max(0, (int)((timeLeft % 1) * 1000));
            timerText.text = $"TIEMPO: {mins:00}:{secs:00}:{millis:000}";
        }

        if (timeLeft <= 0f)
        {
            Finish(false);
            return;
        }

        if (columns != null)
        {
            foreach (var c in columns)
            {
                if (c == null || c.locked || c.strip == null || c.strip.Length == 0) continue;

                c.timer += Time.unscaledDeltaTime;
                if (c.timer >= c.interval)
                {
                    c.timer = 0f;
                    c.index = (c.index + 1) % c.strip.Length;
                }
            }
        }

        Refresh();

        // Soporte de entrada seguro tanto para Input System como para Input clásico
        bool lockPressed = false;
        bool cancelPressed = false;

        if (Keyboard.current != null)
        {
            lockPressed = Keyboard.current.spaceKey.wasPressedThisFrame || 
                          Keyboard.current.enterKey.wasPressedThisFrame ||
                          Keyboard.current.numpadEnterKey.wasPressedThisFrame;
            cancelPressed = Keyboard.current.escapeKey.wasPressedThisFrame;
        }

        if (!lockPressed && !cancelPressed)
        {
            try
            {
                lockPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
                cancelPressed = Input.GetKeyDown(KeyCode.Escape);
            }
            catch { }
        }

        if (lockPressed) LockCurrentColumn();
        if (cancelPressed) Finish(false);
    }

    /// <summary>
    /// Intenta bloquear la columna actual si la letra central coincide con el objetivo.
    /// Puede ser llamado desde el botón de la UI o mediante Espacio/Enter.
    /// </summary>
    public void LockCurrentColumn()
    {
        if (!running || columns == null || current >= columns.Length) return;

        var c = columns[current];
        if (c == null || c.strip == null || c.strip.Length == 0) return;

        char centerLetter = c.strip[c.index];

        if (centerLetter == target[current])
        {
            c.locked = true;
            current++;
            Refresh();

            if (current >= columns.Length)
            {
                if (targetText != null)
                {
                    targetText.text = $"<color=#00ff66>¡ACCESO AUTORIZADO: {target}!</color>";
                }
                StartCoroutine(SecuenciaExito());
            }
        }
        else
        {
            timeLeft -= wrongPenalty;
            if (timeLeft < 0f) timeLeft = 0f;
            StartCoroutine(FlashErrorColumna(c));
        }
    }

    private IEnumerator FlashErrorColumna(Column c)
    {
        if (c != null && c.rows != null)
        {
            int centerIdx = c.rows.Length / 2;
            if (centerIdx < c.rows.Length && c.rows[centerIdx] != null)
            {
                Color prev = c.rows[centerIdx].color;
                c.rows[centerIdx].color = Color.red;
                yield return new WaitForSecondsRealtime(0.2f);
                if (c.rows[centerIdx] != null) c.rows[centerIdx].color = prev;
            }
        }
    }

    private IEnumerator SecuenciaExito()
    {
        running = false;
        yield return new WaitForSecondsRealtime(0.6f);
        Finish(true);
    }

    private void Refresh()
    {
        if (columns == null) return;

        for (int i = 0; i < columns.Length; i++)
        {
            var c = columns[i];
            if (c == null || c.rows == null || c.strip == null || c.strip.Length == 0) continue;

            int centerRow = c.rows.Length / 2;

            for (int r = 0; r < c.rows.Length; r++)
            {
                if (c.rows[r] == null) continue;

                int offset = r - centerRow;
                int idx = ((c.index + offset) % c.strip.Length + c.strip.Length) % c.strip.Length;
                
                c.rows[r].text = c.strip[idx].ToString();

                bool isCenter = (r == centerRow);

                if (c.locked)
                {
                    c.rows[r].color = isCenter ? lockedColor : new Color(lockedColor.r, lockedColor.g, lockedColor.b, 0.35f);
                }
                else if (i == current && isCenter)
                {
                    c.rows[r].color = activeColor;
                }
                else
                {
                    c.rows[r].color = isCenter ? Color.white : normalColor;
                }
            }
        }
    }

    private void Finish(bool success)
    {
        running = false;

        if (panel != null)
        {
            panel.SetActive(false);
        }

        (success ? onSuccess : onFail)?.Invoke();
        onClosed?.Invoke();

        var cb = callback;
        callback = null;
        cb?.Invoke(success);
    }

    private static string FormatearEspaciado(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return string.Join(" ", s.ToCharArray());
    }

    private static char[] Shuffled(string s)
    {
        var a = s.ToCharArray();
        for (int i = a.Length - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (a[i], a[j]) = (a[j], a[i]);
        }
        return a;
    }

    /// <summary>
    /// Garantiza que exista una interfaz gráfica funcional para el minijuego.
    /// Si el usuario ya asignó un panel y columnas en el Inspector, se respetan totalmente.
    /// Si faltan referencias, genera automáticamente un panel de terminal cyber-hospital.
    /// </summary>
    private void EnsureUI()
    {
        if (panel != null && columns != null && columns.Length > 0 && columns[0].rows != null && columns[0].rows.Length > 0)
        {
            return;
        }

        // Buscar panel preexistente en la escena
        if (panel == null)
        {
            string[] nombresPosibles = { "BruteForcePanel", "BruteforcePanel", "PanelBruteforce", "Panel Bruteforce" };
            foreach (string n in nombresPosibles)
            {
                GameObject obj = GameObject.Find(n);
                if (obj != null)
                {
                    panel = obj;
                    break;
                }
            }
        }

        // Si aún no hay panel o columnas completas, construir la interfaz procedural
        if (!uiProceduralGenerada && (panel == null || columns == null || columns.Length == 0))
        {
            ConstruirUIProcedural();
        }
    }

    private void ConstruirUIProcedural()
    {
        uiProceduralGenerada = true;

        // 1. Canvas raíz persistente
        GameObject canvasGo = new GameObject("Canvas_BruteforceTerminal");
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250; // Sobre el HUD del juego

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        // 2. Fondo oscuro completo (Overlay)
        GameObject overlayGo = new GameObject("BruteforcePanel");
        overlayGo.transform.SetParent(canvasGo.transform, false);
        RectTransform overlayRt = overlayGo.AddComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;

        Image overlayImg = overlayGo.AddComponent<Image>();
        overlayImg.color = new Color(0.03f, 0.05f, 0.09f, 0.92f);

        panel = overlayGo;

        // 3. Ventana central del terminal
        GameObject windowGo = new GameObject("WindowTerminal");
        windowGo.transform.SetParent(overlayGo.transform, false);
        RectTransform winRt = windowGo.AddComponent<RectTransform>();
        winRt.anchorMin = new Vector2(0.5f, 0.5f);
        winRt.anchorMax = new Vector2(0.5f, 0.5f);
        winRt.sizeDelta = new Vector2(1040f, 660f);
        winRt.anchoredPosition = Vector2.zero;

        Image winImg = windowGo.AddComponent<Image>();
        winImg.color = new Color(0.08f, 0.12f, 0.18f, 0.98f);

        // 4. Título y Subtítulo
        GameObject titleGo = new GameObject("TitleText");
        titleGo.transform.SetParent(windowGo.transform, false);
        RectTransform titleRt = titleGo.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(980f, 45f);
        titleRt.anchoredPosition = new Vector2(0f, -40f);

        TextMeshProUGUI titleTMP = titleGo.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "<color=#00e5ff><b>TERMINAL DE SEGURIDAD // DESCIFRADO BRUTEFORCE</b></color>";
        titleTMP.fontSize = 26;
        titleTMP.alignment = TextAlignmentOptions.Center;

        GameObject subGo = new GameObject("SubtitleText");
        subGo.transform.SetParent(windowGo.transform, false);
        RectTransform subRt = subGo.AddComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0.5f, 1f);
        subRt.anchorMax = new Vector2(0.5f, 1f);
        subRt.sizeDelta = new Vector2(980f, 30f);
        subRt.anchoredPosition = new Vector2(0f, -75f);

        TextMeshProUGUI subTMP = subGo.AddComponent<TextMeshProUGUI>();
        subTMP.text = "Sincroniza cada columna con la letra objetivo cuando pase por el centro.";
        subTMP.fontSize = 15;
        subTMP.color = new Color(0.6f, 0.7f, 0.85f);
        subTMP.alignment = TextAlignmentOptions.Center;

        // 5. Palabra Clave
        GameObject targetGo = new GameObject("TargetText");
        targetGo.transform.SetParent(windowGo.transform, false);
        RectTransform targetRt = targetGo.AddComponent<RectTransform>();
        targetRt.anchorMin = new Vector2(0.5f, 1f);
        targetRt.anchorMax = new Vector2(0.5f, 1f);
        targetRt.sizeDelta = new Vector2(980f, 40f);
        targetRt.anchoredPosition = new Vector2(0f, -115f);

        targetText = targetGo.AddComponent<TextMeshProUGUI>();
        targetText.text = "PALABRA CLAVE: [ H O S P I T A L ]";
        targetText.fontSize = 22;
        targetText.alignment = TextAlignmentOptions.Center;

        // 6. Temporizador
        GameObject timerGo = new GameObject("TimerText");
        timerGo.transform.SetParent(windowGo.transform, false);
        RectTransform timerRt = timerGo.AddComponent<RectTransform>();
        timerRt.anchorMin = new Vector2(0.5f, 1f);
        timerRt.anchorMax = new Vector2(0.5f, 1f);
        timerRt.sizeDelta = new Vector2(300f, 35f);
        timerRt.anchoredPosition = new Vector2(0f, -155f);

        timerText = timerGo.AddComponent<TextMeshProUGUI>();
        timerText.text = "TIEMPO: 01:00:000";
        timerText.fontSize = 20;
        timerText.color = new Color(1f, 0.85f, 0.3f);
        timerText.alignment = TextAlignmentOptions.Center;

        // 7. Contenedor de Carretes (Columnas)
        GameObject reelsContainer = new GameObject("ReelsContainer");
        reelsContainer.transform.SetParent(windowGo.transform, false);
        RectTransform reelsRt = reelsContainer.AddComponent<RectTransform>();
        reelsRt.anchorMin = new Vector2(0.5f, 0.5f);
        reelsRt.anchorMax = new Vector2(0.5f, 0.5f);
        reelsRt.sizeDelta = new Vector2(940f, 280f);
        reelsRt.anchoredPosition = new Vector2(0f, -20f);

        HorizontalLayoutGroup hGroup = reelsContainer.AddComponent<HorizontalLayoutGroup>();
        hGroup.spacing = 14f;
        hGroup.childAlignment = TextAnchor.MiddleCenter;
        hGroup.childControlWidth = false;
        hGroup.childControlHeight = false;

        // Barra indicadora de la fila central
        GameObject centerBar = new GameObject("CenterRowHighlight");
        centerBar.transform.SetParent(reelsContainer.transform, false);
        RectTransform barRt = centerBar.AddComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0f, 0.5f);
        barRt.anchorMax = new Vector2(1f, 0.5f);
        barRt.sizeDelta = new Vector2(0f, 52f);
        barRt.anchoredPosition = Vector2.zero;
        Image barImg = centerBar.AddComponent<Image>();
        barImg.color = new Color(0f, 0.9f, 1f, 0.12f);
        barImg.raycastTarget = false;

        // 8. Crear 8 columnas con 5 filas cada una
        int numCols = 8;
        columns = new Column[numCols];

        for (int c = 0; c < numCols; c++)
        {
            GameObject colGo = new GameObject($"Column_{c}");
            colGo.transform.SetParent(reelsContainer.transform, false);
            RectTransform colRt = colGo.AddComponent<RectTransform>();
            colRt.sizeDelta = new Vector2(100f, 260f);

            Image colBg = colGo.AddComponent<Image>();
            colBg.color = new Color(0.04f, 0.08f, 0.14f, 0.95f);

            VerticalLayoutGroup vGroup = colGo.AddComponent<VerticalLayoutGroup>();
            vGroup.spacing = 2f;
            vGroup.childAlignment = TextAnchor.MiddleCenter;
            vGroup.childControlWidth = true;
            vGroup.childControlHeight = true;

            Column col = new Column();
            col.rows = new TMP_Text[5];

            for (int r = 0; r < 5; r++)
            {
                GameObject rowGo = new GameObject($"Row_{r}");
                rowGo.transform.SetParent(colGo.transform, false);

                TextMeshProUGUI rowTMP = rowGo.AddComponent<TextMeshProUGUI>();
                rowTMP.alignment = TextAlignmentOptions.Center;
                rowTMP.text = "-";

                if (r == 2)
                {
                    rowTMP.fontSize = 32;
                    rowTMP.fontStyle = FontStyles.Bold;
                    rowTMP.color = activeColor;
                }
                else
                {
                    rowTMP.fontSize = 20;
                    rowTMP.color = normalColor;
                }

                col.rows[r] = rowTMP;
            }

            columns[c] = col;
        }

        // 9. Botones de acción inferiores
        GameObject btnContainer = new GameObject("ButtonsContainer");
        btnContainer.transform.SetParent(windowGo.transform, false);
        RectTransform btnCRt = btnContainer.AddComponent<RectTransform>();
        btnCRt.anchorMin = new Vector2(0.5f, 0f);
        btnCRt.anchorMax = new Vector2(0.5f, 0f);
        btnCRt.sizeDelta = new Vector2(600f, 60f);
        btnCRt.anchoredPosition = new Vector2(0f, 40f);

        HorizontalLayoutGroup btnHGroup = btnContainer.AddComponent<HorizontalLayoutGroup>();
        btnHGroup.spacing = 24f;
        btnHGroup.childAlignment = TextAnchor.MiddleCenter;
        btnHGroup.childControlWidth = false;
        btnHGroup.childControlHeight = false;

        // Botón Bloquear
        GameObject lockBtnGo = new GameObject("BtnLockColumn");
        lockBtnGo.transform.SetParent(btnContainer.transform, false);
        RectTransform lockRt = lockBtnGo.AddComponent<RectTransform>();
        lockRt.sizeDelta = new Vector2(320f, 48f);

        Image lockImg = lockBtnGo.AddComponent<Image>();
        lockImg.color = new Color(0f, 0.55f, 0.95f);

        Button lockBtn = lockBtnGo.AddComponent<Button>();
        lockBtn.onClick.AddListener(LockCurrentColumn);

        GameObject lockTxtGo = new GameObject("Text");
        lockTxtGo.transform.SetParent(lockBtnGo.transform, false);
        RectTransform lockTxtRt = lockTxtGo.AddComponent<RectTransform>();
        lockTxtRt.anchorMin = Vector2.zero;
        lockTxtRt.anchorMax = Vector2.one;
        lockTxtRt.offsetMin = Vector2.zero;
        lockTxtRt.offsetMax = Vector2.zero;
        TextMeshProUGUI lockTMP = lockTxtGo.AddComponent<TextMeshProUGUI>();
        lockTMP.text = "<b>BLOQUEAR LETRA [ESPACIO]</b>";
        lockTMP.fontSize = 18;
        lockTMP.color = Color.white;
        lockTMP.alignment = TextAlignmentOptions.Center;

        // Botón Cancelar
        GameObject cancelBtnGo = new GameObject("BtnCancel");
        cancelBtnGo.transform.SetParent(btnContainer.transform, false);
        RectTransform cancelRt = cancelBtnGo.AddComponent<RectTransform>();
        cancelRt.sizeDelta = new Vector2(180f, 48f);

        Image cancelImg = cancelBtnGo.AddComponent<Image>();
        cancelImg.color = new Color(0.35f, 0.12f, 0.16f);

        Button cancelBtn = cancelBtnGo.AddComponent<Button>();
        cancelBtn.onClick.AddListener(() => Finish(false));

        GameObject cancelTxtGo = new GameObject("Text");
        cancelTxtGo.transform.SetParent(cancelBtnGo.transform, false);
        RectTransform cancelTxtRt = cancelTxtGo.AddComponent<RectTransform>();
        cancelTxtRt.anchorMin = Vector2.zero;
        cancelTxtRt.anchorMax = Vector2.one;
        cancelTxtRt.offsetMin = Vector2.zero;
        cancelTxtRt.offsetMax = Vector2.zero;
        TextMeshProUGUI cancelTMP = cancelTxtGo.AddComponent<TextMeshProUGUI>();
        cancelTMP.text = "<b>SALIR [ESC]</b>";
        cancelTMP.fontSize = 16;
        cancelTMP.color = new Color(1f, 0.7f, 0.7f);
        cancelTMP.alignment = TextAlignmentOptions.Center;

        panel.SetActive(false);
    }
}
