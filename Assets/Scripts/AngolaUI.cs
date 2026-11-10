using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Colocar no Canvas (ao lado do MonumentUI). Constrói o painel de Angola em tempo de execução.
public class AngolaUI : MonoBehaviour
{
    public static AngolaUI Instance { get; private set; }

    [Header("Áudio")]
    [Range(0f, 1f)] public float volumeFundo = 0.25f;

    [Header("Mapa 3D")]
    public int resolucaoMapa = 1024;
    public float distanciaCamera = 4.6f;
    public float campoVisao = 45f;

    const string DicaPadrao = "Toque numa província com monumento para abrir os monumentos dessa província.";

    const string DescricaoPadrao =
        "Angola situa-se na costa ocidental da África Austral e é um país de grande diversidade natural e cultural. " +
        "O território é marcado por planaltos, rios e uma extensa costa atlântica, reunindo paisagens muito diferentes " +
        "entre o litoral, o interior e o nordeste. A sua história cruza os povos bantu, o antigo Reino do Kongo e a " +
        "herança portuguesa, visível na língua, na música e nas tradições. Percorra o mapa e descubra os monumentos e " +
        "manifestações culturais de cada província.";

    // Ligação província -> monumentos existentes (pelo campo "nome" do MonumentTarget)
    static readonly Dictionary<string, string[]> monumentosPorProvincia = new Dictionary<string, string[]>
    {
        { "Luanda",      new[] { "Soldado Desconhecido" } },
        { "Malanje",     new[] { "Palanca Negra Gigante", "Marimba" } },
        { "Zaire",       new[] { "Mbanza Kongo" } },
        { "Lunda Norte", new[] { "O Pensador", "Mwana Pwo" } },
        { "Lunda Sul",   new[] { "O Pensador", "Mwana Pwo" } },
        { "Moxico",      new[] { "O Pensador", "Mwana Pwo" } },
        { "Huambo",      new[] { "Monte Luvili" } },
        { "Bie",         new[] { "Monte Luvili" } },
    };

    // ---- UI ----
    GameObject painel;
    TMP_Text titulo;
    TMP_Text descricao;
    TMP_Text dica;
    TMP_Text rotuloGiro;
    TMP_Text rotuloAudio;
    RawImage mapaView;
    TMP_FontAsset fonte;

    // Barra de navegação dentro do painel dos monumentos
    GameObject barraNav;
    TMP_Text rotuloNav;
    Button btnProvAnterior;
    Button btnProvProximo;

    // ---- Palco do mapa ----
    Transform palcoMapa;
    GameObject raizMapa;      // pivô que roda sobre a normal do mapa
    GameObject mapaAtual;
    Camera cameraPreview;
    RenderTexture renderTexture;

    // ---- Estado ----
    AngolaTarget atual;
    AudioSource fundo;
    AudioSource voz;
    bool girando = true;
    bool pausado;
    bool mapaAtivo;
    bool aNavegarMonumento;

    MonumentTarget[] listaAtual;
    int indiceAtual;
    string provinciaAtual;

    int camadaPreview;
    int mascaraPreview;

    void Awake()
    {
        Instance = this;

        camadaPreview = LayerMask.NameToLayer("Preview");
        if (camadaPreview < 0) camadaPreview = 0;
        mascaraPreview = 1 << camadaPreview;

        fundo = gameObject.AddComponent<AudioSource>();
        fundo.playOnAwake = false;
        fundo.spatialBlend = 0f;

        voz = gameObject.AddComponent<AudioSource>();
        voz.playOnAwake = false;
        voz.spatialBlend = 0f;
    }

    void OnDestroy()
    {
        if (cameraPreview != null) cameraPreview.targetTexture = null;
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }

    void Update()
    {
        // Gira o mapa sobre o seu próprio eixo normal
        if (raizMapa != null && girando && !aNavegarMonumento && painel != null && painel.activeSelf && atual != null)
            raizMapa.transform.Rotate(Vector3.up, atual.velocidadeGiro * Time.deltaTime, Space.Self);

        // Se o painel dos monumentos for fechado (X), volta ao mapa
        if (aNavegarMonumento && MonumentUI.Instance != null &&
            MonumentUI.Instance.painel != null && !MonumentUI.Instance.painel.activeSelf)
            VoltarAoMapa();
    }

    // ---- Entrada / saída ----

    public void Mostrar(AngolaTarget t)
    {
        if (t == null) return;
        GarantirUI();
        CriarBarraNav();

        // Já está a navegar num monumento deste mesmo alvo: não sobrepõe o painel
        if (atual == t && aNavegarMonumento) return;
        if (atual == t && painel.activeSelf) return;

        atual = t;
        mapaAtivo = true;
        aNavegarMonumento = false;

        titulo.text = string.IsNullOrEmpty(t.titulo) ? "Angola" : t.titulo;
        descricao.text = string.IsNullOrEmpty(t.descricao) ? DescricaoPadrao : t.descricao;
        dica.text = DicaPadrao;

        girando = true;
        AtualizarRotuloGiro();
        pausado = false;

        CriarMapa(t);
        IniciarAudio(t);

        painel.SetActive(true);
        painel.transform.SetAsLastSibling();
        if (cameraPreview != null) cameraPreview.enabled = true;
        if (barraNav != null) barraNav.SetActive(false);
    }

    public void AoPerderTarget(AngolaTarget t)
    {
        if (t != atual) return;
        Fechar();
    }

    public void Fechar()
    {
        CancelInvoke(nameof(RestaurarDica));

        mapaAtivo = false;
        aNavegarMonumento = false;
        atual = null;
        listaAtual = null;
        indiceAtual = 0;
        provinciaAtual = null;

        if (fundo != null) fundo.Stop();
        if (voz != null) voz.Stop();

        if (raizMapa != null) Destroy(raizMapa);
        raizMapa = null;
        mapaAtual = null;

        if (cameraPreview != null) cameraPreview.enabled = false;
        if (painel != null) painel.SetActive(false);
        if (barraNav != null) barraNav.SetActive(false);
        if (MonumentUI.Instance != null) MonumentUI.Instance.Fechar();
    }

    // ---- Botões do painel ----

    public void AlternarGiro()
    {
        girando = !girando;
        AtualizarRotuloGiro();
    }

    public void AlternarAudio()
    {
        if (fundo == null || voz == null) return;

        pausado = !pausado;
        if (pausado) { fundo.Pause(); voz.Pause(); }
        else { fundo.UnPause(); voz.UnPause(); }
        AtualizarRotuloAudio();
    }

    // ---- Toque no mapa ----

    public void ClicarNoMapa(float u, float v, Camera cam)
    {
        if (!mapaAtivo || atual == null || cam == null || mapaAtual == null) return;

        Ray raio = cam.ViewportPointToRay(new Vector3(Mathf.Clamp01(u), Mathf.Clamp01(v), 0f));
        RaycastHit hit;
        if (!Physics.Raycast(raio, out hit, 100f, mascaraPreview)) return;

        var provincia = hit.collider.GetComponentInParent<ProvinciaAngola>();
        if (provincia == null || string.IsNullOrEmpty(provincia.nome)) return;

        AbrirProvincia(provincia.nome);
    }

    void AbrirProvincia(string provincia)
    {
        var alvos = MonumentosDaProvincia(provincia);
        if (alvos.Length == 0)
        {
            dica.text = "«" + provincia + "» ainda não tem monumentos nesta aplicação.";
            CancelInvoke(nameof(RestaurarDica));
            Invoke(nameof(RestaurarDica), 3f);
            return;
        }

        provinciaAtual = provincia;
        listaAtual = alvos;
        indiceAtual = 0;
        aNavegarMonumento = true;

        painel.SetActive(false);
        if (cameraPreview != null) cameraPreview.enabled = false;
        SuspenderAudio();

        MostrarMonumentoAtual();
    }

    void RestaurarDica()
    {
        if (dica != null) dica.text = DicaPadrao;
    }

    // ---- Navegação entre monumentos da província ----

    public void ProvinciaAnterior()
    {
        if (listaAtual == null || listaAtual.Length == 0) return;
        indiceAtual = (indiceAtual - 1 + listaAtual.Length) % listaAtual.Length;
        MostrarMonumentoAtual();
    }

    public void ProximaProvincia()
    {
        if (listaAtual == null || listaAtual.Length == 0) return;
        indiceAtual = (indiceAtual + 1) % listaAtual.Length;
        MostrarMonumentoAtual();
    }

    public void VoltarAoMapa()
    {
        aNavegarMonumento = false;
        listaAtual = null;
        indiceAtual = 0;
        provinciaAtual = null;

        if (barraNav != null) barraNav.SetActive(false);
        if (MonumentUI.Instance != null) MonumentUI.Instance.Fechar();

        if (mapaAtivo && atual != null)
        {
            painel.SetActive(true);
            painel.transform.SetAsLastSibling();
            if (cameraPreview != null) cameraPreview.enabled = true;
            if (!pausado && fundo != null && voz != null) { fundo.UnPause(); voz.UnPause(); }
        }
    }

    void MostrarMonumentoAtual()
    {
        if (listaAtual == null || listaAtual.Length == 0) return;

        var alvo = listaAtual[Mathf.Clamp(indiceAtual, 0, listaAtual.Length - 1)];
        if (alvo == null) return;

        if (MonumentUI.Instance != null) MonumentUI.Instance.Mostrar(alvo);

        if (barraNav != null)
        {
            rotuloNav.text = provinciaAtual + "  ·  " + (indiceAtual + 1) + "/" + listaAtual.Length;
            btnProvAnterior.interactable = listaAtual.Length > 1;
            btnProvProximo.interactable = listaAtual.Length > 1;
            barraNav.SetActive(true);
            barraNav.transform.SetAsLastSibling();
        }
    }

    MonumentTarget[] MonumentosDaProvincia(string provincia)
    {
        string[] nomes;
        if (!monumentosPorProvincia.TryGetValue(provincia, out nomes))
            return new MonumentTarget[0];

        var todos = FindObjectsByType<MonumentTarget>(FindObjectsInactive.Include);
        var lista = new List<MonumentTarget>();
        foreach (var nome in nomes)
        {
            foreach (var m in todos)
            {
                if (m != null && m.nome == nome && !lista.Contains(m))
                    lista.Add(m);
            }
        }
        return lista.ToArray();
    }

    // ---- Áudio ----

    void IniciarAudio(AngolaTarget t)
    {
        if (fundo == null || voz == null) return;

        pausado = false;

        fundo.Stop();
        fundo.clip = t.musicaFundo;
        fundo.loop = true;
        fundo.volume = volumeFundo;
        if (fundo.clip != null) fundo.Play();

        voz.Stop();
        voz.clip = t.narracao;
        voz.loop = false;
        if (voz.clip != null) voz.Play();

        AtualizarRotuloAudio();
    }

    void SuspenderAudio()
    {
        if (fundo == null || voz == null) return;

        fundo.Pause();
        voz.Pause();
    }

    void AtualizarRotuloGiro()
    {
        if (rotuloGiro != null) rotuloGiro.text = girando ? "Parar giro" : "Continuar giro";
    }

    void AtualizarRotuloAudio()
    {
        if (rotuloAudio != null) rotuloAudio.text = pausado ? ">" : "II";
    }

    // ---- Mapa 3D ----

    void CriarMapa(AngolaTarget t)
    {
        if (raizMapa != null) Destroy(raizMapa);
        raizMapa = null;
        mapaAtual = null;

        raizMapa = new GameObject("Mapa Angola (Pivô)");
        raizMapa.transform.SetParent(palcoMapa, false);
        raizMapa.transform.localPosition = Vector3.zero;
        raizMapa.transform.localRotation = Quaternion.Euler(t.rotacaoInicial);

        if (t.modeloPrefab == null)
        {
            Debug.LogWarning("AngolaTarget: falta atribuir o modelo do mapa (modeloPrefab).");
            return;
        }

        mapaAtual = Instantiate(t.modeloPrefab, raizMapa.transform);
        mapaAtual.transform.localPosition = Vector3.zero;
        mapaAtual.transform.localRotation = Quaternion.identity;
        mapaAtual.transform.localScale = Vector3.one;
        DefinirLayer(mapaAtual, camadaPreview);

        var rends = mapaAtual.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return;

        // Normaliza o tamanho: a maior dimensão fica igual a tamanhoAlvo
        Bounds b = CalcularBounds(rends);
        float maior = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (maior > 0f) mapaAtual.transform.localScale *= t.tamanhoAlvo / maior;

        // Centra o mapa no pivô para girar sobre o próprio centro
        b = CalcularBounds(rends);
        mapaAtual.transform.position += raizMapa.transform.position - b.center;

        PrepararProvincias(mapaAtual);
    }

    void PrepararProvincias(GameObject mapa)
    {
        foreach (var mf in mapa.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!mf.name.StartsWith("Province_")) continue;
            if (mf.sharedMesh == null) continue;

            var colisor = mf.gameObject.GetComponent<MeshCollider>();
            if (colisor == null) colisor = mf.gameObject.AddComponent<MeshCollider>();
            colisor.sharedMesh = mf.sharedMesh;

            var provincia = mf.gameObject.GetComponent<ProvinciaAngola>();
            if (provincia == null) provincia = mf.gameObject.AddComponent<ProvinciaAngola>();
            provincia.nome = NomeProvincia(mf.name);
        }
    }

    static string NomeProvincia(string nomeObjeto)
    {
        string n = nomeObjeto.StartsWith("Province_") ? nomeObjeto.Substring("Province_".Length) : nomeObjeto;
        return n.Replace("_", " ");
    }

    static Bounds CalcularBounds(Renderer[] rends)
    {
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
    }

    static void DefinirLayer(GameObject go, int layer)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }

    // ---- Construção da UI ----

    void GarantirUI()
    {
        if (painel != null) return;

        GarantirPalco();
        fonte = ObterFonte();

        painel = CriarUI("PainelAngola", transform);
        var fundoPainel = painel.AddComponent<Image>();
        fundoPainel.color = new Color(0.04f, 0.05f, 0.08f, 0.96f);
        Esticar(painel.GetComponent<RectTransform>());

        // Coluna de texto (esquerda)
        var conteudo = CriarUI("ConteudoAngola", painel.transform);
        var rtc = conteudo.GetComponent<RectTransform>();
        rtc.anchorMin = new Vector2(0.04f, 0.06f);
        rtc.anchorMax = new Vector2(0.50f, 0.94f);
        rtc.offsetMin = Vector2.zero;
        rtc.offsetMax = Vector2.zero;

        titulo = CriarTexto(conteudo.transform, "Titulo", "Angola", 64, FontStyles.Bold, Color.white);
        var rtTitulo = titulo.rectTransform;
        rtTitulo.anchorMin = new Vector2(0f, 1f);
        rtTitulo.anchorMax = new Vector2(1f, 1f);
        rtTitulo.pivot = new Vector2(0f, 1f);
        rtTitulo.anchoredPosition = Vector2.zero;
        rtTitulo.sizeDelta = new Vector2(0f, 120f);
        titulo.alignment = TextAlignmentOptions.TopLeft;

        var linha = CriarUI("Linha", conteudo.transform);
        var imgLinha = linha.AddComponent<Image>();
        imgLinha.color = new Color(0.85f, 0.55f, 0.20f, 1f);
        imgLinha.raycastTarget = false;
        var rtLinha = linha.GetComponent<RectTransform>();
        rtLinha.anchorMin = new Vector2(0f, 1f);
        rtLinha.anchorMax = new Vector2(0f, 1f);
        rtLinha.pivot = new Vector2(0f, 1f);
        rtLinha.anchoredPosition = new Vector2(0f, -150f);
        rtLinha.sizeDelta = new Vector2(140f, 6f);

        descricao = CriarTexto(conteudo.transform, "Descricao", "", 28, FontStyles.Normal, new Color(0.88f, 0.90f, 0.94f));
        var rtDesc = descricao.rectTransform;
        rtDesc.anchorMin = new Vector2(0f, 1f);
        rtDesc.anchorMax = new Vector2(1f, 1f);
        rtDesc.pivot = new Vector2(0f, 1f);
        rtDesc.anchoredPosition = new Vector2(0f, -195f);
        rtDesc.sizeDelta = new Vector2(0f, 420f);
        descricao.alignment = TextAlignmentOptions.TopLeft;
        descricao.lineSpacing = 8f;

        dica = CriarTexto(conteudo.transform, "Dica", DicaPadrao, 24, FontStyles.Italic, new Color(0.62f, 0.68f, 0.78f));
        var rtDica = dica.rectTransform;
        rtDica.anchorMin = new Vector2(0f, 0f);
        rtDica.anchorMax = new Vector2(1f, 0f);
        rtDica.pivot = new Vector2(0f, 0f);
        rtDica.anchoredPosition = new Vector2(0f, 130f);
        rtDica.sizeDelta = new Vector2(0f, 120f);
        dica.alignment = TextAlignmentOptions.BottomLeft;

        // Botões do painel
        TMP_Text rotulo;
        Button btnGiro = CriarBotao(conteudo.transform, "BtnGiro", "Parar giro", 26, out rotuloGiro);
        var rtGiro = btnGiro.GetComponent<RectTransform>();
        rtGiro.anchorMin = new Vector2(0f, 0f);
        rtGiro.anchorMax = new Vector2(0f, 0f);
        rtGiro.pivot = new Vector2(0f, 0f);
        rtGiro.anchoredPosition = new Vector2(0f, 20f);
        rtGiro.sizeDelta = new Vector2(250f, 70f);
        btnGiro.onClick.AddListener(AlternarGiro);

        Button btnAudio = CriarBotao(conteudo.transform, "BtnAudio", "II", 26, out rotuloAudio);
        var rtAudio = btnAudio.GetComponent<RectTransform>();
        rtAudio.anchorMin = new Vector2(0f, 0f);
        rtAudio.anchorMax = new Vector2(0f, 0f);
        rtAudio.pivot = new Vector2(0f, 0f);
        rtAudio.anchoredPosition = new Vector2(270f, 20f);
        rtAudio.sizeDelta = new Vector2(70f, 70f);
        btnAudio.onClick.AddListener(AlternarAudio);

        Button btnFechar = CriarBotao(painel.transform, "BtnFecharAngola", "X", 30, out rotulo);
        var rtFechar = btnFechar.GetComponent<RectTransform>();
        rtFechar.anchorMin = new Vector2(1f, 1f);
        rtFechar.anchorMax = new Vector2(1f, 1f);
        rtFechar.pivot = new Vector2(1f, 1f);
        rtFechar.anchoredPosition = new Vector2(-30f, -30f);
        rtFechar.sizeDelta = new Vector2(70f, 70f);
        btnFechar.onClick.AddListener(Fechar);

        // Moldura + mapa (direita)
        var moldura = CriarUI("MolduraMapa", painel.transform);
        var imgMoldura = moldura.AddComponent<Image>();
        imgMoldura.color = new Color(0.16f, 0.18f, 0.24f, 1f);
        imgMoldura.raycastTarget = false;
        var rtMoldura = moldura.GetComponent<RectTransform>();
        rtMoldura.anchorMin = new Vector2(1f, 0.5f);
        rtMoldura.anchorMax = new Vector2(1f, 0.5f);
        rtMoldura.pivot = new Vector2(1f, 0.5f);
        rtMoldura.anchoredPosition = new Vector2(-70f, 0f);
        rtMoldura.sizeDelta = new Vector2(860f, 860f);

        var mapaObj = CriarUI("MapaView", painel.transform);
        mapaView = mapaObj.AddComponent<RawImage>();
        mapaView.texture = renderTexture;
        mapaView.raycastTarget = true;
        var rtMapa = mapaObj.GetComponent<RectTransform>();
        rtMapa.anchorMin = new Vector2(1f, 0.5f);
        rtMapa.anchorMax = new Vector2(1f, 0.5f);
        rtMapa.pivot = new Vector2(1f, 0.5f);
        rtMapa.anchoredPosition = new Vector2(-80f, 0f);
        rtMapa.sizeDelta = new Vector2(840f, 840f);

        var clique = mapaObj.AddComponent<AngolaMapClick>();
        clique.cameraPreview = cameraPreview;
        clique.angolaUI = this;

        CriarBarraNav();

        painel.SetActive(false);
    }

    void GarantirPalco()
    {
        if (cameraPreview != null) return;

        var palco = new GameObject("PalcoAngola");
        palco.transform.position = new Vector3(0f, -2000f, 0f);
        palcoMapa = palco.transform;

        var camObj = new GameObject("Camera Preview Angola");
        camObj.transform.SetParent(palco.transform, false);
        camObj.transform.localPosition = new Vector3(0f, 0f, -distanciaCamera);
        cameraPreview = camObj.AddComponent<Camera>();
        cameraPreview.clearFlags = CameraClearFlags.SolidColor;
        cameraPreview.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 1f);
        cameraPreview.fieldOfView = campoVisao;
        cameraPreview.nearClipPlane = 0.1f;
        cameraPreview.farClipPlane = 100f;
        cameraPreview.depth = -10f;
        cameraPreview.allowHDR = false;
        cameraPreview.cullingMask = mascaraPreview;
        cameraPreview.enabled = false;

        renderTexture = new RenderTexture(resolucaoMapa, resolucaoMapa, 24, RenderTextureFormat.ARGB32);
        renderTexture.name = "RT_AngolaMap";
        renderTexture.antiAliasing = 4;
        renderTexture.Create();
        cameraPreview.targetTexture = renderTexture;

        // Luz frontal para o mapa ficar legível
        var luzObj = new GameObject("Luz Mapa Angola");
        luzObj.transform.SetParent(palco.transform, false);
        luzObj.transform.localPosition = new Vector3(0f, 0.8f, -2.2f);
        var luz = luzObj.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.range = 14f;
        luz.intensity = 2.5f;
        luz.color = new Color(1f, 0.96f, 0.9f);
    }

    void CriarBarraNav()
    {
        if (barraNav != null) return;
        if (MonumentUI.Instance == null || MonumentUI.Instance.painel == null) return;

        barraNav = CriarUI("BarraNavegacaoProvincia", MonumentUI.Instance.painel.transform);
        var img = barraNav.AddComponent<Image>();
        img.color = new Color(0.10f, 0.12f, 0.17f, 0.92f);
        img.raycastTarget = true;
        var rt = barraNav.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-110f, -10f);
        rt.sizeDelta = new Vector2(700f, 52f);

        TMP_Text rotulo;
        btnProvAnterior = CriarBotao(barraNav.transform, "BtnProvAnterior", "<", 30, out rotulo);
        PosicionarCentro(btnProvAnterior.GetComponent<RectTransform>(), new Vector2(-310f, 0f), new Vector2(46f, 42f));
        btnProvAnterior.onClick.AddListener(ProvinciaAnterior);

        rotuloNav = CriarTexto(barraNav.transform, "RotuloNav", "", 24, FontStyles.Normal, Color.white);
        PosicionarCentro(rotuloNav.rectTransform, new Vector2(-100f, 0f), new Vector2(300f, 42f));
        rotuloNav.alignment = TextAlignmentOptions.Center;

        btnProvProximo = CriarBotao(barraNav.transform, "BtnProvProximo", ">", 30, out rotulo);
        PosicionarCentro(btnProvProximo.GetComponent<RectTransform>(), new Vector2(120f, 0f), new Vector2(46f, 42f));
        btnProvProximo.onClick.AddListener(ProximaProvincia);

        Button btnVoltar = CriarBotao(barraNav.transform, "BtnVoltarMapa", "Voltar ao mapa", 22, out rotulo);
        PosicionarCentro(btnVoltar.GetComponent<RectTransform>(), new Vector2(250f, 0f), new Vector2(180f, 42f));
        btnVoltar.onClick.AddListener(VoltarAoMapa);

        barraNav.SetActive(false);
    }

    TMP_FontAsset ObterFonte()
    {
        if (MonumentUI.Instance != null && MonumentUI.Instance.titulo != null && MonumentUI.Instance.titulo.font != null)
            return MonumentUI.Instance.titulo.font;
        return TMP_Settings.defaultFontAsset;
    }

    TMP_Text CriarTexto(Transform pai, string nome, string texto, int tamanho, FontStyles estilo, Color cor)
    {
        var go = CriarUI(nome, pai);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = texto;
        tmp.fontSize = tamanho;
        tmp.fontStyle = estilo;
        tmp.color = cor;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;
        if (fonte != null) tmp.font = fonte;
        return tmp;
    }

    Button CriarBotao(Transform pai, string nome, string texto, int tamanhoFonte, out TMP_Text rotulo)
    {
        var go = CriarUI(nome, pai);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.16f, 0.18f, 0.24f, 1f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var cores = btn.colors;
        cores.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        cores.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        btn.colors = cores;

        rotulo = CriarTexto(go.transform, "Rotulo", texto, tamanhoFonte, FontStyles.Normal, Color.white);
        Esticar(rotulo.rectTransform);
        rotulo.alignment = TextAlignmentOptions.Center;
        return btn;
    }

    static GameObject CriarUI(string nome, Transform pai)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(pai, false);
        return go;
    }

    static void Esticar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void PosicionarCentro(RectTransform rt, Vector2 pos, Vector2 tamanho)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = tamanho;
    }
}
